#include "deformation_field.hlsl"

float3 JF_EvaluateBoundStates(
  float3 worldPosition,
  float vertexMask,
  out bool hasMovingInfluence,
  out float minimumActiveRadius)
{
  hasMovingInfluence = false;
  minimumActiveRadius = 100.0;
  float3 totalDisplacement = float3(0.0, 0.0, 0.0);

  uint stateListCount;
  uint motionRecordCount;
  uint parameterRecordCount;
  JiggleForgeDirectStateIndex.GetDimensions(stateListCount);
  JF_MotionState.GetDimensions(motionRecordCount);
  JF_GroupParams.GetDimensions(parameterRecordCount);

  [loop]
  for (uint listIndex = 0u; listIndex < stateListCount; ++listIndex)
  {
    uint stateIndex = JiggleForgeDirectStateIndex[listIndex];
    uint motionBase = stateIndex * 7u;
    uint parameterBase = stateIndex * 5u;
    if (motionBase + 7u > motionRecordCount
        || parameterBase + 5u > parameterRecordCount)
      continue;

    float4 motion = JF_MotionState[motionBase + 0u];
    float4 anchor = JF_MotionState[motionBase + 2u];
    float4 p0 = JF_GroupParams[parameterBase + 0u];
    float4 p1 = JF_GroupParams[parameterBase + 1u];
    float4 p4 = JF_GroupParams[parameterBase + 4u];
    bool valid =
      motion.w > 0.5
      && abs(p0.y - 2.0) < 0.25
      && p4.w > 0.5
      && abs(anchor.w - p0.x) < 0.25;
    if (!valid)
      continue;

    float3 stateDisplacement = JF_EvaluateGrabField(
      worldPosition,
      anchor.xyz,
      motion.xyz,
      p0.z,
      p1.x,
      p1.y) * vertexMask;
    totalDisplacement += stateDisplacement;
    if (dot(motion.xyz, motion.xyz) > 1e-12
        && dot(stateDisplacement, stateDisplacement) > 1e-16)
    {
      hasMovingInfluence = true;
      minimumActiveRadius = min(minimumActiveRadius, max(p0.z, 0.000001));
    }
  }

  if (!hasMovingInfluence)
    minimumActiveRadius = 0.0;
  return totalDisplacement;
}
