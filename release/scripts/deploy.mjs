import { createHash, createHmac } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import { loadEnvFile } from 'node:process';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import {releaseConfig,moduleDirectory,outputDirectory,projectPath} from './config.mjs';
import {buildRelease} from './build.mjs';
export const hash = body => createHash('sha256').update(body).digest('hex');
const hmac=(key,value)=>createHmac('sha256',key).update(value).digest();
const encode=value=>encodeURIComponent(value).replace(/[!'()*]/g,char=>'%'+char.charCodeAt(0).toString(16).toUpperCase());
export function configuration(env) {
  const required=['STORAGE_BUCKET_PUBLIC','STORAGE_ENDPOINT_API','STORAGE_ACCESS_KEY_ID','STORAGE_SECRET_ACCESS_KEY'];
  for(const name of required) if(!env[name]?.trim()) throw Error('Falta '+name+' en .env.');
  const endpoint=new URL(env.STORAGE_ENDPOINT_API);
  if(endpoint.protocol!=='https:' || endpoint.username || endpoint.password || endpoint.search || endpoint.hash ||
    !endpoint.hostname.endsWith('.r2.cloudflarestorage.com')) throw Error('STORAGE_ENDPOINT_API debe ser el endpoint HTTPS S3 de Cloudflare R2.');
  if(endpoint.pathname!=='/' && endpoint.pathname!=='') throw Error('El endpoint R2 debe estar sin bucket ni carpeta.');
  const bucket=env.STORAGE_BUCKET_PUBLIC.trim();
  if(!/^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$/.test(bucket)) throw Error('Invalid bucket name.');
  return {endpoint,bucket,access:env.STORAGE_ACCESS_KEY_ID.trim(),secret:env.STORAGE_SECRET_ACCESS_KEY.trim()};
}
export function signPut(config,key,body,type,date=new Date()) {
  const url=new URL(config.endpoint);
  url.pathname='/'+[config.bucket,...key.split('/')].map(encode).join('/');
  const timestamp=date.toISOString().replace(/[:-]|\.\d{3}/g,'');
  const day=timestamp.slice(0,8), scope=day+'/auto/s3/aws4_request';
  const headers={'cache-control':'no-store, max-age=0','content-type':type,host:url.host,'x-amz-content-sha256':hash(body),'x-amz-date':timestamp};
  const names=Object.keys(headers).sort();
  const canonical=['PUT',url.pathname,'',names.map(name=>name+':'+headers[name].trim()+'\n').join(''),names.join(';'),hash(body)].join('\n');
  const signing=hmac(hmac(hmac(hmac('AWS4'+config.secret,day),'auto'),'s3'),'aws4_request');
  const signature=createHmac('sha256',signing).update('AWS4-HMAC-SHA256\n'+timestamp+'\n'+scope+'\n'+hash(canonical)).digest('hex');
  headers.authorization='AWS4-HMAC-SHA256 Credential='+config.access+'/'+scope+', SignedHeaders='+names.join(';')+', Signature='+signature;
  delete headers.host;
  return {url,headers};
}
export async function upload(config,key,body,type,request=fetch) {
  const {url,headers}=signPut(config,key,body,type);
  const response=await request(url,{method:'PUT',headers,body,redirect:'error',signal:AbortSignal.timeout(120000)});
  if(!response.ok) throw Error('R2 upload failed: '+key+' (HTTP '+response.status+').');
  await response.body?.cancel();
}
export function validateArtifacts(executable,manifest,settings=releaseConfig) {
  if(!/^\d+\.\d+\.\d+$/.test(manifest.version??'') || manifest.downloadUrl!==settings.publicBaseUrl+settings.executable) throw Error('Invalid release manifest.');
  if(executable[0]!==0x4d || executable[1]!==0x5a || hash(executable).toUpperCase()!==manifest.sha256?.toUpperCase()) throw Error('El EXE no coincide con latest.json. Vuelve a ejecutar npm run deploy.');
  if(typeof manifest.releaseNotes!=='string' || manifest.releaseNotes.length>4000) throw Error('Invalid release notes.');
}
export async function publishPair(config,executable,json,send=upload,settings=releaseConfig) {
  validateArtifacts(executable,JSON.parse(json.toString('utf8')),settings);
  await send(config,settings.storagePrefix+'/'+settings.executable,executable,'application/octet-stream');
  await send(config,settings.storagePrefix+'/latest.json',json,'application/json; charset=utf-8');
}
export function nextPatch(version) {
  if(!/^\d+\.\d+\.\d+$/.test(version??'')) throw Error('Version remota no valida.');
  const parts=version.split('.').map(Number);
  if(parts.some(value=>!Number.isSafeInteger(value)||value<0||value>65534) || parts[2]>=65534) throw Error('Version fuera de rango.');
  parts[2]++; return parts.join('.');
}
export async function remoteRelease(request=fetch,settings=releaseConfig) {
  const source=settings.publicBaseUrl+'latest.json';
  const response=await request(source+'?check='+Date.now(),{cache:'no-store',redirect:'error',signal:AbortSignal.timeout(15000)});
  const expectedUrl=settings.publicBaseUrl+settings.executable;
  if(response.status===404) { await response.body?.cancel(); return null; }
  if(!response.ok) throw Error('No se pudo leer latest.json remoto (HTTP '+response.status+'). No se publicara.');
  const reader=response.body.getReader(); let total=0; const chunks=[];
  try { for(;;) { const {done,value}=await reader.read(); if(done) break; total+=value.length; if(total>16384) throw Error('Manifiesto remoto demasiado grande.'); chunks.push(value); } }
  finally { await reader.cancel(); }
  let manifest; try { manifest=JSON.parse(Buffer.concat(chunks).toString('utf8')); } catch { throw Error('latest.json remoto no es JSON valido.'); }
  nextPatch(manifest.version);
  if(manifest.downloadUrl!==expectedUrl||!/^([a-f0-9]{64})$/i.test(manifest.sha256??'')||typeof manifest.releaseNotes!=='string') throw Error('Manifiesto remoto no valido.');
  return manifest;
}
export function firstReleaseVersion(projectXml) {
  const version=projectXml.match(/<Version>([^<]+)<\/Version>/)?.[1]?.trim();
  nextPatch(version); // Validate without incrementing the local initial version.
  return version;
}
export function assertUnchangedRelease(before,after) {
  if((before===null)!==(after===null) || (before && (before.version!==after.version||before.sha256!==after.sha256)))
    throw Error('La version remota cambio durante el build. Vuelve a ejecutar deploy.');
}
async function main() {
  const args=process.argv.slice(2); let dry=false,notes=releaseConfig.defaultNotes;
  for(let i=0;i<args.length;i++) {
    if(args[i]==='--dry-run') dry=true;
    else if(args[i]==='--notes' && args[i+1]) notes=args[++i];
    else throw Error('Opciones: --dry-run y --notes "Texto".');
  }
  if(!notes.trim()||notes.length>4000) throw Error('Notas no validas.');
  try { loadEnvFile(resolve(moduleDirectory,'.env')); } catch(error) { if(error.code!=='ENOENT') throw Error('No se pudo leer .env.'); }
  const config=configuration(process.env);
  const current=await remoteRelease();
  let version;
  if(current) {
    version=nextPatch(current.version);
    console.log('Version publicada: '+current.version+' -> siguiente: '+version);
  } else {
    const project=await readFile(projectPath,'utf8');
    version=firstReleaseVersion(project);
    console.log('Primera publicacion detectada. Se publicara '+version+'.');
  }
  if(dry) { console.log('Configuracion valida. No se compilo ni se subieron archivos.'); return; }
  await buildRelease(version,notes);
  const folder=outputDirectory;
  const [executable,json]=await Promise.all([readFile(resolve(folder,releaseConfig.executable)),readFile(resolve(folder,'latest.json'))]);
  const manifest=JSON.parse(json.toString('utf8')); validateArtifacts(executable,manifest);
  if(manifest.version!==version) throw Error('La version compilada no coincide con la esperada.');
  // Recheck after building, so a concurrent deployment cannot silently be overwritten.
  const rechecked=await remoteRelease();
  assertUnchangedRelease(current,rechecked);
  await publishPair(config,executable,json,async (...items)=>{ await upload(...items); console.log('Subido: '+items[1]); });
  console.log('Publicacion completa: '+version);
}
if(process.argv[1] && import.meta.url===pathToFileURL(resolve(process.argv[1])).href)
  main().catch(error=>{
    // Do not print request objects or environment values.
    const message = error instanceof TypeError ? 'No se pudo conectar con R2. Comprueba la red y el endpoint.' : error.message;
    console.error(message); process.exitCode=1;
  });
