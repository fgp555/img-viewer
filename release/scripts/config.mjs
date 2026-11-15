import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {envValues,projectUrls} from './project-settings.mjs';
export const moduleDirectory=resolve(dirname(fileURLToPath(import.meta.url)),'..');
export const root=resolve(moduleDirectory,'..');
export function validateReleaseConfig(value) {
  if(!value || typeof value.project!=='string' || !value.project || !/^[a-z0-9-]+$/.test(value.runtime??'')) throw Error('Invalid project/runtime in release.config.json.');
  if(!/^[a-zA-Z0-9._-]+\.exe$/.test(value.executable??'')) throw Error('Invalid executable filename.');
  const base=new URL(value.publicBaseUrl);
  if(base.protocol!=='https:'||base.search||base.hash||base.username||base.password||!base.pathname.endsWith('/')) throw Error('publicBaseUrl must be HTTPS and end in /.');
  if(!/^[a-zA-Z0-9_-]+(?:\/[a-zA-Z0-9_-]+)*$/.test(value.storagePrefix??'')) throw Error('Invalid storagePrefix.');
  if(typeof value.defaultNotes!=='string'||!value.defaultNotes.trim()||value.defaultNotes.length>4000) throw Error('Invalid defaultNotes.');
  return Object.freeze({...value});
}
const settings=JSON.parse(readFileSync(resolve(moduleDirectory,'release.config.json'),'utf8'));
let local={};try {local=envValues(readFileSync(resolve(moduleDirectory,'.env'),'utf8'));} catch(error) {if(error.code!=='ENOENT') throw error;}
const project=local.PROJECT??process.env.PROJECT;
const publicUrl=local.STORAGE_PUBLIC_URL??process.env.STORAGE_PUBLIC_URL;
if(!project||!publicUrl) throw Error('Run npm run wpf:init and configure PROJECT/STORAGE_PUBLIC_URL in release/.env.');
export const releaseConfig=validateReleaseConfig({...settings,...projectUrls(project,publicUrl)});
export const outputDirectory=resolve(moduleDirectory,'output');
export const projectPath=resolve(moduleDirectory,releaseConfig.project);
