using System.Numerics;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class MotionModelTests
{
    private static readonly Vector2 Viewport = new(1000.0f, 1000.0f);

    [TestMethod]
    public void NoInputRemainsAtRest()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new();

        Step(state, capture, parameters, 17, false, new Vector2(500.0f), InvalidPick());

        AssertVectorNear(Vector3.Zero, state.Position);
        AssertVectorNear(Vector3.Zero, state.Velocity);
        Assert.IsFalse(state.Active);
    }

    [TestMethod]
    public void RisingEdgeFreezesThePickAndScreenBasis()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new() { TargetFollowSeconds = 0.0f };
        CpuPick firstPick = Pick(
            objectId: 17,
            worldPosition: new Vector3(1.0f, 2.0f, 3.0f),
            right: Vector3.UnitX,
            up: Vector3.UnitY,
            sourceDraw: 4);

        Step(state, capture, parameters, 17, true, new Vector2(500.0f), firstPick);
        CpuPick laterPick = Pick(
            objectId: 17,
            worldPosition: new Vector3(9.0f),
            right: Vector3.UnitY,
            up: Vector3.UnitZ,
            sourceDraw: 99);
        Step(
            state,
            capture,
            parameters,
            17,
            true,
            new Vector2(600.0f, 500.0f),
            laterPick);

        AssertVectorNear(firstPick.WorldPosition, state.Anchor);
        AssertVectorNear(Vector3.UnitX, state.ScreenRight);
        AssertVectorNear(Vector3.UnitY, state.ScreenUp);
        Assert.AreEqual(4, state.SourceDraw);
        Assert.IsTrue(state.Position.X > 0.0f);
        Assert.AreEqual(0.0f, state.Position.Y, 1.0e-6f);
        Assert.AreEqual(0.0f, state.Position.Z, 1.0e-6f);
    }

    [TestMethod]
    public void DefaultHeldResponseReachesTheCursorWithoutVisibleLag()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new();
        CpuPick pick = Pick(17);

        Step(state, capture, parameters, 17, true, new Vector2(500.0f), pick);
        for (int frame = 0; frame < 6; frame++)
        {
            Step(
                state,
                capture,
                parameters,
                17,
                true,
                new Vector2(600.0f, 500.0f),
                pick);
        }

        float requestedDisplacement =
            0.1f * parameters.DragScale * parameters.Strength;
        Assert.IsTrue(
            state.Position.X >= requestedDisplacement * 0.8f,
            $"Default hold response reached only {state.Position.X} of {requestedDisplacement} after 100 ms.");
    }

    [TestMethod]
    public void InvalidPickDoesNotActivateAState()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();

        Step(
            state,
            capture,
            new CpuParameters(),
            17,
            true,
            new Vector2(500.0f),
            InvalidPick());

        Assert.IsFalse(state.Active);
        AssertVectorNear(Vector3.Zero, state.Position);
    }

    [TestMethod]
    public void WheelMovesOnlyAlongTheFrozenScreenNormal()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            Strength = 1.0f,
            DragScale = 1.0f,
            MaxOffset = 1.0f,
            TargetFollowSeconds = 0.0f,
            WheelDepthStep = 0.1f,
            WheelMaxDepth = 1.0f,
        };

        Step(state, capture, parameters, 3, true, new Vector2(500.0f), Pick(3));
        Step(
            state,
            capture,
            parameters,
            3,
            true,
            new Vector2(500.0f),
            Pick(3, right: Vector3.UnitZ, up: Vector3.UnitY),
            towardSequence: 2);

        Assert.AreEqual(0.2f, state.DepthTarget, 1.0e-6f);
        Assert.AreEqual(0.0f, state.Position.X, 1.0e-6f);
        Assert.AreEqual(0.0f, state.Position.Y, 1.0e-6f);
        Assert.IsTrue(state.Position.Z > 0.0f);
    }

    [TestMethod]
    public void ReleaseImpulseIsAppliedExactlyOnce()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            TargetFollowSeconds = 0.1f,
            ReleaseImpulse = 0.5f,
        };

        Step(state, capture, parameters, 7, true, new Vector2(500.0f), Pick(7));
        Step(state, capture, parameters, 7, true, new Vector2(650.0f), Pick(7));
        Step(state, capture, parameters, 7, false, new Vector2(650.0f), Pick(7));
        Vector3 velocityAfterRelease = state.Velocity;
        Step(state, capture, parameters, 7, false, new Vector2(650.0f), Pick(7));

        Assert.AreEqual(1, state.ReleaseImpulseApplications);
        Assert.AreNotEqual(Vector3.Zero, velocityAfterRelease);
    }

    [TestMethod]
    public void ShortStationaryClickAppliesOneInwardSurfaceNormalImpulse()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            Strength = 1.0f,
            MaxOffset = 0.15f,
            ReleaseImpulse = 0.5f,
            ReleaseFrequencyHz = 0.0f,
            ReleaseDampingRatio = 0.0f,
        };
        CpuPick pick = Pick(7) with { SurfaceNormal = Vector3.UnitY };

        Step(state, capture, parameters, 7, true, new Vector2(500.0f), pick);
        Step(state, capture, parameters, 7, false, new Vector2(504.0f), pick);

        Assert.AreEqual(1, state.TapImpulseApplications);
        Assert.IsTrue(state.Velocity.Y < 0.0f);
        Assert.AreEqual(0.0f, state.Velocity.X, 1.0e-6f);
        Assert.AreEqual(0.0f, state.Velocity.Z, 1.0e-6f);

        Step(state, capture, parameters, 7, false, new Vector2(504.0f), pick);
        Assert.AreEqual(1, state.TapImpulseApplications);
    }

    [TestMethod]
    public void DragAndLongHoldDoNotTriggerTapImpulse()
    {
        CpuParameters parameters = new() { ReleaseImpulse = 0.5f };

        CpuCaptureState draggedCapture = new();
        CpuMotionState draggedState = new();
        CpuPick pick = Pick(7) with { SurfaceNormal = Vector3.UnitZ };
        Step(draggedState, draggedCapture, parameters, 7, true, Vector2.Zero, pick);
        Step(draggedState, draggedCapture, parameters, 7, false, new Vector2(20.0f), pick);
        Assert.AreEqual(0, draggedState.TapImpulseApplications);

        CpuCaptureState heldCapture = new();
        CpuMotionState heldState = new();
        for (int frame = 0; frame < 13; frame++)
        {
            Step(heldState, heldCapture, parameters, 7, true, Vector2.Zero, pick);
        }
        Step(heldState, heldCapture, parameters, 7, false, Vector2.Zero, pick);
        Assert.AreEqual(0, heldState.TapImpulseApplications);
    }

    [TestMethod]
    public void ReleasedStateReturnsToRestAndSleeps()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            TargetFollowSeconds = 0.02f,
            ReleaseImpulse = 0.0f,
            ReleaseFrequencyHz = 4.0f,
            ReleaseDampingRatio = 1.0f,
        };

        Step(state, capture, parameters, 11, true, new Vector2(500.0f), Pick(11));
        for (int index = 0; index < 30; index++)
        {
            Step(state, capture, parameters, 11, true, new Vector2(650.0f), Pick(11));
        }

        for (int index = 0; index < 300; index++)
        {
            Step(state, capture, parameters, 11, false, new Vector2(650.0f), Pick(11));
        }

        AssertVectorNear(Vector3.Zero, state.Position, 1.0e-4f);
        AssertVectorNear(Vector3.Zero, state.Velocity, 1.0e-4f);
        Assert.IsFalse(state.Active);
    }

    [TestMethod]
    public void MaximumOffsetIsAHardSphericalLimit()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            Strength = 10.0f,
            DragScale = 100.0f,
            MaxOffset = 0.05f,
            TargetFollowSeconds = 0.0f,
            HoldFrequencyHz = 60.0f,
        };

        Step(state, capture, parameters, 5, true, Vector2.Zero, Pick(5));
        for (int index = 0; index < 60; index++)
        {
            Step(state, capture, parameters, 5, true, new Vector2(1000.0f), Pick(5));
        }

        Assert.IsTrue(state.Position.Length() <= 0.050001f);
    }

    [TestMethod]
    public void ObjectStatesAreIsolatedByCapturedObjectId()
    {
        CpuCaptureState capture = new();
        CpuMotionState first = new();
        CpuMotionState second = new();
        CpuParameters parameters = new() { TargetFollowSeconds = 0.0f };

        CpuInput press = Input(true, new Vector2(500.0f));
        capture.Step(press, Pick(101));
        CpuMotionSolver.Step(first, 101, press, capture, parameters);
        CpuMotionSolver.Step(second, 202, press, capture, parameters);

        CpuInput drag = Input(true, new Vector2(650.0f));
        capture.Step(drag, Pick(202));
        CpuMotionSolver.Step(first, 101, drag, capture, parameters);
        CpuMotionSolver.Step(second, 202, drag, capture, parameters);

        Assert.IsTrue(first.Position.LengthSquared() > 0.0f);
        AssertVectorNear(Vector3.Zero, second.Position);
        Assert.IsTrue(first.Active);
        Assert.IsFalse(second.Active);
    }

    [TestMethod]
    public void ResultsAreCloseAcrossCommonFrameRates()
    {
        Vector3 at30 = SimulateAtFrameRate(30);
        Vector3 at60 = SimulateAtFrameRate(60);
        Vector3 at120 = SimulateAtFrameRate(120);

        AssertVectorNear(at60, at30, 0.006f);
        AssertVectorNear(at60, at120, 0.006f);
    }

    [TestMethod]
    public void InvalidAndExtremeParametersCannotCreateNonFiniteState()
    {
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            Strength = float.PositiveInfinity,
            DragScale = float.NaN,
            MaxOffset = float.PositiveInfinity,
            TargetFollowSeconds = float.NaN,
            HoldFrequencyHz = float.PositiveInfinity,
            HoldDampingRatio = float.NaN,
            ReleaseFrequencyHz = float.NegativeInfinity,
            ReleaseDampingRatio = float.PositiveInfinity,
            ReleaseImpulse = float.NaN,
            WheelDepthStep = float.PositiveInfinity,
            WheelMinDepth = float.NaN,
            WheelMaxDepth = float.PositiveInfinity,
        };

        Step(
            state,
            capture,
            parameters,
            1,
            true,
            new Vector2(float.PositiveInfinity, float.NaN),
            new CpuPick(
                true,
                1,
                new Vector3(float.NaN),
                new Vector3(float.PositiveInfinity),
                Vector3.Zero,
                1),
            deltaSeconds: float.NaN,
            towardSequence: int.MaxValue);

        AssertFinite(state.Position);
        AssertFinite(state.Velocity);
        AssertFinite(state.FilteredTarget);
        AssertFinite(state.ScreenRight);
        AssertFinite(state.ScreenUp);
        Assert.IsTrue(float.IsFinite(state.DepthTarget));
    }

    private static Vector3 SimulateAtFrameRate(int framesPerSecond)
    {
        float deltaSeconds = 1.0f / framesPerSecond;
        CpuCaptureState capture = new();
        CpuMotionState state = new();
        CpuParameters parameters = new()
        {
            TargetFollowSeconds = 0.08f,
            HoldFrequencyHz = 3.5f,
            HoldDampingRatio = 0.85f,
            ReleaseFrequencyHz = 2.0f,
            ReleaseDampingRatio = 0.9f,
            ReleaseImpulse = 0.0f,
        };

        Step(
            state,
            capture,
            parameters,
            77,
            true,
            new Vector2(500.0f),
            Pick(77),
            deltaSeconds);
        for (int index = 0; index < framesPerSecond; index++)
        {
            Step(
                state,
                capture,
                parameters,
                77,
                true,
                new Vector2(620.0f, 540.0f),
                Pick(77),
                deltaSeconds);
        }

        for (int index = 0; index < framesPerSecond / 2; index++)
        {
            Step(
                state,
                capture,
                parameters,
                77,
                false,
                new Vector2(620.0f, 540.0f),
                Pick(77),
                deltaSeconds);
        }

        return state.Position;
    }

    private static void Step(
        CpuMotionState state,
        CpuCaptureState capture,
        CpuParameters parameters,
        int expectedObjectId,
        bool held,
        Vector2 cursor,
        CpuPick pick,
        float deltaSeconds = 1.0f / 60.0f,
        int towardSequence = 0,
        int awaySequence = 0)
    {
        CpuInput input = new(
            cursor,
            Viewport,
            held,
            towardSequence,
            awaySequence,
            deltaSeconds);
        capture.Step(input, pick);
        CpuMotionSolver.Step(state, expectedObjectId, input, capture, parameters);
    }

    private static CpuInput Input(bool held, Vector2 cursor) =>
        new(cursor, Viewport, held, 0, 0, 1.0f / 60.0f);

    private static CpuPick Pick(
        int objectId,
        Vector3? worldPosition = null,
        Vector3? right = null,
        Vector3? up = null,
        int sourceDraw = 1) =>
        new(
            true,
            objectId,
            worldPosition ?? Vector3.Zero,
            right ?? Vector3.UnitX,
            up ?? Vector3.UnitY,
            sourceDraw);

    private static CpuPick InvalidPick() =>
        new(false, 0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 0);

    private static void AssertVectorNear(
        Vector3 expected,
        Vector3 actual,
        float tolerance = 1.0e-6f)
    {
        Assert.AreEqual(expected.X, actual.X, tolerance);
        Assert.AreEqual(expected.Y, actual.Y, tolerance);
        Assert.AreEqual(expected.Z, actual.Z, tolerance);
    }

    private static void AssertFinite(Vector3 value)
    {
        Assert.IsTrue(float.IsFinite(value.X));
        Assert.IsTrue(float.IsFinite(value.Y));
        Assert.IsTrue(float.IsFinite(value.Z));
    }
}
