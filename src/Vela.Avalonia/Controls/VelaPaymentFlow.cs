using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// High-level stage of a <see cref="VelaPaymentFlow"/> transaction lifecycle.
/// </summary>
public enum VelaPaymentFlowStage
{
    Idle,
    CheckoutTap,
    ProcessorRiskCheck,
    NetworkRouting,
    BankAuthorization,
    AuthorizationReturn,
    SettlementClearing,
    SettlementFee,
    Settled,
}

/// <summary>
/// Visual state of an individual node card inside <see cref="VelaPaymentFlow"/>.
/// </summary>
public enum VelaPaymentNodeState
{
    Idle,
    Active,
    Completed,
    Success,
}

/// <summary>
/// Active path along which the amount chip is currently traveling.
/// </summary>
public enum VelaPaymentChipTrack
{
    None,
    TopForward,
    TopReturn,
    BottomSettlement,
}

/// <summary>
/// Immutable frame snapshot evaluated by <see cref="VelaPaymentFlowTimeline"/> for a given elapsed time.
/// </summary>
public readonly record struct VelaPaymentFlowSnapshot(
    VelaPaymentFlowStage Stage,
    VelaPaymentNodeState CheckoutState,
    string CheckoutStatus,
    VelaPaymentNodeState ProcessorState,
    string ProcessorStatus,
    VelaPaymentNodeState NetworkState,
    string NetworkStatus,
    VelaPaymentNodeState BankState,
    string BankStatus,
    VelaPaymentNodeState BalanceState,
    decimal DisplayedBalance,
    double AuthorizationProgress,
    double DisplayedAuthSeconds,
    double SettlementProgress,
    bool ChipVisible,
    double ChipOpacity,
    double ChipScale,
    VelaPaymentChipTrack ChipTrack,
    double ChipTrackPosition,
    double TopSegment0Fill,
    double TopSegment1Fill,
    double TopSegment2Fill,
    double TopReturnProgress,
    double BottomTrackFill,
    double MastercardDropActive,
    double StripeDropActive,
    double ContactlessPulse);

/// <summary>
/// Pure, deterministic choreography engine for <see cref="VelaPaymentFlow"/>.
/// Maps elapsed timeline seconds into node states, connector highlights, chip coordinates, and progress bars.
/// </summary>
public static class VelaPaymentFlowTimeline
{
    /// <summary>Total active animation duration before the end-of-cycle hold (seconds).</summary>
    public const double ActiveDuration = 8.15;

    /// <summary>Total cycle duration including the post-settlement hold (seconds).</summary>
    public const double TotalDuration = 10.00;

    /// <summary>Canonical stage anchor timestamps used for keyboard stepping.</summary>
    public static readonly double[] StageAnchors =
    [
        0.00, // Idle / start
        0.22, // CheckoutTap
        1.25, // ProcessorRiskCheck
        2.00, // NetworkRouting
        3.05, // BankAuthorization
        3.95, // AuthorizationReturn
        6.10, // SettlementClearing (matches reference preview frame)
        7.05, // SettlementFee
        8.35, // Settled
    ];

    public static VelaPaymentFlowSnapshot Evaluate(
        double elapsedSeconds,
        decimal amount,
        decimal initialBalance,
        double authorizationDurationSeconds,
        double mastercardBottomFraction = 0.44,
        double stripeBottomFraction = 0.72)
    {
        var t = Math.Clamp(elapsedSeconds, 0.0, TotalDuration);
        var authDur = Math.Max(0.01, authorizationDurationSeconds);

        // Phase 1: Authorization (0.00 .. 4.45)
        //   0.00 - 0.35 : Checkout tap & chip pop-in
        //   0.35 - 1.15 : Chip travels Checkout -> Stripe
        //   1.15 - 1.55 : Stripe risk check dwell
        //   1.55 - 2.25 : Chip travels Stripe -> Mastercard
        //   2.25 - 2.95 : Chip travels Mastercard -> Bank
        //   2.95 - 3.40 : Bank authorization verify & approve
        //   3.40 - 4.45 : Authorization returns Bank -> Mastercard -> Stripe -> Checkout
        //   4.45 - 4.95 : Post-authorization beat
        //
        // Phase 2: Settlement (4.95 .. 8.15)
        //   4.95 - 6.25 : Bank settled -> chip curves down & left to Mastercard junction (Mastercard: Clearing)
        //   6.25 - 7.25 : Chip travels Mastercard junction -> Stripe junction (Stripe: Settling)
        //   7.25 - 8.15 : Chip travels Stripe junction -> Your balance & credits balance
        //   8.15 - 10.00: Settled hold

        if (t <= 0.0001)
        {
            return new VelaPaymentFlowSnapshot(
                Stage: VelaPaymentFlowStage.Idle,
                CheckoutState: VelaPaymentNodeState.Idle,
                CheckoutStatus: "Ready to tap",
                ProcessorState: VelaPaymentNodeState.Idle,
                ProcessorStatus: "Processor",
                NetworkState: VelaPaymentNodeState.Idle,
                NetworkStatus: "Network",
                BankState: VelaPaymentNodeState.Idle,
                BankStatus: "Issuer",
                BalanceState: VelaPaymentNodeState.Idle,
                DisplayedBalance: initialBalance,
                AuthorizationProgress: 0.0,
                DisplayedAuthSeconds: 0.0,
                SettlementProgress: 0.0,
                ChipVisible: false,
                ChipOpacity: 0.0,
                ChipScale: 0.85,
                ChipTrack: VelaPaymentChipTrack.None,
                ChipTrackPosition: 0.0,
                TopSegment0Fill: 0.0,
                TopSegment1Fill: 0.0,
                TopSegment2Fill: 0.0,
                TopReturnProgress: 0.0,
                BottomTrackFill: 0.0,
                MastercardDropActive: 0.0,
                StripeDropActive: 0.0,
                ContactlessPulse: 0.0);
        }

        // Authorization progress bar (0.00 .. 4.45)
        var authProg = EaseOutCubic(SmoothStep(0.05, 4.45, t));
        var displayedAuthSec = authProg * authDur;

        // Settlement progress bar (4.95 .. 8.15)
        var settleProg = t < 4.95 ? 0.0 : EaseInOutQuad(SmoothStep(4.95, 8.15, t));

        // Contactless pulse during initial tap
        var contactlessPulse = t < 0.70 ? Math.Sin(SmoothStep(0.0, 0.70, t) * Math.PI) : 0.0;

        // Default values
        VelaPaymentFlowStage stage;
        var checkoutState = VelaPaymentNodeState.Idle;
        var checkoutStatus = "Ready to tap";
        var processorState = VelaPaymentNodeState.Idle;
        var processorStatus = "Processor";
        var networkState = VelaPaymentNodeState.Idle;
        var networkStatus = "Network";
        var bankState = VelaPaymentNodeState.Idle;
        var bankStatus = "Issuer";
        var balanceState = VelaPaymentNodeState.Idle;
        var displayedBalance = initialBalance;

        var chipVisible = false;
        var chipOpacity = 0.0;
        var chipScale = 1.0;
        var chipTrack = VelaPaymentChipTrack.None;
        var chipPos = 0.0;

        double topSeg0 = 0.0, topSeg1 = 0.0, topSeg2 = 0.0;
        var topReturn = 0.0;
        var bottomFill = 0.0;
        var mcDrop = 0.0;
        var stripeDrop = 0.0;

        if (t < 0.35)
        {
            // Tap & chip appear cleanly (no scale overshoot)
            var u = SmoothStep(0.0, 0.35, t);
            stage = VelaPaymentFlowStage.CheckoutTap;
            checkoutState = VelaPaymentNodeState.Active;
            checkoutStatus = "Card tapped";

            chipVisible = true;
            chipOpacity = EaseOutCubic(u);
            chipScale = 0.90 + 0.10 * EaseOutCubic(u);
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = 0.0;
        }
        else if (t < 1.15)
        {
            // Checkout -> Stripe
            var u = EaseInOutCubic(SmoothStep(0.35, 1.15, t));
            stage = u < 0.7 ? VelaPaymentFlowStage.CheckoutTap : VelaPaymentFlowStage.ProcessorRiskCheck;
            checkoutState = VelaPaymentNodeState.Completed;
            checkoutStatus = "Card tapped";
            processorState = u > 0.65 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Idle;
            processorStatus = u > 0.65 ? "Checking risk" : "Processor";

            topSeg0 = u;
            chipVisible = true;
            chipOpacity = 1.0;
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = u / 3.0;
        }
        else if (t < 1.55)
        {
            // Stripe risk check dwell (chip rests inside Stripe card)
            var u = SmoothStep(1.15, 1.55, t);
            stage = VelaPaymentFlowStage.ProcessorRiskCheck;
            checkoutState = VelaPaymentNodeState.Completed;
            checkoutStatus = "Card tapped";
            processorState = VelaPaymentNodeState.Active;
            processorStatus = u < 0.55 ? "Checking risk" : "Risk checked";

            topSeg0 = 1.0;
            chipVisible = false;
            chipOpacity = 0.0;
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = 1.0 / 3.0;
        }
        else if (t < 2.25)
        {
            // Stripe -> Mastercard
            var u = EaseInOutCubic(SmoothStep(1.55, 2.25, t));
            stage = VelaPaymentFlowStage.NetworkRouting;
            checkoutState = VelaPaymentNodeState.Completed;
            checkoutStatus = "Card tapped";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = u > 0.5 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Idle;
            networkStatus = u > 0.5 ? "Routing" : "Network";

            topSeg0 = 1.0;
            topSeg1 = u;
            chipVisible = true;
            chipOpacity = 1.0;
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = (1.0 + u) / 3.0;
        }
        else if (t < 2.95)
        {
            // Mastercard -> Bank
            var u = EaseInOutCubic(SmoothStep(2.25, 2.95, t));
            stage = u < 0.6 ? VelaPaymentFlowStage.NetworkRouting : VelaPaymentFlowStage.BankAuthorization;
            checkoutState = VelaPaymentNodeState.Completed;
            checkoutStatus = "Card tapped";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = VelaPaymentNodeState.Completed;
            networkStatus = "Routed";
            bankState = u > 0.55 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Idle;
            bankStatus = u > 0.55 ? "Authorizing" : "Issuer";

            topSeg0 = 1.0;
            topSeg1 = 1.0;
            topSeg2 = u;
            chipVisible = true;
            chipOpacity = 1.0;
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = (2.0 + u) / 3.0;
        }
        else if (t < 3.40)
        {
            // Bank verifies & approves (chip rests inside Bank card)
            var u = SmoothStep(2.95, 3.40, t);
            stage = VelaPaymentFlowStage.BankAuthorization;
            checkoutState = VelaPaymentNodeState.Completed;
            checkoutStatus = "Card tapped";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = VelaPaymentNodeState.Completed;
            networkStatus = "Routed";
            bankState = u < 0.55 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Success;
            bankStatus = u < 0.55 ? "Authorizing" : "Authorized";

            topSeg0 = 1.0;
            topSeg1 = 1.0;
            topSeg2 = 1.0;
            chipVisible = false;
            chipOpacity = 0.0;
            chipTrack = VelaPaymentChipTrack.TopForward;
            chipPos = 1.0;
        }
        else if (t < 4.45)
        {
            // Authorization returns Bank -> Mastercard -> Stripe -> Checkout
            var u = EaseInOutCubic(SmoothStep(3.40, 4.45, t));
            stage = VelaPaymentFlowStage.AuthorizationReturn;

            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Authorized";

            networkState = u is >= 0.20 and < 0.58 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            networkStatus = u >= 0.25 ? "Authorized" : "Routed";

            processorState = u is >= 0.55 and < 0.88 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";

            checkoutState = u >= 0.90 ? VelaPaymentNodeState.Success : VelaPaymentNodeState.Completed;
            checkoutStatus = u >= 0.90 ? "Approved" : "Authorizing";

            topReturn = u;
            chipVisible = true;
            chipOpacity = u > 0.92 ? 1.0 - SmoothStep(0.92, 1.0, u) : 1.0;
            chipTrack = VelaPaymentChipTrack.TopReturn;
            chipPos = 1.0 - u; // 1.0 (Bank) -> 0.0 (Checkout)
        }
        else if (t < 4.95)
        {
            // Post-authorization beat
            stage = VelaPaymentFlowStage.AuthorizationReturn;
            checkoutState = VelaPaymentNodeState.Success;
            checkoutStatus = "Approved";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = VelaPaymentNodeState.Completed;
            networkStatus = "Authorized";
            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Authorized";

            chipVisible = false;
            chipOpacity = 0.0;
        }
        else if (t < 6.25)
        {
            // Settlement leg 1: Bank bottom -> rounded corner -> Mastercard junction
            var u = EaseInOutCubic(SmoothStep(4.95, 6.25, t));
            stage = VelaPaymentFlowStage.SettlementClearing;

            checkoutState = VelaPaymentNodeState.Success;
            checkoutStatus = "Approved";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = u >= 0.55 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            networkStatus = u >= 0.55 ? "Clearing" : "Authorized";
            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Settled";

            var pos = u * mastercardBottomFraction;
            bottomFill = pos;
            mcDrop = SmoothStep(0.55, 0.95, u);

            chipVisible = true;
            chipOpacity = SmoothStep(0.0, 0.14, u);
            chipScale = 0.88 + 0.12 * SmoothStep(0.0, 0.18, u);
            chipTrack = VelaPaymentChipTrack.BottomSettlement;
            chipPos = pos;
        }
        else if (t < 7.25)
        {
            // Settlement leg 2: Mastercard junction -> Stripe junction
            var u = EaseInOutCubic(SmoothStep(6.25, 7.25, t));
            stage = VelaPaymentFlowStage.SettlementFee;

            checkoutState = VelaPaymentNodeState.Success;
            checkoutStatus = "Approved";
            processorState = u >= 0.55 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            processorStatus = u >= 0.55 ? "Settling" : "Risk checked";
            networkState = u < 0.25 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            networkStatus = u < 0.25 ? "Clearing" : "Cleared";
            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Settled";

            var pos = mastercardBottomFraction + u * (stripeBottomFraction - mastercardBottomFraction);
            bottomFill = pos;
            mcDrop = 1.0 - SmoothStep(0.0, 0.35, u);
            stripeDrop = SmoothStep(0.55, 0.95, u);

            chipVisible = true;
            chipOpacity = 1.0;
            chipTrack = VelaPaymentChipTrack.BottomSettlement;
            chipPos = pos;
        }
        else if (t < 8.15)
        {
            // Settlement leg 3: Stripe junction -> Your balance
            var u = EaseInOutCubic(SmoothStep(7.25, 8.15, t));
            stage = VelaPaymentFlowStage.Settled;

            checkoutState = VelaPaymentNodeState.Success;
            checkoutStatus = "Approved";
            processorState = u < 0.25 ? VelaPaymentNodeState.Active : VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = VelaPaymentNodeState.Completed;
            networkStatus = "Cleared";
            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Settled";

            var pos = stripeBottomFraction + u * (1.0 - stripeBottomFraction);
            bottomFill = pos;
            stripeDrop = 1.0 - SmoothStep(0.0, 0.35, u);

            var creditU = SmoothStep(0.72, 1.0, u);
            balanceState = creditU > 0.05 ? VelaPaymentNodeState.Success : VelaPaymentNodeState.Idle;
            displayedBalance = initialBalance + Math.Round(amount * (decimal)EaseOutCubic(creditU), 2, MidpointRounding.AwayFromZero);

            chipVisible = true;
            chipOpacity = u > 0.80 ? 1.0 - SmoothStep(0.80, 1.0, u) : 1.0;
            chipScale = u > 0.80 ? 1.0 - 0.18 * SmoothStep(0.80, 1.0, u) : 1.0;
            chipTrack = VelaPaymentChipTrack.BottomSettlement;
            chipPos = pos;
        }
        else
        {
            // Settled hold
            stage = VelaPaymentFlowStage.Settled;
            checkoutState = VelaPaymentNodeState.Success;
            checkoutStatus = "Approved";
            processorState = VelaPaymentNodeState.Completed;
            processorStatus = "Risk checked";
            networkState = VelaPaymentNodeState.Completed;
            networkStatus = "Cleared";
            bankState = VelaPaymentNodeState.Success;
            bankStatus = "Settled";
            balanceState = VelaPaymentNodeState.Success;
            displayedBalance = initialBalance + amount;

            bottomFill = 1.0;
            chipVisible = false;
            chipOpacity = 0.0;
        }

        return new VelaPaymentFlowSnapshot(
            Stage: stage,
            CheckoutState: checkoutState,
            CheckoutStatus: checkoutStatus,
            ProcessorState: processorState,
            ProcessorStatus: processorStatus,
            NetworkState: networkState,
            NetworkStatus: networkStatus,
            BankState: bankState,
            BankStatus: bankStatus,
            BalanceState: balanceState,
            DisplayedBalance: displayedBalance,
            AuthorizationProgress: authProg,
            DisplayedAuthSeconds: displayedAuthSec,
            SettlementProgress: settleProg,
            ChipVisible: chipVisible,
            ChipOpacity: chipOpacity,
            ChipScale: chipScale,
            ChipTrack: chipTrack,
            ChipTrackPosition: chipPos,
            TopSegment0Fill: topSeg0,
            TopSegment1Fill: topSeg1,
            TopSegment2Fill: topSeg2,
            TopReturnProgress: topReturn,
            BottomTrackFill: bottomFill,
            MastercardDropActive: mcDrop,
            StripeDropActive: stripeDrop,
            ContactlessPulse: contactlessPulse);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        if (edge1 <= edge0) return x >= edge1 ? 1.0 : 0.0;
        return Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
    }

    private static double EaseOutCubic(double x)
    {
        var inv = 1.0 - Math.Clamp(x, 0.0, 1.0);
        return 1.0 - inv * inv * inv;
    }

    private static double EaseInOutQuad(double x)
    {
        x = Math.Clamp(x, 0.0, 1.0);
        return x < 0.5 ? 2.0 * x * x : 1.0 - Math.Pow(-2.0 * x + 2.0, 2.0) * 0.5;
    }

    private static double EaseInOutCubic(double x)
    {
        x = Math.Clamp(x, 0.0, 1.0);
        return x < 0.5 ? 4.0 * x * x * x : 1.0 - Math.Pow(-2.0 * x + 2.0, 3.0) * 0.5;
    }
}

/// <summary>
/// Interactive, animated payment lifecycle visualization:
/// A card tap goes from Checkout to the processor (Stripe), network (Mastercard), and issuing bank,
/// returns with authorization, and then settles into the merchant balance while the amount travels as a chip.
/// Supports keyboard controls (Space/Enter: Play/Pause, R: Restart, Left/Right: step stages).
/// </summary>
public class VelaPaymentFlow : TemplatedControl
{
    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, decimal>(nameof(Amount), 42.50m);

    public static readonly StyledProperty<decimal> InitialBalanceProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, decimal>(nameof(InitialBalance), 1284.10m);

    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(CurrencySymbol), "$");

    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, bool>(nameof(AutoPlay), true);

    public static readonly StyledProperty<bool> IsLoopingProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, bool>(nameof(IsLooping), true);

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, double>(
            nameof(PlaybackSpeed),
            1.0,
            coerce: static (_, v) => Math.Clamp(double.IsNaN(v) ? 1.0 : v, 0.25, 4.0));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, bool>(
            nameof(IsPlaying),
            false,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> AuthorizationDurationSecondsProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, double>(nameof(AuthorizationDurationSeconds), 1.34);

    public static readonly StyledProperty<string> SettlementEtaLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(SettlementEtaLabel), "Next business day");

    public static readonly StyledProperty<string> CheckoutLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(CheckoutLabel), "Checkout");

    public static readonly StyledProperty<string> ProcessorLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(ProcessorLabel), "Stripe");

    public static readonly StyledProperty<string> NetworkLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(NetworkLabel), "Mastercard");

    public static readonly StyledProperty<string> BankLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(BankLabel), "Bank");

    public static readonly StyledProperty<string> BalanceLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(BalanceLabel), "Your balance");

    public static readonly StyledProperty<string> AuthorizationLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(AuthorizationLabel), "Authorization");

    public static readonly StyledProperty<string> SettlementLabelProperty =
        AvaloniaProperty.Register<VelaPaymentFlow, string>(nameof(SettlementLabel), "Settlement");

    public static readonly DirectProperty<VelaPaymentFlow, VelaPaymentFlowStage> CurrentStageProperty =
        AvaloniaProperty.RegisterDirect<VelaPaymentFlow, VelaPaymentFlowStage>(
            nameof(CurrentStage),
            o => o._currentStage);

    public static readonly DirectProperty<VelaPaymentFlow, double> NormalizedProgressProperty =
        AvaloniaProperty.RegisterDirect<VelaPaymentFlow, double>(
            nameof(NormalizedProgress),
            o => o._elapsed / VelaPaymentFlowTimeline.TotalDuration);

    public decimal Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }
    public decimal InitialBalance { get => GetValue(InitialBalanceProperty); set => SetValue(InitialBalanceProperty, value); }
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }
    public bool IsLooping { get => GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }
    public double PlaybackSpeed { get => GetValue(PlaybackSpeedProperty); set => SetValue(PlaybackSpeedProperty, value); }
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public double AuthorizationDurationSeconds { get => GetValue(AuthorizationDurationSecondsProperty); set => SetValue(AuthorizationDurationSecondsProperty, value); }
    public string SettlementEtaLabel { get => GetValue(SettlementEtaLabelProperty); set => SetValue(SettlementEtaLabelProperty, value); }
    public string CheckoutLabel { get => GetValue(CheckoutLabelProperty); set => SetValue(CheckoutLabelProperty, value); }
    public string ProcessorLabel { get => GetValue(ProcessorLabelProperty); set => SetValue(ProcessorLabelProperty, value); }
    public string NetworkLabel { get => GetValue(NetworkLabelProperty); set => SetValue(NetworkLabelProperty, value); }
    public string BankLabel { get => GetValue(BankLabelProperty); set => SetValue(BankLabelProperty, value); }
    public string BalanceLabel { get => GetValue(BalanceLabelProperty); set => SetValue(BalanceLabelProperty, value); }
    public string AuthorizationLabel { get => GetValue(AuthorizationLabelProperty); set => SetValue(AuthorizationLabelProperty, value); }
    public string SettlementLabel { get => GetValue(SettlementLabelProperty); set => SetValue(SettlementLabelProperty, value); }

    public VelaPaymentFlowStage CurrentStage => _currentStage;
    public double NormalizedProgress => _elapsed / VelaPaymentFlowTimeline.TotalDuration;

    public event EventHandler<VelaPaymentFlowStage>? StageChanged;
    public event EventHandler? FlowCompleted;

    protected override Type StyleKeyOverride => typeof(VelaPaymentFlow);

    private readonly SpringRunner _runner;
    private VelaPaymentFlowCanvas? _canvas;
    private double _elapsed;
    private VelaPaymentFlowStage _currentStage = VelaPaymentFlowStage.Idle;
    private bool _completedNotified;
    private bool _syncingIsPlaying;

    static VelaPaymentFlow()
    {
        FocusableProperty.OverrideDefaultValue<VelaPaymentFlow>(true);

        IsPlayingProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, e) =>
        {
            if (s._syncingIsPlaying)
                return;

            if (e.NewValue is true)
                s.StartRunner();
            else
                s._runner.Stop();
        });

        IsVisibleProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, e) =>
        {
            if (e.NewValue is true)
            {
                if (s.IsPlaying && s.IsEffectivelyVisible)
                    s.StartRunner();
            }
            else
            {
                s._runner.Stop();
            }
        });

        AmountProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        InitialBalanceProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        CurrencySymbolProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        AuthorizationDurationSecondsProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        SettlementEtaLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        CheckoutLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        ProcessorLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        NetworkLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        BankLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        BalanceLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        AuthorizationLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
        SettlementLabelProperty.Changed.AddClassHandler<VelaPaymentFlow>((s, _) => s.InvalidateCanvas());
    }

    public VelaPaymentFlow()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    public void Play()
    {
        if (_elapsed >= VelaPaymentFlowTimeline.TotalDuration - 0.01)
        {
            _elapsed = 0.0;
            _completedNotified = false;
        }

        SetIsPlayingInternal(true);
        StartRunner();
        InvalidateCanvas();
    }

    public void Pause()
    {
        SetIsPlayingInternal(false);
        _runner.Stop();
    }

    public void TogglePlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    public void Restart()
    {
        _elapsed = 0.0;
        _completedNotified = false;
        UpdateSnapshotState();
        SetIsPlayingInternal(true);
        StartRunner();
        InvalidateCanvas();
    }

    public void Reset()
    {
        SetIsPlayingInternal(false);
        _runner.Stop();
        _elapsed = 0.0;
        _completedNotified = false;
        UpdateSnapshotState();
        InvalidateCanvas();
    }

    /// <summary>
    /// Seeks to a normalized timeline position in [0, 1].
    /// </summary>
    public void SeekNormalized(double progress)
    {
        var clamped = Math.Clamp(double.IsNaN(progress) ? 0.0 : progress, 0.0, 1.0);
        var prev = NormalizedProgress;
        _elapsed = clamped * VelaPaymentFlowTimeline.TotalDuration;
        _completedNotified = _elapsed >= VelaPaymentFlowTimeline.ActiveDuration;
        UpdateSnapshotState();
        RaisePropertyChanged(NormalizedProgressProperty, prev, NormalizedProgress);
        InvalidateCanvas();
    }

    /// <summary>
    /// Steps forward or backward to the next/previous canonical stage anchor.
    /// </summary>
    public void StepStage(int delta)
    {
        var anchors = VelaPaymentFlowTimeline.StageAnchors;
        var currentIdx = 0;
        for (var i = 0; i < anchors.Length; i++)
        {
            if (_elapsed >= anchors[i] - 0.05)
                currentIdx = i;
        }

        var nextIdx = Math.Clamp(currentIdx + delta, 0, anchors.Length - 1);
        Pause();
        SeekNormalized(anchors[nextIdx] / VelaPaymentFlowTimeline.TotalDuration);
    }

    internal double ElapsedSeconds => _elapsed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _canvas = e.NameScope.Find<VelaPaymentFlowCanvas>("PART_Canvas");
        if (_canvas is not null)
        {
            _canvas.Owner = this;
            _canvas.InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnThemeOrAccentChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;

        if (AutoPlay || IsPlaying)
        {
            if (VelaMotionOptions.ReducedMotion && _elapsed <= 0.0001)
            {
                // In reduced motion, land directly on the iconic clearing frame unless user manually steps
                SeekNormalized(6.10 / VelaPaymentFlowTimeline.TotalDuration);
            }
            else
            {
                Play();
            }
        }
        else
        {
            InvalidateCanvas();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaTheme.AccentChanged -= OnThemeOrAccentChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _runner.Stop();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            Focus();
            TogglePlayPause();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                TogglePlayPause();
                e.Handled = true;
                break;
            case Key.R:
                Restart();
                e.Handled = true;
                break;
            case Key.Right:
                StepStage(1);
                e.Handled = true;
                break;
            case Key.Left:
                StepStage(-1);
                e.Handled = true;
                break;
        }
    }

    private void StartRunner()
    {
        if (!IsVisible || TopLevel.GetTopLevel(this) is null)
            return;
        _runner.Wake();
    }

    private void SetIsPlayingInternal(bool value)
    {
        if (IsPlaying == value)
            return;
        _syncingIsPlaying = true;
        try
        {
            SetCurrentValue(IsPlayingProperty, value);
        }
        finally
        {
            _syncingIsPlaying = false;
        }
    }

    private bool OnTick(double dt)
    {
        if (!IsPlaying || !IsVisible)
            return false;

        if (VelaMotionOptions.ReducedMotion)
        {
            // Advance cleanly without rapid frame churn when reduced motion is enabled
            SetIsPlayingInternal(false);
            SeekNormalized(6.10 / VelaPaymentFlowTimeline.TotalDuration);
            return false;
        }

        var prevProgress = NormalizedProgress;
        _elapsed += dt * PlaybackSpeed;

        if (_elapsed >= VelaPaymentFlowTimeline.ActiveDuration && !_completedNotified)
        {
            _completedNotified = true;
            FlowCompleted?.Invoke(this, EventArgs.Empty);
        }

        if (_elapsed >= VelaPaymentFlowTimeline.TotalDuration)
        {
            if (IsLooping)
            {
                _elapsed %= VelaPaymentFlowTimeline.TotalDuration;
                _completedNotified = false;
            }
            else
            {
                _elapsed = VelaPaymentFlowTimeline.TotalDuration;
                UpdateSnapshotState();
                RaisePropertyChanged(NormalizedProgressProperty, prevProgress, NormalizedProgress);
                SetIsPlayingInternal(false);
                InvalidateCanvas();
                return false;
            }
        }

        UpdateSnapshotState();
        RaisePropertyChanged(NormalizedProgressProperty, prevProgress, NormalizedProgress);
        InvalidateCanvas();
        return true;
    }

    private void UpdateSnapshotState()
    {
        var snapshot = VelaPaymentFlowTimeline.Evaluate(
            _elapsed,
            Amount,
            InitialBalance,
            AuthorizationDurationSeconds);

        if (snapshot.Stage != _currentStage)
        {
            var oldStage = _currentStage;
            _currentStage = snapshot.Stage;
            RaisePropertyChanged(CurrentStageProperty, oldStage, _currentStage);
            StageChanged?.Invoke(this, _currentStage);
        }
    }

    private void InvalidateCanvas()
    {
        _canvas?.InvalidateVisual();
    }

    private void OnThemeOrAccentChanged(object? sender, Tokens.VelaAccent accent) => InvalidateCanvas();
    private void OnThemeVariantChanged(object? sender, EventArgs e) => InvalidateCanvas();
}

/// <summary>
/// High-DPI vector canvas renderer for <see cref="VelaPaymentFlow"/>.
/// Draws the 5 payment cards, port markers, solid & dashed routing tracks, traveling amount chip,
/// and dual-phase Authorization / Settlement progress bars.
/// </summary>
public sealed class VelaPaymentFlowCanvas : Control
{
    internal VelaPaymentFlow? Owner { get; set; }

    // Cached static icon geometries (normalized in 24x24 viewbox)
    private static readonly Geometry BankIconGeometry = Geometry.Parse(
        "M3,10 L12,4 L21,10 M5,10 L5,18 M10,10 L10,18 M14,10 L14,18 M19,10 L19,18 M3,18 L21,18 M2,21 L22,21");

    private static readonly Geometry StoreIconGeometry = Geometry.Parse(
        "M3,9 L5,4 L19,4 L21,9 M4,9 C4,10.8 5.4,12 7,12 C8.6,12 10,10.8 10,9 C10,10.8 11.4,12 13,12 C14.6,12 16,10.8 16,9 C16,10.8 17.4,12 19,12 C20.1,12 21,11.1 21,9 M5,12 L5,20 L19,20 L19,12 M10,20 L10,15 L14,15 L14,20");

    public VelaPaymentFlowCanvas()
    {
        ClipToBounds = true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var w = double.IsInfinity(availableSize.Width) ? 760.0 : Math.Max(320.0, availableSize.Width);
        return new Size(w, 296.0);
    }

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (owner is null || w < 120 || h < 120)
            return;

        // Resolve theme colors
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var surfaceColor = ResolveColor("VelaSurfaceColor", isDark ? Color.Parse("#FF191919") : Colors.White);
        var surfaceRaisedColor = ResolveColor("VelaSurfaceRaisedColor", isDark ? Color.Parse("#FF1F1F1F") : Colors.White);
        var surfaceMutedColor = ResolveColor("VelaSurfaceMutedColor", isDark ? Color.Parse("#FF262626") : Color.Parse("#FFECECEC"));
        var fgColor = ResolveColor("VelaForegroundColor", isDark ? Color.Parse("#FFF2F2F2") : Color.Parse("#FF111111"));
        var textSecondaryColor = ResolveColor("VelaTextSecondaryColor", isDark ? Color.Parse("#FFB4B4B4") : Color.Parse("#FF585858"));
        var textMutedColor = ResolveColor("VelaTextMutedColor", isDark ? Color.Parse("#FF8C8C8C") : Color.Parse("#FF7D7D7D"));
        var borderColor = ResolveColor("VelaBorderColor", isDark ? Color.Parse("#FF2B2B2B") : Color.Parse("#FFE4E4E4"));
        var borderStrongColor = ResolveColor("VelaBorderStrongColor", isDark ? Color.Parse("#FF4A4A4A") : Color.Parse("#FFB8B8B8"));
        var trackColor = ResolveColor("VelaControlTrackColor", isDark ? Color.Parse("#FF333333") : Color.Parse("#FFDCDCDC"));
        var successColor = ResolveColor("VelaSuccessColor", isDark ? Color.Parse("#FF55D69F") : Color.Parse("#FF00774D"));

        var surfaceBrush = new SolidColorBrush(surfaceColor);
        var surfaceRaisedBrush = new SolidColorBrush(surfaceRaisedColor);
        var surfaceMutedBrush = new SolidColorBrush(surfaceMutedColor);
        var fgBrush = new SolidColorBrush(fgColor);
        var textSecondaryBrush = new SolidColorBrush(textSecondaryColor);
        var textMutedBrush = new SolidColorBrush(textMutedColor);
        var borderBrush = new SolidColorBrush(borderColor);
        var trackBrush = new SolidColorBrush(trackColor);
        var successBrush = new SolidColorBrush(successColor);
        var successBorderBrush = new SolidColorBrush(Color.FromArgb(isDark ? (byte)130 : (byte)115, successColor.R, successColor.G, successColor.B));

        // Compute responsive layout
        var padX = Math.Clamp(w * 0.032, 12.0, 28.0);
        var innerW = w - padX * 2.0;
        var gap = Math.Clamp(innerW * 0.088, 16.0, 66.0);
        var cardW = Math.Max(72.0, (innerW - 3.0 * gap) / 4.0);
        var cardH = 62.0;
        var cardRadius = 15.0;

        var topY = Math.Max(16.0, (h - 244.0) * 0.42);
        var bottomY = topY + cardH + 54.0;
        var topCenterY = topY + cardH * 0.5;
        var bottomCenterY = bottomY + cardH * 0.5;

        var checkoutRect = new Rect(padX, topY, cardW, cardH);
        var stripeRect = new Rect(padX + (cardW + gap), topY, cardW, cardH);
        var mastercardRect = new Rect(padX + 2.0 * (cardW + gap), topY, cardW, cardH);
        var bankRect = new Rect(padX + 3.0 * (cardW + gap), topY, cardW, cardH);
        var balanceRect = new Rect(padX, bottomY, cardW, cardH);

        // Compute exact bottom settlement path metrics so Mastercard & Stripe junctions align pixel-perfectly
        var cornerR = Math.Min(16.0, (bottomCenterY - bankRect.Bottom) * 0.45);
        var bankBottomX = bankRect.Center.X;
        var bankBottomY = bankRect.Bottom;
        var balanceRightX = balanceRect.Right;

        var seg1Len = Math.Max(1.0, (bottomCenterY - cornerR) - bankBottomY);
        var arcLen = Math.PI * 0.5 * cornerR;
        var seg3Len = Math.Max(1.0, (bankBottomX - cornerR) - balanceRightX);
        var bottomTotalLen = seg1Len + arcLen + seg3Len;

        var mcDistFromCornerEnd = Math.Max(0.0, (bankBottomX - cornerR) - mastercardRect.Center.X);
        var stripeDistFromCornerEnd = Math.Max(0.0, (bankBottomX - cornerR) - stripeRect.Center.X);

        var mcFraction = Math.Clamp((seg1Len + arcLen + mcDistFromCornerEnd) / bottomTotalLen, 0.20, 0.65);
        var stripeFraction = Math.Clamp((seg1Len + arcLen + stripeDistFromCornerEnd) / bottomTotalLen, mcFraction + 0.12, 0.90);

        var snapshot = VelaPaymentFlowTimeline.Evaluate(
            owner.ElapsedSeconds,
            owner.Amount,
            owner.InitialBalance,
            owner.AuthorizationDurationSeconds,
            mcFraction,
            stripeFraction);

        var fontFamily = owner.FontFamily ?? FontFamily.Default;

        // 1. Draw base tracks (top horizontal, bottom L-curve, vertical dashed clearing lines)
        var idleTrackPen = new Pen(new SolidColorBrush(Color.FromArgb(isDark ? (byte)160 : (byte)215, trackColor.R, trackColor.G, trackColor.B)), 1.25, lineCap: PenLineCap.Round);
        var activeTrackPen = new Pen(new SolidColorBrush(Color.FromArgb(220, textSecondaryColor.R, textSecondaryColor.G, textSecondaryColor.B)), 1.65, lineCap: PenLineCap.Round);
        var returnTrackPen = new Pen(new SolidColorBrush(Color.FromArgb(200, successColor.R, successColor.G, successColor.B)), 1.65, lineCap: PenLineCap.Round);

        // Top segments
        DrawHorizontalSegment(context, checkoutRect.Right, stripeRect.Left, topCenterY, idleTrackPen, activeTrackPen, snapshot.TopSegment0Fill);
        DrawHorizontalSegment(context, stripeRect.Right, mastercardRect.Left, topCenterY, idleTrackPen, activeTrackPen, snapshot.TopSegment1Fill);
        DrawHorizontalSegment(context, mastercardRect.Right, bankRect.Left, topCenterY, idleTrackPen, activeTrackPen, snapshot.TopSegment2Fill);

        // Return authorization highlight traveling right-to-left across top segments
        if (snapshot.TopReturnProgress > 0.001 && snapshot.TopReturnProgress < 0.999)
        {
            var r2 = Math.Clamp(snapshot.TopReturnProgress * 3.0, 0.0, 1.0);
            var r1 = Math.Clamp(snapshot.TopReturnProgress * 3.0 - 1.0, 0.0, 1.0);
            var r0 = Math.Clamp(snapshot.TopReturnProgress * 3.0 - 2.0, 0.0, 1.0);
            DrawHorizontalSegmentReverse(context, mastercardRect.Right, bankRect.Left, topCenterY, returnTrackPen, r2);
            DrawHorizontalSegmentReverse(context, stripeRect.Right, mastercardRect.Left, topCenterY, returnTrackPen, r1);
            DrawHorizontalSegmentReverse(context, checkoutRect.Right, stripeRect.Left, topCenterY, returnTrackPen, r0);
        }

        // Vertical dashed lines from Stripe & Mastercard down to bottom track
        DrawVerticalDashedDrop(context, stripeRect.Center.X, stripeRect.Bottom, bottomCenterY, trackColor, fgColor, snapshot.StripeDropActive);
        DrawVerticalDashedDrop(context, mastercardRect.Center.X, mastercardRect.Bottom, bottomCenterY, trackColor, fgColor, snapshot.MastercardDropActive);

        // Bottom settlement L-curve track (full base + partial active progress)
        DrawBottomTrack(context, bankBottomX, bankBottomY, balanceRightX, bottomCenterY, cornerR, idleTrackPen, 1.0);
        if (snapshot.BottomTrackFill > 0.001)
        {
            DrawBottomTrack(context, bankBottomX, bankBottomY, balanceRightX, bottomCenterY, cornerR, activeTrackPen, snapshot.BottomTrackFill);
        }

        // Bottom track junction dots (under Stripe & Mastercard)
        DrawPortDot(context, new Point(stripeRect.Center.X, bottomCenterY), surfaceBrush,
            snapshot.StripeDropActive > 0.3 || snapshot.BottomTrackFill >= stripeFraction ? textSecondaryBrush : trackBrush, 3.0);
        DrawPortDot(context, new Point(mastercardRect.Center.X, bottomCenterY), surfaceBrush,
            snapshot.MastercardDropActive > 0.3 || snapshot.BottomTrackFill >= mcFraction ? textSecondaryBrush : trackBrush, 3.0);

        // 2. Draw the traveling amount chip (above tracks & junction dots, behind node cards so it emerges/tucks cleanly)
        if (snapshot.ChipVisible && snapshot.ChipOpacity > 0.01)
        {
            var chipCenter = ComputeChipPosition(
                snapshot.ChipTrack,
                snapshot.ChipTrackPosition,
                checkoutRect,
                stripeRect,
                mastercardRect,
                bankRect,
                balanceRect,
                topCenterY,
                bottomCenterY,
                cornerR);

            var chipLabel = FormatCurrency(owner.CurrencySymbol, owner.Amount);
            var isReturnAuth = snapshot.ChipTrack == VelaPaymentChipTrack.TopReturn;
            DrawAmountChip(
                context,
                chipCenter,
                chipLabel,
                snapshot.ChipOpacity,
                snapshot.ChipScale,
                isReturnAuth,
                isDark,
                fontFamily,
                surfaceMutedBrush,
                new SolidColorBrush(borderStrongColor),
                fgBrush,
                successColor);
        }

        // 3. Draw the 5 node cards
        var formattedBalance = FormatCurrency(owner.CurrencySymbol, snapshot.DisplayedBalance);

        DrawNodeCard(context, checkoutRect, cardRadius, NodeIconKind.Checkout, owner.CheckoutLabel, snapshot.CheckoutStatus,
            snapshot.CheckoutState, snapshot.ContactlessPulse, isDark, fontFamily,
            surfaceBrush, surfaceRaisedBrush, borderBrush, fgBrush, textSecondaryBrush, textMutedBrush, successBrush, successBorderBrush);

        DrawNodeCard(context, stripeRect, cardRadius, NodeIconKind.Stripe, owner.ProcessorLabel, snapshot.ProcessorStatus,
            snapshot.ProcessorState, 0.0, isDark, fontFamily,
            surfaceBrush, surfaceRaisedBrush, borderBrush, fgBrush, textSecondaryBrush, textMutedBrush, successBrush, successBorderBrush);

        DrawNodeCard(context, mastercardRect, cardRadius, NodeIconKind.Mastercard, owner.NetworkLabel, snapshot.NetworkStatus,
            snapshot.NetworkState, 0.0, isDark, fontFamily,
            surfaceBrush, surfaceRaisedBrush, borderBrush, fgBrush, textSecondaryBrush, textMutedBrush, successBrush, successBorderBrush);

        DrawNodeCard(context, bankRect, cardRadius, NodeIconKind.Bank, owner.BankLabel, snapshot.BankStatus,
            snapshot.BankState, 0.0, isDark, fontFamily,
            surfaceBrush, surfaceRaisedBrush, borderBrush, fgBrush, textSecondaryBrush, textMutedBrush, successBrush, successBorderBrush);

        DrawNodeCard(context, balanceRect, cardRadius, NodeIconKind.Balance, owner.BalanceLabel, formattedBalance,
            snapshot.BalanceState, 0.0, isDark, fontFamily,
            surfaceBrush, surfaceRaisedBrush, borderBrush, fgBrush, textSecondaryBrush, textMutedBrush, successBrush, successBorderBrush);

        // 4. Draw port dots on card edges (drawn after cards so they sit crisply on borders)
        DrawPortDot(context, new Point(checkoutRect.Right, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(stripeRect.Left, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(stripeRect.Right, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(stripeRect.Center.X, stripeRect.Bottom), surfaceBrush,
            snapshot.StripeDropActive > 0.3 ? fgBrush : trackBrush, 2.5);
        DrawPortDot(context, new Point(mastercardRect.Left, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(mastercardRect.Right, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(mastercardRect.Center.X, mastercardRect.Bottom), surfaceBrush,
            snapshot.MastercardDropActive > 0.3 ? fgBrush : trackBrush, 2.5);
        DrawPortDot(context, new Point(bankRect.Left, topCenterY), surfaceBrush, trackBrush, 2.5);
        DrawPortDot(context, new Point(bankRect.Center.X, bankRect.Bottom), surfaceBrush,
            snapshot.BottomTrackFill > 0.01 ? textSecondaryBrush : trackBrush, 2.5);
        DrawPortDot(context, new Point(balanceRect.Right, bottomCenterY), surfaceBrush,
            snapshot.BottomTrackFill > 0.96 ? successBrush : trackBrush, 2.5);

        // 5. Draw bottom 2-column progress bars (Authorization & Settlement)
        var footerY = bottomY + cardH + 34.0;
        var footerGap = Math.Clamp(innerW * 0.028, 12.0, 24.0);
        var halfW = (innerW - footerGap) * 0.5;

        var authTimeText = snapshot.DisplayedAuthSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        DrawPhaseBar(
            context,
            new Rect(padX, footerY, halfW, 30.0),
            owner.AuthorizationLabel,
            authTimeText,
            snapshot.AuthorizationProgress,
            fontFamily,
            fgBrush,
            textMutedBrush,
            trackBrush,
            fgBrush);

        DrawPhaseBar(
            context,
            new Rect(padX + halfW + footerGap, footerY, halfW, 30.0),
            owner.SettlementLabel,
            owner.SettlementEtaLabel,
            snapshot.SettlementProgress,
            fontFamily,
            fgBrush,
            textMutedBrush,
            trackBrush,
            fgBrush);
    }

    private enum NodeIconKind
    {
        Checkout,
        Stripe,
        Mastercard,
        Bank,
        Balance,
    }

    private static void DrawNodeCard(
        DrawingContext context,
        Rect rect,
        double radius,
        NodeIconKind iconKind,
        string title,
        string status,
        VelaPaymentNodeState state,
        double pulse,
        bool isDark,
        FontFamily fontFamily,
        IBrush surfaceBrush,
        IBrush surfaceRaisedBrush,
        IBrush borderBrush,
        IBrush fgBrush,
        IBrush textSecondaryBrush,
        IBrush textMutedBrush,
        IBrush successBrush,
        IBrush successBorderBrush)
    {
        // Soft elevation shadow
        var shadowAlpha = state == VelaPaymentNodeState.Active ? (isDark ? (byte)64 : (byte)22) : (isDark ? (byte)36 : (byte)11);
        var shadowBrush = new SolidColorBrush(Color.FromArgb(shadowAlpha, 0, 0, 0));
        var shadowRect = new Rect(rect.X, rect.Y + (state == VelaPaymentNodeState.Active ? 2.5 : 1.5), rect.Width, rect.Height);
        context.DrawRectangle(shadowBrush, null, new RoundedRect(shadowRect, radius));

        // Card fill & border
        var fill = state == VelaPaymentNodeState.Active ? surfaceRaisedBrush : surfaceBrush;
        var strokeBrush = state switch
        {
            VelaPaymentNodeState.Active => fgBrush,
            VelaPaymentNodeState.Success => successBorderBrush,
            _ => borderBrush,
        };
        var strokeThickness = state == VelaPaymentNodeState.Active ? 1.25 : 1.0;

        context.DrawRectangle(fill, new Pen(strokeBrush, strokeThickness), new RoundedRect(rect, radius));

        // Icon box on the left
        var compact = rect.Width < 126;
        var iconAreaLeft = rect.X + (compact ? 10.0 : 14.0);
        var iconCenter = new Point(iconAreaLeft + 10.0, rect.Center.Y);
        DrawNodeIcon(context, iconKind, iconCenter, state, pulse, isDark, textSecondaryBrush, fgBrush, successBrush);

        // Text stack
        var textX = iconAreaLeft + (compact ? 25.0 : 30.0);
        var maxTextW = Math.Max(24.0, rect.Right - textX - 10.0);

        var titleFt = new FormattedText(
            title,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyle.Normal, FontWeight.SemiBold),
            compact ? 11.5 : 12.8,
            fgBrush)
        {
            MaxTextWidth = maxTextW,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        var statusBrush = state switch
        {
            VelaPaymentNodeState.Success => successBrush,
            VelaPaymentNodeState.Active => textSecondaryBrush,
            _ => textMutedBrush,
        };

        var dotBrush = state switch
        {
            VelaPaymentNodeState.Success => successBrush,
            VelaPaymentNodeState.Active => fgBrush,
            VelaPaymentNodeState.Completed => textSecondaryBrush,
            _ => new SolidColorBrush(Color.FromArgb(140, 140, 140, 140)),
        };

        var statusFt = new FormattedText(
            status,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal),
            compact ? 10.5 : 11.5,
            statusBrush)
        {
            MaxTextWidth = Math.Max(16.0, maxTextW - 11.0),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

        var totalTextH = titleFt.Height + 3.0 + statusFt.Height;
        var textTop = rect.Y + (rect.Height - totalTextH) * 0.5;

        context.DrawText(titleFt, new Point(textX, textTop));

        var statusTop = textTop + titleFt.Height + 3.0;
        var dotCenter = new Point(textX + 2.8, statusTop + statusFt.Height * 0.52);
        context.DrawEllipse(dotBrush, null, dotCenter, 2.5, 2.5);
        context.DrawText(statusFt, new Point(textX + 10.0, statusTop));
    }

    private static void DrawNodeIcon(
        DrawingContext context,
        NodeIconKind kind,
        Point center,
        VelaPaymentNodeState state,
        double pulse,
        bool isDark,
        IBrush secondaryBrush,
        IBrush fgBrush,
        IBrush successBrush)
    {
        switch (kind)
        {
            case NodeIconKind.Checkout:
            {
                // Contactless NFC arcs
                var arcBrush = state == VelaPaymentNodeState.Active ? fgBrush : secondaryBrush;
                var pen = new Pen(arcBrush, 1.5, lineCap: PenLineCap.Round);
                for (var i = 0; i < 3; i++)
                {
                    var r = 3.2 + i * 3.1 + pulse * (i * 0.6);
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        var startAngle = -Math.PI * 0.30;
                        var endAngle = Math.PI * 0.30;
                        var originX = center.X - 4.5;
                        var p0 = new Point(originX + r * Math.Cos(startAngle), center.Y + r * Math.Sin(startAngle));
                        var p1 = new Point(originX + r * Math.Cos(endAngle), center.Y + r * Math.Sin(endAngle));
                        g.BeginFigure(p0, isFilled: false);
                        g.ArcTo(p1, new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise);
                    }
                    context.DrawGeometry(null, pen, geo);
                }
                break;
            }

            case NodeIconKind.Stripe:
            {
                var stripeBrush = new SolidColorBrush(Color.Parse(isDark ? "#FF7A73FF" : "#FF635BFF"));
                var ft = new FormattedText(
                    "S",
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Black),
                    17.5,
                    stripeBrush);
                context.DrawText(ft, new Point(center.X - ft.Width * 0.5, center.Y - ft.Height * 0.5));
                break;
            }

            case NodeIconKind.Mastercard:
            {
                const double r = 5.4;
                const double offset = 3.1;
                var leftCenter = new Point(center.X - offset, center.Y);
                var rightCenter = new Point(center.X + offset, center.Y);
                var red = new SolidColorBrush(Color.Parse("#FFEB001B"));
                var amber = new SolidColorBrush(Color.Parse("#FFF79E1B"));
                var overlap = new SolidColorBrush(Color.Parse("#FFFF5F00"));

                context.DrawEllipse(red, null, leftCenter, r, r);
                context.DrawEllipse(amber, null, rightCenter, r, r);

                // Crisp orange overlap lens clipped to left circle
                var leftClip = new EllipseGeometry(new Rect(leftCenter.X - r, leftCenter.Y - r, r * 2, r * 2));
                using (context.PushGeometryClip(leftClip))
                {
                    context.DrawEllipse(overlap, null, rightCenter, r, r);
                }
                break;
            }

            case NodeIconKind.Bank:
            {
                var iconBrush = state == VelaPaymentNodeState.Active ? fgBrush : secondaryBrush;
                DrawScaledVectorIcon(context, BankIconGeometry, center, 16.0, iconBrush, 1.5);
                break;
            }

            case NodeIconKind.Balance:
            {
                var iconBrush = state == VelaPaymentNodeState.Success ? successBrush : secondaryBrush;
                DrawScaledVectorIcon(context, StoreIconGeometry, center, 16.5, iconBrush, 1.45);
                break;
            }
        }
    }

    private static void DrawScaledVectorIcon(DrawingContext context, Geometry geometry, Point center, double size, IBrush brush, double strokeWidth)
    {
        var scale = size / 24.0;
        var transform = Matrix.CreateTranslation(-12.0, -12.0)
                        * Matrix.CreateScale(scale, scale)
                        * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform))
        {
            var pen = new Pen(brush, strokeWidth / scale, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, pen, geometry);
        }
    }

    private static void DrawHorizontalSegment(
        DrawingContext context,
        double x0,
        double x1,
        double y,
        Pen basePen,
        Pen activePen,
        double fill)
    {
        context.DrawLine(basePen, new Point(x0, y), new Point(x1, y));
        if (fill > 0.001)
        {
            var fx = x0 + (x1 - x0) * Math.Clamp(fill, 0.0, 1.0);
            context.DrawLine(activePen, new Point(x0, y), new Point(fx, y));
        }
    }

    private static void DrawHorizontalSegmentReverse(
        DrawingContext context,
        double x0,
        double x1,
        double y,
        Pen activePen,
        double fill)
    {
        if (fill <= 0.001) return;
        var fx = x1 - (x1 - x0) * Math.Clamp(fill, 0.0, 1.0);
        context.DrawLine(activePen, new Point(x1, y), new Point(fx, y));
    }

    private static void DrawVerticalDashedDrop(
        DrawingContext context,
        double x,
        double yTop,
        double yBottom,
        Color idleColor,
        Color activeColor,
        double activeIntensity)
    {
        var blend = Math.Clamp(activeIntensity, 0.0, 1.0);
        var c = LerpColor(idleColor, activeColor, blend * 0.78);
        var thickness = 1.15 + blend * 0.35;
        var pen = new Pen(
            new SolidColorBrush(c),
            thickness,
            dashStyle: new DashStyle([3.0, 3.5], 0.0),
            lineCap: PenLineCap.Round);
        context.DrawLine(pen, new Point(x, yTop + 2.0), new Point(x, yBottom - 2.0));
    }

    private static void DrawBottomTrack(
        DrawingContext context,
        double bankX,
        double bankBottomY,
        double balanceRightX,
        double bottomY,
        double r,
        Pen pen,
        double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        if (fraction <= 0.0001) return;

        var seg1Len = Math.Max(0.0, (bottomY - r) - bankBottomY);
        var arcLen = Math.PI * 0.5 * r;
        var seg3Len = Math.Max(0.0, (bankX - r) - balanceRightX);
        var totalLen = seg1Len + arcLen + seg3Len;
        if (totalLen <= 0.01) return;

        var targetDist = fraction * totalLen;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(bankX, bankBottomY), isFilled: false);

            if (targetDist <= seg1Len)
            {
                g.LineTo(new Point(bankX, bankBottomY + targetDist));
            }
            else
            {
                var cornerStartY = bottomY - r;
                g.LineTo(new Point(bankX, cornerStartY));

                var rem = targetDist - seg1Len;
                if (rem <= arcLen)
                {
                    var angle = (rem / arcLen) * (Math.PI * 0.5); // 0 -> pi/2
                    var cx = bankX - r;
                    var cy = bottomY - r;
                    var px = cx + r * Math.Cos(angle);
                    var py = cy + r * Math.Sin(angle);
                    g.ArcTo(new Point(px, py), new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise);
                }
                else
                {
                    g.ArcTo(new Point(bankX - r, bottomY), new Size(r, r), 0, isLargeArc: false, SweepDirection.Clockwise);
                    var rem3 = Math.Min(seg3Len, rem - arcLen);
                    g.LineTo(new Point(bankX - r - rem3, bottomY));
                }
            }
        }

        context.DrawGeometry(null, pen, geo);
    }

    private static Point ComputeChipPosition(
        VelaPaymentChipTrack track,
        double trackPos,
        Rect checkoutRect,
        Rect stripeRect,
        Rect mastercardRect,
        Rect bankRect,
        Rect balanceRect,
        double topY,
        double bottomY,
        double cornerR)
    {
        var p = Math.Clamp(trackPos, 0.0, 1.0);

        if (track is VelaPaymentChipTrack.TopForward or VelaPaymentChipTrack.TopReturn)
        {
            // Glide continuously center-to-center so the chip smoothly emerges from behind each card and tucks into the next
            var seg = p * 3.0;
            if (seg <= 1.0)
                return new Point(Lerp(checkoutRect.Center.X, stripeRect.Center.X, seg), topY);
            if (seg <= 2.0)
                return new Point(Lerp(stripeRect.Center.X, mastercardRect.Center.X, seg - 1.0), topY);
            return new Point(Lerp(mastercardRect.Center.X, bankRect.Center.X, seg - 2.0), topY);
        }

        if (track == VelaPaymentChipTrack.BottomSettlement)
        {
            var bankX = bankRect.Center.X;
            var bankBottomY = bankRect.Bottom;
            var balanceRightX = balanceRect.Right;

            var seg1Len = Math.Max(0.0, (bottomY - cornerR) - bankBottomY);
            var arcLen = Math.PI * 0.5 * cornerR;
            var seg3Len = Math.Max(0.0, (bankX - cornerR) - balanceRightX);
            var totalLen = seg1Len + arcLen + seg3Len;
            var dist = p * totalLen;

            if (dist <= seg1Len)
                return new Point(bankX, bankBottomY + dist);

            var rem = dist - seg1Len;
            if (rem <= arcLen)
            {
                var angle = (rem / arcLen) * (Math.PI * 0.5);
                var cx = bankX - cornerR;
                var cy = bottomY - cornerR;
                return new Point(cx + cornerR * Math.Cos(angle), cy + cornerR * Math.Sin(angle));
            }

            var rem3 = rem - arcLen;
            return new Point(bankX - cornerR - rem3, bottomY);
        }

        return new Point(checkoutRect.Right, topY);
    }

    private static void DrawAmountChip(
        DrawingContext context,
        Point center,
        string label,
        double opacity,
        double scale,
        bool isReturnAuth,
        bool isDark,
        FontFamily fontFamily,
        IBrush mutedBrush,
        IBrush borderStrongBrush,
        IBrush fgBrush,
        Color successColor)
    {
        var textBrush = isReturnAuth ? new SolidColorBrush(successColor) : fgBrush;
        var ft = new FormattedText(
            label,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyle.Normal, FontWeight.Medium),
            11.0,
            textBrush);

        var chipW = ft.Width + 14.0;
        var chipH = 20.0;
        var rect = new Rect(center.X - chipW * 0.5, center.Y - chipH * 0.5, chipW, chipH);
        var radius = chipH * 0.5;

        var transform = Matrix.CreateTranslation(-center.X, -center.Y)
                        * Matrix.CreateScale(scale, scale)
                        * Matrix.CreateTranslation(center.X, center.Y);

        using (context.PushOpacity(Math.Clamp(opacity, 0.0, 1.0)))
        using (context.PushTransform(transform))
        {
            // Subtle floating shadow
            var shadowBrush = new SolidColorBrush(Color.FromArgb(isDark ? (byte)70 : (byte)24, 0, 0, 0));
            context.DrawRectangle(shadowBrush, null, new RoundedRect(new Rect(rect.X, rect.Y + 1.5, rect.Width, rect.Height), radius));

            IBrush chipFill = isReturnAuth
                ? new SolidColorBrush(Color.FromArgb(isDark ? (byte)55 : (byte)32, successColor.R, successColor.G, successColor.B))
                : mutedBrush;

            IBrush chipBorder = isReturnAuth
                ? new SolidColorBrush(Color.FromArgb(150, successColor.R, successColor.G, successColor.B))
                : borderStrongBrush;

            context.DrawRectangle(chipFill, new Pen(chipBorder, 1.0), new RoundedRect(rect, radius));
            context.DrawText(ft, new Point(center.X - ft.Width * 0.5, center.Y - ft.Height * 0.5));
        }
    }

    private static void DrawPortDot(DrawingContext context, Point center, IBrush fill, IBrush stroke, double radius)
    {
        context.DrawEllipse(fill, new Pen(stroke, 1.15), center, radius, radius);
    }

    private static void DrawPhaseBar(
        DrawingContext context,
        Rect rect,
        string leftLabel,
        string rightLabel,
        double progress,
        FontFamily fontFamily,
        IBrush fgBrush,
        IBrush mutedBrush,
        IBrush trackBrush,
        IBrush fillBrush)
    {
        var leftFt = new FormattedText(
            leftLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyle.Normal, FontWeight.Medium),
            12.5,
            fgBrush);

        var rightFt = new FormattedText(
            rightLabel,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal),
            12.0,
            mutedBrush);

        context.DrawText(leftFt, new Point(rect.X, rect.Y));
        context.DrawText(rightFt, new Point(rect.Right - rightFt.Width, rect.Y + 0.5));

        var barY = rect.Y + leftFt.Height + 7.0;
        const double barH = 2.5;
        var trackRect = new Rect(rect.X, barY, rect.Width, barH);
        context.DrawRectangle(trackBrush, null, new RoundedRect(trackRect, barH * 0.5));

        var clamped = Math.Clamp(progress, 0.0, 1.0);
        if (clamped > 0.002)
        {
            var fillRect = new Rect(rect.X, barY, rect.Width * clamped, barH);
            context.DrawRectangle(fillBrush, null, new RoundedRect(fillRect, barH * 0.5));
        }
    }

    private static string FormatCurrency(string symbol, decimal value)
    {
        return string.Concat(symbol, value.ToString("#,##0.00", CultureInfo.InvariantCulture));
    }

    private Color ResolveColor(string key, Color fallback)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var res) && res is Color c)
            return c;
        return fallback;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * Math.Clamp(t, 0.0, 1.0);

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
