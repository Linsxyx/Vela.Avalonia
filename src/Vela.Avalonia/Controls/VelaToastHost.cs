using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public enum VelaToastTone
{
    Neutral,
    Success,
    Warning,
    Danger,
    Info,
}

public sealed class VelaToastOptions
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public Control? Content { get; init; }
    public VelaToastTone Tone { get; init; } = VelaToastTone.Neutral;
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(4);
    public bool DismissOnClick { get; init; }
    public string? ActionLabel { get; init; }
    public Action? OnAction { get; init; }
}

/// <summary>
/// Clean, non-intrusive stacking toast host inspired by Arc UI's Toast / ToastStack:
/// - Smooth spring entrance and AnimatePresence spring exit before removal
/// - Hover pauses auto-dismiss timer cleanly
/// - Supports semantic tones, optional inline action button (e.g. Undo), and max stack cap
/// </summary>
public class VelaToastHost : Panel
{
    public static readonly StyledProperty<int> MaxVisibleProperty =
        AvaloniaProperty.Register<VelaToastHost, int>(nameof(MaxVisible), 4);

    public int MaxVisible
    {
        get => GetValue(MaxVisibleProperty);
        set => SetValue(MaxVisibleProperty, Math.Max(1, value));
    }

    private readonly StackPanel _stack;
    private readonly List<ToastCard> _activeToasts = new();

    public VelaToastHost()
    {
        IsHitTestVisible = true;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(20);
        ZIndex = 1100;

        _stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        Children.Add(_stack);
    }

    public void Show(
        string title,
        string? description = null,
        VelaToastTone tone = VelaToastTone.Neutral,
        TimeSpan? duration = null,
        string? actionLabel = null,
        Action? onAction = null)
    {
        Show(new VelaToastOptions
        {
            Title = title,
            Description = description,
            Tone = tone,
            Duration = duration ?? TimeSpan.FromSeconds(4),
            ActionLabel = actionLabel,
            OnAction = onAction,
        });
    }

    public void Show(VelaToastOptions options) => _ = ShowWithHandle(options);

    public IDisposable ShowWithHandle(VelaToastOptions options)
    {
        var card = new ToastCard(options, OnCardDismissed);
        _activeToasts.Add(card);
        _stack.Children.Add(card);

        // Enforce stack cap by dismissing oldest active toast gracefully
        while (_activeToasts.Count > MaxVisible)
        {
            var oldest = _activeToasts[0];
            _activeToasts.RemoveAt(0);
            oldest.Dismiss();
        }

        return new ToastDismissHandle(card);
    }

    private sealed class ToastDismissHandle(ToastCard card) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Dispatcher.UIThread.Post(card.Dismiss);
        }
    }

    private void OnCardDismissed(ToastCard card)
    {
        _activeToasts.Remove(card);
        _stack.Children.Remove(card);
    }

    private sealed class ToastCard : Border
    {
        private readonly SpringChannel _opacity = new(0.0, VelaMotionTokens.Springs.Snappy);
        private readonly SpringChannel _y = new(14.0, VelaMotionTokens.Springs.Snappy);
        private readonly SpringChannel _scale = new(0.96, VelaMotionTokens.Springs.Snappy);
        private readonly SpringRunner _runner;
        private readonly TranslateTransform _translate = new(0, 14);
        private readonly ScaleTransform _scaleTransform = new(0.96, 0.96);
        private readonly DispatcherTimer? _timer;
        private readonly Action<ToastCard> _onRemoved;
        private bool _dismissing;

        public ToastCard(VelaToastOptions options, Action<ToastCard> onRemoved)
        {
            _onRemoved = onRemoved;
            _runner = new SpringRunner(this, OnTick);

            Width = 344;
            CornerRadius = new CornerRadius(14);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(14, 12);
            Opacity = 0;
            RenderTransformOrigin = RelativePoint.Center;
            RenderTransform = new TransformGroup { Children = { _scaleTransform, _translate } };

            Bind(BackgroundProperty, this.GetResourceObservable("VelaSurfaceRaisedBrush"));
            Bind(BorderBrushProperty, this.GetResourceObservable("VelaBorderBrush"));
            Bind(BoxShadowProperty, this.GetResourceObservable("VelaShadowRaised"));

            Child = BuildContent(options);

            if (options.DismissOnClick)
            {
                Cursor = new Cursor(StandardCursorType.Hand);
                PointerPressed += (_, _) => Dismiss();
            }

            if (options.Duration > TimeSpan.Zero)
            {
                _timer = new DispatcherTimer { Interval = options.Duration };
                _timer.Tick += (_, _) =>
                {
                    _timer.Stop();
                    Dismiss();
                };
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _timer?.Start();

            if (VelaMotionOptions.ReducedMotion)
            {
                _opacity.Jump(1.0);
                _y.Jump(0.0);
                _scale.Jump(1.0);
                ApplyVisuals();
                return;
            }

            _opacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _y.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _scale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _timer?.Stop();
            _runner.Stop();
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _timer?.Stop();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (!_dismissing)
                _timer?.Start();
        }

        public void Dismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            _timer?.Stop();
            IsHitTestVisible = false;

            if (VelaMotionOptions.ReducedMotion)
            {
                _onRemoved(this);
                return;
            }

            _opacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
            _y.SetTarget(8.0, VelaMotionTokens.Springs.Snappy);
            _scale.SetTarget(0.95, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        private bool OnTick(double dt)
        {
            var active = _opacity.Step(dt);
            active |= _y.Step(dt);
            active |= _scale.Step(dt);
            ApplyVisuals();

            if (!active && _dismissing)
                _onRemoved(this);

            return active;
        }

        private void ApplyVisuals()
        {
            Opacity = Math.Clamp(_opacity.Value, 0.0, 1.0);
            _translate.Y = _y.Value;
            var s = Math.Max(0.5, _scale.Value);
            _scaleTransform.ScaleX = s;
            _scaleTransform.ScaleY = s;
        }

        private Control BuildContent(VelaToastOptions options)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
                ColumnSpacing = 10,
            };

            // Tone icon
            var (iconKey, brushKey) = options.Tone switch
            {
                VelaToastTone.Success => ("VelaIconCheckCircle", "VelaSuccessBrush"),
                VelaToastTone.Warning => ("VelaIconAlertCircle", "VelaWarningBrush"),
                VelaToastTone.Danger => ("VelaIconAlertCircle", "VelaDangerBrush"),
                VelaToastTone.Info => ("VelaIconInfo", "VelaInfoBrush"),
                _ => ("VelaIconBell", "VelaTextSecondaryBrush"),
            };

            var icon = new PathIcon
            {
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
                [!PathIcon.DataProperty] = this.GetResourceObservable(iconKey).ToBinding(),
                [!PathIcon.ForegroundProperty] = this.GetResourceObservable(brushKey).ToBinding(),
            };
            grid.Children.Add(icon);

            // Title + description
            var textStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = options.Title,
                FontSize = 13.5,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaForegroundBrush").ToBinding(),
            });

            if (!string.IsNullOrEmpty(options.Description))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = options.Description,
                    FontSize = 12.5,
                    TextWrapping = TextWrapping.Wrap,
                    [!TextBlock.ForegroundProperty] = this.GetResourceObservable("VelaTextSecondaryBrush").ToBinding(),
                });
            }

            if (options.Content is not null)
            {
                textStack.Children.Add(options.Content);
            }

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Optional Action Button
            if (!string.IsNullOrEmpty(options.ActionLabel))
            {
                var actionBtn = new VelaButton
                {
                    Variant = VelaButtonVariant.Secondary,
                    Size = VelaControlSize.Small,
                    MinHeight = 28,
                    Padding = new Thickness(10, 0),
                    CornerRadius = new CornerRadius(8),
                    Content = options.ActionLabel,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                actionBtn.Click += (_, _) =>
                {
                    options.OnAction?.Invoke();
                    Dismiss();
                };
                Grid.SetColumn(actionBtn, 2);
                grid.Children.Add(actionBtn);
            }

            // Quiet dismiss X button
            var closeBtn = new Button
            {
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new PathIcon
                {
                    Width = 11,
                    Height = 11,
                    [!PathIcon.DataProperty] = this.GetResourceObservable("VelaIconClose").ToBinding(),
                    [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
                },
            };
            closeBtn.Click += (_, _) => Dismiss();
            Grid.SetColumn(closeBtn, 3);
            grid.Children.Add(closeBtn);

            return grid;
        }
    }
}
