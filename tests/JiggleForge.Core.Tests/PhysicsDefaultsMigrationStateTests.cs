namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class PhysicsDefaultsMigrationStateTests
{
    [TestMethod]
    public void RecommendedDefaults_MatchVersion018Release()
    {
        PhysicsSettings physics = new();

        Assert.AreEqual(0.12, physics.Radius, 0.000001);
        Assert.AreEqual(0.7, physics.Strength, 0.000001);
        Assert.AreEqual(1.0, physics.Falloff, 0.000001);
        Assert.AreEqual(2.0, physics.VolumeResponse, 0.000001);
        Assert.AreEqual(0.75, physics.DragScale, 0.000001);
        Assert.AreEqual(0.84, physics.HoldDampingRatio, 0.000001);
        Assert.AreEqual(10.0, physics.HoldFrequencyHz, 0.000001);
        Assert.AreEqual(0.0, physics.ReleaseDampingRatio, 0.000001);
        Assert.AreEqual(5.0, physics.ReleaseFrequencyHz, 0.000001);
        Assert.AreEqual(5.0, physics.ReleaseImpulse, 0.000001);
        Assert.AreEqual(0.1, physics.MaxOffset, 0.000001);
        Assert.AreEqual(0.02, physics.TargetFollowSeconds, 0.000001);
        Assert.AreEqual(0.02, physics.WheelDepthStep, 0.000001);
        Assert.AreEqual(0.0, physics.WheelMinDepth, 0.000001);
        Assert.AreEqual(0.15, physics.WheelMaxDepth, 0.000001);
    }

    [TestMethod]
    public void MissingMarker_RequiresMigrationAndMarkingPersists()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            PhysicsDefaultsMigrationState state = new(root);

            Assert.IsTrue(state.IsRequired);
            Assert.AreEqual(0, state.ReadAppliedVersion());

            state.MarkApplied();

            PhysicsDefaultsMigrationState reloaded = new(root);
            Assert.IsFalse(reloaded.IsRequired);
            Assert.AreEqual(PhysicsDefaultsMigrationState.CurrentVersion, reloaded.ReadAppliedVersion());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void InvalidMarker_RequiresMigration()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(root, PhysicsDefaultsMigrationState.FileName),
                "invalid");

            Assert.IsTrue(new PhysicsDefaultsMigrationState(root).IsRequired);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "JiggleForge-PhysicsMigration-" + Guid.NewGuid());
        Directory.CreateDirectory(path);
        return path;
    }
}
