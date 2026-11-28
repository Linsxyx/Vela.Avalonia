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

/// <summary>
/// Presentation layout style for dialogs rendered by <see cref="VelaDialogHost"/>.
/// </summary>
public enum VelaDialogStyle
{
    /// <summary>Classic Vela modal: left-aligned header, subtle divider, top-right close button.</summary>
    Standard,

    /// <summary>
    /// SukiUI-inspired centered modal: spacious rounded card, optional floating top icon emblem,
    /// centered title &amp; description, and clean divider-free body.
    /// </summary>
    Centered,
}

/// <summary>
/// Semantic accent tone for dialog icon emblems and visual emphasis.
/// </summary>
public enum VelaDialogTone
{
    Neutral,
    Accent,
    Info,
    Success,
    Warning,
    Danger,
}

public sealed class VelaDialogOptions
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public double MaxWidth { get; init; } = 440;
    public bool CloseOnBackdropClick { get; init; } = true;
    public bool ShowCloseButton { get; init; } = true;

    /// <summary>
    /// Surface material override for this dialog. When <see langword="null"/>,
    /// inherits <see cref="VelaDialogHost.DefaultMaterial"/>.
    /// </summary>
    public VelaSurfaceMaterial? Material { get; init; }

    /// <summary>
    /// Layout style override for this dialog. When <see langword="null"/>,
    /// inherits <see cref="VelaDialogHost.DefaultStyle"/>.
    /// </summary>
    public VelaDialogStyle? Style { get; init; }

    /// <summary>
    /// Optional vector icon geometry displayed as a floating emblem (in <see cref="VelaDialogStyle.Centered"/>)
    /// or compact header badge (in <see cref="VelaDialogStyle.Standard"/>).
    /// </summary>
    public Geometry? Icon { get; init; }

    /// <summary>Semantic tone used to color the icon emblem.</summary>
    public VelaDialogTone Tone { get; init; } = VelaDialogTone.Neutral;

    /// <summary>Optional custom background brush overriding the tone fill on the icon emblem.</summary>
    public IBrush? IconBackground { get; init; }

    /// <summary>Backdrop blur radius used when rendering with <see cref="VelaSurfaceMaterial.GlassCard"/>.</summary>
    public double GlassBlurRadius { get; init; } = 80.0;
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
    public bool ShowCloseButton { get; init; } = true;
    public double MaxWidth { get; init; } = 440;

    /// <summary>
    /// Surface material override for this dialog. When <see langword="null"/>,
    /// inherits <see cref="VelaDialogHost.DefaultMaterial"/>.
    /// </summary>
    public VelaSurfaceMaterial? Material { get; init; }

    /// <summary>
    /// Layout style override for this dialog. When <see langword="null"/>,
    /// inherits <see cref="VelaDialogHost.DefaultStyle"/>.
    /// </summary>
    public VelaDialogStyle? Style { get; init; }

    /// <summary>Optional vector icon geometry for the dialog emblem.</summary>
    public Geometry? Icon { get; init; }

    /// <summary>
    /// Semantic tone override. When <see langword="null"/>, resolves to
    /// <see cref="VelaDialogTone.Danger"/> if <see cref="IsDestructive"/> is true, otherwise <see cref="VelaDialogTone.Neutral"/>.
    /// </summary>
    public VelaDialogTone? Tone { get; init; }

    /// <summary>Optional custom background brush overriding the tone fill on the icon emblem.</summary>
    public IBrush? IconBackground { get; init; }

    /// <summary>Backdrop blur radius used when rendering with <see cref="VelaSurfaceMaterial.GlassCard"/>.</summary>
    public double GlassBlurRadius { get; init; } = 80.0;
}

/// <summary>
/// Decoupled modal dialog host with real AnimatePresence lifecycle:
/// - Spring entrance (scale 0.96 -> 1, y 8 -> 0, scrim fade) and exit animation before DOM removal
/// - Supports Solid, Frosted, SukiUI-inspired GlassCard (deep retained blur + diagonal specular rim), and GPU LiquidGlass materials
/// - Supports Standard and SukiUI-inspired Centered (floating emblem) dialog styles
/// - Pinned-dialog spring bounce when backdrop dismissal is disabled
/// - Click-through during exit (reopening mid-exit never gets blocked)
/// - Esc key &amp; backdrop dismissal, works inside any Window or UserControl
/// </summary>
public class VelaDialogHost : ContentControl
{
    public static readonly StyledProperty<VelaSurfaceMaterial> DefaultMaterialProperty =
        AvaloniaProperty.Register<VelaDialogHost, VelaSurfaceMaterial>(
            nameof(DefaultMaterial),
            VelaSurfaceMaterial.Solid);

    public static readonly StyledProperty<VelaDialogStyle> DefaultStyleProperty =
        AvaloniaProperty.Register<VelaDialogHost, VelaDialogStyle>(
            nameof(DefaultStyle),
            VelaDialogStyle.Standard);

    /// <summary>Default surface material used when dialog options do not specify one.</summary>
    public VelaSurfaceMaterial DefaultMaterial
    {
        get => GetValue(DefaultMaterialProperty);
        set => SetValue(DefaultMaterialProperty, value);
    }

    /// <summary>Default presentation layout style used when dialog options do not specify one.</summary>
    public VelaDialogStyle DefaultStyle
    {
        get => GetValue(DefaultStyleProperty);
        set => SetValue(DefaultStyleProperty, value);
    }

    private readonly SpringChannel _scrimOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardScale = new(0.96, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _cardY = new(8.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private readonly List<IDisposable> _shellBindings = new();

    private Grid? _rootGrid;
    private Grid? _overlayLayer;
    private Border? _scrim;
    private Panel? _dialogContainer;
    private Border? _cardShell;
    private ContentControl? _floatingEmblemHost;
    private ScaleTransform? _cardScaleTransform;
    private TranslateTransform? _cardTranslateTransform;

    private bool _isOpen;
    private bool _closeOnBackdropClick = true;
    private double _scrimTargetOpacity = 1.0;
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
        _runner.Stop();
        DisposeShellBindings();
        if (!_isOpen)
            FinishCloseCleanup();
        base.OnDetachedFromVisualTree(e);
        if (_attachedTopLevel is not null)
        {
            _attachedTopLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _attachedTopLevel = null;
        }
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
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaOverlayScrimBrush"),
                Opacity = 0,
            };
            _scrim.PointerPressed += (_, ev) =>
            {
                if (!_isOpen)
                    return;

                if (_closeOnBackdropClick)
                {
                    Close(null);
                    ev.Handled = true;
                }
                else
                {
                    var pos = ev.GetPosition(_overlayLayer);
                    var direction = _overlayLayer is not null && pos.Y >= _overlayLayer.Bounds.Height * 0.5 ? -1.0 : 1.0;
                    TriggerPinnedBounce(direction);
                    ev.Handled = true;
                }
            };

            _cardScaleTransform = new ScaleTransform(0.96, 0.96);
            _cardTranslateTransform = new TranslateTransform(0, 8);

            _cardShell = new Border
            {
                CornerRadius = new CornerRadius(24),
                BorderThickness = new Thickness(1),
                ClipToBounds = false,
                [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceRaisedBrush"),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
                [!Border.BoxShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
            };

            _floatingEmblemHost = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                ClipToBounds = false,
                IsVisible = false,
                IsHitTestVisible = false,
            };

            _dialogContainer = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24),
                ClipToBounds = false,
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new TransformGroup { Children = { _cardScaleTransform, _cardTranslateTransform } },
                Opacity = 0,
                Children = { _cardShell, _floatingEmblemHost },
            };

            _overlayLayer = new Grid
            {
                IsVisible = false,
                IsHitTestVisible = false,
                ZIndex = 1000,
                Children = { _scrim, _dialogContainer },
            };

            _rootGrid.Children.Add(_overlayLayer);
            return _rootGrid;
        });
    }

    public Task<bool> ShowConfirmAsync(VelaConfirmDialogOptions options)
    {
        var tcs = new TaskCompletionSource<bool>();
        var effectiveStyle = options.Style ?? DefaultStyle;
        var isCentered = effectiveStyle == VelaDialogStyle.Centered;

        var cancelBtn = new VelaButton
        {
            Variant = VelaButtonVariant.Secondary,
            Size = VelaControlSize.Small,
            MinWidth = isCentered ? 108 : 0,
            Content = options.CancelText,
        };
        cancelBtn.Click += (_, _) => Close(false);

        var confirmBtn = new VelaButton
        {
            Variant = options.IsDestructive ? VelaButtonVariant.Danger : VelaButtonVariant.Primary,
            Size = VelaControlSize.Small,
            MinWidth = isCentered ? 108 : 0,
            Content = options.ConfirmText,
        };
        confirmBtn.Click += (_, _) => Close(true);

        var bodyStack = new StackPanel { Spacing = 18 };
        if (options.Body is not null)
            bodyStack.Children.Add(options.Body);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = isCentered ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelBtn, confirmBtn },
        };
        bodyStack.Children.Add(footer);

        var resolvedTone = options.Tone ?? (options.IsDestructive ? VelaDialogTone.Danger : VelaDialogTone.Neutral);

        OpenCore(
            bodyStack,
            new VelaDialogOptions
            {
                Title = options.Title,
                Description = options.Description,
                MaxWidth = options.MaxWidth,
                CloseOnBackdropClick = options.CloseOnBackdropClick,
                ShowCloseButton = options.ShowCloseButton,
                Material = options.Material,
                Style = options.Style,
                Icon = options.Icon,
                Tone = resolvedTone,
                IconBackground = options.IconBackground,
                GlassBlurRadius = options.GlassBlurRadius,
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

    private void TriggerPinnedBounce(double direction)
    {
        if (VelaMotionOptions.ReducedMotion)
            return;

        _cardY.SetTarget(0.0, VelaMotionTokens.Springs.SnapBack, initialVelocity: direction * 220.0);
        _runner.Wake();
    }

    internal static VelaSurfaceMaterial ResolveMaterial(VelaSurfaceMaterial? optionMaterial, VelaSurfaceMaterial hostDefault)
        => optionMaterial ?? hostDefault;

    internal static VelaDialogStyle ResolveStyle(VelaDialogStyle? optionStyle, VelaDialogStyle hostDefault)
        => optionStyle ?? hostDefault;

    private void OpenCore(Control body, VelaDialogOptions options, Action<object?> onResolve)
    {
        EnsureOverlayInitialized();
        if (_overlayLayer is null || _cardShell is null || _dialogContainer is null)
        {
            onResolve(null);
            return;
        }

        // Resolve any previously open dialog cleanly
        _pendingResolve?.Invoke(null);
        _pendingResolve = onResolve;
        _closeOnBackdropClick = options.CloseOnBackdropClick;
        _isOpen = true;

        var material = ResolveMaterial(options.Material, DefaultMaterial);
        var style = ResolveStyle(options.Style, DefaultStyle);
        var cornerRadius = new CornerRadius(style == VelaDialogStyle.Centered || material == VelaSurfaceMaterial.GlassCard ? 26 : 24);
        var resolvedIcon = ResolveIconGeometry(options);
        var hasFloatingEmblem = style == VelaDialogStyle.Centered && (resolvedIcon is not null || options.Tone != VelaDialogTone.Neutral);

        _cardShell.Width = options.MaxWidth;
        _cardShell.CornerRadius = cornerRadius;
        _cardShell.Margin = new Thickness(0, hasFloatingEmblem ? 36 : 0, 0, 0);

        if (_floatingEmblemHost is not null)
        {
            if (hasFloatingEmblem)
            {
                _floatingEmblemHost.Content = BuildFloatingEmblem(resolvedIcon, options.Tone, options.IconBackground);
                _floatingEmblemHost.IsVisible = true;
            }
            else
            {
                _floatingEmblemHost.Content = null;
                _floatingEmblemHost.IsVisible = false;
            }
        }

        var isGlassSurface = material != VelaSurfaceMaterial.Solid;
        var chrome = style == VelaDialogStyle.Centered
            ? BuildCenteredDialogChrome(body, options, hasFloatingEmblem, isGlassSurface)
            : BuildStandardDialogChrome(body, options, resolvedIcon, isGlassSurface);

        ApplySurfaceMaterial(material, cornerRadius, options.GlassBlurRadius, chrome);

        _scrimTargetOpacity = material switch
        {
            VelaSurfaceMaterial.GlassCard => 0.60,
            VelaSurfaceMaterial.LiquidGlass => 0.58,
            VelaSurfaceMaterial.Frosted => 0.78,
            _ => 1.0,
        };

        _overlayLayer.IsVisible = true;
        _overlayLayer.IsHitTestVisible = true;

        if (VelaMotionOptions.ReducedMotion)
        {
            _scrimOpacity.Jump(_scrimTargetOpacity);
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

        _scrimOpacity.SetTarget(_scrimTargetOpacity, VelaMotionTokens.Springs.Snappy);
        _cardOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _cardScale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _cardY.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void DisposeShellBindings()
    {
        foreach (var d in _shellBindings)
            d.Dispose();
        _shellBindings.Clear();
    }

    private void ApplySurfaceMaterial(
        VelaSurfaceMaterial material,
        CornerRadius cornerRadius,
        double glassBlurRadius,
        Control chrome)
    {
        if (_cardShell is null)
            return;

        DisposeShellBindings();

        switch (material)
        {
            case VelaSurfaceMaterial.GlassCard:
            {
                _cardShell.BackdropEffect = null;
                _cardShell.Background = Brushes.Transparent;
                _cardShell.BorderBrush = Brushes.Transparent;
                _cardShell.BorderThickness = new Thickness(0);
                _cardShell.BoxShadow = default;
                _cardShell.ClipToBounds = false;

                var glassCard = new VelaGlassCard
                {
                    CornerRadius = cornerRadius,
                    Padding = new Thickness(0),
                    BlurRadius = glassBlurRadius,
                    BlurBackground = true,
                    TintLevel = VelaGlassTintLevel.HighContrast,
                    RimThickness = new Thickness(1.2),
                    Content = chrome,
                    [!VelaGlassCard.CardShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
                };
                _cardShell.Child = glassCard;
                break;
            }

            case VelaSurfaceMaterial.LiquidGlass:
            {
                _cardShell.BackdropEffect = null;
                _cardShell.Background = Brushes.Transparent;
                _cardShell.BorderBrush = Brushes.Transparent;
                _cardShell.BorderThickness = new Thickness(0);
                _cardShell.BoxShadow = default;
                _cardShell.ClipToBounds = false;

                _cardShell.Child = new VelaLiquidGlass
                {
                    CornerRadius = cornerRadius,
                    Padding = new Thickness(0),
                    BlurRadius = 12,
                    BevelWidth = 18,
                    Refraction = 0.65,
                    Dispersion = 0.35,
                    Specular = 0.85,
                    Content = chrome,
                };
                break;
            }

            case VelaSurfaceMaterial.Frosted:
            {
                _cardShell.ClipToBounds = false;
                _cardShell.BackdropEffect = null;
                _cardShell.Background = Brushes.Transparent;
                _shellBindings.Add(VelaResourceBinding.Bind(_cardShell, Border.BorderBrushProperty, "VelaGlassCardOuterHairlineBrush"));
                _shellBindings.Add(VelaResourceBinding.Bind(_cardShell, Border.BoxShadowProperty, "VelaShadowFloating"));
                _cardShell.BorderThickness = new Thickness(1);

                var frostedClip = new Border
                {
                    CornerRadius = cornerRadius,
                    ClipToBounds = true,
                    BackdropEffect = new BlurEffect { Radius = 16 },
                    BackdropEffectCache = new RetainedBackdropEffectCache(),
                    Child = chrome,
                    [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaLiquidGlassTintBrush"),
                };
                _cardShell.Child = frostedClip;
                break;
            }

            default:
            {
                _cardShell.ClipToBounds = false;
                _cardShell.BackdropEffect = null;
                _shellBindings.Add(VelaResourceBinding.Bind(_cardShell, Border.BackgroundProperty, "VelaSurfaceRaisedBrush"));
                _shellBindings.Add(VelaResourceBinding.Bind(_cardShell, Border.BorderBrushProperty, "VelaBorderBrush"));
                _shellBindings.Add(VelaResourceBinding.Bind(_cardShell, Border.BoxShadowProperty, "VelaShadowFloating"));
                _cardShell.BorderThickness = new Thickness(1);
                _cardShell.Child = new Border
                {
                    CornerRadius = cornerRadius,
                    ClipToBounds = true,
                    Child = chrome,
                };
                break;
            }
        }
    }

    private Control BuildStandardDialogChrome(
        Control body,
        VelaDialogOptions options,
        Geometry? resolvedIcon,
        bool isGlassSurface)
    {
        var root = new StackPanel();
        var showHeaderBadge = resolvedIcon is not null && (options.Icon is not null || options.Tone != VelaDialogTone.Neutral);

        if (!string.IsNullOrEmpty(options.Title) || !string.IsNullOrEmpty(options.Description) || options.ShowCloseButton || showHeaderBadge)
        {
            var headerGrid = new Grid
            {
                ColumnDefinitions = showHeaderBadge
                    ? new ColumnDefinitions("Auto,*,Auto")
                    : new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 14,
            };

            var textCol = 0;
            if (showHeaderBadge)
            {
                var badge = BuildCompactBadge(resolvedIcon!, options.Tone, options.IconBackground);
                headerGrid.Children.Add(badge);
                textCol = 1;
            }

            var textStack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(options.Title))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = options.Title,
                    FontSize = 17,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
                });
            }

            if (!string.IsNullOrEmpty(options.Description))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = options.Description,
                    FontSize = 13.5,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                });
            }

            Grid.SetColumn(textStack, textCol);
            headerGrid.Children.Add(textStack);

            if (options.ShowCloseButton)
            {
                var closeBtn = BuildCloseButton(isGlassSurface);
                Grid.SetColumn(closeBtn, textCol + 1);
                headerGrid.Children.Add(closeBtn);
            }

            var dividerKey = isGlassSurface ? "VelaGlassDividerBrush" : "VelaBorderSubtleBrush";
            root.Children.Add(new Border
            {
                Padding = new Thickness(22, 20),
                BorderThickness = new Thickness(0, 0, 0, 1),
                [!Border.BorderBrushProperty] = VelaResourceBinding.Create(dividerKey),
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

    private Control BuildCenteredDialogChrome(
        Control body,
        VelaDialogOptions options,
        bool hasFloatingEmblem,
        bool isGlassSurface)
    {
        var panel = new Panel();

        var contentStack = new StackPanel
        {
            Spacing = 18,
            Margin = new Thickness(28, hasFloatingEmblem ? 50 : 28, 28, 24),
        };

        if (!string.IsNullOrEmpty(options.Title) || !string.IsNullOrEmpty(options.Description))
        {
            var headerStack = new StackPanel
            {
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            if (!string.IsNullOrEmpty(options.Title))
            {
                headerStack.Children.Add(new TextBlock
                {
                    Text = options.Title,
                    FontSize = 20,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
                });
            }

            if (!string.IsNullOrEmpty(options.Description))
            {
                headerStack.Children.Add(new TextBlock
                {
                    Text = options.Description,
                    FontSize = 13.5,
                    LineHeight = 20,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                });
            }

            contentStack.Children.Add(headerStack);
        }

        contentStack.Children.Add(body);
        panel.Children.Add(contentStack);

        if (options.ShowCloseButton)
        {
            var closeBtn = BuildCloseButton(isGlassSurface);
            closeBtn.HorizontalAlignment = HorizontalAlignment.Right;
            closeBtn.VerticalAlignment = VerticalAlignment.Top;
            closeBtn.Margin = new Thickness(0, 14, 14, 0);
            panel.Children.Add(closeBtn);
        }

        return panel;
    }

    private Button BuildCloseButton(bool isGlassSurface)
    {
        var closeIcon = new PathIcon
        {
            Width = 13,
            Height = 13,
            [!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconClose"),
            [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
        };

        var bgKey = isGlassSurface ? "VelaGlassCloseBackgroundBrush" : "VelaSurfaceMutedBrush";
        var borderKey = isGlassSurface ? "VelaGlassCloseBorderBrush" : "VelaBorderBrush";

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
            [!Button.BackgroundProperty] = VelaResourceBinding.Create(bgKey),
            [!Button.BorderBrushProperty] = VelaResourceBinding.Create(borderKey),
        };
        closeBtn.Click += (_, _) => Close(null);
        return closeBtn;
    }

    private Border BuildFloatingEmblem(Geometry? icon, VelaDialogTone tone, IBrush? customBackground)
    {
        var emblem = new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            ClipToBounds = false,
            BorderThickness = new Thickness(1.2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x58, 0xFF, 0xFF, 0xFF)),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 10,
                Blur = 24,
                Spread = 0,
                Color = Color.FromArgb(0x46, 0x00, 0x00, 0x00),
            }),
        };

        ApplyToneBackground(emblem, tone, customBackground);

        if (icon is not null)
        {
            var iconVisual = new PathIcon
            {
                Width = 28,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = icon,
            };

            if (tone == VelaDialogTone.Neutral && customBackground is null)
                iconVisual[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush");
            else if (tone == VelaDialogTone.Accent && customBackground is null)
                iconVisual[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaAccentForegroundBrush");
            else
                iconVisual.Foreground = Brushes.White;

            emblem.Child = iconVisual;
        }

        return emblem;
    }

    private Border BuildCompactBadge(Geometry icon, VelaDialogTone tone, IBrush? customBackground)
    {
        var badge = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Top,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x36, 0xFF, 0xFF, 0xFF)),
        };

        ApplyToneBackground(badge, tone, customBackground);

        var iconVisual = new PathIcon
        {
            Width = 17,
            Height = 17,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Data = icon,
        };

        if (tone == VelaDialogTone.Neutral && customBackground is null)
            iconVisual[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush");
        else if (tone == VelaDialogTone.Accent && customBackground is null)
            iconVisual[!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaAccentForegroundBrush");
        else
            iconVisual.Foreground = Brushes.White;

        badge.Child = iconVisual;
        return badge;
    }

    private void ApplyToneBackground(Border target, VelaDialogTone tone, IBrush? customBackground)
    {
        if (customBackground is not null)
        {
            target.Background = customBackground;
            return;
        }

        var key = tone switch
        {
            VelaDialogTone.Accent => "VelaAccentBrush",
            VelaDialogTone.Info => "VelaInfoBrush",
            VelaDialogTone.Success => "VelaSuccessBrush",
            VelaDialogTone.Warning => "VelaWarningBrush",
            VelaDialogTone.Danger => "VelaDangerBrush",
            _ => "VelaSurfaceMutedBrush",
        };
        target[!Border.BackgroundProperty] = VelaResourceBinding.Create(key);
    }

    private Geometry? ResolveIconGeometry(VelaDialogOptions options)
    {
        if (options.Icon is not null)
            return options.Icon;

        var defaultKey = options.Tone switch
        {
            VelaDialogTone.Accent => "VelaIconSparkles",
            VelaDialogTone.Info => "VelaIconInfo",
            VelaDialogTone.Success => "VelaIconCheckCircle",
            VelaDialogTone.Warning => "VelaIconAlertCircle",
            VelaDialogTone.Danger => "VelaIconAlertCircle",
            _ => null,
        };

        if (defaultKey is not null && this.TryFindResource(defaultKey, ActualThemeVariant, out var res) && res is Geometry g)
            return g;

        return null;
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

        if (_dialogContainer is not null)
            _dialogContainer.Opacity = Math.Clamp(_cardOpacity.Value, 0.0, 1.0);

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
        if (_floatingEmblemHost is not null)
        {
            _floatingEmblemHost.Content = null;
            _floatingEmblemHost.IsVisible = false;
        }
    }
}
