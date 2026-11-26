using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaHoldTone
{
    Accent,
    Danger,
    Neutral,
}

/// <summary>
/// Port of Arc UI's HoldToConfirm:
/// - Destructive / high-stakes action button requiring a sustained hold (mouse/touch or Space/Enter)
/// - Dual-face razor-clipped fill layer so label & icon invert cleanly right at the fill edge
/// - Linear charge while holding, spring rewind on early release, press micro-scale, and width-morphing done state
/// </summary>
[PseudoClasses(":holding", ":confirmed", ":accent", ":danger", ":neutral")]
public class VelaHoldToConfirm : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, string>(nameof(Label), "Hold to confirm");

    public static readonly StyledProperty<string> ConfirmedLabelProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, string>(nameof(ConfirmedLabel), "Confirmed");

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<VelaHoldTone> ToneProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, VelaHoldTone>(nameof(Tone), VelaHoldTone.Danger);

    public static readonly StyledProperty<int> DurationMsProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, int>(nameof(DurationMs), 1100);

    public static readonly StyledProperty<bool> IsConfirmedProperty =
        AvaloniaProperty.Register<VelaHoldToConfirm, bool>(
            nameof(IsConfirmed),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<VelaHoldToConfirm, string> DisplayLabelProperty =
        AvaloniaProperty.RegisterDirect<VelaHoldToConfirm, string>(
            nameof(DisplayLabel),
            o => o.DisplayLabel);

    private string _displayLabel = "Hold to confirm";

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string ConfirmedLabel { get => GetValue(ConfirmedLabelProperty); set => SetValue(ConfirmedLabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public VelaHoldTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public int DurationMs { get => GetValue(DurationMsProperty); set => SetValue(DurationMsProperty, value); }
    public bool IsConfirmed { get => GetValue(IsConfirmedProperty); set => SetValue(IsConfirmedProperty, value); }

    public string DisplayLabel
    {
        get => _displayLabel;
        private set => SetAndRaise(DisplayLabelProperty, ref _displayLabel, value);
    }

    public event EventHandler? Confirmed;
    public event EventHandler<bool>? HoldChanged;

    protected override Type StyleKeyOverride => typeof(VelaHoldToConfirm);

    private readonly SpringChannel _rewindSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringChannel _scaleSpring = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Border? _shell;
    private Grid? _contentGrid;
    private ScaleTransform? _shellScale;
    private Border? _fillClipBorder;
    private Border? _fillLayer;
    private PathIcon? _baseIcon;
    private PathIcon? _fillIcon;

    private bool _isHolding;
    private bool _holdingViaKey;
    private double _progress; // 0..1

    static VelaHoldToConfirm()
    {
        FocusableProperty.OverrideDefaultValue<VelaHoldToConfirm>(true);
        LabelProperty.Changed.AddClassHandler<VelaHoldToConfirm>((c, _) => c.SyncFace());
        ConfirmedLabelProperty.Changed.AddClassHandler<VelaHoldToConfirm>((c, _) => c.SyncFace());
        IconProperty.Changed.AddClassHandler<VelaHoldToConfirm>((c, _) => c.SyncFace());
        ToneProperty.Changed.AddClassHandler<VelaHoldToConfirm>((c, _) => c.UpdatePseudoClasses());
        IsConfirmedProperty.Changed.AddClassHandler<VelaHoldToConfirm>((c, _) => c.OnIsConfirmedChanged());
    }

    public VelaHoldToConfirm()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    public void Reset()
    {
        _isHolding = false;
        _holdingViaKey = false;
        IsConfirmed = false;
        StartRewind();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_contentGrid is not null)
            _contentGrid.SizeChanged -= OnShellSizeChanged;

        _shell = e.NameScope.Find<Border>("PART_Shell");
        _contentGrid = e.NameScope.Find<Grid>("PART_ContentGrid");
        _fillClipBorder = e.NameScope.Find<Border>("PART_FillClip");
        _fillLayer = e.NameScope.Find<Border>("PART_FillLayer");
        _baseIcon = e.NameScope.Find<PathIcon>("PART_BaseIcon");
        _fillIcon = e.NameScope.Find<PathIcon>("PART_FillIcon");

        if (_shell is not null)
        {
            _shellScale = new ScaleTransform(1, 1);
            _shell.RenderTransformOrigin = RelativePoint.Center;
            _shell.RenderTransform = _shellScale;
        }

        if (_contentGrid is not null)
            _contentGrid.SizeChanged += OnShellSizeChanged;

        _progress = IsConfirmed ? 1.0 : 0.0;
        _rewindSpring.Jump(_progress);
        UpdatePseudoClasses();
        SyncFace();
        ApplyVisuals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isHolding = false;
        _holdingViaKey = false;
        _runner.Stop();
    }

    private void OnShellSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyVisuals();

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":holding", _isHolding);
        PseudoClasses.Set(":confirmed", IsConfirmed);
        PseudoClasses.Set(":accent", Tone == VelaHoldTone.Accent);
        PseudoClasses.Set(":danger", Tone == VelaHoldTone.Danger);
        PseudoClasses.Set(":neutral", Tone == VelaHoldTone.Neutral);
    }

    private void SyncFace()
    {
        DisplayLabel = IsConfirmed ? ConfirmedLabel : Label;

        var geom = IsConfirmed
            ? ResolveGeometry("VelaIconCheck")
            : Icon ?? ResolveGeometry("VelaIconTrash");

        if (_baseIcon is not null) _baseIcon.Data = geom;
        if (_fillIcon is not null) _fillIcon.Data = geom;
    }

    private void OnIsConfirmedChanged()
    {
        UpdatePseudoClasses();
        SyncFace();

        if (IsConfirmed)
        {
            _isHolding = false;
            _holdingViaKey = false;
            if (VelaMotionOptions.ReducedMotion)
            {
                _progress = 1.0;
                _rewindSpring.Jump(1.0);
                ApplyVisuals();
            }
            else
            {
                _rewindSpring.Jump(_progress);
                _rewindSpring.SetTarget(1.0, VelaMotionTokens.Springs.Smooth);
                _runner.Wake();
            }
        }
        else if (!_isHolding)
        {
            StartRewind();
        }
    }

    private void BeginHold(bool viaKey)
    {
        if (!IsEnabled || IsConfirmed || _isHolding)
            return;

        _isHolding = true;
        _holdingViaKey = viaKey;
        UpdatePseudoClasses();
        HoldChanged?.Invoke(this, true);

        if (!VelaMotionOptions.ReducedMotion)
            _scaleSpring.SetTarget(0.972, VelaMotionTokens.Springs.Snappy);

        _runner.Wake();
    }

    private void CancelHold()
    {
        if (!_isHolding)
            return;

        _isHolding = false;
        _holdingViaKey = false;
        UpdatePseudoClasses();
        HoldChanged?.Invoke(this, false);
        StartRewind();
    }

    private void StartRewind()
    {
        if (VelaMotionOptions.ReducedMotion)
        {
            _progress = 0.0;
            _rewindSpring.Jump(0.0);
            _scaleSpring.Jump(1.0);
            ApplyVisuals();
            return;
        }

        _scaleSpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _rewindSpring.Jump(_progress);
        _rewindSpring.SetTarget(0.0, VelaMotionTokens.Springs.Smooth);
        _runner.Wake();
    }

    private void CompleteHold()
    {
        _isHolding = false;
        _holdingViaKey = false;
        _progress = 1.0;
        _rewindSpring.Jump(1.0);
        _scaleSpring.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        HoldChanged?.Invoke(this, false);
        IsConfirmed = true;
        UpdatePseudoClasses();
        SyncFace();
        ApplyVisuals();
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || IsConfirmed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        e.Pointer.Capture(this);
        BeginHold(viaKey: false);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isHolding || _holdingViaKey) return;

        const double slack = 24.0;
        var pos = e.GetPosition(this);
        if (pos.X < -slack || pos.X > Bounds.Width + slack ||
            pos.Y < -slack || pos.Y > Bounds.Height + slack)
        {
            e.Pointer.Capture(null);
            CancelHold();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isHolding && !_holdingViaKey)
        {
            e.Pointer.Capture(null);
            CancelHold();
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_isHolding && !_holdingViaKey)
            CancelHold();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter && !IsConfirmed && IsEnabled)
        {
            BeginHold(viaKey: true);
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.Space or Key.Enter && _isHolding && _holdingViaKey)
        {
            CancelHold();
            e.Handled = true;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_isHolding)
            CancelHold();
    }

    private bool OnTick(double dt)
    {
        var active = _scaleSpring.Step(dt);

        if (_isHolding)
        {
            var durSec = Math.Max(0.25, DurationMs / 1000.0);
            _progress = Math.Min(1.0, _progress + dt / durSec);
            _rewindSpring.Jump(_progress);

            if (_progress >= 1.0)
            {
                CompleteHold();
                return true;
            }

            ApplyVisuals();
            return true;
        }

        active |= _rewindSpring.Step(dt);
        _progress = Math.Clamp(_rewindSpring.Value, 0.0, 1.0);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (_shellScale is not null)
        {
            var s = Math.Clamp(_scaleSpring.Value, 0.9, 1.05);
            _shellScale.ScaleX = s;
            _shellScale.ScaleY = s;
        }

        if (_fillLayer is not null && _fillClipBorder is not null && _contentGrid is not null)
        {
            var w = _contentGrid.Bounds.Width;
            var h = _contentGrid.Bounds.Height;
            var clipW = Math.Clamp(_progress, 0.0, 1.0) * w;
            _fillClipBorder.Width = clipW;
            _fillClipBorder.Height = h;
            _fillLayer.Width = w;
            _fillLayer.Height = h;
        }
    }

    private Geometry? ResolveGeometry(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is Geometry g ? g : null;
}
