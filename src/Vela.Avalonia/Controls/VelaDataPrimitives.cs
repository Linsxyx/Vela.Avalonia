using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Row/node selection mode for <see cref="VelaTreeView"/>, <see cref="VelaDataGrid"/>, and <see cref="VelaTreeTable"/>.
/// </summary>
public enum VelaSelectionMode
{
    None,
    Single,
    Multiple,
}

/// <summary>
/// Vertical density preset controlling row height and cell padding across Vela data views.
/// </summary>
public enum VelaDataDensity
{
    Compact,
    Standard,
    Comfortable,
}

/// <summary>
/// Sort direction applied to a column in <see cref="VelaDataGrid"/> or <see cref="VelaTreeTable"/>.
/// </summary>
public enum VelaSortDirection
{
    None,
    Ascending,
    Descending,
}

/// <summary>
/// Event arguments raised when a data view column sort changes.
/// </summary>
public sealed class VelaSortChangedEventArgs : EventArgs
{
    public VelaSortChangedEventArgs(VelaDataGridColumn? column, VelaSortDirection direction)
    {
        Column = column;
        Direction = direction;
    }

    public VelaDataGridColumn? Column { get; }
    public VelaSortDirection Direction { get; }
}

/// <summary>
/// Event arguments raised when a user interactively resizes a column.
/// </summary>
public sealed class VelaColumnResizedEventArgs : EventArgs
{
    public VelaColumnResizedEventArgs(VelaDataGridColumn column, double oldWidth, double newWidth)
    {
        Column = column;
        OldWidth = oldWidth;
        NewWidth = newWidth;
    }

    public VelaDataGridColumn Column { get; }
    public double OldWidth { get; }
    public double NewWidth { get; }
}

/// <summary>
/// Event arguments raised when a row or tree node is invoked (double-click or Enter).
/// </summary>
public sealed class VelaDataItemEventArgs : EventArgs
{
    public VelaDataItemEventArgs(object item)
    {
        Item = item;
    }

    public object Item { get; }
}

/// <summary>
/// Ready-to-use observable hierarchical node model for <see cref="VelaTreeView"/> and <see cref="VelaTreeTable"/>.
/// Supports optional icon, expanded icon, subtitle description, badge, tri-state check, and lazy <see cref="HasChildren"/>.
/// </summary>
public class VelaTreeNode : INotifyPropertyChanged
{
    private string? _header;
    private string? _description;
    private Geometry? _icon;
    private Geometry? _expandedIcon;
    private string? _badge;
    private VelaBadgeTone _badgeTone = VelaBadgeTone.Neutral;
    private object? _tag;
    private bool _isExpanded;
    private bool? _isChecked = false;
    private bool _isEnabled = true;
    private bool? _hasChildren;

    public VelaTreeNode()
    {
    }

    public VelaTreeNode(string header, Geometry? icon = null, string? badge = null, bool isExpanded = false)
    {
        _header = header;
        _icon = icon;
        _badge = badge;
        _isExpanded = isExpanded;
    }

    public string? Header
    {
        get => _header;
        set => SetField(ref _header, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public Geometry? Icon
    {
        get => _icon;
        set
        {
            if (SetField(ref _icon, value))
                OnPropertyChanged(nameof(EffectiveIcon));
        }
    }

    public Geometry? ExpandedIcon
    {
        get => _expandedIcon;
        set
        {
            if (SetField(ref _expandedIcon, value))
                OnPropertyChanged(nameof(EffectiveIcon));
        }
    }

    public Geometry? EffectiveIcon => IsExpanded && ExpandedIcon is not null ? ExpandedIcon : Icon;

    public string? Badge
    {
        get => _badge;
        set => SetField(ref _badge, value);
    }

    public VelaBadgeTone BadgeTone
    {
        get => _badgeTone;
        set => SetField(ref _badgeTone, value);
    }

    public object? Tag
    {
        get => _tag;
        set => SetField(ref _tag, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetField(ref _isExpanded, value))
                OnPropertyChanged(nameof(EffectiveIcon));
        }
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    /// <summary>
    /// Optional explicit override for lazy-loaded nodes. When <c>null</c>, inferred from <see cref="Children"/>.
    /// </summary>
    public bool? HasChildren
    {
        get => _hasChildren;
        set => SetField(ref _hasChildren, value);
    }

    [Content]
    public ObservableCollection<VelaTreeNode> Children { get; } = new();

    public bool ResolvedHasChildren => HasChildren ?? Children.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public override string ToString() => Header ?? string.Empty;
}

/// <summary>
/// Lightweight vector renderer for crisp 1px vertical ancestor indent guide lines
/// used in <see cref="VelaTreeView"/> and <see cref="VelaTreeTable"/>.
/// </summary>
public sealed class VelaTreeIndentGuides : Control
{
    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<VelaTreeIndentGuides, int>(nameof(Depth));

    public static readonly StyledProperty<double> IndentSizeProperty =
        AvaloniaProperty.Register<VelaTreeIndentGuides, double>(nameof(IndentSize), 20.0);

    public static readonly StyledProperty<bool> ShowGuidesProperty =
        AvaloniaProperty.Register<VelaTreeIndentGuides, bool>(nameof(ShowGuides), true);

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<VelaTreeIndentGuides, IBrush?>(nameof(LineBrush));

    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }
    public double IndentSize { get => GetValue(IndentSizeProperty); set => SetValue(IndentSizeProperty, value); }
    public bool ShowGuides { get => GetValue(ShowGuidesProperty); set => SetValue(ShowGuidesProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    static VelaTreeIndentGuides()
    {
        AffectsMeasure<VelaTreeIndentGuides>(DepthProperty, IndentSizeProperty);
        AffectsRender<VelaTreeIndentGuides>(DepthProperty, IndentSizeProperty, ShowGuidesProperty, LineBrushProperty);
    }

    public VelaTreeIndentGuides()
    {
        IsHitTestVisible = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var depth = Math.Max(0, Depth);
        var indent = Math.Max(0.0, IndentSize);
        return new Size(depth * indent, 0);
    }

    public override void Render(DrawingContext context)
    {
        if (!ShowGuides || Depth <= 0 || LineBrush is null || Bounds.Height <= 0)
            return;

        var indent = Math.Max(4.0, IndentSize);
        var pen = new Pen(LineBrush, 1.0);
        for (var level = 0; level < Depth; level++)
        {
            var x = Math.Floor(level * indent + indent * 0.5) + 0.5;
            context.DrawLine(pen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }
}

/// <summary>
/// Interactive spring-rotating disclosure chevron (0 deg collapsed -> 90 deg expanded)
/// designed to recycle cleanly inside virtualized tree rows without phantom animations.
/// </summary>
[PseudoClasses(":expanded", ":pointerover")]
public sealed class VelaTreeChevron : Control
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<VelaTreeChevron, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> HasChildrenProperty =
        AvaloniaProperty.Register<VelaTreeChevron, bool>(nameof(HasChildren));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<VelaTreeChevron, IBrush?>(nameof(Foreground));

    public static readonly StyledProperty<IBrush?> HoverBackgroundProperty =
        AvaloniaProperty.Register<VelaTreeChevron, IBrush?>(nameof(HoverBackground));

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool HasChildren { get => GetValue(HasChildrenProperty); set => SetValue(HasChildrenProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? HoverBackground { get => GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }

    public event EventHandler? Toggled;

    private readonly SpringChannel _angleSpring = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private bool _isPointerOver;

    static VelaTreeChevron()
    {
        AffectsRender<VelaTreeChevron>(IsExpandedProperty, HasChildrenProperty, ForegroundProperty, HoverBackgroundProperty);
        IsExpandedProperty.Changed.AddClassHandler<VelaTreeChevron>((c, _) => c.OnIsExpandedChanged());
        HasChildrenProperty.Changed.AddClassHandler<VelaTreeChevron>((c, _) => c.UpdateCursor());
    }

    public VelaTreeChevron()
    {
        Width = 22;
        Height = 22;
        VerticalAlignment = VerticalAlignment.Center;
        _runner = new SpringRunner(this, OnTick);
    }

    /// <summary>
    /// Updates expanded state with explicit animation control (pass <c>false</c> during container recycling).
    /// </summary>
    public void SetExpanded(bool expanded, bool animate)
    {
        var target = expanded ? 90.0 : 0.0;
        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _angleSpring.Jump(target);
            _runner.Stop();
            SetCurrentValue(IsExpandedProperty, expanded);
            PseudoClasses.Set(":expanded", expanded);
            InvalidateVisual();
            return;
        }

        SetCurrentValue(IsExpandedProperty, expanded);
    }

    private void OnIsExpandedChanged()
    {
        PseudoClasses.Set(":expanded", IsExpanded);
        var target = IsExpanded ? 90.0 : 0.0;

        if (VelaMotionOptions.ReducedMotion)
        {
            _angleSpring.Jump(target);
            InvalidateVisual();
            return;
        }

        _angleSpring.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void UpdateCursor()
    {
        Cursor = HasChildren ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        IsHitTestVisible = HasChildren;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        PseudoClasses.Set(":pointerover", true);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        PseudoClasses.Set(":pointerover", false);
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!HasChildren || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Toggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private bool OnTick(double dt)
    {
        var active = _angleSpring.Step(dt);
        InvalidateVisual();
        return active;
    }

    public override void Render(DrawingContext context)
    {
        if (!HasChildren)
            return;

        var rect = new Rect(Bounds.Size);
        if (_isPointerOver && HoverBackground is not null)
        {
            context.DrawRectangle(HoverBackground, null, rect.Deflate(1), 6, 6);
        }

        if (Foreground is null)
            return;

        var cx = rect.Width * 0.5;
        var cy = rect.Height * 0.5;
        var pen = new Pen(Foreground, 1.55, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var radians = _angleSpring.Value * Math.PI / 180.0;
        var transform = Matrix.CreateTranslation(-cx, -cy)
                        * Matrix.CreateRotation(radians)
                        * Matrix.CreateTranslation(cx, cy);

        using (context.PushTransform(transform))
        {
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(cx - 2.2, cy - 4.0), isFilled: false);
                ctx.LineTo(new Point(cx + 2.4, cy));
                ctx.LineTo(new Point(cx - 2.2, cy + 4.0));
                ctx.EndFigure(isClosed: false);
            }
            context.DrawGeometry(null, pen, geom);
        }
    }
}

/// <summary>
/// Input specification for a single column in <see cref="VelaColumnLayoutEngine"/>.
/// </summary>
public readonly record struct VelaColumnSpec(
    GridLength Width,
    double MinWidth,
    double MaxWidth,
    double? ExplicitPixelWidth = null,
    double AutoWidth = 140.0);

/// <summary>
/// Deterministic pixel / Auto / Star column width solver shared by
/// <see cref="VelaDataGrid"/> and <see cref="VelaTreeTable"/>.
/// </summary>
public static class VelaColumnLayoutEngine
{
    public static double[] ResolveWidths(IReadOnlyList<VelaColumnSpec> columns, double availableWidth)
    {
        var count = columns.Count;
        var result = new double[count];
        if (count == 0)
            return result;

        var safeAvailable = double.IsFinite(availableWidth) && availableWidth > 0 ? availableWidth : 0.0;
        var fixedUsed = 0.0;
        var starIndices = new List<int>(count);

        for (var i = 0; i < count; i++)
        {
            var spec = columns[i];
            var minW = Math.Max(24.0, double.IsFinite(spec.MinWidth) ? spec.MinWidth : 24.0);
            var maxW = double.IsFinite(spec.MaxWidth) && spec.MaxWidth >= minW ? spec.MaxWidth : double.PositiveInfinity;

            if (spec.ExplicitPixelWidth.HasValue)
            {
                var w = Math.Clamp(spec.ExplicitPixelWidth.Value, minW, maxW);
                result[i] = w;
                fixedUsed += w;
            }
            else if (spec.Width.IsAbsolute)
            {
                var w = Math.Clamp(spec.Width.Value, minW, maxW);
                result[i] = w;
                fixedUsed += w;
            }
            else if (spec.Width.IsAuto)
            {
                var w = Math.Clamp(spec.AutoWidth, minW, maxW);
                result[i] = w;
                fixedUsed += w;
            }
            else
            {
                starIndices.Add(i);
            }
        }

        if (starIndices.Count == 0)
            return result;

        var remaining = Math.Max(0.0, safeAvailable - fixedUsed);
        var activeStars = new List<int>(starIndices);

        // Iterative clamp distribution: lock any star column that hits MinWidth or MaxWidth
        // and redistribute remaining space proportionally to unlocked star columns.
        while (activeStars.Count > 0)
        {
            var totalWeight = 0.0;
            foreach (var idx in activeStars)
                totalWeight += Math.Max(0.0001, columns[idx].Width.Value);

            var clampedAny = false;
            for (var i = activeStars.Count - 1; i >= 0; i--)
            {
                var idx = activeStars[i];
                var spec = columns[idx];
                var minW = Math.Max(24.0, double.IsFinite(spec.MinWidth) ? spec.MinWidth : 24.0);
                var maxW = double.IsFinite(spec.MaxWidth) && spec.MaxWidth >= minW ? spec.MaxWidth : double.PositiveInfinity;
                var weight = Math.Max(0.0001, spec.Width.Value);
                var proposed = totalWeight > 0 ? remaining * (weight / totalWeight) : minW;

                if (proposed < minW)
                {
                    result[idx] = minW;
                    remaining = Math.Max(0.0, remaining - minW);
                    activeStars.RemoveAt(i);
                    clampedAny = true;
                }
                else if (proposed > maxW)
                {
                    result[idx] = maxW;
                    remaining = Math.Max(0.0, remaining - maxW);
                    activeStars.RemoveAt(i);
                    clampedAny = true;
                }
            }

            if (!clampedAny)
            {
                foreach (var idx in activeStars)
                {
                    var weight = Math.Max(0.0001, columns[idx].Width.Value);
                    result[idx] = totalWeight > 0 ? remaining * (weight / totalWeight) : Math.Max(24.0, columns[idx].MinWidth);
                }
                break;
            }
        }

        return result;
    }

    public static double GetRowHeight(VelaDataDensity density, bool isTreeView = false) => (density, isTreeView) switch
    {
        (VelaDataDensity.Compact, true) => 30.0,
        (VelaDataDensity.Standard, true) => 36.0,
        (VelaDataDensity.Comfortable, true) => 42.0,
        (VelaDataDensity.Compact, false) => 34.0,
        (VelaDataDensity.Standard, false) => 42.0,
        (VelaDataDensity.Comfortable, false) => 50.0,
        _ => 38.0,
    };
}

/// <summary>
/// Zero-reflection, Native AOT-safe value comparison utility for sorting data grid and tree table columns.
/// </summary>
public static class VelaDataComparer
{
    public static int Compare(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a is null)
            return -1;
        if (b is null)
            return 1;

        if (a is string sa && b is string sb)
            return StringComparer.OrdinalIgnoreCase.Compare(sa, sb);

        if (TryToDouble(a, out var da) && TryToDouble(b, out var db))
            return da.CompareTo(db);

        if (a is DateTime dta && b is DateTime dtb)
            return dta.CompareTo(dtb);

        if (a is DateTimeOffset dtoa && b is DateTimeOffset dtob)
            return dtoa.CompareTo(dtob);

        if (a is bool ba && b is bool bb)
            return ba.CompareTo(bb);

        if (a.GetType() == b.GetType() && a is IComparable comparable)
            return comparable.CompareTo(b);

        return StringComparer.OrdinalIgnoreCase.Compare(
            Convert.ToString(a, CultureInfo.CurrentCulture),
            Convert.ToString(b, CultureInfo.CurrentCulture));
    }

    private static bool TryToDouble(object value, out double number)
    {
        switch (value)
        {
            case byte v: number = v; return true;
            case sbyte v: number = v; return true;
            case short v: number = v; return true;
            case ushort v: number = v; return true;
            case int v: number = v; return true;
            case uint v: number = v; return true;
            case long v: number = v; return true;
            case ulong v: number = v; return true;
            case float v: number = v; return true;
            case double v: number = v; return true;
            case decimal v: number = (double)v; return true;
            default: number = 0; return false;
        }
    }
}

/// <summary>
/// Base column definition shared by <see cref="VelaDataGrid"/> and <see cref="VelaTreeTable"/>.
/// Designed for zero-reflection Native AOT execution with compiled bindings or strongly-typed delegates.
/// </summary>
public abstract class VelaDataGridColumn : AvaloniaObject
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, object?>(nameof(Header));

    public static readonly StyledProperty<GridLength> WidthProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, GridLength>(nameof(Width), new GridLength(1, GridUnitType.Star));

    public static readonly StyledProperty<double> MinWidthProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, double>(nameof(MinWidth), 72.0);

    public static readonly StyledProperty<double> MaxWidthProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, double>(nameof(MaxWidth), double.PositiveInfinity);

    public static readonly StyledProperty<bool> CanSortProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, bool>(nameof(CanSort), true);

    public static readonly StyledProperty<bool> CanResizeProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, bool>(nameof(CanResize), true);

    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, bool>(nameof(IsVisible), true);

    public static readonly StyledProperty<HorizontalAlignment> HorizontalAlignmentProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, HorizontalAlignment>(nameof(HorizontalAlignment), HorizontalAlignment.Left);

    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<VelaDataGridColumn, object?>(nameof(Tag));

    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    public GridLength Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    public double MinWidth { get => GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }
    public double MaxWidth { get => GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }
    public bool CanSort { get => GetValue(CanSortProperty); set => SetValue(CanSortProperty, value); }
    public bool CanResize { get => GetValue(CanResizeProperty); set => SetValue(CanResizeProperty, value); }
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
    public HorizontalAlignment HorizontalAlignment { get => GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty, value); }
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>Optional AOT-safe delegate returning the sort key for a row item.</summary>
    public Func<object, object?>? SortKeySelector { get; set; }

    /// <summary>Optional custom comparer applied to extracted sort keys.</summary>
    public IComparer<object?>? SortComparer { get; set; }

    /// <summary>Optional explicit pixel width set when a user interactively resizes the column.</summary>
    internal double? ResizedWidth { get; set; }

    /// <summary>Resolved pixel width computed by <see cref="VelaColumnLayoutEngine"/>.</summary>
    public double ActualWidth { get; internal set; }

    /// <summary>Current sort direction on this column.</summary>
    public VelaSortDirection SortDirection { get; internal set; }

    internal event EventHandler? LayoutInvalidated;

    static VelaDataGridColumn()
    {
        HeaderProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        WidthProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        MinWidthProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        MaxWidthProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        IsVisibleProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        CanSortProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
        CanResizeProperty.Changed.AddClassHandler<VelaDataGridColumn>((c, _) => c.RaiseLayoutInvalidated());
    }

    internal void RaiseLayoutInvalidated() => LayoutInvalidated?.Invoke(this, EventArgs.Empty);

    internal VelaColumnSpec ToSpec() => new(Width, MinWidth, MaxWidth, ResizedWidth, AutoWidth: 140.0);

    internal abstract Control CreateCellContent(object item);

    internal virtual object? GetSortValue(object item) => SortKeySelector?.Invoke(item);

    internal virtual string? GetSearchableText(object item)
        => Convert.ToString(GetSortValue(item), CultureInfo.CurrentCulture);

    internal int CompareItems(object a, object b)
    {
        var ka = GetSortValue(a);
        var kb = GetSortValue(b);
        return SortComparer is not null ? SortComparer.Compare(ka, kb) : VelaDataComparer.Compare(ka, kb);
    }

    protected static object? EvaluateBindingOnce(BindingBase binding, object item)
    {
        var probe = new ContentControl { DataContext = item };
        using var sub = probe.Bind(ContentControl.ContentProperty, binding);
        return probe.Content;
    }
}

/// <summary>
/// Text column supporting compiled XAML <see cref="Binding"/>, zero-reflection <see cref="ValueSelector"/>,
/// <see cref="StringFormat"/>, monospace tabular numerals, and muted metadata styling.
/// </summary>
public class VelaDataGridTextColumn : VelaDataGridColumn
{
    public static readonly StyledProperty<string?> StringFormatProperty =
        AvaloniaProperty.Register<VelaDataGridTextColumn, string?>(nameof(StringFormat));

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<VelaDataGridTextColumn, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    public static readonly StyledProperty<bool> IsMonospaceProperty =
        AvaloniaProperty.Register<VelaDataGridTextColumn, bool>(nameof(IsMonospace));

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<VelaDataGridTextColumn, bool>(nameof(IsMuted));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<VelaDataGridTextColumn, IBrush?>(nameof(Foreground));

    [AssignBinding]
    public BindingBase? Binding { get; set; }

    public Func<object, object?>? ValueSelector { get; set; }

    public string? StringFormat { get => GetValue(StringFormatProperty); set => SetValue(StringFormatProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public bool IsMonospace { get => GetValue(IsMonospaceProperty); set => SetValue(IsMonospaceProperty, value); }
    public bool IsMuted { get => GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private static readonly FontFamily MonospaceFamily = new("JetBrains Mono, SF Mono, Menlo, Consolas, monospace");

    internal override Control CreateCellContent(object item)
    {
        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = FontWeight,
            FontSize = 13,
        };

        if (IsMonospace)
            tb.FontFamily = MonospaceFamily;

        if (Foreground is not null)
        {
            tb.Foreground = Foreground;
        }
        else
        {
            var key = IsMuted ? "VelaTextSecondaryBrush" : "VelaForegroundBrush";
            tb[!TextBlock.ForegroundProperty] = tb.GetResourceObservable(key).ToBinding();
        }

        if (ValueSelector is not null)
        {
            var raw = ValueSelector(item);
            tb.Text = FormatValue(raw);
        }
        else if (Binding is not null)
        {
            tb.DataContext = item;
            tb.Bind(TextBlock.TextProperty, Binding);
        }
        else
        {
            tb.Text = FormatValue(item);
        }

        return tb;
    }

    internal override object? GetSortValue(object item)
    {
        if (SortKeySelector is not null)
            return SortKeySelector(item);
        if (ValueSelector is not null)
            return ValueSelector(item);
        if (Binding is not null)
            return EvaluateBindingOnce(Binding, item);
        return item;
    }

    internal override string? GetSearchableText(object item)
    {
        if (ValueSelector is not null)
            return FormatValue(ValueSelector(item));
        if (Binding is not null)
            return FormatValue(EvaluateBindingOnce(Binding, item));
        return Convert.ToString(item, CultureInfo.CurrentCulture);
    }

    private string FormatValue(object? raw)
    {
        if (raw is null)
            return string.Empty;
        if (!string.IsNullOrEmpty(StringFormat))
            return string.Format(CultureInfo.CurrentCulture, StringFormat, raw);
        return Convert.ToString(raw, CultureInfo.CurrentCulture) ?? string.Empty;
    }
}

/// <summary>
/// Semantic status badge column that renders a native <see cref="VelaBadge"/> per cell.
/// </summary>
public class VelaDataGridBadgeColumn : VelaDataGridColumn
{
    public static readonly StyledProperty<VelaBadgeTone> DefaultToneProperty =
        AvaloniaProperty.Register<VelaDataGridBadgeColumn, VelaBadgeTone>(nameof(DefaultTone), VelaBadgeTone.Neutral);

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<VelaDataGridBadgeColumn, bool>(nameof(ShowDot));

    public static readonly StyledProperty<bool> IsPillProperty =
        AvaloniaProperty.Register<VelaDataGridBadgeColumn, bool>(nameof(IsPill), true);

    public static readonly StyledProperty<bool> IsOutlineProperty =
        AvaloniaProperty.Register<VelaDataGridBadgeColumn, bool>(nameof(IsOutline));

    [AssignBinding]
    public BindingBase? Binding { get; set; }

    public Func<object, object?>? ValueSelector { get; set; }
    public Func<object, VelaBadgeTone>? ToneSelector { get; set; }

    public VelaBadgeTone DefaultTone { get => GetValue(DefaultToneProperty); set => SetValue(DefaultToneProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
    public bool IsPill { get => GetValue(IsPillProperty); set => SetValue(IsPillProperty, value); }
    public bool IsOutline { get => GetValue(IsOutlineProperty); set => SetValue(IsOutlineProperty, value); }

    internal override Control CreateCellContent(object item)
    {
        var badge = new VelaBadge
        {
            Size = VelaControlSize.Small,
            Tone = ToneSelector?.Invoke(item) ?? DefaultTone,
            ShowDot = ShowDot,
            IsPill = IsPill,
            IsOutline = IsOutline,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment,
        };

        if (ValueSelector is not null)
        {
            badge.Content = ValueSelector(item);
        }
        else if (Binding is not null)
        {
            badge.DataContext = item;
            badge.Bind(ContentControl.ContentProperty, Binding);
        }

        return badge;
    }

    internal override object? GetSortValue(object item)
    {
        if (SortKeySelector is not null)
            return SortKeySelector(item);
        if (ValueSelector is not null)
            return ValueSelector(item);
        if (Binding is not null)
            return EvaluateBindingOnce(Binding, item);
        return null;
    }
}

/// <summary>
/// Interactive boolean column rendering a native <see cref="VelaCheckBox"/> per cell.
/// </summary>
public class VelaDataGridCheckBoxColumn : VelaDataGridColumn
{
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<VelaDataGridCheckBoxColumn, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<VelaDataGridCheckBoxColumn, bool>(nameof(IsThreeState));

    public Func<object, bool?>? ValueSelector { get; set; }
    public Action<object, bool?>? ValueSetter { get; set; }

    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool IsThreeState { get => GetValue(IsThreeStateProperty); set => SetValue(IsThreeStateProperty, value); }

    public VelaDataGridCheckBoxColumn()
    {
        Width = new GridLength(76);
        MinWidth = 52;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    internal override Control CreateCellContent(object item)
    {
        var cb = new VelaCheckBox
        {
            IsThreeState = IsThreeState,
            IsEnabled = !IsReadOnly,
            IsChecked = ValueSelector?.Invoke(item) ?? false,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment,
        };

        if (!IsReadOnly && ValueSetter is not null)
        {
            cb.IsCheckedChanged += (_, _) => ValueSetter(item, cb.IsChecked);
        }

        return cb;
    }

    internal override object? GetSortValue(object item)
    {
        if (SortKeySelector is not null)
            return SortKeySelector(item);
        return ValueSelector?.Invoke(item) ?? false;
    }
}

/// <summary>
/// Custom templated column supporting any Avalonia <see cref="IDataTemplate"/> with full compiled bindings.
/// </summary>
public class VelaDataGridTemplateColumn : VelaDataGridColumn
{
    public static readonly StyledProperty<IDataTemplate?> CellTemplateProperty =
        AvaloniaProperty.Register<VelaDataGridTemplateColumn, IDataTemplate?>(nameof(CellTemplate));

    [Content]
    public IDataTemplate? CellTemplate
    {
        get => GetValue(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    /// <summary>Optional delegate supplying searchable text for built-in filtering.</summary>
    public Func<object, string?>? SearchTextSelector { get; set; }

    internal override Control CreateCellContent(object item)
    {
        if (CellTemplate is not null && CellTemplate.Build(item) is Control built)
        {
            built.DataContext = item;
            built.VerticalAlignment = VerticalAlignment.Center;
            return built;
        }

        return new ContentPresenter
        {
            Content = item,
            ContentTemplate = CellTemplate,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment,
        };
    }

    internal override string? GetSearchableText(object item)
        => SearchTextSelector?.Invoke(item) ?? base.GetSearchableText(item);
}
