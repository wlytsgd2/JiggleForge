// Reconstructs the selected role-composition quad directly from a raw copy of
// its shared UI vertex buffer. This avoids replaying the original indexed draw
// while retaining the game's native position and UV transformations.

cbuffer cb3 : register(b3)
{
    float4 cb3[21];
}

cbuffer cb2 : register(b2)
{
    float4 cb2[4];
}

cbuffer cb0 : register(b0)
{
    float4 cb0[14];
}

ByteAddressBuffer RoleCompositeVertices : register(t110);
Texture1D<float4> IniParams : register(t120);

void main(
    uint vertexId : SV_VertexID,
    out float4 outputPosition : SV_Position,
    out float4 outputColor : COLOR0,
    out float2 outputUV : TEXCOORD0)
{
    static const uint quadIndices[6] = { 0u, 1u, 2u, 2u, 3u, 0u };
    float4 bufferParameters = IniParams[94];
    uint firstVertex = (uint)max(bufferParameters.x, 0.0);
    uint stride = (uint)max(bufferParameters.y, 1.0);
    uint sourceVertex = firstVertex + quadIndices[vertexId];
    uint sourceAddress = sourceVertex * stride;

    float3 inputPosition = asfloat(
        RoleCompositeVertices.Load3(sourceAddress));
    float2 inputUV = asfloat(
        RoleCompositeVertices.Load2(sourceAddress + 44u));

    float4 localPosition = cb2[1] * inputPosition.y;
    localPosition = cb2[0] * inputPosition.x + localPosition;
    localPosition = cb2[2] * inputPosition.z + localPosition;
    localPosition = cb2[3] + localPosition;

    float4 clipPosition = cb3[18] * localPosition.y;
    clipPosition = cb3[17] * localPosition.x + clipPosition;
    clipPosition = cb3[19] * localPosition.z + clipPosition;
    clipPosition = cb3[20] * localPosition.w + clipPosition;

    outputPosition = clipPosition;
    outputColor = 1.0;
    outputUV = inputUV * cb0[12].xy + cb0[12].zw;
}
