#include "motion_model.hlsl"

RWStructuredBuffer<float4> OutputRecords : register(u0);

static const uint JF_PARITY_SCENARIO_COUNT = 6u;

JF_GroupParameters JF_ParityDefaultParameters()
{
    float4 p0 = float4(17.0f, 2.0f, 0.25f, 0.7f);
    float4 p1 = float4(2.2f, 0.3f, 0.75f, 0.15f);
    float4 p2 = float4(0.02f, 10.0f, 0.84f, 2.2f);
    float4 p3 = float4(0.9f, 0.12f, 0.02f, 0.0f);
    float4 p4 = float4(0.15f, 1.0f, -1.0f, 1.0f);
    return JF_DecodeGroupParameters(p0, p1, p2, p3, p4);
}

JF_InputFrame JF_ParityInput(
    float2 cursor,
    uint held,
    uint towardSequence,
    uint awaySequence)
{
    JF_InputFrame input;
    input.CursorPixels = cursor;
    input.ViewportPixels = float2(1000.0f, 1000.0f);
    input.DragHeld = held;
    input.WheelTowardSequence = towardSequence;
    input.WheelAwaySequence = awaySequence;
    input.DeltaSeconds = 1.0f / 60.0f;
    return input;
}

JF_CapturedPick JF_ParityPick(uint valid)
{
    JF_CapturedPick capture = (JF_CapturedPick)0;
    capture.Valid = valid;
    capture.ObjectId = 17u;
    capture.Generation = valid != 0u ? 1u : 0u;
    capture.SourceDraw = 4u;
    capture.PressCursorPixels = float2(500.0f, 500.0f);
    capture.WorldPosition = float3(1.0f, 2.0f, 3.0f);
    capture.ScreenRight = float3(1.0f, 0.0f, 0.0f);
    capture.ScreenUp = float3(0.0f, 1.0f, 0.0f);
    capture.SurfaceNormal = float3(0.0f, 0.6f, 0.8f);
    capture.HoldSeconds = 1.0f / 60.0f;
    return capture;
}

void JF_StoreParityState(uint scenario, JF_MotionState state)
{
    float4 m0;
    float4 m1;
    float4 m2;
    float4 m3;
    float4 m4;
    float4 m5;
    float4 m6;
    JF_EncodeMotionState(state, m0, m1, m2, m3, m4, m5, m6);

    uint baseRecord = scenario * JF_STATE_RECORD_COUNT;
    OutputRecords[baseRecord + 0u] = m0;
    OutputRecords[baseRecord + 1u] = m1;
    OutputRecords[baseRecord + 2u] = m2;
    OutputRecords[baseRecord + 3u] = m3;
    OutputRecords[baseRecord + 4u] = m4;
    OutputRecords[baseRecord + 5u] = m5;
    OutputRecords[baseRecord + 6u] = m6;
}

JF_MotionState JF_RunIdleScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    JF_InputFrame input = JF_ParityInput(
        float2(500.0f, 500.0f),
        0u,
        0u,
        0u);
    JF_CapturedPick capture = JF_ParityPick(0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    return state;
}

JF_MotionState JF_RunPlanarDragScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    JF_CapturedPick capture = JF_ParityPick(1u);
    JF_InputFrame input = JF_ParityInput(
        float2(500.0f, 500.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    input = JF_ParityInput(
        float2(620.0f, 540.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    return state;
}

JF_MotionState JF_RunWheelScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    JF_CapturedPick capture = JF_ParityPick(1u);
    JF_InputFrame input = JF_ParityInput(
        float2(500.0f, 500.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    input = JF_ParityInput(
        float2(500.0f, 500.0f),
        1u,
        3u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    return state;
}

JF_MotionState JF_RunReleaseScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    JF_CapturedPick capture = JF_ParityPick(1u);
    JF_InputFrame input = JF_ParityInput(
        float2(500.0f, 500.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    input = JF_ParityInput(
        float2(650.0f, 500.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    input = JF_ParityInput(
        float2(650.0f, 500.0f),
        0u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    return state;
}

JF_MotionState JF_RunClampScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    parameters.Strength = 10.0f;
    parameters.DragScale = 100.0f;
    parameters.MaxOffset = 0.05f;
    parameters.TargetFollowSeconds = 0.0f;
    parameters.HoldFrequencyHz = 60.0f;
    JF_CapturedPick capture = JF_ParityPick(1u);
    capture.PressCursorPixels = 0.0f;
    capture.WorldPosition = 0.0f;
    JF_InputFrame input = JF_ParityInput(0.0f, 1u, 0u, 0u);
    JF_StepMotion(state, 17u, input, capture, parameters);

    [loop]
    for (uint index = 0u; index < 4u; index++)
    {
        input = JF_ParityInput(1000.0f, 1u, 0u, 0u);
        JF_StepMotion(state, 17u, input, capture, parameters);
    }

    return state;
}

JF_MotionState JF_RunTapScenario()
{
    JF_MotionState state = (JF_MotionState)0;
    JF_GroupParameters parameters = JF_ParityDefaultParameters();
    JF_CapturedPick capture = JF_ParityPick(1u);
    JF_InputFrame input = JF_ParityInput(
        float2(500.0f, 500.0f),
        1u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    capture.HoldSeconds = 2.0f / 60.0f;
    input = JF_ParityInput(
        float2(504.0f, 500.0f),
        0u,
        0u,
        0u);
    JF_StepMotion(state, 17u, input, capture, parameters);
    return state;
}

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
    {
        return;
    }

    JF_StoreParityState(0u, JF_RunIdleScenario());
    JF_StoreParityState(1u, JF_RunPlanarDragScenario());
    JF_StoreParityState(2u, JF_RunWheelScenario());
    JF_StoreParityState(3u, JF_RunReleaseScenario());
    JF_StoreParityState(4u, JF_RunClampScenario());
    JF_StoreParityState(5u, JF_RunTapScenario());
}
