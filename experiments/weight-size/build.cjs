'use strict';
// Isolated v26/r11 branch. Never installs into ZZMI or edits character Mods.
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto'),{execFileSync}=require('node:child_process');
const repo=path.resolve(__dirname,'../..'),work=path.join(repo,'artifacts/weight-size-r1');
const baseWork=path.join(work,'base'),base=path.join(baseWork,'JiggleForge-SharedWeight-Test-r11-options'),out=path.join(work,'runtime');
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'').replace(/\r/g,''),write=(p,s)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,s);};
const once=(s,a,b)=>{assert.equal(s.split(a).length,2,'Expected one anchor: '+a.slice(0,95));return s.replace(a,b);};
assert(!fs.existsSync(path.join(work,'packages/JiggleForge-WeightSize-r1-FULL.zip')),'Do not mutate an already packaged revision');
execFileSync(process.execPath,[path.join(repo,'experiments/binding-weight/build.cjs')],{env:{...process.env,JIGGLEFORGE_EXPERIMENT_OUTPUT:baseWork},stdio:'inherit'});
fs.cpSync(base,out,{recursive:true});
for(const name of ['binding_resolve_cs.hlsl','size_estimate.hlsl'])write(path.join(out,'JiggleForge/runtime',name),read(path.join(__dirname,name)));

let consumer=read(path.join(base,'JiggleForge/runtime/binding_consumer.hlsl'));
consumer=once(consumer,'static float4 JF_BindingCacheValues = 0.0;',`static float4 JF_BindingCacheValues = 0.0;
static float4 JF_BindingCacheScales = 1.0;
static float JF_LastBindingScale = 1.0;`);
consumer=once(consumer,'    JF_BindingCacheValues = 0.0;','    JF_BindingCacheValues = 0.0;\n    JF_BindingCacheScales = 1.0;\n    JF_LastBindingScale = 1.0;');
consumer=once(consumer,'    if (!JF_LoadVertexBlend()) return 0.0;',`    if (!JF_LoadVertexBlend()) return 0.0;
    // Frozen per capture, independent of draw/state order. Zero is legacy scale 1.
    if (IniParams[115].z > 0.5 && isfinite(header.w) && header.w >= 0.25 && header.w <= 4.0)
        JF_LastBindingScale = header.w;`);
consumer=once(consumer,'    // y115=1 restores','    JF_LastBindingScale = 1.0;\n    // y115=1 restores');
for(const c of 'xyzw'){
 consumer=once(consumer,`if (generation == JF_BindingCacheKeys.${c}) return JF_BindingCacheValues.${c};`,`if (generation == JF_BindingCacheKeys.${c}) { JF_LastBindingScale=JF_BindingCacheScales.${c}; return JF_BindingCacheValues.${c}; }`);
 consumer=once(consumer,`JF_BindingCacheValues.${c}=weight;`,`JF_BindingCacheValues.${c}=weight; JF_BindingCacheScales.${c}=JF_LastBindingScale;`);
}
let draw=read(path.join(base,'JiggleForge/runtime/draw_state_consumer.hlsl'));
// Coarse cutoff must include every allowed size factor. Strict culling happens
// AFTER generation/identity matching has retrieved the frozen factor.
draw=once(draw,'  float bindingRadius = max(p0.z, 0.000001);',`  float bindingRadius = max(p0.z, 0.000001);
  float bindingSearchRadius = bindingRadius * ((IniParams[115].z > 0.5 && IniParams[115].y <= 0.5) ? 4.0 : 1.0);`);
draw=once(draw,'dot(bindingRelative,bindingRelative) >= bindingRadius*bindingRadius','dot(bindingRelative,bindingRelative) >= bindingSearchRadius*bindingSearchRadius');
draw=once(draw,'  if (bindingWeight <= 0.0) return;',`  if (bindingWeight <= 0.0) return;
  float sizeScale = JF_LastBindingScale;
  bindingRadius *= sizeScale;
  if (dot(bindingRelative,bindingRelative) >= bindingRadius*bindingRadius) return;`);
draw=once(draw,'    motion.xyz,','    motion.xyz * sizeScale,');
draw=once(draw,'    p0.z,','    bindingRadius,');
draw=once(draw,'minimumActiveRadius, max(p0.z, 0.000001)','minimumActiveRadius, bindingRadius');
for(const folder of ['JiggleForge/runtime','ShaderFixes/JiggleForgeRuntime']){
 write(path.join(out,folder,'binding_consumer.hlsl'),consumer);
 write(path.join(out,folder,'draw_state_consumer.hlsl'),draw);
 let layout=read(path.join(base,folder,'binding_layout.hlsl'));
 layout=once(layout,'vertex, reserved','vertex, frozen size scale (.25..4; legacy zero means 1)');
 write(path.join(out,folder,'binding_layout.hlsl'),layout);
}
// Cap base physics without editing saved Mod settings. Smaller user limits are
// preserved; the spatial consumer scales this cap with the captured size.
let motion=read(path.join(base,'JiggleForge/runtime/motion_model.hlsl'));
motion=once(motion,'result.MaxOffset = JF_FiniteOr(p1.w, 0.0f);','result.MaxOffset = min(JF_FiniteOr(p1.w, 0.0f), 0.05f); // WeightSize r1 reference-scale cap');
write(path.join(out,'JiggleForge/runtime/motion_model.hlsl'),motion);

let ini=read(path.join(base,'JiggleForge.ini'));
const start=ini.indexOf('[CustomShaderBindingResolve]'),end=ini.indexOf('\n[Constants]',start);
assert(start>=0&&end>start);
let section=ini.slice(start,end);
section=once(section,'cs-t3 = PoolBindingTyped[$bindingKey]','cs-t3 = PoolBindingTyped[$bindingKey]\ncs-t4 = ResourceBindingStructuredPositions\ncs-t5 = ResourceBindingTypedPositions');
section=once(section,'post cs-t3 = null','post cs-t3 = null\npost cs-t4 = null\npost cs-t5 = null');
section=section.replace('w is fade gain.post cs-u2 = null','w is fade gain.\npost cs-u2 = null');
ini=ini.slice(0,start)+section+ini.slice(end);
ini=once(ini,'y115 = 1 - $boneWeightEnabled','y115 = 1 - $boneWeightEnabled\nz115 = $sizeAdaptationEnabled\nw115 = $sizeReferenceLength');
ini=once(ini,'global $defaultMaxOffset = 0.1','global $defaultMaxOffset = 0.05');
ini=once(ini,'data = 1 2 0.12 0.7 1.0 2.0 0.75 0.1 0.02','data = 1 2 0.12 0.7 1.0 2.0 0.75 0.05 0.02');
ini=once(ini,'JF r11 | Bone-weight option | Visible cursor only','JF WeightSize r1 | L max 0.04 | Base offset 0.05');
ini+=`\n[Constants]
; JIGGLEFORGE_WEIGHT_SIZE_BEGIN
; Shared reference, not a per-character setting. Restart after editing.
; Measured L is clamped to 0.04 in size_estimate.hlsl before scale calculation.
; Set enabled to 0 for v26 fixed-size comparison with the same base offset cap.
global $sizeAdaptationEnabled = 1
global $sizeReferenceLength = 0.03
; JIGGLEFORGE_WEIGHT_SIZE_END
z115 = 1
w115 = 0.03
`;
write(path.join(out,'JiggleForge.ini'),ini);
const oldKernel=read(path.join(base,'JiggleForge/runtime/deformation_field.hlsl'));
assert.equal(read(path.join(out,'JiggleForge/runtime/deformation_field.hlsl')),oldKernel,'Do not change v26 spatial kernel');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
write(path.join(work,'build.json'),JSON.stringify({revision:'WeightSize-r1',base,out,baseSource:'binding-weight-r11-options / v26, not Tactile',maxMeasuredL:.04,referenceL:.03,baseMaxOffset:.05,smallerGroupLimitsPreserved:true,finalMaximumMotionAtDefaultReference:.05*(.04/.03),scaleBounds:[.25,4],scan:'all supplied skinned vertices up to 262144; larger streams use factor 1',statisticalWeight:'linear raw merged weights; strongest captured bone',newGpuBuffers:0,newDispatches:0,modAbiChanged:false,gameInstalled:false,spatialKernelSha256:sha(path.join(out,'JiggleForge/runtime/deformation_field.hlsl'))},null,2));
console.log(JSON.stringify({out,baseMaxOffset:.05,maxMeasuredL:.04,gameInstalled:false}));
