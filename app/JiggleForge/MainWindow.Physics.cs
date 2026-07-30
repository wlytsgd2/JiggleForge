using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using JiggleForge.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Line = Microsoft.UI.Xaml.Shapes.Line;
using Polygon = Microsoft.UI.Xaml.Shapes.Polygon;

namespace JiggleForge;

public sealed partial class MainWindow : Window
{
    private void InitializeProjectPhysicsEditor(JiggleProjectConfig config)
    {
        physicsScopeChanging = true;
        editorPhysicsByScope.Clear();
        editorPhysicsByScope[DefaultPhysicsScopeKey] = config.Physics.Clone();
        foreach (string groupName in editorGroupNames)
        {
            JiggleGroupConfig? group = config.Groups.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, groupName, StringComparison.OrdinalIgnoreCase));
            editorPhysicsByScope[groupName] =
                (group?.Physics ?? config.Physics).Clone();
        }

        activePhysicsScopeKey = DefaultPhysicsScopeKey;
        RefreshPhysicsScopeOptions(DefaultPhysicsScopeKey, commitCurrent: false);
        LoadProjectPhysicsFields(editorPhysicsByScope[DefaultPhysicsScopeKey]);
        physicsScopeChanging = false;
    }

    private void RefreshPhysicsScopeOptions(
        string? preferredKey = null,
        bool commitCurrent = true)
    {
        if (commitCurrent)
        {
            CommitCurrentPhysicsScope();
        }

        string selection = preferredKey ?? activePhysicsScopeKey;
        physicsScopeChanging = true;
        physicsScopeOptions.Clear();
        physicsScopeOptions.Add(new PhysicsScopeOption(
            DefaultPhysicsScopeKey,
            "Mod 默认参数（未分组 Draw）"));
        foreach (string groupName in editorGroupNames
                     .OrderBy(name =>
                         string.Equals(name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)
                             ? 0
                             : 1)
                     .ThenBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            editorPhysicsByScope.TryAdd(
                groupName,
                GetDefaultProjectPhysics().Clone());
            string displayName = string.Equals(
                groupName,
                OriginalPartsConfig.GroupName,
                StringComparison.OrdinalIgnoreCase)
                ? "OriginalParts · 原版部件"
                : groupName;
            physicsScopeOptions.Add(new PhysicsScopeOption(groupName, displayName));
        }

        PhysicsScopeOption selected = physicsScopeOptions.FirstOrDefault(option =>
                string.Equals(option.Key, selection, StringComparison.OrdinalIgnoreCase))
            ?? physicsScopeOptions[0];
        activePhysicsScopeKey = selected.Key;
        PhysicsScopeComboBox.SelectedItem = selected;
        LoadProjectPhysicsFields(editorPhysicsByScope[selected.Key]);
        physicsScopeChanging = false;
    }

    private void CommitCurrentPhysicsScope()
    {
        if (physicsScopeChanging ||
            !editorPhysicsByScope.ContainsKey(activePhysicsScopeKey))
        {
            return;
        }

        editorPhysicsByScope[activePhysicsScopeKey] = ReadProjectPhysicsFields();
    }

    private void PhysicsScopeComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (physicsScopeChanging ||
            PhysicsScopeComboBox.SelectedItem is not PhysicsScopeOption selected)
        {
            return;
        }

        CommitCurrentPhysicsScope();
        activePhysicsScopeKey = selected.Key;
        if (editorPhysicsByScope.TryGetValue(selected.Key, out PhysicsSettings? physics))
        {
            physicsScopeChanging = true;
            LoadProjectPhysicsFields(physics);
            physicsScopeChanging = false;
        }
    }

    private void CopyDefaultPhysicsToGroup_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(
                activePhysicsScopeKey,
                DefaultPhysicsScopeKey,
                StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage("当前正在编辑 Mod 默认参数。", InfoBarSeverity.Informational);
            return;
        }

        PhysicsSettings copied = GetDefaultProjectPhysics().Clone();
        editorPhysicsByScope[activePhysicsScopeKey] = copied;
        LoadProjectPhysicsFields(copied);
        ShowMessage($"已把 Mod 默认参数复制到组“{activePhysicsScopeKey}”。", InfoBarSeverity.Success);
    }

    private PhysicsSettings GetDefaultProjectPhysics() =>
        editorPhysicsByScope.TryGetValue(DefaultPhysicsScopeKey, out PhysicsSettings? physics)
            ? physics
            : new PhysicsSettings();

    private void LoadProjectPhysicsFields(PhysicsSettings physics)
    {
        RadiusNumber.Value = physics.Radius;
        StrengthNumber.Value = physics.Strength;
        FalloffNumber.Value = physics.Falloff;
        VolumeResponseNumber.Value = physics.VolumeResponse;
        DragScaleNumber.Value = physics.DragScale;
        GrabDampingNumber.Value = physics.HoldDampingRatio;
        GrabSpringNumber.Value = physics.HoldFrequencyHz;
        ReleaseDampingNumber.Value = physics.ReleaseDampingRatio;
        ReleaseSpringNumber.Value = physics.ReleaseFrequencyHz;
        ReleaseKickNumber.Value = physics.ReleaseImpulse;
        TargetFollowNumber.Value = physics.TargetFollowSeconds;
        MaxOffsetNumber.Value = physics.MaxOffset;
        WheelStepNumber.Value = physics.WheelDepthStep;
        WheelMinDepthNumber.Value = physics.WheelMinDepth;
        WheelMaxDepthNumber.Value = physics.WheelMaxDepth;
    }

    private PhysicsSettings ReadProjectPhysicsFields() => new()
    {
        Radius = RadiusNumber.Value,
        Strength = StrengthNumber.Value,
        Falloff = FalloffNumber.Value,
        VolumeResponse = VolumeResponseNumber.Value,
        DragScale = DragScaleNumber.Value,
        HoldDampingRatio = GrabDampingNumber.Value,
        HoldFrequencyHz = GrabSpringNumber.Value,
        ReleaseDampingRatio = ReleaseDampingNumber.Value,
        ReleaseFrequencyHz = ReleaseSpringNumber.Value,
        ReleaseImpulse = ReleaseKickNumber.Value,
        TargetFollowSeconds = TargetFollowNumber.Value,
        MaxOffset = MaxOffsetNumber.Value,
        WheelDepthStep = WheelStepNumber.Value,
        WheelMinDepth = WheelMinDepthNumber.Value,
        WheelMaxDepth = WheelMaxDepthNumber.Value,
    };

    private void LoadDefaultPhysicsEditor(PhysicsSettings physics)
    {
        DefaultRadiusNumber.Value = physics.Radius;
        DefaultStrengthNumber.Value = physics.Strength;
        DefaultFalloffNumber.Value = physics.Falloff;
        DefaultVolumeResponseNumber.Value = physics.VolumeResponse;
        DefaultDragScaleNumber.Value = physics.DragScale;
        DefaultGrabDampingNumber.Value = physics.HoldDampingRatio;
        DefaultGrabSpringNumber.Value = physics.HoldFrequencyHz;
        DefaultReleaseDampingNumber.Value = physics.ReleaseDampingRatio;
        DefaultReleaseSpringNumber.Value = physics.ReleaseFrequencyHz;
        DefaultReleaseKickNumber.Value = physics.ReleaseImpulse;
        DefaultTargetFollowNumber.Value = physics.TargetFollowSeconds;
        DefaultMaxOffsetNumber.Value = physics.MaxOffset;
        DefaultWheelStepNumber.Value = physics.WheelDepthStep;
        DefaultWheelMinDepthNumber.Value = physics.WheelMinDepth;
        DefaultWheelMaxDepthNumber.Value = physics.WheelMaxDepth;
    }

    private PhysicsSettings ReadDefaultPhysicsEditor() => new()
    {
        Radius = DefaultRadiusNumber.Value,
        Strength = DefaultStrengthNumber.Value,
        Falloff = DefaultFalloffNumber.Value,
        VolumeResponse = DefaultVolumeResponseNumber.Value,
        DragScale = DefaultDragScaleNumber.Value,
        HoldDampingRatio = DefaultGrabDampingNumber.Value,
        HoldFrequencyHz = DefaultGrabSpringNumber.Value,
        ReleaseDampingRatio = DefaultReleaseDampingNumber.Value,
        ReleaseFrequencyHz = DefaultReleaseSpringNumber.Value,
        ReleaseImpulse = DefaultReleaseKickNumber.Value,
        TargetFollowSeconds = DefaultTargetFollowNumber.Value,
        MaxOffset = DefaultMaxOffsetNumber.Value,
        WheelDepthStep = DefaultWheelStepNumber.Value,
        WheelMinDepth = DefaultWheelMinDepthNumber.Value,
        WheelMaxDepth = DefaultWheelMaxDepthNumber.Value,
    };

    private static PhysicsSettings LoadDefaultPhysicsPreference()
    {
        try
        {
            if (File.Exists(PhysicsDefaultsPath))
            {
                string json = File.ReadAllText(PhysicsDefaultsPath);
                PhysicsSettings? saved = JsonSerializer.Deserialize<PhysicsSettings>(json);
                if (saved is not null)
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    JsonElement root = document.RootElement;
                    if (TryReadLegacyNumber(root, "GrabDamping", out double grabDamping))
                    {
                        saved.HoldDampingRatio = grabDamping;
                    }
                    if (TryReadLegacyNumber(root, "GrabSpring", out double grabSpring))
                    {
                        saved.HoldFrequencyHz =
                            PhysicsSchemaMigration.HoldFrequencyFromV1(grabSpring);
                    }
                    if (TryReadLegacyNumber(root, "ReleaseDamping", out double releaseDamping))
                    {
                        saved.ReleaseDampingRatio = releaseDamping;
                    }
                    if (TryReadLegacyNumber(root, "ReleaseSpring", out double releaseSpring))
                    {
                        saved.ReleaseFrequencyHz =
                            PhysicsSchemaMigration.ReleaseFrequencyFromV1(releaseSpring);
                    }
                    if (TryReadLegacyNumber(root, "ReleaseKick", out double releaseKick))
                    {
                        saved.ReleaseImpulse =
                            PhysicsSchemaMigration.ReleaseImpulseFromV1(releaseKick);
                    }
                    if (TryReadLegacyNumber(root, "TargetFollow", out double targetFollow))
                    {
                        saved.TargetFollowSeconds =
                            PhysicsSchemaMigration.TargetFollowSecondsFromV1(targetFollow);
                    }
                    if (document.RootElement.TryGetProperty("WheelAngleStep", out JsonElement legacyAngleStep) &&
                        legacyAngleStep.TryGetDouble(out double angleStep))
                    {
                        saved.WheelDepthStep = Math.Clamp(angleStep / 400.0, 0.00025, 0.075);
                    }

                    JiggleProjectConfig validationConfig = new() { Physics = saved.Clone() };
                    if (JiggleConfigValidator.Validate(validationConfig).Count == 0)
                    {
                        return saved;
                    }
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }

        return new PhysicsSettings();
    }

    private static bool TryReadLegacyNumber(
        JsonElement root,
        string propertyName,
        out double value)
    {
        if (root.TryGetProperty(propertyName, out JsonElement property) &&
            property.TryGetDouble(out value))
        {
            return true;
        }

        value = 0.0;
        return false;
    }

    private static void SaveDefaultPhysicsPreference(PhysicsSettings physics)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PhysicsDefaultsPath)!);
        File.WriteAllText(
            PhysicsDefaultsPath,
            JsonSerializer.Serialize(physics, new JsonSerializerOptions { WriteIndented = true }));
    }

}
