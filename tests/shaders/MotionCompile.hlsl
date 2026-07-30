#include "motion_model.hlsl"

RWStructuredBuffer<float4> OutputRecords : register(u0);

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
    {
        return;
    }

    float4 p0 = float4(17.0f, 2.0f, 0.25f, 0.7f);
    float4 p1 = float4(2.2f, 0.3f, 0.75f, 0.15f);
    float4 p2 = float4(0.3f, 4.0f, 0.84f, 2.2f);
    float4 p3 = float4(0.9f, 0.12f, 0.02f, 0.0f);
    float4 p4 = float4(0.15f, 1.0f, -1.0f, 1.0f);
    JF_GroupParameters parameters =
        JF_DecodeGroupParameters(p0, p1, p2, p3, p4);

    JF_InputFrame input;
    input.CursorPixels = float2(600.0f, 500.0f);
    input.ViewportPixels = float2(1000.0f, 1000.0f);
    input.DragHeld = 1u;
    input.WheelTowardSequence = 0u;
    input.WheelAwaySequence = 0u;
    input.DeltaSeconds = 1.0f / 60.0f;

    JF_CapturedPick capture = (JF_CapturedPick)0;
    capture.Valid = 1u;
    capture.ObjectId = 17u;
    capture.Generation = 1u;
    capture.SourceDraw = 4u;
    capture.PressCursorPixels = float2(500.0f, 500.0f);
    capture.WorldPosition = float3(1.0f, 2.0f, 3.0f);
    capture.ScreenRight = float3(1.0f, 0.0f, 0.0f);
    capture.ScreenUp = float3(0.0f, 1.0f, 0.0f);

    JF_MotionState state = (JF_MotionState)0;
    JF_StepMotion(state, 17u, input, capture, parameters);

    float4 m0;
    float4 m1;
    float4 m2;
    float4 m3;
    float4 m4;
    float4 m5;
    float4 m6;
    JF_EncodeMotionState(state, m0, m1, m2, m3, m4, m5, m6);

    OutputRecords[0] = m0;
    OutputRecords[1] = m1;
    OutputRecords[2] = m2;
    OutputRecords[3] = m3;
    OutputRecords[4] = m4;
    OutputRecords[5] = m5;
    OutputRecords[6] = m6;
}
