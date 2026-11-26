using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Vela.Avalonia.Controls;

public enum VelaConfirmMorphState
{
    Idle,
    Confirming,
    Busy,
    Done,
}

/// <summary>
/// Port of Arc UI's ConfirmMorph:
/// A single pill that smoothly springs its width in place across
/// Idle -> Confirming ("Delete? Cancel / Confirm") -> Busy (spinner) -> Done ("Deleted · Undo").
/// Zero modal dialog interruption for destructive actions.
/// </summary>
[PseudoClasses(":idle", ":confirming", ":busy", ":done")]
public class VelaConfirmMorph : TemplatedControl
{
    public static readonly StyledProperty<string> IdleTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(IdleText), "Delete");

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string> PromptTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(PromptText), "Confirm?");

    public static readonly StyledProperty<string> ConfirmTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(ConfirmText), "Delete");

    public static readonly StyledProperty<string> CancelTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(CancelText), "Cancel");

    public static readonly StyledProperty<string> BusyTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(BusyText), "Working...");

    public static readonly StyledProperty<string> DoneTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string>(nameof(DoneText), "Done");

    public static readonly StyledProperty<string?> UndoTextProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, string?>(nameof(UndoText), "Undo");

    public static readonly StyledProperty<TimeSpan> AutoResetDelayProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, TimeSpan>(nameof(AutoResetDelay), TimeSpan.FromSeconds(3.2));

    public static readonly StyledProperty<Func<Task>?> ConfirmActionProperty =
        AvaloniaProperty.Register<VelaConfirmMorph, Func<Task>?>(nameof(ConfirmAction));

    public static readonly DirectProperty<VelaConfirmMorph, VelaConfirmMorphState> StateProperty =
        AvaloniaProperty.RegisterDirect<VelaConfirmMorph, VelaConfirmMorphState>(nameof(State), o => o.State);

    public string IdleText { get => GetValue(IdleTextProperty); set => SetValue(IdleTextProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string PromptText { get => GetValue(PromptTextProperty); set => SetValue(PromptTextProperty, value); }
    public string ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }
    public string CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }
    public string BusyText { get => GetValue(BusyTextProperty); set => SetValue(BusyTextProperty, value); }
    public string DoneText { get => GetValue(DoneTextProperty); set => SetValue(DoneTextProperty, value); }
    public string? UndoText { get => GetValue(UndoTextProperty); set => SetValue(UndoTextProperty, value); }
    public TimeSpan AutoResetDelay { get => GetValue(AutoResetDelayProperty); set => SetValue(AutoResetDelayProperty, value); }
    public Func<Task>? ConfirmAction { get => GetValue(ConfirmActionProperty); set => SetValue(ConfirmActionProperty, value); }

    private VelaConfirmMorphState _state = VelaConfirmMorphState.Idle;
    public VelaConfirmMorphState State
    {
        get => _state;
        private set
        {
            if (SetAndRaise(StateProperty, ref _state, value))
                UpdatePseudoClasses();
        }
    }

    public event EventHandler? Confirmed;
    public event EventHandler? Undone;

    protected override Type StyleKeyOverride => typeof(VelaConfirmMorph);

    private Button? _idleBtn;
    private Button? _cancelBtn;
    private Button? _confirmBtn;
    private Button? _undoBtn;
    private DispatcherTimer? _resetTimer;

    public VelaConfirmMorph()
    {
        UpdatePseudoClasses();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        DetachButtons();
        _idleBtn = e.NameScope.Find<Button>("PART_IdleButton");
        _cancelBtn = e.NameScope.Find<Button>("PART_CancelButton");
        _confirmBtn = e.NameScope.Find<Button>("PART_ConfirmButton");
        _undoBtn = e.NameScope.Find<Button>("PART_UndoButton");

        if (_idleBtn is not null) _idleBtn.Click += OnIdleClick;
        if (_cancelBtn is not null) _cancelBtn.Click += OnCancelClick;
        if (_confirmBtn is not null) _confirmBtn.Click += OnConfirmClick;
        if (_undoBtn is not null) _undoBtn.Click += OnUndoClick;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopResetTimer();
        DetachButtons();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && State == VelaConfirmMorphState.Confirming)
        {
            State = VelaConfirmMorphState.Idle;
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    public void Reset()
    {
        StopResetTimer();
        State = VelaConfirmMorphState.Idle;
    }

    private void DetachButtons()
    {
        if (_idleBtn is not null) _idleBtn.Click -= OnIdleClick;
        if (_cancelBtn is not null) _cancelBtn.Click -= OnCancelClick;
        if (_confirmBtn is not null) _confirmBtn.Click -= OnConfirmClick;
        if (_undoBtn is not null) _undoBtn.Click -= OnUndoClick;
    }

    private void OnIdleClick(object? sender, RoutedEventArgs e)
    {
        if (State != VelaConfirmMorphState.Idle) return;
        StopResetTimer();
        State = VelaConfirmMorphState.Confirming;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (State == VelaConfirmMorphState.Confirming)
            State = VelaConfirmMorphState.Idle;
    }

    private async void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (State != VelaConfirmMorphState.Confirming) return;
        State = VelaConfirmMorphState.Busy;

        try
        {
            if (ConfirmAction is not null)
                await ConfirmAction();
            else
                await Task.Delay(550);

            Confirmed?.Invoke(this, EventArgs.Empty);
            State = VelaConfirmMorphState.Done;
            StartResetTimer();
        }
        catch
        {
            State = VelaConfirmMorphState.Idle;
        }
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (State != VelaConfirmMorphState.Done) return;
        StopResetTimer();
        Undone?.Invoke(this, EventArgs.Empty);
        State = VelaConfirmMorphState.Idle;
    }

    private void StartResetTimer()
    {
        StopResetTimer();
        if (AutoResetDelay <= TimeSpan.Zero) return;
        _resetTimer = new DispatcherTimer { Interval = AutoResetDelay };
        _resetTimer.Tick += (_, _) =>
        {
            StopResetTimer();
            if (State == VelaConfirmMorphState.Done)
                State = VelaConfirmMorphState.Idle;
        };
        _resetTimer.Start();
    }

    private void StopResetTimer()
    {
        _resetTimer?.Stop();
        _resetTimer = null;
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":idle", State == VelaConfirmMorphState.Idle);
        PseudoClasses.Set(":confirming", State == VelaConfirmMorphState.Confirming);
        PseudoClasses.Set(":busy", State == VelaConfirmMorphState.Busy);
        PseudoClasses.Set(":done", State == VelaConfirmMorphState.Done);
    }
}
