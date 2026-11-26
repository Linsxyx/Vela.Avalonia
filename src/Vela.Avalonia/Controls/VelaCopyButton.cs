using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaCopyButtonVariant
{
    Outline,
    Plain,
}

/// <summary>
/// Port of Arc UI's CopyButton:
/// - One-click clipboard copy via Avalonia TopLevel.Clipboard
/// - Spring icon swap (Copy -> Checkmark in VelaSuccessBrush)
/// - Width-morphing label slot via <see cref="VelaMorphSlotPanel"/> so longer labels like "Copy API key"
///   smoothly resize to "Copied" and back without jarring jumps.
/// </summary>
[PseudoClasses(":copied", ":plain", ":icon-only")]
public class VelaCopyButton : TemplatedControl
{
    public static readonly StyledProperty<string?> CopyTextProperty =
        AvaloniaProperty.Register<VelaCopyButton, string?>(nameof(CopyText));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VelaCopyButton, string>(nameof(Label), "Copy");

    public static readonly StyledProperty<string> CopiedLabelProperty =
        AvaloniaProperty.Register<VelaCopyButton, string>(nameof(CopiedLabel), "Copied");

    public static readonly StyledProperty<bool> IconOnlyProperty =
        AvaloniaProperty.Register<VelaCopyButton, bool>(nameof(IconOnly));

    public static readonly StyledProperty<VelaCopyButtonVariant> VariantProperty =
        AvaloniaProperty.Register<VelaCopyButton, VelaCopyButtonVariant>(nameof(Variant), VelaCopyButtonVariant.Outline);

    public static readonly StyledProperty<int> ResetDelayMsProperty =
        AvaloniaProperty.Register<VelaCopyButton, int>(nameof(ResetDelayMs), 1800);

    public static readonly DirectProperty<VelaCopyButton, bool> IsCopiedProperty =
        AvaloniaProperty.RegisterDirect<VelaCopyButton, bool>(nameof(IsCopied), o => o.IsCopied);

    public static readonly DirectProperty<VelaCopyButton, string> DisplayLabelProperty =
        AvaloniaProperty.RegisterDirect<VelaCopyButton, string>(nameof(DisplayLabel), o => o.DisplayLabel);

    private bool _isCopied;
    private string _displayLabel = "Copy";

    public string? CopyText { get => GetValue(CopyTextProperty); set => SetValue(CopyTextProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string CopiedLabel { get => GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }
    public bool IconOnly { get => GetValue(IconOnlyProperty); set => SetValue(IconOnlyProperty, value); }
    public VelaCopyButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public int ResetDelayMs { get => GetValue(ResetDelayMsProperty); set => SetValue(ResetDelayMsProperty, value); }

    public bool IsCopied
    {
        get => _isCopied;
        private set => SetAndRaise(IsCopiedProperty, ref _isCopied, value);
    }

    public string DisplayLabel
    {
        get => _displayLabel;
        private set => SetAndRaise(DisplayLabelProperty, ref _displayLabel, value);
    }

    public event EventHandler? Copied;

    protected override Type StyleKeyOverride => typeof(VelaCopyButton);

    private readonly SpringChannel _swapSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private DispatcherTimer? _resetTimer;

    private Button? _button;
    private PathIcon? _copyIcon;
    private PathIcon? _checkIcon;
    private ScaleTransform? _copyScale;
    private ScaleTransform? _checkScale;

    static VelaCopyButton()
    {
        LabelProperty.Changed.AddClassHandler<VelaCopyButton>((b, _) => b.SyncDisplayLabel());
        CopiedLabelProperty.Changed.AddClassHandler<VelaCopyButton>((b, _) => b.SyncDisplayLabel());
        IconOnlyProperty.Changed.AddClassHandler<VelaCopyButton>((b, _) => b.UpdatePseudoClasses());
        VariantProperty.Changed.AddClassHandler<VelaCopyButton>((b, _) => b.UpdatePseudoClasses());
    }

    public VelaCopyButton()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_button is not null)
            _button.Click -= OnButtonClick;

        _button = e.NameScope.Find<Button>("PART_Button");
        _copyIcon = e.NameScope.Find<PathIcon>("PART_CopyIcon");
        _checkIcon = e.NameScope.Find<PathIcon>("PART_CheckIcon");

        if (_copyIcon is not null)
        {
            _copyScale = new ScaleTransform(1, 1);
            _copyIcon.RenderTransformOrigin = RelativePoint.Center;
            _copyIcon.RenderTransform = _copyScale;
        }

        if (_checkIcon is not null)
        {
            _checkScale = new ScaleTransform(0.6, 0.6);
            _checkIcon.RenderTransformOrigin = RelativePoint.Center;
            _checkIcon.RenderTransform = _checkScale;
        }

        if (_button is not null)
            _button.Click += OnButtonClick;

        UpdatePseudoClasses();
        SyncDisplayLabel();
        ApplySwap();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopResetTimer();
        _runner.Stop();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":copied", IsCopied);
        PseudoClasses.Set(":plain", Variant == VelaCopyButtonVariant.Plain);
        PseudoClasses.Set(":icon-only", IconOnly);
    }

    private void SyncDisplayLabel()
    {
        DisplayLabel = IsCopied ? CopiedLabel : Label;
    }

    private async void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        var text = CopyText ?? string.Empty;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch
            {
                // Ignore platform clipboard busy errors gracefully
            }
        }

        SetCopiedState(true);
        Copied?.Invoke(this, EventArgs.Empty);

        StopResetTimer();
        _resetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(Math.Max(400, ResetDelayMs)),
        };
        _resetTimer.Tick += (_, _) =>
        {
            StopResetTimer();
            SetCopiedState(false);
        };
        _resetTimer.Start();
    }

    private void StopResetTimer()
    {
        if (_resetTimer is null) return;
        _resetTimer.Stop();
        _resetTimer = null;
    }

    private void SetCopiedState(bool copied)
    {
        IsCopied = copied;
        UpdatePseudoClasses();
        SyncDisplayLabel();

        var target = copied ? 1.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _swapSpring.Jump(target);
            ApplySwap();
            return;
        }

        _swapSpring.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _swapSpring.Step(dt);
        ApplySwap();
        return active;
    }

    private void ApplySwap()
    {
        var t = Math.Clamp(_swapSpring.Value, 0.0, 1.15);
        var p = Math.Clamp(t, 0.0, 1.0);

        if (_copyIcon is not null && _copyScale is not null)
        {
            var inv = 1.0 - p;
            _copyIcon.Opacity = inv;
            var s = 0.72 + 0.28 * inv;
            _copyScale.ScaleX = s;
            _copyScale.ScaleY = s;
        }

        if (_checkIcon is not null && _checkScale is not null)
        {
            _checkIcon.Opacity = p;
            var s = 0.65 + 0.35 * t;
            _checkScale.ScaleX = s;
            _checkScale.ScaleY = s;
        }
    }
}
