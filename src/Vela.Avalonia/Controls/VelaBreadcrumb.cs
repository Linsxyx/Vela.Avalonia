using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Declarative path segment inside <see cref="VelaBreadcrumb"/>.
/// </summary>
public class VelaBreadcrumbItem : AvaloniaObject
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<VelaBreadcrumbItem, string?>(nameof(Label));

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaBreadcrumbItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<VelaBreadcrumbItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<VelaBreadcrumbItem, object?>(nameof(CommandParameter));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    public event EventHandler? Click;

    internal void Invoke()
    {
        Click?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }
}

/// <summary>
/// Port of Arc UI's Breadcrumb:
/// Clean horizontal path navigation trail with chevron separators, interactive ancestor buttons,
/// bold current segment, and spring slide-in when deeper path segments are appended.
/// </summary>
public class VelaBreadcrumb : TemplatedControl
{
    [Content]
    public ObservableCollection<VelaBreadcrumbItem> Items { get; } = new();

    public event EventHandler<VelaBreadcrumbItem>? ItemClicked;

    protected override Type StyleKeyOverride => typeof(VelaBreadcrumb);

    private StackPanel? _host;
    private readonly SpringChannel _enterSpring = new(1.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;
    private Control? _enteringNode;
    private TranslateTransform? _enteringTranslate;
    private int _lastCount;
    private bool _templateReady;

    public VelaBreadcrumb()
    {
        _runner = new SpringRunner(this, OnTick);
        Items.CollectionChanged += OnItemsChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<StackPanel>("PART_Host");
        _templateReady = true;
        _lastCount = Items.Count;
        Rebuild(animateTail: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var grew = _templateReady && Items.Count > _lastCount;
        _lastCount = Items.Count;
        Rebuild(animateTail: grew);
    }

    private void Rebuild(bool animateTail)
    {
        if (_host is null)
            return;

        _host.Children.Clear();
        _enteringNode = null;
        _enteringTranslate = null;

        var chevronGeom = ResolveGeometry("VelaIconChevronRight");
        var iconBtnTheme = ResolveResource("VelaIconButtonTheme") as ControlTheme;

        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var isCurrent = i == Items.Count - 1;
            var segment = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (i > 0)
            {
                segment.Children.Add(new PathIcon
                {
                    Width = 12,
                    Height = 12,
                    Data = chevronGeom,
                    Margin = new Thickness(2, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    [!PathIcon.ForegroundProperty] = this.GetResourceObservable("VelaTextMutedBrush").ToBinding(),
                });
            }

            var inner = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (item.Icon is not null)
            {
                inner.Children.Add(new PathIcon
                {
                    Width = 13,
                    Height = 13,
                    Data = item.Icon,
                    VerticalAlignment = VerticalAlignment.Center,
                    [!PathIcon.ForegroundProperty] = this.GetResourceObservable(
                        isCurrent ? "VelaForegroundBrush" : "VelaTextSecondaryBrush").ToBinding(),
                });
            }

            inner.Children.Add(new TextBlock
            {
                Text = item.Label,
                FontSize = 13.5,
                FontWeight = isCurrent ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center,
                [!TextBlock.ForegroundProperty] = this.GetResourceObservable(
                    isCurrent ? "VelaForegroundBrush" : "VelaTextSecondaryBrush").ToBinding(),
            });

            if (isCurrent)
            {
                segment.Children.Add(new Border
                {
                    Padding = new Thickness(6, 4),
                    Child = inner,
                });
            }
            else
            {
                var btn = new Button
                {
                    Theme = iconBtnTheme,
                    Padding = new Thickness(7, 4),
                    CornerRadius = new CornerRadius(8),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Content = inner,
                };
                btn.Click += (_, _) =>
                {
                    item.Invoke();
                    ItemClicked?.Invoke(this, item);
                };
                segment.Children.Add(btn);
            }

            if (animateTail && isCurrent && !VelaMotionOptions.ReducedMotion)
            {
                _enteringTranslate = new TranslateTransform(-8, 0);
                segment.RenderTransform = _enteringTranslate;
                segment.Opacity = 0;
                _enteringNode = segment;
                _enterSpring.Jump(0.0);
                _enterSpring.SetTarget(1.0, VelaMotionTokens.Springs.Smooth);
                _runner.Wake();
            }

            _host.Children.Add(segment);
        }
    }

    private bool OnTick(double dt)
    {
        var active = _enterSpring.Step(dt);
        if (_enteringNode is not null && _enteringTranslate is not null)
        {
            var p = Math.Clamp(_enterSpring.Value, 0.0, 1.0);
            _enteringNode.Opacity = p;
            _enteringTranslate.X = (1.0 - p) * -8.0;
        }
        return active;
    }

    private Geometry? ResolveGeometry(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is Geometry g ? g : null;

    private object? ResolveResource(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) ? res : null;
}
