#ifndef JIGGLEFORGE_RUNTIME_MOTION_SOLVER
#define JIGGLEFORGE_RUNTIME_MOTION_SOLVER

static const uint JF_SCHEMA_VERSION = 2u;
static const uint JF_STATE_RECORD_COUNT = 7u;
static const float JF_MINIMUM_DELTA_SECONDS = 1.0f / 240.0f;
static const float JF_MAXIMUM_DELTA_SECONDS = 0.05f;
static const float JF_VECTOR_EPSILON = 1.0e-8f;
static const float JF_SLEEP_POSITION_SQUARED = 1.0e-8f;
static const float JF_SLEEP_VELOCITY_SQUARED = 1.0e-8f;
static const uint JF_SLEEP_FRAME_THRESHOLD = 12u;
static const float JF_TAP_MAXIMUM_HOLD_SECONDS = 0.20f;
static const float JF_TAP_MAXIMUM_CURSOR_DISTANCE = 10.0f;

struct JF_GroupParameters
{
    uint ObjectId;
    uint SchemaVersion;
    float Radius;
    float Strength;
    float Falloff;
    float VolumeResponse;
    float DragScale;
    float MaxOffset;
    float TargetFollowSeconds;
    float HoldFrequencyHz;
    float HoldDampingRatio;
    float ReleaseFrequencyHz;
    float ReleaseDampingRatio;
    float ReleaseImpulse;
    float WheelDepthStep;
    float WheelMinDepth;
    float WheelMaxDepth;
    float MouseXSign;
    float MouseYSign;
    uint Valid;
};

struct JF_InputFrame
{
    float2 CursorPixels;
    float2 ViewportPixels;
    uint DragHeld;
    uint WheelTowardSequence;
    uint WheelAwaySequence;
    float DeltaSeconds;
};

struct JF_CapturedPick
{
    uint Valid;
    uint ObjectId;
    uint Generation;
    uint SourceDraw;
    float2 PressCursorPixels;
    float3 WorldPosition;
    float3 ScreenRight;
    float3 ScreenUp;
    float3 SurfaceNormal;
    float HoldSeconds;
    float Depth;
    float Priority;
    uint TriangleOrdinal;
    uint3 TriangleIndices;
    float3 Barycentric;
};

struct JF_MotionState
{
    float3 Position;
    float3 Velocity;
    float3 Anchor;
    float3 ScreenRight;
    float3 ScreenUp;
    float DepthTarget;
    float3 FilteredTarget;
    float3 PreviousFilteredTarget;
    uint Active;
    uint WasHeld;
    uint OwnerObjectId;
    uint SourceDraw;
    uint CaptureGeneration;
    int LastWheelSequenceCode;
    uint SleepFrames;
};

bool JF_IsFinite(float value)
{
    return (asuint(value) & 0x7f800000u) != 0x7f800000u;
}

bool JF_IsFinite2(float2 value)
{
    return JF_IsFinite(value.x) && JF_IsFinite(value.y);
}

bool JF_IsFinite3(float3 value)
{
    return JF_IsFinite(value.x)
        && JF_IsFinite(value.y)
        && JF_IsFinite(value.z);
}

float JF_FiniteOr(float value, float fallback)
{
    return JF_IsFinite(value) ? value : fallback;
}

float2 JF_FiniteOr2(float2 value, float2 fallback)
{
    return JF_IsFinite2(value) ? value : fallback;
}

float3 JF_FiniteOr3(float3 value, float3 fallback)
{
    return JF_IsFinite3(value) ? value : fallback;
}

float JF_SafeMaximumOffset(float value)
{
    return clamp(JF_FiniteOr(value, 0.0f), 0.0f, 100.0f);
}

float JF_SignOrOne(float value)
{
    return JF_FiniteOr(value, 1.0f) < 0.0f ? -1.0f : 1.0f;
}

float3 JF_SafeNormalize(float3 value, float3 fallback)
{
    float lengthSquared = dot(value, value);
    if (!JF_IsFinite(lengthSquared)
        || lengthSquared <= JF_VECTOR_EPSILON)
    {
        return fallback;
    }

    return value * rsqrt(lengthSquared);
}

float3 JF_OrthogonalFallback(float3 direction)
{
    float3 candidate = abs(direction.y) < 0.9f
        ? float3(0.0f, 1.0f, 0.0f)
        : float3(1.0f, 0.0f, 0.0f);
    float3 rejected = candidate - direction * dot(candidate, direction);
    return JF_SafeNormalize(rejected, float3(0.0f, 1.0f, 0.0f));
}

void JF_BuildOrthonormalBasis(
    float3 sourceRight,
    float3 sourceUp,
    out float3 right,
    out float3 up)
{
    right = JF_SafeNormalize(
        JF_FiniteOr3(sourceRight, float3(1.0f, 0.0f, 0.0f)),
        float3(1.0f, 0.0f, 0.0f));
    float3 safeUp = JF_FiniteOr3(sourceUp, float3(0.0f, 1.0f, 0.0f));
    float3 rejectedUp = safeUp - right * dot(safeUp, right);
    up = JF_SafeNormalize(rejectedUp, JF_OrthogonalFallback(right));
}

float3 JF_ClampMagnitude(float3 value, float maximum)
{
    float lengthSquared = dot(value, value);
    if (!JF_IsFinite(lengthSquared))
    {
        return 0.0f;
    }

    float maximumSquared = maximum * maximum;
    if (lengthSquared > maximumSquared && lengthSquared > JF_VECTOR_EPSILON)
    {
        return value * (maximum * rsqrt(lengthSquared));
    }

    return value;
}

int JF_WheelSequenceCode(uint toward, uint away)
{
    int safeToward = (int)min(toward, 0x007fffffu);
    int safeAway = (int)min(away, 0x007fffffu);
    return safeToward - safeAway;
}

float JF_PackHoldMetadata(uint wasHeld, uint captureGeneration)
{
    uint safeGeneration = min(captureGeneration, 0x007fffffu);
    return (float)(safeGeneration * 2u + min(wasHeld, 1u));
}

void JF_UnpackHoldMetadata(
    float packedValue,
    out uint wasHeld,
    out uint captureGeneration)
{
    uint packed = (uint)max(round(JF_FiniteOr(packedValue, 0.0f)), 0.0f);
    wasHeld = packed & 1u;
    captureGeneration = packed >> 1u;
}

JF_GroupParameters JF_DecodeGroupParameters(
    float4 p0,
    float4 p1,
    float4 p2,
    float4 p3,
    float4 p4)
{
    JF_GroupParameters result;
    result.ObjectId = (uint)max(round(JF_FiniteOr(p0.x, 0.0f)), 0.0f);
    result.SchemaVersion = (uint)max(round(JF_FiniteOr(p0.y, 0.0f)), 0.0f);
    result.Radius = JF_FiniteOr(p0.z, 0.0f);
    result.Strength = JF_FiniteOr(p0.w, 0.0f);
    result.Falloff = JF_FiniteOr(p1.x, 0.0f);
    result.VolumeResponse = JF_FiniteOr(p1.y, 0.0f);
    result.DragScale = JF_FiniteOr(p1.z, 0.0f);
    result.MaxOffset = JF_FiniteOr(p1.w, 0.0f);
    result.TargetFollowSeconds = JF_FiniteOr(p2.x, 0.0f);
    result.HoldFrequencyHz = JF_FiniteOr(p2.y, 0.0f);
    result.HoldDampingRatio = JF_FiniteOr(p2.z, 1.0f);
    result.ReleaseFrequencyHz = JF_FiniteOr(p2.w, 0.0f);
    result.ReleaseDampingRatio = JF_FiniteOr(p3.x, 1.0f);
    result.ReleaseImpulse = JF_FiniteOr(p3.y, 0.0f);
    result.WheelDepthStep = JF_FiniteOr(p3.z, 0.0f);
    result.WheelMinDepth = JF_FiniteOr(p3.w, 0.0f);
    result.WheelMaxDepth = JF_FiniteOr(p4.x, 0.0f);
    result.MouseXSign = JF_FiniteOr(p4.y, 1.0f);
    result.MouseYSign = JF_FiniteOr(p4.z, -1.0f);
    result.Valid = p4.w > 0.5f && result.SchemaVersion == JF_SCHEMA_VERSION;
    return result;
}

void JF_EncodeGroupParameters(
    JF_GroupParameters parameters,
    out float4 p0,
    out float4 p1,
    out float4 p2,
    out float4 p3,
    out float4 p4)
{
    p0 = float4(
        (float)parameters.ObjectId,
        (float)parameters.SchemaVersion,
        parameters.Radius,
        parameters.Strength);
    p1 = float4(
        parameters.Falloff,
        parameters.VolumeResponse,
        parameters.DragScale,
        parameters.MaxOffset);
    p2 = float4(
        parameters.TargetFollowSeconds,
        parameters.HoldFrequencyHz,
        parameters.HoldDampingRatio,
        parameters.ReleaseFrequencyHz);
    p3 = float4(
        parameters.ReleaseDampingRatio,
        parameters.ReleaseImpulse,
        parameters.WheelDepthStep,
        parameters.WheelMinDepth);
    p4 = float4(
        parameters.WheelMaxDepth,
        parameters.MouseXSign,
        parameters.MouseYSign,
        parameters.Valid != 0u ? 1.0f : 0.0f);
}

JF_MotionState JF_DecodeMotionState(
    float4 m0,
    float4 m1,
    float4 m2,
    float4 m3,
    float4 m4,
    float4 m5,
    float4 m6)
{
    JF_MotionState result;
    result.Position = JF_FiniteOr3(m0.xyz, 0.0f);
    result.Velocity = JF_FiniteOr3(m1.xyz, 0.0f);
    result.Anchor = JF_FiniteOr3(m2.xyz, 0.0f);
    result.ScreenRight = JF_FiniteOr3(m3.xyz, float3(1.0f, 0.0f, 0.0f));
    result.ScreenUp = JF_FiniteOr3(m4.xyz, float3(0.0f, 1.0f, 0.0f));
    result.DepthTarget = JF_FiniteOr(m4.w, 0.0f);
    result.FilteredTarget = JF_FiniteOr3(m5.xyz, 0.0f);
    result.PreviousFilteredTarget = JF_FiniteOr3(m6.xyz, 0.0f);
    result.Active = m0.w > 0.5f;
    result.OwnerObjectId = (uint)max(round(JF_FiniteOr(m2.w, 0.0f)), 0.0f);
    result.SourceDraw = (uint)max(round(JF_FiniteOr(m3.w, 0.0f)), 0.0f);
    result.SleepFrames = (uint)max(round(JF_FiniteOr(m1.w, 0.0f)), 0.0f);
    result.LastWheelSequenceCode = (int)clamp(
        round(JF_FiniteOr(m5.w, 0.0f)),
        -16777215.0f,
        16777215.0f);
    JF_UnpackHoldMetadata(
        m6.w,
        result.WasHeld,
        result.CaptureGeneration);
    return result;
}

void JF_EncodeMotionState(
    JF_MotionState state,
    out float4 m0,
    out float4 m1,
    out float4 m2,
    out float4 m3,
    out float4 m4,
    out float4 m5,
    out float4 m6)
{
    m0 = float4(state.Position, state.Active != 0u ? 1.0f : 0.0f);
    m1 = float4(state.Velocity, (float)state.SleepFrames);
    m2 = float4(state.Anchor, (float)state.OwnerObjectId);
    m3 = float4(state.ScreenRight, (float)state.SourceDraw);
    m4 = float4(state.ScreenUp, state.DepthTarget);
    m5 = float4(state.FilteredTarget, (float)state.LastWheelSequenceCode);
    m6 = float4(
        state.PreviousFilteredTarget,
        JF_PackHoldMetadata(
            state.WasHeld,
            state.CaptureGeneration));
}

void JF_BeginCapture(
    inout JF_MotionState state,
    JF_InputFrame input,
    JF_CapturedPick capture)
{
    float3 right;
    float3 up;
    JF_BuildOrthonormalBasis(
        capture.ScreenRight,
        capture.ScreenUp,
        right,
        up);

    state.Position = 0.0f;
    state.Velocity = 0.0f;
    state.Anchor = JF_FiniteOr3(capture.WorldPosition, 0.0f);
    state.ScreenRight = right;
    state.ScreenUp = up;
    state.DepthTarget = 0.0f;
    state.FilteredTarget = 0.0f;
    state.PreviousFilteredTarget = 0.0f;
    state.Active = 1u;
    state.OwnerObjectId = capture.ObjectId;
    state.SourceDraw = capture.SourceDraw;
    state.CaptureGeneration = capture.Generation;
    state.LastWheelSequenceCode = JF_WheelSequenceCode(
        input.WheelTowardSequence,
        input.WheelAwaySequence);
    state.SleepFrames = 0u;
}

float3 JF_BuildHeldTarget(
    inout JF_MotionState state,
    JF_InputFrame input,
    JF_CapturedPick capture,
    JF_GroupParameters parameters)
{
    int currentWheelCode = JF_WheelSequenceCode(
        input.WheelTowardSequence,
        input.WheelAwaySequence);
    int wheelSteps = currentWheelCode - state.LastWheelSequenceCode;
    if (abs(wheelSteps) > 32767)
    {
        wheelSteps = 0;
    }
    state.LastWheelSequenceCode = currentWheelCode;

    float wheelStep = clamp(
        JF_FiniteOr(parameters.WheelDepthStep, 0.0f),
        0.0f,
        10.0f);
    float minimumDepth = JF_FiniteOr(parameters.WheelMinDepth, 0.0f);
    float maximumDepth = JF_FiniteOr(
        parameters.WheelMaxDepth,
        minimumDepth);
    if (minimumDepth > maximumDepth)
    {
        float temporary = minimumDepth;
        minimumDepth = maximumDepth;
        maximumDepth = temporary;
    }

    state.DepthTarget = clamp(
        state.DepthTarget
            + (float)wheelSteps * wheelStep,
        minimumDepth,
        maximumDepth);

    float2 viewport = abs(JF_FiniteOr2(input.ViewportPixels, 1.0f));
    float referencePixels = max(min(viewport.x, viewport.y), 1.0f);
    float2 cursorDelta = JF_FiniteOr2(
        input.CursorPixels - capture.PressCursorPixels,
        0.0f) / referencePixels;
    float dragScale = clamp(
        JF_FiniteOr(parameters.DragScale, 0.0f),
        0.0f,
        100.0f);
    float strength = clamp(
        JF_FiniteOr(parameters.Strength, 0.0f),
        0.0f,
        10.0f);
    float xSign = JF_SignOrOne(parameters.MouseXSign);
    float ySign = JF_SignOrOne(parameters.MouseYSign);
    float3 forward = JF_SafeNormalize(
        cross(state.ScreenRight, state.ScreenUp),
        float3(0.0f, 0.0f, 1.0f));

    float3 target =
        state.ScreenRight * (cursorDelta.x * xSign * dragScale)
        + state.ScreenUp * (cursorDelta.y * ySign * dragScale)
        + forward * state.DepthTarget;
    target *= strength;
    return JF_ClampMagnitude(
        target,
        JF_SafeMaximumOffset(parameters.MaxOffset));
}

void JF_ApplyReleaseImpulse(
    inout JF_MotionState state,
    JF_GroupParameters parameters,
    float deltaSeconds)
{
    float3 targetVelocity =
        (state.FilteredTarget - state.PreviousFilteredTarget)
        / deltaSeconds;
    float maximumTargetSpeed = max(
        JF_SafeMaximumOffset(parameters.MaxOffset) / deltaSeconds,
        0.0f);
    targetVelocity = JF_ClampMagnitude(
        targetVelocity,
        maximumTargetSpeed);
    float impulse = clamp(
        JF_FiniteOr(parameters.ReleaseImpulse, 0.0f),
        0.0f,
        10.0f);
    state.Velocity += targetVelocity * impulse;
}

bool JF_IsTapGesture(
    JF_InputFrame input,
    JF_CapturedPick capture)
{
    float holdSeconds = clamp(
        JF_FiniteOr(capture.HoldSeconds, JF_TAP_MAXIMUM_HOLD_SECONDS + 1.0f),
        0.0f,
        10.0f);
    float2 cursorDelta = JF_FiniteOr2(
        input.CursorPixels - capture.PressCursorPixels,
        JF_TAP_MAXIMUM_CURSOR_DISTANCE + 1.0f);
    float distanceSquared = dot(cursorDelta, cursorDelta);
    return holdSeconds <= JF_TAP_MAXIMUM_HOLD_SECONDS
        && JF_IsFinite(distanceSquared)
        && distanceSquared
            <= JF_TAP_MAXIMUM_CURSOR_DISTANCE
                * JF_TAP_MAXIMUM_CURSOR_DISTANCE;
}

void JF_ApplyTapImpulse(
    inout JF_MotionState state,
    JF_CapturedPick capture,
    JF_GroupParameters parameters,
    float deltaSeconds)
{
    float3 screenNormal = JF_SafeNormalize(
        cross(state.ScreenRight, state.ScreenUp),
        float3(0.0f, 0.0f, 1.0f));
    float3 surfaceNormal = JF_SafeNormalize(
        JF_FiniteOr3(capture.SurfaceNormal, screenNormal),
        screenNormal);
    float impulse = clamp(
        JF_FiniteOr(parameters.ReleaseImpulse, 0.0f),
        0.0f,
        10.0f);
    float strength = clamp(
        JF_FiniteOr(parameters.Strength, 0.0f),
        0.0f,
        10.0f);
    float tapSpeed = JF_SafeMaximumOffset(parameters.MaxOffset)
        * impulse
        * strength
        / deltaSeconds;

    // A physical tap first presses into the selected surface. The release
    // spring then supplies the visible outward rebound.
    state.Velocity -= surfaceNormal * tapSpeed;
    state.Active = 1u;
    state.SleepFrames = 0u;
}

void JF_IntegrateImplicitSpring(
    inout JF_MotionState state,
    float3 target,
    float frequencyHz,
    float dampingRatio,
    float deltaSeconds)
{
    float safeFrequency = clamp(
        JF_FiniteOr(frequencyHz, 0.0f),
        0.0f,
        60.0f);
    float safeDamping = clamp(
        JF_FiniteOr(dampingRatio, 1.0f),
        0.0f,
        10.0f);
    float omega = 6.2831853071795864769f * safeFrequency;
    float stiffness = omega * omega;
    float damping = 2.0f * safeDamping * omega;
    float denominator =
        1.0f
        + deltaSeconds * damping
        + deltaSeconds * deltaSeconds * stiffness;
    float3 velocity =
        (state.Velocity
            + deltaSeconds * stiffness * (target - state.Position))
        / denominator;
    state.Velocity = velocity;
    state.Position += deltaSeconds * velocity;
}

void JF_ClampOffset(
    inout JF_MotionState state,
    float maximumOffset)
{
    float safeMaximum = JF_SafeMaximumOffset(maximumOffset);
    float lengthSquared = dot(state.Position, state.Position);
    if (lengthSquared <= safeMaximum * safeMaximum
        || lengthSquared <= JF_VECTOR_EPSILON)
    {
        return;
    }

    float3 outward = state.Position * rsqrt(lengthSquared);
    state.Position = outward * safeMaximum;
    float outwardSpeed = dot(state.Velocity, outward);
    if (outwardSpeed > 0.0f)
    {
        state.Velocity -= outward * outwardSpeed;
    }
}

void JF_SanitizeState(inout JF_MotionState state)
{
    state.Position = JF_FiniteOr3(state.Position, 0.0f);
    state.Velocity = JF_FiniteOr3(state.Velocity, 0.0f);
    state.Anchor = JF_FiniteOr3(state.Anchor, 0.0f);
    float3 right;
    float3 up;
    JF_BuildOrthonormalBasis(
        JF_FiniteOr3(
            state.ScreenRight,
            float3(1.0f, 0.0f, 0.0f)),
        JF_FiniteOr3(
            state.ScreenUp,
            float3(0.0f, 1.0f, 0.0f)),
        right,
        up);
    state.ScreenRight = right;
    state.ScreenUp = up;
    state.DepthTarget = JF_FiniteOr(state.DepthTarget, 0.0f);
    state.FilteredTarget = JF_FiniteOr3(state.FilteredTarget, 0.0f);
    state.PreviousFilteredTarget = JF_FiniteOr3(
        state.PreviousFilteredTarget,
        0.0f);
}

void JF_UpdateSleepState(inout JF_MotionState state)
{
    bool sleeping =
        dot(state.Position, state.Position) <= JF_SLEEP_POSITION_SQUARED
        && dot(state.Velocity, state.Velocity) <= JF_SLEEP_VELOCITY_SQUARED
        && dot(state.FilteredTarget, state.FilteredTarget)
            <= JF_SLEEP_POSITION_SQUARED;
    state.SleepFrames = sleeping ? state.SleepFrames + 1u : 0u;
    if (state.SleepFrames < JF_SLEEP_FRAME_THRESHOLD)
    {
        return;
    }

    state.Position = 0.0f;
    state.Velocity = 0.0f;
    state.FilteredTarget = 0.0f;
    state.PreviousFilteredTarget = 0.0f;
    state.Active = 0u;
    state.OwnerObjectId = 0u;
}

void JF_StepMotion(
    inout JF_MotionState state,
    uint expectedObjectId,
    JF_InputFrame input,
    JF_CapturedPick capture,
    JF_GroupParameters parameters)
{
    float deltaSeconds = clamp(
        JF_FiniteOr(input.DeltaSeconds, 1.0f / 60.0f),
        JF_MINIMUM_DELTA_SECONDS,
        JF_MAXIMUM_DELTA_SECONDS);
    bool ownsCapture =
        capture.Valid != 0u
        && capture.ObjectId == expectedObjectId;
    bool heldByState = input.DragHeld != 0u && ownsCapture;
    bool newCapture =
        heldByState
        && (state.WasHeld == 0u
            || state.CaptureGeneration != capture.Generation);

    if (newCapture)
    {
        JF_BeginCapture(state, input, capture);
    }

    bool releaseEdge = state.WasHeld != 0u && !heldByState;
    if (releaseEdge)
    {
        JF_ApplyReleaseImpulse(state, parameters, deltaSeconds);
        if (JF_IsTapGesture(input, capture))
        {
            JF_ApplyTapImpulse(
                state,
                capture,
                parameters,
                deltaSeconds);
        }
    }

    float3 target = heldByState
        ? JF_BuildHeldTarget(state, input, capture, parameters)
        : 0.0f;
    float followSeconds = clamp(
        JF_FiniteOr(parameters.TargetFollowSeconds, 0.0f),
        0.0f,
        10.0f);
    float followDenominator = max(followSeconds, 1.0e-5f);
    float targetAlpha = followSeconds <= 1.0e-5f
        ? 1.0f
        : 1.0f - exp(-deltaSeconds / followDenominator);
    state.PreviousFilteredTarget = state.FilteredTarget;
    state.FilteredTarget = lerp(
        state.FilteredTarget,
        target,
        targetAlpha);

    float frequencyHz = heldByState
        ? parameters.HoldFrequencyHz
        : parameters.ReleaseFrequencyHz;
    float dampingRatio = heldByState
        ? parameters.HoldDampingRatio
        : parameters.ReleaseDampingRatio;
    JF_IntegrateImplicitSpring(
        state,
        state.FilteredTarget,
        frequencyHz,
        dampingRatio,
        deltaSeconds);
    JF_ClampOffset(state, parameters.MaxOffset);
    JF_SanitizeState(state);

    if (heldByState)
    {
        state.Active = 1u;
        state.SleepFrames = 0u;
    }
    else
    {
        JF_UpdateSleepState(state);
    }

    state.WasHeld = heldByState ? 1u : 0u;
}

#endif
