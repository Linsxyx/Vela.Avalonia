using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Visual surface style for <see cref="VelaListBox"/>.
/// </summary>
public enum VelaListBoxVariant
{
    /// <summary>Rounded bordered card surface matching Vela panels and data views.</summary>
    Card,

    /// <summary>Borderless transparent list ideal for sidebars, drawers, popups, and split panes.</summary>
    Plain,

    /// <summary>Muted recessed surface where hovered and selected rows pop softly against the track.</summary>
    Inset,
}

/// <summary>
/// Styled item container for <see cref="VelaListBox"/>.
/// Supports both standard <see cref="ContentControl.Content"/> / <see cref="ItemsControl.ItemTemplate"/>
/// and optional structured <see cref="Icon"/>, <see cref="Description"/>, and <see cref="Badge"/> slots.
/// </summary>
[PseudoClasses(":compact", ":comfortable", ":inset", ":selection-indicator", ":checkmark", ":divider")]
public class VelaListBoxItem : ListBoxItem
{
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaListBoxItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaListBoxItem, string?>(nameof(Description));

    public static readonly StyledProperty<object?> BadgeProperty =
        AvaloniaProperty.Register<VelaListBoxItem, object?>(nameof(Badge));

    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaListBoxItem);

    internal void SyncOwnerState(
        VelaListBoxVariant variant,
        VelaDataDensity density,
        bool showSelectionIndicator,
        bool showCheckmark,
        bool showDivider)
    {
        PseudoClasses.Set(":inset", variant == VelaListBoxVariant.Inset);
        PseudoClasses.Set(":compact", density == VelaDataDensity.Compact);
        PseudoClasses.Set(":comfortable", density == VelaDataDensity.Comfortable);
        PseudoClasses.Set(":selection-indicator", showSelectionIndicator);
        PseudoClasses.Set(":checkmark", showCheckmark);
        PseudoClasses.Set(":divider", showDivider);
    }

    internal void ClearOwnerState()
    {
        PseudoClasses.Set(":inset", false);
        PseudoClasses.Set(":compact", false);
        PseudoClasses.Set(":comfortable", false);
        PseudoClasses.Set(":selection-indicator", false);
        PseudoClasses.Set(":checkmark", false);
        PseudoClasses.Set(":divider", false);
    }

    protected override void OnDoubleTapped(TappedEventArgs e)
    {
        base.OnDoubleTapped(e);
        if (!IsEffectivelyEnabled)
            return;

        if (ItemsControl.ItemsControlFromItemContainer(this) is VelaListBox listBox)
        {
            var item = listBox.ItemFromContainer(this) ?? this;
            listBox.RaiseItemInvoked(item);
            e.Handled = true;
        }
    }
}

/// <summary>
/// Virtualized Vela ListBox control inheriting directly from Avalonia's <see cref="ListBox"/>.
/// Preserves full <see cref="VirtualizingStackPanel"/> virtualization, single/multiple selection,
/// keyboard navigation, and DataTemplates, while adding Vela surface variants, density presets,
/// selection indicator pills, optional checkmarks, row dividers, header/footer slots, empty states,
/// and <see cref="ItemInvoked"/> activation events.
/// </summary>
[PseudoClasses(":card", ":plain", ":inset", ":compact", ":standard", ":comfortable", ":empty")]
public class VelaListBox : ListBox
{
    public static readonly StyledProperty<VelaListBoxVariant> VariantProperty =
        AvaloniaProperty.Register<VelaListBox, VelaListBoxVariant>(nameof(Variant), VelaListBoxVariant.Card);

    public static readonly StyledProperty<VelaDataDensity> DensityProperty =
        AvaloniaProperty.Register<VelaListBox, VelaDataDensity>(nameof(Density), VelaDataDensity.Standard);

    public static readonly StyledProperty<bool> ShowSelectionIndicatorProperty =
        AvaloniaProperty.Register<VelaListBox, bool>(nameof(ShowSelectionIndicator), true);

    public static readonly StyledProperty<bool> ShowCheckmarksProperty =
        AvaloniaProperty.Register<VelaListBox, bool>(nameof(ShowCheckmarks));

    public static readonly StyledProperty<bool> ShowDividersProperty =
        AvaloniaProperty.Register<VelaListBox, bool>(nameof(ShowDividers));

    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<VelaListBox, object?>(nameof(Header));

    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty =
        AvaloniaProperty.Register<VelaListBox, IDataTemplate?>(nameof(HeaderTemplate));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<VelaListBox, object?>(nameof(Footer));

    public static readonly StyledProperty<IDataTemplate?> FooterTemplateProperty =
        AvaloniaProperty.Register<VelaListBox, IDataTemplate?>(nameof(FooterTemplate));

    public static readonly StyledProperty<object?> EmptyContentProperty =
        AvaloniaProperty.Register<VelaListBox, object?>(nameof(EmptyContent));

    public VelaListBoxVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public VelaDataDensity Density
    {
        get => GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    public bool ShowSelectionIndicator
    {
        get => GetValue(ShowSelectionIndicatorProperty);
        set => SetValue(ShowSelectionIndicatorProperty, value);
    }

    public bool ShowCheckmarks
    {
        get => GetValue(ShowCheckmarksProperty);
        set => SetValue(ShowCheckmarksProperty, value);
    }

    public bool ShowDividers
    {
        get => GetValue(ShowDividersProperty);
        set => SetValue(ShowDividersProperty, value);
    }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public IDataTemplate? FooterTemplate
    {
        get => GetValue(FooterTemplateProperty);
        set => SetValue(FooterTemplateProperty, value);
    }

    public object? EmptyContent
    {
        get => GetValue(EmptyContentProperty);
        set => SetValue(EmptyContentProperty, value);
    }

    /// <summary>
    /// Raised when an item is double-clicked or activated via the Enter key.
    /// </summary>
    public event EventHandler<VelaDataItemEventArgs>? ItemInvoked;

    protected override Type StyleKeyOverride => typeof(VelaListBox);

    private ContentPresenter? _emptyHost;

    static VelaListBox()
    {
        VariantProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.OnVisualOptionChanged());
        DensityProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.OnVisualOptionChanged());
        ShowSelectionIndicatorProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.SyncAllContainers());
        ShowCheckmarksProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.SyncAllContainers());
        ShowDividersProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.SyncAllContainers());
        EmptyContentProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.UpdateEmptyState());
        ItemCountProperty.Changed.AddClassHandler<VelaListBox>((lb, _) => lb.OnItemCountChanged());
    }

    public VelaListBox()
    {
        UpdatePseudoClasses();
        UpdateEmptyState();
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        => new VelaListBoxItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<VelaListBoxItem>(item, out recycleKey);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is VelaListBoxItem listItem)
            SyncItemVisualState(listItem, index);
    }

    protected override void ClearContainerForItemOverride(Control container)
    {
        base.ClearContainerForItemOverride(container);
        if (container is VelaListBoxItem listItem)
            listItem.ClearOwnerState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _emptyHost = e.NameScope.Find<ContentPresenter>("PART_EmptyHost");
        UpdatePseudoClasses();
        UpdateEmptyState();
        SyncAllContainers();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SelectedItem is not null && IsEffectivelyEnabled)
        {
            RaiseItemInvoked(SelectedItem);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    internal void RaiseItemInvoked(object item)
        => ItemInvoked?.Invoke(this, new VelaDataItemEventArgs(item));

    private void OnVisualOptionChanged()
    {
        UpdatePseudoClasses();
        SyncAllContainers();
    }

    private void OnItemCountChanged()
    {
        UpdateEmptyState();
        if (ShowDividers)
            SyncAllContainers();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":card", Variant == VelaListBoxVariant.Card);
        PseudoClasses.Set(":plain", Variant == VelaListBoxVariant.Plain);
        PseudoClasses.Set(":inset", Variant == VelaListBoxVariant.Inset);
        PseudoClasses.Set(":compact", Density == VelaDataDensity.Compact);
        PseudoClasses.Set(":standard", Density == VelaDataDensity.Standard);
        PseudoClasses.Set(":comfortable", Density == VelaDataDensity.Comfortable);
    }

    private void UpdateEmptyState()
    {
        var isEmpty = ItemCount == 0;
        PseudoClasses.Set(":empty", isEmpty);
        if (_emptyHost is not null)
            _emptyHost.IsVisible = isEmpty && EmptyContent is not null;
    }

    private void SyncAllContainers()
    {
        var count = ItemCount;
        foreach (var container in GetRealizedContainers())
        {
            if (container is VelaListBoxItem listItem)
            {
                var index = IndexFromContainer(listItem);
                SyncItemVisualState(listItem, index, count);
            }
        }
    }

    private void SyncItemVisualState(VelaListBoxItem item, int index)
        => SyncItemVisualState(item, index, ItemCount);

    private void SyncItemVisualState(VelaListBoxItem item, int index, int totalCount)
    {
        var showDivider = ShowDividers && index >= 0 && index < totalCount - 1;
        item.SyncOwnerState(Variant, Density, ShowSelectionIndicator, ShowCheckmarks, showDivider);
    }
}
