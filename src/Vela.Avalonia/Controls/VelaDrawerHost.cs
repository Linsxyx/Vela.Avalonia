using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

public enum VelaDrawerSide
{
    Right,
    Left,
}

public sealed class VelaDrawerOptions
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public VelaDrawerSide Side { get; init; } = VelaDrawerSide.Right;
    public double Width { get; init; } = 390.0;
    public bool CloseOnBackdropClick { get; init; } = true;
    public VelaSurfaceMaterial Material { get; init; } = VelaSurfaceMaterial.Solid;
    public bool ShowHeader { get; init; } = true;
    public bool WrapInScrollViewer { get; init; } = true;
    public Thickness? BodyPadding { get; init; }
    public Control? HeaderAction { get; init; }
}

/// <summary>
/// Port of Arc UI's Drawer (side sheet):
/// - Slides smoothly from Left or Right edge on critically damped spring physics
/// - Real AnimatePresence exit animation before clearing visual tree
/// - Decoupled from Window chrome; Esc &amp; backdrop dismissal built in
/// </summary>
public class VelaDrawerHost : ContentControl
{
    private readonly SpringChannel _scrimOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _slideX = new(390.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;

    private Grid? _overlayLayer;
    private Border? _scrim;
    private Border? _sheet;
    private TranslateTransform? _sheetTranslate;

    private bool _isOpen;
    private bool _closeOnBackdropClick = true;
    private VelaDrawerSide _currentSide = VelaDrawerSide.Right;
    private double _currentWidth = 390.0;
    private Action? _onClosed;
    private readonly List<IDisposable> _sheetBindings = new();

    public bool IsDrawerOpen => _isOpen;

    public VelaDrawerHost()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        EnsureOverlayInitialized();
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
            topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
            topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
    }

    private void EnsureOverlayInitialized()
    {
        if (_overlayLayer is not null)
            return;

        Template = new FuncControlTemplate<VelaDrawerHost>((host, _) =>
        {
            var rootGrid = new Grid();

            var mainPresenter = new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = host[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = host[!ContentTemplateProperty],
            };
            rootGrid.Children.Add(mainPresenter);

            _scrim = new Border
            {
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaOverlayScrimBrush"),
                Opacity = 0,
            };
            _scrim.PointerPressed += (_, ev) =>
            {
                if (_isOpen && _closeOnBackdropClick)
                {
                    Close();
                    ev.Handled = true;
                }
            };

            _sheetTranslate = new TranslateTransform();
            _sheet = new Border
            {
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 390,
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(1),
                ClipToBounds = false,
                RenderTransform = _sheetTranslate,
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceRaisedBrush"),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                [!Border.BoxShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
            };

            _overlayLayer = new Grid
            {
                IsVisible = false,
                IsHitTestVisible = false,
                ZIndex = 950,
                Children = { _scrim, _sheet },
            };

            rootGrid.Children.Add(_overlayLayer);
            return rootGrid;
        });
    }

    public Task ShowAsync(Control body, VelaDrawerOptions? options = null)
    {
        var tcs = new TaskCompletionSource();
        var opts = options ?? new VelaDrawerOptions();

        EnsureOverlayInitialized();
        if (_overlayLayer is null || _sheet is null)
        {
            tcs.TrySetResult();
            return tcs.Task;
        }

        _onClosed?.Invoke();
        _onClosed = () => tcs.TrySetResult();
        _closeOnBackdropClick = opts.CloseOnBackdropClick;
        _currentSide = opts.Side;
        _currentWidth = opts.Width;
        _isOpen = true;

        _sheet.Width = opts.Width;
        _sheet.HorizontalAlignment = opts.Side == VelaDrawerSide.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        ClearSheetBindings();
        var chrome = BuildDrawerChrome(body, opts);

        if (opts.Material == VelaSurfaceMaterial.GlassCard)
        {
            _sheet.BackdropEffect = null;
            _sheet.Background = Brushes.Transparent;
            _sheet.BorderBrush = Brushes.Transparent;
            _sheet.BorderThickness = new Thickness(0);
            _sheet.BoxShadow = default;
            _sheet.ClipToBounds = false;
            _sheet.Child = new VelaGlassCard
            {
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(0),
                BlurRadius = 80,
                BlurBackground = true,
                TintLevel = VelaGlassTintLevel.HighContrast,
                RimThickness = new Thickness(1.2),
                Content = chrome,
                [!VelaGlassCard.CardShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
            };
        }
        else if (opts.Material == VelaSurfaceMaterial.LiquidGlass)
        {
            _sheet.BackdropEffect = null;
            _sheet.Background = Brushes.Transparent;
            _sheet.BorderBrush = Brushes.Transparent;
            _sheet.BorderThickness = new Thickness(0);
            _sheet.BoxShadow = default;
            _sheet.ClipToBounds = false;
            _sheet.Child = new VelaLiquidGlass
            {
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(0),
                BlurRadius = 14,
                BevelWidth = 18,
                Refraction = 0.6,
                Dispersion = 0.35,
                Specular = 0.85,
                Content = chrome,
            };
        }
        else if (opts.Material == VelaSurfaceMaterial.Frosted)
        {
            _sheet.ClipToBounds = false;
            _sheet.BackdropEffect = null;
            _sheet.Background = Brushes.Transparent;
            _sheetBindings.Add(VelaResourceBinding.Bind(_sheet, Border.BorderBrushProperty, "VelaGlassCardOuterHairlineBrush"));
            _sheetBindings.Add(VelaResourceBinding.Bind(_sheet, Border.BoxShadowProperty, "VelaShadowFloating"));
            _sheet.BorderThickness = new Thickness(1);
            _sheet.Child = new Border
            {
                CornerRadius = new CornerRadius(20),
                ClipToBounds = true,
                BackdropEffect = new BlurEffect { Radius = 14 },
                Child = chrome,
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaLiquidGlassTintBrush"),
            };
        }
        else
        {
            _sheet.ClipToBounds = false;
            _sheet.BackdropEffect = null;
            _sheetBindings.Add(VelaResourceBinding.Bind(_sheet, Border.BackgroundProperty, "VelaSurfaceRaisedBrush"));
            _sheetBindings.Add(VelaResourceBinding.Bind(_sheet, Border.BorderBrushProperty, "VelaBorderBrush"));
            _sheetBindings.Add(VelaResourceBinding.Bind(_sheet, Border.BoxShadowProperty, "VelaShadowFloating"));
            _sheet.BorderThickness = new Thickness(1);
            _sheet.Child = new Border
            {
                CornerRadius = new CornerRadius(20),
                ClipToBounds = true,
                Child = chrome,
            };
        }

        var offscreenX = opts.Side == VelaDrawerSide.Left ? -(opts.Width + 24) : (opts.Width + 24);
        _overlayLayer.IsVisible = true;
        _overlayLayer.IsHitTestVisible = true;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(1.0);
            _slideX.Jump(0.0);
            ApplyVisuals();
            return tcs.Task;
        }

        _scrimOpacity.Jump(0.0);
        _slideX.Jump(offscreenX);
        ApplyVisuals();

        _scrimOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _slideX.SetTarget(0.0, VelaMotionTokens.Springs.Smooth);
        _runner.Wake();

        return tcs.Task;
    }

    public void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;
        var callback = _onClosed;
        _onClosed = null;

        if (_overlayLayer is not null)
            _overlayLayer.IsHitTestVisible = false;

        var offscreenX = _currentSide == VelaDrawerSide.Left ? -(_currentWidth + 24) : (_currentWidth + 24);

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(0.0);
            _slideX.Jump(offscreenX);
            FinishClose();
            callback?.Invoke();
            return;
        }

        _scrimOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _slideX.SetTarget(offscreenX, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        callback?.Invoke();
    }

    private Control BuildDrawerChrome(Control body, VelaDrawerOptions opts)
    {
        var dock = new DockPanel();

        if (opts.ShowHeader)
        {
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 8,
            };

            var titleStack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(opts.Title))
            {
                titleStack.Children.Add(new TextBlock
                {
                    Text = opts.Title,
                    FontSize = 16.5,
                    FontWeight = FontWeight.SemiBold,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
                });
            }
            if (!string.IsNullOrEmpty(opts.Description))
            {
                titleStack.Children.Add(new TextBlock
                {
                    Text = opts.Description,
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                });
            }
            headerGrid.Children.Add(titleStack);

            if (opts.HeaderAction is not null)
            {
                opts.HeaderAction.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(opts.HeaderAction, 1);
                headerGrid.Children.Add(opts.HeaderAction);
            }

            var closeBtn = new Button
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(9),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new PathIcon
                {
                    Width = 13,
                    Height = 13,
                    [!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconClose"),
                    [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                },
            };
            if (this.TryFindResource("VelaIconButtonTheme", ActualThemeVariant, out var themeObj) && themeObj is ControlTheme ct)
                closeBtn.Theme = ct;
            closeBtn.Click += (_, _) => Close();
            Grid.SetColumn(closeBtn, 2);
            headerGrid.Children.Add(closeBtn);

            var dividerKey = opts.Material == VelaSurfaceMaterial.Solid ? "VelaBorderSubtleBrush" : "VelaGlassDividerBrush";
            var headerBorder = new Border
            {
                Padding = new Thickness(20, 16),
                BorderThickness = new Thickness(0, 0, 0, 1),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create(dividerKey),
                Child = headerGrid,
            };
            DockPanel.SetDock(headerBorder, Dock.Top);
            dock.Children.Add(headerBorder);
        }

        if (opts.WrapInScrollViewer)
        {
            dock.Children.Add(new ScrollViewer
            {
                Padding = opts.BodyPadding ?? new Thickness(20, 18),
                Content = body,
            });
        }
        else
        {
            dock.Children.Add(new Border
            {
                Padding = opts.BodyPadding ?? new Thickness(0),
                Child = body,
            });
        }

        return dock;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isOpen && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private bool OnTick(double dt)
    {
        var active = _scrimOpacity.Step(dt);
        active |= _slideX.Step(dt);
        ApplyVisuals();

        if (!active && !_isOpen)
            FinishClose();

        return active;
    }

    private void ApplyVisuals()
    {
        if (_scrim is not null)
            _scrim.Opacity = Math.Clamp(_scrimOpacity.Value, 0.0, 1.0);
        if (_sheetTranslate is not null)
            _sheetTranslate.X = _slideX.Value;
    }

    private void FinishClose()
    {
        if (_overlayLayer is not null)
            _overlayLayer.IsVisible = false;
        ClearSheetBindings();
        if (_sheet is not null)
            _sheet.Child = null;
    }

    private void ClearSheetBindings()
    {
        foreach (var d in _sheetBindings)
            d.Dispose();
        _sheetBindings.Clear();
    }
}
