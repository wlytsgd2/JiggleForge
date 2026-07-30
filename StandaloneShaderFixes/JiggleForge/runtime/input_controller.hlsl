#ifndef JIGGLEFORGE_RUNTIME_INPUT_CONTROLLER
#define JIGGLEFORGE_RUNTIME_INPUT_CONTROLLER

#include "motion_model.hlsl"

static const uint JF_CONTROLLER_RECORD_COUNT = 2u;
static const uint JF_CAPTURED_PICK_RECORD_COUNT = 6u;
static const uint JF_MAXIMUM_EXACT_GENERATION = 0x007fffffu;

struct JF_PickRecord
{
    uint Valid;
    uint ObjectId;
    uint SourceDraw;
    float3 WorldPosition;
    float3 ScreenRight;
    float3 ScreenUp;
    float Depth;
    float Priority;
    float PipelineToken;
    uint TriangleOrdinal;
    uint3 TriangleIndices;
    float3 Barycentric;
};

struct JF_InputControllerState
{
    float2 PressCursorPixels;
    float2 CurrentCursorPixels;
    uint PreviousHeld;
    uint CaptureGeneration;
    int WheelSequenceCode;
    uint CurrentPickValid;
};

JF_InputControllerState JF_DecodeInputControllerState(
    float4 c0,
    float4 c1)
{
    JF_InputControllerState result;
    result.PressCursorPixels = JF_FiniteOr2(c0.xy, 0.0f);
    result.CurrentCursorPixels = JF_FiniteOr2(c1.xy, 0.0f);
    result.PreviousHeld = c0.z > 0.5f;
    result.CaptureGeneration = (uint)clamp(
        round(JF_FiniteOr(c0.w, 0.0f)),
        0.0f,
        (float)JF_MAXIMUM_EXACT_GENERATION);
    result.WheelSequenceCode = (int)clamp(
        round(JF_FiniteOr(c1.z, 0.0f)),
        -16777215.0f,
        16777215.0f);
    result.CurrentPickValid = c1.w > 0.5f;
    return result;
}

void JF_EncodeInputControllerState(
    JF_InputControllerState state,
    out float4 c0,
    out float4 c1)
{
    c0 = float4(
        state.PressCursorPixels,
        state.PreviousHeld != 0u ? 1.0f : 0.0f,
        (float)state.CaptureGeneration);
    c1 = float4(
        state.CurrentCursorPixels,
        (float)state.WheelSequenceCode,
        state.CurrentPickValid != 0u ? 1.0f : 0.0f);
}

JF_CapturedPick JF_DecodeCapturedPick(
    float4 q0,
    float4 q1,
    float4 q2,
    float4 q3,
    float4 q4,
    float4 q5)
{
    JF_CapturedPick result;
    result.Valid = 0u;
    result.ObjectId = 0u;
    result.Generation = 0u;
    result.SourceDraw = 0u;
    result.PressCursorPixels = 0.0f;
    result.WorldPosition = 0.0f;
    result.ScreenRight = 0.0f;
    result.ScreenUp = 0.0f;
    result.Depth = 0.0f;
    result.Priority = 0.0f;
    result.TriangleOrdinal = 0u;
    result.TriangleIndices = 0u;
    result.Barycentric = 0.0f;
    result.WorldPosition = JF_FiniteOr3(q0.xyz, 0.0f);
    result.Valid = q0.w > 0.5f;
    result.ScreenRight = JF_FiniteOr3(
        q1.xyz,
        float3(1.0f, 0.0f, 0.0f));
    result.ObjectId = (uint)max(
        round(JF_FiniteOr(q1.w, 0.0f)),
        0.0f);
    result.ScreenUp = JF_FiniteOr3(
        q2.xyz,
        float3(0.0f, 1.0f, 0.0f));
    result.SourceDraw = (uint)max(
        round(JF_FiniteOr(q2.w, 0.0f)),
        0.0f);
    result.Depth = JF_FiniteOr(q3.x, 0.0f);
    result.Priority = JF_FiniteOr(q3.y, 0.0f);
    result.TriangleOrdinal = (uint)max(
        round(JF_FiniteOr(q3.z, 0.0f)),
        0.0f);
    result.Generation = (uint)clamp(
        round(JF_FiniteOr(q3.w, 0.0f)),
        0.0f,
        (float)JF_MAXIMUM_EXACT_GENERATION);
    result.TriangleIndices = uint3(
        max(round(JF_FiniteOr(q4.x, 0.0f)), 0.0f),
        max(round(JF_FiniteOr(q4.y, 0.0f)), 0.0f),
        max(round(JF_FiniteOr(q4.z, 0.0f)), 0.0f));
    result.PressCursorPixels.x = JF_FiniteOr(q4.w, 0.0f);
    result.Barycentric = JF_FiniteOr3(q5.xyz, 0.0f);
    result.PressCursorPixels.y = JF_FiniteOr(q5.w, 0.0f);
    return result;
}

void JF_EncodeCapturedPick(
    JF_CapturedPick capture,
    out float4 q0,
    out float4 q1,
    out float4 q2,
    out float4 q3,
    out float4 q4,
    out float4 q5)
{
    q0 = float4(
        capture.WorldPosition,
        capture.Valid != 0u ? 1.0f : 0.0f);
    q1 = float4(capture.ScreenRight, (float)capture.ObjectId);
    q2 = float4(capture.ScreenUp, (float)capture.SourceDraw);
    q3 = float4(
        capture.Depth,
        capture.Priority,
        (float)capture.TriangleOrdinal,
        (float)capture.Generation);
    q4 = float4(
        (float3)capture.TriangleIndices,
        capture.PressCursorPixels.x);
    q5 = float4(capture.Barycentric, capture.PressCursorPixels.y);
}

uint JF_NextCaptureGeneration(uint currentGeneration)
{
    return currentGeneration >= JF_MAXIMUM_EXACT_GENERATION
        ? 1u
        : currentGeneration + 1u;
}

JF_CapturedPick JF_FreezePick(
    JF_PickRecord currentPick,
    float2 pressCursorPixels,
    uint generation)
{
    JF_CapturedPick result;
    result.Valid = 0u;
    result.ObjectId = 0u;
    result.Generation = generation;
    result.SourceDraw = 0u;
    result.PressCursorPixels = pressCursorPixels;
    result.WorldPosition = 0.0f;
    result.ScreenRight = 0.0f;
    result.ScreenUp = 0.0f;
    result.Depth = 0.0f;
    result.Priority = 0.0f;
    result.TriangleOrdinal = 0u;
    result.TriangleIndices = 0u;
    result.Barycentric = 0.0f;
    result.Valid = currentPick.Valid;
    if (currentPick.Valid != 0u)
    {
        result.ObjectId = currentPick.ObjectId;
        result.SourceDraw = currentPick.SourceDraw;
        result.WorldPosition = JF_FiniteOr3(
            currentPick.WorldPosition,
            0.0f);
        float3 right = float3(1.0f, 0.0f, 0.0f);
        float3 up = float3(0.0f, 1.0f, 0.0f);
        JF_BuildOrthonormalBasis(
            currentPick.ScreenRight,
            currentPick.ScreenUp,
            right,
            up);
        result.ScreenRight = right;
        result.ScreenUp = up;
        result.Depth = JF_FiniteOr(currentPick.Depth, 0.0f);
        result.Priority = JF_FiniteOr(currentPick.Priority, 0.0f);
        result.TriangleOrdinal = currentPick.TriangleOrdinal;
        result.TriangleIndices = currentPick.TriangleIndices;
        result.Barycentric = JF_FiniteOr3(
            currentPick.Barycentric,
            0.0f);
    }
    return result;
}

void JF_UpdateInputController(
    inout JF_InputControllerState controller,
    inout JF_CapturedPick capturedPick,
    JF_InputFrame input,
    JF_PickRecord currentPick)
{
    float2 currentCursor = JF_FiniteOr2(input.CursorPixels, 0.0f);
    bool held = input.DragHeld != 0u;
    bool risingEdge = held && controller.PreviousHeld == 0u;
    if (risingEdge)
    {
        controller.CaptureGeneration = JF_NextCaptureGeneration(
            controller.CaptureGeneration);
        controller.PressCursorPixels = currentCursor;
        capturedPick = JF_FreezePick(
            currentPick,
            currentCursor,
            controller.CaptureGeneration);
    }

    controller.CurrentCursorPixels = currentCursor;
    controller.WheelSequenceCode = JF_WheelSequenceCode(
        input.WheelTowardSequence,
        input.WheelAwaySequence);
    controller.CurrentPickValid = currentPick.Valid;
    controller.PreviousHeld = held ? 1u : 0u;
}

#endif
