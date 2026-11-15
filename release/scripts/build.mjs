import {execFileSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {createReadStream,readFileSync,mkdirSync,writeFileSync,copyFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {releaseConfig,root,projectPath,outputDirectory} from './config.mjs';
export async function localBuild() {
  const version=readFileSync(projectPath,'utf8').match(/<Version>([^<]+)<\/Version>/)?.[1];
  await buildRelease(version);
  console.log('Distribution ready for local testing. No version increment or upload.');
}
export async function buildRelease(version,notes=releaseConfig.defaultNotes) {
  if(!/^\d+\.\d+\.\d+$/.test(version??'')||version.split('.').some(value=>Number(value)>65534)) throw Error('Invalid release version.');
  if(!notes.trim()||notes.length>4000) throw Error('Invalid release notes.');
  const assembly=readFileSync(projectPath,'utf8').match(/<AssemblyName>([^<]+)<\/AssemblyName>/)?.[1] ?? projectPath.split(/[\\/]/).at(-1).replace(/\.csproj$/,'');
  const build=resolve(outputDirectory,'.build'); mkdirSync(build,{recursive:true});
  execFileSync('dotnet',['publish',projectPath,'-c','Release','-r',releaseConfig.runtime,'--self-contained','false','-p:PublishSingleFile=true','-p:Version='+version,'-o',build,'--nologo'],{cwd:root,stdio:'inherit'});
  const executable=resolve(outputDirectory,releaseConfig.executable); copyFileSync(resolve(build,assembly+'.exe'),executable);
  const hash=createHash('sha256');for await(const chunk of createReadStream(executable)) hash.update(chunk);
  const manifest={version,downloadUrl:releaseConfig.publicBaseUrl+releaseConfig.executable,sha256:hash.digest('hex').toUpperCase(),releaseNotes:notes};
  writeFileSync(resolve(outputDirectory,'latest.json'),JSON.stringify(manifest,null,2)+'\n');
  console.log('Release prepared: '+executable);
}
if(process.argv[1] && import.meta.url===pathToFileURL(resolve(process.argv[1])).href) {
  try {
    if(process.argv.slice(2).length) throw Error('build is local only; use npm run deploy to publish.');
    await localBuild();
  } catch(error) {console.error(error.message);process.exitCode=1;}
}
