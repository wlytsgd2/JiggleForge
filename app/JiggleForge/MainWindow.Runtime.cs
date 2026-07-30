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

        RuntimePathTextBox.Text = folder.Path;
        await RefreshRuntimeStatusAsync();
    }

    private async void RefreshRuntime_Click(object sender, RoutedEventArgs e)
    {
        await RefreshRuntimeStatusAsync();
    }

    private async void InstallRuntime_Click(object sender, RoutedEventArgs e)
    {
        string runtimePath = RuntimePathTextBox.Text;
        IReadOnlyList<string> dragKeys = GetSelectedDragKeys();
        SaveDragKeyPreference(dragKeys);
        if (RuntimeEnvironmentService.IsWheelBridgeRunning() &&
            !await RunWheelOperationOnUiThreadAsync(
                () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
                "WheelBridge 已停止，可以更新运行环境。"))
        {
            return;
        }

        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.Install(runtimePath, dragKeys, defaultPhysics),
            "运行环境已安装或更新。回到游戏按 F10 生效。");
    }

    private async void ApplyDragKey_Click(object sender, RoutedEventArgs e)
    {
        string runtimePath = RuntimePathTextBox.Text;
        IReadOnlyList<string> dragKeys = GetSelectedDragKeys();
        SaveDragKeyPreference(dragKeys);
        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.SetDragKeys(runtimePath, dragKeys),
            "全局拖动键已更新。按下其中任意一个都可以拖动；回到游戏按 F10 生效。");
    }

    private async void SaveDefaultPhysics_Click(object sender, RoutedEventArgs e)
    {
        DefaultPhysicsSaveStatusText.Text = "正在保存…";
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
            string runtimePath = RuntimePathTextBox.Text;
            RuntimeEnvironmentStatus liveStatus = await Task.Run(
                () => runtimeEnvironmentService.Inspect(runtimePath));
            if (liveStatus.RuntimeInstalled)
            {
                bool updated = await RunRuntimeOperationAsync(
                    () => runtimeEnvironmentService.SetDefaultPhysics(runtimePath, settings),
                    "全局默认物理参数已保存并写入游戏运行环境。现有 Mod 保持各自参数；回到游戏按 F10 生效。");
                DefaultPhysicsSaveStatusText.Text = updated
                    ? "已保存并写入游戏；按 F10 生效。"
                    : "应用设置已保存，但游戏运行时写入失败；请查看顶部错误信息。";
            }
            else
            {
                DefaultPhysicsSaveStatusText.Text = "已保存；安装运行环境时会自动写入游戏。";
                ShowMessage(
                    "全局默认物理参数已保存。首次生成 Mod 时会使用这些值；安装运行环境后也会用于无 Mod 角色。",
                    InfoBarSeverity.Success);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          InvalidDataException or ArgumentException)
        {
            DefaultPhysicsSaveStatusText.Text = "保存失败；请查看顶部错误信息。";
            ShowMessage(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SaveDefaultPhysicsButton.IsEnabled = true;
        }
    }

    private async void UninstallRuntime_Click(object sender, RoutedEventArgs e)
    {
        string runtimePath = RuntimePathTextBox.Text;
        RuntimeOverallStatusText.Text = "正在卸载运行环境";
        RuntimeDetailText.Text = "正在移除全局运行时和 6 个 JiggleForge ShaderFix；安装前备份会自动恢复。";
        ShowMessage("正在卸载运行环境…", InfoBarSeverity.Informational);
        await Task.Yield();

        if (RuntimeEnvironmentService.IsWheelBridgeRunning() &&
            !await RunWheelOperationOnUiThreadAsync(
                () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
                "WheelBridge 已停止，可以卸载运行环境。"))
        {
            return;
        }

        await RunRuntimeOperationAsync(
            () => runtimeEnvironmentService.Uninstall(runtimePath, stopWheelBridge: false),
            "运行环境已卸载；安装前备份的 ShaderFix 已恢复。回到游戏按 F10 生效。");
    }

    private async void StartWheel_Click(object sender, RoutedEventArgs e)
    {
        RuntimeOverallStatusText.Text = "等待管理员授权";
        RuntimeDetailText.Text = "WheelBridge 需要管理员权限来读取并拦截鼠标滚轮。请在 Windows 用户账户控制窗口中选择“是”。";
        ShowMessage("正在等待 WheelBridge 的管理员授权…", InfoBarSeverity.Informational);
        await RunWheelOperationOnUiThreadAsync(
            () => runtimeEnvironmentService.StartWheelBridge(RuntimePathTextBox.Text),
            "WheelBridge 已启动。拖动模型时可以使用滚轮调整冻结法向上的深度。" );
    }

    private async void StopWheel_Click(object sender, RoutedEventArgs e)
    {
        await RunWheelOperationOnUiThreadAsync(
            () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
            "WheelBridge 已停止。");
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
            ShowMessage(exception.Message, InfoBarSeverity.Error);
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
            ShowMessage(exception.Message, InfoBarSeverity.Error);
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
            string path = RuntimePathTextBox.Text;
            runtimeStatus = await Task.Run(() => runtimeEnvironmentService.Inspect(path));
            if (runtimeStatus.DragKeys is not null)
            {
                SelectDragKeys(runtimeStatus.DragKeys);
                SaveDragKeyPreference(runtimeStatus.DragKeys);
            }
            RenderRuntimeStatus(runtimeStatus);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            runtimeStatus = null;
            RuntimeOverallStatusText.Text = "无法检查运行环境";
            RuntimeDetailText.Text = exception.Message;
            RuntimeComponentStatusText.Text = "未知";
            ShaderFixStatusText.Text = "未知";
            WheelBridgeStatusText.Text = RuntimeEnvironmentService.IsWheelBridgeRunning() ? "正在运行" : "未运行";
            if (showErrors)
            {
                ShowMessage(exception.Message, InfoBarSeverity.Error);
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
            ? "运行环境已就绪"
            : status.ZzmiRootExists
                ? "运行环境需要安装或更新"
                : "未找到 ZZMI 目录";

        if (!status.PayloadAvailable)
        {
            RuntimeDetailText.Text = "应用目录缺少 RuntimePayload，无法执行安装。请重新构建或重新安装 JiggleForge。";
        }
        else if (!status.ZzmiRootExists)
        {
            RuntimeDetailText.Text = $"目录不存在：{status.ZzmiRoot}";
        }
        else if (!status.ModsDirectoryExists || !status.ShaderFixesDirectoryExists)
        {
            RuntimeDetailText.Text = "所选目录必须同时包含 Mods 和 ShaderFixes 文件夹。";
        }
        else if (status.Ready)
        {
            RuntimeDetailText.Text = status.BackupCount > 0
                ? $"所有组件均为当前版本；保留了 {status.BackupCount} 个安装前 ShaderFix 备份。"
                : "所有组件均为当前版本。";
        }
        else
        {
            RuntimeDetailText.Text = "点击“安装或更新运行环境”可补齐缺失文件并更新旧版本。";
        }

        RuntimeComponentStatusText.Text = !status.RuntimeInstalled
            ? "未安装"
            : status.RuntimeCurrent ? "当前版本" : "需要更新";
        ShaderFixStatusText.Text = $"{status.CurrentShaderCount}/{status.RequiredShaderCount} 当前版本";
        if (status.InstalledShaderCount != status.CurrentShaderCount)
        {
            ShaderFixStatusText.Text += $"（检测到 {status.InstalledShaderCount} 个）";
        }
        WheelBridgeStatusText.Text = status.WheelBridgeRunning ? "正在运行" : "未运行";

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

}
