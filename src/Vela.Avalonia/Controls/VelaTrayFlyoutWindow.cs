using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Frameless, topmost, transparent floating window used by <see cref="VelaTrayIcon"/>
/// for <see cref="VelaTrayPresentation.VelaMenu"/> and <see cref="VelaTrayPresentation.VelaPopover"/> modes.
/// Features spring entrance motion, optional gliding hover highlight, and <see cref="VelaSurfaceMaterial"/> styling.
/// </summary>
internal sealed class VelaTrayFlyoutWindow : Window
{
    private readonly SpringChannel _hlY = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlH = new(34.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _hlOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private VelaPopupSurface? _surface;
    private Grid? _menuTrack;
    private Border? _highlight;
    private TranslateTransform? _hlTranslate;
    private bool _highlightPlaced;

    private ContentPresenter? _headerPresenter;
    private ContentPresenter? _popoverPresenter;
    private ContentPresenter? _footerPresenter;

    private bool _isDismissing;
    private long _presentedTicks;

    public event EventHandler? Dismissed;

    public VelaTrayFlyoutWindow()
    {
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;
        TransparencyBackgroundFallback = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];

        _runner = new SpringRunner(this, OnTick);

        Deactivated += (_, _) =>
        {
            if (Environment.TickCount64 - _presentedTicks < 140)
                return;
            RequestDismiss();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                RequestDismiss();
            }
        };
    }

    public void Present(VelaTrayIcon owner, PixelPoint? anchorPoint)
    {
        _isDismissing = false;
        _highlightPlaced = false;
        _hlOpacity.Jump(0.0);

        BuildContent(owner);

        // Measure before positioning so we know exact desired dimensions
        Measure(new Size(
            Math.Max(owner.FlyoutMinWidth, owner.FlyoutMaxWidth) + 24,
            double.PositiveInfinity));

        var primaryScaling = Screens?.Primary?.Scaling ?? 1.0;
        var resolvedAnchor = anchorPoint ?? VelaTrayFlyoutPositioner.TryGetCursorScreenPosition(primaryScaling);
        var screen = VelaTrayFlyoutPositioner.ResolveScreen(Screens, resolvedAnchor);

        var anchoredAtTop = OperatingSystem.IsMacOS();
        if (screen is not null)
        {
            var desired = DesiredSize;
            var estWidth = desired.Width > 10 ? desired.Width : owner.FlyoutMinWidth + 24;
            var estHeight = desired.Height > 10 ? desired.Height : 220.0;
            var pxWidth = Math.Max(1, (int)Math.Ceiling(estWidth * screen.Scaling));
            var pxHeight = Math.Max(1, (int)Math.Ceiling(estHeight * screen.Scaling));
            var edgeMargin = (int)Math.Round(10 * screen.Scaling);

            var layout = VelaTrayFlyoutPositioner.ComputePosition(
                screen.WorkingArea,
                new PixelSize(pxWidth, pxHeight),
                resolvedAnchor,
                owner.FlyoutPlacement,
                OperatingSystem.IsMacOS(),
                edgeMargin);

            Position = layout.Position;
            anchoredAtTop = layout.AnchoredAtTop;
        }

        if (_surface is not null)
        {
            _surface.EnterOffsetX = 0.0;
            _surface.EnterOffsetY = anchoredAtTop ? -6.0 : 6.0;
            _surface.RenderTransformOrigin = anchoredAtTop
                ? new RelativePoint(0.5, 0.0, RelativeUnit.Relative)
                : new RelativePoint(0.5, 1.0, RelativeUnit.Relative);
        }

        _presentedTicks = Environment.TickCount64;
        if (OperatingSystem.IsMacOS())
            VelaMacOsStatusItem.ActivateApplication();

        Show();
        Activate();
        _surface?.PlayEnter();

        // Refine position once real layout bounds are finalized (covers dynamic content / fonts)
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsVisible || screen is null)
                return;

            var actual = Bounds.Size;
            if (actual.Width <= 1 || actual.Height <= 1)
                return;

            var pxWidth = Math.Max(1, (int)Math.Ceiling(actual.Width * screen.Scaling));
            var pxHeight = Math.Max(1, (int)Math.Ceiling(actual.Height * screen.Scaling));
            var edgeMargin = (int)Math.Round(10 * screen.Scaling);

            var refined = VelaTrayFlyoutPositioner.ComputePosition(
                screen.WorkingArea,
                new PixelSize(pxWidth, pxHeight),
                resolvedAnchor,
                owner.FlyoutPlacement,
                OperatingSystem.IsMacOS(),
                edgeMargin);

            Position = refined.Position;
        }, DispatcherPriority.Loaded);
    }

    public void RequestDismiss()
    {
        if (_isDismissing || !IsVisible)
            return;

        _isDismissing = true;
        _runner.Stop();
        Hide();
        DetachHostedContent();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    public void DetachHostedContent()
    {
        if (_headerPresenter is not null) _headerPresenter.Content = null;
        if (_popoverPresenter is not null) _popoverPresenter.Content = null;
        if (_footerPresenter is not null) _footerPresenter.Content = null;
        if (_menuTrack is not null) _menuTrack.PointerExited -= OnMenuPointerExited;
        _menuTrack = null;
        _highlight = null;
        _hlTranslate = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _runner.Stop();
        DetachHostedContent();
        base.OnClosed(e);
    }

    private void BuildContent(VelaTrayIcon owner)
    {
        DetachHostedContent();

        var isPopoverMode = owner.Presentation == VelaTrayPresentation.VelaPopover && owner.PopoverContent is not null;

        var bodyStack = new StackPanel
        {
            Spacing = 0,
        };

        if (owner.FlyoutHeader is not null)
        {
            _headerPresenter = new ContentPresenter
            {
                Content = owner.FlyoutHeader,
                Margin = new Thickness(6, 4, 6, 6),
            };
            bodyStack.Children.Add(_headerPresenter);
            bodyStack.Children.Add(BuildDivider());
        }

        if (isPopoverMode)
        {
            _popoverPresenter = new ContentPresenter
            {
                Content = owner.PopoverContent,
                Margin = new Thickness(4),
            };
            bodyStack.Children.Add(_popoverPresenter);
        }
        else
        {
            bodyStack.Children.Add(BuildMenuTrack(owner));
        }

        if (owner.FlyoutFooter is not null)
        {
            bodyStack.Children.Add(BuildDivider());
            _footerPresenter = new ContentPresenter
            {
                Content = owner.FlyoutFooter,
                Margin = new Thickness(6, 6, 6, 4),
            };
            bodyStack.Children.Add(_footerPresenter);
        }

        _surface = WrapInMaterialSurface(bodyStack, owner);
        Content = _surface;
    }

    private VelaPopupSurface WrapInMaterialSurface(Control inner, VelaTrayIcon owner)
    {
        var material = owner.SurfaceMaterial;
        var minWidth = Math.Max(140.0, owner.FlyoutMinWidth);
        var maxWidth = Math.Max(minWidth, owner.FlyoutMaxWidth);
        var isDark = ActualThemeVariant == ThemeVariant.Dark;

        if (material == VelaSurfaceMaterial.GlassCard)
        {
            var glassCard = new VelaGlassCard
            {
                MinWidth = minWidth,
                MaxWidth = maxWidth,
                Padding = owner.FlyoutPadding,
                CornerRadius = new CornerRadius(16),
                BlurRadius = 56.0,
                TintLevel = VelaGlassTintLevel.HighContrast,
                Content = inner,
            };

            return new VelaPopupSurface
            {
                Margin = new Thickness(12, 8, 12, 16),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Child = glassCard,
            };
        }

        if (material == VelaSurfaceMaterial.LiquidGlass)
        {
            var liquidGlass = new VelaLiquidGlass
            {
                MinWidth = minWidth,
                MaxWidth = maxWidth,
                Padding = owner.FlyoutPadding,
                CornerRadius = new CornerRadius(16),
                Content = inner,
            };

            return new VelaPopupSurface
            {
                Margin = new Thickness(12, 8, 12, 16),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Child = liquidGlass,
            };
        }

        var surface = new VelaPopupSurface
        {
            Margin = new Thickness(10, 6, 10, 14),
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Padding = owner.FlyoutPadding,
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            Child = inner,
            [!Border.BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush"),
            [!Border.BoxShadowProperty] = VelaResourceBinding.Create("VelaShadowFloating"),
        };

        if (material == VelaSurfaceMaterial.Frosted)
        {
            surface.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(0xDC, 0x1F, 0x1F, 0x22))
                : new SolidColorBrush(Color.FromArgb(0xE2, 0xFF, 0xFF, 0xFF));
            VelaBackdropCacheHelper.ApplyBlur(surface, enabled: true, radius: 24.0);
        }
        else
        {
            surface[!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceRaisedBrush");
        }

        return surface;
    }

    private Control BuildMenuTrack(VelaTrayIcon owner)
    {
        _hlTranslate = new TranslateTransform();
        _highlight = new Border
        {
            CornerRadius = new CornerRadius(10),
            IsHitTestVisible = false,
            Opacity = 0.0,
            RenderTransform = _hlTranslate,
            [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceMutedBrush"),
        };

        var canvas = new Canvas
        {
            IsHitTestVisible = false,
            Children = { _highlight },
        };

        var itemsHost = new StackPanel
        {
            Spacing = 1,
        };

        var hasAnyCheckable = owner.MenuItems.Any(i => i.IsVisible && !i.IsSeparator && i.IsChecked);

        foreach (var item in owner.MenuItems)
        {
            if (!item.IsVisible)
                continue;

            if (item.IsSeparator)
            {
                itemsHost.Children.Add(BuildDivider());
                continue;
            }

            itemsHost.Children.Add(BuildMenuRow(item, hasAnyCheckable));
        }

        if (itemsHost.Children.Count == 0)
        {
            itemsHost.Children.Add(new TextBlock
            {
                Text = "No tray actions configured",
                FontSize = 12.5,
                Margin = new Thickness(10, 8),
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
            });
        }

        _menuTrack = new Grid
        {
            Children = { canvas, itemsHost },
        };
        _menuTrack.PointerExited += OnMenuPointerExited;
        return _menuTrack;
    }

    private Control BuildMenuRow(VelaMenuItem item, bool reserveCheckColumn)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 9,
            MinHeight = 34,
        };

        var fgKey = item.IsDestructive ? "VelaDangerBrush" : "VelaForegroundBrush";
        var iconKey = item.IsDestructive ? "VelaDangerBrush" : "VelaTextSecondaryBrush";

        if (item.IsChecked)
        {
            var checkIcon = new PathIcon
            {
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center,
                [!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconCheck"),
                [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaAccentBrush"),
            };
            grid.Children.Add(checkIcon);
        }
        else if (item.Icon is not null)
        {
            var icon = new PathIcon
            {
                Width = 15,
                Height = 15,
                Data = item.Icon,
                VerticalAlignment = VerticalAlignment.Center,
                [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create(iconKey),
            };
            grid.Children.Add(icon);
        }
        else if (reserveCheckColumn)
        {
            grid.Children.Add(new Border { Width = 14, Height = 14 });
        }

        var label = new TextBlock
        {
            Text = item.Header,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create(fgKey),
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (!string.IsNullOrEmpty(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaTextMutedBrush"),
            };
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        var rowBorder = new Border
        {
            Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            Cursor = item.IsEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Opacity = item.IsEnabled ? 1.0 : 0.45,
            Child = grid,
        };

        if (item.IsEnabled)
        {
            rowBorder.PointerEntered += (_, _) => HighlightRow(rowBorder, item.IsDestructive);
            rowBorder.PointerPressed += (_, ev) =>
            {
                if (ev.GetCurrentPoint(rowBorder).Properties.IsLeftButtonPressed)
                {
                    ev.Handled = true;
                    RequestDismiss();
                    item.Invoke();
                }
            };
        }

        return rowBorder;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Margin = new Thickness(4, 4),
        [!Border.BackgroundProperty] = VelaResourceBinding.Create("VelaBorderSubtleBrush"),
    };

    private void OnMenuPointerExited(object? sender, PointerEventArgs e)
    {
        _hlOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private void HighlightRow(Control row, bool destructive)
    {
        if (_menuTrack is null || _highlight is null)
            return;

        var pos = row.TranslatePoint(new Point(0, 0), _menuTrack);
        if (!pos.HasValue)
            return;

        var bgKey = destructive ? "VelaDangerSubtleBrush" : "VelaSurfaceMutedBrush";
        if (this.TryFindResource(bgKey, ActualThemeVariant, out var res) && res is IBrush brush)
            _highlight.Background = brush;

        _highlight.Width = _menuTrack.Bounds.Width;

        if (!_highlightPlaced || VelaMotionOptions.ReducedMotion)
        {
            _highlightPlaced = true;
            _hlY.Jump(pos.Value.Y);
            _hlH.Jump(row.Bounds.Height);
            _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            ApplyHighlight();
            _runner.Wake();
            return;
        }

        _hlY.SetTarget(pos.Value.Y, VelaMotionTokens.Springs.Snappy);
        _hlH.SetTarget(row.Bounds.Height, VelaMotionTokens.Springs.Snappy);
        _hlOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _hlY.Step(dt);
        active |= _hlH.Step(dt);
        active |= _hlOpacity.Step(dt);
        ApplyHighlight();
        return active;
    }

    private void ApplyHighlight()
    {
        if (_highlight is null || _hlTranslate is null)
            return;

        _hlTranslate.Y = _hlY.Value;
        _highlight.Height = Math.Max(0, _hlH.Value);
        _highlight.Opacity = Math.Clamp(_hlOpacity.Value, 0.0, 1.0);
    }
}
