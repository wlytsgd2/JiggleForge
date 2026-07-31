using System.Numerics;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class InputControllerTests
{
    [TestMethod]
    public void ValidRisingEdgeCopiesTheCompletePick()
    {
        CpuCaptureState controller = new();
        CpuPick pick = Pick(17, 4) with
        {
            Depth = 0.42f,
            Priority = 3.0f,
            PipelineToken = 91.0f,
            TriangleOrdinal = 7,
            TriangleIndices = new Vector3(10.0f, 20.0f, 30.0f),
            Barycentric = new Vector3(0.2f, 0.3f, 0.5f),
            SurfaceNormal = new Vector3(0.0f, 0.6f, 0.8f),
        };

        controller.Step(Input(true, new Vector2(100.0f, 200.0f)), pick);

        Assert.IsTrue(controller.Valid);
        Assert.AreEqual(1u, controller.Generation);
        Assert.AreEqual(new Vector2(100.0f, 200.0f), controller.PressCursorPixels);
        Assert.AreEqual(17, controller.Pick.ObjectId);
        Assert.AreEqual(4, controller.Pick.SourceDraw);
        Assert.AreEqual(0.42f, controller.Pick.Depth);
        Assert.AreEqual(7, controller.Pick.TriangleOrdinal);
        Assert.AreEqual(new Vector3(10.0f, 20.0f, 30.0f), controller.Pick.TriangleIndices);
        Assert.AreEqual(new Vector3(0.0f, 0.6f, 0.8f), controller.Pick.SurfaceNormal);
        Assert.AreEqual(1.0f / 60.0f, controller.HoldSeconds, 1.0e-6f);
    }

    [TestMethod]
    public void HeldInputUpdatesCursorButDoesNotReplaceTheFrozenPick()
    {
        CpuCaptureState controller = new();
        CpuPick first = Pick(17, 4);
        controller.Step(Input(true, new Vector2(100.0f, 200.0f)), first);

        CpuPick later = Pick(99, 8);
        controller.Step(
            Input(true, new Vector2(300.0f, 400.0f), toward: 7, away: 2),
            later);

        Assert.AreEqual(17, controller.Pick.ObjectId);
        Assert.AreEqual(4, controller.Pick.SourceDraw);
        Assert.AreEqual(new Vector2(100.0f, 200.0f), controller.PressCursorPixels);
        Assert.AreEqual(new Vector2(300.0f, 400.0f), controller.CurrentCursorPixels);
        Assert.AreEqual(5, controller.WheelSequenceCode);
        Assert.AreEqual(1u, controller.Generation);
        Assert.AreEqual(2.0f / 60.0f, controller.HoldSeconds, 1.0e-6f);
    }

    [TestMethod]
    public void ReleasePreservesCaptureAndNextPressAdvancesGeneration()
    {
        CpuCaptureState controller = new();
        controller.Step(Input(true, new Vector2(100.0f)), Pick(17, 4));
        controller.Step(Input(false, new Vector2(120.0f)), Pick(99, 8));

        Assert.IsTrue(controller.Valid);
        Assert.AreEqual(17, controller.Pick.ObjectId);
        Assert.IsFalse(controller.PreviousHeld);

        controller.Step(Input(true, new Vector2(200.0f)), Pick(99, 8));

        Assert.AreEqual(2u, controller.Generation);
        Assert.AreEqual(99, controller.Pick.ObjectId);
        Assert.AreEqual(8, controller.Pick.SourceDraw);
        Assert.AreEqual(new Vector2(200.0f), controller.PressCursorPixels);
    }

    [TestMethod]
    public void InvalidNewPressClearsThePreviousCapture()
    {
        CpuCaptureState controller = new();
        controller.Step(Input(true, new Vector2(100.0f)), Pick(17, 4));
        controller.Step(Input(false, new Vector2(120.0f)), Pick(17, 4));
        controller.Step(Input(true, new Vector2(200.0f)), InvalidPick());

        Assert.IsFalse(controller.Valid);
        Assert.IsFalse(controller.Pick.Valid);
        Assert.AreEqual(2u, controller.Generation);
        Assert.AreEqual(new Vector2(200.0f), controller.PressCursorPixels);
    }

    [TestMethod]
    public void FrozenBasisIsOrthonormalEvenWhenThePickBasisIsNot()
    {
        CpuCaptureState controller = new();
        CpuPick pick = new(
            true,
            17,
            Vector3.Zero,
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(2.0f, 3.0f, 0.0f),
            4);

        controller.Step(Input(true, Vector2.Zero), pick);

        Assert.AreEqual(1.0f, controller.Pick.ScreenRight.Length(), 1.0e-6f);
        Assert.AreEqual(1.0f, controller.Pick.ScreenUp.Length(), 1.0e-6f);
        Assert.AreEqual(
            0.0f,
            Vector3.Dot(
                controller.Pick.ScreenRight,
                controller.Pick.ScreenUp),
            1.0e-6f);
    }

    private static CpuInput Input(
        bool held,
        Vector2 cursor,
        int toward = 0,
        int away = 0) =>
        new(
            cursor,
            new Vector2(1000.0f),
            held,
            toward,
            away,
            1.0f / 60.0f);

    private static CpuPick Pick(int objectId, int sourceDraw) =>
        new(
            true,
            objectId,
            new Vector3(1.0f, 2.0f, 3.0f),
            Vector3.UnitX,
            Vector3.UnitY,
            sourceDraw);

    private static CpuPick InvalidPick() =>
        new(false, 0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 0);
}
