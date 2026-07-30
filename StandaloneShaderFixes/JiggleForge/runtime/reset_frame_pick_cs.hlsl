// Clears the transient eight-record pick packet after the controller has
// consumed it. Each lane owns one record, so no record-copy or loop state is
// carried over from the previous frame.

RWBuffer<float4> FramePickRecords : register(u0);

static const uint JF_FRAME_PICK_RECORD_COUNT = 8u;

float4 JF_EmptyPickRecord(uint recordIndex)
{
    if (recordIndex == 0u)
    {
        float maximumFiniteDepth = asfloat(0x7f7fffffu);
        return float4(-1.0f, maximumFiniteDepth, 0.0f, 0.0f);
    }

    return 0.0f;
}

[numthreads(8, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint availableRecords;
    FramePickRecords.GetDimensions(availableRecords);

    uint recordIndex = dispatchThreadId.x;
    if (recordIndex >= JF_FRAME_PICK_RECORD_COUNT
        || recordIndex >= availableRecords)
    {
        return;
    }

    FramePickRecords[recordIndex] = JF_EmptyPickRecord(recordIndex);
}
