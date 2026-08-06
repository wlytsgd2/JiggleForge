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
    private void RefreshGraphGroups(bool clearInvalidEdges)
    {
        string[] groups = editorGroupNames
            .OrderBy(group => group, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        GroupSummaryText.Text = groups.Length == 0
            ? L("NoGroupsYet")
            : string.Join(AppLanguageService.CurrentLanguage == AppLanguageService.English ? ", " : "、", groups);

        List<(string From, string To)> existingEdges = edgeRows
            .Select(edge => (edge.From, edge.To))
            .ToList();
        graphGroupOptions.Clear();
        foreach (string group in groups)
        {
            graphGroupOptions.Add(group);
        }
        graphTargetGroupOptions.Clear();
        foreach (string group in groups.Where(group => !string.Equals(
                     group,
                     OriginalPartsConfig.GroupName,
                     StringComparison.OrdinalIgnoreCase)))
        {
            graphTargetGroupOptions.Add(group);
        }

        if (clearInvalidEdges && edgeRows.Count > 0)
        {
            edgeRows.Clear();
            foreach ((string from, string to) in existingEdges)
            {
                if (groups.Contains(from, StringComparer.OrdinalIgnoreCase) &&
                    graphTargetGroupOptions.Contains(to, StringComparer.OrdinalIgnoreCase))
                {
                    AddEdgeRow(from, to);
                }
            }
        }

        HashSet<string> validGroups = groups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string staleGroup in graphNodePositions.Keys.Where(name => !validGroups.Contains(name)).ToArray())
        {
            graphNodePositions.Remove(staleGroup);
        }
    }

    private void GraphMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EdgeListPanel is null || InteractiveGraphPanel is null)
        {
            return;
        }

        bool showGraph = GraphModeComboBox.SelectedIndex == 1;
        EdgeListPanel.Visibility = showGraph ? Visibility.Collapsed : Visibility.Visible;
        InteractiveGraphPanel.Visibility = showGraph ? Visibility.Visible : Visibility.Collapsed;
        if (showGraph)
        {
            RefreshGraphGroups(clearInvalidEdges: true);
            BuildInteractiveGraph();
        }
    }

    private void AddEdge_Click(object sender, RoutedEventArgs e)
    {
        RefreshGraphGroups(clearInvalidEdges: true);
        string from = graphGroupOptions.FirstOrDefault() ?? string.Empty;
        string to = graphTargetGroupOptions.FirstOrDefault(group =>
            !string.Equals(group, from, StringComparison.OrdinalIgnoreCase))
            ?? graphTargetGroupOptions.FirstOrDefault()
            ?? string.Empty;
        if (to.Length == 0)
        {
            ShowMessage(L("NeedNormalGroupForEdge"), InfoBarSeverity.Warning);
            return;
        }
        AddEdgeRow(from, to);
    }

    private void DeleteEdge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EdgeEditorRow row })
        {
            row.PropertyChanged -= EdgeRow_PropertyChanged;
            edgeRows.Remove(row);
            RedrawGraphEdges();
        }
    }

    private void AddEdgeRow(string from, string to)
    {
        from = graphGroupOptions.FirstOrDefault(group =>
            string.Equals(group, from, StringComparison.OrdinalIgnoreCase)) ?? from;
        to = graphTargetGroupOptions.FirstOrDefault(group =>
            string.Equals(group, to, StringComparison.OrdinalIgnoreCase)) ?? to;
        EdgeEditorRow row = new(graphGroupOptions, graphTargetGroupOptions)
        {
            From = from,
            To = to,
        };
        row.PropertyChanged += EdgeRow_PropertyChanged;
        edgeRows.Add(row);
        RedrawGraphEdges();
    }

    private void AddGraphEdge(string from, string to)
    {
        if (string.Equals(to, OriginalPartsConfig.GroupName, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(L("OriginalPartsCannotBeTarget"), InfoBarSeverity.Warning);
            return;
        }
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) ||
            edgeRows.Any(edge => string.Equals(edge.From, from, StringComparison.OrdinalIgnoreCase) &&
                                 string.Equals(edge.To, to, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        AddEdgeRow(from, to);
    }

    private void EdgeRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RedrawGraphEdges();
    }

    private void BuildInteractiveGraph()
    {
        if (GraphCanvas is null)
        {
            return;
        }

        GraphCanvas.Children.Clear();
        graphEdgeVisuals.Clear();
        graphNodeElements.Clear();
        connectionPreview = null;
        EnsureGraphNodePositions();

        foreach (string group in graphGroupOptions)
        {
            Grid node = new()
            {
                Width = GraphNodeWidth,
                Height = GraphNodeHeight,
                Tag = group,
            };

            Border body = new()
            {
                Tag = group,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(14, 0, 14, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.DeepSkyBlue),
                Background = new SolidColorBrush(Colors.Transparent),
                Child = new TextBlock
                {
                    Text = group,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            ToolTipService.SetToolTip(body, AppLanguageService.Format("GraphNodeTooltip", group));
            body.PointerPressed += GraphNode_PointerPressed;
            node.Children.Add(body);

            Point position = graphNodePositions[group];
            Canvas.SetLeft(node, position.X);
            Canvas.SetTop(node, position.Y);
            Canvas.SetZIndex(node, 2);
            GraphCanvas.Children.Add(node);
            graphNodeElements[group] = node;
        }

        RedrawGraphEdges();
    }

    private void EnsureGraphNodePositions()
    {
        double availableWidth = Math.Max(GraphCanvas.ActualWidth, 720);
        int columns = Math.Max(1, (int)((availableWidth - 36) / (GraphNodeWidth + 54)));
        int index = 0;
        foreach (string group in graphGroupOptions)
        {
            if (!graphNodePositions.ContainsKey(group))
            {
                int column = index % columns;
                int row = index / columns;
                graphNodePositions[group] = new Point(
                    28 + column * (GraphNodeWidth + 54),
                    28 + row * (GraphNodeHeight + 70));
            }

            index++;
        }

        double requiredHeight = graphNodePositions.Count == 0
            ? 520
            : graphNodePositions.Values.Max(position => position.Y) + GraphNodeHeight + 56;
        GraphCanvas.Height = Math.Max(520, requiredHeight);
    }

    private void GraphNode_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string group } element)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(GraphCanvas);
        if (pointer.Properties.IsLeftButtonPressed)
        {
            draggedGraphNode = group;
            connectingFromGroup = null;
            graphDragPointerStart = pointer.Position;
            graphDragNodeStart = graphNodePositions[group];
        }
        else if (pointer.Properties.IsRightButtonPressed)
        {
            draggedGraphNode = null;
            connectingFromGroup = group;
            Point start = GraphNodeCenter(group);
            connectionPreview = new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = pointer.Position.X,
                Y2 = pointer.Position.Y,
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                IsHitTestVisible = false,
            };
            Canvas.SetZIndex(connectionPreview, 3);
            GraphCanvas.Children.Add(connectionPreview);
        }
        else
        {
            return;
        }

        GraphCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void GraphCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        Point pointer = e.GetCurrentPoint(GraphCanvas).Position;
        if (draggedGraphNode is not null)
        {
            double maxX = Math.Max(0, GraphCanvas.ActualWidth - GraphNodeWidth);
            double maxY = Math.Max(0, GraphCanvas.Height - GraphNodeHeight);
            Point position = new(
                Math.Clamp(graphDragNodeStart.X + pointer.X - graphDragPointerStart.X, 0, maxX),
                Math.Clamp(graphDragNodeStart.Y + pointer.Y - graphDragPointerStart.Y, 0, maxY));
            graphNodePositions[draggedGraphNode] = position;
            if (graphNodeElements.TryGetValue(draggedGraphNode, out FrameworkElement? node))
            {
                Canvas.SetLeft(node, position.X);
                Canvas.SetTop(node, position.Y);
            }

            RedrawGraphEdges();
            e.Handled = true;
        }
        else if (connectionPreview is not null)
        {
            connectionPreview.X2 = pointer.X;
            connectionPreview.Y2 = pointer.Y;
            e.Handled = true;
        }
    }

    private void GraphCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (connectingFromGroup is not null)
        {
            Point pointer = e.GetCurrentPoint(GraphCanvas).Position;
            string? target = FindGraphNodeAt(pointer, connectingFromGroup);
            if (target is not null)
            {
                AddGraphEdge(connectingFromGroup, target);
            }
        }

        EndGraphPointerOperation(e);
    }

    private void GraphCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        EndGraphPointerOperation(e);
    }

    private void EndGraphPointerOperation(PointerRoutedEventArgs e)
    {
        GraphCanvas.ReleasePointerCapture(e.Pointer);
        if (connectionPreview is not null)
        {
            GraphCanvas.Children.Remove(connectionPreview);
        }

        connectionPreview = null;
        connectingFromGroup = null;
        draggedGraphNode = null;
        e.Handled = true;
    }

    private string? FindGraphNodeAt(Point position, string excludedGroup)
    {
        return graphGroupOptions.LastOrDefault(group =>
        {
            if (string.Equals(group, excludedGroup, StringComparison.OrdinalIgnoreCase) ||
                !graphNodePositions.TryGetValue(group, out Point nodePosition))
            {
                return false;
            }

            return position.X >= nodePosition.X && position.X <= nodePosition.X + GraphNodeWidth &&
                   position.Y >= nodePosition.Y && position.Y <= nodePosition.Y + GraphNodeHeight;
        });
    }

    private void RedrawGraphEdges()
    {
        if (GraphCanvas is null || graphNodeElements.Count == 0)
        {
            return;
        }

        foreach (UIElement visual in graphEdgeVisuals)
        {
            GraphCanvas.Children.Remove(visual);
        }
        graphEdgeVisuals.Clear();
        foreach (DispatcherTimer timer in graphEdgeHideTimers)
        {
            timer.Stop();
        }
        graphEdgeHideTimers.Clear();

        SolidColorBrush edgeBrush = new(Colors.DeepSkyBlue);
        foreach (EdgeEditorRow edge in edgeRows)
        {
            if (!graphNodePositions.ContainsKey(edge.From) || !graphNodePositions.ContainsKey(edge.To) ||
                string.Equals(edge.From, edge.To, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (Point start, Point end) = GraphEdgeEndpoints(edge.From, edge.To);
            Line line = new()
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = edgeBrush,
                StrokeThickness = 2.5,
                IsHitTestVisible = false,
            };
            AddGraphEdgeVisual(line, zIndex: 0);

            Line hitLine = new()
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = new SolidColorBrush(Colors.Transparent),
                StrokeThickness = 16,
            };
            AddGraphEdgeVisual(hitLine, zIndex: 1);

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length > 0.1)
            {
                double ux = dx / length;
                double uy = dy / length;
                double px = -uy;
                double py = ux;
                const double arrowLength = 12;
                const double arrowWidth = 6;
                Polygon arrow = new()
                {
                    Fill = edgeBrush,
                    IsHitTestVisible = false,
                    Points = new PointCollection
                    {
                        end,
                        new Point(end.X - ux * arrowLength + px * arrowWidth, end.Y - uy * arrowLength + py * arrowWidth),
                        new Point(end.X - ux * arrowLength - px * arrowWidth, end.Y - uy * arrowLength - py * arrowWidth),
                    },
                };
                AddGraphEdgeVisual(arrow, zIndex: 0);
            }

            Button delete = new()
            {
                Content = "×",
                Tag = edge,
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Visibility = Visibility.Collapsed,
            };
            ToolTipService.SetToolTip(delete, AppLanguageService.Format("DeleteEdgeTooltip", edge.From, edge.To));
            delete.Click += DeleteEdge_Click;
            Canvas.SetLeft(delete, (start.X + end.X) / 2 - 14);
            Canvas.SetTop(delete, (start.Y + end.Y) / 2 - 14);
            AddGraphEdgeVisual(delete, zIndex: 2);

            bool lineHovered = false;
            bool buttonHovered = false;
            DispatcherTimer hideTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
            graphEdgeHideTimers.Add(hideTimer);
            hideTimer.Tick += (_, _) =>
            {
                hideTimer.Stop();
                if (!lineHovered && !buttonHovered)
                {
                    delete.Visibility = Visibility.Collapsed;
                }
            };
            hitLine.PointerEntered += (_, _) =>
            {
                lineHovered = true;
                hideTimer.Stop();
                delete.Visibility = Visibility.Visible;
            };
            hitLine.PointerExited += (_, _) =>
            {
                lineHovered = false;
                hideTimer.Stop();
                hideTimer.Start();
            };
            delete.PointerEntered += (_, _) =>
            {
                buttonHovered = true;
                hideTimer.Stop();
            };
            delete.PointerExited += (_, _) =>
            {
                buttonHovered = false;
                hideTimer.Stop();
                hideTimer.Start();
            };
        }
    }

    private void AddGraphEdgeVisual(UIElement visual, int zIndex)
    {
        Canvas.SetZIndex(visual, zIndex);
        GraphCanvas.Children.Add(visual);
        graphEdgeVisuals.Add(visual);
    }

    private Point GraphNodeCenter(string group)
    {
        Point position = graphNodePositions[group];
        return new Point(position.X + GraphNodeWidth / 2, position.Y + GraphNodeHeight / 2);
    }

    private (Point Start, Point End) GraphEdgeEndpoints(string from, string to)
    {
        Point source = GraphNodeCenter(from);
        Point target = GraphNodeCenter(to);
        double dx = target.X - source.X;
        double dy = target.Y - source.Y;
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01)
        {
            return (source, target);
        }

        double sourceScale = Math.Min(
            Math.Abs(dx) < 0.01 ? double.MaxValue : GraphNodeWidth / 2 / Math.Abs(dx),
            Math.Abs(dy) < 0.01 ? double.MaxValue : GraphNodeHeight / 2 / Math.Abs(dy));
        Point start = new(source.X + dx * sourceScale, source.Y + dy * sourceScale);
        Point end = new(target.X - dx * sourceScale, target.Y - dy * sourceScale);
        return (start, end);
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawGraphEdges();
    }

}
