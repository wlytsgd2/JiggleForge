Texture1D<float4> IniParams : register(t120);
Texture2D<float4> CalibrationMap : register(t0);
RWBuffer<float4> InlineCursor : register(u0);

#define CURSOR_VIEWPORT IniParams[80]
#define CALIBRATION_ROUTE IniParams[90].x

float4 SampleCalibrationMap(float2 screenUV)
{
    uint width;
    uint height;
    CalibrationMap.GetDimensions(width, height);
    if (width == 0u || height == 0u)
        return 0.0;

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
        CalibrationMap.Load(int3(p00, 0)),
        CalibrationMap.Load(int3(p10, 0)),
        fraction.x);
    float4 bottom = lerp(
        CalibrationMap.Load(int3(p01, 0)),
        CalibrationMap.Load(int3(p11, 0)),
        fraction.x);
    return lerp(top, bottom, fraction.y);
}

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
        return;

    float2 viewport = max(CURSOR_VIEWPORT.zw, float2(1.0, 1.0));
    float2 screenUV = saturate(CURSOR_VIEWPORT.xy / viewport);
    float4 result = float4(screenUV, 0.0, CALIBRATION_ROUTE);
    if (CALIBRATION_ROUTE > 0.0)
    {
        float4 calibrated = SampleCalibrationMap(screenUV);
        if (calibrated.z > 0.5)
            result = float4(calibrated.xy, 1.0, CALIBRATION_ROUTE);
    }
    InlineCursor[0] = result;
}
