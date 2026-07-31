#include "input_controller.hlsl"

RWStructuredBuffer<float4> OutputRecords : register(u0);

static const uint JF_INPUT_PARITY_SCENARIO_COUNT = 5u;
static const uint JF_INPUT_PARITY_RECORD_COUNT =
    JF_CONTROLLER_RECORD_COUNT + JF_CAPTURED_PICK_RECORD_COUNT;

JF_InputFrame JF_InputParityFrame(
    float2 cursor,
    uint held,
    uint toward,
    uint away)
{
    JF_InputFrame input;
    input.CursorPixels = cursor;
    input.ViewportPixels = 1000.0f;
    input.DragHeld = held;
    input.WheelTowardSequence = toward;
    input.WheelAwaySequence = away;
    input.DeltaSeconds = 1.0f / 60.0f;
    return input;
}

JF_PickRecord JF_InputParityPick(
    uint valid,
    uint objectId,
    uint sourceDraw)
{
    JF_PickRecord pick = (JF_PickRecord)0;
    pick.Valid = valid;
    pick.ObjectId = objectId;
    pick.SourceDraw = sourceDraw;
    pick.WorldPosition = float3(1.0f, 2.0f, 3.0f);
    pick.ScreenRight = float3(2.0f, 0.0f, 0.0f);
    pick.ScreenUp = float3(2.0f, 3.0f, 0.0f);
    pick.SurfaceNormal = float3(0.0f, 0.6f, 0.8f);
    pick.Depth = 0.42f;
    pick.Priority = 3.0f;
    pick.PipelineToken = 91.0f;
    pick.TriangleOrdinal = 7u;
    pick.TriangleIndices = uint3(10u, 20u, 30u);
    pick.Barycentric = float3(0.2f, 0.3f, 0.5f);
    return pick;
}

void JF_StoreInputParity(
    uint scenario,
    JF_InputControllerState controller,
    JF_CapturedPick capture)
{
    float4 c0;
    float4 c1;
    JF_EncodeInputControllerState(controller, c0, c1);
    float4 q0;
    float4 q1;
    float4 q2;
    float4 q3;
    float4 q4;
    float4 q5;
    float4 q6;
    JF_EncodeCapturedPick(capture, q0, q1, q2, q3, q4, q5, q6);

    uint baseRecord = scenario * JF_INPUT_PARITY_RECORD_COUNT;
    OutputRecords[baseRecord + 0u] = c0;
    OutputRecords[baseRecord + 1u] = c1;
    OutputRecords[baseRecord + 2u] = q0;
    OutputRecords[baseRecord + 3u] = q1;
    OutputRecords[baseRecord + 4u] = q2;
    OutputRecords[baseRecord + 5u] = q3;
    OutputRecords[baseRecord + 6u] = q4;
    OutputRecords[baseRecord + 7u] = q5;
    OutputRecords[baseRecord + 8u] = q6;
}

void JF_RunIdleInputScenario(
    out JF_InputControllerState controller,
    out JF_CapturedPick capture)
{
    controller = (JF_InputControllerState)0;
    capture = (JF_CapturedPick)0;
    JF_InputFrame input = JF_InputParityFrame(
        float2(50.0f, 60.0f),
        0u,
        0u,
        0u);
    JF_PickRecord pick = JF_InputParityPick(0u, 0u, 0u);
    JF_UpdateInputController(controller, capture, input, pick);
}

void JF_RunValidPressScenario(
    out JF_InputControllerState controller,
    out JF_CapturedPick capture)
{
    controller = (JF_InputControllerState)0;
    capture = (JF_CapturedPick)0;
    JF_InputFrame input = JF_InputParityFrame(
        float2(100.0f, 200.0f),
        1u,
        0u,
        0u);
    JF_PickRecord pick = JF_InputParityPick(1u, 17u, 4u);
    JF_UpdateInputController(controller, capture, input, pick);
}

void JF_RunHeldChangedPickScenario(
    out JF_InputControllerState controller,
    out JF_CapturedPick capture)
{
    controller = (JF_InputControllerState)0;
    capture = (JF_CapturedPick)0;
    JF_InputFrame input = JF_InputParityFrame(
        float2(100.0f, 200.0f),
        1u,
        0u,
        0u);
    JF_PickRecord pick = JF_InputParityPick(1u, 17u, 4u);
    JF_UpdateInputController(controller, capture, input, pick);
    input = JF_InputParityFrame(
        float2(300.0f, 400.0f),
        1u,
        7u,
        2u);
    pick = JF_InputParityPick(1u, 99u, 8u);
    JF_UpdateInputController(controller, capture, input, pick);
}

void JF_RunReleaseScenario(
    out JF_InputControllerState controller,
    out JF_CapturedPick capture)
{
    controller = (JF_InputControllerState)0;
    capture = (JF_CapturedPick)0;
    JF_InputFrame input = JF_InputParityFrame(
        float2(100.0f, 100.0f),
        1u,
        0u,
        0u);
    JF_PickRecord pick = JF_InputParityPick(1u, 17u, 4u);
    JF_UpdateInputController(controller, capture, input, pick);
    input = JF_InputParityFrame(
        float2(120.0f, 120.0f),
        0u,
        0u,
        0u);
    pick = JF_InputParityPick(1u, 99u, 8u);
    JF_UpdateInputController(controller, capture, input, pick);
}

void JF_RunInvalidRepressScenario(
    out JF_InputControllerState controller,
    out JF_CapturedPick capture)
{
    controller = (JF_InputControllerState)0;
    capture = (JF_CapturedPick)0;
    JF_InputFrame input = JF_InputParityFrame(
        float2(100.0f, 100.0f),
        1u,
        0u,
        0u);
    JF_PickRecord pick = JF_InputParityPick(1u, 17u, 4u);
    JF_UpdateInputController(controller, capture, input, pick);
    input = JF_InputParityFrame(
        float2(120.0f, 120.0f),
        0u,
        0u,
        0u);
    JF_UpdateInputController(controller, capture, input, pick);
    input = JF_InputParityFrame(
        float2(200.0f, 200.0f),
        1u,
        0u,
        0u);
    pick = JF_InputParityPick(0u, 0u, 0u);
    JF_UpdateInputController(controller, capture, input, pick);
}

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
    {
        return;
    }

    JF_InputControllerState controller;
    JF_CapturedPick capture;

    JF_RunIdleInputScenario(controller, capture);
    JF_StoreInputParity(0u, controller, capture);

    JF_RunValidPressScenario(controller, capture);
    JF_StoreInputParity(1u, controller, capture);

    JF_RunHeldChangedPickScenario(controller, capture);
    JF_StoreInputParity(2u, controller, capture);

    JF_RunReleaseScenario(controller, capture);
    JF_StoreInputParity(3u, controller, capture);

    JF_RunInvalidRepressScenario(controller, capture);
    JF_StoreInputParity(4u, controller, capture);
}
