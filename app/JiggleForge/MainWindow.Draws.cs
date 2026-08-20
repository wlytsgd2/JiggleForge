using System.Collections.ObjectModel;
using JiggleForge.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private const string DrawDragDataFormat = "JiggleForge.DrawId";

    private void RefreshDrawTree()
    {
        Dictionary<string, bool> expanded = new(StringComparer.OrdinalIgnoreCase);
        foreach (TreeViewNode node in DrawGroupTree.RootNodes)
        {
            if (node.Content is DrawGroupEditorNode group)
            {
                string key = group.IsUngrouped ? "\0Ungrouped" : group.Name;
                expanded[key] = node.IsExpanded;
            }
        }

        editorGroupNames.Add(OriginalPartsConfig.GroupName);
        foreach (DrawEditorRow row in drawRows)
        {
            row.Group = row.Group.Trim();
            if (row.Group.Length > 0)
            {
                editorGroupNames.Add(row.Group);
            }
        }

        List<DrawTreeItem> rebuiltRoots = [];
        DrawGroupEditorNode original = CreateGroupNode(
            OriginalPartsConfig.GroupName,
            L("OriginalPartsDisplayName"),
            isOriginalParts: true,
            isUngrouped: false,
            expanded);
        AddDrawChildren(original, OriginalPartsConfig.GroupName);
        rebuiltRoots.Add(original);

        DrawGroupEditorNode ungrouped = CreateGroupNode(
            string.Empty,
            L("UngroupedDisplayName"),
            isOriginalParts: false,
            isUngrouped: true,
            expanded);
        AddDrawChildren(ungrouped, string.Empty);
        rebuiltRoots.Add(ungrouped);

        foreach (string groupName in editorGroupNames
                     .Where(name => !string.Equals(
                         name,
                         OriginalPartsConfig.GroupName,
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            DrawGroupEditorNode group = CreateGroupNode(
                groupName,
                groupName,
                isOriginalParts: false,
                isUngrouped: false,
                expanded);
            AddDrawChildren(group, groupName);
            rebuiltRoots.Add(group);
        }

        // TreeView already creates a TreeViewItem container for every TreeViewNode.
        // Keep only data in the item templates and describe hierarchy here.
        drawTreeRoots = new ObservableCollection<DrawTreeItem>(rebuiltRoots);
        DrawGroupTree.RootNodes.Clear();
        foreach (DrawGroupEditorNode group in rebuiltRoots.OfType<DrawGroupEditorNode>())
        {
            TreeViewNode root = new()
            {
                Content = group,
                IsExpanded = group.IsExpanded,
            };
            foreach (DrawTreeItem child in group.Children)
            {
                root.Children.Add(new TreeViewNode { Content = child });
            }
            DrawGroupTree.RootNodes.Add(root);
        }
    }

    private static DrawGroupEditorNode CreateGroupNode(
        string name,
        string displayName,
        bool isOriginalParts,
        bool isUngrouped,
        IReadOnlyDictionary<string, bool> expanded)
    {
        string expansionKey = isUngrouped ? "\0Ungrouped" : name;
        return new DrawGroupEditorNode
        {
            Name = name,
            DisplayName = displayName,
            IsOriginalParts = isOriginalParts,
            IsUngrouped = isUngrouped,
            IsExpanded = !expanded.TryGetValue(expansionKey, out bool wasExpanded) || wasExpanded,
        };
    }

    private void AddDrawChildren(DrawGroupEditorNode group, string groupName)
    {
        foreach (DrawEditorRow row in drawRows.Where(row =>
                     string.Equals(row.Group, groupName, StringComparison.OrdinalIgnoreCase)))
        {
            group.Children.Add(new DrawTreeDrawNode { Row = row });
        }
        group.RefreshCount();
    }

    private async void CreateDrawGroup_Click(object sender, RoutedEventArgs e)
    {
        string? name = await PromptGroupNameAsync(L("NewGroup"), string.Empty);
        if (name is null || !TryAddGroupName(name))
        {
            return;
        }

        RefreshDrawTree();
        RefreshGraphGroups(clearInvalidEdges: true);
        RefreshPhysicsScopeOptions();
    }

    private void DrawLeaf_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DrawEditorRow row })
        {
            e.Data.SetData(DrawDragDataFormat, row.Id);
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void DrawGroup_DragOver(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DrawGroupEditorNode })
        {
            bool isDraw = e.DataView.Contains(DrawDragDataFormat);
            e.AcceptedOperation = isDraw
                ? DataPackageOperation.Move
                : DataPackageOperation.None;
            if (isDraw)
            {
                e.DragUIOverride.Caption = L("MoveToThisGroup");
            }
            e.Handled = true;
        }
    }

    private async void DrawGroup_Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DrawGroupEditorNode group } ||
            !e.DataView.Contains(DrawDragDataFormat))
        {
            e.Handled = true;
            return;
        }

        object drawData = await e.DataView.GetDataAsync(DrawDragDataFormat);
        string drawId = drawData as string ?? string.Empty;
        DrawEditorRow? row = drawRows.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, drawId, StringComparison.OrdinalIgnoreCase));
        if (row is not null)
        {
            string destination = group.IsUngrouped ? string.Empty : group.Name;
            // Rebuilding a TreeView from inside its Drop event can leave recycled
            // TreeViewItem containers blank. Finish the drop first, then rebuild.
            DispatcherQueue.TryEnqueue(() => MoveDrawToGroup(row, destination));
        }
        e.Handled = true;
    }

    private void DrawLeaf_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DrawEditorRow row } element)
        {
            return;
        }

        MenuFlyout flyout = new();
        MenuFlyoutSubItem move = new() { Text = L("MoveToGroup") };
        move.Items.Add(CreateMoveMenuItem(row, string.Empty, L("UngroupedDisplayName")));
        foreach (string groupName in editorGroupNames
                     .OrderBy(name =>
                         string.Equals(name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase)
                             ? 0
                             : 1)
                     .ThenBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            move.Items.Add(CreateMoveMenuItem(row, groupName, groupName));
        }
        flyout.Items.Add(move);

        MenuFlyoutItem create = new() { Text = L("NewGroupAndMove") };
        create.Click += async (_, _) =>
        {
            string? name = await PromptGroupNameAsync(L("NewGroupAndMove"), string.Empty);
            if (name is not null && TryAddGroupName(name))
            {
                MoveDrawToGroup(row, name.Trim());
                RefreshPhysicsScopeOptions();
            }
        };
        flyout.Items.Add(create);
        flyout.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }

    private MenuFlyoutItem CreateMoveMenuItem(
        DrawEditorRow row,
        string groupName,
        string displayName)
    {
        bool current = string.Equals(row.Group, groupName, StringComparison.OrdinalIgnoreCase);
        MenuFlyoutItem item = new()
        {
            Text = current ? $"✓ {displayName}" : displayName,
            IsEnabled = !current,
        };
        item.Click += (_, _) => MoveDrawToGroup(row, groupName);
        return item;
    }

    private void DrawGroup_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DrawGroupEditorNode group } element ||
            group.IsOriginalParts ||
            group.IsUngrouped)
        {
            return;
        }

        MenuFlyout flyout = new();
        MenuFlyoutItem rename = new() { Text = L("RenameGroup") };
        rename.Click += async (_, _) =>
        {
            string? name = await PromptGroupNameAsync(L("RenameGroup"), group.Name);
            if (name is not null)
            {
                RenameGroup(group.Name, name);
            }
        };
        flyout.Items.Add(rename);

        MenuFlyoutItem delete = new() { Text = L("DeleteGroup") };
        delete.Click += (_, _) => DeleteGroup(group.Name);
        flyout.Items.Add(delete);
        flyout.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }

    private void MoveDrawToGroup(DrawEditorRow row, string groupName)
    {
        groupName = groupName.Trim();
        if (groupName.Length > 0)
        {
            editorGroupNames.Add(groupName);
        }
        row.Group = groupName;
        RefreshDrawTree();
        RefreshGraphGroups(clearInvalidEdges: true);
    }

    private bool TryAddGroupName(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            ShowMessage(L("GroupNameRequired"), InfoBarSeverity.Warning);
            return false;
        }
        if (string.Equals(name, L("UngroupedDisplayName"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(L("GroupNameReserved"), InfoBarSeverity.Warning);
            return false;
        }
        if (!editorGroupNames.Add(name))
        {
            ShowMessage(AppLanguageService.Format("GroupAlreadyExists", name), InfoBarSeverity.Warning);
            return false;
        }
        editorPhysicsByScope[name] = GetDefaultProjectPhysics().Clone();
        return true;
    }

    private void RenameGroup(string oldName, string newName)
    {
        newName = newName.Trim();
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        CommitCurrentPhysicsScope();
        PhysicsSettings oldPhysics = editorPhysicsByScope.TryGetValue(
            oldName,
            out PhysicsSettings? existingPhysics)
            ? existingPhysics.Clone()
            : GetDefaultProjectPhysics().Clone();
        string previousActiveScope = activePhysicsScopeKey;
        if (!TryAddGroupName(newName))
        {
            return;
        }

        editorGroupNames.Remove(oldName);
        editorPhysicsByScope.Remove(oldName);
        editorPhysicsByScope[newName] = oldPhysics;
        foreach (DrawEditorRow row in drawRows.Where(row =>
                     string.Equals(row.Group, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            row.Group = newName;
        }
        foreach (EdgeEditorRow edge in edgeRows)
        {
            if (string.Equals(edge.From, oldName, StringComparison.OrdinalIgnoreCase))
            {
                edge.From = newName;
            }
            if (string.Equals(edge.To, oldName, StringComparison.OrdinalIgnoreCase))
            {
                edge.To = newName;
            }
        }
        if (graphNodePositions.Remove(oldName, out Point position))
        {
            graphNodePositions[newName] = position;
        }

        RefreshDrawTree();
        RefreshGraphGroups(clearInvalidEdges: true);
        RefreshPhysicsScopeOptions(
            string.Equals(previousActiveScope, oldName, StringComparison.OrdinalIgnoreCase)
                ? newName
                : previousActiveScope,
            commitCurrent: false);
    }

    private void DeleteGroup(string groupName)
    {
        CommitCurrentPhysicsScope();
        bool deletingActiveScope = string.Equals(
            activePhysicsScopeKey,
            groupName,
            StringComparison.OrdinalIgnoreCase);
        editorGroupNames.Remove(groupName);
        editorPhysicsByScope.Remove(groupName);
        foreach (DrawEditorRow row in drawRows.Where(row =>
                     string.Equals(row.Group, groupName, StringComparison.OrdinalIgnoreCase)))
        {
            row.Group = string.Empty;
        }
        foreach (EdgeEditorRow edge in edgeRows.Where(edge =>
                     string.Equals(edge.From, groupName, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(edge.To, groupName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            edge.PropertyChanged -= EdgeRow_PropertyChanged;
            edgeRows.Remove(edge);
        }
        graphNodePositions.Remove(groupName);
        RefreshDrawTree();
        RefreshGraphGroups(clearInvalidEdges: true);
        RefreshPhysicsScopeOptions(
            deletingActiveScope ? DefaultPhysicsScopeKey : activePhysicsScopeKey,
            commitCurrent: false);
    }

    private async Task<string?> PromptGroupNameAsync(string title, string initialValue)
    {
        TextBox input = new()
        {
            Text = initialValue,
            PlaceholderText = L("EnterGroupName"),
            MinWidth = 320,
            SelectionStart = 0,
            SelectionLength = initialValue.Length,
        };
        ContentDialog dialog = new()
        {
            XamlRoot = DrawView.XamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = L("Confirm"),
            CloseButtonText = L("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text.Trim() : null;
    }
}
