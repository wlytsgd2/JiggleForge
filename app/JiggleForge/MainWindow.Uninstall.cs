using System.ComponentModel;
using System.Diagnostics;
using JiggleForge.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private enum ApplicationUninstallMode
    {
        Cancel,
        KeepCompatibility,
        RestoreMods,
    }

    private sealed class ApplicationUninstallException(string message, Exception? innerException = null)
        : Exception(message, innerException);

    private async void UninstallApplication_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetValidatedRuntimePath(out string runtimePath))
        {
            return;
        }

        ApplicationUninstallMode mode = await ShowApplicationUninstallDialogAsync();
        if (mode == ApplicationUninstallMode.Cancel)
        {
            return;
        }

        UninstallApplicationButton.IsEnabled = false;
        ShowMessage(L("ApplicationUninstallPreparing"), InfoBarSeverity.Informational);
        try
        {
            ValidateSelfUninstallPrerequisites();

            if (RuntimeEnvironmentService.IsWheelBridgeRunning())
            {
                bool stopped = await RunWheelOperationOnUiThreadAsync(
                    () => runtimeEnvironmentService.StopWheelBridge(requestElevation: true),
                    L("WheelStoppedForUninstall"));
                if (!stopped)
                {
                    return;
                }
            }

            if (mode == ApplicationUninstallMode.RestoreMods)
            {
                IReadOnlyList<string> projects = await Task.Run(() => FindProjectsForFullUninstall(runtimePath));
                ShowMessage(
                    AppLanguageService.Format("ApplicationUninstallRestoringMods", projects.Count),
                    InfoBarSeverity.Informational);
                await Task.Run(() => RestoreProjectsForFullUninstall(projects));
                projectHistoryService.Replace([]);
                await Task.Run(() => runtimeEnvironmentService.Uninstall(runtimePath, stopWheelBridge: false));
            }
            else
            {
                await Task.Run(() =>
                    runtimeEnvironmentService.UninstallKeepingCompatibility(runtimePath, stopWheelBridge: false));
            }

            LaunchApplicationUninstaller();
            Close();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          InvalidDataException or InvalidOperationException or
                                          ArgumentException or Win32Exception or ApplicationUninstallException)
        {
            string reason = exception is ApplicationUninstallException
                ? exception.Message
                : AppLanguageService.LocalizeException(exception);
            ShowMessage(
                AppLanguageService.Format(
                    "ApplicationUninstallFailed",
                    reason),
                InfoBarSeverity.Error);
            await RefreshRuntimeStatusAsync(showErrors: false);
        }
        finally
        {
            UninstallApplicationButton.IsEnabled = true;
        }
    }

    private async Task<ApplicationUninstallMode> ShowApplicationUninstallDialogAsync()
    {
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = L("ApplicationUninstallDialogIntro"),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(BuildUninstallChoiceDescription(
            L("ApplicationUninstallKeepTitle"),
            L("ApplicationUninstallKeepDescription")));
        content.Children.Add(BuildUninstallChoiceDescription(
            L("ApplicationUninstallFullTitle"),
            L("ApplicationUninstallFullDescription")));

        ContentDialog dialog = new()
        {
            Title = L("ApplicationUninstallDialogTitle"),
            Content = content,
            PrimaryButtonText = L("ApplicationUninstallKeepAction"),
            SecondaryButtonText = L("ApplicationUninstallFullAction"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.None,
            XamlRoot = RootGrid.XamlRoot,
        };

        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => ApplicationUninstallMode.KeepCompatibility,
            ContentDialogResult.Secondary => ApplicationUninstallMode.RestoreMods,
            _ => ApplicationUninstallMode.Cancel,
        };
    }

    private static Border BuildUninstallChoiceDescription(string title, string description)
    {
        StackPanel text = new() { Spacing = 4 };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["LayerFillColorAltBrush"],
            Child = text,
        };
    }

    private IReadOnlyList<string> FindProjectsForFullUninstall(string zzmiRoot)
    {
        List<string> candidates = projectHistoryService.Load().ToList();
        candidates.AddRange(modLibraryService.FindAdaptedProjectRoots(zzmiRoot));

        List<string> projects = [];
        foreach (string path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            ModProjectInspection inspection = projectService.Inspect(path);
            if (inspection.State == ModImportState.FirstImport)
            {
                continue;
            }

            ModBackupInspection backup = backupService.Inspect(path);
            if (!backup.Exists || !backup.IsValid)
            {
                throw new ApplicationUninstallException(
                    AppLanguageService.Format("ApplicationUninstallBackupInvalid", path));
            }

            projects.Add(inspection.ModPath);
        }

        return projects;
    }

    private void RestoreProjectsForFullUninstall(IReadOnlyList<string> projects)
    {
        List<string> failures = [];
        foreach (string path in projects)
        {
            try
            {
                backupService.Restore(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                              InvalidDataException or ArgumentException)
            {
                failures.Add(AppLanguageService.Format(
                    "ApplicationUninstallRestoreFailureItem",
                    path,
                    AppLanguageService.LocalizeException(exception)));
            }
        }

        if (failures.Count > 0)
        {
            throw new ApplicationUninstallException(
                AppLanguageService.Format(
                    "ApplicationUninstallRestoreFailures",
                    string.Join(Environment.NewLine, failures)));
        }
    }

    private static void ValidateSelfUninstallPrerequisites()
    {
        string updaterPath = Path.Combine(AppContext.BaseDirectory, "JiggleForge.Updater.exe");
        string manifestPath = Path.Combine(AppContext.BaseDirectory, ApplicationUpdateService.IntegrityManifestFileName);
        if (!File.Exists(updaterPath))
        {
            throw new ApplicationUninstallException(L("UpdaterMissing"));
        }
        if (!File.Exists(manifestPath))
        {
            throw new ApplicationUninstallException(L("ApplicationUninstallManifestMissing"));
        }
    }

    private static void LaunchApplicationUninstaller()
    {
        string sourceUpdater = Path.Combine(AppContext.BaseDirectory, "JiggleForge.Updater.exe");
        string uninstallerDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JiggleForge",
            "Uninstaller");
        Directory.CreateDirectory(uninstallerDirectory);
        string uninstallerPath = Path.Combine(uninstallerDirectory, "JiggleForge.Updater.exe");
        File.Copy(sourceUpdater, uninstallerPath, overwrite: true);

        string targetDirectory = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string arguments = string.Join(' ',
            "--operation", "uninstall",
            "--parent", Environment.ProcessId.ToString(),
            "--target", QuoteArgument(targetDirectory),
            "--language", AppLanguageService.CurrentLanguage);
        ProcessStartInfo startInfo = new(uninstallerPath, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = uninstallerDirectory,
        };
        if (!CanWriteInstallationDirectory(targetDirectory))
        {
            startInfo.Verb = "runas";
        }

        _ = Process.Start(startInfo) ?? throw new ApplicationUninstallException(L("ApplicationUninstallCannotStart"));
    }
}
