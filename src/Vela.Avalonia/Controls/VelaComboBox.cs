using System.Collections;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's Combobox (searchable filterable select):
/// - Type directly inside the field to filter options in real time
/// - Full Up/Down/Enter/Escape keyboard navigation
/// - Clear button, checkmark on selected option, empty-state message, and spring chevron
/// </summary>
[PseudoClasses(":open", ":has-value")]
public class VelaComboBox : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaComboBox, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaComboBox, string?>(nameof(Description));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<VelaComboBox, string?>(nameof(PlaceholderText), "Search or select...");

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<VelaComboBox, string>(nameof(EmptyText), "No matching results.");

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VelaComboBox, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<VelaComboBox, object?>(nameof(SelectedItem), null, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsDropDownOpenProperty =
        AvaloniaProperty.Register<VelaComboBox, bool>(nameof(IsDropDownOpen), false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Func<object, string>?> DisplaySelectorProperty =
        AvaloniaProperty.Register<VelaComboBox, Func<object, string>?>(nameof(DisplaySelector));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public bool IsDropDownOpen { get => GetValue(IsDropDownOpenProperty); set => SetValue(IsDropDownOpenProperty, value); }
    public Func<object, string>? DisplaySelector { get => GetValue(DisplaySelectorProperty); set => SetValue(DisplaySelectorProperty, value); }

    public event EventHandler<object?>? SelectionChanged;

    protected override Type StyleKeyOverride => typeof(VelaComboBox);

    private readonly VelaObservableList<object> _filtered = new();
    private readonly SpringChannel _chevronAngle = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private TextBox? _searchBox;
    private Button? _clearButton;
    private Button? _toggleButton;
    private ListBox? _listBox;
    private TextBlock? _emptyLabel;
    private Popup? _popup;
    private RotateTransform? _chevronRotate;
    private bool _suppressSearchUpdate;

    static VelaComboBox()
    {
        ItemsSourceProperty.Changed.AddClassHandler<VelaComboBox>((c, _) => c.RefreshFilter(showAll: true));
        SelectedItemProperty.Changed.AddClassHandler<VelaComboBox>((c, _) => c.OnSelectedItemChanged());
        IsDropDownOpenProperty.Changed.AddClassHandler<VelaComboBox>((c, _) => c.OnOpenChanged());
    }

    public VelaComboBox()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_searchBox is not null)
        {
            _searchBox.TextChanged -= OnSearchTextChanged;
            _searchBox.GotFocus -= OnSearchGotFocus;
            _searchBox.KeyDown -= OnSearchKeyDown;
        }
        if (_clearButton is not null)
            _clearButton.Click -= OnClearClick;
        if (_toggleButton is not null)
            _toggleButton.Click -= OnToggleClick;
        if (_listBox is not null)
            _listBox.PointerReleased -= OnListPointerReleased;
        if (_popup is not null)
            _popup.Closed -= OnPopupClosed;

        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        _toggleButton = e.NameScope.Find<Button>("PART_ToggleButton");
        _listBox = e.NameScope.Find<ListBox>("PART_ListBox");
        _emptyLabel = e.NameScope.Find<TextBlock>("PART_EmptyLabel");
        _popup = e.NameScope.Find<Popup>("PART_Popup");

        var chevron = e.NameScope.Find<PathIcon>("PART_Chevron");
        if (chevron is not null)
        {
            _chevronRotate = new RotateTransform(IsDropDownOpen ? 180.0 : 0.0);
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _chevronRotate;
        }

        if (_searchBox is not null)
        {
            _searchBox.TextChanged += OnSearchTextChanged;
            _searchBox.GotFocus += OnSearchGotFocus;
            _searchBox.KeyDown += OnSearchKeyDown;
        }
        if (_clearButton is not null)
            _clearButton.Click += OnClearClick;
        if (_toggleButton is not null)
            _toggleButton.Click += OnToggleClick;
        if (_listBox is not null)
        {
            _listBox.ItemsSource = _filtered;
            _listBox.PointerReleased += OnListPointerReleased;
        }
        if (_popup is not null)
            _popup.Closed += OnPopupClosed;

        SyncSearchTextFromSelected();
        RefreshFilter(showAll: true);
        UpdatePseudoClasses();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private string FormatItem(object? item)
    {
        if (item is null) return string.Empty;
        return DisplaySelector is not null ? DisplaySelector(item) : item.ToString() ?? string.Empty;
    }

    private void OnSearchGotFocus(object? sender, FocusChangedEventArgs e)
    {
        RefreshFilter(showAll: true);
        IsDropDownOpen = true;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressSearchUpdate)
            return;

        RefreshFilter(showAll: false);
        if (!IsDropDownOpen)
            IsDropDownOpen = true;
        UpdatePseudoClasses();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_listBox is null) return;

        switch (e.Key)
        {
            case Key.Down:
                if (!IsDropDownOpen)
                {
                    RefreshFilter(showAll: true);
                    IsDropDownOpen = true;
                }
                if (_filtered.Count > 0)
                {
                    var next = _listBox.SelectedIndex < _filtered.Count - 1 ? _listBox.SelectedIndex + 1 : 0;
                    _listBox.SelectedIndex = next;
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (_filtered.Count > 0)
                {
                    var prev = _listBox.SelectedIndex > 0 ? _listBox.SelectedIndex - 1 : _filtered.Count - 1;
                    _listBox.SelectedIndex = prev;
                }
                e.Handled = true;
                break;

            case Key.Enter:
                if (IsDropDownOpen && _listBox.SelectedItem is not null)
                {
                    CommitSelection(_listBox.SelectedItem);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                if (IsDropDownOpen)
                {
                    IsDropDownOpen = false;
                    SyncSearchTextFromSelected();
                    e.Handled = true;
                }
                break;
        }
    }

    private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_listBox?.SelectedItem is not null)
        {
            CommitSelection(_listBox.SelectedItem);
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        SelectedItem = null;
        _suppressSearchUpdate = true;
        if (_searchBox is not null) _searchBox.Text = string.Empty;
        _suppressSearchUpdate = false;
        RefreshFilter(showAll: true);
        UpdatePseudoClasses();
        _searchBox?.Focus();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (!IsDropDownOpen)
        {
            RefreshFilter(showAll: true);
            IsDropDownOpen = true;
            _searchBox?.Focus();
        }
        else
        {
            IsDropDownOpen = false;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        SyncSearchTextFromSelected();
    }

    private void CommitSelection(object? item)
    {
        SelectedItem = item;
        SyncSearchTextFromSelected();
        IsDropDownOpen = false;
        SelectionChanged?.Invoke(this, item);
    }

    private void OnSelectedItemChanged()
    {
        SyncSearchTextFromSelected();
        UpdatePseudoClasses();
    }

    private void SyncSearchTextFromSelected()
    {
        if (_searchBox is null) return;
        _suppressSearchUpdate = true;
        _searchBox.Text = FormatItem(SelectedItem);
        _suppressSearchUpdate = false;
    }

    private void RefreshFilter(bool showAll)
    {
        var query = showAll ? null : _searchBox?.Text?.Trim();
        var hasQuery = !string.IsNullOrEmpty(query);
        var next = new List<object>();

        if (ItemsSource is not null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is null) continue;
                if (!hasQuery || FormatItem(item).Contains(query!, StringComparison.OrdinalIgnoreCase))
                    next.Add(item);
            }
        }
        _filtered.ReplaceAll(next);

        if (_emptyLabel is not null)
            _emptyLabel.IsVisible = _filtered.Count == 0;

        if (_listBox is not null)
        {
            if (SelectedItem is not null && _filtered.Contains(SelectedItem))
                _listBox.SelectedItem = SelectedItem;
            else if (_filtered.Count > 0)
                _listBox.SelectedIndex = 0;
        }
    }

    private void OnOpenChanged()
    {
        UpdatePseudoClasses();
        var target = IsDropDownOpen ? 180.0 : 0.0;
        if (VelaMotionOptions.ReducedMotion)
        {
            _chevronAngle.Jump(target);
            if (_chevronRotate is not null) _chevronRotate.Angle = target;
            return;
        }
        _chevronAngle.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _chevronAngle.Step(dt);
        if (_chevronRotate is not null)
            _chevronRotate.Angle = _chevronAngle.Value;
        return active;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":open", IsDropDownOpen);
        PseudoClasses.Set(":has-value", SelectedItem is not null || !string.IsNullOrEmpty(_searchBox?.Text));
    }
}
