#include "binding_layout.hlsl"
Buffer<float4> CapturedPick : register(t0);
Buffer<uint> Nearest : register(t1);
StructuredBuffer<JF_BlendVertex> Blend : register(t2);
Buffer<uint4> BlendTyped : register(t3);
RWBuffer<float4> Captures : register(u0);
RWBuffer<uint> DiagnosticScan : register(u2);
Texture1D<float4> IniParams : register(t120);
cbuffer ObjectTransform : register(b0) { float4 Model[29]; }
#include "size_estimate.hlsl"

groupshared uint ResolveValid;
groupshared uint ResolveCount;
groupshared uint ResolveVertex;
groupshared uint ResolveBase;
groupshared uint ResolveGeneration;
groupshared JF_BlendVertex ResolveSeed;

void JF_PrepareResolve()
{
    ResolveValid = 0u;
    ResolveCount = 0u;
    ResolveSeed = (JF_BlendVertex)0;
    uint pickCount, nearestCount, captureCount;
    CapturedPick.GetDimensions(pickCount);
    Nearest.GetDimensions(nearestCount);
    Captures.GetDimensions(captureCount);
    if (pickCount < 4u || nearestCount < 2u) return;
    float generationValue = CapturedPick[3].w;
    if (!isfinite(generationValue) || generationValue < 0.0) return;
    uint generation = (uint)generationValue;
    uint base = JF_BindingBase(generation);
    if (base + JF_BINDING_STRIDE > captureCount) return;
    if (CapturedPick[0].w < 0.5 || Captures[base].x != (float)generation
        || Captures[base].y != 1.0) return;
    if (any(asuint(Captures[base + 1u]) != asuint(IniParams[113]))) return;

    uint count, stride = 32u;
    if (IniParams[114].y > 0.5)
    {
        BlendTyped.GetDimensions(count);
        count /= 2u;
    }
    else Blend.GetDimensions(count, stride);

    uint diagnostics;
    DiagnosticScan.GetDimensions(diagnostics);
    bool diagnosticValid = false;
    if (diagnostics >= 32u) diagnosticValid = DiagnosticScan[0] == generation;
    uint vertex = Nearest[1];
    if (diagnosticValid) DiagnosticScan[12] = vertex;
    if (vertex >= count || stride != 32u)
    {
        if (diagnosticValid) DiagnosticScan[11] = 1u;
        return;
    }

    JF_BlendVertex v;
    if (IniParams[114].y > 0.5)
    {
        v.weights = asfloat(BlendTyped[vertex * 2u]);
        v.indices = BlendTyped[vertex * 2u + 1u];
    }
    else v = Blend[vertex];
    float weightSum = dot(v.weights, 1.0);
    if (any(!isfinite(v.weights)) || any(v.weights < 0.0)
        || !isfinite(weightSum) || weightSum < JF_BINDING_EPSILON)
    {
        if (diagnosticValid)
            DiagnosticScan[11] = any(!isfinite(v.weights)) || !isfinite(weightSum)
                ? 2u : any(v.weights < 0.0) ? 3u : 4u;
        return;
    }
    if (diagnosticValid) DiagnosticScan[11] = 5u;
    ResolveCount = count;
    ResolveVertex = vertex;
    ResolveBase = base;
    ResolveGeneration = generation;
    ResolveSeed = v;
    ResolveValid = 1u;
}

[numthreads(128, 1, 1)]
void main(uint3 tid : SV_GroupThreadID)
{
    if (tid.x == 0u) JF_PrepareResolve();
    GroupMemoryBarrierWithGroupSync();
    // The existing indirect arguments dispatch one group. Every lane reaches
    // every reduction barrier, even for disabled or rejected captures.
    bool enabled = ResolveValid != 0u && IniParams[115].z > 0.5 && IniParams[115].y <= 0.5;
    float scale = JF_EstimateSize(tid.x, ResolveSeed, ResolveCount, enabled);
    if (tid.x != 0u || ResolveValid == 0u) return;

    uint base = ResolveBase;
    Captures[base + 2u] = (float4)ResolveSeed.indices;
    Captures[base + 3u] = ResolveSeed.weights;
    Captures[base + 4u] = float4(Model[3].xyz, 1.0);
    // Freeze scale for this capture without changing the five-float4 ABI.
    // A failed/disabled size estimate remains a successful binding resolve.
    Captures[base] = float4((float)ResolveGeneration, 2.0, (float)ResolveVertex, scale);
}
