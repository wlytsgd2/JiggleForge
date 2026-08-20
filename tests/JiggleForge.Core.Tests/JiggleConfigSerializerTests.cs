using JiggleForge.Core;

namespace JiggleForge.Core.Tests;

[TestClass]
public sealed class JiggleConfigSerializerTests
{
    [TestMethod]
    public void ConfigurationRoundTripsWithoutLosingGraphOrMaskData()
    {
        JiggleProjectConfig source = new()
        {
            ProjectId = Guid.Parse("62bfda16-a9ae-4e9a-97de-d89f8dc00cc7"),
            StateNamespace = 37,
        };
        source.Physics.HoldDampingRatio = 0.73;
        source.Physics.HoldFrequencyHz = 14.0;
        source.Physics.ReleaseDampingRatio = 0.81;
        source.Physics.ReleaseFrequencyHz = 1.4;
        source.Physics.ReleaseImpulse = 0.42;
        source.Physics.TargetFollowSeconds = 0.038;
        source.Physics.VolumeResponse = 3.25;
        source.Inspector.Enabled = true;
        source.Draws.Add(new JiggleDrawConfig
        {
            Id = "Draw0001",
            Alias = "Body Nude",
            SourceFile = "resources\\Character.ini",
            SourceSection = "CommandListBody",
            SourceLine = 541,
            Branch = "else if $swapvar == 2",
            Command = "drawindexed = auto",
            Kind = JiggleDrawKind.Auto,
            StateIndex = 9473,
            ObjectId = 9474,
            Group = "Body",
            Mask = "Masks\\Body.dds",
        });
        JiggleGroupConfig group = new()
        {
            Name = "Body",
            GraphX = 48.5,
            GraphY = 96.25,
            Physics = source.Physics.Clone(),
        };
        group.Physics.Radius = 0.18;
        group.Physics.Strength = 0.92;
        group.Draws.Add("Draw0001");
        source.Groups.Add(group);
        source.Draws.Add(new JiggleDrawConfig
        {
            Id = "Draw0002",
            DeformationEnabled = false,
            SourceFile = "resources\\Character.ini",
            SourceSection = "CommandListClothes",
            SourceLine = 620,
            Command = "drawindexed = 300, 0, 0",
            Kind = JiggleDrawKind.Numeric,
            Count = 300,
            FirstIndex = 0,
            BaseVertex = 0,
            StateIndex = 9474,
            ObjectId = 9475,
            Group = "Clothes",
        });
        JiggleGroupConfig clothes = new() { Name = "Clothes" };
        clothes.Draws.Add("Draw0002");
        source.Groups.Add(clothes);
        source.Edges.Add(new JiggleEdgeConfig { From = "Body", To = "Clothes" });

        string text = JiggleConfigSerializer.Serialize(source);
        JiggleProjectConfig result = JiggleConfigSerializer.Parse(text);

        Assert.AreEqual(source.ProjectId, result.ProjectId);
        Assert.AreEqual(37, result.StateNamespace);
        Assert.AreEqual("Body Nude", result.Draws[0].Alias);
        Assert.AreEqual("Masks\\Body.dds", result.Draws[0].Mask);
        Assert.AreEqual("Draw0001", result.Groups[0].Draws[0]);
        Assert.AreEqual(48.5, result.Groups[0].GraphX);
        Assert.AreEqual(96.25, result.Groups[0].GraphY);
        Assert.AreEqual(0.18, result.Groups[0].Physics!.Radius);
        Assert.AreEqual(0.92, result.Groups[0].Physics!.Strength);
        Assert.AreEqual("Clothes", result.Edges[0].To);
        Assert.IsFalse(result.Draws[1].DeformationEnabled);
        Assert.AreEqual(0.73, result.Physics.HoldDampingRatio);
        Assert.AreEqual(14.0, result.Physics.HoldFrequencyHz);
        Assert.AreEqual(0.81, result.Physics.ReleaseDampingRatio);
        Assert.AreEqual(1.4, result.Physics.ReleaseFrequencyHz);
        Assert.AreEqual(0.42, result.Physics.ReleaseImpulse);
        Assert.AreEqual(0.038, result.Physics.TargetFollowSeconds);
        Assert.AreEqual(3.25, result.Physics.VolumeResponse);
        Assert.IsTrue(result.Inspector.Enabled);
    }

    [TestMethod]
    public void RemovedOriginalPartsSwitchIsAcceptedButNotSerialized()
    {
        JiggleProjectConfig source = new()
        {
            ProjectId = Guid.Parse("62bfda16-a9ae-4e9a-97de-d89f8dc00cc7"),
            StateNamespace = 37,
        };
        source.Draws.Add(new JiggleDrawConfig
        {
            Id = "Draw0001",
            SourceFile = "Body.ini",
            SourceSection = "CommandListBody",
            SourceLine = 10,
            Command = "drawindexed = auto",
            Kind = JiggleDrawKind.Auto,
            StateIndex = 100,
            ObjectId = 101,
        });
        string current = JiggleConfigSerializer.Serialize(source);
        Assert.IsFalse(current.Contains("[OriginalParts]", StringComparison.Ordinal));
        string legacy = current + "\r\n[OriginalParts]\r\ndeform_enabled = false\r\n";

        JiggleProjectConfig result = JiggleConfigSerializer.Parse(legacy);

        Assert.IsTrue(result.Draws[0].DeformationEnabled);
        Assert.IsFalse(JiggleConfigSerializer.Serialize(result)
            .Contains("[OriginalParts]", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SchemaTwoOriginalPartsSwitchMigratesToAlwaysEnabledDefaultChannel()
    {
        JiggleProjectConfig source = new()
        {
            ProjectId = Guid.Parse("62bfda16-a9ae-4e9a-97de-d89f8dc00cc7"),
            StateNamespace = 37,
        };
        string legacy = JiggleConfigSerializer.Serialize(source)
            .Replace("schema = 3", "schema = 2", StringComparison.Ordinal) +
            "\r\n[OriginalParts]\r\ndeform_enabled = false\r\n";
        string root = Path.Combine(
            Path.GetTempPath(),
            "JiggleForgeSchemaTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        File.WriteAllText(path, legacy);

        try
        {
            JiggleProjectConfig migrated = JiggleConfigSerializer.Load(path);

            Assert.AreEqual(JiggleProjectConfig.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.IsTrue(File.Exists(path + ".schema2.bak"));
            string current = File.ReadAllText(path);
            Assert.IsFalse(current.Contains("[OriginalParts]", StringComparison.Ordinal));
            StringAssert.Contains(current, "schema = 3");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GroupsFromOlderConfigurationsCopyTheModPhysics()
    {
        JiggleProjectConfig source = new()
        {
            ProjectId = Guid.Parse("62bfda16-a9ae-4e9a-97de-d89f8dc00cc7"),
            StateNamespace = 37,
        };
        source.Physics.Radius = 0.41;
        source.Physics.Strength = 0.63;
        source.Groups.Add(new JiggleGroupConfig { Name = "Body" });
        string serialized = JiggleConfigSerializer.Serialize(source);
        string legacy = System.Text.RegularExpressions.Regex.Replace(
            serialized,
            @"(?ms)(^\[Group:Body\]\r?\ndraws\s*=\s*\[\]\r?\n)(?:radius|strength|falloff|volume_response|drag_scale|hold_damping_ratio|hold_frequency_hz|release_damping_ratio|release_frequency_hz|release_impulse|max_offset|target_follow_seconds|wheel_depth_step|wheel_min_depth|wheel_max_depth)\s*=\s*[^\r\n]+\r?\n",
            "$1");

        JiggleProjectConfig result = JiggleConfigSerializer.Parse(legacy);

        Assert.AreEqual(0.41, result.Groups[0].Physics!.Radius);
        Assert.AreEqual(0.63, result.Groups[0].Physics!.Strength);
    }

    [TestMethod]
    public void SchemaOnePhysicsMigratesToCurrentSchemaAndLoadCreatesABackup()
    {
        string legacy = """
            [Project]
            schema = 1
            project_id = 62bfda16-a9ae-4e9a-97de-d89f8dc00cc7
            state_namespace = 37

            [Physics]
            radius = 0.25
            strength = 0.7
            falloff = 2.2
            volume_response = 2.5
            drag_scale = 0.75
            grab_damping = 0.84
            grab_spring = 0.12
            release_damping = 0.9
            release_spring = 0.1
            release_kick = 0.3
            max_offset = 0.15
            target_follow = 0.3
            wheel_depth_step = 0.02
            wheel_min_depth = -0.15
            wheel_max_depth = 0.15

            [Inspector]
            enabled = false

            [OriginalParts]
            deform_enabled = false
            """;
        string root = Path.Combine(
            Path.GetTempPath(),
            "JiggleForgeSchemaTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, JiggleProjectConfig.DefaultFileName);
        File.WriteAllText(path, legacy);

        try
        {
            JiggleProjectConfig migrated = JiggleConfigSerializer.Load(path);

            Assert.AreEqual(JiggleProjectConfig.CurrentSchemaVersion, migrated.SchemaVersion);
            Assert.AreEqual(10.0, migrated.Physics.HoldFrequencyHz, 0.0001);
            Assert.AreEqual(2.2, migrated.Physics.ReleaseFrequencyHz, 0.0001);
            Assert.AreEqual(0.12, migrated.Physics.ReleaseImpulse, 0.0001);
            Assert.AreEqual(0.02, migrated.Physics.TargetFollowSeconds, 0.0001);
            Assert.IsTrue(File.Exists(path + ".schema1.bak"));
            string current = File.ReadAllText(path);
            StringAssert.Contains(current, $"schema = {JiggleProjectConfig.CurrentSchemaVersion}");
            StringAssert.Contains(current, "hold_frequency_hz = 10");
            Assert.IsFalse(current.Contains("grab_spring", StringComparison.Ordinal));
            Assert.IsFalse(current.Contains("[OriginalParts]", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
