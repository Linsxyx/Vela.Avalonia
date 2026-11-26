using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
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
/// Port of Arc UI's Calendar:
/// - Fixed 6x7 month grid (zero height jump across months)
/// - Single gliding circular selection disc that springs smoothly between dates
/// - Subtle today dot indicator + Prev / Today / Next navigation controls
/// </summary>
public class VelaCalendar : TemplatedControl
{
    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<VelaCalendar, DateTime?>(nameof(SelectedDate), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<DateTime> DisplayMonthProperty =
        AvaloniaProperty.Register<VelaCalendar, DateTime>(nameof(DisplayMonth), new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

    public DateTime? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public DateTime DisplayMonth { get => GetValue(DisplayMonthProperty); set => SetValue(DisplayMonthProperty, value); }

    public event EventHandler<DateTime>? DateSelected;

    protected override Type StyleKeyOverride => typeof(VelaCalendar);

    private readonly SpringChannel _discX = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _discY = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringChannel _discOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private TextBlock? _monthTitle;
    private Button? _prevButton;
    private Button? _todayButton;
    private Button? _nextButton;
    private Grid? _daysContainer;
    private UniformGrid? _daysGrid;
    private Border? _selectionDisc;
    private TranslateTransform? _discTranslate;

    private readonly List<(DateTime Date, Button Button, TextBlock Number, Ellipse TodayDot)> _cells = new();
    private bool _discPlaced;

    static VelaCalendar()
    {
        SelectedDateProperty.Changed.AddClassHandler<VelaCalendar>((c, _) => c.OnSelectedDateChanged());
        DisplayMonthProperty.Changed.AddClassHandler<VelaCalendar>((c, _) => c.RebuildMonth(animateDisc: false));
    }

    public VelaCalendar()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_prevButton is not null) _prevButton.Click -= OnPrevClick;
        if (_todayButton is not null) _todayButton.Click -= OnTodayClick;
        if (_nextButton is not null) _nextButton.Click -= OnNextClick;
        if (_daysContainer is not null) _daysContainer.LayoutUpdated -= OnGridLayoutUpdated;

        _monthTitle = e.NameScope.Find<TextBlock>("PART_MonthTitle");
        _prevButton = e.NameScope.Find<Button>("PART_PrevButton");
        _todayButton = e.NameScope.Find<Button>("PART_TodayButton");
        _nextButton = e.NameScope.Find<Button>("PART_NextButton");
        _daysContainer = e.NameScope.Find<Grid>("PART_DaysContainer");
        _daysGrid = e.NameScope.Find<UniformGrid>("PART_DaysGrid");
        _selectionDisc = e.NameScope.Find<Border>("PART_SelectionDisc");

        if (_selectionDisc is not null)
        {
            _discTranslate = new TranslateTransform();
            _selectionDisc.RenderTransform = _discTranslate;
            _selectionDisc.Opacity = 0;
        }

        if (_prevButton is not null) _prevButton.Click += OnPrevClick;
        if (_todayButton is not null) _todayButton.Click += OnTodayClick;
        if (_nextButton is not null) _nextButton.Click += OnNextClick;
        if (_daysContainer is not null) _daysContainer.LayoutUpdated += OnGridLayoutUpdated;

        if (SelectedDate.HasValue)
            DisplayMonth = new DateTime(SelectedDate.Value.Year, SelectedDate.Value.Month, 1);

        RebuildMonth(animateDisc: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnPrevClick(object? sender, RoutedEventArgs e)
        => DisplayMonth = DisplayMonth.AddMonths(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e)
        => DisplayMonth = DisplayMonth.AddMonths(1);

    private void OnTodayClick(object? sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        DisplayMonth = new DateTime(today.Year, today.Month, 1);
        SelectedDate = today;
        DateSelected?.Invoke(this, today);
    }

    private void OnSelectedDateChanged()
    {
        if (SelectedDate.HasValue)
        {
            var sel = SelectedDate.Value.Date;
            if (sel.Year != DisplayMonth.Year || sel.Month != DisplayMonth.Month)
            {
                DisplayMonth = new DateTime(sel.Year, sel.Month, 1);
                return;
            }
        }

        SyncCellStyles();
        UpdateSelectionDisc(animate: true);
    }

    private void OnGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_discPlaced)
            UpdateSelectionDisc(animate: false);
    }

    private void RebuildMonth(bool animateDisc)
    {
        if (_daysGrid is null) return;

        var monthStart = new DateTime(DisplayMonth.Year, DisplayMonth.Month, 1);
        if (_monthTitle is not null)
            _monthTitle.Text = monthStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        _daysGrid.Children.Clear();
        _cells.Clear();
        _discPlaced = false;

        // Start grid on Sunday (0 = Sunday .. 6 = Saturday)
        var offset = (int)monthStart.DayOfWeek;
        var gridStart = monthStart.AddDays(-offset);
        var today = DateTime.Today;

        for (var i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var isOutside = date.Month != monthStart.Month;
            var isToday = date == today;

            var num = new TextBlock
            {
                Text = date.Day.ToString(CultureInfo.InvariantCulture),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var dot = new Ellipse
            {
                Width = 4,
                Height = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 5),
                IsVisible = isToday,
            };

            var content = new Grid { Children = { num, dot } };
            var btn = new Button
            {
                Width = 38,
                Height = 38,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(999),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = content,
                Opacity = isOutside ? 0.45 : 1.0,
            };
            if (this.TryFindResource("VelaIconButtonTheme", ActualThemeVariant, out var res) && res is ControlTheme ct)
                btn.Theme = ct;

            var capturedDate = date;
            btn.Click += (_, _) =>
            {
                SelectedDate = capturedDate;
                DateSelected?.Invoke(this, capturedDate);
            };

            _cells.Add((date, btn, num, dot));
            _daysGrid.Children.Add(btn);
        }

        SyncCellStyles();
        Dispatcher.UIThread.Post(() => UpdateSelectionDisc(animate: animateDisc), DispatcherPriority.Loaded);
    }

    private void SyncCellStyles()
    {
        var sel = SelectedDate?.Date;
        var fg = ResolveBrush("VelaForegroundBrush", Brushes.Black);
        var accentFg = ResolveBrush("VelaAccentForegroundBrush", Brushes.White);

        foreach (var (date, _, num, dot) in _cells)
        {
            var isSelected = sel.HasValue && date == sel.Value;
            num.Foreground = isSelected ? accentFg : fg;
            num.FontWeight = isSelected || date == DateTime.Today ? FontWeight.SemiBold : FontWeight.Normal;
            dot.Fill = isSelected ? accentFg : fg;
        }
    }

    private void UpdateSelectionDisc(bool animate)
    {
        if (_daysContainer is null || _selectionDisc is null) return;
        var sel = SelectedDate?.Date;
        if (!sel.HasValue)
        {
            _discOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
            return;
        }

        var match = _cells.FirstOrDefault(c => c.Date == sel.Value);
        if (match.Button is null || match.Button.Bounds.Width <= 0)
        {
            _discOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
            return;
        }

        var origin = match.Button.TranslatePoint(new Point(0, 0), _daysContainer);
        if (!origin.HasValue) return;

        _selectionDisc.Width = match.Button.Bounds.Width;
        _selectionDisc.Height = match.Button.Bounds.Height;

        var shouldAnimate = _discPlaced && animate && !VelaMotionOptions.ReducedMotion;
        _discPlaced = true;

        if (!shouldAnimate)
        {
            _discX.Jump(origin.Value.X);
            _discY.Jump(origin.Value.Y);
            _discOpacity.Jump(1.0);
            ApplyDisc();
            return;
        }

        _discX.SetTarget(origin.Value.X, VelaMotionTokens.Springs.Morph);
        _discY.SetTarget(origin.Value.Y, VelaMotionTokens.Springs.Morph);
        _discOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _discX.Step(dt) | _discY.Step(dt) | _discOpacity.Step(dt);
        ApplyDisc();
        return active;
    }

    private void ApplyDisc()
    {
        if (_selectionDisc is null || _discTranslate is null) return;
        _discTranslate.X = _discX.Value;
        _discTranslate.Y = _discY.Value;
        _selectionDisc.Opacity = Math.Clamp(_discOpacity.Value, 0.0, 1.0);
    }

    private IBrush ResolveBrush(string key, IBrush fallback)
        => this.TryFindResource(key, ActualThemeVariant, out var r) && r is IBrush b ? b : fallback;
}
