#include "binding_layout.hlsl"
Buffer<float4> CapturedPick : register(t0);
Buffer<float4> Captures : register(t1);
StructuredBuffer<JF_SkinnedVertex> Positions : register(t2);
StructuredBuffer<JF_BlendVertex> Blend : register(t3);
Buffer<float> PositionsTyped : register(t4);
Buffer<uint4> BlendTyped : register(t5);
RWBuffer<uint> Nearest : register(u0);
Texture1D<float4> IniParams : register(t120);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
[numthreads(64,1,1)]
void main(uint3 tid : SV_DispatchThreadID)
{
    uint generation = (uint)CapturedPick[3u].w;
    uint base = JF_BindingBase(generation);
    if (CapturedPick[0u].w < 0.5 || Captures[base].x != (float)generation || Captures[base].y != 1.0) return;
    if (any(asuint(Captures[base+1u]) != asuint(IniParams[113]))) return;
    uint count, stride=40u, blends, blendStride=32u;
    if (IniParams[114].w > 0.5) { PositionsTyped.GetDimensions(count); count/=10u; }
    else Positions.GetDimensions(count,stride);
    if (IniParams[114].y > 0.5) { BlendTyped.GetDimensions(blends); blends/=2u; }
    else Blend.GetDimensions(blends,blendStride);
    uint vertex = tid.x + (uint)max(IniParams[114].x,0.0);
    if (tid.x >= count || vertex >= blends || stride != 40u || blendStride != 32u) return;
    float3 p;
    if (IniParams[114].w > 0.5) p=float3(PositionsTyped[tid.x*10u],PositionsTyped[tid.x*10u+1u],PositionsTyped[tid.x*10u+2u]);
    else p=Positions[tid.x].position;
    float3 world = Model[0].xyz*p.x + Model[1].xyz*p.y + Model[2].xyz*p.z + Model[3].xyz;
    float3 r = world - CapturedPick[0].xyz;
    float d2 = dot(r,r);
    // Reject a stale or unrelated geometry snapshot instead of using distance-only fallback.
    if (!isfinite(d2) || d2 > 0.0016) return;
#ifdef JF_NEAREST_INDEX
    if (asuint(d2) == Nearest[0]) InterlockedMin(Nearest[1], vertex);
#else
    InterlockedMin(Nearest[0], asuint(d2));
#endif
}
