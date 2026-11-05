'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{execFileSync}=require('node:child_process');
const repo=path.resolve(__dirname,'../..'),work=path.join(repo,'artifacts/weight-size-r1'),source=path.join(__dirname,'payload');
const stage=path.join(work,'packages/JiggleForge-manual-v0.1.26-beta.3'),zip=stage+'.zip';
const read=p=>fs.readFileSync(p,'utf8'),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function files(root){return fs.readdirSync(root,{withFileTypes:true}).flatMap(e=>{assert(!e.isSymbolicLink(),'No linked payload');const p=path.join(root,e.name);return e.isDirectory()?files(p):[p];});}
assert(!fs.existsSync(stage)&&!fs.existsSync(zip),'Use a new revision rather than changing a packaged test');
const compile=JSON.parse(read(path.join(work,'validation/compile.json'))),gpu=JSON.parse(read(path.join(work,'validation/gpu.json'))),numeric=JSON.parse(read(path.join(work,'validation/numeric.json')));
assert(compile.pass&&compile.demandReplay.pass&&gpu.warp.pass&&gpu.hardware.pass&&numeric.pass,'All validation gates must pass');
assert(compile.runtimeFiles?.length>50,'Verification must fingerprint the tested runtime');
for(const f of compile.runtimeFiles)assert.equal(hash(path.join(source,f.path)),f.sha256,'Runtime changed after verification: '+f.path);
const manifest=[];
for(const p of [path.join(source,'JiggleForge.ini'),...files(path.join(source,'JiggleForge')),...files(path.join(source,'ShaderFixes'))]){
 assert(['.ini','.hlsl','.txt'].includes(path.extname(p)),'Unexpected runtime extension');
 const relative=path.relative(source,p).replaceAll('\\','/');
 const rel=relative.startsWith('ShaderFixes/')?relative:'Mods/JiggleForgeShaderFix/'+relative;
 const target=path.join(stage,rel);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(p,target);
 assert.equal(hash(target),hash(p));manifest.push({path:rel,sha256:hash(p),bytes:fs.statSync(p).size});
}
for(const name of ['INSTALL-zh-CN.md','INSTALL-en.md','README.md'])fs.copyFileSync(path.join(__dirname,name),path.join(stage,name));
for(const name of ['LICENSE','THIRD-PARTY-NOTICES.md','ACKNOWLEDGEMENTS.md','BRANDING.md'])fs.copyFileSync(path.join(repo,name),path.join(stage,name));
fs.writeFileSync(path.join(stage,'manifest.json'),JSON.stringify({package:'JiggleForge-manual-v0.1.26-beta.3',runtimeRevision:'WeightSize-r1',type:'full-manual-test-runtime',gameInstalled:false,referenceSize:.03,sizeCeiling:.04,baseMaxOffset:.05,runtimeFiles:manifest},null,2));
// Deliberately exclude workstation paths from the redistributable report.
fs.writeFileSync(path.join(stage,'VALIDATION.json'),JSON.stringify({compilePassed:compile.pass,shaderCount:compile.shaders.length,vsCount:compile.vs.length,demandReplayPassed:compile.demandReplay.pass,gpu,numeric,gameTested:false},null,2));
fs.writeFileSync(path.join(stage,'SHADER-HASHES.txt'),compile.vs.map(v=>v.file.slice(0,16)).join('\r\n')+'\r\n');
let references=0;
function reference(owner,name){const p=path.resolve(path.dirname(owner),name.replaceAll('\\','/'));assert(p.startsWith(stage+path.sep)&&fs.existsSync(p),'Missing/outside dependency '+name);references++;}
for(const file of files(stage)){
 assert(!/\.(exe|dll|com|scr|msi|bat|cmd|ps1|js|bin|buf)$/i.test(file),'Do not bundle executables, caches or game meshes');
 const text=read(file);assert(!/[A-Z]:\\(?:Users|Program Files)|C:\/Users\//i.test(text),'Workstation path leaked: '+file);
 for(const m of text.matchAll(/^\s*#include\s+"([^"]+)"/gm))reference(file,m[1]);
}
const iniPath=path.join(stage,'Mods/JiggleForgeShaderFix/JiggleForge.ini');
for(const m of read(iniPath).matchAll(/^\s*(?:vs|ps|gs|cs|hs|ds|filename|include)\s*=\s*([^\r\n;]+)/gm))if(m[1].trim()!=='null')reference(iniPath,m[1].trim());
// Existing verified release tooling; creates archive only under workspace.
const quote=s=>"'"+s.replaceAll("'","''")+"'";
execFileSync('powershell.exe',['-NoProfile','-Command','$ErrorActionPreference="Stop"; Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory('+quote(stage)+','+quote(zip)+',[IO.Compression.CompressionLevel]::Optimal,$false)'],{stdio:'pipe'});
fs.writeFileSync(zip+'.sha256',hash(zip)+'  '+path.basename(zip)+'\r\n');
const report={zip,bytes:fs.statSync(zip).size,sha256:hash(zip),runtimeFiles:manifest.length,references,gameInstalled:false};
fs.writeFileSync(path.join(work,'package-report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report));
