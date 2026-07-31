#include "input_controller.hlsl"

Buffer<float4> ControllerRecords : register(t0);
Buffer<float4> CapturedPickRecords : register(t1);
Buffer<float4> MotionStateRecords : register(t2);
Buffer<float4> GroupParameterRecords : register(t3);
RWBuffer<uint> OutputText : register(u0);

static const uint JF_DIAGNOSTIC_CAPACITY = 256u;

void JF_AppendCharacter(inout uint cursor, uint character)
{
    if (cursor + 1u < JF_DIAGNOSTIC_CAPACITY)
    {
        OutputText[cursor++] = character;
    }
}

void JF_AppendUnsigned(inout uint cursor, uint value)
{
    uint divisor = 1000000000u;
    bool started = false;
    [unroll]
    for (uint index = 0u; index < 10u; ++index)
    {
        uint digit = value / divisor;
        if (started || digit > 0u || divisor == 1u)
        {
            JF_AppendCharacter(cursor, 48u + digit);
            started = true;
        }
        value -= digit * divisor;
        divisor /= 10u;
    }
}

void JF_AppendSignedFixed3(inout uint cursor, float value)
{
    float safeValue = clamp(JF_FiniteOr(value, 0.0f), -9999.999f, 9999.999f);
    if (safeValue < 0.0f)
    {
        JF_AppendCharacter(cursor, 45u);
        safeValue = -safeValue;
    }
    else
    {
        JF_AppendCharacter(cursor, 43u);
    }

    uint scaled = (uint)round(safeValue * 1000.0f);
    JF_AppendUnsigned(cursor, scaled / 1000u);
    JF_AppendCharacter(cursor, 46u);
    uint fractional = scaled % 1000u;
    JF_AppendCharacter(cursor, 48u + (fractional / 100u));
    JF_AppendCharacter(cursor, 48u + ((fractional / 10u) % 10u));
    JF_AppendCharacter(cursor, 48u + (fractional % 10u));
}

void JF_AppendVector3(inout uint cursor, float3 value)
{
    JF_AppendSignedFixed3(cursor, value.x);
    JF_AppendCharacter(cursor, 44u);
    JF_AppendSignedFixed3(cursor, value.y);
    JF_AppendCharacter(cursor, 44u);
    JF_AppendSignedFixed3(cursor, value.z);
}

void JF_AppendHeader(inout uint cursor)
{
    uint label[10] =
    {
        74u, 70u, 32u, 82u, 85u, 78u, 84u, 73u, 77u, 69u
    };
    [unroll]
    for (uint index = 0u; index < 10u; ++index)
    {
        JF_AppendCharacter(cursor, label[index]);
    }
    JF_AppendCharacter(cursor, 10u);
}

void JF_AppendCaptureLine(
    inout uint cursor,
    JF_InputControllerState controller,
    JF_CapturedPick capture)
{
    JF_AppendCharacter(cursor, 67u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, capture.Valid);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendCharacter(cursor, 72u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, controller.PreviousHeld);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendCharacter(cursor, 71u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, controller.CaptureGeneration);
    JF_AppendCharacter(cursor, 10u);
}

void JF_AppendIdentityLine(
    inout uint cursor,
    JF_CapturedPick capture)
{
    JF_AppendCharacter(cursor, 79u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, capture.ObjectId);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendCharacter(cursor, 68u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, capture.SourceDraw);
    JF_AppendCharacter(cursor, 10u);
}

void JF_AppendStateLine(
    inout uint cursor,
    uint stateIndex,
    uint parametersValid,
    uint stateActive)
{
    JF_AppendCharacter(cursor, 83u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, stateIndex);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendCharacter(cursor, 86u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, parametersValid);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendCharacter(cursor, 65u);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendUnsigned(cursor, stateActive);
    JF_AppendCharacter(cursor, 10u);
}

void JF_AppendWorldLine(
    inout uint cursor,
    uint label,
    float3 value)
{
    JF_AppendCharacter(cursor, label);
    JF_AppendCharacter(cursor, 32u);
    JF_AppendVector3(cursor, value);
    JF_AppendCharacter(cursor, 10u);
}

[numthreads(1, 1, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (any(dispatchThreadId != 0u))
    {
        return;
    }

    uint outputCount;
    OutputText.GetDimensions(outputCount);
    if (outputCount < JF_DIAGNOSTIC_CAPACITY)
    {
        return;
    }

    [loop]
    for (uint index = 0u; index < JF_DIAGNOSTIC_CAPACITY; ++index)
    {
        OutputText[index] = 0u;
    }

    uint controllerCount;
    ControllerRecords.GetDimensions(controllerCount);
    uint captureCount;
    CapturedPickRecords.GetDimensions(captureCount);
    uint motionCount;
    MotionStateRecords.GetDimensions(motionCount);
    uint parameterCount;
    GroupParameterRecords.GetDimensions(parameterCount);
    if (controllerCount < JF_CONTROLLER_RECORD_COUNT
        || captureCount < JF_CAPTURED_PICK_RECORD_COUNT
        || motionCount < JF_STATE_RECORD_COUNT
        || parameterCount < 5u)
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
            CapturedPickRecords[5u],
            CapturedPickRecords[6u]);
    uint stateIndex = capture.ObjectId > 0u
        ? capture.ObjectId - 1u
        : 0u;
    uint motionBase = stateIndex * JF_STATE_RECORD_COUNT;
    uint parameterBase = stateIndex * 5u;
    if (motionBase + JF_STATE_RECORD_COUNT > motionCount
        || parameterBase + 5u > parameterCount)
    {
        return;
    }
    JF_MotionState state = JF_DecodeMotionState(
        MotionStateRecords[motionBase + 0u],
        MotionStateRecords[motionBase + 1u],
        MotionStateRecords[motionBase + 2u],
        MotionStateRecords[motionBase + 3u],
        MotionStateRecords[motionBase + 4u],
        MotionStateRecords[motionBase + 5u],
        MotionStateRecords[motionBase + 6u]);
    JF_GroupParameters parameters = JF_DecodeGroupParameters(
        GroupParameterRecords[parameterBase + 0u],
        GroupParameterRecords[parameterBase + 1u],
        GroupParameterRecords[parameterBase + 2u],
        GroupParameterRecords[parameterBase + 3u],
        GroupParameterRecords[parameterBase + 4u]);

    uint cursor = 0u;
    JF_AppendHeader(cursor);
    JF_AppendCaptureLine(cursor, controller, capture);
    JF_AppendIdentityLine(cursor, capture);
    JF_AppendStateLine(
        cursor,
        stateIndex,
        parameters.Valid,
        state.Active);
    JF_AppendWorldLine(cursor, 80u, capture.WorldPosition);
    JF_AppendWorldLine(cursor, 77u, state.Position);
    OutputText[min(cursor, JF_DIAGNOSTIC_CAPACITY - 1u)] = 0u;
}
