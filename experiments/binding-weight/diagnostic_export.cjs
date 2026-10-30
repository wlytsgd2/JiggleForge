'use strict';
// FrameAnalysis alone does not dump custom shader inputs/outputs. Explicitly
// save them at their point of use, without store/readback or runtime changes.
module.exports=function({ini,once}){
  const edit=(name,fn)=>{
    const re=new RegExp('(\\['+name+'\\]\\n[\\s\\S]*?)(?=\\n\\[|$)');
    if(!re.test(ini))throw Error('Missing export hook '+name);
    ini=ini.replace(re,fn);
  };
  edit('CustomShaderBindingDistance',s=>once(s,
    'dispatchindirect = ResourceBindingDispatchArgs, 0\n',
    `dispatchindirect = ResourceBindingDispatchArgs, 0
; Only on F8 FrameAnalysis. Preserve the exact live SRV/CB bindings below.
if frame_analysis
    run = CustomShaderBindingDiagnosticSearchContext
    dump = buf cs-t0
    dump = buf cs-t1
    dump = buf cs-t2
    dump = buf cs-t3
    dump = buf cs-t4
    dump = buf cs-t5
    dump = buf txt cs-cb0
    dump = buf ResourceBindingNearest
    dump = buf ResourceBindingDispatchArgs
    dump = buf ResourceBindingDiagnosticScan
endif
`));
  edit('CustomShaderBindingResolve',s=>once(s,
    'dispatchindirect = ResourceBindingDispatchArgs, 12\n',
    `dispatchindirect = ResourceBindingDispatchArgs, 12
if frame_analysis
    dump = buf ResourceBindingCaptures
    dump = buf ResourceBindingNearest
    dump = buf ResourceBindingDiagnosticScan
endif
`));
  ini=once(ini,'    if $demandPickActive == 1\n        run = CustomShaderResetFramePick',
    `    if frame_analysis
        run = CommandListBindingDiagnosticDumpFrame
    endif
    if $demandPickActive == 1
        run = CustomShaderResetFramePick`);
  return ini+`
; These commands must never activate FrameAnalysis themselves.
[CommandListBindingDiagnosticDumpFrame]
if frame_analysis
    dump = buf ResourceFramePick
    dump = buf ResourceInputController
    dump = buf ResourceCapturedPick
    dump = buf ResourceFrameContext
    dump = buf ResourceBindingCaptures
    dump = buf ResourceBindingDiagnosticCapture
    dump = buf ResourceBindingDiagnosticScan
    dump = buf ResourceBindingNearest
    dump = buf ResourceBindingDispatchArgs
    dump = buf ResourceInlineCursor
endif

[ResourceBindingDiagnosticSearchContext]
type = RWBuffer
format = R32G32B32A32_FLOAT
array = 14
bind_flags = unordered_access shader_resource

[CustomShaderBindingDiagnosticSearchContext]
cs = ./JiggleForge/runtime/binding_diagnostic_context_cs.hlsl
if frame_analysis
    cs-u3 = ResourceBindingDiagnosticSearchContext
    dispatch = 1, 1, 1
    dump = buf ResourceBindingDiagnosticSearchContext
    cs-u3 = null
endif
`;
};
