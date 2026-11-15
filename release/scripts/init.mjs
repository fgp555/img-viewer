import {readFile,writeFile,readdir,mkdir} from 'node:fs/promises';
import {resolve,dirname,relative,basename} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {envValues,updateEnv,projectUrls} from './project-settings.mjs';
const moduleDirectory=resolve(dirname(fileURLToPath(import.meta.url)),'..');
const root=resolve(moduleDirectory,'..');
export async function findWpfProjects(folder) {
  const results=[];
  const ignored=new Set(['.git','.vs','bin','obj','node_modules','release','artifacts']);
  async function visit(path) {
    for(const entry of await readdir(path,{withFileTypes:true})) {
      if(entry.isSymbolicLink()) continue;
      const file=resolve(path,entry.name);
      if(entry.isDirectory()&&!ignored.has(entry.name)) await visit(file);
      else if(entry.isFile()&&entry.name.endsWith('.csproj')&&/<UseWPF>\s*true\s*<\/UseWPF>/i.test(await readFile(file,'utf8'))) results.push(file);
    }
  }
  await visit(folder);return results.sort();
}
async function optional(path,fallback='') {try{return await readFile(path,'utf8');}catch(error){if(error.code==='ENOENT')return fallback;throw error;}}
export async function initialize(projectArgument,projectRoot=root,releaseDirectory=moduleDirectory) {
  let project;
  if(projectArgument) project=resolve(projectRoot,projectArgument);
  else {
    const projects=await findWpfProjects(projectRoot);
    if(projects.length===1) project=projects[0];
    else {
      const saved=JSON.parse(await optional(resolve(releaseDirectory,'release.config.json'),'{}'));
      const configured=saved.project?resolve(releaseDirectory,saved.project):null;
      if(configured && projects.includes(configured)) project=configured;
      else throw Error('Found '+projects.length+' WPF projects. Use --project path/to/App.csproj.');
    }
  }
  const xml=await readFile(project,'utf8');
  if(!/<UseWPF>\s*true\s*<\/UseWPF>/i.test(xml)) throw Error('The selected project is not WPF.');
  const name=xml.match(/<AssemblyName>([^<]+)<\/AssemblyName>/)?.[1]?.trim()??basename(project,'.csproj');
  if(name.includes('$')) throw Error('AssemblyName must be a literal value.');
  const envPath=resolve(releaseDirectory,'.env');
  const envText=await optional(envPath);const env=envValues(envText);
  const slug=name.replace(/([a-z0-9])([A-Z])/g,'$1-$2').toLowerCase();
  const base=env.STORAGE_PUBLIC_URL||'https://cdn.frankgp.com/'+slug+'/';
  const urls=projectUrls(name,base);
  const configPath=resolve(releaseDirectory,'release.config.json');
  const previous=JSON.parse(await optional(configPath,'{}'));
  const settings={project:relative(releaseDirectory,project).replaceAll('\\','/'),runtime:previous.runtime??'win-x64',defaultNotes:previous.defaultNotes??'Mejoras y correcciones'};
  const packagePath=resolve(projectRoot,'package.json');
  const pkg=JSON.parse(await optional(packagePath,'{}'));
  const prefix=relative(projectRoot,releaseDirectory).replaceAll('\\','/');
  pkg.private??=true;pkg.scripts={...pkg.scripts,'wpf:init':'node '+prefix+'/scripts/init.mjs','build':'node '+prefix+'/scripts/build.mjs','deploy':'node '+prefix+'/scripts/deploy.mjs','show':'node '+prefix+'/scripts/show.mjs','test:deploy':'node --test '+prefix+'/scripts/*.test.mjs'};
  await mkdir(releaseDirectory,{recursive:true});
  await writeFile(envPath,updateEnv(envText,{PROJECT:name,STORAGE_PUBLIC_URL:base}));
  await writeFile(configPath,JSON.stringify(settings,null,2)+'\n');
  await writeFile(packagePath,JSON.stringify(pkg,null,2)+'\n');
  // Configure the updater only if the project already contains this contract.
  const manifestPath=resolve(dirname(project),'Services/Updates/UpdateManifest.cs');
  const manifest=await optional(manifestPath);
  if(manifest) {
    let updated=manifest.replace(/(public const string FeedUrl = ")[^"]+(";)/,'$1'+urls.publicBaseUrl+'latest.json$2')
      .replace(/(public const string ExecutableUrl = ")[^"]+(";)/,'$1'+urls.publicBaseUrl+urls.executable+'$2');
    if(updated!==manifest) await writeFile(manifestPath,updated);
  }
  return {project:name,publicBaseUrl:urls.publicBaseUrl};
}
if(process.argv[1]&&import.meta.url===pathToFileURL(resolve(process.argv[1])).href) {
  try {
    const args=process.argv.slice(2);
    if(args.length && (args.length!==2||args[0]!=='--project')) throw Error('Usage: npm run wpf:init -- --project path/to/App.csproj');
    const result=await initialize(args[1]);console.log('Configured project: '+result.project);console.log('Downloads: '+result.publicBaseUrl);console.log('Existing credentials and public URL preserved.');
  } catch(error) {console.error(error.code?'Unable to read/write initializer files ('+error.code+').':error.message);process.exitCode=1;}
}
