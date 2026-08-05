// Captures normalized source UV from the native role-composition pipeline.
// The native cdc90aee00e7900d VS exports COLOR0 in o1 and TEXCOORD0 in o2;
// retaining the color input keeps the custom PS registers aligned exactly.

float4 main(
    float4 position : SV_Position,
    float4 inputColor : COLOR0,
    float2 sourceUV : TEXCOORD0)
    : SV_Target0
{
    return float4(sourceUV, 1.0, 1.0);
}
