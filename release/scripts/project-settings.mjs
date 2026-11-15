import {parseEnv} from 'node:util';
export function envValues(text) { return parseEnv(text); }
export function updateEnv(text,changes) {
  let output=text;
  for(const [key,value] of Object.entries(changes)) {
    const pattern=new RegExp('^\\s*(?:export\\s+)?'+key+'\\s*=.*$','m');
    const line=key+'='+value;
    output=pattern.test(output)?output.replace(pattern,()=>line):output+(output&&!output.endsWith('\n')?'\n':'')+line+'\n';
  }
  return output;
}
export function projectUrls(name,base) {
  if(!/^[A-Za-z0-9._-]+$/.test(name??'')) throw Error('PROJECT must be a valid executable name without extension.');
  const url=new URL(base);
  if(url.protocol!=='https:'||url.search||url.hash||url.username||url.password) throw Error('STORAGE_PUBLIC_URL must be an HTTPS base URL.');
  url.pathname=url.pathname.replace(/\/+$/,'')+'/';
  const prefix=decodeURIComponent(url.pathname).replace(/^\/+|\/+$/g,'');
  if(!/^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*$/.test(prefix)) throw Error('STORAGE_PUBLIC_URL must include the project path.');
  return {executable:name+'.exe',publicBaseUrl:new URL('downloads/',url).href,storagePrefix:prefix+'/downloads'};
}
