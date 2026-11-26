using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

public sealed class VelaDialogOptions
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public double MaxWidth { get; init; } = 440;
    public bool CloseOnBackdropClick { get; init; } = true;
    public bool ShowCloseButton { get; init; } = true;
    public VelaSurfaceMaterial Material { get; init; } = VelaSurfaceMaterial.Solid;
}

public sealed class VelaConfirmDialogOptions
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Control? Body { get; init; }
    public string ConfirmText { get; init; } = "Confirm";
    public string CancelText { get; init; } = "Cancel";
    public bool IsDestructive { get; init; }
    public bool CloseOnBackdropClick { get; init; } = true;
    public VelaSurfaceMaterial Material { get; init; } = VelaSurfaceMaterial.Solid;
}

/// <summary>
/// Decoupled modal dialog host with real AnimatePresence lifecycle:
/// - Spring entrance (scale 0.96 -> 1, y 8 -> 0, scrim fade) and exit animation before DOM removal
/// - Click-through during exit (reopening mid-exit never gets blocked)
/// - Esc key &amp; backdrop dismissal, works inside any Window or UserControl
/// </summary>
public class VelaDialogHost : ContentControl
{
    private readonly SpringChannel _scrimOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardScale = new(0.96, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardY = new(8.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private Grid? _rootGrid;
    private Grid? _overlayLayer;
    private Border? _scrim;
    private Border? _cardShell;
    private ScaleTransform? _cardScaleTransform;
    private TranslateTransform? _cardTranslateTransform;

    private bool _isOpen;
    private bool _closeOnBackdropClick = true;
    private Action<object?>? _pendingResolve;

    public bool IsDialogOpen => _isOpen;

    public VelaDialogHost()
    {
        _runner = new SpringRunner(this, OnMotionTick);
    }

    public static VelaDialogHost? Find(Visual visual)
        => visual.FindAncestorOfType<VelaDialogHost>(includeSelf: true);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
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

        Template = new FuncControlTemplate<VelaDialogHost>((host, scope) =>
        {
            _rootGrid = new Grid();

            var mainPresenter = new ContentPresenter
            {
                [!ContentPresenter.ContentProperty] = host[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = host[!ContentTemplateProperty],
            };
            _rootGrid.Children.Add(mainPresenter);

            _scrim = new Border
            {
                [!Border.BackgroundProperty] = host.GetResourceObservable("VelaOverlayScrimBrush").ToBinding(),
                Opacity = 0,
            };
            _scrim.PointerPressed += (_, ev) =>
            {
                if (_isOpen && _closeOnBackdropClick)
                {
                    Close(null);
                    ev.Handled = true;
                }
            };

            _cardScaleTransform = new ScaleTransform(0.96, 0.96);
            _cardTranslateTransform = new TranslateTransform(0, 8);

            _cardShell = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24),
                CornerRadius = new CornerRadius(24),
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new TransformGroup { Children = { _cardScaleTransform, _cardTranslateTransform } },
                Opacity = 0,
                [!Border.BackgroundProperty] = host.GetResourceObservable("VelaSurfaceRaisedBrush").ToBinding(),
                [!Border.BorderBrushProperty] = host.GetResourceObservable("VelaBorderBrush").ToBinding(),
                [!Border.BoxShadowProperty] = host.GetResourceObservable("VelaShadowFloating").ToBinding(),
            };

            _overlayLayer = new Grid
            {
                IsVisible = false,
                IsHitTestVisible = false,
                ZIndex = 1000,
                Children = { _scrim, _cardShell },
            };

            _rootGrid.Children.Add(_overlayLayer);
            return _rootGrid;
        });
    }

    public Task<bool> ShowConfirmAsync(VelaConfirmDialogOptions options)
    {
        var tcs = new TaskCompletionSource<bool>();

        var cancelBtn = new VelaButton
        {
            Variant = VelaButtonVariant.Secondary,
            Size = VelaControlSize.Small,
            Content = options.CancelText,
        };
        cancelBtn.Click += (_, _) => Close(false);

        var confirmBtn = new VelaButton
        {
            Variant = options.IsDestructive ? VelaButtonVariant.Danger : VelaButtonVariant.Primary,
            Size = VelaControlSize.Small,
            Content = options.ConfirmText,
        };
        confirmBtn.Click += (_, _) => Close(true);

        var bodyStack = new StackPanel { Spacing = 18 };
        if (options.Body is not null)
            bodyStack.Children.Add(options.Body);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelBtn, confirmBtn },
        };
        bodyStack.Children.Add(footer);

        OpenCore(
            bodyStack,
            new VelaDialogOptions
            {
                Title = options.Title,
                Description = options.Description,
                CloseOnBackdropClick = options.CloseOnBackdropClick,
                ShowCloseButton = true,
                Material = options.Material,
            },
            result => tcs.TrySetResult(result is bool b && b));

        return tcs.Task;
    }

    public Task<TResult?> ShowAsync<TResult>(Control content, VelaDialogOptions? options = null)
    {
        var tcs = new TaskCompletionSource<TResult?>();
        OpenCore(content, options ?? new VelaDialogOptions(), result =>
        {
            tcs.TrySetResult(result is TResult typed ? typed : default);
        });
        return tcs.Task;
    }

    public void Close(object? result = null)
    {
        if (!_isOpen)
            return;

        _isOpen = false;
        var resolver = _pendingResolve;
        _pendingResolve = null;

        if (_overlayLayer is not null)
            _overlayLayer.IsHitTestVisible = false;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(0.0);
            _cardOpacity.Jump(0.0);
            _cardScale.Jump(0.98);
            _cardY.Jump(4.0);
            FinishCloseCleanup();
            resolver?.Invoke(result);
            return;
        }

        _scrimOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _cardOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _cardScale.SetTarget(0.98, VelaMotionTokens.Springs.Snappy);
        _cardY.SetTarget(4.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();

        resolver?.Invoke(result);
    }

    private void OpenCore(Control body, VelaDialogOptions options, Action<object?> onResolve)
    {
        EnsureOverlayInitialized();
        if (_overlayLayer is null || _cardShell is null)
        {
            onResolve(null);
            return;
        }

        // Resolve any previously open dialog cleanly
        _pendingResolve?.Invoke(null);
        _pendingResolve = onResolve;
        _closeOnBackdropClick = options.CloseOnBackdropClick;
        _isOpen = true;

        _cardShell.Width = options.MaxWidth;
        var chrome = BuildDialogChrome(body, options);

        if (options.Material == VelaSurfaceMaterial.LiquidGlass)
        {
            _cardShell.BackdropEffect = null;
            _cardShell.Background = Brushes.Transparent;
            _cardShell.BorderThickness = new Thickness(0);
            _cardShell.Child = new VelaLiquidGlass
            {
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(0),
                BlurRadius = 12,
                BevelWidth = 18,
                Refraction = 0.65,
                Dispersion = 0.35,
                Specular = 0.85,
                Content = chrome,
            };
        }
        else if (options.Material == VelaSurfaceMaterial.Frosted)
        {
            _cardShell.BackdropEffect = new BlurEffect { Radius = 14 };
            _cardShell.Bind(Border.BackgroundProperty, this.GetResourceObservable("VelaLiquidGlassTintBrush"));
            _cardShell.BorderThickness = new Thickness(1);
            _cardShell.Child = chrome;
        }
        else
        {
            _cardShell.BackdropEffect = null;
            _cardShell.Bind(Border.BackgroundProperty, this.GetResourceObservable("VelaSurfaceRaisedBrush"));
            _cardShell.BorderThickness = new Thickness(1);
            _cardShell.Child = chrome;
        }

        _overlayLayer.IsVisible = true;
        _overlayLayer.IsHitTestVisible = true;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(1.0);
            _cardOpacity.Jump(1.0);
            _cardScale.Jump(1.0);
            _cardY.Jump(0.0);
            ApplyVisuals();
            return;
        }

        _scrimOpacity.Jump(0.0);
        _cardOpacity.Jump(0.0);
        _cardScale.Jump(0.96);
        _cardY.Jump(8.0);
        ApplyVisuals();

        _scrimOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _cardOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _cardScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _cardY.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private Control BuildDialogChrome(Control body, VelaDialogOptions options)
    {
        var root = new StackPanel();

        if (!string.IsNullOrEmpty(options.Title) || !string.IsNullOrEmpty(options.Description) || options.ShowCloseButton)
        {
            var headerGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 16,
            };

            var textStack = new StackPanel { Spacing = 4 };
            if (!string.IsNullOrEmpty(options.Title))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = options.Title,
                    FontSize = 17,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaForegroundBrush").ToBinding(),
                });
            }

            if (!string.IsNullOrEmpty(options.Description))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = options.Description,
                    FontSize = 13.5,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
                });
            }

            headerGrid.Children.Add(textStack);

            if (options.ShowCloseButton)
            {
                var closeIcon = new PathIcon
                {
                    Width = 13,
                    Height = 13,
                    [!PathIcon.DataProperty] = this.GetResourceObservable("VelaIconClose").ToBinding(),
                    [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
                };

                var closeBtn = new Button
                {
                    Width = 30,
                    Height = 30,
                    Padding = new Thickness(0),
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Content = closeIcon,
                    [!Button.BackgroundProperty] = this.GetResourceObservable("VelaSurfaceMutedBrush").ToBinding(),
                    [!Button.BorderBrushProperty] = this.GetResourceObservable("VelaBorderBrush").ToBinding(),
                };
                closeBtn.Click += (_, _) => Close(null);
                Grid.SetColumn(closeBtn, 1);
                headerGrid.Children.Add(closeBtn);
            }

            root.Children.Add(new Border
            {
                Padding = new Thickness(22, 20),
                BorderThickness = new Thickness(0, 0, 0, 1),
                [!Border.BorderBrushProperty] = this.GetResourceObservable("VelaBorderSubtleBrush").ToBinding(),
                Child = headerGrid,
            });
        }

        root.Children.Add(new Border
        {
            Padding = new Thickness(22, 18, 22, 20),
            Child = body,
        });

        return root;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isOpen && e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    private bool OnMotionTick(double dt)
    {
        var active = _scrimOpacity.Step(dt);
        active |= _cardOpacity.Step(dt);
        active |= _cardScale.Step(dt);
        active |= _cardY.Step(dt);

        ApplyVisuals();

        if (!active && !_isOpen)
            FinishCloseCleanup();

        return active;
    }

    private void ApplyVisuals()
    {
        if (_scrim is not null)
            _scrim.Opacity = Math.Clamp(_scrimOpacity.Value, 0.0, 1.0);

        if (_cardShell is not null)
            _cardShell.Opacity = Math.Clamp(_cardOpacity.Value, 0.0, 1.0);

        if (_cardScaleTransform is not null)
        {
            var s = Math.Max(0.5, _cardScale.Value);
            _cardScaleTransform.ScaleX = s;
            _cardScaleTransform.ScaleY = s;
        }

        if (_cardTranslateTransform is not null)
            _cardTranslateTransform.Y = _cardY.Value;
    }

    private void FinishCloseCleanup()
    {
        if (_overlayLayer is not null)
            _overlayLayer.IsVisible = false;
        if (_cardShell is not null)
            _cardShell.Child = null;
    }
}
