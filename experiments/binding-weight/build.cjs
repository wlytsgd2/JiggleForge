'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict'),crypto=require('node:crypto');
const repo=path.resolve(__dirname,'../..');
const work=path.resolve(process.env.JIGGLEFORGE_EXPERIMENT_OUTPUT||path.join(repo,'artifacts/binding-weight-runtime'));
const out=path.join(work,'JiggleForge-SharedWeight-Test-r11-options');
const source=path.join(repo,'StandaloneShaderFixes');
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'').replace(/\r/g,'');
const write=(p,s)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,s);};
function once(s,a,b){assert(s.includes(a),'Missing patch anchor '+a.slice(0,100));assert.equal(s.split(a).length,2,'Ambiguous patch anchor');return s.replace(a,b);}
fs.mkdirSync(work,{recursive:true});fs.cpSync(source,out,{recursive:true});
for(const name of fs.readdirSync(__dirname).filter(n=>n.endsWith('.hlsl'))){
 write(path.join(out,'JiggleForge/runtime',name),read(path.join(__dirname,name)));
 if(['binding_layout.hlsl','binding_consumer.hlsl'].includes(name))write(path.join(out,'ShaderFixes/JiggleForgeRuntime',name),read(path.join(__dirname,name)));
}
let consumer=read(path.join(source,'JiggleForge/runtime/draw_state_consumer.hlsl'));
consumer=once(consumer,'#include "deformation_field.hlsl"','#include "deformation_field.hlsl"\n#include "binding_consumer.hlsl"');
consumer=once(consumer,'  float4 p4 = 0.0;','  float4 p4 = 0.0;\n  float bindingHoldMetadata = 0.0;');
consumer=once(consumer,'    motion = JF_MotionState[motionBase + 0u];','    motion = JF_MotionState[motionBase + 0u];\n    bindingHoldMetadata = JF_MotionState[motionBase + 6u].w;');
consumer=once(consumer,'    motion = JF_GlobalMotionState[motionBase + 0u];','    motion = JF_GlobalMotionState[motionBase + 0u];\n    bindingHoldMetadata = JF_GlobalMotionState[motionBase + 6u].w;');
consumer=once(consumer,'  float3 stateDisplacement = JF_EvaluateGrabField(',`  float3 bindingRelative = worldPosition - anchor.xyz;
  float bindingRadius = max(p0.z, 0.000001);
  if (vertexMask <= 0.0 || dot(bindingRelative,bindingRelative) >= bindingRadius*bindingRadius || all(motion.xyz == 0.0)) return;
  float bindingWeight = JF_GetBindingMultiplier(bindingHoldMetadata);
  if (bindingWeight <= 0.0) return;
  float3 stateDisplacement = JF_EvaluateGrabField(`);
consumer=once(consumer,'p1.y) * vertexMask;','p1.y) * vertexMask * bindingWeight;');
for(const folder of ['JiggleForge/runtime','ShaderFixes/JiggleForgeRuntime'])write(path.join(out,folder,'draw_state_consumer.hlsl'),consumer);
const hashes=[];
for(const name of fs.readdirSync(path.join(source,'ShaderFixes')).filter(n=>n.endsWith('-vs_replace.txt'))){
 let vs=read(path.join(source,'ShaderFixes',name));
 assert(vs.includes('cb1[3]'),name+' object transform');
 // Input layouts were created from the game's original VS signature. NEVER
 // prepend a new system input: it shifts every existing register by one.
 vs=once(vs,'  float3 v8 : TEXCOORD4,\n','  float3 v8 : TEXCOORD4,\n  uint jfBindingVertexID : SV_VertexID,\n');
 vs=once(vs,'  uint4 bitmask, uiDest;','  JF_SetBindingVertex(jfBindingVertexID, cb1[3].xyz);\n  uint4 bitmask, uiDest;');
 write(path.join(out,'ShaderFixes',name),vs);hashes.push(name.slice(0,16));
}
// Both modes share the original physics and the configured MaxOffset.
let motion=read(path.join(source,'JiggleForge/runtime/motion_model.hlsl'));
write(path.join(out,'JiggleForge/runtime/motion_model.hlsl'),motion);
for (const name of ['update_motion_cs','update_project_motion_cs']) {
 const s=read(path.join(source,'JiggleForge/runtime',name+'.hlsl'));
 write(path.join(out,'JiggleForge/runtime',name+'.hlsl'),s);
}
let input=read(path.join(source,'JiggleForge/runtime/update_input_cs.hlsl'));
input=once(input,'#include "pick_decoder.hlsl"','#include "pick_decoder.hlsl"\n#include "binding_layout.hlsl"');
input=once(input,'Texture1D<float4> IniParams : register(t120);','RWBuffer<float4> BindingCaptures : register(u3);\nTexture1D<float4> IniParams : register(t120);');
input=once(input,'    JF_UpdateInputController(','    uint bindingOldGeneration = controller.CaptureGeneration;\n    JF_UpdateInputController(');
input=once(input,'    CapturedPickRecords[8u] = q8;',`    CapturedPickRecords[8u] = q8;
    if (controller.CaptureGeneration != bindingOldGeneration)
    {
        uint base = JF_BindingBase(capture.Generation);
        float4 metadata = sourceCount > 10u ? SourceFramePick[10u] : 0.0;
        bool pending = capture.Valid != 0u && metadata.x != 0.0;
        BindingCaptures[base] = float4((float)capture.Generation, pending ? 1.0 : 0.0, 0.0, 0.0);
        BindingCaptures[base+1u] = metadata;
        BindingCaptures[base+2u] = 0.0;
        BindingCaptures[base+3u] = 0.0;
        BindingCaptures[base+4u] = 0.0;
    }`);
write(path.join(out,'JiggleForge/runtime/update_input_cs.hlsl'),input);
write(path.join(out,'JiggleForge/runtime/reset_frame_pick_cs.hlsl'),read(path.join(source,'JiggleForge/runtime/reset_frame_pick_cs.hlsl')).replace('COUNT = 10u','COUNT = 11u'));
let ini=read(path.join(source,'JiggleForge.ini'));
let patchedMetadata=0;
ini=ini.replaceAll('store_uav_typed u7.xyzw, l(9,9,9,9), ${jf0}.xyzw\\n',()=>{patchedMetadata++;return 'store_uav_typed u7.xyzw, l(9,9,9,9), ${jf0}.xyzw\\n\n  ld_indexable(texture1d)(float,float,float,float) ${jf4}.xyzw, l(113,0,0,0), t120.xyzw\\n\n  store_uav_typed u7.xyzw, l(10,10,10,10), ${jf4}.xyzw\\n';});
assert.equal(patchedMetadata,2);
ini=ini.replaceAll('store_uav_typed u7.xyzw, l(9,9,9,9), l(0,0,0,0)\\n','store_uav_typed u7.xyzw, l(9,9,9,9), l(0,0,0,0)\\n\n    store_uav_typed u7.xyzw, l(10,10,10,10), l(0,0,0,0)\\n');
ini=once(ini,'[ResourceFramePick]\ntype = RWBuffer\nformat = R32G32B32A32_FLOAT\narray = 10','[ResourceFramePick]\ntype = RWBuffer\nformat = R32G32B32A32_FLOAT\narray = 11');
let drawHooks=0;
ini=ini.replace(/(\[ShaderOverrideJiggleForgeGlobal[^\]]+\]\n[\s\S]*?)(?=\n\[|$)/g,chunk=>{
 const marker='if $runtimeEnabled == 1 && $diagnosticPassThrough == 0 && (DRAW_TYPE == 2 || DRAW_TYPE == 4)\n';
 if(!chunk.includes(marker))return chunk;drawHooks++;
 const pre=`    local $bindingBackX113 = x113
    local $bindingBackY113 = y113
    local $bindingBackZ113 = z113
    local $bindingBackW113 = w113
    local $bindingBackX114 = x114
    local $bindingBackY114 = y114
    local $bindingBackW114 = w114
    ResourceBindingSavedT79 = ref vs-t79
    ResourceBindingSavedT80 = ref vs-t80
    ResourceBindingSavedT81 = ref vs-t81
    $bindingCanResolve = ${chunk.includes('hash = c280f6945b23a42a')||chunk.includes('hash = 26214fb5eedfcbdd')?1:0}
    run = CommandListBindingBeginDraw
`;
 const post=`    post vs-t79 = ResourceBindingSavedT79
    post vs-t80 = ResourceBindingSavedT80
    post vs-t81 = ResourceBindingSavedT81
    post x113 = $bindingBackX113
    post y113 = $bindingBackY113
    post z113 = $bindingBackZ113
    post w113 = $bindingBackW113
    post x114 = $bindingBackX114
    post y114 = $bindingBackY114
    post w114 = $bindingBackW114
`;
 return chunk.replace(marker,marker+pre).replace('\nelse\n','\n'+post+'else\n');
});
assert.equal(drawHooks,12);
ini=once(ini,'cs-u2 = ResourceFrameContext\ndispatch = 1, 1, 1\npost cs-t0 = null\npost cs-u0 = null\npost cs-u1 = null\npost cs-u2 = null','cs-u2 = ResourceFrameContext\ncs-u3 = ResourceBindingCaptures\ndispatch = 1, 1, 1\npost cs-t0 = null\npost cs-u0 = null\npost cs-u1 = null\npost cs-u2 = null\npost cs-u3 = null');
ini=once(ini,'[CommandListResetRuntimeState]\n','[CommandListResetRuntimeState]\nclear = ResourceBindingCaptures 0.0\nPoolBindingOwner[*] = null\nPoolBindingSource[*] = null\n$PoolBindingSource[*] = null\nPoolBindingBlend[*] = null\nPoolBindingTyped[*] = null\n$PoolBindingResolveFrame[*] = null\nResourceBindingSkinSO = null\nResourceBindingSavedProbe = null\nResourceBindingTryTyped = null\nResourceBindingTryStructured = null\nResourceBindingTypedPositions = null\nResourceBindingStructuredPositions = null\nResourceBindingPositions = null\n$bindingResolveFrames = 0\n$bindingWasHeld = 0\n');
ini=once(ini,'[Present]\n','[Present]\n$bindingFrame = $bindingFrame + 1\n');
ini=once(ini,'    if $initialised == 1\n        run = CommandListRuntimeStep',`    if $mouseDown == 1 && $bindingWasHeld == 0
        $bindingResolveFrames = 3
    else if $bindingResolveFrames > 0
        $bindingResolveFrames = $bindingResolveFrames - 1
    endif
    $bindingWasHeld = $mouseDown
    if $initialised == 1
        run = CommandListRuntimeStep`);
// Keep the application's configured MaxOffset in both modes.
ini+=`\n[Constants]
global $bindingFrame = 1
global $bindingSkinCandidate = 0
global $bindingWasHeld = 0
global $bindingResolveFrames = 0
global $bindingKey = 0
global $bindingCanResolve = 0
\n[ResourceBindingSavedT79]\n[ResourceBindingSavedT80]\n[ResourceBindingSavedT81]\n\n`+read(path.join(__dirname,'hooks.ini'));
ini=require('./demand.cjs')({ini,out,read,write,once});
ini=require('./diagnostic.cjs')({ini,out,read,write,once});
ini=require('./diagnostic_export.cjs')({ini,once});
ini=once(ini,'[Present]\n','[Present]\ny115 = 1 - $boneWeightEnabled\n');
ini+='\n[Constants]\n; JIGGLEFORGE_BONE_WEIGHT_BEGIN\nglobal $boneWeightEnabled = 1\ny115 = 0\n; JIGGLEFORGE_BONE_WEIGHT_END\n';
ini=once(ini,'global $bindingDiagnosticEnabled = 1','global $bindingDiagnosticEnabled = 0');
ini=ini.replace('data = "JiggleForge Enabled"','data = "JF r11 | Bone-weight option | Visible cursor only"');
write(path.join(out,'JiggleForge.ini'),ini);
write(path.join(out,'测试说明.md'),read(path.join(__dirname,'BUILD.md')));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const baseline=[];function collect(dir,base){for(const ent of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,ent.name);if(ent.isDirectory())collect(p,base);else baseline.push({path:path.relative(base,p).replaceAll('\\','/'),sha256:hash(fs.readFileSync(p))});}}collect(source,source);
write(path.join(work,'build-report-r11-options.json'),JSON.stringify({revision:'shared-vertex-weight-r11-options',boneWeightOption:true,onDemand:true,visibleCursorOnly:true,hiddenCursorCancels:true,diagnosticsOnly:false,cbRegionsPreserved:true,frameAnalysisExports:true,maxOffset:'configured value in both modes',hashes,drawHooks,modFilesChanged:false,pickup:'visible pixel plus nearest skinned vertex binding approximation',baseline},null,2));
console.log(JSON.stringify({out,drawHooks,hashes:hashes.length,sharedConfiguredMaxOffset:true,modFilesChanged:false}));
