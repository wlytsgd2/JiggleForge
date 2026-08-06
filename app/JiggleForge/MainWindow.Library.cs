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
        if (e.ClickedItem is not ModLibraryRow row)
        {
            return;
        }

        OpenProject(row.ModPath);
    }

    private async Task RefreshModLibraryAsync(bool showErrors = false)
    {
        modLibraryScanCancellation?.Cancel();
        modLibraryScanCancellation?.Dispose();
        modLibraryScanCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = modLibraryScanCancellation.Token;

        ZzmiPathResolution resolution = ZzmiPathResolver.Resolve(RuntimePathTextBox.Text);
        if (!resolution.IsValid)
        {
            modLibraryRows.Clear();
            ModLibraryStatusText.Text = L("SelectValidZzmiFirst");
            ModLibraryRootText.Text = resolution.Message;
            ModLibraryBusyRing.IsActive = false;
            if (showErrors)
            {
                ShowMessage(AppLanguageService.Format("CannotScanMods", resolution.Message), InfoBarSeverity.Warning);
            }
            return;
        }

        ApplyResolvedRuntimePath(resolution, showCorrectionMessage: showErrors);
        ModLibraryBusyRing.IsActive = true;
        ModLibraryStatusText.Text = L("ScanningMods");
        ModLibraryRootText.Text = Path.Combine(resolution.ResolvedPath, "Mods");
        try
        {
            IReadOnlyList<ModLibraryEntry> entries = await Task.Run(
                () => modLibraryService.ScanZzmiRoot(resolution.ResolvedPath, cancellationToken),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            modLibraryRows.Clear();
            foreach (ModLibraryEntry entry in entries)
            {
                modLibraryRows.Add(new ModLibraryRow(
                    entry.ModPath,
                    entry.DisplayName,
                    StateLabel(entry.State),
                    entry.DrawCount,
                    entry.Messages.FirstOrDefault() ?? string.Empty));
            }

            ModLibraryStatusText.Text = entries.Count == 0
                ? L("NoModsDetected")
                : AppLanguageService.Format("ModsDetected", entries.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            modLibraryRows.Clear();
            ModLibraryStatusText.Text = AppLanguageService.Format("ModScanFailed", exception.Message);
            if (showErrors)
            {
                ShowMessage(ModLibraryStatusText.Text, InfoBarSeverity.Error);
            }
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                ModLibraryBusyRing.IsActive = false;
            }
        }
    }

    private void OpenSelectedProject(string selectedPath)
    {
        ModFolderResolution resolution = modLibraryService.ResolveSelection(selectedPath);
        if (!resolution.IsValid || resolution.ResolvedPath is null)
        {
            string detail = resolution.Candidates.Count > 1
                ? AppLanguageService.Format("ZzmiCandidatesFound", resolution.Message, string.Join(AppLanguageService.CurrentLanguage == AppLanguageService.English ? ", " : "、", resolution.Candidates.Select(Path.GetFileName)))
                : resolution.Message;
            ShowMessage(detail, InfoBarSeverity.Warning);
            return;
        }

        if (resolution.WasCorrected)
        {
            ShowMessage(
                AppLanguageService.Format("AutoLocatedModRoot", resolution.ResolvedPath),
                InfoBarSeverity.Informational);
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
    public string DrawCountText => $"{DrawCount} Draw";
}
