using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaStepperOrientation
{
    Horizontal,
    Vertical,
}

public enum VelaStepStatus
{
    Upcoming,
    Current,
    Complete,
    Error,
}

/// <summary>
/// Declarative step definition inside <see cref="VelaStepper"/>.
/// </summary>
public class VelaStepperItem : AvaloniaObject
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<VelaStepperItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaStepperItem, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<VelaStepperItem, string?>(nameof(ErrorText));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaStepperItem, Geometry?>(nameof(Icon));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}

/// <summary>
/// Port of Arc UI's Stepper:
/// - Horizontal and vertical multi-step progress indicator / wizard header
/// - Spring-filling connector bars with sequential stagger across multi-step jumps
/// - Animated checkmark pop-in on completed steps, pulsing focus halo on current step, and error state
/// - Optional click navigation back to completed steps
/// </summary>
public class VelaStepper : TemplatedControl
{
    public static readonly StyledProperty<int> CurrentStepProperty =
        AvaloniaProperty.Register<VelaStepper, int>(
            nameof(CurrentStep),
            0,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<VelaStepperOrientation> OrientationProperty =
        AvaloniaProperty.Register<VelaStepper, VelaStepperOrientation>(
            nameof(Orientation),
            VelaStepperOrientation.Horizontal);

    public static readonly StyledProperty<bool> AllowStepNavigationProperty =
        AvaloniaProperty.Register<VelaStepper, bool>(nameof(AllowStepNavigation), true);

    public int CurrentStep { get => GetValue(CurrentStepProperty); set => SetValue(CurrentStepProperty, value); }
    public VelaStepperOrientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    public bool AllowStepNavigation { get => GetValue(AllowStepNavigationProperty); set => SetValue(AllowStepNavigationProperty, value); }

    [Content]
    public ObservableCollection<VelaStepperItem> Steps { get; } = new();

    public event EventHandler<int>? StepSelected;

    protected override Type StyleKeyOverride => typeof(VelaStepper);

    private sealed class StepVisual
    {
        public required Border Ring;
        public required ScaleTransform RingScale;
        public required Border Disc;
        public required TextBlock NumberText;
        public required PathIcon CheckIcon;
        public required ScaleTransform GlyphScale;
        public required TextBlock TitleText;
        public required TextBlock DescText;
        public required Border HitRoot;
        public ScaleTransform? ConnectorScale;

        public readonly SpringChannel RingSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
        public readonly SpringChannel GlyphSpring = new(1.0, VelaMotionTokens.Springs.Snappy);
        public readonly SpringChannel ConnectorSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
        public double ConnectorDelay;
        public double RingDelay;
        public double TargetConnector;
        public double TargetRing;
        public VelaStepStatus LastStatus;
    }

    private readonly List<StepVisual> _visuals = new();
    private readonly SpringRunner _runner;
    private Panel? _host;
    private int _previousStep;

    static VelaStepper()
    {
        CurrentStepProperty.Changed.AddClassHandler<VelaStepper>((s, e) =>
            s.OnCurrentStepChanged(e.OldValue is int o ? o : 0, e.NewValue is int n ? n : 0));
        OrientationProperty.Changed.AddClassHandler<VelaStepper>((s, _) => s.RebuildVisuals());
        AllowStepNavigationProperty.Changed.AddClassHandler<VelaStepper>((s, _) => s.UpdateStepStates(animate: false));
    }

    public VelaStepper()
    {
        _runner = new SpringRunner(this, OnTick);
        Steps.CollectionChanged += OnStepsCollectionChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        _previousStep = ClampStep(CurrentStep);
        RebuildVisuals();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnThemeOrAccentChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        UpdateStepStates(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaTheme.AccentChanged -= OnThemeOrAccentChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _runner.Stop();
    }

    private void OnThemeOrAccentChanged(object? sender, Tokens.VelaAccent accent) => UpdateStepStates(animate: false);
    private void OnThemeVariantChanged(object? sender, EventArgs e) => UpdateStepStates(animate: false);

    private void OnStepsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (VelaStepperItem item in e.OldItems)
                item.PropertyChanged -= OnStepItemPropertyChanged;
        }
        if (e.NewItems is not null)
        {
            foreach (VelaStepperItem item in e.NewItems)
                item.PropertyChanged += OnStepItemPropertyChanged;
        }
        RebuildVisuals();
    }

    private void OnStepItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        UpdateStepStates(animate: true);
    }

    private int ClampStep(int step) => Math.Clamp(step, 0, Steps.Count);

    private void OnCurrentStepChanged(int oldVal, int newVal)
    {
        _previousStep = Math.Clamp(oldVal, 0, Steps.Count);
        UpdateStepStates(animate: true);
    }

    private void RebuildVisuals()
    {
        if (_host is null)
            return;

        _host.Children.Clear();
        _visuals.Clear();

        var count = Steps.Count;
        if (count == 0)
            return;

        var checkGeom = ResolveGeometry("VelaIconCheck");
        var alertGeom = ResolveGeometry("VelaIconAlertCircle");

        if (Orientation == VelaStepperOrientation.Horizontal)
        {
            var grid = new Grid();
            for (var i = 0; i < count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(
                    i < count - 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto));
            }
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < count; i++)
            {
                var stepIndex = i;
                var visual = CreateStepVisual(stepIndex, isVertical: false, hasConnector: i < count - 1, checkGeom, alertGeom);
                _visuals.Add(visual);

                // Row 0: Marker + Connector rail
                var topRow = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Height = 36,
                };

                var markerPanel = BuildMarkerPanel(visual);
                topRow.Children.Add(markerPanel);

                if (i < count - 1)
                {
                    var track = new Border
                    {
                        Height = 2,
                        CornerRadius = new CornerRadius(1),
                        Margin = new Thickness(10, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        ClipToBounds = true,
                        [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
                    };

                    var fillScale = new ScaleTransform(0, 1);
                    var fill = new Border
                    {
                        CornerRadius = new CornerRadius(1),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                        RenderTransform = fillScale,
                        [!Border.BackgroundProperty] = this.GetResourceObservable("VelaAccentBrush").ToBinding(),
                    };
                    track.Child = fill;
                    visual.ConnectorScale = fillScale;

                    Grid.SetColumn(track, 1);
                    topRow.Children.Add(track);
                }

                Grid.SetColumn(topRow, i);
                Grid.SetRow(topRow, 0);
                grid.Children.Add(topRow);

                // Row 1: Title + Description
                var textStack = new StackPanel
                {
                    Spacing = 2,
                    Margin = new Thickness(2, 8, i < count - 1 ? 16 : 2, 0),
                    Children = { visual.TitleText, visual.DescText },
                };

                visual.HitRoot.Child = textStack;
                Grid.SetColumn(visual.HitRoot, i);
                Grid.SetRow(visual.HitRoot, 1);
                grid.Children.Add(visual.HitRoot);

                // Make marker clickable too
                markerPanel.PointerPressed += (_, ev) => HandleStepClick(stepIndex, ev);
                visual.HitRoot.PointerPressed += (_, ev) => HandleStepClick(stepIndex, ev);
            }

            _host.Children.Add(grid);
        }
        else
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 14,
            };

            for (var i = 0; i < count; i++)
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < count; i++)
            {
                var stepIndex = i;
                var visual = CreateStepVisual(stepIndex, isVertical: true, hasConnector: i < count - 1, checkGeom, alertGeom);
                _visuals.Add(visual);

                // Left rail: marker + vertical connector
                var rail = new Grid
                {
                    RowDefinitions = new RowDefinitions("Auto,*"),
                    Width = 36,
                };

                var markerPanel = BuildMarkerPanel(visual);
                rail.Children.Add(markerPanel);

                if (i < count - 1)
                {
                    var track = new Border
                    {
                        Width = 2,
                        MinHeight = 24,
                        CornerRadius = new CornerRadius(1),
                        Margin = new Thickness(0, 6, 0, 6),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ClipToBounds = true,
                        [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
                    };

                    var fillScale = new ScaleTransform(1, 0);
                    var fill = new Border
                    {
                        CornerRadius = new CornerRadius(1),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                        RenderTransform = fillScale,
                        [!Border.BackgroundProperty] = this.GetResourceObservable("VelaAccentBrush").ToBinding(),
                    };
                    track.Child = fill;
                    visual.ConnectorScale = fillScale;

                    Grid.SetRow(track, 1);
                    rail.Children.Add(track);
                }

                Grid.SetRow(rail, i);
                Grid.SetColumn(rail, 0);
                grid.Children.Add(rail);

                // Right column: title + description
                var textStack = new StackPanel
                {
                    Spacing = 2,
                    Margin = new Thickness(0, 6, 0, i < count - 1 ? 20 : 4),
                    Children = { visual.TitleText, visual.DescText },
                };

                visual.HitRoot.Child = textStack;
                Grid.SetRow(visual.HitRoot, i);
                Grid.SetColumn(visual.HitRoot, 1);
                grid.Children.Add(visual.HitRoot);

                markerPanel.PointerPressed += (_, ev) => HandleStepClick(stepIndex, ev);
                visual.HitRoot.PointerPressed += (_, ev) => HandleStepClick(stepIndex, ev);
            }

            _host.Children.Add(grid);
        }

        UpdateStepStates(animate: false);
    }

    private StepVisual CreateStepVisual(int index, bool isVertical, bool hasConnector, Geometry? checkGeom, Geometry? alertGeom)
    {
        var ringScale = new ScaleTransform(0.6, 0.6);
        var ring = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(999),
            Opacity = 0,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = ringScale,
            IsHitTestVisible = false,
        };

        var glyphScale = new ScaleTransform(1, 1);
        var numberText = new TextBlock
        {
            Text = (index + 1).ToString(),
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = glyphScale,
        };

        var checkIcon = new PathIcon
        {
            Width = 13,
            Height = 13,
            Data = checkGeom,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = glyphScale,
        };

        var disc = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Panel { Children = { numberText, checkIcon } },
        };

        var titleText = new TextBlock
        {
            FontSize = 13.5,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var descText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = isVertical ? double.PositiveInfinity : 200,
        };

        var hitRoot = new Border
        {
            Background = Brushes.Transparent,
        };

        return new StepVisual
        {
            Ring = ring,
            RingScale = ringScale,
            Disc = disc,
            NumberText = numberText,
            CheckIcon = checkIcon,
            GlyphScale = glyphScale,
            TitleText = titleText,
            DescText = descText,
            HitRoot = hitRoot,
        };
    }

    private static Panel BuildMarkerPanel(StepVisual visual) => new()
    {
        Width = 36,
        Height = 36,
        Background = Brushes.Transparent,
        Children = { visual.Ring, visual.Disc },
    };

    private void HandleStepClick(int index, PointerPressedEventArgs e)
    {
        if (!AllowStepNavigation || !IsEnabled)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var active = ClampStep(CurrentStep);
        if (index < active)
        {
            CurrentStep = index;
            StepSelected?.Invoke(this, index);
            e.Handled = true;
        }
    }

    private void UpdateStepStates(bool animate)
    {
        if (_visuals.Count != Steps.Count || _visuals.Count == 0)
            return;

        var active = ClampStep(CurrentStep);
        var from = _previousStep;
        var reduced = VelaMotionOptions.ReducedMotion || !animate;
        const double staggerGap = 0.065;

        var accent = ResolveBrush("VelaAccentBrush");
        var accentFg = ResolveBrush("VelaAccentForegroundBrush");
        var accentSubtle = ResolveBrush("VelaAccentSubtleBrush");
        var surface = ResolveBrush("VelaSurfaceBrush");
        var border = ResolveBrush("VelaBorderStrongBrush");
        var fg = ResolveBrush("VelaForegroundBrush");
        var secondary = ResolveBrush("VelaTextSecondaryBrush");
        var muted = ResolveBrush("VelaTextMutedBrush");
        var danger = ResolveBrush("VelaDangerBrush");
        var dangerSubtle = ResolveBrush("VelaDangerSubtleBrush");

        var checkGeom = ResolveGeometry("VelaIconCheck");
        var alertGeom = ResolveGeometry("VelaIconAlertCircle");

        var ringDelayBase = reduced ? 0.0 : Math.Max(0, Math.Abs(active - from) - 1) * staggerGap + 0.08;
        var needWake = false;

        for (var i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var v = _visuals[i];
            var hasError = !string.IsNullOrEmpty(step.ErrorText);
            var status = hasError
                ? VelaStepStatus.Error
                : i < active
                    ? VelaStepStatus.Complete
                    : i == active
                        ? VelaStepStatus.Current
                        : VelaStepStatus.Upcoming;

            v.TitleText.Text = step.Title ?? $"Step {i + 1}";
            var detail = hasError ? step.ErrorText : step.Description;
            v.DescText.Text = detail;
            v.DescText.IsVisible = !string.IsNullOrEmpty(detail);

            var clickable = AllowStepNavigation && i < active;
            var cursor = clickable ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
            v.HitRoot.Cursor = cursor;
            v.Disc.Cursor = cursor;

            switch (status)
            {
                case VelaStepStatus.Complete:
                    v.Disc.Background = accent;
                    v.Disc.BorderBrush = accent;
                    v.NumberText.IsVisible = false;
                    v.CheckIcon.Data = checkGeom;
                    v.CheckIcon.Foreground = accentFg;
                    v.CheckIcon.IsVisible = true;
                    v.TitleText.Foreground = fg;
                    v.TitleText.FontWeight = FontWeight.Medium;
                    v.DescText.Foreground = secondary;
                    v.Ring.Background = accentSubtle;
                    break;

                case VelaStepStatus.Current:
                    v.Disc.Background = accent;
                    v.Disc.BorderBrush = accent;
                    v.NumberText.Foreground = accentFg;
                    v.NumberText.IsVisible = step.Icon is null;
                    v.CheckIcon.Data = step.Icon;
                    v.CheckIcon.Foreground = accentFg;
                    v.CheckIcon.IsVisible = step.Icon is not null;
                    v.TitleText.Foreground = fg;
                    v.TitleText.FontWeight = FontWeight.SemiBold;
                    v.DescText.Foreground = secondary;
                    v.Ring.Background = accentSubtle;
                    break;

                case VelaStepStatus.Error:
                    v.Disc.Background = danger;
                    v.Disc.BorderBrush = danger;
                    v.NumberText.IsVisible = false;
                    v.CheckIcon.Data = alertGeom;
                    v.CheckIcon.Foreground = Brushes.White;
                    v.CheckIcon.IsVisible = true;
                    v.TitleText.Foreground = danger;
                    v.TitleText.FontWeight = FontWeight.SemiBold;
                    v.DescText.Foreground = danger;
                    v.Ring.Background = dangerSubtle;
                    break;

                default: // Upcoming
                    v.Disc.Background = surface;
                    v.Disc.BorderBrush = border;
                    v.NumberText.Foreground = muted;
                    v.NumberText.IsVisible = step.Icon is null;
                    v.CheckIcon.Data = step.Icon;
                    v.CheckIcon.Foreground = muted;
                    v.CheckIcon.IsVisible = step.Icon is not null;
                    v.TitleText.Foreground = muted;
                    v.TitleText.FontWeight = FontWeight.Medium;
                    v.DescText.Foreground = muted;
                    v.Ring.Background = accentSubtle;
                    break;
            }

            // Glyph pop when status changes
            if (animate && !reduced && v.LastStatus != status)
            {
                v.GlyphSpring.Jump(0.55);
                v.GlyphSpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
                needWake = true;
            }
            else if (reduced)
            {
                v.GlyphSpring.Jump(1.0);
                v.GlyphScale.ScaleX = 1.0;
                v.GlyphScale.ScaleY = 1.0;
            }
            v.LastStatus = status;

            // Active halo ring
            var targetRing = (status == VelaStepStatus.Current || status == VelaStepStatus.Error) ? 1.0 : 0.0;
            v.TargetRing = targetRing;
            if (reduced)
            {
                v.RingDelay = 0;
                v.RingSpring.Jump(targetRing);
                ApplyRing(v);
            }
            else
            {
                v.RingDelay = targetRing > 0.5 ? ringDelayBase : 0.0;
                if (v.RingDelay <= 0)
                    v.RingSpring.SetTarget(targetRing, VelaMotionTokens.Springs.Snappy);
                needWake = true;
            }

            // Connector fill
            if (v.ConnectorScale is not null)
            {
                var targetFill = i < active ? 1.0 : 0.0;
                v.TargetConnector = targetFill;
                if (reduced)
                {
                    v.ConnectorDelay = 0;
                    v.ConnectorSpring.Jump(targetFill);
                    ApplyConnector(v);
                }
                else
                {
                    double delay = 0;
                    if (active > from && i >= from && i < active)
                        delay = (i - from) * staggerGap;
                    else if (active < from && i >= active && i < from)
                        delay = (from - 1 - i) * staggerGap;

                    v.ConnectorDelay = delay;
                    if (delay <= 0)
                        v.ConnectorSpring.SetTarget(targetFill, VelaMotionTokens.Springs.Smooth);
                    needWake = true;
                }
            }
        }

        if (needWake)
            _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var anyActive = false;

        foreach (var v in _visuals)
        {
            if (v.RingDelay > 0)
            {
                v.RingDelay -= dt;
                anyActive = true;
                if (v.RingDelay <= 0)
                    v.RingSpring.SetTarget(v.TargetRing, VelaMotionTokens.Springs.Snappy);
            }
            anyActive |= v.RingSpring.Step(dt);
            ApplyRing(v);

            anyActive |= v.GlyphSpring.Step(dt);
            var gs = Math.Clamp(v.GlyphSpring.Value, 0.2, 1.4);
            v.GlyphScale.ScaleX = gs;
            v.GlyphScale.ScaleY = gs;

            if (v.ConnectorScale is not null)
            {
                if (v.ConnectorDelay > 0)
                {
                    v.ConnectorDelay -= dt;
                    anyActive = true;
                    if (v.ConnectorDelay <= 0)
                        v.ConnectorSpring.SetTarget(v.TargetConnector, VelaMotionTokens.Springs.Smooth);
                }
                anyActive |= v.ConnectorSpring.Step(dt);
                ApplyConnector(v);
            }
        }

        return anyActive;
    }

    private void ApplyRing(StepVisual v)
    {
        var r = Math.Clamp(v.RingSpring.Value, 0.0, 1.25);
        v.Ring.Opacity = Math.Clamp(r, 0.0, 1.0);
        var s = 0.62 + 0.38 * r;
        v.RingScale.ScaleX = s;
        v.RingScale.ScaleY = s;
    }

    private void ApplyConnector(StepVisual v)
    {
        if (v.ConnectorScale is null) return;
        var f = Math.Clamp(v.ConnectorSpring.Value, 0.0, 1.0);
        if (Orientation == VelaStepperOrientation.Horizontal)
        {
            v.ConnectorScale.ScaleX = f;
            v.ConnectorScale.ScaleY = 1.0;
        }
        else
        {
            v.ConnectorScale.ScaleX = 1.0;
            v.ConnectorScale.ScaleY = f;
        }
    }

    private IBrush ResolveBrush(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is IBrush b)
            return b;
        return Brushes.Gray;
    }

    private Geometry? ResolveGeometry(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is Geometry g)
            return g;
        return null;
    }
}
