using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaColorFormat
{
    Hex,
    Rgb,
    Hsl,
    Oklch,
}

internal enum VelaColorBarMode
{
    Hue,
    Alpha,
}

/// <summary>
/// 2D Saturation × Brightness spectrum surface with spring-chasing thumb for <see cref="VelaColorPicker"/>.
/// </summary>
public sealed class VelaColorSpectrumCanvas : Control
{
    internal double Hue { get; set; } = 220;
    internal double Saturation { get; set; } = 0.8;
    internal double Value { get; set; } = 1.0;

    internal event Action<double, double>? Picked;

    private readonly SpringChannel _sx = new(0.8, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _sy = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private bool _dragging;

    public VelaColorSpectrumCanvas()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = false;
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    internal void SyncFromModel(double h, double s, double v, bool animate)
    {
        Hue = h;
        Saturation = s;
        Value = v;

        if (!animate || _dragging || VelaMotionOptions.ReducedMotion)
        {
            _sx.Jump(s);
            _sy.Jump(1.0 - v);
            InvalidateVisual();
            return;
        }

        _sx.SetTarget(s, VelaMotionTokens.Springs.Snappy);
        _sy.SetTarget(1.0 - v, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        InvalidateVisual();
    }

    private bool OnTick(double dt)
    {
        var active = _sx.Step(dt);
        active |= _sy.Step(dt);
        InvalidateVisual();
        return active;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        UpdateFromPoint(e.GetPosition(this), instant: false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        UpdateFromPoint(e.GetPosition(this), instant: true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        InvalidateVisual();
    }

    private void UpdateFromPoint(Point p, bool instant)
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1) return;
        var s = Math.Clamp(p.X / Bounds.Width, 0.0, 1.0);
        var v = 1.0 - Math.Clamp(p.Y / Bounds.Height, 0.0, 1.0);

        if (instant || VelaMotionOptions.ReducedMotion)
        {
            _sx.Jump(s);
            _sy.Jump(1.0 - v);
        }
        else
        {
            _sx.SetTarget(s, VelaMotionTokens.Springs.Snappy);
            _sy.SetTarget(1.0 - v, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        Picked?.Invoke(s, v);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 2 || h <= 2) return;

        var rect = new Rect(0, 0, w, h);
        var radius = 10.0;
        var rrect = new RoundedRect(rect, radius);

        var pureHue = VelaColorMath.HsvToColor(Hue, 1.0, 1.0, 1.0);

        // Horizontal: white -> pure hue
        var satBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.White, 0),
                new GradientStop(pureHue, 1),
            },
        };

        // Vertical: transparent -> black
        var valBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                new GradientStop(Colors.Black, 1),
            },
        };

        using (context.PushClip(rrect))
        {
            context.DrawRectangle(satBrush, null, rect);
            context.DrawRectangle(valBrush, null, rect);
        }

        // Subtle crisp border
        var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 1);
        context.DrawRectangle(null, borderPen, rect, radius, radius);

        // Thumb indicator
        var tx = Math.Clamp(_sx.Value, 0.0, 1.0) * w;
        var ty = Math.Clamp(_sy.Value, 0.0, 1.0) * h;
        var thumbR = _dragging ? 8.5 : 7.5;
        var currentColor = VelaColorMath.HsvToColor(Hue, Saturation, Value, 1.0);

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, new Point(tx, ty + 1.5), thumbR + 1.5, thumbR + 1.5);
        context.DrawEllipse(new SolidColorBrush(currentColor), new Pen(Brushes.White, 2.4), new Point(tx, ty), thumbR, thumbR);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)), 0.75), new Point(tx, ty), thumbR + 1.2, thumbR + 1.2);
    }
}

/// <summary>
/// Rainbow Hue or checkerboard Alpha slider bar for <see cref="VelaColorPicker"/>.
/// </summary>
public sealed class VelaColorBarCanvas : Control
{
    internal VelaColorBarMode Mode { get; set; } = VelaColorBarMode.Hue;
    internal double Fraction { get; set; }
    internal Color ActiveColor { get; set; } = Colors.DodgerBlue;

    internal event Action<double>? Picked;

    private readonly SpringChannel _fx = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private bool _dragging;

    public VelaColorBarCanvas()
    {
        Height = 16;
        Cursor = new Cursor(StandardCursorType.Hand);
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    internal void Sync(double fraction, Color activeColor, bool animate)
    {
        Fraction = Math.Clamp(fraction, 0.0, 1.0);
        ActiveColor = activeColor;
        if (!animate || _dragging || VelaMotionOptions.ReducedMotion)
        {
            _fx.Jump(Fraction);
            InvalidateVisual();
            return;
        }
        _fx.SetTarget(Fraction, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        InvalidateVisual();
    }

    private bool OnTick(double dt)
    {
        var active = _fx.Step(dt);
        InvalidateVisual();
        return active;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        UpdateFromX(e.GetPosition(this).X, instant: false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging) return;
        UpdateFromX(e.GetPosition(this).X, instant: true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
        InvalidateVisual();
    }

    private void UpdateFromX(double x, bool instant)
    {
        if (Bounds.Width <= 1) return;
        var f = Math.Clamp(x / Bounds.Width, 0.0, 1.0);
        if (instant || VelaMotionOptions.ReducedMotion)
            _fx.Jump(f);
        else
        {
            _fx.SetTarget(f, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }
        Picked?.Invoke(f);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 4 || h <= 4) return;

        var barH = 12.0;
        var barY = (h - barH) * 0.5;
        var rect = new Rect(0, barY, w, barH);
        var r = barH * 0.5;
        var rrect = new RoundedRect(rect, r);

        using (context.PushClip(rrect))
        {
            if (Mode == VelaColorBarMode.Hue)
            {
                var hueBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(255, 0, 0), 0.0),
                        new GradientStop(Color.FromRgb(255, 255, 0), 1.0 / 6.0),
                        new GradientStop(Color.FromRgb(0, 255, 0), 2.0 / 6.0),
                        new GradientStop(Color.FromRgb(0, 255, 255), 3.0 / 6.0),
                        new GradientStop(Color.FromRgb(0, 0, 255), 4.0 / 6.0),
                        new GradientStop(Color.FromRgb(255, 0, 255), 5.0 / 6.0),
                        new GradientStop(Color.FromRgb(255, 0, 0), 1.0),
                    },
                };
                context.DrawRectangle(hueBrush, null, rect);
            }
            else
            {
                // Crisp subtle checkerboard behind alpha gradient
                var cell = 6.0;
                var light = new SolidColorBrush(Color.FromRgb(235, 235, 238));
                var dark = new SolidColorBrush(Color.FromRgb(198, 198, 204));
                context.DrawRectangle(light, null, rect);
                for (double cx = 0; cx < w; cx += cell)
                {
                    for (double cy = barY; cy < barY + barH; cy += cell)
                    {
                        var odd = (((int)(cx / cell) + (int)((cy - barY) / cell)) & 1) == 1;
                        if (odd)
                            context.DrawRectangle(dark, null, new Rect(cx, cy, cell, cell));
                    }
                }

                var opaque = Color.FromRgb(ActiveColor.R, ActiveColor.G, ActiveColor.B);
                var alphaBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0, opaque.R, opaque.G, opaque.B), 0),
                        new GradientStop(opaque, 1),
                    },
                };
                context.DrawRectangle(alphaBrush, null, rect);
            }
        }

        var thumbR = _dragging ? 8.0 : 7.0;
        var tx = Math.Clamp(_fx.Value * w, thumbR, Math.Max(thumbR, w - thumbR));
        var ty = h * 0.5;
        var fillColor = Mode == VelaColorBarMode.Hue
            ? VelaColorMath.HsvToColor(Fraction * 360.0, 1.0, 1.0, 1.0)
            : ActiveColor;

        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(65, 0, 0, 0)), null, new Point(tx, ty + 1.2), thumbR + 1.2, thumbR + 1.2);
        context.DrawEllipse(new SolidColorBrush(fillColor), new Pen(Brushes.White, 2.2), new Point(tx, ty), thumbR, thumbR);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 0.75), new Point(tx, ty), thumbR + 1.1, thumbR + 1.1);
    }
}

/// <summary>
/// Port of Arc UI's ColorPicker:
/// - 2D HSV spectrum area + rainbow Hue slider + checkerboard Alpha slider with spring-chasing thumbs
/// - Format cycler (Hex / RGB / HSL / OKLCH) with live editable Fluent-isolated input field
/// - Real-time WCAG 2.1 contrast ratio &amp; grade badge (AAA / AA / AA Large / Fail)
/// - Saved swatch palette with one-click pin (`+`)
/// - Supports both compact popover trigger (`IsInline=False`) and embedded studio panel (`IsInline=True`)
/// </summary>
[PseudoClasses(":open", ":inline")]
public class VelaColorPicker : TemplatedControl
{
    public static readonly StyledProperty<Color> SelectedColorProperty =
        AvaloniaProperty.Register<VelaColorPicker, Color>(
            nameof(SelectedColor),
            Color.FromRgb(47, 107, 255),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VelaColorPicker, string>(nameof(Label), "Color");

    public static readonly StyledProperty<bool> IsInlineProperty =
        AvaloniaProperty.Register<VelaColorPicker, bool>(nameof(IsInline), false);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<VelaColorPicker, bool>(
            nameof(IsOpen),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowAlphaProperty =
        AvaloniaProperty.Register<VelaColorPicker, bool>(nameof(ShowAlpha), true);

    public static readonly StyledProperty<bool> ShowContrastProperty =
        AvaloniaProperty.Register<VelaColorPicker, bool>(nameof(ShowContrast), true);

    public static readonly StyledProperty<Color?> ContrastBackgroundProperty =
        AvaloniaProperty.Register<VelaColorPicker, Color?>(nameof(ContrastBackground));

    public static readonly StyledProperty<VelaColorFormat> FormatProperty =
        AvaloniaProperty.Register<VelaColorPicker, VelaColorFormat>(nameof(Format), VelaColorFormat.Hex);

    public static readonly DirectProperty<VelaColorPicker, string> HexTextProperty =
        AvaloniaProperty.RegisterDirect<VelaColorPicker, string>(nameof(HexText), o => o.HexText);

    public static readonly DirectProperty<VelaColorPicker, IBrush> PreviewBrushProperty =
        AvaloniaProperty.RegisterDirect<VelaColorPicker, IBrush>(nameof(PreviewBrush), o => o.PreviewBrush);

    public static readonly DirectProperty<VelaColorPicker, string> FormatLabelProperty =
        AvaloniaProperty.RegisterDirect<VelaColorPicker, string>(nameof(FormatLabel), o => o.FormatLabel);

    public static readonly DirectProperty<VelaColorPicker, string> ContrastRatioTextProperty =
        AvaloniaProperty.RegisterDirect<VelaColorPicker, string>(nameof(ContrastRatioText), o => o.ContrastRatioText);

    public static readonly DirectProperty<VelaColorPicker, string> ContrastGradeProperty =
        AvaloniaProperty.RegisterDirect<VelaColorPicker, string>(nameof(ContrastGrade), o => o.ContrastGrade);

    private string _hexText = "#2F6BFF";
    private IBrush _previewBrush = new SolidColorBrush(Color.FromRgb(47, 107, 255));
    private string _formatLabel = "Hex";
    private string _contrastRatioText = "4.68:1";
    private string _contrastGrade = "AA";

    public Color SelectedColor { get => GetValue(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsInline { get => GetValue(IsInlineProperty); set => SetValue(IsInlineProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool ShowAlpha { get => GetValue(ShowAlphaProperty); set => SetValue(ShowAlphaProperty, value); }
    public bool ShowContrast { get => GetValue(ShowContrastProperty); set => SetValue(ShowContrastProperty, value); }
    public Color? ContrastBackground { get => GetValue(ContrastBackgroundProperty); set => SetValue(ContrastBackgroundProperty, value); }
    public VelaColorFormat Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }

    public string HexText { get => _hexText; private set => SetAndRaise(HexTextProperty, ref _hexText, value); }
    public IBrush PreviewBrush { get => _previewBrush; private set => SetAndRaise(PreviewBrushProperty, ref _previewBrush, value); }
    public string FormatLabel { get => _formatLabel; private set => SetAndRaise(FormatLabelProperty, ref _formatLabel, value); }
    public string ContrastRatioText { get => _contrastRatioText; private set => SetAndRaise(ContrastRatioTextProperty, ref _contrastRatioText, value); }
    public string ContrastGrade { get => _contrastGrade; private set => SetAndRaise(ContrastGradeProperty, ref _contrastGrade, value); }

    public ObservableCollection<Color> Swatches { get; } = new()
    {
        Color.FromRgb(47, 107, 255),
        Color.FromRgb(104, 80, 185),
        Color.FromRgb(0, 119, 77),
        Color.FromRgb(187, 44, 84),
        Color.FromRgb(217, 119, 6),
        Color.FromRgb(17, 24, 39),
    };

    public event EventHandler<Color>? ColorChanged;

    protected override Type StyleKeyOverride => typeof(VelaColorPicker);

    private double _h = 220.0;
    private double _s = 0.815;
    private double _v = 1.0;
    private double _a = 1.0;
    private bool _updatingInternal;

    private Button? _triggerButton;
    private VelaColorSpectrumCanvas? _spectrum;
    private VelaColorBarCanvas? _hueBar;
    private VelaColorBarCanvas? _alphaBar;
    private Button? _formatButton;
    private TextBox? _valueInput;
    private Button? _addSwatchButton;
    private StackPanel? _swatchesHost;
    private Border? _gradePill;

    static VelaColorPicker()
    {
        SelectedColorProperty.Changed.AddClassHandler<VelaColorPicker>((p, _) => p.OnSelectedColorChanged());
        IsInlineProperty.Changed.AddClassHandler<VelaColorPicker>((p, _) => p.UpdatePseudoClasses());
        IsOpenProperty.Changed.AddClassHandler<VelaColorPicker>((p, _) => p.UpdatePseudoClasses());
        FormatProperty.Changed.AddClassHandler<VelaColorPicker>((p, _) => p.SyncOutputs());
        ContrastBackgroundProperty.Changed.AddClassHandler<VelaColorPicker>((p, _) => p.SyncOutputs());
    }

    public VelaColorPicker()
    {
        Swatches.CollectionChanged += OnSwatchesChanged;
        VelaColorMath.ColorToHsv(SelectedColor, _h, out _h, out _s, out _v, out _a);
        SyncOutputs();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachTemplateHandlers();

        _triggerButton = e.NameScope.Find<Button>("PART_Trigger");
        _spectrum = e.NameScope.Find<VelaColorSpectrumCanvas>("PART_Spectrum");
        _hueBar = e.NameScope.Find<VelaColorBarCanvas>("PART_HueBar");
        _alphaBar = e.NameScope.Find<VelaColorBarCanvas>("PART_AlphaBar");
        _formatButton = e.NameScope.Find<Button>("PART_FormatButton");
        _valueInput = e.NameScope.Find<TextBox>("PART_ValueInput");
        _addSwatchButton = e.NameScope.Find<Button>("PART_AddSwatch");
        _swatchesHost = e.NameScope.Find<StackPanel>("PART_SwatchesHost");
        _gradePill = e.NameScope.Find<Border>("PART_GradePill");

        if (_triggerButton is not null) _triggerButton.Click += OnTriggerClick;
        if (_spectrum is not null) _spectrum.Picked += OnSpectrumPicked;
        if (_hueBar is not null)
        {
            _hueBar.Mode = VelaColorBarMode.Hue;
            _hueBar.Picked += OnHuePicked;
        }
        if (_alphaBar is not null)
        {
            _alphaBar.Mode = VelaColorBarMode.Alpha;
            _alphaBar.Picked += OnAlphaPicked;
        }
        if (_formatButton is not null) _formatButton.Click += OnFormatCycleClick;
        if (_valueInput is not null)
        {
            _valueInput.KeyDown += OnValueInputKeyDown;
            _valueInput.LostFocus += OnValueInputLostFocus;
        }
        if (_addSwatchButton is not null) _addSwatchButton.Click += OnAddSwatchClick;

        UpdatePseudoClasses();
        SyncVisualControls(animate: false);
        SyncOutputs();
        RebuildSwatches();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeVariantChanged;
        SyncOutputs();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeVariantChanged;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e) => SyncOutputs();

    private void DetachTemplateHandlers()
    {
        if (_triggerButton is not null) _triggerButton.Click -= OnTriggerClick;
        if (_spectrum is not null) _spectrum.Picked -= OnSpectrumPicked;
        if (_hueBar is not null) _hueBar.Picked -= OnHuePicked;
        if (_alphaBar is not null) _alphaBar.Picked -= OnAlphaPicked;
        if (_formatButton is not null) _formatButton.Click -= OnFormatCycleClick;
        if (_valueInput is not null)
        {
            _valueInput.KeyDown -= OnValueInputKeyDown;
            _valueInput.LostFocus -= OnValueInputLostFocus;
        }
        if (_addSwatchButton is not null) _addSwatchButton.Click -= OnAddSwatchClick;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":open", IsOpen);
        PseudoClasses.Set(":inline", IsInline);
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e) => IsOpen = !IsOpen;

    private void OnSpectrumPicked(double s, double v)
    {
        _s = s;
        _v = v;
        CommitHsv();
    }

    private void OnHuePicked(double f)
    {
        _h = Math.Clamp(f * 359.99, 0.0, 359.99);
        CommitHsv();
    }

    private void OnAlphaPicked(double f)
    {
        _a = Math.Clamp(f, 0.0, 1.0);
        CommitHsv();
    }

    private void OnFormatCycleClick(object? sender, RoutedEventArgs e)
    {
        Format = Format switch
        {
            VelaColorFormat.Hex => VelaColorFormat.Rgb,
            VelaColorFormat.Rgb => VelaColorFormat.Hsl,
            VelaColorFormat.Hsl => VelaColorFormat.Oklch,
            _ => VelaColorFormat.Hex,
        };
    }

    private void OnValueInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TryCommitInputText();
            e.Handled = true;
        }
    }

    private void OnValueInputLostFocus(object? sender, FocusChangedEventArgs e) => TryCommitInputText();

    private void TryCommitInputText()
    {
        if (_valueInput is null) return;
        if (VelaColorMath.TryParse(_valueInput.Text, out var parsed))
        {
            VelaColorMath.ColorToHsv(parsed, _h, out _h, out _s, out _v, out _a);
            CommitHsv(animateVisuals: true);
        }
        else
        {
            SyncOutputs();
        }
    }

    private void OnAddSwatchClick(object? sender, RoutedEventArgs e)
    {
        var current = SelectedColor;
        if (Swatches.Count > 0 && Swatches[0] == current)
            return;

        Swatches.Insert(0, current);
        while (Swatches.Count > 8)
            Swatches.RemoveAt(Swatches.Count - 1);
    }

    private void OnSwatchesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildSwatches();

    private void OnSelectedColorChanged()
    {
        if (_updatingInternal)
            return;

        VelaColorMath.ColorToHsv(SelectedColor, _h, out _h, out _s, out _v, out _a);
        SyncVisualControls(animate: true);
        SyncOutputs();
        RebuildSwatches();
    }

    private void CommitHsv(bool animateVisuals = false)
    {
        var next = VelaColorMath.HsvToColor(_h, _s, _v, _a);
        _updatingInternal = true;
        try
        {
            SetCurrentValue(SelectedColorProperty, next);
        }
        finally
        {
            _updatingInternal = false;
        }

        SyncVisualControls(animate: animateVisuals);
        SyncOutputs();
        RebuildSwatches();
        ColorChanged?.Invoke(this, next);
    }

    private void SyncVisualControls(bool animate)
    {
        var current = VelaColorMath.HsvToColor(_h, _s, _v, _a);
        _spectrum?.SyncFromModel(_h, _s, _v, animate);
        _hueBar?.Sync(_h / 360.0, current, animate);
        _alphaBar?.Sync(_a, current, animate);
    }

    private void SyncOutputs()
    {
        var c = SelectedColor;
        HexText = VelaColorMath.ToHex(c);
        PreviewBrush = new SolidColorBrush(c);
        FormatLabel = Format switch
        {
            VelaColorFormat.Rgb => "RGB",
            VelaColorFormat.Hsl => "HSL",
            VelaColorFormat.Oklch => "OKLCH",
            _ => "Hex",
        };

        if (_valueInput is not null && !_valueInput.IsFocused)
            _valueInput.Text = VelaColorMath.Format(c, _h, Format);

        var bg = ContrastBackground ?? ResolveSurfaceColor();
        var ratio = VelaColorMath.ContrastRatio(c, bg);
        ContrastRatioText = $"{ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1";
        ContrastGrade = ratio >= 7.0 ? "AAA" : ratio >= 4.5 ? "AA" : ratio >= 3.0 ? "AA Large" : "Fail";

        if (_gradePill is not null)
        {
            var bgKey = ratio >= 4.5 ? "VelaSuccessSubtleBrush" : ratio >= 3.0 ? "VelaWarningSubtleBrush" : "VelaDangerSubtleBrush";
            var fgKey = ratio >= 4.5 ? "VelaSuccessBrush" : ratio >= 3.0 ? "VelaWarningBrush" : "VelaDangerBrush";
            if (this.TryFindResource(bgKey, ActualThemeVariant, out var bRes) && bRes is IBrush bBrush)
                _gradePill.Background = bBrush;
            if (_gradePill.Child is TextBlock tb && this.TryFindResource(fgKey, ActualThemeVariant, out var fRes) && fRes is IBrush fBrush)
                tb.Foreground = fBrush;
        }
    }

    private void RebuildSwatches()
    {
        if (_swatchesHost is null)
            return;

        _swatchesHost.Children.Clear();
        var selected = SelectedColor;
        var accentBrush = this.TryFindResource("VelaAccentBrush", ActualThemeVariant, out var ar) && ar is IBrush ab
            ? ab
            : Brushes.DodgerBlue;
        var borderBrush = this.TryFindResource("VelaBorderBrush", ActualThemeVariant, out var br) && br is IBrush bb
            ? bb
            : Brushes.Gray;

        foreach (var swatch in Swatches)
        {
            var c = swatch;
            var isCurrent = c == selected;
            var dot = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(999),
                Background = new SolidColorBrush(c),
                BorderBrush = isCurrent ? accentBrush : borderBrush,
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(dot, VelaColorMath.ToHex(c));
            dot.PointerPressed += (_, ev) =>
            {
                if (!ev.GetCurrentPoint(dot).Properties.IsLeftButtonPressed) return;
                VelaColorMath.ColorToHsv(c, _h, out _h, out _s, out _v, out _a);
                CommitHsv(animateVisuals: true);
                ev.Handled = true;
            };
            _swatchesHost.Children.Add(dot);
        }
    }

    private Color ResolveSurfaceColor() =>
        this.TryFindResource("VelaSurfaceColor", ActualThemeVariant, out var res) && res is Color c
            ? c
            : ActualThemeVariant == ThemeVariant.Dark ? Color.FromRgb(25, 25, 25) : Colors.White;
}

internal static class VelaColorMath
{
    public static Color HsvToColor(double h, double s, double v, double a)
    {
        h = ((h % 360.0) + 360.0) % 360.0;
        s = Math.Clamp(s, 0.0, 1.0);
        v = Math.Clamp(v, 0.0, 1.0);
        a = Math.Clamp(a, 0.0, 1.0);

        double F(double n)
        {
            var k = (n + h / 60.0) % 6.0;
            return v - v * s * Math.Max(0.0, Math.Min(Math.Min(k, 4.0 - k), 1.0));
        }

        return Color.FromArgb(
            (byte)Math.Round(a * 255.0),
            (byte)Math.Round(F(5) * 255.0),
            (byte)Math.Round(F(3) * 255.0),
            (byte)Math.Round(F(1) * 255.0));
    }

    public static void ColorToHsv(Color c, double fallbackHue, out double h, out double s, out double v, out double a)
    {
        var r = c.R / 255.0;
        var g = c.G / 255.0;
        var b = c.B / 255.0;
        a = c.A / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;

        h = fallbackHue;
        if (d > 1e-6)
        {
            if (Math.Abs(max - r) < 1e-9) h = 60.0 * (((g - b) / d) % 6.0);
            else if (Math.Abs(max - g) < 1e-9) h = 60.0 * ((b - r) / d + 2.0);
            else h = 60.0 * ((r - g) / d + 4.0);
            if (h < 0) h += 360.0;
        }

        s = max <= 1e-9 ? 0.0 : d / max;
        v = max;
    }

    public static string ToHex(Color c) =>
        c.A < 255
            ? $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}"
            : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static string Format(Color c, double hue, VelaColorFormat format)
    {
        var alphaSuffix = c.A < 255 ? $" / {Math.Round(c.A * 100.0 / 255.0)}%" : string.Empty;
        switch (format)
        {
            case VelaColorFormat.Rgb:
                return $"rgb({c.R} {c.G} {c.B}{alphaSuffix})";
            case VelaColorFormat.Hsl:
            {
                var r = c.R / 255.0;
                var g = c.G / 255.0;
                var b = c.B / 255.0;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                var l = (max + min) * 0.5;
                var d = max - min;
                var s = d < 1e-6 ? 0.0 : d / (1.0 - Math.Abs(2.0 * l - 1.0));
                return $"hsl({Math.Round(hue) % 360} {Math.Round(s * 100)}% {Math.Round(l * 100)}%{alphaSuffix})";
            }
            case VelaColorFormat.Oklch:
            {
                var lr = ToLinear(c.R / 255.0);
                var lg = ToLinear(c.G / 255.0);
                var lb = ToLinear(c.B / 255.0);
                var l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
                var m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
                var s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
                var L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
                var A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
                var B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
                var C = Math.Sqrt(A * A + B * B);
                var H = Math.Atan2(B, A) * 180.0 / Math.PI;
                if (H < 0) H += 360.0;
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "oklch({0:0.0}% {1:0.000} {2:0.0}{3})",
                    Math.Clamp(L * 100.0, 0, 100),
                    C,
                    C < 0.0005 ? hue : H,
                    alphaSuffix);
            }
            default:
                return ToHex(c);
        }
    }

    public static bool TryParse(string? input, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var text = input.Trim();

        // Hex (#RGB, #RGBA, #RRGGBB, #RRGGBBAA)
        var hexMatch = Regex.Match(text, "^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");
        if (hexMatch.Success)
        {
            var d = hexMatch.Groups[1].Value;
            if (d.Length is 3 or 4)
                d = string.Concat(d.Select(ch => $"{ch}{ch}"));
            var r = Convert.ToByte(d[..2], 16);
            var g = Convert.ToByte(d[2..4], 16);
            var b = Convert.ToByte(d[4..6], 16);
            var a = d.Length == 8 ? Convert.ToByte(d[6..8], 16) : (byte)255;
            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        // rgb(r g b) / rgba(r, g, b, a)
        var rgbMatch = Regex.Match(text, @"^rgba?\(\s*(\d+)[\s,]+(\d+)[\s,]+(\d+)(?:\s*[/,]\s*([\d.]+%?))?\s*\)$", RegexOptions.IgnoreCase);
        if (rgbMatch.Success)
        {
            var r = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[1].Value, CultureInfo.InvariantCulture), 0, 255);
            var g = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[2].Value, CultureInfo.InvariantCulture), 0, 255);
            var b = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[3].Value, CultureInfo.InvariantCulture), 0, 255);
            var a = ParseAlpha(rgbMatch.Groups[4].Value);
            color = Color.FromArgb((byte)Math.Round(a * 255), r, g, b);
            return true;
        }

        return Color.TryParse(text, out color);
    }

    private static double ParseAlpha(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 1.0;
        if (text.EndsWith('%') && double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            return Math.Clamp(pct / 100.0, 0.0, 1.0);
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return Math.Clamp(val, 0.0, 1.0);
        return 1.0;
    }

    public static double ContrastRatio(Color fg, Color bg)
    {
        var a = fg.A / 255.0;
        var mr = (fg.R / 255.0) * a + (bg.R / 255.0) * (1.0 - a);
        var mg = (fg.G / 255.0) * a + (bg.G / 255.0) * (1.0 - a);
        var mb = (fg.B / 255.0) * a + (bg.B / 255.0) * (1.0 - a);

        var lum1 = 0.2126 * ToLinear(mr) + 0.7152 * ToLinear(mg) + 0.0722 * ToLinear(mb);
        var lum2 = 0.2126 * ToLinear(bg.R / 255.0) + 0.7152 * ToLinear(bg.G / 255.0) + 0.0722 * ToLinear(bg.B / 255.0);
        return (Math.Max(lum1, lum2) + 0.05) / (Math.Min(lum1, lum2) + 0.05);
    }

    private static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
