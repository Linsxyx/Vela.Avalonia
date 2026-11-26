using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's DatePicker: anchored trigger button opening <see cref="VelaCalendar"/>
/// inside a spring-entering <see cref="VelaPopupSurface"/>.
/// </summary>
[PseudoClasses(":open", ":has-value")]
public class VelaDatePicker : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaDatePicker, string?>(nameof(Label));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<VelaDatePicker, string?>(nameof(PlaceholderText), "Select a date...");

    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<VelaDatePicker, string>(nameof(DateFormat), "MMM dd, yyyy");

    public static readonly StyledProperty<DateTime?> SelectedDateProperty =
        AvaloniaProperty.Register<VelaDatePicker, DateTime?>(nameof(SelectedDate), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<VelaDatePicker, bool>(nameof(IsDropDownOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<VelaDatePicker, string> DisplayTextProperty =
        AvaloniaProperty.RegisterDirect<VelaDatePicker, string>(nameof(DisplayText), o => o.DisplayText);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }
    public DateTime? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }

    private string _displayText = "Select a date...";
    public string DisplayText
    {
        get => _displayText;
        private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaDatePicker);

    private Button? _trigger;
    private Button? _clearBtn;
    private VelaCalendar? _calendar;

    static VelaDatePicker()
    {
        SelectedDateProperty.Changed.AddClassHandler<VelaDatePicker>((p, _) => p.SyncDisplay());
        PlaceholderTextProperty.Changed.AddClassHandler<VelaDatePicker>((p, _) => p.SyncDisplay());
        DateFormatProperty.Changed.AddClassHandler<VelaDatePicker>((p, _) => p.SyncDisplay());
        IsDropDownOpenProperty.Changed.AddClassHandler<VelaDatePicker>((p, _) => p.PseudoClasses.Set(":open", p.IsDropDownOpen));
    }

    public VelaDatePicker()
    {
        SyncDisplay();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_trigger is not null) _trigger.Click -= OnTriggerClick;
        if (_clearBtn is not null) _clearBtn.Click -= OnClearClick;
        if (_calendar is not null) _calendar.DateSelected -= OnCalendarDateSelected;

        _trigger = e.NameScope.Find<Button>("PART_Trigger");
        _clearBtn = e.NameScope.Find<Button>("PART_ClearButton");
        _calendar = e.NameScope.Find<VelaCalendar>("PART_Calendar");

        if (_trigger is not null) _trigger.Click += OnTriggerClick;
        if (_clearBtn is not null) _clearBtn.Click += OnClearClick;
        if (_calendar is not null) _calendar.DateSelected += OnCalendarDateSelected;

        SyncDisplay();
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
        => IsDropDownOpen = !IsDropDownOpen;

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        SelectedDate = null;
        e.Handled = true;
    }

    private void OnCalendarDateSelected(object? sender, DateTime date)
    {
        SelectedDate = date;
        IsDropDownOpen = false;
    }

    private void SyncDisplay()
    {
        var hasVal = SelectedDate.HasValue;
        PseudoClasses.Set(":has-value", hasVal);
        DisplayText = hasVal
            ? SelectedDate!.Value.ToString(DateFormat, CultureInfo.InvariantCulture)
            : (PlaceholderText ?? string.Empty);
    }
}
