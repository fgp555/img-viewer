import test from 'node:test';
import assert from 'node:assert/strict';
import {configuration,hash,signPut,publishPair,upload} from './deploy.mjs';
const env={STORAGE_BUCKET_PUBLIC:'test-bucket',STORAGE_ENDPOINT_API:'https://account.r2.cloudflarestorage.com',STORAGE_ACCESS_KEY_ID:'test-access',STORAGE_SECRET_ACCESS_KEY:'test-secret'};
const config=configuration(env);
const exe=Buffer.from('MZtest');
const json=Buffer.from(JSON.stringify({version:'1.0.1',downloadUrl:'https://cdn.frankgp.com/img-viewer/downloads/IMGViewer.exe',sha256:hash(exe),releaseNotes:'Test'}));
test('requires credentials and pinned HTTPS endpoint',()=>{
 assert.throws(()=>configuration({...env,STORAGE_SECRET_ACCESS_KEY:''}));
 assert.throws(()=>configuration({...env,STORAGE_ENDPOINT_API:'https://example.com'}));
});
test('signs payload and cache metadata with R2 scope',()=>{
 const request=signPut(config,'img-viewer/downloads/IMGViewer.exe',exe,'application/octet-stream',new Date('2026-11-15T12:00:00Z'));
 assert.equal(request.url.pathname,'/test-bucket/img-viewer/downloads/IMGViewer.exe');
 assert.equal(request.headers['x-amz-content-sha256'],hash(exe));
 assert.equal(request.headers['x-amz-date'],'20261115T120000Z');
 assert.match(request.headers.authorization,/20261115\/auto\/s3\/aws4_request/);
 assert.equal(request.headers['cache-control'],'no-store, max-age=0');
});
test('publishes executable before manifest',async()=>{
 const calls=[]; await publishPair(config,exe,json,async(_,key)=>calls.push(key));
 assert.deepEqual(calls,['img-viewer/downloads/IMGViewer.exe','img-viewer/downloads/latest.json']);
});
test('never publishes manifest if executable upload fails',async()=>{
 const calls=[]; await assert.rejects(publishPair(config,exe,json,async(_,key)=>{calls.push(key);throw Error('Failed');}));
 assert.deepEqual(calls,['img-viewer/downloads/IMGViewer.exe']);
});
test('rejects changed executable before uploads',async()=>{
 let calls=0;await assert.rejects(publishPair(config,Buffer.from('MZchanged'),json,async()=>calls++));assert.equal(calls,0);
});
test('HTTP failures do not expose response secrets',async()=>{
 await assert.rejects(upload(config,'img-viewer/downloads/latest.json',json,'application/json',async()=>({ok:false,status:403})),/HTTP 403/);
});

import {nextPatch,remoteRelease} from './deploy.mjs';
test('increments patch numerically and rejects invalid versions',()=>{assert.equal(nextPatch('1.0.9'),'1.0.10'); assert.throws(()=>nextPatch('invalid'));assert.throws(()=>nextPatch('1.0.65534'));});
test('remote lookup validates JSON and stops on failures',async()=>{
 const release=await remoteRelease(async()=>new Response(json));assert.equal(release.version,'1.0.1');
 assert.equal(await remoteRelease(async()=>new Response('missing',{status:404})),null);
 await assert.rejects(remoteRelease(async()=>new Response('denied',{status:403})));
 await assert.rejects(remoteRelease(async()=>new Response('unavailable',{status:500})));
 await assert.rejects(remoteRelease(async()=>new Response('invalid')));
 await assert.rejects(remoteRelease(async()=>new Response('a'.repeat(17000))));
});

import {validateReleaseConfig,releaseConfig} from './config.mjs';
test('module can publish another project using configuration only',async()=>{
 const other=validateReleaseConfig({...releaseConfig,project:'../Other/Other.csproj',executable:'Other.exe',publicBaseUrl:'https://cdn.example.com/other/downloads/',storagePrefix:'other/downloads'});
 const manifest=Buffer.from(JSON.stringify({version:'1.2.3',downloadUrl:other.publicBaseUrl+other.executable,sha256:hash(exe),releaseNotes:'Test'}));
 const calls=[];await publishPair(config,exe,manifest,async(_,key)=>calls.push(key),other);
 assert.deepEqual(calls,['other/downloads/Other.exe','other/downloads/latest.json']);
});
import {firstReleaseVersion,assertUnchangedRelease} from './deploy.mjs';
test('first publication keeps the project version without incrementing',()=>{
 assert.equal(firstReleaseVersion('<Project><Version>1.0.1</Version></Project>'),'1.0.1');
 assert.throws(()=>firstReleaseVersion('<Project/>'));
 assert.throws(()=>firstReleaseVersion('<Version>bad</Version>'));
});
test('pre-upload recheck detects another first deployment or a missing release',()=>{
 assert.doesNotThrow(()=>assertUnchangedRelease(null,null));
 const release={version:'1.0.1',sha256:'hash'};
 assert.doesNotThrow(()=>assertUnchangedRelease(release,{...release}));
 assert.throws(()=>assertUnchangedRelease(null,release));
 assert.throws(()=>assertUnchangedRelease(release,null));
 assert.throws(()=>assertUnchangedRelease(release,{...release,sha256:'changed'}));
});

test('404 checks only the configured URL, without legacy lookup',async()=>{
 const urls=[];assert.equal(await remoteRelease(async url=>{urls.push(url);return new Response('',{status:404});}),null);
 assert.equal(urls.length,1);assert.ok(urls[0].startsWith(releaseConfig.publicBaseUrl+'latest.json?'));
});
