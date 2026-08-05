// Captures source UV only when the current shared-UI input is the role texture
// produced earlier in the same frame. Several fixed texels form a lightweight
// GPU-side fingerprint, avoiding resolution-specific resource hashes.

Texture2D<float4> CandidateTexture : register(t110);
Texture2D<float4> RoleTexture : register(t111);

bool FingerprintMatches()
{
    uint candidateWidth;
    uint candidateHeight;
    uint roleWidth;
    uint roleHeight;
    CandidateTexture.GetDimensions(candidateWidth, candidateHeight);
    RoleTexture.GetDimensions(roleWidth, roleHeight);

    if (candidateWidth == 0u || candidateHeight == 0u ||
        candidateWidth != roleWidth || candidateHeight != roleHeight)
    {
        return false;
    }

    static const float2 sampleUV[8] =
    {
        float2(0.127f, 0.193f),
        float2(0.503f, 0.109f),
        float2(0.841f, 0.227f),
        float2(0.293f, 0.467f),
        float2(0.671f, 0.531f),
        float2(0.157f, 0.809f),
        float2(0.479f, 0.887f),
        float2(0.863f, 0.743f)
    };

    [unroll]
    for (uint i = 0u; i < 8u; ++i)
    {
        uint2 texel = min(
            uint2(sampleUV[i] * float2(roleWidth, roleHeight)),
            uint2(roleWidth - 1u, roleHeight - 1u));
        float4 candidate = CandidateTexture.Load(int3(texel, 0));
        float4 reference = RoleTexture.Load(int3(texel, 0));
        if (any(abs(candidate - reference) > 1.0e-6f))
        {
            return false;
        }
    }

    return true;
}

float4 main(
    float4 position : SV_Position,
    float4 inputColor : COLOR0,
    float2 sourceUV : TEXCOORD0)
    : SV_Target0
{
    if (!FingerprintMatches())
    {
        discard;
    }

    return float4(sourceUV, 1.0, 1.0);
}
