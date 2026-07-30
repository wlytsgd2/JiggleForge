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
    private readonly ModProjectService projectService = new();
    private readonly ModRuntimeCompiler runtimeCompiler = new();
    private readonly RuntimeEnvironmentService runtimeEnvironmentService = new(
        Path.Combine(AppContext.BaseDirectory, "RuntimePayload"));
    private readonly ObservableCollection<DrawEditorRow> drawRows = [];
    private readonly ObservableCollection<DrawEditorRow> maskRows = [];
    private ObservableCollection<DrawTreeItem> drawTreeRoots = [];
    private readonly HashSet<string> editorGroupNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<PhysicsScopeOption> physicsScopeOptions = [];
    private readonly Dictionary<string, PhysicsSettings> editorPhysicsByScope =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<DragKeyOption> dragKeyOptions =
    [
        new("VK_LBUTTON", "鼠标左键"),
        new("VK_RBUTTON", "鼠标右键"),
        new("VK_MBUTTON", "鼠标中键"),
        new("VK_XBUTTON1", "鼠标侧键 1"),
        new("VK_XBUTTON2", "鼠标侧键 2"),
        new("X", "键盘 X"),
        new("C", "键盘 C"),
        new("V", "键盘 V"),
    ];
    private readonly ObservableCollection<string> graphGroupOptions = [];
    private readonly ObservableCollection<string> graphTargetGroupOptions = [];
    private readonly ObservableCollection<EdgeEditorRow> edgeRows = [];
    private readonly Dictionary<string, Point> graphNodePositions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FrameworkElement> graphNodeElements = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UIElement> graphEdgeVisuals = [];
    private readonly List<DispatcherTimer> graphEdgeHideTimers = [];
    private const double GraphNodeWidth = 164;
    private const double GraphNodeHeight = 58;
    private static readonly string DragKeyPreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "DragKey.txt");
    private static readonly string PhysicsDefaultsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "PhysicsDefaults.json");
    private PhysicsSettings defaultPhysics = LoadDefaultPhysicsPreference();
    private string? draggedGraphNode;
    private Point graphDragPointerStart;
    private Point graphDragNodeStart;
    private string? connectingFromGroup;
    private Line? connectionPreview;
    private ModProjectInspection? currentInspection;
    private JiggleProjectConfig? currentConfiguration;
    private RuntimeEnvironmentStatus? runtimeStatus;
    private bool runtimeStatusInitialized;
    private int runtimeBusyCount;
    private bool originalPartsEnabledEditor;
    private const string DefaultPhysicsScopeKey = "";
    private string activePhysicsScopeKey = DefaultPhysicsScopeKey;
    private bool physicsScopeChanging;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1120, 800));
        string appIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "JiggleForge-Silver.ico");
        if (File.Exists(appIconPath))
        {
            AppWindow.SetIcon(appIconPath);
        }

        MaskEditorList.ItemsSource = maskRows;
        EdgeEditorList.ItemsSource = edgeRows;
        PhysicsScopeComboBox.ItemsSource = physicsScopeOptions;
        DragKeyOptionsList.ItemsSource = dragKeyOptions;
        RuntimePathTextBox.Text = RuntimeEnvironmentService.DefaultZzmiRoot;
        SelectDragKeys(LoadDragKeyPreference());
        LoadDefaultPhysicsEditor(defaultPhysics);
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (runtimeStatusInitialized)
        {
            return;
        }

        runtimeStatusInitialized = true;
        await RefreshRuntimeStatusAsync();
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag)
        {
            ShowView(tag);
        }
    }

    private void ShowView(string tag)
    {
        OverviewView.Visibility = tag == "overview" ? Visibility.Visible : Visibility.Collapsed;
        DrawView.Visibility = tag == "draws" ? Visibility.Visible : Visibility.Collapsed;
        GraphView.Visibility = tag == "graph" ? Visibility.Visible : Visibility.Collapsed;
        MaskView.Visibility = tag == "mask" ? Visibility.Visible : Visibility.Collapsed;
        PhysicsView.Visibility = tag == "physics" ? Visibility.Visible : Visibility.Collapsed;
        RuntimeView.Visibility = tag == "runtime" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "graph")
        {
            RefreshGraphGroups(clearInvalidEdges: true);
            if (GraphModeComboBox.SelectedIndex == 1)
            {
                BuildInteractiveGraph();
            }
        }
        else if (tag == "runtime")
        {
            _ = RefreshRuntimeStatusAsync();
        }
    }

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private static string StateLabel(ModImportState state) => state switch
    {
        ModImportState.FirstImport => "首次导入",
        ModImportState.Ready => "配置已就绪",
        ModImportState.RuntimeRepairRequired => "需要修复运行文件",
        ModImportState.PatchedConfigurationMissing => "需要恢复配置",
        ModImportState.LegacyMigrationRequired => "需要迁移旧版本",
        _ => "无法导入",
    };

}
public sealed class DrawEditorRow
{
    public required string Id { get; init; }

    public string Alias { get; set; } = string.Empty;

    public bool DeformationEnabled { get; set; } = true;

    public bool IsAliasReadOnly { get; init; }

    public bool IsOriginalParts { get; init; }

    public string Group { get; set; } = string.Empty;

    public string Mask { get; set; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string Branch { get; init; } = string.Empty;

    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Id : $"{Id} · {Alias}";
}

public abstract class DrawTreeItem
{
}

public sealed class DrawTreeDrawNode : DrawTreeItem
{
    public required DrawEditorRow Row { get; init; }
}

public sealed class DrawGroupEditorNode : DrawTreeItem, INotifyPropertyChanged
{
    private bool originalPartsEnabled;

    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public bool IsOriginalParts { get; init; }

    public bool IsUngrouped { get; init; }

    public bool IsExpanded { get; set; } = true;

    public Visibility OriginalToggleVisibility =>
        IsOriginalParts ? Visibility.Visible : Visibility.Collapsed;

    public ObservableCollection<DrawTreeItem> Children { get; } = [];

    public bool OriginalPartsEnabled
    {
        get => originalPartsEnabled;
        set
        {
            if (originalPartsEnabled == value)
            {
                return;
            }

            originalPartsEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OriginalPartsEnabled)));
        }
    }

    public string CountText => $"{Children.Count} 个 Draw";

    public void RefreshCount() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DrawTreeTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GroupTemplate { get; set; }

    public DataTemplate? DrawTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        object value = item is TreeViewNode node ? node.Content : item;
        return value is DrawGroupEditorNode ? GroupTemplate : DrawTemplate;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) =>
        SelectTemplateCore(item);
}

public sealed class DragKeyOption : INotifyPropertyChanged
{
    private bool isSelected;

    public DragKeyOption(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record PhysicsScopeOption(string Key, string DisplayName);

public sealed class EdgeEditorRow : INotifyPropertyChanged
{
    private string from = string.Empty;
    private string to = string.Empty;

    public EdgeEditorRow(
        ObservableCollection<string> fromOptions,
        ObservableCollection<string> toOptions)
    {
        FromOptions = fromOptions;
        ToOptions = toOptions;
    }

    public ObservableCollection<string> FromOptions { get; }

    public ObservableCollection<string> ToOptions { get; }

    public string From
    {
        get => from;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(from, normalized, StringComparison.Ordinal))
            {
                return;
            }

            from = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(From)));
        }
    }

    public string To
    {
        get => to;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(to, normalized, StringComparison.Ordinal))
            {
                return;
            }

            to = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(To)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
