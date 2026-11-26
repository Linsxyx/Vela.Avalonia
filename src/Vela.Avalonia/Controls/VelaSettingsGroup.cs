using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Metadata;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Clean, macOS / Arc-inspired grouped settings card with optional section header,
/// description, and automatic 1px hairline dividers between rows.
/// </summary>
public class VelaSettingsGroup : TemplatedControl
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<VelaSettingsGroup, string?>(nameof(Header));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaSettingsGroup, string?>(nameof(Description));

    public static readonly StyledProperty<object?> HeaderActionProperty =
        AvaloniaProperty.Register<VelaSettingsGroup, object?>(nameof(HeaderAction));

    public string? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public object? HeaderAction { get => GetValue(HeaderActionProperty); set => SetValue(HeaderActionProperty, value); }

    [Content]
    public ObservableCollection<Control> Items { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaSettingsGroup);

    private StackPanel? _rowsHost;

    public VelaSettingsGroup()
    {
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rowsHost = e.NameScope.Find<StackPanel>("PART_RowsHost");
        RebuildRows();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildRows();
    }

    private void RebuildRows()
    {
        if (_rowsHost is null)
            return;

        _rowsHost.Children.Clear();
        for (var i = 0; i < Items.Count; i++)
        {
            if (i > 0)
            {
                _rowsHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(16, 0),
                    [!Border.BackgroundProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding(),
                });
            }

            _rowsHost.Children.Add(Items[i]);
        }
    }
}

/// <summary>
/// Individual preference row inside <see cref="VelaSettingsGroup"/> with optional icon tile,
/// title, subtitle description, trailing control slot, and optional click navigation.
/// </summary>
[PseudoClasses(":clickable", ":pressed")]
public class VelaSettingsRow : TemplatedControl
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaSettingsRow, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<VelaSettingsRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaSettingsRow, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaSettingsRow, object?>(nameof(Content));

    public static readonly StyledProperty<bool> IsClickableProperty =
        AvaloniaProperty.Register<VelaSettingsRow, bool>(nameof(IsClickable));

    public static readonly StyledProperty<bool> ShowChevronProperty =
        AvaloniaProperty.Register<VelaSettingsRow, bool>(nameof(ShowChevron));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<VelaSettingsRow, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<VelaSettingsRow, object?>(nameof(CommandParameter));

    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public bool IsClickable { get => GetValue(IsClickableProperty); set => SetValue(IsClickableProperty, value); }
    public bool ShowChevron { get => GetValue(ShowChevronProperty); set => SetValue(ShowChevronProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public event EventHandler? Click;

    protected override Type StyleKeyOverride => typeof(VelaSettingsRow);

    static VelaSettingsRow()
    {
        IsClickableProperty.Changed.AddClassHandler<VelaSettingsRow>((r, _) => r.UpdatePseudoClasses());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":clickable", IsClickable);
        Cursor = IsClickable ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsClickable || !IsEnabled)
            return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            PseudoClasses.Set(":pressed", true);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsClickable)
            return;

        var wasPressed = PseudoClasses.Contains(":pressed");
        PseudoClasses.Set(":pressed", false);

        if (wasPressed && IsEnabled && this.Bounds.Contains(e.GetPosition(this)))
        {
            Click?.Invoke(this, EventArgs.Empty);
            if (Command?.CanExecute(CommandParameter) == true)
                Command.Execute(CommandParameter);
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        PseudoClasses.Set(":pressed", false);
    }
}
