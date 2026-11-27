using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Port of Arc UI's TagInput:
/// - Type text and press Enter or Comma to create removable pill tags
/// - Press Backspace on an empty input to remove the last tag
/// - Spring pop-in on newly created tags, duplicate prevention, optional MaxTags cap
/// </summary>
[PseudoClasses(":focused")]
public class VelaTagInput : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaTagInput, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaTagInput, string?>(nameof(Description));

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<VelaTagInput, string?>(nameof(PlaceholderText), "Add tag and press Enter...");

    public static readonly StyledProperty<int> MaxTagsProperty =
        AvaloniaProperty.Register<VelaTagInput, int>(nameof(MaxTags), 20);

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public int MaxTags { get => GetValue(MaxTagsProperty); set => SetValue(MaxTagsProperty, Math.Max(1, value)); }

    public ObservableCollection<string> Tags { get; } = new();

    protected override Type StyleKeyOverride => typeof(VelaTagInput);

    private WrapPanel? _wrapHost;
    private TextBox? _entryBox;
    private Border? _shell;

    public VelaTagInput()
    {
        Tags.CollectionChanged += (_, _) => RebuildTags();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_shell is not null)
            _shell.PointerPressed -= OnShellPointerPressed;

        _wrapHost = e.NameScope.Find<WrapPanel>("PART_WrapHost");
        _shell = e.NameScope.Find<Border>("PART_Shell");

        if (_shell is not null)
            _shell.PointerPressed += OnShellPointerPressed;

        EnsureEntryBox();
        RebuildTags();
    }

    private void EnsureEntryBox()
    {
        if (_entryBox is not null) return;
        _entryBox = new TextBox
        {
            MinWidth = 110,
            Height = 28,
            FontSize = 13.5,
            VerticalAlignment = VerticalAlignment.Center,
            PlaceholderText = PlaceholderText,
        };
        if (this.TryFindResource("VelaBareTextBoxTheme", ActualThemeVariant, out var res) && res is ControlTheme ct)
            _entryBox.Theme = ct;

        _entryBox.KeyDown += OnEntryKeyDown;
        _entryBox.GotFocus += (_, _) => PseudoClasses.Set(":focused", true);
        _entryBox.LostFocus += (_, _) =>
        {
            PseudoClasses.Set(":focused", false);
            TryCommitPendingText();
        };
    }

    private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _entryBox?.Focus();
    }

    private void OnEntryKeyDown(object? sender, KeyEventArgs e)
    {
        if (_entryBox is null) return;

        if (e.Key is Key.Enter or Key.OemComma)
        {
            TryCommitPendingText();
            e.Handled = true;
        }
        else if (e.Key == Key.Back && string.IsNullOrEmpty(_entryBox.Text) && Tags.Count > 0)
        {
            Tags.RemoveAt(Tags.Count - 1);
            e.Handled = true;
        }
    }

    private void TryCommitPendingText()
    {
        if (_entryBox is null) return;
        var raw = _entryBox.Text?.Trim().TrimEnd(',');
        if (string.IsNullOrWhiteSpace(raw))
        {
            _entryBox.Text = string.Empty;
            return;
        }

        if (Tags.Count < MaxTags && !Tags.Contains(raw, StringComparer.OrdinalIgnoreCase))
            Tags.Add(raw);

        _entryBox.Text = string.Empty;
    }

    private void RebuildTags()
    {
        if (_wrapHost is null) return;
        EnsureEntryBox();

        _wrapHost.Children.Clear();
        for (var i = 0; i < Tags.Count; i++)
        {
            var tag = Tags[i];
            var animateIn = i == Tags.Count - 1;
            _wrapHost.Children.Add(new AnimatedTagPill(tag, () => Tags.Remove(tag), animateIn));
        }

        if (_entryBox is not null)
        {
            _entryBox.PlaceholderText = Tags.Count == 0 ? PlaceholderText : "Add more...";
            _wrapHost.Children.Add(_entryBox);
        }
    }

    private sealed class AnimatedTagPill : Border
    {
        private readonly SpringChannel _scale = new(0.86, VelaMotionTokens.Springs.Snappy);
        private readonly SpringChannel _opacity = new(0.0, VelaMotionTokens.Springs.Snappy);
        private readonly SpringRunner _runner;
        private readonly ScaleTransform _scaleTransform = new(1.0, 1.0);
        private readonly bool _animateIn;

        public AnimatedTagPill(string text, Action onRemove, bool animateIn)
        {
            _animateIn = animateIn;
            _runner = new SpringRunner(this, OnTick);

            Height = 28;
            CornerRadius = new CornerRadius(999);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(10, 0, 4, 0);
            RenderTransformOrigin = RelativePoint.Center;
            RenderTransform = _scaleTransform;

            this[!BackgroundProperty] = VelaResourceBinding.Create("VelaSurfaceMutedBrush");
            this[!BorderBrushProperty] = VelaResourceBinding.Create("VelaBorderBrush");

            var closeBtn = new Button
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(999),
                Focusable = false,
                Content = new PathIcon
                {
                    Width = 10,
                    Height = 10,
                    [!PathIcon.DataProperty] = VelaResourceBinding.Create("VelaIconClose"),
                    [!PathIcon.ForegroundProperty] = VelaResourceBinding.Create("VelaTextSecondaryBrush"),
                },
            };
            closeBtn[!ThemeProperty] = VelaResourceBinding.Create("VelaIconButtonTheme");
            closeBtn.Click += (_, _) => onRemove();

            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = text,
                        FontSize = 13,
                        FontWeight = FontWeight.Medium,
                        VerticalAlignment = VerticalAlignment.Center,
                        [!TextBlock.ForegroundProperty] = VelaResourceBinding.Create("VelaForegroundBrush"),
                    },
                    closeBtn,
                },
            };
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            if (!_animateIn || VelaMotionOptions.ReducedMotion)
            {
                _scale.Jump(1.0);
                _opacity.Jump(1.0);
                ApplyVisuals();
                return;
            }

            _scale.Jump(0.86);
            _opacity.Jump(0.0);
            ApplyVisuals();
            _scale.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _opacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
            _runner.Wake();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _runner.Stop();
        }

        private bool OnTick(double dt)
        {
            var active = _scale.Step(dt) | _opacity.Step(dt);
            ApplyVisuals();
            return active;
        }

        private void ApplyVisuals()
        {
            var s = Math.Max(0.5, _scale.Value);
            _scaleTransform.ScaleX = s;
            _scaleTransform.ScaleY = s;
            Opacity = Math.Clamp(_opacity.Value, 0.0, 1.0);
        }
    }
}
