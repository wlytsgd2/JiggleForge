#include "binding_layout.hlsl"
Buffer<float4> CapturedPick : register(t0);
Buffer<uint> Nearest : register(t1);
StructuredBuffer<JF_BlendVertex> Blend : register(t2);
Buffer<uint4> BlendTyped : register(t3);
RWBuffer<float4> Captures : register(u0);
Texture1D<float4> IniParams : register(t120);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
[numthreads(1,1,1)]
void main(uint3 tid : SV_DispatchThreadID)
{
    uint generation = (uint)CapturedPick[3].w, base = JF_BindingBase(generation);
    if (CapturedPick[0].w < 0.5 || Captures[base].x != (float)generation || Captures[base].y != 1.0) return;
    if (any(asuint(Captures[base+1u]) != asuint(IniParams[113]))) return;
    uint count,stride=32u;
    if (IniParams[114].y > 0.5) { BlendTyped.GetDimensions(count); count/=2u; }
    else Blend.GetDimensions(count,stride);
    uint vertex=Nearest[1]; if (vertex>=count || stride!=32u) return;
    JF_BlendVertex v;
    if (IniParams[114].y > 0.5) { v.weights=asfloat(BlendTyped[vertex*2u]); v.indices=BlendTyped[vertex*2u+1u]; }
    else v=Blend[vertex];
    if (any(!isfinite(v.weights)) || any(v.weights<0.0) || dot(v.weights,1.0)<JF_BINDING_EPSILON) return;
    Captures[base+2u]=(float4)v.indices;
    Captures[base+3u]=v.weights;
    Captures[base+4u]=float4(Model[3].xyz,1.0);
    Captures[base]=float4((float)generation,2.0,(float)vertex,0.0);
}
