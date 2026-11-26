using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Pagination:
/// - Prev / Next navigation with smart ellipsis truncation for large page counts
/// - Gliding spring active page pill (<see cref="VelaMotionTokens.Springs.Morph"/>) isolated in a Canvas layer
/// - TwoWay <see cref="CurrentPage"/> binding and <see cref="PageChanged"/> event
/// </summary>
public class VelaPagination : TemplatedControl
{
    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<VelaPagination, int>(
            nameof(CurrentPage),
            1,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> TotalPagesProperty =
        AvaloniaProperty.Register<VelaPagination, int>(nameof(TotalPages), 1);

    public static readonly StyledProperty<int> MaxVisiblePagesProperty =
        AvaloniaProperty.Register<VelaPagination, int>(nameof(MaxVisiblePages), 5);

    public int CurrentPage { get => GetValue(CurrentPageProperty); set => SetValue(CurrentPageProperty, value); }
    public int TotalPages { get => GetValue(TotalPagesProperty); set => SetValue(TotalPagesProperty, value); }
    public int MaxVisiblePages { get => GetValue(MaxVisiblePagesProperty); set => SetValue(MaxVisiblePagesProperty, value); }

    public event EventHandler<int>? PageChanged;

    protected override Type StyleKeyOverride => typeof(VelaPagination);

    private readonly SpringChannel _pillX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _pillW = new(34.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private Button? _prevButton;
    private Button? _nextButton;
    private Grid? _track;
    private StackPanel? _pagesHost;
    private Border? _indicator;
    private TranslateTransform? _indicatorTranslate;

    private readonly Dictionary<int, Button> _pageButtons = new();
    private List<int> _renderedTokens = new(); // positive = page number, -1 / -2 = ellipsis
    private bool _hasPositioned;
    private int _positionedPage = -1;

    static VelaPagination()
    {
        CurrentPageProperty.Changed.AddClassHandler<VelaPagination>((p, _) => p.OnPagePropertyChanged());
        TotalPagesProperty.Changed.AddClassHandler<VelaPagination>((p, _) => p.RebuildPages(animate: false));
        MaxVisiblePagesProperty.Changed.AddClassHandler<VelaPagination>((p, _) => p.RebuildPages(animate: false));
    }

    public VelaPagination()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_prevButton is not null) _prevButton.Click -= OnPrevClick;
        if (_nextButton is not null) _nextButton.Click -= OnNextClick;
        if (_track is not null) _track.LayoutUpdated -= OnTrackLayoutUpdated;

        _prevButton = e.NameScope.Find<Button>("PART_Prev");
        _nextButton = e.NameScope.Find<Button>("PART_Next");
        _track = e.NameScope.Find<Grid>("PART_Track");
        _pagesHost = e.NameScope.Find<StackPanel>("PART_PagesHost");
        _indicator = e.NameScope.Find<Border>("PART_Indicator");

        if (_indicator is not null)
        {
            _indicatorTranslate = new TranslateTransform();
            _indicator.RenderTransform = _indicatorTranslate;
        }

        if (_prevButton is not null) _prevButton.Click += OnPrevClick;
        if (_nextButton is not null) _nextButton.Click += OnNextClick;
        if (_track is not null) _track.LayoutUpdated += OnTrackLayoutUpdated;

        _hasPositioned = false;
        _positionedPage = -1;
        RebuildPages(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private int SafeTotal => Math.Max(1, TotalPages);
    private int SafePage => Math.Clamp(CurrentPage, 1, SafeTotal);

    private void OnPrevClick(object? sender, RoutedEventArgs e)
    {
        SelectPage(SafePage - 1);
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        SelectPage(SafePage + 1);
    }

    private void SelectPage(int page)
    {
        var clamped = Math.Clamp(page, 1, SafeTotal);
        if (clamped == CurrentPage)
            return;

        CurrentPage = clamped;
        PageChanged?.Invoke(this, clamped);
    }

    private void OnPagePropertyChanged()
    {
        var desiredTokens = BuildPageTokens(SafePage, SafeTotal, Math.Max(3, MaxVisiblePages));
        if (!desiredTokens.SequenceEqual(_renderedTokens))
        {
            RebuildPages(animate: _hasPositioned);
        }
        else
        {
            UpdateButtonVisuals();
            UpdateNavButtons();
            MoveIndicator(animate: _hasPositioned);
        }
    }

    private void OnTrackLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_hasPositioned)
        {
            MoveIndicator(animate: false);
        }
        else if (_positionedPage == SafePage && !_runner.IsRunning)
        {
            MoveIndicator(animate: false);
        }
    }

    private void RebuildPages(bool animate)
    {
        if (_pagesHost is null)
            return;

        var total = SafeTotal;
        var current = SafePage;
        var tokens = BuildPageTokens(current, total, Math.Max(3, MaxVisiblePages));
        _renderedTokens = tokens;

        _pagesHost.Children.Clear();
        _pageButtons.Clear();

        var btnTheme = this.TryFindResource("VelaIconButtonTheme", ActualThemeVariant, out var res)
            ? res as ControlTheme
            : null;

        foreach (var token in tokens)
        {
            if (token < 0)
            {
                _pagesHost.Children.Add(new TextBlock
                {
                    Text = "…",
                    Width = 28,
                    Height = 34,
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(0, 8, 0, 0),
                    [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
                });
                continue;
            }

            var pageNum = token;
            var btn = new Button
            {
                Theme = btnTheme,
                MinWidth = 34,
                Height = 34,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(10),
                FontSize = 13.5,
                Cursor = new Cursor(StandardCursorType.Hand),
                Content = pageNum.ToString(),
            };
            btn.Click += (_, _) => SelectPage(pageNum);

            _pageButtons[pageNum] = btn;
            _pagesHost.Children.Add(btn);
        }

        UpdateButtonVisuals();
        UpdateNavButtons();

        if (animate)
        {
            Dispatcher.UIThread.Post(() => MoveIndicator(animate: true), DispatcherPriority.Loaded);
        }
        else
        {
            Dispatcher.UIThread.Post(() => MoveIndicator(animate: false), DispatcherPriority.Loaded);
        }
    }

    private void UpdateNavButtons()
    {
        if (_prevButton is not null) _prevButton.IsEnabled = SafePage > 1;
        if (_nextButton is not null) _nextButton.IsEnabled = SafePage < SafeTotal;
    }

    private void UpdateButtonVisuals()
    {
        var current = SafePage;
        foreach (var (page, btn) in _pageButtons)
        {
            var isCurrent = page == current;
            btn.FontWeight = isCurrent ? FontWeight.SemiBold : FontWeight.Normal;
            btn.Bind(
                TemplatedControl.ForegroundProperty,
                this.GetResourceObservable(isCurrent ? "VelaAccentForegroundBrush" : "VelaTextSecondaryBrush"));
        }
    }

    private void MoveIndicator(bool animate)
    {
        if (_track is null || _indicator is null)
            return;

        var current = SafePage;
        if (!_pageButtons.TryGetValue(current, out var activeBtn) || activeBtn.Bounds.Width <= 0)
            return;

        var pos = activeBtn.TranslatePoint(new Point(0, 0), _track);
        if (!pos.HasValue)
            return;

        var targetX = pos.Value.X;
        var targetW = activeBtn.Bounds.Width;

        _indicator.Height = activeBtn.Bounds.Height;
        _indicator.Opacity = 1;
        _positionedPage = current;

        if (!animate || !_hasPositioned || VelaMotionOptions.ReducedMotion)
        {
            _hasPositioned = true;
            _pillX.Jump(targetX);
            _pillW.Jump(targetW);
            ApplyIndicator();
            return;
        }

        _pillX.SetTarget(targetX, VelaMotionTokens.Springs.Morph);
        _pillW.SetTarget(targetW, VelaMotionTokens.Springs.Morph);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _pillX.Step(dt);
        active |= _pillW.Step(dt);
        ApplyIndicator();
        return active;
    }

    private void ApplyIndicator()
    {
        if (_indicator is null || _indicatorTranslate is null)
            return;

        _indicatorTranslate.X = _pillX.Value;
        _indicator.Width = Math.Max(0, _pillW.Value);
    }

    private static List<int> BuildPageTokens(int current, int total, int maxVisible)
    {
        var list = new List<int>();
        if (total <= maxVisible + 2)
        {
            for (var i = 1; i <= total; i++)
                list.Add(i);
            return list;
        }

        var inner = Math.Max(3, maxVisible - 2);
        var half = inner / 2;
        var start = Math.Max(2, current - half);
        var end = Math.Min(total - 1, start + inner - 1);
        start = Math.Max(2, end - inner + 1);

        list.Add(1);
        if (start > 2)
            list.Add(-1);
        for (var i = start; i <= end; i++)
            list.Add(i);
        if (end < total - 1)
            list.Add(-2);
        list.Add(total);

        return list;
    }
}
