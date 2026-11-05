'use strict';
// Verifies a frozen runtime inside a full application package. No game writes.
// node validate-release.cjs --package DIR --payload FROZEN_RUNTIME --manifest MANIFEST_JSON
//   --zip ZIP --version 0.1.26-beta.3 --output VALIDATION_DIR
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{execFileSync}=require('node:child_process');
const args=process.argv.slice(2),options={};
for(let i=0;i<args.length;i+=2){assert(args[i].startsWith('--')&&args[i+1],'Use --name value pairs');assert(!options[args[i]],'Duplicate option');options[args[i]]=args[i+1];}
for(const name of ['package','payload','manifest','zip','version','output'])assert(options['--'+name],'Missing --'+name);
const packageRoot=path.resolve(options['--package']),payload=path.resolve(options['--payload']),manifestPath=path.resolve(options['--manifest']),zip=path.resolve(options['--zip']),output=path.resolve(options['--output']),version=options['--version'];
assert(/^\d+\.\d+\.\d+-beta\.\d+$/.test(version),'Expected a beta version');
assert(!output.startsWith(packageRoot+path.sep)&&output!==packageRoot,'Validation output must not alter packaged files');
fs.mkdirSync(output,{recursive:true});
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''),sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function files(root){return fs.readdirSync(root,{withFileTypes:true}).flatMap(e=>{assert(!e.isSymbolicLink(),'Unexpected symlink');const p=path.join(root,e.name);return e.isDirectory()?files(p):[p];});}
function confined(root,rel){assert(!path.isAbsolute(rel));const p=path.resolve(root,rel);assert(p.startsWith(root+path.sep),'Path outside package');return p;}
const manifest=JSON.parse(read(manifestPath));assert(Array.isArray(manifest.runtimeFiles),'Expected runtimeFiles manifest');assert.equal(manifest.runtimeFiles.length,51,'WeightSize r1 has 51 frozen runtime files');
const expected=[];
for(const entry of manifest.runtimeFiles){const rel=entry.path.replace(/^Mods\/JiggleForgeShaderFix\//,'').replaceAll('\\','/');assert(/\.(ini|hlsl|txt)$/.test(rel),'Unexpected frozen file');expected.push(rel);assert.equal(sha(confined(payload,rel)),entry.sha256.toLowerCase(),'Frozen input changed: '+rel);assert.equal(sha(confined(path.join(packageRoot,'Runtime'),rel)),entry.sha256.toLowerCase(),'Packaged runtime differs: '+rel);}
const allRuntime=files(path.join(packageRoot,'Runtime')).map(p=>path.relative(path.join(packageRoot,'Runtime'),p).replaceAll('\\','/')).sort();
assert.deepEqual(allRuntime,[...expected,'JiggleForge/WheelBridge.exe'].sort(),'Unexpected runtime payload (only the wheel helper may be added)');
for(const name of ['JiggleForge.exe','App/JiggleForge.exe','App/JiggleForge.dll','App/JiggleForge.Core.dll','App/JiggleForge.Updater.exe','App/hostfxr.dll','App/coreclr.dll','App/Microsoft.WindowsAppRuntime.dll','App/Microsoft.UI.Xaml.dll','App/DWriteCore.dll','LICENSE','TEST-BUILD.zh-CN.md','TEST-BUILD.en.md'])assert(fs.statSync(confined(packageRoot,name)).size>0,'Missing application dependency '+name);
const config=JSON.parse(read(path.join(packageRoot,'App/JiggleForge.runtimeconfig.json'))).runtimeOptions;
assert(config.includedFrameworks?.some(f=>f.name==='Microsoft.NETCore.App'),'Not a self-contained .NET application');
assert(!config.framework&&!config.frameworks,'Unexpected framework-dependent app');
const quote=s=>"'"+s.replaceAll("'","''")+"'";
const metadata=JSON.parse(execFileSync('powershell.exe',['-NoProfile','-Command',
  '$ErrorActionPreference="Stop"; $v=[Diagnostics.FileVersionInfo]::GetVersionInfo('+quote(path.join(packageRoot,'App/JiggleForge.dll'))+'); @{ ProductVersion=$v.ProductVersion; FileVersion=$v.FileVersion } | ConvertTo-Json -Compress'],{encoding:'utf8',windowsHide:true}));
assert(metadata.ProductVersion===version||metadata.ProductVersion.startsWith(version+'+'),'Wrong app ProductVersion: '+metadata.ProductVersion);
assert.equal(metadata.FileVersion,version.split('-')[0]+'.0');
const integrity=read(path.join(packageRoot,'JiggleForge.manifest.sha256')).trim().split(/\r?\n/),listed=new Set();
for(const line of integrity){const m=line.match(/^([0-9a-f]{64})\s+\*?(.+)$/i);assert(m,'Invalid integrity line');const name=m[2].replaceAll('\\','/');assert(!listed.has(name.toLowerCase()),'Duplicate integrity entry');listed.add(name.toLowerCase());assert.equal(sha(confined(packageRoot,name)),m[1].toLowerCase(),'Integrity mismatch '+name);}
const allPackage=files(packageRoot).map(p=>path.relative(packageRoot,p).replaceAll('\\','/').toLowerCase()).filter(p=>p!=='jiggleforge.manifest.sha256');assert.equal(listed.size,allPackage.length,'Integrity manifest count differs');assert(allPackage.every(p=>listed.has(p)),'Unlisted package files');
const checksum=read(zip+'.sha256').match(/^([a-f0-9]{64})\s+\*?(.+)\s*$/i);assert(checksum,'Invalid ZIP checksum file');assert.equal(checksum[1].toLowerCase(),sha(zip));assert.equal(checksum[2].trim(),path.basename(zip));
const project=path.join(__dirname,'release-smoke/ReleaseSmoke.csproj');
const build=execFileSync('dotnet',['build',project,'-c','Release','--artifacts-path',path.join(output,'build'),'-p:NuGetAudit=false','-p:PackagedCorePath='+path.join(packageRoot,'App/JiggleForge.Core.dll')],{encoding:'utf8',windowsHide:true});
fs.writeFileSync(path.join(output,'smoke-build.txt'),build);
execFileSync('dotnet',[path.join(output,'build/bin/ReleaseSmoke/release/ReleaseSmoke.dll'),packageRoot,zip,output,version],{stdio:'inherit',windowsHide:true});
const smoke=JSON.parse(read(path.join(output,'installer-smoke.json')));assert(smoke.pass);
const result={pass:true,version,frozenRuntimeFiles:expected.length,packageFiles:allPackage.length+1,sha256:sha(zip),bytes:fs.statSync(zip).size,metadata,installerSmoke:smoke,gameTested:false};
fs.writeFileSync(path.join(output,'release-validation.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
