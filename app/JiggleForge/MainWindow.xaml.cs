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
    private static string L(string key) => AppLanguageService.Get(key);

    private readonly ModProjectService projectService = new();
    private readonly ModLibraryService modLibraryService;
    private readonly ModRuntimeCompiler runtimeCompiler = new();
    private readonly ModBackupService backupService = new();
    private readonly ModProjectHistoryService projectHistoryService = new(
        ApplicationSettingsDirectory);
    private readonly RuntimeEnvironmentService runtimeEnvironmentService = new(
        ApplicationLayout.RuntimePayloadDirectory);
    private readonly ObservableCollection<DrawEditorRow> drawRows = [];
    private readonly ObservableCollection<DrawEditorRow> maskRows = [];
    private readonly ObservableCollection<ModLibraryRow> modLibraryRows = [];
    private ObservableCollection<DrawTreeItem> drawTreeRoots = [];
    private readonly HashSet<string> editorGroupNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<PhysicsScopeOption> physicsScopeOptions = [];
    private readonly Dictionary<string, PhysicsSettings> editorPhysicsByScope =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<DragKeyOption> dragKeyOptions =
    [
        new("VK_LBUTTON", L("DragKeyLeftMouse")),
        new("VK_RBUTTON", L("DragKeyRightMouse")),
        new("VK_MBUTTON", L("DragKeyMiddleMouse")),
        new("VK_XBUTTON1", L("DragKeySideMouse1")),
        new("VK_XBUTTON2", L("DragKeySideMouse2")),
        new("X", L("DragKeyKeyboardX")),
        new("C", L("DragKeyKeyboardC")),
        new("V", L("DragKeyKeyboardV")),
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
    private static readonly string RuntimeToggleKeyPreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "RuntimeToggleKey.txt");
    private static readonly string ZzmiRootPreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "ZzmiRoot.txt");
    private static readonly string PhysicsDefaultsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "PhysicsDefaults.json");
    private static readonly string ApplicationSettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge");
    private readonly PhysicsDefaultsMigrationState physicsDefaultsMigration = new(
        ApplicationSettingsDirectory);
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
    private const string DefaultPhysicsScopeKey = "";
    private string activePhysicsScopeKey = DefaultPhysicsScopeKey;
    private bool physicsScopeChanging;

    public MainWindow()
    {
        modLibraryService = new ModLibraryService(projectService);
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
        ModLibraryList.ItemsSource = modLibraryRows;
        LanguageComboBox.SelectedIndex =
            AppLanguageService.CurrentLanguage == AppLanguageService.English ? 1 : 0;
        RuntimePathTextBox.Text = LoadZzmiRootPreference();
        SelectDragKeys(LoadDragKeyPreference());
        SelectRuntimeToggleKey(LoadRuntimeToggleKeyPreference());
        LoadDefaultPhysicsEditor(defaultPhysics);
        InitializeApplicationUpdateView();
    }

    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (runtimeStatusInitialized)
        {
            return;
        }

        runtimeStatusInitialized = true;
        if (!AppLanguageService.HasSavedLanguage)
        {
            await ShowInitialLanguageSelectionAsync();
            return;
        }

        await ApplyRecommendedPhysicsDefaultsMigrationAsync();
        await RefreshRuntimeStatusAsync();
        await RefreshModLibraryAsync();
        await CheckApplicationUpdatesAsync(promptIfAvailable: true, showResult: false);
        if (ShouldShowOnboarding())
        {
            await ShowOnboardingAsync(automatic: true);
        }
    }

    private async Task ShowInitialLanguageSelectionAsync()
    {
        ContentDialog dialog = new()
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "选择语言 / Choose your language",
            Content = "请选择 JiggleForge 的显示语言。\nChoose the display language for JiggleForge.",
            PrimaryButtonText = "简体中文",
            SecondaryButtonText = "English",
            DefaultButton = ContentDialogButton.Primary,
        };

        ContentDialogResult result = await dialog.ShowAsync();
        string language = result == ContentDialogResult.Secondary
            ? AppLanguageService.English
            : AppLanguageService.Chinese;
        try
        {
            AppLanguageService.SaveLanguage(language);
            AppLanguageService.RestartApplication();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowMessage(
                AppLanguageService.Format("LanguageSwitchFailed", AppLanguageService.LocalizeException(exception)),
                InfoBarSeverity.Error);
        }
    }

    private void ApplyLanguage_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string language)
        {
            return;
        }

        if (string.Equals(language, AppLanguageService.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            ShowMessage(AppLanguageService.Get("LanguageAlreadyActive"), InfoBarSeverity.Informational);
            return;
        }

        try
        {
            AppLanguageService.SaveLanguage(language);
            AppLanguageService.RestartApplication();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowMessage(
                AppLanguageService.Format("LanguageSwitchFailed", AppLanguageService.LocalizeException(exception)),
                InfoBarSeverity.Error);
        }
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
        ModImportState.FirstImport => L("StateFirstImport"),
        ModImportState.Ready => L("StateReady"),
        ModImportState.RuntimeRepairRequired => L("StateRuntimeRepair"),
        ModImportState.PatchedConfigurationMissing => L("StateConfigMissing"),
        ModImportState.LegacyMigrationRequired => L("StateLegacyMigration"),
        _ => L("StateCannotImport"),
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
    public required string Name { get; init; }

    public required string DisplayName { get; init; }

    public bool IsOriginalParts { get; init; }

    public bool IsUngrouped { get; init; }

    public bool IsExpanded { get; set; } = true;

    public ObservableCollection<DrawTreeItem> Children { get; } = [];

    public string CountText => AppLanguageService.Format("DrawCount", Children.Count);

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
