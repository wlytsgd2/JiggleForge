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
    private void Root_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Link
            : DataPackageOperation.None;
        e.DragUIOverride.Caption = L("OpenModFolder");
        e.DragUIOverride.IsCaptionVisible = true;
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
        StorageFolder? folder = items.OfType<StorageFolder>().FirstOrDefault();
        if (folder is null)
        {
            ShowMessage(L("DropFolderNotFile"), InfoBarSeverity.Warning);
            return;
        }

        OpenSelectedProject(folder.Path);
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        FolderPicker picker = new();
        picker.FileTypeFilter.Add("*");
        nint windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            OpenSelectedProject(folder.Path);
        }
    }

    private void OpenProject(string path)
    {
        // Opening a project changes the navigation targets used by the tour.
        // End an active tour first so it cannot retain stale controls.
        if (onboardingTourActive)
        {
            EndOnboardingTour();
        }

        currentInspection = projectService.Inspect(path);
        currentConfiguration = currentInspection.Configuration;
        UpdateProjectHistory(currentInspection);
        ProjectCard.Visibility = Visibility.Visible;
        ProjectNameText.Text = Path.GetFileName(currentInspection.ModPath);
        ProjectPathText.Text = currentInspection.ModPath;
        StateText.Text = StateLabel(currentInspection.State);
        int drawCount = currentConfiguration?.Draws.Count ?? currentInspection.DiscoveredDraws.Count;
        DrawCountText.Text = drawCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SchemaText.Text = currentConfiguration?.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
        DetailsText.Text = string.Join(
            Environment.NewLine,
            currentInspection.Messages.Select(AppLanguageService.Localize));
        UpdateBackupStatus();
        CreateConfigButton.Visibility = currentInspection.State == ModImportState.FirstImport
            ? Visibility.Visible
            : Visibility.Collapsed;
        RepairRuntimeButton.Visibility = currentInspection.State == ModImportState.RuntimeRepairRequired
            ? Visibility.Visible
            : Visibility.Collapsed;
        ModBackupInspection backup = backupService.Inspect(currentInspection.ModPath);
        RestoreOriginalButton.Visibility = backup.IsValid && currentInspection.State != ModImportState.FirstImport
            ? Visibility.Visible
            : Visibility.Collapsed;

        bool hasEditor = currentConfiguration is not null;
        SetEditorEnabled(hasEditor);
        if (hasEditor)
        {
            LoadEditor(currentConfiguration!);
            Navigation.SelectedItem = DrawNavItem;
            ShowView("draws");
        }
        else
        {
            Navigation.SelectedItem = OverviewNavItem;
            ShowView("overview");
        }

        InfoBarSeverity severity = currentInspection.State switch
        {
            ModImportState.Ready => InfoBarSeverity.Success,
            ModImportState.Invalid => InfoBarSeverity.Error,
            ModImportState.FirstImport => InfoBarSeverity.Informational,
            _ => InfoBarSeverity.Warning,
        };
        ShowMessage(
            currentInspection.Messages.Count == 0
                ? StateLabel(currentInspection.State)
                : AppLanguageService.Localize(currentInspection.Messages[0]),
            severity);
    }

    private void LoadEditor(JiggleProjectConfig config)
    {
        drawRows.Clear();
        maskRows.Clear();
        editorGroupNames.Clear();
        editorGroupNames.Add(OriginalPartsConfig.GroupName);
        foreach (JiggleGroupConfig group in config.Groups)
        {
            editorGroupNames.Add(group.Name);
        }
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            DrawEditorRow row = new()
            {
                Id = draw.Id,
                Alias = draw.Alias,
                DeformationEnabled = draw.DeformationEnabled,
                Group = draw.Group,
                Mask = draw.Mask,
                Source = $"{draw.SourceFile}:{draw.SourceLine}  [{draw.SourceSection}]",
                Branch = draw.Branch,
            };
            drawRows.Add(row);
            maskRows.Add(row);
            if (!string.IsNullOrWhiteSpace(row.Group))
            {
                editorGroupNames.Add(row.Group.Trim());
            }
        }
        RefreshDrawTree();

        InitializeProjectPhysicsEditor(config);
        InspectorToggleButton.IsChecked = config.Inspector.Enabled;
        UpdateInspectorButtonText();
        graphNodePositions.Clear();
        foreach (JiggleGroupConfig group in config.Groups)
        {
            if (group.GraphX.HasValue && group.GraphY.HasValue)
            {
                graphNodePositions[group.Name] = new Point(group.GraphX.Value, group.GraphY.Value);
            }
        }

        RefreshGraphGroups(clearInvalidEdges: true);
        edgeRows.Clear();
        foreach (JiggleEdgeConfig edge in config.Edges)
        {
            AddEdgeRow(edge.From, edge.To);
        }
    }

    private void SetEditorEnabled(bool enabled)
    {
        DrawNavItem.IsEnabled = enabled;
        GraphNavItem.IsEnabled = enabled;
        MaskNavItem.IsEnabled = enabled;
        PhysicsNavItem.IsEnabled = enabled;
        InspectorToggleButton.IsEnabled = enabled;
    }

    private async void CreateConfig_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection?.State != ModImportState.FirstImport)
        {
            return;
        }

        try
        {
            JiggleProjectConfig config = projectService.CreateInitialConfiguration(
                currentInspection,
                defaultPhysics);
            _ = backupService.EnsureOriginalBackup(
                currentInspection.ModPath,
                config);
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, config);
            OpenProject(currentInspection.ModPath);
            await RefreshModLibraryAsync();
            ShowMessage(AppLanguageService.Format("AdaptedDraws", result.DrawCount), InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
        }
    }

    private async void RestoreOriginal_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection is null)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            Title = L("RestoreOriginalTitle"),
            Content = L("RestoreOriginalDescription"),
            PrimaryButtonText = L("RestoreOriginalAction"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            string path = currentInspection.ModPath;
            backupService.Restore(path);
            OpenProject(path);
            await RefreshModLibraryAsync();
            ShowMessage(L("OriginalModRestored"), InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(
                AppLanguageService.Format("RestoreFailed", AppLanguageService.LocalizeException(exception)),
                InfoBarSeverity.Error);
        }
    }

    private void UpdateBackupStatus()
    {
        if (currentInspection is null)
        {
            BackupStatusText.Text = string.Empty;
            return;
        }

        ModBackupInspection backup = backupService.Inspect(currentInspection.ModPath);
        if (backup.IsValid)
        {
            BackupStatusText.Text = AppLanguageService.Format("BackupSaved", backup.Files.Count, ModBackupService.BackupFileName);
            BackupStatusText.Foreground = new SolidColorBrush(Colors.DarkGreen);
        }
        else if (backup.Exists)
        {
            BackupStatusText.Text = AppLanguageService.Format(
                "BackupInvalid",
                backup.Error is null ? string.Empty : AppLanguageService.Localize(backup.Error));
            BackupStatusText.Foreground = new SolidColorBrush(Colors.DarkRed);
        }
        else
        {
            BackupStatusText.Text = currentInspection.State == ModImportState.FirstImport
                ? L("BackupCreatedOnAdapt")
                : L("BackupNotFound");
            BackupStatusText.Foreground = new SolidColorBrush(Colors.Gray);
        }
    }

    private async void RepairRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection?.Configuration is null)
        {
            return;
        }

        try
        {
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, currentInspection.Configuration);
            OpenProject(currentInspection.ModPath);
            await RefreshModLibraryAsync();
            ShowMessage(AppLanguageService.Format("RuntimeFilesRepaired", result.DrawCount), InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
        }
    }

    private async void ApplyConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection is null || currentConfiguration is null)
        {
            return;
        }

        try
        {
            UpdateConfigurationFromEditor(currentConfiguration);
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, currentConfiguration);
            string path = currentInspection.ModPath;
            OpenProject(path);
            await RefreshModLibraryAsync();
            ShowMessage(AppLanguageService.Format("ConfigurationApplied", result.DrawCount), InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
        }
    }

    private async void InspectorToggle_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection is null || currentConfiguration is null)
        {
            return;
        }

        bool previous = currentConfiguration.Inspector.Enabled;
        try
        {
            UpdateConfigurationFromEditor(currentConfiguration);
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, currentConfiguration);
            string path = currentInspection.ModPath;
            bool enabled = currentConfiguration.Inspector.Enabled;
            OpenProject(path);
            await RefreshModLibraryAsync();
            ShowMessage(
                enabled
                    ? AppLanguageService.Format("InspectorEnabled", result.DrawCount)
                    : L("InspectorDisabled"),
                InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            currentConfiguration.Inspector.Enabled = previous;
            InspectorToggleButton.IsChecked = previous;
            UpdateInspectorButtonText();
            ShowMessage(AppLanguageService.LocalizeException(exception), InfoBarSeverity.Error);
        }
    }

    private void UpdateConfigurationFromEditor(JiggleProjectConfig config)
    {
        Dictionary<string, DrawEditorRow> rows = drawRows.ToDictionary(row => row.Id, StringComparer.OrdinalIgnoreCase);
        foreach (JiggleDrawConfig draw in config.Draws)
        {
            DrawEditorRow row = rows[draw.Id];
            draw.Alias = row.Alias.Trim();
            draw.DeformationEnabled = row.DeformationEnabled;
            draw.Group = row.Group.Trim();
            draw.Mask = row.Mask.Trim();
        }
        CommitCurrentPhysicsScope();
        config.OriginalParts.LegacyGroup = string.Empty;
        config.Physics = GetDefaultProjectPhysics().Clone();

        RefreshGraphGroups(clearInvalidEdges: true);
        config.Groups.Clear();
        foreach (string groupName in editorGroupNames
                     .OrderBy(name =>
                         string.Equals(name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)
                             ? 0
                             : 1)
                     .ThenBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            JiggleGroupConfig group = new()
            {
                Name = groupName,
                Physics = editorPhysicsByScope.TryGetValue(
                    groupName,
                    out PhysicsSettings? groupPhysics)
                    ? groupPhysics.Clone()
                    : config.Physics.Clone(),
            };
            group.Draws.AddRange(drawRows
                .Where(row => string.Equals(row.Group, groupName, StringComparison.OrdinalIgnoreCase))
                .Select(row => row.Id));
            if (graphNodePositions.TryGetValue(group.Name, out Point position))
            {
                group.GraphX = position.X;
                group.GraphY = position.Y;
            }
            config.Groups.Add(group);
        }

        config.Edges.Clear();
        HashSet<string> edgeKeys = new(StringComparer.OrdinalIgnoreCase);
        foreach (EdgeEditorRow row in edgeRows)
        {
            string from = row.From.Trim();
            string to = row.To.Trim();
            if (from.Length == 0 || to.Length == 0)
            {
                throw new InvalidDataException(L("EdgeGroupsRequired"));
            }

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(AppLanguageService.Format("SelfEdgeInvalid", from, to));
            }

            if (!edgeKeys.Add(from + "\0" + to))
            {
                throw new InvalidDataException(AppLanguageService.Format("DuplicateEdge", from, to));
            }

            config.Edges.Add(new JiggleEdgeConfig { From = from, To = to });
        }

        config.Inspector.Enabled = InspectorToggleButton.IsChecked == true;
    }

    private void UpdateInspectorButtonText()
    {
        InspectorToggleButton.Content = InspectorToggleButton.IsChecked == true
            ? L("InspectorOn")
            : L("InspectorOff");
    }

}
