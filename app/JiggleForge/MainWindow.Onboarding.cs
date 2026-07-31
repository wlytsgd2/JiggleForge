using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private const int CurrentOnboardingVersion = 2;

    private static readonly string OnboardingStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JiggleForge",
        "OnboardingState.json");

    private bool onboardingTourActive;
    private bool onboardingStepChanging;
    private int onboardingStepIndex;
    private IReadOnlyList<OnboardingTourStep> onboardingTourSteps = [];
    private FrameworkElement? onboardingCurrentTarget;
    private object? onboardingPreviousSelection;
    private bool onboardingPreviousPaneState;
    private bool[]? onboardingPreviousPageStates;

    private static bool ShouldShowOnboarding()
    {
        try
        {
            if (!File.Exists(OnboardingStatePath))
            {
                return true;
            }

            string json = File.ReadAllText(OnboardingStatePath);
            OnboardingState? state = JsonSerializer.Deserialize<OnboardingState>(json);
            return state is null || state.CompletedVersion < CurrentOnboardingVersion;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return true;
        }
    }

    private static void SaveOnboardingCompletion()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(OnboardingStatePath)!);
        OnboardingState state = new()
        {
            CompletedVersion = CurrentOnboardingVersion,
            CompletedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(
            OnboardingStatePath,
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private IReadOnlyList<OnboardingTourStep> CreateOnboardingTourSteps() =>
    [
        new(
            "overview",
            AppTitleBar,
            "欢迎使用 JiggleForge 测试版",
            "接下来会直接带你浏览每个页面和关键控件。JiggleForge 会修改所选 Mod 文件夹，测试重要 Mod 前请先保留副本。"),
        new(
            "overview",
            DropCard,
            "打开 Mod 项目",
            "把一个具体的角色 Mod 文件夹拖到这里，或点击“选择文件夹”。不要选择整个 Mods 或 _MANAGED_ 目录。"),
        new(
            "runtime",
            RuntimePathTextBox,
            "选择 ZZMI 根目录",
            "这里填写包含 Mods 与 ShaderFixes 的 ZZMI 根目录。默认通常位于 %APPDATA%\\XXMI Launcher\\ZZMI。"),
        new(
            "runtime",
            InstallRuntimeButton,
            "安装或更新运行环境",
            "首次使用、应用升级或游戏更新后点击这里。安装完成后回到游戏按 F10 重新加载。"),
        new(
            "runtime",
            DragKeyOptionsList,
            "设置全局拖动键",
            "可同时勾选多个鼠标键或键盘键。所有原版角色和已适配 Mod 共用这套全局拖动键。"),
        new(
            "runtime",
            StartWheelButton,
            "启用滚轮深度输入",
            "需要用滚轮控制屏幕前后方向时启动 WheelBridge；只使用平面拖动时可以不启动。",
            RuntimeOverallStatusText),
        new(
            "draws",
            DrawGroupTree,
            "识别、命名和分组 Draw",
            "每个 Draw 对应原 Mod 的一次适配绘制。可修改别名、关闭变形，并把有关联的 Draw 放入同一组。没有打开项目时这里会暂时为空。"),
        new(
            "draws",
            InspectorToggleButton,
            "游戏内 Draw 检测器",
            "辨认模型部位时开启；回到游戏按 F10 后，拖动目标会在左上角显示命中的 Draw。完成配置后建议关闭。"),
        new(
            "graph",
            GraphModeComboBox,
            "切换依赖关系编辑方式",
            "可使用边列表，或切换为可拖动节点、右键连边的互动图。"),
        new(
            "graph",
            EdgeEditorList,
            "设置组之间的影响",
            "有向边表示拖动起点组时也会带动终点组。依赖关系支持传递；请避免不需要的循环和跨部件影响。"),
        new(
            "mask",
            MaskEditorList,
            "为 Draw 指定纹理 Mask",
            "白色区域权重接近 1，黑色区域接近 0。未指定 Mask 时，整个 Draw 默认按 1.0 参与变形。"),
        new(
            "physics",
            PhysicsScopeComboBox,
            "选择要调整的物理范围",
            "可以编辑项目默认参数，也可以为每个 Draw 组设置独立参数。"),
        new(
            "physics",
            PhysicsFieldsGrid,
            "调整变形质感",
            "影响半径和强度决定范围与幅度；频率、阻尼、最大位移、深度范围和体积响应共同决定手感。"),
        new(
            "overview",
            GuideNavItem,
            "随时重新查看向导",
            "完成配置后点击页面底部的“应用配置”，回到游戏按 F10 测试。以后可从这里重新播放完整界面导览。"),
    ];

    private async Task ShowOnboardingAsync(bool automatic)
    {
        if (onboardingTourActive || RootGrid.XamlRoot is null)
        {
            return;
        }

        onboardingTourActive = true;
        onboardingStepChanging = false;
        onboardingStepIndex = 0;
        onboardingTourSteps = CreateOnboardingTourSteps();
        onboardingPreviousSelection = Navigation.SelectedItem;
        onboardingPreviousPaneState = Navigation.IsPaneOpen;
        onboardingPreviousPageStates =
        [
            DrawNavItem.IsEnabled,
            GraphNavItem.IsEnabled,
            MaskNavItem.IsEnabled,
            PhysicsNavItem.IsEnabled,
        ];

        DrawNavItem.IsEnabled = true;
        GraphNavItem.IsEnabled = true;
        MaskNavItem.IsEnabled = true;
        PhysicsNavItem.IsEnabled = true;
        Navigation.IsPaneOpen = true;
        OnboardingExitButton.Content = automatic ? "稍后再看" : "退出向导";
        OnboardingHighlightLayer.Visibility = Visibility.Visible;
        RootGrid.SizeChanged += OnboardingRoot_SizeChanged;

        await ShowOnboardingStepAsync(0);
    }

    private async Task ShowOnboardingStepAsync(int stepIndex)
    {
        if (!onboardingTourActive ||
            onboardingStepChanging ||
            stepIndex < 0 ||
            stepIndex >= onboardingTourSteps.Count)
        {
            return;
        }

        onboardingStepChanging = true;
        try
        {
            onboardingStepIndex = stepIndex;
            OnboardingTourStep step = onboardingTourSteps[stepIndex];
            bool tipWasOpen = OnboardingTip.IsOpen;
            if (tipWasOpen)
            {
                OnboardingTip.Target = RootGrid;
            }

            NavigateForOnboarding(step.ViewTag);

            await Task.Delay(80);
            step.Target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = false,
                HorizontalAlignmentRatio = 0.5,
                VerticalAlignmentRatio = 0.5,
            });
            await Task.Delay(100);

            OnboardingTip.PreferredPlacement =
                ReferenceEquals(step.Target, GuideNavItem)
                    ? TeachingTipPlacementMode.Top
                    : TeachingTipPlacementMode.Auto;
            onboardingCurrentTarget = step.Target;
            PositionOnboardingHighlight(step.Target);
            OnboardingTip.Target = step.TipTarget ?? step.Target;
            OnboardingTip.Title = step.Title;
            OnboardingTip.Subtitle = step.Description;
            OnboardingProgressText.Text =
                $"第 {stepIndex + 1} 步，共 {onboardingTourSteps.Count} 步";
            OnboardingPreviousButton.Visibility =
                stepIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
            OnboardingNextButton.Content =
                stepIndex == onboardingTourSteps.Count - 1 ? "完成向导" : "下一步";
            if (!tipWasOpen)
            {
                OnboardingTip.IsOpen = true;
            }
        }
        finally
        {
            onboardingStepChanging = false;
        }
    }

    private void NavigateForOnboarding(string viewTag)
    {
        NavigationViewItem targetItem = viewTag switch
        {
            "draws" => DrawNavItem,
            "graph" => GraphNavItem,
            "mask" => MaskNavItem,
            "physics" => PhysicsNavItem,
            "runtime" => RuntimeNavItem,
            _ => OverviewNavItem,
        };

        Navigation.SelectedItem = targetItem;
        ShowView(viewTag);
    }

    private void PositionOnboardingHighlight(FrameworkElement target)
    {
        if (!onboardingTourActive ||
            target.ActualWidth <= 0 ||
            target.ActualHeight <= 0 ||
            RootGrid.ActualWidth <= 0 ||
            RootGrid.ActualHeight <= 0)
        {
            OnboardingHighlightBorder.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            Point origin = target.TransformToVisual(RootGrid).TransformPoint(new Point(0, 0));
            const double padding = 6;
            double left = Math.Max(3, origin.X - padding);
            double top = Math.Max(3, origin.Y - padding);
            double width = Math.Min(target.ActualWidth + (padding * 2), RootGrid.ActualWidth - left - 3);
            double height = Math.Min(target.ActualHeight + (padding * 2), RootGrid.ActualHeight - top - 3);

            if (width <= 0 || height <= 0)
            {
                OnboardingHighlightBorder.Visibility = Visibility.Collapsed;
                return;
            }

            Canvas.SetLeft(OnboardingHighlightBorder, left);
            Canvas.SetTop(OnboardingHighlightBorder, top);
            OnboardingHighlightBorder.Width = width;
            OnboardingHighlightBorder.Height = height;
            OnboardingHighlightBorder.Visibility = Visibility.Visible;
        }
        catch (ArgumentException)
        {
            OnboardingHighlightBorder.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnboardingNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!onboardingTourActive || onboardingStepChanging)
        {
            return;
        }

        if (onboardingStepIndex < onboardingTourSteps.Count - 1)
        {
            await ShowOnboardingStepAsync(onboardingStepIndex + 1);
            return;
        }

        try
        {
            SaveOnboardingCompletion();
            EndOnboardingTour();
            ShowMessage(
                "界面导览已完成。以后可从左侧底部的“使用向导”重新查看。",
                InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EndOnboardingTour();
            ShowMessage($"导览已完成，但无法保存首次运行状态：{exception.Message}", InfoBarSeverity.Warning);
        }
    }

    private void OnboardingExitButton_Click(object sender, RoutedEventArgs e)
    {
        EndOnboardingTour();
    }

    private void OnboardingTip_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        if (onboardingTourActive && !onboardingStepChanging)
        {
            EndOnboardingTour();
        }
    }

    private async void OnboardingPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        if (onboardingTourActive && onboardingStepIndex > 0)
        {
            await ShowOnboardingStepAsync(onboardingStepIndex - 1);
        }
    }

    private void EndOnboardingTour()
    {
        if (!onboardingTourActive)
        {
            return;
        }

        onboardingTourActive = false;
        OnboardingTip.IsOpen = false;
        OnboardingTip.Target = null;
        onboardingCurrentTarget = null;
        OnboardingHighlightLayer.Visibility = Visibility.Collapsed;
        RootGrid.SizeChanged -= OnboardingRoot_SizeChanged;

        if (onboardingPreviousPageStates is { Length: 4 } previousStates)
        {
            DrawNavItem.IsEnabled = previousStates[0];
            GraphNavItem.IsEnabled = previousStates[1];
            MaskNavItem.IsEnabled = previousStates[2];
            PhysicsNavItem.IsEnabled = previousStates[3];
        }

        Navigation.IsPaneOpen = onboardingPreviousPaneState;
        object targetSelection = onboardingPreviousSelection ?? OverviewNavItem;
        Navigation.SelectedItem = targetSelection;
        if (targetSelection is NavigationViewItem item && item.Tag is string tag)
        {
            ShowView(tag);
        }
    }

    private void OnboardingRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (onboardingCurrentTarget is not null)
        {
            PositionOnboardingHighlight(onboardingCurrentTarget);
        }
    }

    private async void Navigation_ItemInvoked(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string tag &&
            string.Equals(tag, "guide", StringComparison.Ordinal))
        {
            await ShowOnboardingAsync(automatic: false);
        }
    }

    private sealed class OnboardingState
    {
        public int CompletedVersion { get; set; }

        public DateTimeOffset CompletedAtUtc { get; set; }
    }

    private sealed record OnboardingTourStep(
        string ViewTag,
        FrameworkElement Target,
        string Title,
        string Description,
        FrameworkElement? TipTarget = null);
}
