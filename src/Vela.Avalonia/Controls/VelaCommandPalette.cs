using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// A searchable command entry inside <see cref="VelaCommandPalette"/>.
/// </summary>
public sealed class VelaCommandItem
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string Group { get; init; } = "Commands";
    public Geometry? Icon { get; init; }
    public string? Shortcut { get; init; }
    public string? Keywords { get; init; }
    public Action? Execute { get; init; }
}

/// <summary>
/// Port of Arc UI's CommandPalette (⌘K / Ctrl+K):
/// - Global ⌘K / Ctrl+K shortcut listener (configurable via <see cref="EnableGlobalShortcut"/>)
/// - Grouped real-time filtering, keyboard Up/Down/Enter/Escape navigation
/// - Spring-gliding row highlight tracking both keyboard and mouse hover
/// - Optional <see cref="UseLiquidGlass"/> optical glass mode
/// - Full AnimatePresence spring entrance &amp; exit lifecycle
/// </summary>
public class VelaCommandPalette : ContentControl
{
    public static readonly StyledProperty<bool> EnableGlobalShortcutProperty =
        AvaloniaProperty.Register<VelaCommandPalette, bool>(nameof(EnableGlobalShortcut), true);

    public static readonly StyledProperty<bool> UseLiquidGlassProperty =
        AvaloniaProperty.Register<VelaCommandPalette, bool>(nameof(UseLiquidGlass), false);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<VelaCommandPalette, string>(nameof(PlaceholderText), "Type a command or search...");

    public bool EnableGlobalShortcut { get => GetValue(EnableGlobalShortcutProperty); set => SetValue(EnableGlobalShortcutProperty, value); }
    public bool UseLiquidGlass { get => GetValue(UseLiquidGlassProperty); set => SetValue(UseLiquidGlassProperty, value); }
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    public ObservableCollection<VelaCommandItem> Commands { get; } = new();

    private readonly SpringChannel _scrimOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _panelOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _panelScale = new(0.97, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _panelY = new(-10.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Grid? _overlayLayer;
    private Border? _scrim;
    private Panel? _panelWrapper;
    private Border? _solidSurface;
    private VelaLiquidGlass? _glassSurface;
    private TextBox? _searchBox;
    private Grid? _resultsTrack;
    private StackPanel? _resultsHost;
    private Border? _highlight;
    private TextBlock? _emptyState;

    private ScaleTransform? _panelScaleTransform;
    private TranslateTransform? _panelTranslateTransform;
    private TranslateTransform? _hlTranslate;

    private readonly List<(VelaCommandItem Item, Border Row)> _visibleRows = new();
    private int _activeRowIndex = -1;
    private bool _isOpen;
    private bool _hlPlaced;

    public bool IsPaletteOpen => _isOpen;

    static VelaCommandPalette()
    {
        UseLiquidGlassProperty.Changed.AddClassHandler<VelaCommandPalette>((p, _) => p.SyncSurfaceMode());
    }

    public VelaCommandPalette()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    private TopLevel? _attachedTopLevel;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureOverlayInitialized();
        if (_attachedTopLevel is not null)
        {
            _attachedTopLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
        {
            _attachedTopLevel = topLevel;
            topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (_attachedTopLevel is not null)
        {
            _attachedTopLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
    }

    public void Open()
    {
        EnsureOverlayInitialized();
        if (_overlayLayer is null) return;

        _isOpen = true;
        _hlPlaced = false;
        SyncSurfaceMode();

        if (_searchBox is not null)
            _searchBox.Text = string.Empty;

        RebuildResults();

        _overlayLayer.IsVisible = true;
        _overlayLayer.IsHitTestVisible = true;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(1.0);
            _panelOpacity.Jump(1.0);
            _panelScale.Jump(1.0);
            _panelY.Jump(0.0);
            ApplyPanelVisuals();
        }
        else
        {
            _scrimOpacity.Jump(0.0);
            _panelOpacity.Jump(0.0);
            _panelScale.Jump(0.97);
            _panelY.Jump(-10.0);
            ApplyPanelVisuals();

            _scrimOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _panelOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _panelScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _panelY.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        Dispatcher.UIThread.Post(() =>
        {
            _searchBox?.Focus();
            SetActiveIndex(_visibleRows.Count > 0 ? 0 : -1, animate: false);
        }, DispatcherPriority.Input);
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        if (_overlayLayer is not null)
            _overlayLayer.IsHitTestVisible = false;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(0.0);
            _panelOpacity.Jump(0.0);
            if (_overlayLayer is not null) _overlayLayer.IsVisible = false;
            return;
        }

        _scrimOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _panelOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _panelScale.SetTarget(0.98, VelaMotionTokens.Springs.Snappy);
        _panelY.SetTarget(-6.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    public void Toggle()
    {
        if (_isOpen) Close();
        else Open();
    }

    private void EnsureOverlayInitialized()
    {
        if (_overlayLayer is not null) return;

        Template = new FuncControlTemplate<VelaCommandPalette>((host, _) =>
        {
            var rootGrid = new Grid();

            rootGrid.Children.Add(new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = host[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = host[!ContentTemplateProperty],
            });

            _scrim = new Border
            {
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaOverlayScrimBrush"),
                Opacity = 0,
            };
            _scrim.PointerPressed += (_, ev) =>
            {
                if (_isOpen)
                {
                    Close();
                    ev.Handled = true;
                }
            };

            var innerChrome = BuildPaletteContent();

            _solidSurface = new Border
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1),
                ClipToBounds = false,
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceRaisedBrush"),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                [!Border.BoxShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
            };

            _glassSurface = new VelaLiquidGlass
            {
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(0),
                BlurRadius = 14,
                BevelWidth = 18,
                Refraction = 0.65,
                Dispersion = 0.35,
                Specular = 0.85,
                IsVisible = false,
            };

            _panelScaleTransform = new ScaleTransform(0.97, 0.97);
            _panelTranslateTransform = new TranslateTransform(0, -10);

            _panelWrapper = new Panel
            {
                Width = 560,
                MaxHeight = 460,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(24, 96, 24, 24),
                RenderTransformOrigin = new RelativePoint(0.5, 0.0, RelativeUnit.Relative),
                RenderTransform = new TransformGroup { Children = { _panelScaleTransform, _panelTranslateTransform } },
                Opacity = 0,
                Children = { _solidSurface, _glassSurface, innerChrome },
            };

            _overlayLayer = new Grid
            {
                IsVisible = false,
                IsHitTestVisible = false,
                ZIndex = 1050,
                Children = { _scrim, _panelWrapper },
            };

            rootGrid.Children.Add(_overlayLayer);
            SyncSurfaceMode();
            return rootGrid;
        });
    }

    private Control BuildPaletteContent()
    {
        var dock = new DockPanel();

        // Search Row
        var searchIcon = new PathIcon
        {
            Width = 16,
            Height = 16,
            [!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconSearch"),
            [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
        };

        _searchBox = new TextBox
        {
            PlaceholderText = PlaceholderText,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (this.TryFindResource("VelaBareTextBoxTheme", ActualThemeVariant, out var bareObj) && bareObj is ControlTheme bareTheme)
            _searchBox.Theme = bareTheme;
        _searchBox.TextChanged += (_, _) =>
        {
            RebuildResults();
            Dispatcher.UIThread.Post(() => SetActiveIndex(_visibleRows.Count > 0 ? 0 : -1, animate: false), DispatcherPriority.Loaded);
        };
        _searchBox.KeyDown += OnSearchKeyDown;

        var escBadge = new Border
        {
            Padding = new Thickness(6, 3),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush"),
            Child = new TextBlock
            {
                Text = "ESC",
                FontSize = 10.5,
                FontWeight = FontWeight.Medium,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
            },
        };

        var searchGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10,
            MinHeight = 52,
        };
        searchGrid.Children.Add(searchIcon);
        Grid.SetColumn(_searchBox, 1);
        searchGrid.Children.Add(_searchBox);
        Grid.SetColumn(escBadge, 2);
        searchGrid.Children.Add(escBadge);

        var searchBar = new Border
        {
            Padding = new Thickness(16, 0),
            BorderThickness = new Thickness(0, 0, 0, 1),
            [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush"),
            Child = searchGrid,
        };
        DockPanel.SetDock(searchBar, Dock.Top);
        dock.Children.Add(searchBar);

        // Results area with gliding highlight
        _hlTranslate = new TranslateTransform();
        _highlight = new Border
        {
            Height = 38,
            CornerRadius = new CornerRadius(9),
            IsHitTestVisible = false,
            Opacity = 0,
            RenderTransform = _hlTranslate,
            [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceMutedBrush"),
        };

        var hlCanvas = new Canvas { IsHitTestVisible = false };
        hlCanvas.Children.Add(_highlight);

        _resultsHost = new StackPanel { Spacing = 2 };

        _emptyState = new TextBlock
        {
            Text = "No matching commands.",
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 28),
            IsVisible = false,
            [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
        };

        _resultsTrack = new Grid
        {
            Margin = new Thickness(8, 6, 8, 8),
            Children = { hlCanvas, _resultsHost, _emptyState },
        };

        dock.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _resultsTrack,
        });

        return dock;
    }

    private void SyncSurfaceMode()
    {
        if (_solidSurface is null || _glassSurface is null) return;
        _solidSurface.IsVisible = !UseLiquidGlass;
        _glassSurface.IsVisible = UseLiquidGlass;
    }

    private sealed class PooledCommandRow
    {
        public Border Root { get; }
        public PathIcon Icon { get; }
        public TextBlock Title { get; }
        public TextBlock Description { get; }
        public Border ShortcutBadge { get; }
        public TextBlock ShortcutText { get; }
        public int RowIndex;

        public PooledCommandRow(VelaCommandPalette owner)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 10,
            };

            Icon = new PathIcon
            {
                Width = 15,
                Height = 15,
                VerticalAlignment = VerticalAlignment.Center,
                [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
            };
            grid.Children.Add(Icon);

            Title = new TextBlock
            {
                FontSize = 13.5,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
            };
            Description = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
            };

            var copyStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { Title, Description },
            };
            Grid.SetColumn(copyStack, 1);
            grid.Children.Add(copyStack);

            ShortcutText = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.Medium,
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
            };
            ShortcutBadge = new Border
            {
                Padding = new Thickness(6, 2),
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceBrush"),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                Child = ShortcutText,
            };
            Grid.SetColumn(ShortcutBadge, 2);
            grid.Children.Add(ShortcutBadge);

            Root = new Border
            {
                Height = 38,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(9),
                Background = Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = grid,
            };

            Root.PointerEntered += (_, _) => owner.SetActiveIndex(RowIndex, animate: true);
            Root.PointerPressed += (_, ev) =>
            {
                if (ev.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
                {
                    owner.ExecuteIndex(RowIndex);
                    ev.Handled = true;
                }
            };
        }

        public void Bind(VelaCommandItem cmd, int index)
        {
            RowIndex = index;
            Icon.Data = cmd.Icon;
            Icon.IsVisible = cmd.Icon is not null;
            Title.Text = cmd.Title;
            var hasDesc = !string.IsNullOrEmpty(cmd.Description);
            Description.Text = hasDesc ? cmd.Description : string.Empty;
            Description.IsVisible = hasDesc;
            var hasShortcut = !string.IsNullOrEmpty(cmd.Shortcut);
            ShortcutText.Text = hasShortcut ? cmd.Shortcut : string.Empty;
            ShortcutBadge.IsVisible = hasShortcut;
        }
    }

    private readonly List<PooledCommandRow> _rowPool = new();
    private readonly List<TextBlock> _groupHeaderPool = new();

    private void RebuildResults()
    {
        if (_resultsHost is null || _emptyState is null) return;
        _resultsHost.Children.Clear();
        _visibleRows.Clear();
        _activeRowIndex = -1;

        var query = _searchBox?.Text?.Trim();
        var hasQuery = !string.IsNullOrEmpty(query);

        var matches = Commands.Where(c =>
            !hasQuery ||
            c.Title.Contains(query!, StringComparison.OrdinalIgnoreCase) ||
            (c.Description?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false) ||
            c.Group.Contains(query!, StringComparison.OrdinalIgnoreCase) ||
            (c.Keywords?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false))
            .GroupBy(c => c.Group)
            .ToList();

        _emptyState.IsVisible = matches.Count == 0;
        if (matches.Count == 0)
        {
            _hlOpacity.Jump(0);
            if (_highlight is not null) _highlight.Opacity = 0;
            return;
        }

        var groupIdx = 0;
        var rowPoolIdx = 0;
        foreach (var group in matches)
        {
            TextBlock header;
            if (groupIdx < _groupHeaderPool.Count)
            {
                header = _groupHeaderPool[groupIdx];
            }
            else
            {
                header = new TextBlock
                {
                    FontSize = 11.5,
                    FontWeight = FontWeight.Medium,
                    Margin = new Thickness(10, 8, 10, 3),
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
                };
                _groupHeaderPool.Add(header);
            }
            groupIdx++;
            header.Text = group.Key;
            _resultsHost.Children.Add(header);

            foreach (var cmd in group)
            {
                var rowIndex = _visibleRows.Count;
                PooledCommandRow pooled;
                if (rowPoolIdx < _rowPool.Count)
                {
                    pooled = _rowPool[rowPoolIdx];
                }
                else
                {
                    pooled = new PooledCommandRow(this);
                    _rowPool.Add(pooled);
                }
                rowPoolIdx++;
                pooled.Bind(cmd, rowIndex);
                _visibleRows.Add((cmd, pooled.Root));
                _resultsHost.Children.Add(pooled.Root);
            }
        }
    }

    private void SetActiveIndex(int index, bool animate)
    {
        if (index < 0 || index >= _visibleRows.Count || _resultsTrack is null || _highlight is null)
        {
            _activeRowIndex = -1;
            _hlOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
            return;
        }

        _activeRowIndex = index;
        var row = _visibleRows[index].Row;
        var origin = row.TranslatePoint(new Point(0, 0), _resultsTrack);
        if (!origin.HasValue) return;

        _highlight.Width = Math.Max(0, _resultsTrack.Bounds.Width);
        _highlight.Height = row.Bounds.Height;

        if (!_hlPlaced || !animate || VelaMotionOptions.ReducedMotion)
        {
            _hlPlaced = true;
            _hlY.Jump(origin.Value.Y);
            _hlOpacity.Jump(1.0);
            ApplyHighlight();
            return;
        }

        _hlY.SetTarget(origin.Value.Y, VelaMotionTokens.Springs.Snappy);
        _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void ExecuteIndex(int index)
    {
        if (index < 0 || index >= _visibleRows.Count) return;
        var item = _visibleRows[index].Item;
        Close();
        item.Execute?.Invoke();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_visibleRows.Count == 0) return;

        switch (e.Key)
        {
            case Key.Down:
                SetActiveIndex(_activeRowIndex < _visibleRows.Count - 1 ? _activeRowIndex + 1 : 0, animate: true);
                e.Handled = true;
                break;
            case Key.Up:
                SetActiveIndex(_activeRowIndex > 0 ? _activeRowIndex - 1 : _visibleRows.Count - 1, animate: true);
                e.Handled = true;
                break;
            case Key.Enter:
                ExecuteIndex(_activeRowIndex >= 0 ? _activeRowIndex : 0);
                e.Handled = true;
                break;
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isOpen)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (EnableGlobalShortcut && e.Key == Key.K &&
            (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            Toggle();
            e.Handled = true;
        }
    }

    private bool OnTick(double dt)
    {
        var active = _scrimOpacity.Step(dt);
        active |= _panelOpacity.Step(dt);
        active |= _panelScale.Step(dt);
        active |= _panelY.Step(dt);
        active |= _hlY.Step(dt);
        active |= _hlOpacity.Step(dt);

        ApplyPanelVisuals();
        ApplyHighlight();

        if (!active && !_isOpen && _overlayLayer is not null)
            _overlayLayer.IsVisible = false;

        return active;
    }

    private void ApplyPanelVisuals()
    {
        if (_scrim is not null)
            _scrim.Opacity = Math.Clamp(_scrimOpacity.Value, 0.0, 1.0);
        if (_panelWrapper is not null)
            _panelWrapper.Opacity = Math.Clamp(_panelOpacity.Value, 0.0, 1.0);
        if (_panelScaleTransform is not null)
        {
            var s = Math.Max(0.5, _panelScale.Value);
            _panelScaleTransform.ScaleX = s;
            _panelScaleTransform.ScaleY = s;
        }
        if (_panelTranslateTransform is not null)
            _panelTranslateTransform.Y = _panelY.Value;
    }

    private void ApplyHighlight()
    {
        if (_highlight is null || _hlTranslate is null) return;
        _hlTranslate.Y = _hlY.Value;
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
    }
}
