// The one-thread comparison pass computes the expensive texture fingerprint
// once per candidate. Every calibration pixel only reads this cached result.

Buffer<uint> RoleTextureMatch : register(t111);

float4 main(
    float4 position : SV_Position,
    float4 inputColor : COLOR0,
    float2 sourceUV : TEXCOORD0)
    : SV_Target0
{
    if (RoleTextureMatch[0] == 0u)
    {
        discard;
    }

    return float4(sourceUV, 1.0, 1.0);
}
