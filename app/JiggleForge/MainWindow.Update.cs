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
        ApplicationVersionText.Text = AppLanguageService.Format("ApplicationVersion", applicationUpdateService.CurrentVersionText);
        ApplicationUpdateStatusText.Text = L("AutoCheckStableRelease");
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
        ApplicationIntegrityStatusText.Text = L("VerifyingInstallation");
        bool checkReleaseAfterVerification = false;
        try
        {
            ApplicationIntegrityResult result = await Task.Run(applicationUpdateService.VerifyInstallation);
            if (result.IsValid)
            {
                ApplicationIntegrityStatusText.Text =
                    AppLanguageService.Format("VerificationPassedDetails", result.VerifiedFileCount, result.ExpectedFileCount);
                ShowMessage(L("VerificationPassed"), InfoBarSeverity.Success);
                return;
            }

            string details = string.Join(
                AppLanguageService.Get("ListSeparator"),
                result.Errors.Take(4).Select(AppLanguageService.Localize));
            if (result.Errors.Count > 4)
            {
                details += AppLanguageService.Format("AdditionalIssues", result.Errors.Count - 4);
            }

            ApplicationIntegrityStatusText.Text = result.ManifestFound
                ? AppLanguageService.Format("VerificationFailedDetails", result.VerifiedFileCount, result.ExpectedFileCount, details)
                : result.Errors.FirstOrDefault() is { } firstError
                    ? AppLanguageService.Localize(firstError)
                    : L("CannotVerifyVersion");
            ShowMessage(L("InstallationFilesChanged"), InfoBarSeverity.Warning);
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
            ApplicationIntegrityStatusText.Text = AppLanguageService.Format(
                "VerificationError",
                AppLanguageService.LocalizeException(exception));
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
        ApplicationUpdateStatusText.Text = L("CheckingGithubRelease");
        ApplicationUpdateCheckResult? result = null;
        try
        {
            result = await applicationUpdateService.CheckForUpdatesAsync();
            latestApplicationRelease = result.LatestRelease;
            applicationUpdateAvailable = result.UpdateAvailable;
            if (result.UpdateAvailable)
            {
                ApplicationUpdateStatusText.Text =
                    AppLanguageService.Format("NewVersionFoundWithName", result.LatestRelease.VersionText, result.LatestRelease.Name);
                UpdateTitleButton.Content = AppLanguageService.Format("NewVersionFound", result.LatestRelease.VersionText);
                UpdateTitleButton.Visibility = Visibility.Visible;
                if (showResult)
                {
                    ShowMessage(
                        AppLanguageService.Format("OneClickUpdateAvailable", result.LatestRelease.VersionText),
                        InfoBarSeverity.Informational);
                }
            }
            else if (result.LatestRelease.Version == result.CurrentVersion)
            {
                ApplicationUpdateStatusText.Text = AppLanguageService.Format("AlreadyLatestVersion", result.CurrentVersionText);
                UpdateTitleButton.Visibility = Visibility.Collapsed;
                if (showResult)
                {
                    ShowMessage(L("JiggleForgeAlreadyLatest"), InfoBarSeverity.Success);
                }
            }
            else
            {
                ApplicationUpdateStatusText.Text =
                    AppLanguageService.Format("VersionAheadOfRelease", result.CurrentVersionText, result.LatestRelease.VersionText);
                UpdateTitleButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          IOException or InvalidDataException or JsonException)
        {
            ApplicationUpdateStatusText.Text = L("CannotConnectGithub");
            if (showResult)
            {
                ShowMessage(
                    AppLanguageService.Format("UpdateCheckFailed", AppLanguageService.LocalizeException(exception)),
                    InfoBarSeverity.Warning);
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
            ? L("NoReleaseNotes")
            : release.ReleaseNotes.Trim();
        if (notes.Length > 1600)
        {
            notes = notes[..1600] + "\n…";
        }

        StackPanel content = new() { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = AppLanguageService.Format("UpdateVersionComparison", applicationUpdateService.CurrentVersionText, release.VersionText),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(new TextBlock
        {
            Text = L("WhatsNew"),
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
            Text = L("UpdateCommunityText"),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        ContentDialog dialog = new()
        {
            Title = L("NewVersionDialogTitle"),
            Content = content,
            PrimaryButtonText = L("UpdateNow"),
            CloseButtonText = L("NotNow"),
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
        ApplicationUpdateStatusText.Text = AppLanguageService.Format("DownloadingVersion", release.VersionText);
        try
        {
            Progress<double> progress = new(value =>
            {
                ApplicationUpdateProgressBar.Value = value * 100;
                ApplicationUpdateStatusText.Text =
                    AppLanguageService.Format("DownloadingVersionProgress", release.VersionText, value);
            });
            ApplicationUpdateDownload download = await applicationUpdateService.DownloadUpdateAsync(
                release,
                progress);
            ApplicationUpdateStatusText.Text = L("DownloadVerifiedStartingUpdater");
            LaunchApplicationUpdater(download);
            Close();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or
                                          IOException or UnauthorizedAccessException or InvalidDataException or
                                          InvalidOperationException or Win32Exception)
        {
            ApplicationUpdateStatusText.Text = AppLanguageService.Format(
                "UpdateFailed",
                AppLanguageService.LocalizeException(exception));
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
            throw new InvalidOperationException(L("UpdaterMissing"));
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

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException(L("CannotStartUpdater"));
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
            ? L("UpdateToLatest")
            : L("ReinstallLatest");
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
            throw new ArgumentException(L("UnsupportedQuoteInPath"), nameof(value));
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
