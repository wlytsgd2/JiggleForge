// Builds the drag-captured Draw Inspector text for an adapted Mod.

Buffer<float4> Detect : register(t0);
Buffer<uint> Labels : register(t1);
Buffer<uint> ObjectIDs : register(t2);
RWBuffer<uint> OutputText : register(u0);
Texture1D<float4> IniParams : register(t120);

void AppendUInt(inout uint cursor, uint value)
{
    uint divisor = 1u;
    uint probe = value;
    while (probe >= 10u)
    {
        probe /= 10u;
        divisor *= 10u;
    }

    do
    {
        uint digit = value / divisor;
        OutputText[cursor++] = 48u + digit;
        value -= digit * divisor;
        divisor /= 10u;
    }
    while (divisor > 0u);
}

[numthreads(1, 1, 1)]
void main(uint3 threadID : SV_DispatchThreadID)
{
    [loop]
    for (uint clearIndex = 0u; clearIndex < 256u; ++clearIndex)
        OutputText[clearIndex] = 0u;

    uint labelStride = (uint)max(IniParams[31].x, 0.0);
    uint drawCount = (uint)max(IniParams[31].y, 0.0);
    uint originalPartsNumber = (uint)max(IniParams[31].z, 0.0);
    uint valid = Detect[0u].w > 0.5 ? 1u : 0u;
    uint drawNumber = (uint)round(max(Detect[2u].w, 0.0));
    uint objectID = (uint)round(max(Detect[1u].w, 0.0));
    uint cursor = 0u;

    if (drawNumber == 0u && objectID == 1u)
        drawNumber = originalPartsNumber;

    if (valid == 0u || drawNumber < 1u || drawNumber > drawCount || labelStride == 0u ||
        ObjectIDs[drawNumber - 1u] != objectID)
    {
        uint message[15] = { 78u, 111u, 32u, 97u, 100u, 97u, 112u, 116u, 101u, 100u, 32u, 100u, 114u, 97u, 119u };
        [unroll]
        for (uint messageIndex = 0u; messageIndex < 15u; ++messageIndex)
            OutputText[cursor++] = message[messageIndex];
        OutputText[cursor] = 0u;
        return;
    }

    uint labelBase = (drawNumber - 1u) * labelStride;
    [loop]
    for (uint labelIndex = 0u; labelIndex < labelStride; ++labelIndex)
    {
        uint character = Labels[labelBase + labelIndex];
        if (character == 0u)
            break;
        OutputText[cursor++] = character;
    }

    OutputText[cursor++] = 10u;
    uint triangleText[9] = { 84u, 114u, 105u, 97u, 110u, 103u, 108u, 101u, 32u };
    [unroll]
    for (uint triangleIndex = 0u; triangleIndex < 9u; ++triangleIndex)
        OutputText[cursor++] = triangleText[triangleIndex];
    AppendUInt(cursor, (uint)round(max(Detect[3u].z, 0.0)));

    uint vertexText[12] = { 32u, 124u, 32u, 86u, 101u, 114u, 116u, 105u, 99u, 101u, 115u, 32u };
    [unroll]
    for (uint vertexIndex = 0u; vertexIndex < 12u; ++vertexIndex)
        OutputText[cursor++] = vertexText[vertexIndex];
    float4 indices = Detect[4u];
    AppendUInt(cursor, (uint)round(max(indices.x, 0.0)));
    OutputText[cursor++] = 44u;
    AppendUInt(cursor, (uint)round(max(indices.y, 0.0)));
    OutputText[cursor++] = 44u;
    AppendUInt(cursor, (uint)round(max(indices.z, 0.0)));
    OutputText[cursor] = 0u;
}
