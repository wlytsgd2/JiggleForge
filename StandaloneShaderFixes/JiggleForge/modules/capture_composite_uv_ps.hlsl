// Captures the final composition draw as a normalized screen-to-source map.
// TEXCOORD0 is the source texture UV produced by both supported composition
// vertex shaders. Z/W are validity markers; untouched pixels remain zero.

float4 main(
    float4 position : SV_Position,
    float2 sourceUV : TEXCOORD0)
    : SV_Target0
{
    return float4(sourceUV, 1.0, 1.0);
}
