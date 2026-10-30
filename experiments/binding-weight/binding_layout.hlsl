#ifndef JF_BINDING_WEIGHT_LAYOUT
#define JF_BINDING_WEIGHT_LAYOUT
// Runtime-only sidecar. Existing adapted-Mod parameter/state strides stay 5/7.
static const uint JF_BINDING_SLOTS = 1024u;
static const uint JF_BINDING_STRIDE = 5u;
static const float JF_BINDING_EPSILON = 1e-8;
uint JF_BindingBase(uint generation) { return (generation % JF_BINDING_SLOTS) * JF_BINDING_STRIDE; }
// 0: generation, status (0 invalid / 1 pending / 2 resolved), vertex, reserved
// 1: actual Blend identity, vertex count, schema (3), reserved (0).
// SO and object-CB resources/regions may rotate each render frame and must not
// be the frozen logical binding identity. The SO only locates a binding cache.
// 2: bone IDs (numeric, not slot identities); 3: weights; 4: object origin
float JF_SharedVertexWeight(uint4 ids, float4 weights, uint4 capturedIds, float4 capturedWeights)
{
    float amount = 0.0;
    [unroll] for (uint i=0u; i<4u; ++i)
    {
        bool hasCommonBone = false;
        [unroll] for (uint j=0u; j<4u; ++j)
            hasCommonBone = hasCommonBone || (capturedWeights[j] > JF_BINDING_EPSILON && ids[i] == capturedIds[j]);
        // Each current slot contributes once, even when capture has duplicates.
        if (hasCommonBone && weights[i] > JF_BINDING_EPSILON) amount += weights[i];
    }
    return amount;
}
struct JF_BlendVertex { float4 weights; uint4 indices; };
struct JF_SkinnedVertex { float3 position; float3 normal; float4 tangent; };
#endif
