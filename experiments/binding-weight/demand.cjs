'use strict';
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
module.exports=function applyDemand({ini,out,read,write,once}){
  const root=__dirname;
  // Keep the public F7 switch and z112 bypass ABI independent of auto-sleep.
  ini=ini.replaceAll('if $runtimeEnabled == 1 && $diagnosticPassThrough == 0 && (DRAW_TYPE == 2 || DRAW_TYPE == 4)',
    'if $runtimeEnabled == 1 && $demandAwake == 1 && $diagnosticPassThrough == 0 && (DRAW_TYPE == 2 || DRAW_TYPE == 4)');
  for(const name of ['ShaderOverrideJiggleForgeCalibrationRoleProducer','ShaderOverrideJiggleForgeCalibrationRoleCompositeEDF7','ShaderOverrideJiggleForgeCalibrationRoleComposite857C','ShaderOverrideJiggleForgeCalibrationWorldComposite','ShaderOverrideJiggleForgeCalibrationCombatComposite']){
    const regex=new RegExp('(\\['+name+'\\]\\n[\\s\\S]*?)(?=\\n\\[|$)');
    ini=ini.replace(regex,body=>once(body,'if $runtimeEnabled == 1','if $runtimeEnabled == 1 && $demandPickActive == 1'));
  }
  // Both injected PS paths return before cursor/binding accesses while sleeping.
  const psGate='retc_nz ${jf2}.y\\n\n';
  assert.equal(ini.split(psGate).length,3);
  ini=ini.replaceAll(psGate,psGate+
    'ld_indexable(texture1d)(float,float,float,float) ${jf0}.xyzw, l(116,0,0,0), t120.xyzw\\n\n'+
    'ge ${jf0}.x, ${jf0}.x, l(0.500000)\\n\n'+
    'lt ${jf0}.y, ${jf0}.y, l(0.500000)\\n\n'+
    'and ${jf0}.x, ${jf0}.x, ${jf0}.y\\n\n'+
    'retc_nz ${jf0}.x\\n\n');
  // All current adapted projects use this shared hook. No changes to Mod INIs.
  const shared=ini.match(/\[CustomShaderUpdateProjectMotion\]\n([\s\S]*?)(?=\n\[)/)[1];
  const sharedGated=shared.replace('cs-t0 = ResourceInputController',
    `if $demandAwake == 1 && @cs-u0
    if $PoolDemandProjectSession[@cs-u0] != $demandSession
        clear = cs-u0 0.0
        $PoolDemandProjectSession[@cs-u0] = $demandSession
    endif
endif
if $demandMotionActive == 1
cs-t0 = ResourceInputController`).replace('dispatch = 16, 1, 1','dispatch = 16, 1, 1\nendif');
  ini=once(ini,shared,sharedGated);
  // Global state and cursor paths are separated, so hovering does no physics.
  ini=once(ini,'run = CustomShaderRegisterDefaultParameters\nrun = CustomShaderUpdateInput\nrun = CustomShaderUpdateMotion\nrun = CustomShaderResolveInlineCursor',
    'if $demandMotionActive == 1\n    run = CustomShaderRegisterDefaultParameters\nendif\nrun = CustomShaderUpdateInput\nif $demandMotionActive == 1\n    run = CustomShaderUpdateMotion\nendif\nif $demandPickActive == 1\n    run = CustomShaderResolveInlineCursor\nendif');
  let present=ini.match(/\[Present\]\n([\s\S]*?)(?=\n\[)/)[1];
  ini=once(ini,'x81 = $mouseDown\n','x81 = $demandDragHeld\n');
  let p=present;
  p='run = CommandListDemandSchedule\n'+p;
  p=once(p,'if $runtimeEnabled == 1\n','if $runtimeEnabled == 1 && $demandAwake == 1\n');
  p=once(p,'else if $runtimeEnabledPrevious == 1\n','else if $runtimeEnabled == 0 && $runtimeEnabledPrevious == 1\n');
  p=once(p,'if $mouseDown == 1 && $bindingWasHeld == 0','if $demandDragHeld == 1');
  p=once(p,'$bindingWasHeld = $mouseDown','$bindingWasHeld = $demandDragHeld');
  p=once(p,'    run = CustomShaderResetFramePick\n    clear = ResourceNativePick0 0.0\n    clear = ResourceNativePickDepth depth 0.0\n    run = CommandListComputeDimensions',
    '    if $demandPickActive == 1\n        run = CustomShaderResetFramePick\n        run = CommandListComputeDimensions\n    endif');
  ini=once(ini,present,p);
  ini+='\n'+read(path.join(root,'demand.ini'));
  let consumer=read(path.join(out,'JiggleForge/runtime/draw_state_consumer.hlsl'));
  consumer=once(consumer,'return IniParams[112].x > 0.5 || IniParams[112].z > 0.5;',
    'return IniParams[112].x > 0.5 || IniParams[112].z > 0.5\n    || (IniParams[116].x > 0.5 && max(IniParams[116].y, IniParams[116].z) < 0.5);');
  consumer=once(consumer,'  minimumActiveRadius = 100.0;',
    '  if (IniParams[116].x > 0.5 && IniParams[116].z < 0.5)\n    return float3(0.0, 0.0, 0.0);\n\n  minimumActiveRadius = 100.0;');
  consumer=once(consumer,'p1.y) * vertexMask * bindingWeight;',
    'p1.y) * vertexMask * bindingWeight * (IniParams[116].x > 0.5 ? saturate(IniParams[116].w) : 1.0);');
  for(const folder of ['JiggleForge/runtime','ShaderFixes/JiggleForgeRuntime'])write(path.join(out,folder,'draw_state_consumer.hlsl'),consumer);
  const inputFile=path.join(out,'JiggleForge/runtime/update_input_cs.hlsl');
  let input=read(inputFile);
  input=once(input,'    uint bindingOldGeneration = controller.CaptureGeneration;',
    `    // A cold start must see a fresh calibrated pixel, not a stale old hover.
    if (IniParams[116].x > 0.5)
    {
        if (IniParams[117].y > 0.5) currentPick.Valid = 0u;
        if (input.DragHeld != 0u && capturedCount >= 9u && capture.Valid == 0u
            && currentPick.Valid != 0u && IniParams[117].x > 0.5)
            controller.PreviousHeld = 0u;
    }
    uint bindingOldGeneration = controller.CaptureGeneration;`);
  write(inputFile,input);
  return ini;
};
