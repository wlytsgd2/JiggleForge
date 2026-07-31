using System.Numerics;

namespace JiggleForge.Core.Tests;

internal sealed class CpuParameters
{
    public float Strength { get; set; } = 0.7f;

    public float DragScale { get; set; } = 0.75f;

    public float MaxOffset { get; set; } = 0.15f;

    public float TargetFollowSeconds { get; set; } = 0.02f;

    public float HoldFrequencyHz { get; set; } = 10.0f;

    public float HoldDampingRatio { get; set; } = 0.84f;

    public float ReleaseFrequencyHz { get; set; } = 2.2f;

    public float ReleaseDampingRatio { get; set; } = 0.9f;

    public float ReleaseImpulse { get; set; } = 0.12f;

    public float WheelDepthStep { get; set; } = 0.02f;

    public float WheelMinDepth { get; set; } = 0.0f;

    public float WheelMaxDepth { get; set; } = 0.15f;

    public float MouseXSign { get; set; } = 1.0f;

    public float MouseYSign { get; set; } = -1.0f;
}

internal readonly record struct CpuInput(
    Vector2 CursorPixels,
    Vector2 ViewportPixels,
    bool DragHeld,
    int WheelTowardSequence,
    int WheelAwaySequence,
    float DeltaSeconds);

internal readonly record struct CpuPick(
    bool Valid,
    int ObjectId,
    Vector3 WorldPosition,
    Vector3 ScreenRight,
    Vector3 ScreenUp,
    int SourceDraw)
{
    public Vector3 SurfaceNormal { get; init; } = Vector3.UnitZ;

    public float Depth { get; init; }

    public float Priority { get; init; }

    public float PipelineToken { get; init; }

    public int TriangleOrdinal { get; init; }

    public Vector3 TriangleIndices { get; init; }

    public Vector3 Barycentric { get; init; }
}

internal sealed class CpuCaptureState
{
    public bool PreviousHeld { get; private set; }

    public bool Valid { get; private set; }

    public Vector2 PressCursorPixels { get; private set; }

    public Vector2 CurrentCursorPixels { get; private set; }

    public int WheelSequenceCode { get; private set; }

    public bool CurrentPickValid { get; private set; }

    public CpuPick Pick { get; private set; }

    public uint Generation { get; private set; }

    public float HoldSeconds { get; private set; }

    public void Step(in CpuInput input, in CpuPick currentPick)
    {
        Vector2 currentCursor = IsFinite(input.CursorPixels)
            ? input.CursorPixels
            : Vector2.Zero;
        bool risingEdge = input.DragHeld && !PreviousHeld;
        if (risingEdge)
        {
            Generation = Generation >= 0x007fffff
                ? 1u
                : Generation + 1u;
            PressCursorPixels = currentCursor;
            Valid = currentPick.Valid;
            Pick = currentPick.Valid ? Freeze(currentPick) : default;
            HoldSeconds = 0.0f;
        }

        if (input.DragHeld && Valid)
        {
            HoldSeconds = MathF.Min(
                HoldSeconds + Math.Clamp(
                    float.IsFinite(input.DeltaSeconds)
                        ? input.DeltaSeconds
                        : 1.0f / 60.0f,
                    1.0f / 240.0f,
                    0.05f),
                10.0f);
        }

        CurrentCursorPixels = currentCursor;
        WheelSequenceCode = BuildWheelSequenceCode(input);
        CurrentPickValid = currentPick.Valid;
        PreviousHeld = input.DragHeld;
    }

    private static CpuPick Freeze(in CpuPick source)
    {
        BuildBasis(
            source.ScreenRight,
            source.ScreenUp,
            out Vector3 right,
            out Vector3 up);
        return source with
        {
            WorldPosition = IsFinite(source.WorldPosition)
                ? source.WorldPosition
                : Vector3.Zero,
            ScreenRight = right,
            ScreenUp = up,
            SurfaceNormal = SafeNormalize(
                IsFinite(source.SurfaceNormal)
                    ? source.SurfaceNormal
                    : Vector3.Cross(right, up),
                SafeNormalize(Vector3.Cross(right, up), Vector3.UnitZ)),
            Depth = float.IsFinite(source.Depth) ? source.Depth : 0.0f,
            Priority = float.IsFinite(source.Priority) ? source.Priority : 0.0f,
            Barycentric = IsFinite(source.Barycentric)
                ? source.Barycentric
                : Vector3.Zero,
        };
    }

    private static void BuildBasis(
        Vector3 sourceRight,
        Vector3 sourceUp,
        out Vector3 right,
        out Vector3 up)
    {
        right = SafeNormalize(
            IsFinite(sourceRight) ? sourceRight : Vector3.UnitX,
            Vector3.UnitX);
        Vector3 safeUp = IsFinite(sourceUp) ? sourceUp : Vector3.UnitY;
        Vector3 rejectedUp = safeUp - (right * Vector3.Dot(safeUp, right));
        Vector3 fallback = MathF.Abs(right.Y) < 0.9f
            ? Vector3.UnitY
            : Vector3.UnitX;
        fallback -= right * Vector3.Dot(fallback, right);
        up = SafeNormalize(rejectedUp, SafeNormalize(fallback, Vector3.UnitY));
    }

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        float lengthSquared = value.LengthSquared();
        return float.IsFinite(lengthSquared) && lengthSquared > 1.0e-8f
            ? value / MathF.Sqrt(lengthSquared)
            : fallback;
    }

    private static int BuildWheelSequenceCode(in CpuInput input)
    {
        int toward = Math.Clamp(input.WheelTowardSequence, 0, 0x007fffff);
        int away = Math.Clamp(input.WheelAwaySequence, 0, 0x007fffff);
        return toward - away;
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z);
}

internal sealed class CpuMotionState
{
    public Vector3 Position { get; set; }

    public Vector3 Velocity { get; set; }

    public Vector3 Anchor { get; set; }

    public Vector3 ScreenRight { get; set; } = Vector3.UnitX;

    public Vector3 ScreenUp { get; set; } = Vector3.UnitY;

    public float DepthTarget { get; set; }

    public Vector3 FilteredTarget { get; set; }

    public Vector3 PreviousFilteredTarget { get; set; }

    public bool Active { get; set; }

    public bool WasHeld { get; set; }

    public int OwnerObjectId { get; set; }

    public int SourceDraw { get; set; }

    public uint CaptureGeneration { get; set; }

    public int LastWheelSequenceCode { get; set; }

    public int SleepFrames { get; set; }

    public int ReleaseImpulseApplications { get; set; }

    public int TapImpulseApplications { get; set; }
}

internal static class CpuMotionSolver
{
    private const float MinimumDeltaSeconds = 1.0f / 240.0f;
    private const float MaximumDeltaSeconds = 0.05f;
    private const float BasisEpsilon = 1.0e-8f;
    private const float SleepPositionSquared = 1.0e-8f;
    private const float SleepVelocitySquared = 1.0e-8f;
    private const int SleepFrameThreshold = 12;
    private const float TapMaximumHoldSeconds = 0.20f;
    private const float TapMaximumCursorDistance = 10.0f;

    public static void Step(
        CpuMotionState state,
        int expectedObjectId,
        in CpuInput input,
        CpuCaptureState capture,
        CpuParameters parameters)
    {
        float deltaSeconds = Math.Clamp(
            FiniteOr(input.DeltaSeconds, 1.0f / 60.0f),
            MinimumDeltaSeconds,
            MaximumDeltaSeconds);

        bool ownsCapture = capture.Valid && capture.Pick.ObjectId == expectedObjectId;
        bool heldByState = input.DragHeld && ownsCapture;
        bool newCapture = heldByState
            && (!state.WasHeld || state.CaptureGeneration != capture.Generation);

        if (newCapture)
        {
            Capture(state, input, capture);
        }

        bool releaseEdge = state.WasHeld && !heldByState;
        if (releaseEdge)
        {
            ApplyReleaseImpulse(state, parameters, deltaSeconds);
            if (IsTapGesture(input, capture))
            {
                ApplyTapImpulse(state, capture, parameters, deltaSeconds);
            }
        }

        Vector3 target = heldByState
            ? BuildHeldTarget(state, input, capture, parameters)
            : Vector3.Zero;

        float targetFollowSeconds = Math.Clamp(
            FiniteOr(parameters.TargetFollowSeconds, 0.0f),
            0.0f,
            10.0f);
        float targetAlpha = targetFollowSeconds <= 1.0e-5f
            ? 1.0f
            : 1.0f - MathF.Exp(-deltaSeconds / targetFollowSeconds);

        state.PreviousFilteredTarget = state.FilteredTarget;
        state.FilteredTarget = Vector3.Lerp(state.FilteredTarget, target, targetAlpha);

        float frequencyHz = heldByState
            ? parameters.HoldFrequencyHz
            : parameters.ReleaseFrequencyHz;
        float dampingRatio = heldByState
            ? parameters.HoldDampingRatio
            : parameters.ReleaseDampingRatio;

        IntegrateImplicitSpring(
            state,
            state.FilteredTarget,
            frequencyHz,
            dampingRatio,
            deltaSeconds);

        ClampOffset(state, parameters.MaxOffset);
        SanitizeState(state);

        if (heldByState)
        {
            state.Active = true;
            state.SleepFrames = 0;
        }
        else
        {
            UpdateSleepState(state);
        }

        state.WasHeld = heldByState;
    }

    private static void Capture(
        CpuMotionState state,
        in CpuInput input,
        CpuCaptureState capture)
    {
        BuildOrthonormalBasis(
            capture.Pick.ScreenRight,
            capture.Pick.ScreenUp,
            out Vector3 right,
            out Vector3 up);

        state.Position = Vector3.Zero;
        state.Velocity = Vector3.Zero;
        state.Anchor = FiniteOr(capture.Pick.WorldPosition, Vector3.Zero);
        state.ScreenRight = right;
        state.ScreenUp = up;
        state.DepthTarget = 0.0f;
        state.FilteredTarget = Vector3.Zero;
        state.PreviousFilteredTarget = Vector3.Zero;
        state.Active = true;
        state.OwnerObjectId = capture.Pick.ObjectId;
        state.SourceDraw = capture.Pick.SourceDraw;
        state.CaptureGeneration = capture.Generation;
        state.LastWheelSequenceCode = WheelSequenceCode(input);
        state.SleepFrames = 0;
    }

    private static Vector3 BuildHeldTarget(
        CpuMotionState state,
        in CpuInput input,
        CpuCaptureState capture,
        CpuParameters parameters)
    {
        int currentWheelCode = WheelSequenceCode(input);
        int wheelSteps = currentWheelCode - state.LastWheelSequenceCode;
        if (Math.Abs(wheelSteps) > 32767)
        {
            wheelSteps = 0;
        }

        state.LastWheelSequenceCode = currentWheelCode;

        float wheelStep = Math.Clamp(
            FiniteOr(parameters.WheelDepthStep, 0.0f),
            0.0f,
            10.0f);
        float minimumDepth = FiniteOr(parameters.WheelMinDepth, 0.0f);
        float maximumDepth = FiniteOr(parameters.WheelMaxDepth, minimumDepth);
        if (minimumDepth > maximumDepth)
        {
            (minimumDepth, maximumDepth) = (maximumDepth, minimumDepth);
        }

        state.DepthTarget = Math.Clamp(
            state.DepthTarget + (wheelSteps * wheelStep),
            minimumDepth,
            maximumDepth);

        Vector2 viewport = FiniteOr(input.ViewportPixels, Vector2.One);
        float referencePixels = MathF.Max(
            MathF.Min(MathF.Abs(viewport.X), MathF.Abs(viewport.Y)),
            1.0f);
        Vector2 cursorDelta = FiniteOr(
            input.CursorPixels - capture.PressCursorPixels,
            Vector2.Zero) / referencePixels;

        float dragScale = Math.Clamp(FiniteOr(parameters.DragScale, 0.0f), 0.0f, 100.0f);
        float strength = Math.Clamp(FiniteOr(parameters.Strength, 0.0f), 0.0f, 10.0f);
        float xSign = SignOrOne(parameters.MouseXSign);
        float ySign = SignOrOne(parameters.MouseYSign);
        Vector3 forward = SafeNormalize(
            Vector3.Cross(state.ScreenRight, state.ScreenUp),
            Vector3.UnitZ);

        Vector3 target =
            (state.ScreenRight * (cursorDelta.X * xSign * dragScale))
            + (state.ScreenUp * (cursorDelta.Y * ySign * dragScale))
            + (forward * state.DepthTarget);
        target *= strength;

        return ClampMagnitude(target, SafeMaximumOffset(parameters.MaxOffset));
    }

    private static void ApplyReleaseImpulse(
        CpuMotionState state,
        CpuParameters parameters,
        float deltaSeconds)
    {
        Vector3 targetVelocity =
            (state.FilteredTarget - state.PreviousFilteredTarget) / deltaSeconds;
        float maximumTargetSpeed = MathF.Max(
            SafeMaximumOffset(parameters.MaxOffset) / deltaSeconds,
            0.0f);
        targetVelocity = ClampMagnitude(targetVelocity, maximumTargetSpeed);

        float releaseImpulse = Math.Clamp(
            FiniteOr(parameters.ReleaseImpulse, 0.0f),
            0.0f,
            10.0f);
        state.Velocity += targetVelocity * releaseImpulse;
        state.ReleaseImpulseApplications++;
    }

    private static bool IsTapGesture(
        in CpuInput input,
        CpuCaptureState capture)
    {
        float holdSeconds = Math.Clamp(
            FiniteOr(capture.HoldSeconds, TapMaximumHoldSeconds + 1.0f),
            0.0f,
            10.0f);
        Vector2 cursorDelta = FiniteOr(
            input.CursorPixels - capture.PressCursorPixels,
            new Vector2(TapMaximumCursorDistance + 1.0f));
        float distanceSquared = cursorDelta.LengthSquared();
        return holdSeconds <= TapMaximumHoldSeconds
            && float.IsFinite(distanceSquared)
            && distanceSquared
                <= TapMaximumCursorDistance * TapMaximumCursorDistance;
    }

    private static void ApplyTapImpulse(
        CpuMotionState state,
        CpuCaptureState capture,
        CpuParameters parameters,
        float deltaSeconds)
    {
        Vector3 screenNormal = SafeNormalize(
            Vector3.Cross(state.ScreenRight, state.ScreenUp),
            Vector3.UnitZ);
        Vector3 surfaceNormal = SafeNormalize(
            FiniteOr(capture.Pick.SurfaceNormal, screenNormal),
            screenNormal);
        float impulse = Math.Clamp(
            FiniteOr(parameters.ReleaseImpulse, 0.0f),
            0.0f,
            10.0f);
        float strength = Math.Clamp(
            FiniteOr(parameters.Strength, 0.0f),
            0.0f,
            10.0f);
        float tapSpeed = SafeMaximumOffset(parameters.MaxOffset)
            * impulse
            * strength
            / deltaSeconds;

        state.Velocity -= surfaceNormal * tapSpeed;
        state.Active = true;
        state.SleepFrames = 0;
        state.TapImpulseApplications++;
    }

    private static void IntegrateImplicitSpring(
        CpuMotionState state,
        Vector3 target,
        float frequencyHz,
        float dampingRatio,
        float deltaSeconds)
    {
        float safeFrequency = Math.Clamp(FiniteOr(frequencyHz, 0.0f), 0.0f, 60.0f);
        float safeDamping = Math.Clamp(FiniteOr(dampingRatio, 1.0f), 0.0f, 10.0f);
        float omega = 2.0f * MathF.PI * safeFrequency;
        float stiffness = omega * omega;
        float damping = 2.0f * safeDamping * omega;
        float denominator =
            1.0f + (deltaSeconds * damping) + (deltaSeconds * deltaSeconds * stiffness);

        Vector3 velocity =
            (state.Velocity + (deltaSeconds * stiffness * (target - state.Position)))
            / denominator;
        state.Velocity = velocity;
        state.Position += deltaSeconds * velocity;
    }

    private static void ClampOffset(CpuMotionState state, float maximumOffset)
    {
        float safeMaximum = SafeMaximumOffset(maximumOffset);
        float lengthSquared = state.Position.LengthSquared();
        if (lengthSquared <= safeMaximum * safeMaximum || lengthSquared <= BasisEpsilon)
        {
            return;
        }

        Vector3 outward = state.Position / MathF.Sqrt(lengthSquared);
        state.Position = outward * safeMaximum;
        float outwardSpeed = Vector3.Dot(state.Velocity, outward);
        if (outwardSpeed > 0.0f)
        {
            state.Velocity -= outward * outwardSpeed;
        }
    }

    private static void UpdateSleepState(CpuMotionState state)
    {
        bool sleeping =
            state.Position.LengthSquared() <= SleepPositionSquared
            && state.Velocity.LengthSquared() <= SleepVelocitySquared
            && state.FilteredTarget.LengthSquared() <= SleepPositionSquared;
        state.SleepFrames = sleeping ? state.SleepFrames + 1 : 0;
        if (state.SleepFrames < SleepFrameThreshold)
        {
            return;
        }

        state.Position = Vector3.Zero;
        state.Velocity = Vector3.Zero;
        state.FilteredTarget = Vector3.Zero;
        state.PreviousFilteredTarget = Vector3.Zero;
        state.Active = false;
    }

    private static void SanitizeState(CpuMotionState state)
    {
        state.Position = FiniteOr(state.Position, Vector3.Zero);
        state.Velocity = FiniteOr(state.Velocity, Vector3.Zero);
        state.Anchor = FiniteOr(state.Anchor, Vector3.Zero);
        state.ScreenRight = FiniteOr(state.ScreenRight, Vector3.UnitX);
        state.ScreenUp = FiniteOr(state.ScreenUp, Vector3.UnitY);
        state.DepthTarget = FiniteOr(state.DepthTarget, 0.0f);
        state.FilteredTarget = FiniteOr(state.FilteredTarget, Vector3.Zero);
        state.PreviousFilteredTarget = FiniteOr(
            state.PreviousFilteredTarget,
            Vector3.Zero);
    }

    private static void BuildOrthonormalBasis(
        Vector3 sourceRight,
        Vector3 sourceUp,
        out Vector3 right,
        out Vector3 up)
    {
        right = SafeNormalize(FiniteOr(sourceRight, Vector3.UnitX), Vector3.UnitX);
        Vector3 rejectedUp =
            FiniteOr(sourceUp, Vector3.UnitY)
            - (right * Vector3.Dot(FiniteOr(sourceUp, Vector3.UnitY), right));
        up = SafeNormalize(rejectedUp, OrthogonalFallback(right));
    }

    private static Vector3 OrthogonalFallback(Vector3 direction)
    {
        Vector3 candidate = MathF.Abs(direction.Y) < 0.9f
            ? Vector3.UnitY
            : Vector3.UnitX;
        return SafeNormalize(
            candidate - (direction * Vector3.Dot(candidate, direction)),
            Vector3.UnitY);
    }

    private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
    {
        float lengthSquared = value.LengthSquared();
        return float.IsFinite(lengthSquared) && lengthSquared > BasisEpsilon
            ? value / MathF.Sqrt(lengthSquared)
            : fallback;
    }

    private static Vector3 ClampMagnitude(Vector3 value, float maximum)
    {
        float lengthSquared = value.LengthSquared();
        if (!float.IsFinite(lengthSquared))
        {
            return Vector3.Zero;
        }

        float maximumSquared = maximum * maximum;
        return lengthSquared > maximumSquared && lengthSquared > BasisEpsilon
            ? value * (maximum / MathF.Sqrt(lengthSquared))
            : value;
    }

    private static int WheelSequenceCode(in CpuInput input)
    {
        int toward = Math.Clamp(input.WheelTowardSequence, 0, 0x007fffff);
        int away = Math.Clamp(input.WheelAwaySequence, 0, 0x007fffff);
        return toward - away;
    }

    private static float SafeMaximumOffset(float value) =>
        Math.Clamp(FiniteOr(value, 0.0f), 0.0f, 100.0f);

    private static float SignOrOne(float value)
    {
        float safe = FiniteOr(value, 1.0f);
        return safe < 0.0f ? -1.0f : 1.0f;
    }

    private static float FiniteOr(float value, float fallback) =>
        float.IsFinite(value) ? value : fallback;

    private static Vector2 FiniteOr(Vector2 value, Vector2 fallback) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) ? value : fallback;

    private static Vector3 FiniteOr(Vector3 value, Vector3 fallback) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z)
            ? value
            : fallback;
}
