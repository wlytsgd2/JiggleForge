#include "input_controller.hlsl"

Buffer<float4> ControllerRecords : register(t0);
Buffer<float4> CapturedPickRecords : register(t1);
Buffer<float4> GroupParameterRecords : register(t2);
RWBuffer<float4> MotionStateRecords : register(u0);
Texture1D<float4> IniParams : register(t120);

#define JF_CURSOR_VIEWPORT IniParams[80]
#define JF_INPUT_TIME IniParams[81]

[numthreads(64, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint stateIndex = dispatchThreadId.x;
    uint controllerCount;
    ControllerRecords.GetDimensions(controllerCount);
    uint capturedCount;
    CapturedPickRecords.GetDimensions(capturedCount);
    uint parameterCount;
    GroupParameterRecords.GetDimensions(parameterCount);
    uint motionCount;
    MotionStateRecords.GetDimensions(motionCount);
    uint parameterBase = stateIndex * 5u;
    uint motionBase = stateIndex * JF_STATE_RECORD_COUNT;
    if (controllerCount < JF_CONTROLLER_RECORD_COUNT
        || capturedCount < JF_CAPTURED_PICK_RECORD_COUNT
        || parameterBase + 5u > parameterCount
        || motionBase + JF_STATE_RECORD_COUNT > motionCount)
    {
        return;
    }

    JF_GroupParameters parameters = JF_DecodeGroupParameters(
        GroupParameterRecords[parameterBase + 0u],
        GroupParameterRecords[parameterBase + 1u],
        GroupParameterRecords[parameterBase + 2u],
        GroupParameterRecords[parameterBase + 3u],
        GroupParameterRecords[parameterBase + 4u]);
    if (parameters.Valid == 0u)
    {
        return;
    }

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
        CapturedPickRecords[5u]);
    JF_MotionState state = JF_DecodeMotionState(
        MotionStateRecords[motionBase + 0u],
        MotionStateRecords[motionBase + 1u],
        MotionStateRecords[motionBase + 2u],
        MotionStateRecords[motionBase + 3u],
        MotionStateRecords[motionBase + 4u],
        MotionStateRecords[motionBase + 5u],
        MotionStateRecords[motionBase + 6u]);

    JF_InputFrame input;
    input.CursorPixels = controller.CurrentCursorPixels;
    input.ViewportPixels = JF_CURSOR_VIEWPORT.zw;
    input.DragHeld = controller.PreviousHeld;
    input.WheelTowardSequence =
        (uint)max(controller.WheelSequenceCode, 0);
    input.WheelAwaySequence =
        (uint)max(-controller.WheelSequenceCode, 0);
    input.DeltaSeconds = JF_INPUT_TIME.z;
    JF_StepMotion(
        state,
        parameters.ObjectId,
        input,
        capture,
        parameters);

    float4 m0;
    float4 m1;
    float4 m2;
    float4 m3;
    float4 m4;
    float4 m5;
    float4 m6;
    JF_EncodeMotionState(state, m0, m1, m2, m3, m4, m5, m6);
    MotionStateRecords[motionBase + 0u] = m0;
    MotionStateRecords[motionBase + 1u] = m1;
    MotionStateRecords[motionBase + 2u] = m2;
    MotionStateRecords[motionBase + 3u] = m3;
    MotionStateRecords[motionBase + 4u] = m4;
    MotionStateRecords[motionBase + 5u] = m5;
    MotionStateRecords[motionBase + 6u] = m6;
}
