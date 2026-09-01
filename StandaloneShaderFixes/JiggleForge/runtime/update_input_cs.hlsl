#include "pick_decoder.hlsl"

Buffer<float4> SourceFramePick : register(t0);
RWBuffer<float4> ControllerRecords : register(u0);
RWBuffer<float4> CapturedPickRecords : register(u1);
RWBuffer<float4> FrameContextRecords : register(u2);
Texture1D<float4> IniParams : register(t120);

#define JF_CURSOR_VIEWPORT IniParams[80]
#define JF_INPUT_TIME IniParams[81]

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
    {
        return;
    }

    uint sourceCount;
    SourceFramePick.GetDimensions(sourceCount);
    uint controllerCount;
    ControllerRecords.GetDimensions(controllerCount);
    uint capturedCount;
    CapturedPickRecords.GetDimensions(capturedCount);
    uint frameContextCount;
    FrameContextRecords.GetDimensions(frameContextCount);
    if (sourceCount < JF_SOURCE_FRAME_PICK_RECORD_COUNT
        || controllerCount < JF_CONTROLLER_RECORD_COUNT
        || capturedCount < JF_CAPTURED_PICK_RECORD_COUNT
        || frameContextCount < 1u)
    {
        return;
    }

    JF_PickRecord currentPick = JF_DecodeSourceFramePick(
        SourceFramePick[0u],
        SourceFramePick[1u],
        SourceFramePick[2u],
        SourceFramePick[3u],
        SourceFramePick[4u],
        SourceFramePick[5u],
        SourceFramePick[6u],
        SourceFramePick[7u],
        SourceFramePick[8u],
        SourceFramePick[9u]);
    JF_InputControllerState controller =
        JF_DecodeInputControllerState(
            ControllerRecords[0u],
            ControllerRecords[1u]);
    JF_CapturedPick capture = JF_DecodeCapturedPick(
        CapturedPickRecords[0u],
        CapturedPickRecords[1u],
        CapturedPickRecords[2u],
        CapturedPickRecords[3u],
        CapturedPickRecords[4u],
        CapturedPickRecords[5u],
        CapturedPickRecords[6u],
        CapturedPickRecords[7u],
        CapturedPickRecords[8u]);

    JF_InputFrame input;
    input.CursorPixels = JF_CURSOR_VIEWPORT.xy;
    input.ViewportPixels = JF_CURSOR_VIEWPORT.zw;
    input.DragHeld = JF_INPUT_TIME.x > 0.5f;
    int wheelCode = (int)clamp(
        round(JF_FiniteOr(JF_INPUT_TIME.y, 0.0f)),
        -8388607.0f,
        8388607.0f);
    input.WheelTowardSequence = (uint)max(wheelCode, 0);
    input.WheelAwaySequence = (uint)max(-wheelCode, 0);
    input.DeltaSeconds = JF_INPUT_TIME.z;

    JF_UpdateInputController(
        controller,
        capture,
        input,
        currentPick);

    float4 c0;
    float4 c1;
    JF_EncodeInputControllerState(controller, c0, c1);
    ControllerRecords[0u] = c0;
    ControllerRecords[1u] = c1;
    FrameContextRecords[0u] = float4(
        JF_FiniteOr2(input.ViewportPixels, 0.0f),
        clamp(
            JF_FiniteOr(input.DeltaSeconds, 1.0f / 60.0f),
            JF_MINIMUM_DELTA_SECONDS,
            JF_MAXIMUM_DELTA_SECONDS),
        1.0f);

    float4 q0;
    float4 q1;
    float4 q2;
    float4 q3;
    float4 q4;
    float4 q5;
    float4 q6;
    float4 q7;
    float4 q8;
    JF_EncodeCapturedPick(capture, q0, q1, q2, q3, q4, q5, q6, q7, q8);
    CapturedPickRecords[0u] = q0;
    CapturedPickRecords[1u] = q1;
    CapturedPickRecords[2u] = q2;
    CapturedPickRecords[3u] = q3;
    CapturedPickRecords[4u] = q4;
    CapturedPickRecords[5u] = q5;
    CapturedPickRecords[6u] = q6;
    CapturedPickRecords[7u] = q7;
    CapturedPickRecords[8u] = q8;
}
