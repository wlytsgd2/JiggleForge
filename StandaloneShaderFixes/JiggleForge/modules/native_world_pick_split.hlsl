// Coordinate-only picker for supported native body/material vertex shaders.
// The replacement VS exports the undeformed world position and the camera
// screen basis through TEXCOORD9/10/11.

#ifdef JIGGLEFORGE_1F6_LAYOUT

struct VS_OUT
{
    // The semi-transparent shader packs TEXCOORD5.x and TEXCOORD8.yzw into
    // one native output register. Declaring the two semantics separately
    // preserves that packing while leaving the added picker semantics intact.
    float4 texcoord0 : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 texcoord3 : TEXCOORD3;
    float4 texcoord4 : TEXCOORD4;
    float texcoord5 : TEXCOORD5;
    float3 texcoord8 : TEXCOORD8;
    float4 texcoord7 : TEXCOORD7;
    float3 jiggleForgeWorldPosition : TEXCOORD9;
    float3 jiggleForgeScreenRight : TEXCOORD10;
    float3 jiggleForgeScreenUp : TEXCOORD11;
    float4 pos : SV_Position;
};

#else

struct VS_OUT
{
    // Shader-stage values are connected through output/input registers. Keep
    // the complete native VS register order so TEXCOORD2/3/4 remain o2/o3/o4
    // and SV_Position remains the final o9 register.
    float4 texcoord0 : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
    float4 texcoord3 : TEXCOORD3;
    float4 texcoord4 : TEXCOORD4;
    float4 texcoord5 : TEXCOORD5;
    float4 texcoord6 : TEXCOORD6;
    float4 texcoord7 : TEXCOORD7;
    float3 texcoord8 : TEXCOORD8;
    float3 jiggleForgeWorldPosition : TEXCOORD9;
    float3 jiggleForgeScreenRight : TEXCOORD10;
    float3 jiggleForgeScreenUp : TEXCOORD11;
    float4 pos : SV_Position;
};

#endif

struct GS_OUT
{
    float4 pos : SV_Position;
    float3 sourcePosition : TEXCOORD0;
    float3 barycentric : TEXCOORD1;
    nointerpolation float4 objectData : TEXCOORD2;
    nointerpolation float4 indexData : TEXCOORD3;
    nointerpolation float4 normalData : TEXCOORD4;
    nointerpolation float4 rightData : TEXCOORD5;
    nointerpolation float4 upData : TEXCOORD6;
    nointerpolation float4 distanceData : TEXCOORD7;
};

struct PS_OUT
{
    float4 slot0 : SV_Target0;
    float4 slot1 : SV_Target1;
    float4 slot2 : SV_Target2;
    float4 slot3 : SV_Target3;
    float4 slot4 : SV_Target4;
    float4 slot5 : SV_Target5;
    float4 slot6 : SV_Target6;
    float4 slot7 : SV_Target7;
};

Texture1D<float4> IniParams : register(t120);
Buffer<uint> gIndexBuffer : register(t1);
Texture2D<float4> CursorCalibrationMap : register(t2);

#define CURSOR_PARAMS IniParams[24]
#define DRAW_PARAMS IniParams[26]
#define INDEX_PARAMS IniParams[27]
#define CALIBRATION_PARAMS IniParams[28]

float4 SampleCalibrationMap(float2 screenUV)
{
    uint width;
    uint height;
    CursorCalibrationMap.GetDimensions(width, height);
    if (width == 0u || height == 0u)
        return 0.0;

    // Global cursor Y is bottom-up; texture rows are addressed top-down.
    float2 textureUV = saturate(float2(screenUV.x, 1.0 - screenUV.y));
    float2 texelPosition = textureUV * float2(width, height) - 0.5;
    int2 baseTexel = int2(floor(texelPosition));
    float2 fraction = frac(texelPosition);
    int2 maximumTexel = int2(width - 1u, height - 1u);
    int2 p00 = clamp(baseTexel, int2(0, 0), maximumTexel);
    int2 p10 = clamp(baseTexel + int2(1, 0), int2(0, 0), maximumTexel);
    int2 p01 = clamp(baseTexel + int2(0, 1), int2(0, 0), maximumTexel);
    int2 p11 = clamp(baseTexel + int2(1, 1), int2(0, 0), maximumTexel);
    float4 top = lerp(
        CursorCalibrationMap.Load(int3(p00, 0)),
        CursorCalibrationMap.Load(int3(p10, 0)),
        fraction.x);
    float4 bottom = lerp(
        CursorCalibrationMap.Load(int3(p01, 0)),
        CursorCalibrationMap.Load(int3(p11, 0)),
        fraction.x);
    return lerp(top, bottom, fraction.y);
}

float3 SafeNormalize(float3 value, float3 fallback)
{
    float lengthSquared = dot(value, value);
    if (lengthSquared <= 1e-12)
        return fallback;
    return value * rsqrt(lengthSquared);
}

float Cross2(float2 a, float2 b)
{
    return a.x * b.y - a.y * b.x;
}

float2 ClipToUV(float4 clipPosition)
{
    float safeW = abs(clipPosition.w) > 1e-6
        ? clipPosition.w
        : (clipPosition.w < 0.0 ? -1e-6 : 1e-6);
    float2 ndc = clipPosition.xy / safeW;
    return float2(ndc.x * 0.5 + 0.5, 0.5 - ndc.y * 0.5);
}

float3 ReadWorldPosition(VS_OUT input)
{
    return input.jiggleForgeWorldPosition;
}

#ifdef GEOMETRY_SHADER

[maxvertexcount(3)]
void main(
    triangle VS_OUT input[3],
    uint primitiveID : SV_PrimitiveID,
    inout TriangleStream<GS_OUT> stream)
{
    float3 q0 = ReadWorldPosition(input[0]);
    float3 q1 = ReadWorldPosition(input[1]);
    float3 q2 = ReadWorldPosition(input[2]);
    float3 triangleCenterWorld = (q0 + q1 + q2) / 3.0;

    uint firstIndex = (uint)max(INDEX_PARAMS.x, 0.0);
    int baseVertex = (int)INDEX_PARAMS.y;
    uint indexBase = firstIndex + primitiveID * 3u;
    uint indexCount;
    gIndexBuffer.GetDimensions(indexCount);
    if (indexBase + 2u >= indexCount)
        return;

    int a0 = (int)gIndexBuffer[indexBase + 0u] + baseVertex;
    int a1 = (int)gIndexBuffer[indexBase + 1u] + baseVertex;
    int a2 = (int)gIndexBuffer[indexBase + 2u] + baseVertex;
    if (a0 < 0 || a1 < 0 || a2 < 0)
        return;

    uint i0 = (uint)a0;
    uint i1 = (uint)a1;
    uint i2 = (uint)a2;

    float2 s0 = ClipToUV(input[0].pos);
    float2 s1 = ClipToUV(input[1].pos);
    float2 s2 = ClipToUV(input[2].pos);
    float2 e1 = s1 - s0;
    float2 e2 = s2 - s0;
    float det = Cross2(e1, e2);

    float3 faceNormal = SafeNormalize(
        cross(q1 - q0, q2 - q0),
        float3(0.0, 0.0, 1.0));
    float3 exportedRight = input[0].jiggleForgeScreenRight
        + input[1].jiggleForgeScreenRight
        + input[2].jiggleForgeScreenRight;
    float3 exportedUp = input[0].jiggleForgeScreenUp
        + input[1].jiggleForgeScreenUp
        + input[2].jiggleForgeScreenUp;
    float3 screenRight = SafeNormalize(exportedRight, float3(1.0, 0.0, 0.0));
    float3 orthogonalUp = exportedUp
        - screenRight * dot(exportedUp, screenRight);
    float3 screenUp = SafeNormalize(orthogonalUp, float3(0.0, 1.0, 0.0));
    float directionValid = dot(exportedRight, exportedRight) > 1e-10
        && dot(orthogonalUp, orthogonalUp) > 1e-10
        ? 1.0
        : 0.0;

    // Retain the triangle-plane reconstruction only as a compatibility
    // fallback. Its cross product is the face normal and therefore must not
    // be used when the replacement VS supplied the real camera screen basis.
    if (directionValid < 0.5 && abs(det) > 1e-10)
    {
        float3 qe1 = q1 - q0;
        float3 qe2 = q2 - q0;
        screenRight = SafeNormalize(
            qe1 * (e2.y / det) + qe2 * (-e1.y / det),
            screenRight);
        screenUp = SafeNormalize(
            -(qe1 * (-e2.x / det) + qe2 * (e1.x / det)),
            screenUp);
        directionValid = 1.0;
    }

    float2 screenSize = max(CURSOR_PARAMS.zw, float2(1.0, 1.0));
    float2 cursorUV = saturate(CURSOR_PARAMS.xy / screenSize);
    float capturedRoute = CALIBRATION_PARAMS.x;
    if (capturedRoute > 0.0)
    {
        float4 calibrated = SampleCalibrationMap(cursorUV);
        if (calibrated.z > 0.5)
            cursorUV = calibrated.xy;
        else
            cursorUV = float2(-10.0, -10.0);
    }
    else
    {
        // No fixed scene profile or pixel offset is allowed here. A scene
        // without a preceding-frame composition map must fail closed so its
        // missing calibration remains visible during testing.
        cursorUV = float2(-10.0, -10.0);
    }
    float2 cursorNDC = float2(cursorUV.x * 2.0 - 1.0, 1.0 - cursorUV.y * 2.0);
    float winding = det < 0.0 ? -1.0 : 1.0;
    float3 barycentrics[3] = {
        float3(1.0, 0.0, 0.0),
        float3(0.0, 1.0, 0.0),
        float3(0.0, 0.0, 1.0)
    };

    [unroll]
    for (uint vertex = 0u; vertex < 3u; ++vertex)
    {
        GS_OUT output;
        output.pos = input[vertex].pos;
        output.pos.xy -= cursorNDC * output.pos.w;
        output.sourcePosition = triangleCenterWorld;
        output.barycentric = barycentrics[vertex];
        output.objectData = float4(
            DRAW_PARAMS.z,
            DRAW_PARAMS.y,
            DRAW_PARAMS.x,
            DRAW_PARAMS.w);
        output.indexData = float4((float)i0, (float)i1, (float)i2, (float)indexBase);
        output.normalData = float4(faceNormal, winding);
        output.rightData = float4(screenRight, directionValid);
        output.upData = float4(screenUp, directionValid);
        output.distanceData = output.indexData;
        stream.Append(output);
    }
}

#endif

#ifdef PIXEL_SHADER

PS_OUT main(GS_OUT input)
{
    PS_OUT output;
    output.slot0 = float4(input.objectData.x, 1.0 - input.pos.z, input.objectData.z, 1.0);
    output.slot1 = float4(input.sourcePosition, 50.0);
    output.slot2 = float4(
        INDEX_PARAMS.x,
        input.indexData.w,
        input.objectData.y,
        input.objectData.w);
    output.slot3 = input.distanceData;
    output.slot4 = float4(input.barycentric, 1.0);
    output.slot5 = input.normalData;
    output.slot6 = input.rightData;
    output.slot7 = input.upData;
    return output;
}

#endif
