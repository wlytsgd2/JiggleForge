using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace JiggleForge;

public sealed partial class MainWindow
{
    private const int CurrentOnboardingVersion = 3;

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
            L("TourWelcomeTitle"),
            L("TourWelcomeDescription")),
        new(
            "overview",
            DropCard,
            L("TourOpenProjectTitle"),
            L("TourOpenProjectDescription")),
        new(
            "runtime",
            RuntimePathTextBox,
            L("TourZzmiTitle"),
            L("TourZzmiDescription")),
        new(
            "runtime",
            InstallRuntimeButton,
            L("TourRuntimeTitle"),
            L("TourRuntimeDescription")),
        new(
            "runtime",
            DragKeyOptionsList,
            L("TourDragKeysTitle"),
            L("TourDragKeysDescription")),
        new(
            "runtime",
            StartWheelButton,
            L("TourWheelTitle"),
            L("TourWheelDescription"),
            RuntimeOverallStatusText),
        new(
            "draws",
            DrawGroupTree,
            L("TourDrawsTitle"),
            L("TourDrawsDescription")),
        new(
            "draws",
            InspectorToggleButton,
            L("TourInspectorTitle"),
            L("TourInspectorDescription")),
        new(
            "graph",
            GraphModeComboBox,
            L("TourGraphModeTitle"),
            L("TourGraphModeDescription")),
        new(
            "graph",
            EdgeEditorList,
            L("TourEdgesTitle"),
            L("TourEdgesDescription")),
        new(
            "mask",
            MaskEditorList,
            L("TourMaskTitle"),
            L("TourMaskDescription")),
        new(
            "physics",
            PhysicsScopeComboBox,
            L("TourPhysicsScopeTitle"),
            L("TourPhysicsScopeDescription")),
        new(
            "physics",
            PhysicsFieldsGrid,
            L("TourPhysicsTitle"),
            L("TourPhysicsDescription")),
        new(
            "overview",
            GuideNavItem,
            L("TourReplayTitle"),
            L("TourReplayDescription")),
        new(
            "runtime",
            CommunityCard,
            L("TourCommunityTitle"),
            L("TourCommunityDescription")),
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
        OnboardingExitButton.Content = automatic ? L("TourLater") : L("TourExit");
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
                AppLanguageService.Format("TourProgress", stepIndex + 1, onboardingTourSteps.Count);
            OnboardingPreviousButton.Visibility =
                stepIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
            OnboardingNextButton.Content =
                stepIndex == onboardingTourSteps.Count - 1 ? L("TourFinish") : L("TourNext");
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
            ShowMessage(L("TourCompleted"), InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EndOnboardingTour();
            ShowMessage(
                AppLanguageService.Format("TourSaveFailed", AppLanguageService.LocalizeException(exception)),
                InfoBarSeverity.Warning);
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
