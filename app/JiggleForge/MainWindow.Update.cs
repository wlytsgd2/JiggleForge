using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using JiggleForge.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private readonly ApplicationUpdateService applicationUpdateService = new(
        GetCurrentApplicationVersion(),
        AppContext.BaseDirectory);
    private ApplicationReleaseInfo? latestApplicationRelease;
    private bool applicationUpdateAvailable;
    private bool applicationUpdateBusy;

    private void InitializeApplicationUpdateView()
    {
        ApplicationVersionText.Text = $"当前版本：v{applicationUpdateService.CurrentVersionText}";
        ApplicationUpdateStatusText.Text = "启动后会自动检查 GitHub 最新稳定版。";
        RenderApplicationUpdateControls();
    }

    private async void CheckApplicationUpdate_Click(object sender, RoutedEventArgs e)
    {
        await CheckApplicationUpdatesAsync(promptIfAvailable: false, showResult: true);
    }

    private async void UpdateApplication_Click(object sender, RoutedEventArgs e)
    {
        if (latestApplicationRelease is null)
        {
            await CheckApplicationUpdatesAsync(promptIfAvailable: false, showResult: true);
        }

        if (latestApplicationRelease is null ||
            latestApplicationRelease.Version < applicationUpdateService.CurrentVersion)
        {
            return;
        }

        await DownloadAndLaunchApplicationUpdateAsync(latestApplicationRelease);
    }

    private async void VerifyApplication_Click(object sender, RoutedEventArgs e)
    {
        if (applicationUpdateBusy)
        {
            return;
        }

        SetApplicationUpdateBusy(true);
        ApplicationIntegrityStatusText.Text = "正在校验当前安装…";
        bool checkReleaseAfterVerification = false;
        try
        {
            ApplicationIntegrityResult result = await Task.Run(applicationUpdateService.VerifyInstallation);
            if (result.IsValid)
            {
                ApplicationIntegrityStatusText.Text =
                    $"校验通过：{result.VerifiedFileCount}/{result.ExpectedFileCount} 个程序和运行时文件完整。";
                ShowMessage("JiggleForge 当前安装校验通过。", InfoBarSeverity.Success);
                return;
            }

            string details = string.Join("；", result.Errors.Take(4));
            if (result.Errors.Count > 4)
            {
                details += $"；另有 {result.Errors.Count - 4} 项";
            }

            ApplicationIntegrityStatusText.Text = result.ManifestFound
                ? $"校验未通过：{result.VerifiedFileCount}/{result.ExpectedFileCount} 个文件完整。{details}"
                : result.Errors.FirstOrDefault() ?? "当前版本无法校验。";
            ShowMessage("当前安装存在缺失或被修改的文件，可以更新或重新安装最新版本。", InfoBarSeverity.Warning);
            if (latestApplicationRelease is null)
            {
                checkReleaseAfterVerification = true;
            }
            else
            {
                RenderApplicationUpdateControls();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ApplicationIntegrityStatusText.Text = "校验失败：" + exception.Message;
            ShowMessage(ApplicationIntegrityStatusText.Text, InfoBarSeverity.Error);
        }
        finally
        {
            SetApplicationUpdateBusy(false);
        }

        if (checkReleaseAfterVerification)
        {
            await CheckApplicationUpdatesAsync(promptIfAvailable: false, showResult: false);
        }
    }

    private void UpdateTitleButton_Click(object sender, RoutedEventArgs e)
    {
        Navigation.SelectedItem = RuntimeNavItem;
        ShowView("runtime");
        RuntimeView.ChangeView(null, 0, null, disableAnimation: false);
    }

    private async Task CheckApplicationUpdatesAsync(bool promptIfAvailable, bool showResult)
    {
        if (applicationUpdateBusy)
        {
            return;
        }

        SetApplicationUpdateBusy(true);
        ApplicationUpdateStatusText.Text = "正在检查 GitHub 最新版本…";
        ApplicationUpdateCheckResult? result = null;
        try
        {
            result = await applicationUpdateService.CheckForUpdatesAsync();
            latestApplicationRelease = result.LatestRelease;
            applicationUpdateAvailable = result.UpdateAvailable;
            if (result.UpdateAvailable)
            {
                ApplicationUpdateStatusText.Text =
                    $"发现新版本 v{result.LatestRelease.VersionText}：{result.LatestRelease.Name}";
                UpdateTitleButton.Content = $"发现新版本 v{result.LatestRelease.VersionText}";
                UpdateTitleButton.Visibility = Visibility.Visible;
                if (showResult)
                {
                    ShowMessage(
                        $"发现 JiggleForge v{result.LatestRelease.VersionText}，可以一键更新。",
                        InfoBarSeverity.Informational);
                }
            }
            else if (result.LatestRelease.Version == result.CurrentVersion)
            {
                ApplicationUpdateStatusText.Text = $"当前已是最新版本 v{result.CurrentVersionText}。";
                UpdateTitleButton.Visibility = Visibility.Collapsed;
                if (showResult)
                {
                    ShowMessage("JiggleForge 当前已是最新版本。", InfoBarSeverity.Success);
                }
            }
            else
            {
                ApplicationUpdateStatusText.Text =
                    $"当前版本 v{result.CurrentVersionText} 高于最新公开版 v{result.LatestRelease.VersionText}。";
                UpdateTitleButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          IOException or InvalidDataException or JsonException)
        {
            ApplicationUpdateStatusText.Text = "暂时无法连接 GitHub 检查更新。";
            if (showResult)
            {
                ShowMessage("检查更新失败：" + exception.Message, InfoBarSeverity.Warning);
            }
        }
        finally
        {
            SetApplicationUpdateBusy(false);
        }

        if (promptIfAvailable && result?.UpdateAvailable == true)
        {
            await ShowApplicationUpdateDialogAsync(result.LatestRelease);
        }
    }

    private async Task ShowApplicationUpdateDialogAsync(ApplicationReleaseInfo release)
    {
        string notes = string.IsNullOrWhiteSpace(release.ReleaseNotes)
            ? "此版本没有附加更新说明。"
            : release.ReleaseNotes.Trim();
        if (notes.Length > 1600)
        {
            notes = notes[..1600] + "\n…";
        }

        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"当前版本：v{applicationUpdateService.CurrentVersionText}\n最新版本：v{release.VersionText}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(new TextBlock
        {
            Text = "更新内容",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 280,
            Content = new TextBlock
            {
                Text = notes,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            },
        });
        content.Children.Add(new TextBlock
        {
            Text = "QQ 交流群：451901293\n无论有任何问题，任何建议，还是想不落下更新，或者单纯喜欢水群，欢迎加入！",
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        ContentDialog dialog = new()
        {
            Title = "发现 JiggleForge 新版本",
            Content = content,
            PrimaryButtonText = "立即更新",
            CloseButtonText = "暂不更新",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = RootGrid.XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await DownloadAndLaunchApplicationUpdateAsync(release);
        }
    }

    private async Task DownloadAndLaunchApplicationUpdateAsync(ApplicationReleaseInfo release)
    {
        if (applicationUpdateBusy)
        {
            return;
        }

        SetApplicationUpdateBusy(true);
        ApplicationUpdateProgressBar.Value = 0;
        ApplicationUpdateProgressBar.Visibility = Visibility.Visible;
        ApplicationUpdateProgressBar.IsIndeterminate = false;
        ApplicationUpdateStatusText.Text = $"正在下载 JiggleForge v{release.VersionText}…";
        try
        {
            Progress<double> progress = new(value =>
            {
                ApplicationUpdateProgressBar.Value = value * 100;
                ApplicationUpdateStatusText.Text =
                    $"正在下载 JiggleForge v{release.VersionText}：{value:P0}";
            });
            ApplicationUpdateDownload download = await applicationUpdateService.DownloadUpdateAsync(
                release,
                progress);
            ApplicationUpdateStatusText.Text = "下载完成且 SHA-256 校验通过，正在启动更新器…";
            LaunchApplicationUpdater(download);
            Close();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          IOException or UnauthorizedAccessException or InvalidDataException or
                                          InvalidOperationException or Win32Exception)
        {
            ApplicationUpdateStatusText.Text = "更新失败：" + exception.Message;
            ApplicationUpdateProgressBar.Visibility = Visibility.Collapsed;
            ShowMessage(ApplicationUpdateStatusText.Text, InfoBarSeverity.Error);
        }
        finally
        {
            SetApplicationUpdateBusy(false);
        }
    }

    private void LaunchApplicationUpdater(ApplicationUpdateDownload download)
    {
        string sourceUpdater = Path.Combine(AppContext.BaseDirectory, "JiggleForge.Updater.exe");
        if (!File.Exists(sourceUpdater))
        {
            throw new InvalidOperationException("应用目录缺少 JiggleForge.Updater.exe，请手动安装包含更新器的新版本。");
        }

        string updaterDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge",
            "Updater",
            download.Release.VersionText);
        Directory.CreateDirectory(updaterDirectory);
        string updaterPath = Path.Combine(updaterDirectory, "JiggleForge.Updater.exe");
        File.Copy(sourceUpdater, updaterPath, overwrite: true);

        string targetDirectory = Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        string executableName = Path.GetFileName(Environment.ProcessPath) ?? "JiggleForge.exe";
        string arguments = string.Join(' ',
            "--parent", Environment.ProcessId.ToString(),
            "--package", QuoteArgument(download.PackagePath),
            "--target", QuoteArgument(targetDirectory),
            "--executable", QuoteArgument(executableName),
            "--sha256", download.Sha256);
        ProcessStartInfo startInfo = new(updaterPath, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = updaterDirectory,
        };
        if (!CanWriteInstallationDirectory(targetDirectory))
        {
            startInfo.Verb = "runas";
        }

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 JiggleForge 更新器。");
    }

    private void SetApplicationUpdateBusy(bool busy)
    {
        applicationUpdateBusy = busy;
        ApplicationUpdateBusyRing.IsActive = busy;
        ApplicationUpdateProgressBar.IsIndeterminate = busy && ApplicationUpdateProgressBar.Visibility != Visibility.Visible;
        RenderApplicationUpdateControls();
    }

    private void RenderApplicationUpdateControls()
    {
        CheckApplicationUpdateButton.IsEnabled = !applicationUpdateBusy;
        VerifyApplicationButton.IsEnabled = !applicationUpdateBusy;
        UpdateApplicationButton.IsEnabled = !applicationUpdateBusy &&
            latestApplicationRelease is not null &&
            latestApplicationRelease.Version >= applicationUpdateService.CurrentVersion;
        UpdateApplicationButton.Content = latestApplicationRelease is null || applicationUpdateAvailable
            ? "更新到最新版本"
            : "重新安装最新版本";
    }

    private static bool CanWriteInstallationDirectory(string directory)
    {
        string probePath = Path.Combine(directory, $".jiggleforge-write-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException("路径包含不受支持的双引号字符。", nameof(value));
        }

        return '"' + value + '"';
    }

    private static string GetCurrentApplicationVersion()
    {
        Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
    }
}
