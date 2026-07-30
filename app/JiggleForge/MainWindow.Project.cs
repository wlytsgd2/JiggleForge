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
        e.DragUIOverride.Caption = "打开 Mod 文件夹";
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
            ShowMessage("请拖入文件夹，而不是单个文件。", InfoBarSeverity.Warning);
            return;
        }

        OpenProject(folder.Path);
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
            OpenProject(folder.Path);
        }
    }

    private void OpenProject(string path)
    {
        currentInspection = projectService.Inspect(path);
        currentConfiguration = currentInspection.Configuration;
        ProjectCard.Visibility = Visibility.Visible;
        ProjectNameText.Text = Path.GetFileName(currentInspection.ModPath);
        ProjectPathText.Text = currentInspection.ModPath;
        StateText.Text = StateLabel(currentInspection.State);
        int drawCount = currentConfiguration?.Draws.Count ?? currentInspection.DiscoveredDraws.Count;
        DrawCountText.Text = drawCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SchemaText.Text = currentConfiguration?.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";
        DetailsText.Text = string.Join(Environment.NewLine, currentInspection.Messages);
        CreateConfigButton.Visibility = currentInspection.State == ModImportState.FirstImport
            ? Visibility.Visible
            : Visibility.Collapsed;
        RepairRuntimeButton.Visibility = currentInspection.State == ModImportState.RuntimeRepairRequired
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
        ShowMessage(currentInspection.Messages.FirstOrDefault() ?? StateLabel(currentInspection.State), severity);
    }

    private void LoadEditor(JiggleProjectConfig config)
    {
        drawRows.Clear();
        maskRows.Clear();
        editorGroupNames.Clear();
        editorGroupNames.Add(OriginalPartsConfig.GroupName);
        originalPartsEnabledEditor = config.OriginalParts.DeformationEnabled;
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

    private void CreateConfig_Click(object sender, RoutedEventArgs e)
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
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, config);
            OpenProject(currentInspection.ModPath);
            ShowMessage($"已原地适配 {result.DrawCount} 个 Draw。配置界面已经打开；修改后按 F10 查看效果。", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void RepairRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (currentInspection?.Configuration is null)
        {
            return;
        }

        try
        {
            RuntimeApplyResult result = runtimeCompiler.Apply(currentInspection.ModPath, currentInspection.Configuration);
            OpenProject(currentInspection.ModPath);
            ShowMessage($"运行文件已修复，共检查 {result.DrawCount} 个 Draw。回到游戏按 F10。", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowMessage(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void ApplyConfiguration_Click(object sender, RoutedEventArgs e)
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
            ShowMessage($"配置已应用到 {result.DrawCount} 个 Draw。回到游戏按 F10 查看效果。", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            ShowMessage(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void InspectorToggle_Click(object sender, RoutedEventArgs e)
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
            ShowMessage(
                enabled
                    ? $"Draw 检测器已开启，共覆盖 {result.DrawCount} 个 Draw。回到游戏按 F10，然后拖动模型查看。"
                    : "Draw 检测器已关闭。回到游戏按 F10 生效。",
                InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or FormatException)
        {
            currentConfiguration.Inspector.Enabled = previous;
            InspectorToggleButton.IsChecked = previous;
            UpdateInspectorButtonText();
            ShowMessage(exception.Message, InfoBarSeverity.Error);
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
        CaptureOriginalPartsToggle();
        CommitCurrentPhysicsScope();
        config.OriginalParts.DeformationEnabled = originalPartsEnabledEditor;
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
                throw new InvalidDataException("依赖边的两个组都必须选择。");
            }

            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"不能添加自身依赖边：{from} → {to}。");
            }

            if (!edgeKeys.Add(from + "\0" + to))
            {
                throw new InvalidDataException($"依赖边重复：{from} → {to}。");
            }

            config.Edges.Add(new JiggleEdgeConfig { From = from, To = to });
        }

        config.Inspector.Enabled = InspectorToggleButton.IsChecked == true;
    }

    private void UpdateInspectorButtonText()
    {
        InspectorToggleButton.Content = InspectorToggleButton.IsChecked == true
            ? "Draw 检测器：已开启"
            : "Draw 检测器：已关闭";
    }

}
