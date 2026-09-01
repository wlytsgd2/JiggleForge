#include "deformation_field.hlsl"

// Project influence lists now contain only group IDs. The retired JFG4 marker
// is recognized only so an old generated Mod fails closed until the user
// reapplies its JiggleForge.txt configuration with the current application.
static const uint JF_RETIRED_PROJECT_INFLUENCE_MAGIC = 0x4a464734u;

bool JF_IsDrawBypassed()
{
  return IniParams[112].z > 0.5;
}

void JF_AccumulateState(
  float3 worldPosition,
  float vertexMask,
  uint stateId,
  bool useGlobalState,
  bool projectLocal,
  inout float3 totalDisplacement,
  inout bool hasMovingInfluence,
  inout float minimumActiveRadius)
{
  uint motionRecordCount;
  uint parameterRecordCount;
  uint motionBase;
  uint parameterBase;
  float4 motion = 0.0;
  float4 anchor = 0.0;
  float4 p0 = 0.0;
  float4 p1 = 0.0;
  float4 p4 = 0.0;

  if (!useGlobalState)
  {
    if (projectLocal && stateId == 0u)
      return;
    uint localIndex = projectLocal ? stateId - 1u : stateId;
    motionBase = localIndex * 7u;
    parameterBase = localIndex * 5u;
    JF_MotionState.GetDimensions(motionRecordCount);
    JF_GroupParams.GetDimensions(parameterRecordCount);
    if (motionBase + 7u > motionRecordCount
        || parameterBase + 5u > parameterRecordCount)
      return;
    motion = JF_MotionState[motionBase + 0u];
    anchor = JF_MotionState[motionBase + 2u];
    p0 = JF_GroupParams[parameterBase + 0u];
    p1 = JF_GroupParams[parameterBase + 1u];
    p4 = JF_GroupParams[parameterBase + 4u];
  }
  else
  {
    motionBase = stateId * 7u;
    parameterBase = stateId * 5u;
    JF_GlobalMotionState.GetDimensions(motionRecordCount);
    JF_GlobalGroupParams.GetDimensions(parameterRecordCount);
    if (motionBase + 7u > motionRecordCount
        || parameterBase + 5u > parameterRecordCount)
      return;
    motion = JF_GlobalMotionState[motionBase + 0u];
    anchor = JF_GlobalMotionState[motionBase + 2u];
    p0 = JF_GlobalGroupParams[parameterBase + 0u];
    p1 = JF_GlobalGroupParams[parameterBase + 1u];
    p4 = JF_GlobalGroupParams[parameterBase + 4u];
  }

  bool valid =
    motion.w > 0.5
    && abs(p0.y - 2.0) < 0.25
    && p4.w > 0.5
    && abs(anchor.w - p0.x) < 0.25;
  if (!valid)
    return;

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

float3 JF_EvaluateBoundStates(
  float3 worldPosition,
  float vertexMask,
  out bool hasMovingInfluence,
  out float minimumActiveRadius)
{
  hasMovingInfluence = false;
  minimumActiveRadius = 0.0;

  if (JF_IsDrawBypassed())
    return float3(0.0, 0.0, 0.0);

  minimumActiveRadius = 100.0;
  float3 totalDisplacement = float3(0.0, 0.0, 0.0);

  uint stateListCount;
  JiggleForgeDirectStateIndex.GetDimensions(stateListCount);
  if (stateListCount == 0u)
    return totalDisplacement;

  uint globalMotionRecordCount;
  JF_GlobalMotionState.GetDimensions(globalMotionRecordCount);
  // BeginAdaptedDraw binds t77 for project Draws. Ordinary game Draws leave it
  // unbound and therefore keep the legacy/global interpretation of t72.
  bool projectFormat = globalMotionRecordCount > 0u;
  if (projectFormat
      && JiggleForgeDirectStateIndex[0u] == JF_RETIRED_PROJECT_INFLUENCE_MAGIC)
    return totalDisplacement;

  [loop]
  for (uint listIndex = 0u; listIndex < stateListCount; ++listIndex)
  {
    uint stateId = JiggleForgeDirectStateIndex[listIndex];
    if (projectFormat && stateId == 0u)
    {
      JF_AccumulateState(
        worldPosition,
        vertexMask,
        0u,
        true,
        false,
        totalDisplacement,
        hasMovingInfluence,
        minimumActiveRadius);
    }
    else
    {
      JF_AccumulateState(
        worldPosition,
        vertexMask,
        stateId,
        false,
        projectFormat,
        totalDisplacement,
        hasMovingInfluence,
        minimumActiveRadius);
    }
  }

  if (!hasMovingInfluence)
    minimumActiveRadius = 0.0;
  return totalDisplacement;
}
