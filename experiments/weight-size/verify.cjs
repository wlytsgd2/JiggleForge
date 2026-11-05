'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{execFileSync}=require('node:child_process');
const args=process.argv.slice(2);assert(args.length===0||(args.length===2&&args[0]==='--runtime'),'Use --runtime DIR to validate a rebuilt payload instead');
const repo=path.resolve(__dirname,'../..'),work=path.join(repo,'artifacts/weight-size-r1'),root=path.resolve(args[1]||path.join(__dirname,'payload')),base=path.join(work,'base/JiggleForge-SharedWeight-Test-r11-options'),out=path.join(work,'validation');
function findFxc(){
 if(process.env.FXC){assert(fs.existsSync(process.env.FXC),'FXC override not found');return process.env.FXC;}
 try{return execFileSync('where.exe',['fxc.exe'],{encoding:'utf8',windowsHide:true}).trim().split(/\r?\n/)[0];}catch{}
 const sdk=path.join(process.env['ProgramFiles(x86)']||'C:/Program Files (x86)','Windows Kits/10/bin');
 if(fs.existsSync(sdk)){const versions=fs.readdirSync(sdk).filter(n=>/^\d+\.\d+\.\d+\.\d+$/.test(n)).sort((a,b)=>b.localeCompare(a,undefined,{numeric:true}));for(const v of versions){const exe=path.join(sdk,v,'x64/fxc.exe');if(fs.existsSync(exe))return exe;}}
 throw Error('Install the Windows SDK shader compiler, or set FXC to the absolute fxc.exe path.');
}
const fxc=findFxc();fs.mkdirSync(out,{recursive:true});
if(!fs.existsSync(path.join(base,'JiggleForge.ini')))execFileSync(process.execPath,[path.join(repo,'experiments/binding-weight/build.cjs')],{env:{...process.env,JIGGLEFORGE_EXPERIMENT_OUTPUT:path.join(work,'base')},stdio:'inherit'});
const read=p=>fs.readFileSync(p,'utf8').replace(/\r/g,''),sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function files(p){return fs.readdirSync(p,{withFileTypes:true}).flatMap(e=>{assert(!e.isSymbolicLink());return e.isDirectory()?files(path.join(p,e.name)):[path.join(p,e.name)];});}
function compile(file,stage,name,includes=root){try{execFileSync(fxc,['/nologo','/D',({vs:'VERTEX_SHADER',gs:'GEOMETRY_SHADER',ps:'PIXEL_SHADER',cs:'COMPUTE_SHADER'})[stage]+'=1','/T',stage+'_5_0','/E','main','/I',path.join(includes,'JiggleForge/runtime'),'/I',path.join(includes,'ShaderFixes/JiggleForgeRuntime'),'/Fo',path.join(out,name+'.cso'),'/Fc',path.join(out,name+'.asm'),file],{stdio:'pipe',timeout:120000,windowsHide:true});}catch(e){throw Error(String(e.stdout)+'\n'+String(e.stderr));}}
function signature(s,stage){const part=s.split('// '+stage+' signature:')[1].split(stage==='Input'?'// Output signature:':'vs_5_0')[0];return[...part.matchAll(/^\/\/\s+(\w+)\s+(\d+)\s+(\w+)\s+(\d+)\s+(\w+)\s+(\w+)/gm)].map(m=>m.slice(1));}
const report={pass:true,revision:'WeightSize-r1',shaders:[],vs:[],gameTested:false};
for(const f of fs.readdirSync(path.join(root,'ShaderFixes')).filter(n=>n.endsWith('-vs_replace.txt')).sort()){
 compile(path.join(root,'ShaderFixes',f),'vs',f);compile(path.join(base,'ShaderFixes',f),'vs','baseline-'+f,base);
 const s=read(path.join(out,f+'.asm')),old=read(path.join(out,'baseline-'+f+'.asm'));for(const type of ['Input','Output'])assert.deepEqual(signature(s,type),signature(old,type),f+' '+type+' register signature');
 report.vs.push({file:f,instructions:Number(s.match(/Approximately (\d+) instruction/)?.[1]),baselineInstructions:Number(old.match(/Approximately (\d+) instruction/)?.[1]),temps:Number(s.match(/dcl_temps (\d+)/)?.[1]),inputAndOutputRegistersPreserved:true});
}
const ini=read(path.join(root,'JiggleForge.ini')),oldIni=read(path.join(base,'JiggleForge.ini'));
for(const m of ini.matchAll(/^(vs|ps|gs|cs) = (?:\.\/)?(JiggleForge\/[^\r\n]+\.hlsl)/gm)){const key=m[1]+':'+m[2];if(report.shaders.includes(key))continue;compile(path.join(root,m[2]),m[1],m[1]+'-'+path.basename(m[2],'.hlsl'));report.shaders.push(key);}
assert.equal(report.vs.length,12);assert.equal(report.shaders.length,24);
assert.equal(read(path.join(root,'JiggleForge/runtime/deformation_field.hlsl')),read(path.join(base,'JiggleForge/runtime/deformation_field.hlsl')),'Original v26 spatial formula unchanged');
const baseMotion=read(path.join(base,'JiggleForge/runtime/motion_model.hlsl')),motion=read(path.join(root,'JiggleForge/runtime/motion_model.hlsl'));
assert.equal(motion,baseMotion.replace('result.MaxOffset = JF_FiniteOr(p1.w, 0.0f);','result.MaxOffset = min(JF_FiniteOr(p1.w, 0.0f), 0.05f); // WeightSize r1 reference-scale cap'),'Motion solver must change only the requested decode limit');
for(const name of ['CommandListBeginAdaptedDraw','CommandListEndAdaptedDraw']){const body=s=>s.match(new RegExp('\\['+name+'\\]\\n([\\s\\S]*?)(?=\\n\\[|$)'))[1];assert.equal(body(ini),body(oldIni),'Existing adapted Mod ABI '+name);}
for(const name of ['binding_consumer.hlsl','binding_layout.hlsl','draw_state_consumer.hlsl'])assert.equal(read(path.join(root,'JiggleForge/runtime',name)),read(path.join(root,'ShaderFixes/JiggleForgeRuntime',name)),'Both runtime consumers agree '+name);
assert.equal((ini.match(/^\[Resource/gm)||[]).length,(oldIni.match(/^\[Resource/gm)||[]).length,'No extra resource buffers');assert.equal((ini.match(/^dispatch(?:indirect)? =/gm)||[]).length,(oldIni.match(/^dispatch(?:indirect)? =/gm)||[]).length,'No extra dispatch');
assert.equal((ini.match(/cs-cb0 = ref vs-cb1->Region\(vs-cb1->Offset, vs-cb1->Size\)/g)||[]).length,3,'All object transform CB regions preserved');assert(ini.includes('post cs-t4 = null\npost cs-t5 = null'));assert(!/^\s*store\s*=/m.test(ini),'No synchronous GPU-to-CPU readback');
assert(ini.includes('global $defaultMaxOffset = 0.05'));assert(ini.includes('global $sizeReferenceLength = 0.03'));assert(ini.includes('global $sizeAdaptationEnabled = 1'));assert(ini.includes('x81 = $demandDragHeld')&&!ini.includes('x81 = $mouseDown'));
// The PS injections and inherited skin discovery must remain precisely r11.
const sections=s=>new Map([...s.matchAll(/^\[([^\]]+)\]\n([\s\S]*?)(?=^\[|$)/gm)].map(m=>[m[1],m[2]]));const before=sections(oldIni),after=sections(ini);for(const [name,body]of before)if(name.startsWith('ShaderRegex'))assert.equal(after.get(name),body,'Do not change inherited regex '+name);
report.demandReplay=JSON.parse(execFileSync(process.execPath,[path.join(repo,'experiments/binding-weight/tests/demand_replay.cjs'),root],{encoding:'utf8',timeout:30000}));
report.holdReplay=JSON.parse(execFileSync(process.execPath,[path.join(repo,'experiments/binding-weight/tests/hold_replay.cjs'),root],{encoding:'utf8',timeout:30000}));
for(const name of ['gate_cs','draw_gate_cs'])compile(path.join(__dirname,name+'.hlsl'),'cs',name);
report.originalSpatialKernelUnchanged=true;report.onlyMotionDecodeCapChanged=true;report.modAbiUnchanged=true;report.newGpuBuffers=0;report.newDispatches=0;report.cbRegionsPreserved=true;report.regexesUnchanged=true;
report.runtimeFiles=files(root).filter(p=>/\.(ini|hlsl|txt)$/i.test(p)).sort().map(p=>({path:path.relative(root,p).replaceAll('\\','/'),sha256:sha(p),bytes:fs.statSync(p).size}));
report.changedRuntimeFiles=report.runtimeFiles.filter(f=>!fs.existsSync(path.join(base,f.path))||sha(path.join(base,f.path))!==f.sha256).map(f=>f.path);
fs.writeFileSync(path.join(out,'compile.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({...report,runtimeFiles:report.runtimeFiles.length},null,2));
