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
    private void CopyQqGroup_Click(object sender, RoutedEventArgs e)
    {
        DataPackage package = new();
        package.SetText("451901293");
        Clipboard.SetContent(package);
        Clipboard.Flush();
        ShowMessage(L("QqCopied"), InfoBarSeverity.Success);
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", currentInspection.ModPath) { UseShellExecute = true });
    }

    private async void ChooseRuntimeFolder_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        ZzmiPathResolution resolution = ZzmiPathResolver.Resolve(folder.Path);
        if (!resolution.IsValid)
        {
            RuntimePathTextBox.Text = folder.Path;
            RuntimePathValidationText.Text = AppLanguageService.Format(
                "InvalidPath",
                AppLanguageService.Localize(resolution.Message));
            ShowMessage(L("NoValidZzmiRoot"), InfoBarSeverity.Warning);
            return;
        }

        ApplyResolvedRuntimePath(resolution, showCorrectionMessage: true);
        await RefreshRuntimeStatusAsync();
        await RefreshModLibraryAsync();
    }

    private async void RefreshRuntime_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRuntimeStatusAsync();
        await RefreshModLibraryAsync();
    }

    private async void InstallRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string runtimePath))
        {
            return;
        }
        IReadOnlyList<string> dragKeys = GetSelectedDragKeys();
        SaveDragKeyPreference(dragKeys);
        if (RuntimeEnvironmentService.IsWheelBridgeRunning() &&
            !await RunWheelOperationOnUiThreadAsync(
                () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
                L("WheelStoppedForUpdate")))
        {
            return;
        }

        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.Install(runtimePath, dragKeys, defaultPhysics),
            L("RuntimeInstalled"));
    }

    private async void ApplyDragKey_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string runtimePath))
        {
            return;
        }
        IReadOnlyList<string> dragKeys = GetSelectedDragKeys();
        SaveDragKeyPreference(dragKeys);
        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.SetDragKeys(runtimePath, dragKeys),
            L("DragKeysUpdated"));
    }

    private async void SaveDefaultPhysics_Click(object sender, RoutedEventArgs e)
    {
        DefaultPhysicsSaveStatusText.Text = L("Saving");
        DefaultPhysicsSaveStatusText.Visibility = Visibility.Visible;
        SaveDefaultPhysicsButton.IsEnabled = false;
        try
        {
            PhysicsSettings settings = ReadDefaultPhysicsEditor();
            JiggleProjectConfig validationConfig = new() { Physics = settings.Clone() };
            IReadOnlyList<string> errors = JiggleConfigValidator.Validate(validationConfig);
            if (errors.Count > 0)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, errors));
            }

            SaveDefaultPhysicsPreference(settings);
            defaultPhysics = settings;
            if (!TryGetValidatedRuntimePath(out string runtimePath))
            {
                DefaultPhysicsSaveStatusText.Text = L("DefaultsSavedInvalidZzmi");
                return;
            }
            RuntimeEnvironmentStatus liveStatus = await Task.Run(
                () => runtimeEnvironmentService.Inspect(runtimePath));
            if (liveStatus.RuntimeInstalled)
            {
                bool updated = await RunRuntimeOperationAsync(
                    () => runtimeEnvironmentService.SetDefaultPhysics(runtimePath, settings),
                    L("DefaultsSavedToRuntime"));
                DefaultPhysicsSaveStatusText.Text = updated
                    ? L("SavedToGame")
                    : L("SavedButRuntimeWriteFailed");
            }
            else
            {
                DefaultPhysicsSaveStatusText.Text = L("SavedInstallLater");
                ShowMessage(
                    L("GlobalDefaultsSaved"),
                    InfoBarSeverity.Success);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          InvalidDataException or ArgumentException)
        {
            DefaultPhysicsSaveStatusText.Text = L("SaveFailedSeeError");
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
        }
        finally
        {
            SaveDefaultPhysicsButton.IsEnabled = true;
        }
    }

    private async void UninstallRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string runtimePath))
        {
            return;
        }
        RuntimeOverallStatusText.Text = L("UninstallingRuntime");
        RuntimeDetailText.Text = L("UninstallingRuntimeDetails");
        ShowMessage(L("UninstallingRuntimeMessage"), InfoBarSeverity.Informational);
        await Task.Yield();

        if (RuntimeEnvironmentService.IsWheelBridgeRunning() &&
            !await RunWheelOperationOnUiThreadAsync(
                () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
                L("WheelStoppedForUninstall")))
        {
            return;
        }

        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.Uninstall(runtimePath, stopWheelBridge: false),
            L("RuntimeUninstalled"));
    }

    private async void StartWheel_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string runtimePath))
        {
            return;
        }
        RuntimeOverallStatusText.Text = L("WaitingForAdmin");
        RuntimeDetailText.Text = L("WheelAdminDescription");
        ShowMessage(L("WaitingWheelAdmin"), InfoBarSeverity.Informational);
        await RunWheelOperationOnUiThreadAsync(
            () => runtimeEnvironmentService.StartWheelBridge(runtimePath),
            L("WheelStarted"));
    }

    private async void StopWheel_Click(object sender, RoutedEventArgs e)
    {
        await RunWheelOperationOnUiThreadAsync(
            () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
            L("WheelStopped"));
    }

    private async Task<bool> RunWheelOperationOnUiThreadAsync(Action operation, string successMessage)
    {
        SetRuntimeBusy(true);
        try
        {
            await Task.Delay(50);
            operation();
            await Task.Delay(400);
            await RefreshRuntimeStatusAsync(showErrors: false);
            ShowMessage(successMessage, InfoBarSeverity.Success);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          InvalidOperationException or ArgumentException)
        {
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
            await RefreshRuntimeStatusAsync(showErrors: false);
            return false;
        }
        finally
        {
            SetRuntimeBusy(false);
        }
    }

    private async Task<bool> RunRuntimeOperationAsync(Action operation, string successMessage)
    {
        SetRuntimeBusy(true);
        try
        {
            await Task.Run(operation);
            await RefreshRuntimeStatusAsync(showErrors: false);
            ShowMessage(successMessage, InfoBarSeverity.Success);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          InvalidOperationException or ArgumentException)
        {
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
            await RefreshRuntimeStatusAsync(showErrors: false);
            return false;
        }
        finally
        {
            SetRuntimeBusy(false);
        }
    }

    private async Task RefreshRuntimeStatusAsync(bool showErrors = true)
    {
        SetRuntimeBusy(true);
        try
        {
            ZzmiPathResolution resolution = ZzmiPathResolver.Resolve(RuntimePathTextBox.Text);
            if (!resolution.IsValid)
            {
                runtimeStatus = null;
                RuntimePathValidationText.Text = AppLanguageService.Format(
                    "InvalidPath",
                    AppLanguageService.Localize(resolution.Message));
                RuntimeOverallStatusText.Text = L("ValidZzmiNotFound");
                RuntimeDetailText.Text = L("ChooseZzmiRootDetails");
                RuntimeComponentStatusText.Text = L("Unknown");
                ShaderFixStatusText.Text = L("Unknown");
                WheelBridgeStatusText.Text = RuntimeEnvironmentService.IsWheelBridgeRunning() ? L("Running") : L("NotRunning");
                if (showErrors)
                {
                    ShowMessage(
                        AppLanguageService.Format(
                            "InvalidZzmiPath",
                            AppLanguageService.Localize(resolution.Message)),
                        InfoBarSeverity.Warning);
                }
                return;
            }

            ApplyResolvedRuntimePath(resolution, showCorrectionMessage: showErrors);
            runtimeStatus = await Task.Run(() => runtimeEnvironmentService.Inspect(resolution.ResolvedPath));
            if (runtimeStatus.DragKeys is not null)
            {
                SelectDragKeys(runtimeStatus.DragKeys);
                SaveDragKeyPreference(runtimeStatus.DragKeys);
            }
            RenderRuntimeStatus(runtimeStatus);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            runtimeStatus = null;
            RuntimeOverallStatusText.Text = L("CannotCheckRuntime");
            RuntimeDetailText.Text = AppLanguageService.LocalizeException(exception);
            RuntimeComponentStatusText.Text = L("Unknown");
            ShaderFixStatusText.Text = L("Unknown");
            WheelBridgeStatusText.Text = RuntimeEnvironmentService.IsWheelBridgeRunning() ? L("Running") : L("NotRunning");
            if (showErrors)
            {
                ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
            }
        }
        finally
        {
            SetRuntimeBusy(false);
        }
    }

    private void RenderRuntimeStatus(RuntimeEnvironmentStatus status)
    {
        RuntimeOverallStatusText.Text = status.Ready
            ? L("RuntimeReady")
            : status.ZzmiRootExists
                ? L("RuntimeNeedsInstall")
                : L("ZzmiNotFound");

        if (!status.PayloadAvailable)
        {
            RuntimeDetailText.Text = L("RuntimePayloadMissing");
        }
        else if (!status.ZzmiRootExists)
        {
            RuntimeDetailText.Text = AppLanguageService.Format("DirectoryDoesNotExist", status.ZzmiRoot);
        }
        else if (!status.ModsDirectoryExists || !status.ShaderFixesDirectoryExists)
        {
            RuntimeDetailText.Text = L("ZzmiMustContainFolders");
        }
        else if (status.Ready)
        {
            RuntimeDetailText.Text = status.BackupCount > 0
                ? AppLanguageService.Format("AllComponentsCurrentWithBackups", status.BackupCount)
                : L("AllComponentsCurrent");
        }
        else
        {
            RuntimeDetailText.Text = L("InstallRuntimeHint");
        }

        RuntimeComponentStatusText.Text = !status.RuntimeInstalled
            ? L("NotInstalled")
            : status.RuntimeCurrent ? L("CurrentVersion") : L("NeedsUpdate");
        ShaderFixStatusText.Text = AppLanguageService.Format("ShaderFixCurrentCount", status.CurrentShaderCount, status.RequiredShaderCount);
        if (status.InstalledShaderCount != status.CurrentShaderCount)
        {
            ShaderFixStatusText.Text += AppLanguageService.Format("InstalledShaderCount", status.InstalledShaderCount);
        }
        WheelBridgeStatusText.Text = status.WheelBridgeRunning ? L("Running") : L("NotRunning");
        RuntimePathValidationText.Text = AppLanguageService.Format("ValidPath", status.ZzmiRoot);

        bool directoriesReady = status.PayloadAvailable && status.ModsDirectoryExists && status.ShaderFixesDirectoryExists;
        InstallRuntimeButton.IsEnabled = directoriesReady;
        UninstallRuntimeButton.IsEnabled = status.RuntimePresent || status.InstalledShaderCount > 0 || status.BackupCount > 0;
        StartWheelButton.IsEnabled = status.RuntimeInstalled && !status.WheelBridgeRunning;
        StopWheelButton.IsEnabled = status.WheelBridgeRunning;
        DragKeyOptionsList.IsEnabled = true;
        ApplyDragKeyButton.IsEnabled = status.RuntimeInstalled;
        RefreshRuntimeButton.IsEnabled = true;
    }

    private void SetRuntimeBusy(bool busy)
    {
        runtimeBusyCount = busy ? runtimeBusyCount + 1 : Math.Max(0, runtimeBusyCount - 1);
        bool isBusy = runtimeBusyCount > 0;
        RuntimeBusyRing.IsActive = isBusy;
        RuntimePathTextBox.IsEnabled = !isBusy;
        DragKeyOptionsList.IsEnabled = !isBusy;
        if (isBusy)
        {
            InstallRuntimeButton.IsEnabled = false;
            UninstallRuntimeButton.IsEnabled = false;
            StartWheelButton.IsEnabled = false;
            StopWheelButton.IsEnabled = false;
            ApplyDragKeyButton.IsEnabled = false;
            RefreshRuntimeButton.IsEnabled = false;
        }
        else if (runtimeStatus is not null)
        {
            RenderRuntimeStatus(runtimeStatus);
        }
        else
        {
            RefreshRuntimeButton.IsEnabled = true;
        }
    }

    private IReadOnlyList<string> GetSelectedDragKeys()
    {
        List<string> selected = dragKeyOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Key)
            .ToList();
        if (selected.Count == 0)
        {
            DragKeyOption fallback = dragKeyOptions.First(option =>
                string.Equals(option.Key, RuntimeEnvironmentService.DefaultDragKey, StringComparison.OrdinalIgnoreCase));
            fallback.IsSelected = true;
            selected.Add(fallback.Key);
        }

        return selected;
    }

    private void SelectDragKeys(IEnumerable<string> dragKeys)
    {
        HashSet<string> selected = dragKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (DragKeyOption option in dragKeyOptions)
        {
            option.IsSelected = selected.Contains(option.Key);
        }

        if (!dragKeyOptions.Any(option => option.IsSelected))
        {
            dragKeyOptions[0].IsSelected = true;
        }
    }

    private static IReadOnlyList<string> LoadDragKeyPreference()
    {
        try
        {
            if (File.Exists(DragKeyPreferencePath))
            {
                string saved = File.ReadAllText(DragKeyPreferencePath);
                List<string> supported = saved.Split([',', ';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(token => RuntimeEnvironmentService.SupportedDragKeys.FirstOrDefault(
                        key => string.Equals(key, token, StringComparison.OrdinalIgnoreCase)))
                    .Where(key => key is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (supported.Count > 0)
                {
                    return supported;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return [RuntimeEnvironmentService.DefaultDragKey];
    }

    private static void SaveDragKeyPreference(IEnumerable<string> dragKeys)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DragKeyPreferencePath)!);
            File.WriteAllText(DragKeyPreferencePath, string.Join(",", dragKeys));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string LoadZzmiRootPreference()
    {
        try
        {
            if (File.Exists(ZzmiRootPreferencePath))
            {
                string saved = File.ReadAllText(ZzmiRootPreferencePath).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    return saved;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return RuntimeEnvironmentService.DefaultZzmiRoot;
    }

    private static void SaveZzmiRootPreference(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ZzmiRootPreferencePath)!);
            File.WriteAllText(
                ZzmiRootPreferencePath,
                path.Trim().Trim('"'),
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private bool TryGetValidatedRuntimePath(out string runtimePath)
    {
        ZzmiPathResolution resolution = ZzmiPathResolver.Resolve(RuntimePathTextBox.Text);
        if (!resolution.IsValid)
        {
            runtimePath = string.Empty;
            RuntimePathValidationText.Text = AppLanguageService.Format(
                "InvalidPath",
                AppLanguageService.Localize(resolution.Message));
            ShowMessage(
                AppLanguageService.Format(
                    "InvalidZzmiPath",
                    AppLanguageService.Localize(resolution.Message)),
                InfoBarSeverity.Error);
            return false;
        }

        ApplyResolvedRuntimePath(resolution, showCorrectionMessage: true);
        runtimePath = resolution.ResolvedPath;
        return true;
    }

    private void ApplyResolvedRuntimePath(
        ZzmiPathResolution resolution,
        bool showCorrectionMessage)
    {
        RuntimePathTextBox.Text = resolution.ResolvedPath;
        RuntimePathValidationText.Text = resolution.WasCorrected
            ? AppLanguageService.Format("AutoCorrectedPath", resolution.ResolvedPath)
            : AppLanguageService.Format("ValidPath", resolution.ResolvedPath);
        SaveZzmiRootPreference(resolution.ResolvedPath);
        if (resolution.WasCorrected && showCorrectionMessage)
        {
            ShowMessage(AppLanguageService.Format("AutoLocatedZzmiRoot", resolution.ResolvedPath), InfoBarSeverity.Informational);
        }
    }

}
