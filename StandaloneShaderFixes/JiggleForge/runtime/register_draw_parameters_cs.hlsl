#include "motion_model.hlsl"

Buffer<uint> StateIndices : register(t72);
Buffer<float4> SourceParameters : register(t75);
RWBuffer<float4> GlobalParameters : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint listIndex = dispatchThreadId.x;
    uint stateCount;
    StateIndices.GetDimensions(stateCount);
    if (listIndex >= stateCount)
    {
        return;
    }

    uint sourceCount;
    SourceParameters.GetDimensions(sourceCount);
    if (sourceCount < 5u)
    {
        return;
    }

    uint stateIndex = StateIndices[listIndex];
    uint destinationBase = stateIndex * 5u;
    uint destinationCount;
    GlobalParameters.GetDimensions(destinationCount);
    if (destinationBase + 5u > destinationCount)
    {
        return;
    }

    float4 p0 = SourceParameters[0u];
    float4 p1 = SourceParameters[1u];
    float4 p2 = SourceParameters[2u];
    float4 p3 = SourceParameters[3u];
    float4 p4 = SourceParameters[4u];
    p0.x = (float)(stateIndex + 1u);
    JF_GroupParameters parameters =
        JF_DecodeGroupParameters(p0, p1, p2, p3, p4);
    if (parameters.Valid == 0u)
    {
        return;
    }

    GlobalParameters[destinationBase + 0u] = p0;
    GlobalParameters[destinationBase + 1u] = p1;
    GlobalParameters[destinationBase + 2u] = p2;
    GlobalParameters[destinationBase + 3u] = p3;
    GlobalParameters[destinationBase + 4u] = p4;
}
