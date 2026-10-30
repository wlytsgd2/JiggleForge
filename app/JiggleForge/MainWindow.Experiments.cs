using System.Diagnostics;
using System.Text.Json;
using JiggleForge.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private static string BoneWeightPreferencePath => Path.Combine(ApplicationSettingsDirectory, "BoneWeightEnabled.txt");

    private static bool LoadBoneWeightPreference()
    {
        try { return !File.Exists(BoneWeightPreferencePath) || !bool.TryParse(File.ReadAllText(BoneWeightPreferencePath), out bool saved) || saved; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return true; }
    }

    private static void SaveBoneWeightPreference(bool enabled)
    {
        Directory.CreateDirectory(ApplicationSettingsDirectory);
        File.WriteAllText(BoneWeightPreferencePath, enabled.ToString());
    }

    private async void ApplyBoneWeight_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string root)) return;
        bool enabled = BoneWeightToggle.IsOn;
        bool success = await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.SetBoneWeightEnabled(root, enabled), L("BoneWeightUpdated"));
        if (success)
        {
            try { SaveBoneWeightPreference(enabled); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Warning); }
        }
    }

    private void OpenDefaultPhysicsLab_Click(object sender, RoutedEventArgs e) => OpenPhysicsLab(ReadDefaultPhysicsEditor());
    private void OpenProjectPhysicsLab_Click(object sender, RoutedEventArgs e) => OpenPhysicsLab(ReadProjectPhysicsFields());

    private void OpenPhysicsLab(PhysicsSettings physics)
    {
        try
        {
            if (JiggleConfigValidator.Validate(new JiggleProjectConfig { Physics = physics }).Any(error => error.StartsWith("Physics ", StringComparison.Ordinal)))
            { ShowMessage(L("PhysicsLabInvalidParameters"), InfoBarSeverity.Warning); return; }
            string file = Path.Combine(AppContext.BaseDirectory, "Lab", "index.html");
            if (!File.Exists(file)) { ShowMessage(L("PhysicsLabMissing"), InfoBarSeverity.Warning); return; }
            string config = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                boneWeightEnabled = runtimeEnvironmentService.SupportsBoneWeights && BoneWeightToggle.IsOn,
                language = AppLanguageService.CurrentLanguage,
                physics,
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            string url = new Uri(file).AbsoluteUri + "#config=" + Uri.EscapeDataString(config);
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        { ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error); }
    }
}
