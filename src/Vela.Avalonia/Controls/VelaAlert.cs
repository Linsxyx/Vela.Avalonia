using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaAlertTone
{
    Info,
    Success,
    Warning,
    Danger,
}

/// <summary>
/// Port of Arc UI's Alert: calm neutral card surface with semantic tone icon,
/// optional action slot, and spring-animated height+opacity dismissal.
/// </summary>
[PseudoClasses(":info", ":success", ":warning", ":danger")]
public class VelaAlert : TemplatedControl
{
    public static readonly StyledProperty<VelaAlertTone> ToneProperty =
        AvaloniaProperty.Register<VelaAlert, VelaAlertTone>(nameof(Tone), VelaAlertTone.Info);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<VelaAlert, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaAlert, string?>(nameof(Description));

    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<VelaAlert, bool>(nameof(IsDismissible));

    public static readonly StyledProperty<object?> ActionContentProperty =
        AvaloniaProperty.Register<VelaAlert, object?>(nameof(ActionContent));

    public VelaAlertTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }
    public object? ActionContent { get => GetValue(ActionContentProperty); set => SetValue(ActionContentProperty, value); }

    public event EventHandler? Dismissed;

    protected override Type StyleKeyOverride => typeof(VelaAlert);

    private readonly SpringChannel _opacitySpring = new(1.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _heightSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;

    private Button? _dismissButton;
    private bool _dismissing;

    static VelaAlert()
    {
        ToneProperty.Changed.AddClassHandler<VelaAlert>((a, _) => a.UpdatePseudoClasses());
    }

    public VelaAlert()
    {
        _runner = new SpringRunner(this, OnTick);
        UpdatePseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_dismissButton is not null)
            _dismissButton.Click -= OnDismissClick;

        _dismissButton = e.NameScope.Find<Button>("PART_DismissButton");
        if (_dismissButton is not null)
            _dismissButton.Click += OnDismissClick;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_dismissButton is not null)
            _dismissButton.Click -= OnDismissClick;
    }

    public void Dismiss()
    {
        if (_dismissing || !IsVisible)
            return;

        _dismissing = true;
        IsHitTestVisible = false;

        if (VelaMotionOptions.ReducedMotion || Bounds.Height <= 0)
        {
            IsVisible = false;
            Dismissed?.Invoke(this, EventArgs.Empty);
            return;
        }

        ClipToBounds = true;
        _opacitySpring.Jump(1.0);
        _heightSpring.Jump(Bounds.Height);
        _opacitySpring.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _heightSpring.SetTarget(0.0, VelaMotionTokens.Springs.Smooth);
        _runner.Wake();
    }

    public void Restore()
    {
        _dismissing = false;
        _runner.Stop();
        Height = double.NaN;
        Opacity = 1.0;
        IsHitTestVisible = true;
        IsVisible = true;
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Dismiss();

    private bool OnTick(double dt)
    {
        var active = _opacitySpring.Step(dt);
        active |= _heightSpring.Step(dt);

        Opacity = Math.Clamp(_opacitySpring.Value, 0.0, 1.0);
        Height = Math.Max(0.0, _heightSpring.Value);

        if (!active && _dismissing)
        {
            IsVisible = false;
            Height = double.NaN;
            Dismissed?.Invoke(this, EventArgs.Empty);
        }

        return active;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":info", Tone == VelaAlertTone.Info);
        PseudoClasses.Set(":success", Tone == VelaAlertTone.Success);
        PseudoClasses.Set(":warning", Tone == VelaAlertTone.Warning);
        PseudoClasses.Set(":danger", Tone == VelaAlertTone.Danger);
    }
}
