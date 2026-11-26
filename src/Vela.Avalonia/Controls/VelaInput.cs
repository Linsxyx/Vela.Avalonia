using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Input field: clean border-only focus feedback (zero loud halo),
/// built-in Label, Description, ErrorMessage, LeadingIcon, Size presets, and optional Clear button.
/// </summary>
[PseudoClasses(":error", ":sm", ":md", ":lg", ":has-text")]
public class VelaInput : TextBox
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaInput, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaInput, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<VelaInput, string?>(nameof(ErrorMessage));

    public static readonly StyledProperty<Geometry?> LeadingIconProperty =
        AvaloniaProperty.Register<VelaInput, Geometry?>(nameof(LeadingIcon));

    public static readonly StyledProperty<bool> IsClearableProperty =
        AvaloniaProperty.Register<VelaInput, bool>(nameof(IsClearable));

    public static readonly StyledProperty<VelaControlSize> SizeProperty =
        AvaloniaProperty.Register<VelaInput, VelaControlSize>(nameof(Size), VelaControlSize.Medium);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
    public Geometry? LeadingIcon { get => GetValue(LeadingIconProperty); set => SetValue(LeadingIconProperty, value); }
    public bool IsClearable { get => GetValue(IsClearableProperty); set => SetValue(IsClearableProperty, value); }
    public VelaControlSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaInput);

    private Button? _clearButton;

    static VelaInput()
    {
        ErrorMessageProperty.Changed.AddClassHandler<VelaInput>((i, _) => i.UpdatePseudoClasses());
        SizeProperty.Changed.AddClassHandler<VelaInput>((i, _) => i.UpdatePseudoClasses());
        TextProperty.Changed.AddClassHandler<VelaInput>((i, _) => i.UpdatePseudoClasses());
        IsClearableProperty.Changed.AddClassHandler<VelaInput>((i, _) => i.UpdatePseudoClasses());
    }

    public VelaInput()
    {
        UpdatePseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_clearButton is not null)
            _clearButton.Click -= OnClearClick;

        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        if (_clearButton is not null)
            _clearButton.Click += OnClearClick;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_clearButton is not null)
            _clearButton.Click -= OnClearClick;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Clear();
        Focus();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":error", !string.IsNullOrEmpty(ErrorMessage));
        PseudoClasses.Set(":sm", Size == VelaControlSize.Small);
        PseudoClasses.Set(":md", Size == VelaControlSize.Medium);
        PseudoClasses.Set(":lg", Size == VelaControlSize.Large);
        PseudoClasses.Set(":has-text", IsClearable && !string.IsNullOrEmpty(Text));
    }
}
