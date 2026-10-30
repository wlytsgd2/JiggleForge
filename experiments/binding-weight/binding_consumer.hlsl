#include "binding_layout.hlsl"
StructuredBuffer<JF_BlendVertex> JF_VertexBlend : register(t79);
Buffer<uint4> JF_VertexBlendTyped : register(t80);
Buffer<float4> JF_BindingCaptures : register(t81);
static uint JF_BindingVertexID = 0u;
static float3 JF_BindingObjectOrigin = 0.0;
// Per VS invocation, not a GPU buffer. Different capture generations remain
// independent; overflow merely recomputes, never aliases another state.
static uint4 JF_BindingCacheKeys = 0u;
static float4 JF_BindingCacheValues = 0.0;
static uint JF_BlendLoadStatus = 0u;
static JF_BlendVertex JF_CachedVertexBlend = (JF_BlendVertex)0;
#ifdef JF_BINDING_TEST_COUNTERS
static uint JF_BindingEvaluations = 0u;
#endif
void JF_SetBindingVertex(uint vertexID, float3 origin)
{
    JF_BindingVertexID = vertexID + (uint)max(IniParams[114].x, 0.0);
    JF_BindingObjectOrigin = origin;
    JF_BindingCacheKeys = 0u;
    JF_BindingCacheValues = 0.0;
    JF_BlendLoadStatus = 0u;
#ifdef JF_BINDING_TEST_COUNTERS
    JF_BindingEvaluations = 0u;
#endif
}
bool JF_LoadVertexBlend()
{
    if (JF_BlendLoadStatus != 0u) return JF_BlendLoadStatus == 2u;
    JF_BlendLoadStatus = 1u;
    uint vertices, stride;
    if (IniParams[114].y > 0.5)
    {
        JF_VertexBlendTyped.GetDimensions(vertices);
        if (JF_BindingVertexID >= vertices / 2u) return false;
        JF_CachedVertexBlend.weights = asfloat(JF_VertexBlendTyped[JF_BindingVertexID*2u]);
        JF_CachedVertexBlend.indices = JF_VertexBlendTyped[JF_BindingVertexID*2u+1u];
    }
    else
    {
        JF_VertexBlend.GetDimensions(vertices, stride);
        if (JF_BindingVertexID >= vertices || stride != 32u) return false;
        JF_CachedVertexBlend = JF_VertexBlend[JF_BindingVertexID];
    }
    JF_BlendLoadStatus = 2u;
    return true;
}
float JF_ComputeBindingMultiplier(uint generation)
{
#ifdef JF_BINDING_TEST_COUNTERS
    JF_BindingEvaluations++;
#endif
    uint base = JF_BindingBase(generation);
    uint count;
    JF_BindingCaptures.GetDimensions(count);
    if (generation == 0u || base + JF_BINDING_STRIDE > count) return 0.0;
    float4 header = JF_BindingCaptures[base];
    if (header.x != (float)generation || header.y != 2.0) return 0.0;
    // Recycled captures and changed mesh bindings fail closed. SO / object CB
    // ping-pong must not alternate this multiplier between a weight and zero.
    if (any(asuint(JF_BindingCaptures[base+1u]) != asuint(IniParams[113]))) return 0.0;
    if (distance(JF_BindingCaptures[base+4u].xyz, JF_BindingObjectOrigin) > 0.05) return 0.0;
    if (!JF_LoadVertexBlend()) return 0.0;
    JF_BlendVertex v = JF_CachedVertexBlend;
    return JF_SharedVertexWeight(v.indices, v.weights,
        (uint4)JF_BindingCaptures[base+2u], JF_BindingCaptures[base+3u]);
}
float JF_GetBindingMultiplier(float packedHoldMetadata)
{
    // y115=1 restores the original distance field without reading bone data.
    if (IniParams[115].y > 0.5) return 1.0;
    uint generation = (uint)max(round(packedHoldMetadata), 0.0) >> 1u;
    if (generation == 0u) return 0.0;
    if (generation == JF_BindingCacheKeys.x) return JF_BindingCacheValues.x;
    if (generation == JF_BindingCacheKeys.y) return JF_BindingCacheValues.y;
    if (generation == JF_BindingCacheKeys.z) return JF_BindingCacheValues.z;
    if (generation == JF_BindingCacheKeys.w) return JF_BindingCacheValues.w;
    float weight = JF_ComputeBindingMultiplier(generation);
    if (JF_BindingCacheKeys.x == 0u) { JF_BindingCacheKeys.x=generation; JF_BindingCacheValues.x=weight; }
    else if (JF_BindingCacheKeys.y == 0u) { JF_BindingCacheKeys.y=generation; JF_BindingCacheValues.y=weight; }
    else if (JF_BindingCacheKeys.z == 0u) { JF_BindingCacheKeys.z=generation; JF_BindingCacheValues.z=weight; }
    else if (JF_BindingCacheKeys.w == 0u) { JF_BindingCacheKeys.w=generation; JF_BindingCacheValues.w=weight; }
    return weight;
}
