using JiggleForge.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JiggleForge;

public sealed partial class MainWindow : Window
{
    private async void RefreshModLibrary_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModLibraryAsync(showErrors: true);
    }

    private void ModLibraryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModLibraryRow row)
        {
            OpenProject(row.ModPath);
        }
    }

    private Task RefreshModLibraryAsync(bool showErrors = false)
    {
        ModLibraryBusyRing.IsActive = true;
        ModLibraryStatusText.Text = L("LoadingModHistory");
        ModLibraryRootText.Text = L("ModHistoryDescription");
        try
        {
            modLibraryRows.Clear();
            List<string> candidatePaths = projectHistoryService.Load().ToList();
            string? discoveryWarning = null;
            ZzmiPathResolution zzmi = ZzmiPathResolver.Resolve(RuntimePathTextBox.Text);
            if (zzmi.IsValid && zzmi.ResolvedPath is not null)
            {
                try
                {
                    candidatePaths.AddRange(modLibraryService.FindAdaptedProjectRoots(zzmi.ResolvedPath));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    discoveryWarning = AppLanguageService.LocalizeException(exception);
                }
            }

            List<string> retainedPaths = [];
            foreach (string path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
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

                retainedPaths.Add(inspection.ModPath);
                AddOrReplaceHistoryRow(inspection, mostRecent: false);
            }
            projectHistoryService.Replace(retainedPaths);

            ModLibraryStatusText.Text = modLibraryRows.Count == 0
                ? L("NoAdaptedModsRecorded")
                : AppLanguageService.Format("AdaptedModsRecorded", modLibraryRows.Count);
            if (showErrors && discoveryWarning is not null)
            {
                ShowMessage(
                    AppLanguageService.Format("AdaptedModSearchFailed", discoveryWarning),
                    InfoBarSeverity.Warning);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            modLibraryRows.Clear();
            ModLibraryStatusText.Text = AppLanguageService.Format(
                "ModHistoryLoadFailed",
                AppLanguageService.LocalizeException(exception));
            if (showErrors)
            {
                ShowMessage(ModLibraryStatusText.Text, InfoBarSeverity.Error);
            }
        }
        finally
        {
            ModLibraryBusyRing.IsActive = false;
        }

        return Task.CompletedTask;
    }

    private void UpdateProjectHistory(ModProjectInspection inspection)
    {
        if (inspection.State == ModImportState.FirstImport)
        {
            projectHistoryService.Remove(inspection.ModPath);
            RemoveHistoryRow(inspection.ModPath);
            return;
        }

        ModBackupInspection backup = backupService.Inspect(inspection.ModPath);
        if (inspection.State == ModImportState.Invalid && !backup.Exists)
        {
            return;
        }

        projectHistoryService.Add(inspection.ModPath);
        AddOrReplaceHistoryRow(inspection);
    }

    private void AddOrReplaceHistoryRow(ModProjectInspection inspection, bool mostRecent = true)
    {
        RemoveHistoryRow(inspection.ModPath);
        int drawCount = inspection.Configuration?.Draws.Count ?? inspection.DiscoveredDraws.Count;
        ModLibraryRow row = new(
            inspection.ModPath,
            Path.GetFileName(inspection.ModPath),
            StateLabel(inspection.State),
            drawCount,
            inspection.Messages.Count == 0
                ? string.Empty
                : AppLanguageService.Localize(inspection.Messages[0]));
        if (mostRecent)
        {
            modLibraryRows.Insert(0, row);
        }
        else
        {
            modLibraryRows.Add(row);
        }
    }

    private void RemoveHistoryRow(string path)
    {
        ModLibraryRow? existing = modLibraryRows.FirstOrDefault(row =>
            string.Equals(row.ModPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            modLibraryRows.Remove(existing);
        }
    }

    private void OpenSelectedProject(string selectedPath)
    {
        ModFolderResolution resolution = modLibraryService.ResolveSelection(selectedPath);
        if (!resolution.IsValid || resolution.ResolvedPath is null)
        {
            ShowMessage(AppLanguageService.Localize(resolution.Message), InfoBarSeverity.Warning);
            return;
        }

        OpenProject(resolution.ResolvedPath);
    }
}

public sealed record ModLibraryRow(
    string ModPath,
    string DisplayName,
    string StateText,
    int DrawCount,
    string Detail)
{
    public string DrawCountText => AppLanguageService.Format("DrawCountShort", DrawCount);
}
