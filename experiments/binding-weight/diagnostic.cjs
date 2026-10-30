'use strict';
const path=require('node:path'),assert=require('node:assert/strict');
module.exports=function({ini,out,read,write,once}){
  const labels={HEADER:'JF BIND DIAG r6',STATE:'STATE ',SLEEP:'SLEEP',WARMUP:'WARMUP',HOVER:'HOVER_READY',NO_PIXEL_PICK:'NO_PIXEL_PICK',BIND_OK_CHECK_RENDER:'BIND_OK_CHECK_RENDER',SKIN_TYPE3_NOT_REGISTERED:'SKIN_TYPE3_NOT_REGISTERED',SKIN_LAYOUT_REJECTED:'SKIN_LAYOUT_REJECTED',RENDER_STRIDE_REJECTED:'RENDER_STRIDE_REJECTED',NO_RENDER_VB:'NO_RENDER_VB',NO_BINDING_REGISTRATION:'NO_BINDING_REGISTRATION',NO_RESOLVE_CALL:'NO_RESOLVE_CALL',CAPTURE_STREAM_NOT_SEEN:'CAPTURE_STREAM_NOT_SEEN',NO_POSITION_VIEW:'NO_POSITION_VIEW',NO_BLEND_VIEW:'NO_BLEND_VIEW',SCAN_LAYOUT_REJECTED:'SCAN_LAYOUT_REJECTED',NEAREST_OVER_004:'NEAREST_OVER_0.04',INVALID_BONE_WEIGHTS:'INVALID_BONE_WEIGHTS',NO_NEAREST_VERTEX:'NO_NEAREST_VERTEX',R:'R',SO:' SO',STRIDE:' STR',OFFSET:' OFF',TYPE:' TYPE',SKIN:' SK',A:'A',P:' P',H:' H',C:' C',G:' G',W:' W',MAP:' MAP',HOVER_LINE:'HOVER ',CAPTURE_LINE:'CAP   ',BLEND:'BLEND ',N:' N',STATUS:' B',BONES:' BONES',TRY:'TRY ',MATCH:' MATCH',OTHER:' OTHER',DISTANCE:' DIST ',NONE:'NONE',POS:'POS ',WEIGHTS:' WEIGHTS',FORMAT:' FLAGS',RESOLVE:' RES',SKIN1:'SKIN FRAME D1=',SKIN3:' D3=',GOOD:' GOOD=',BAD:' BAD='};
  labels.HEADER='JF BIND DIAG r11';
  labels.DISTANCE_MODE='DISTANCE_MODE';
  let dictionary='',defines='';for(const [key,value] of Object.entries(labels)){defines+=`#define JFD_${key} ${dictionary.length}u\n`;dictionary+=value+'|';}
  write(path.join(out,'JiggleForge/runtime/binding_diagnostic_labels.hlsl'),defines);
  // Add draw-local CPU observations to the same depth-selected visible pixel.
  ini=once(ini,'[ResourceFramePick]\ntype = RWBuffer\nformat = R32G32B32A32_FLOAT\narray = 11',
    '[ResourceFramePick]\ntype = RWBuffer\nformat = R32G32B32A32_FLOAT\narray = 13');
  const stores='  store_uav_typed u7.xyzw, l(10,10,10,10), ${jf4}.xyzw\\n';
  assert.equal(ini.split(stores).length,3);
  ini=ini.replaceAll(stores,stores+'\n  ld_indexable(texture1d)(float,float,float,float) ${jf4}.xyzw, l(118,0,0,0), t120.xyzw\\n\n  store_uav_typed u7.xyzw, l(11,11,11,11), ${jf4}.xyzw\\n\n  ld_indexable(texture1d)(float,float,float,float) ${jf4}.xyzw, l(119,0,0,0), t120.xyzw\\n\n  store_uav_typed u7.xyzw, l(12,12,12,12), ${jf4}.xyzw\\n');
  ini=ini.replaceAll('store_uav_typed u7.xyzw, l(10,10,10,10), l(0,0,0,0)\\n',
    'store_uav_typed u7.xyzw, l(10,10,10,10), l(0,0,0,0)\\n\n    store_uav_typed u7.xyzw, l(11,11,11,11), l(0,0,0,0)\\n\n    store_uav_typed u7.xyzw, l(12,12,12,12), l(0,0,0,0)\\n');
  ini=ini.replace(/(\[ShaderOverrideJiggleForgeGlobal[^\]]+\]\n[\s\S]*?)(?=\n\[|$)/g,chunk=>{
    if(!chunk.includes('run = CommandListBindingBeginDraw'))return chunk;
    let pre='',post='';for(const index of [118,119])for(const c of 'xyzw'){
      pre+=`    local $diagBack${c}${index} = ${c}${index}\n`;
      post+=`    post ${c}${index} = $diagBack${c}${index}\n`;
    }
    return chunk.replace('    run = CommandListBindingBeginDraw\n',pre+'    run = CommandListBindingBeginDraw\n').replace('    post vs-t79 = ResourceBindingSavedT79\n',post+'    post vs-t79 = ResourceBindingSavedT79\n');
  });
  const drawObserve=`x118 = 10
y118 = $bindingKey
z118 = vb0->Stride
w118 = vb0->Offset
x119 = 0
y119 = 0
z119 = 0
w119 = 0
if $bindingKey
    x119 = $PoolBindingDiagnosticSkinType[$bindingKey]
    y119 = $PoolBindingDiagnosticSkinReason[$bindingKey]
    x118 = 11
    z119 = @PoolBindingOwner[$bindingKey]
    if z119 == $bindingKey
        x118 = 12
        w119 = $PoolBindingSource[$bindingKey]
        if w119 != 0
            x118 = 13
            if vb0->Stride == 40
                x118 = 1
            endif
        endif
    endif
endif
`;
  // These are only repeats of existing short-circuited owner/source queries;
  // diagnostics never reads a new key in the main resource pools.
  ini=once(ini,'$bindingKey = @vb0\nx113 = 0','$bindingKey = @vb0\n'+drawObserve+'x113 = 0');
  ini=once(ini,'cs-u3 = ResourceBindingCaptures\ndispatch = 1, 1, 1',
    'cs-u3 = ResourceBindingCaptures\ncs-u4 = ResourceBindingDiagnosticCapture\ndispatch = 1, 1, 1');
  ini=once(ini,'post cs-u3 = null\n','post cs-u3 = null\npost cs-u4 = null\n');
  for(const name of ['CustomShaderBindingClear','CustomShaderBindingDistance','CustomShaderBindingIndex','CustomShaderBindingResolve']){
    const re=new RegExp('(\\['+name+'\\]\\n[\\s\\S]*?)(?=\\n\\[|$)');
    ini=ini.replace(re,body=>body.replace(/^(dispatch(?:indirect)? = .+)$/m,'cs-u2 = ResourceBindingDiagnosticScan\n$1')+'post cs-u2 = null\n');
  }
  // Show the previous frame's packet before Present clears it for the next one.
  ini=once(ini,'    if $demandPickActive == 1\n        run = CustomShaderResetFramePick',
    `    if $bindingDiagnosticEnabled == 1
        run = CustomShaderBindingDiagnosticText
        Resource\\ZZMIv1\\Text = ref ResourceBindingDiagnosticText
        Resource\\ZZMIv1\\TextParams = ref ResourceBindingDiagnosticTextParameters
        run = CommandList\\ZZMIv1\\PrintText
    endif
    if $demandPickActive == 1
        run = CustomShaderResetFramePick`);
  ini=once(ini,'$runtimeEnabledPrevious = $runtimeEnabled\n',
    '$runtimeEnabledPrevious = $runtimeEnabled\npost $diagSkin1 = 0\npost $diagSkin3 = 0\npost $diagSkinGood = 0\npost $diagSkinBad = 0\n');
  ini=once(ini,'[CommandListResetRuntimeState]\n','[CommandListResetRuntimeState]\nclear = ResourceBindingDiagnosticCapture 0.0\nclear = ResourceBindingDiagnosticScan 0.0\n');
  ini=once(ini,'[CommandListDemandSleep]\n','[CommandListDemandSleep]\nclear = ResourceBindingDiagnosticCapture 0.0\nclear = ResourceBindingDiagnosticScan 0.0\n');
  ini+='\n'+read(path.join(__dirname,'diagnostic.ini'))+`\n[ResourceBindingDiagnosticLabels]\ntype = Buffer\nformat = R8_UINT\ndata = "${dictionary}"\n`;
  ini=once(ini,'cs-t6 = ResourceInlineCursor\ncs-u0 = ResourceBindingDiagnosticText',
    'cs-t6 = ResourceInlineCursor\ncs-t7 = ResourceBindingDiagnosticLabels\ncs-u0 = ResourceBindingDiagnosticText');
  ini=once(ini,'post cs-t6 = null\npost cs-u0 = null\npost x121',
    'post cs-t6 = null\npost cs-t7 = null\npost cs-u0 = null\npost x121');
  const edit=(name,fn)=>{const p=path.join(out,'JiggleForge/runtime',name+'.hlsl');write(p,fn(read(p)));};
  edit('reset_frame_pick_cs',s=>once(s,'COUNT = 11u','COUNT = 13u'));
  edit('update_input_cs',s=>{
    s=once(s,'RWBuffer<float4> BindingCaptures : register(u3);','RWBuffer<float4> BindingCaptures : register(u3);\nRWBuffer<float4> BindingDiagnosticCapture : register(u4);');
    return once(s,'        BindingCaptures[base+4u] = 0.0;',`        BindingCaptures[base+4u] = 0.0;
        uint diagnosticCount;
        BindingDiagnosticCapture.GetDimensions(diagnosticCount);
        if (diagnosticCount >= 4u)
        {
            BindingDiagnosticCapture[0] = float4((float)capture.Generation,(float)capture.Valid,0.0,0.0);
            BindingDiagnosticCapture[1] = sourceCount > 11u ? SourceFramePick[11] : 0.0;
            BindingDiagnosticCapture[2] = sourceCount > 12u ? SourceFramePick[12] : 0.0;
            BindingDiagnosticCapture[3] = metadata;
        }`);
  });
  edit('binding_clear_cs',s=>{
    s=once(s,'RWBuffer<uint> DispatchArgs : register(u1);','RWBuffer<uint> DispatchArgs : register(u1);\nRWBuffer<uint> DiagnosticScan : register(u2);');
    return once(s,'    pending=pending && all(asuint(Captures[base+1u])==asuint(IniParams[113]));',`    uint diagnosticCount;
    DiagnosticScan.GetDimensions(diagnosticCount);
    if (diagnosticCount >= 32u && CapturedPick[0].w > 0.5)
    {
        if (DiagnosticScan[0] != generation)
        {
            [unroll] for (uint i=0u;i<32u;++i) DiagnosticScan[i]=0u;
            DiagnosticScan[0]=generation;
            DiagnosticScan[8]=0xffffffffu;
            DiagnosticScan[12]=0xffffffffu;
        }
        if (pending)
        {
            DiagnosticScan[1]++;
            if (all(asuint(Captures[base+1u])==asuint(IniParams[113]))) DiagnosticScan[2]++;
            else DiagnosticScan[3]++;
        }
    }
    pending=pending && all(asuint(Captures[base+1u])==asuint(IniParams[113]));`);
  });
  edit('binding_nearest',s=>{
    s=once(s,'RWBuffer<uint> Nearest : register(u0);','RWBuffer<uint> Nearest : register(u0);\nRWBuffer<uint> DiagnosticScan : register(u2);');
    s=once(s,'    if (tid.x >= count || vertex >= blends || stride != 40u || blendStride != 32u) return;',`    uint diagnosticCount;
    DiagnosticScan.GetDimensions(diagnosticCount);
    bool diagnosticValid = diagnosticCount >= 32u && DiagnosticScan[0] == generation;
    if (diagnosticValid && tid.x == 0u)
    {
        DiagnosticScan[4]=count; DiagnosticScan[5]=blends;
        DiagnosticScan[6]=stride; DiagnosticScan[7]=blendStride;
        DiagnosticScan[9]=(count==0u?1u:0u)|(blends==0u?2u:0u)|((stride!=40u||blendStride!=32u)?4u:0u);
    }
    if (tid.x >= count || vertex >= blends || stride != 40u || blendStride != 32u) return;`);
    return once(s,'    // Reject a stale or unrelated geometry snapshot instead of using distance-only fallback.',`#ifndef JF_NEAREST_INDEX
    if (diagnosticValid)
    {
        if (isfinite(d2) && d2 >= 0.0) InterlockedMin(DiagnosticScan[8],asuint(d2));
        else DiagnosticScan[10]=1u;
    }
#endif
    // Reject a stale or unrelated geometry snapshot instead of using distance-only fallback.`);
  });
  edit('binding_resolve_cs',s=>{
    s=once(s,'RWBuffer<float4> Captures : register(u0);','RWBuffer<float4> Captures : register(u0);\nRWBuffer<uint> DiagnosticScan : register(u2);');
    s=once(s,'    uint vertex=Nearest[1]; if (vertex>=count || stride!=32u) return;',`    uint diagnosticCount;
    DiagnosticScan.GetDimensions(diagnosticCount);
    bool diagnosticValid = diagnosticCount >= 32u && DiagnosticScan[0] == generation;
    uint vertex=Nearest[1];
    if (diagnosticValid) DiagnosticScan[12]=vertex;
    if (vertex>=count || stride!=32u) { if (diagnosticValid) DiagnosticScan[11]=1u; return; }`);
    s=once(s,'    if (any(!isfinite(v.weights)) || any(v.weights<0.0) || dot(v.weights,1.0)<JF_BINDING_EPSILON) return;',`    if (any(!isfinite(v.weights)) || any(v.weights<0.0) || dot(v.weights,1.0)<JF_BINDING_EPSILON)
    {
        if (diagnosticValid) DiagnosticScan[11]=any(!isfinite(v.weights))?2u:any(v.weights<0.0)?3u:4u;
        return;
    }
    if (diagnosticValid) DiagnosticScan[11]=5u;`);
    return s;
  });
  return ini;
};
