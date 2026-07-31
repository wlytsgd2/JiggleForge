#ifndef JIGGLEFORGE_RUNTIME_FRAME_PICK_ADAPTER
#define JIGGLEFORGE_RUNTIME_FRAME_PICK_ADAPTER

#include "input_controller.hlsl"

static const uint JF_SOURCE_FRAME_PICK_RECORD_COUNT = 8u;

JF_PickRecord JF_DecodeSourceFramePick(
    float4 s0,
    float4 s1,
    float4 s2,
    float4 s3,
    float4 s4,
    float4 s5,
    float4 s6,
    float4 s7)
{
    JF_PickRecord result;
    result.Valid = 0u;
    result.ObjectId = 0u;
    result.SourceDraw = 0u;
    result.WorldPosition = 0.0f;
    result.ScreenRight = float3(1.0f, 0.0f, 0.0f);
    result.ScreenUp = float3(0.0f, 1.0f, 0.0f);
    result.SurfaceNormal = float3(0.0f, 0.0f, 1.0f);
    result.Depth = 0.0f;
    result.Priority = 0.0f;
    result.PipelineToken = 0.0f;
    result.TriangleOrdinal = 0u;
    result.TriangleIndices = 0u;
    result.Barycentric = 0.0f;
    bool identityValid =
        JF_IsFinite(s0.x)
        && s0.x > 0.0f
        && s0.w > 0.5f;
    bool basisValid = s6.w > 0.5f && s7.w > 0.5f;
    result.Valid = identityValid && basisValid;
    if (result.Valid != 0u)
    {
        result.ObjectId = (uint)max(round(s0.x), 0.0f);
        result.Depth = JF_FiniteOr(s0.y, 0.0f);
        result.Priority = JF_FiniteOr(s0.z, 0.0f);
        result.WorldPosition = JF_FiniteOr3(s1.xyz, 0.0f);
        result.SourceDraw = (uint)max(
            round(JF_FiniteOr(s2.z, 0.0f)),
            0.0f);
        result.PipelineToken = JF_FiniteOr(s2.w, 0.0f);
        result.TriangleOrdinal = (uint)max(
            round(
                (JF_FiniteOr(s2.y, 0.0f)
                    - JF_FiniteOr(s2.x, 0.0f))
                / 3.0f),
            0.0f);
        result.TriangleIndices = uint3(
            max(round(JF_FiniteOr(s3.x, 0.0f)), 0.0f),
            max(round(JF_FiniteOr(s3.y, 0.0f)), 0.0f),
            max(round(JF_FiniteOr(s3.z, 0.0f)), 0.0f));
        result.Barycentric = JF_FiniteOr3(s4.xyz, 0.0f);
        result.SurfaceNormal = JF_SafeNormalize(
            JF_FiniteOr3(s5.xyz, float3(0.0f, 0.0f, 1.0f)),
            float3(0.0f, 0.0f, 1.0f));
        float3 right = float3(1.0f, 0.0f, 0.0f);
        float3 up = float3(0.0f, 1.0f, 0.0f);
        JF_BuildOrthonormalBasis(
            s6.xyz,
            s7.xyz,
            right,
            up);
        result.ScreenRight = right;
        result.ScreenUp = up;
    }
    return result;
}

#endif
