import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,mkdir,writeFile,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {initialize,findWpfProjects} from './init.mjs';
import {projectUrls,updateEnv,envValues} from './project-settings.mjs';
test('derives names and downloads from environment',()=>{
 assert.deepEqual(projectUrls('Example','https://cdn.example.com/apps/example/'),{executable:'Example.exe',publicBaseUrl:'https://cdn.example.com/apps/example/downloads/',storagePrefix:'apps/example/downloads'});
 assert.throws(()=>projectUrls('../bad','https://cdn.example.com/app/'));
});
test('updates only selected environment entries',()=>{
 const text='# Comment\nSECRET="keep me"\nPROJECT=Old\n';const result=updateEnv(text,{PROJECT:'New'});
 assert.ok(result.includes('SECRET="keep me"'));assert.equal(envValues(result).PROJECT,'New');assert.ok(result.includes('# Comment'));
});
test('initializer detects WPF, preserves credentials/scripts/url and is idempotent',async()=>{
 const root=await mkdtemp(join(tmpdir(),'wpf-init-'));try{
 const release=join(root,'release');await mkdir(release);await mkdir(join(root,'App'));
 await writeFile(join(root,'App/App.csproj'),'<Project><UseWPF>true</UseWPF><AssemblyName>CustomApp</AssemblyName><Version>1.0.1</Version></Project>');
 await writeFile(join(release,'.env'),'STORAGE_ACCESS_KEY_ID=test-value\nSTORAGE_PUBLIC_URL=https://cdn.example.com/custom/\n');
 await writeFile(join(root,'package.json'),JSON.stringify({scripts:{custom:'echo test'}}));
 await initialize(undefined,root,release);const before=await readFile(join(release,'.env'),'utf8');
 await initialize(undefined,root,release);assert.equal(await readFile(join(release,'.env'),'utf8'),before);
 assert.equal(envValues(before).PROJECT,'CustomApp');assert.equal(envValues(before).STORAGE_ACCESS_KEY_ID,'test-value');assert.equal(envValues(before).STORAGE_PUBLIC_URL,'https://cdn.example.com/custom/');
 assert.equal(JSON.parse(await readFile(join(root,'package.json'))).scripts.custom,'echo test');
 await mkdir(join(root,'Second'));await writeFile(join(root,'Second/Other.csproj'),'<Project><UseWPF>true</UseWPF></Project>');
 assert.equal((await findWpfProjects(root)).length,2);
 await initialize(undefined,root,release);
 await rm(join(release,'release.config.json'));await assert.rejects(initialize(undefined,root,release));
 await initialize('Second/Other.csproj',root,release);assert.equal(envValues(await readFile(join(release,'.env'),'utf8')).PROJECT,'Other');
 }finally{await rm(root,{recursive:true,force:true});}
});
