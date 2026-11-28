using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaPaymentFlowTests
{
    [Fact]
    public void Timeline_AtZero_IsIdleWithInitialBalance()
    {
        var snap = VelaPaymentFlowTimeline.Evaluate(0.0, 42.50m, 1284.10m, 1.34);

        Assert.Equal(VelaPaymentFlowStage.Idle, snap.Stage);
        Assert.Equal(VelaPaymentNodeState.Idle, snap.CheckoutState);
        Assert.Equal(1284.10m, snap.DisplayedBalance);
        Assert.Equal(0.0, snap.AuthorizationProgress);
        Assert.Equal(0.0, snap.SettlementProgress);
        Assert.False(snap.ChipVisible);
    }

    [Fact]
    public void Timeline_AtSettlementClearing_MatchesClearingReferenceFrame()
    {
        var snap = VelaPaymentFlowTimeline.Evaluate(6.10, 42.50m, 1284.10m, 1.34);

        Assert.Equal(VelaPaymentFlowStage.SettlementClearing, snap.Stage);
        Assert.Equal(VelaPaymentNodeState.Success, snap.CheckoutState);
        Assert.Equal("Approved", snap.CheckoutStatus);
        Assert.Equal("Risk checked", snap.ProcessorStatus);
        Assert.Equal(VelaPaymentNodeState.Active, snap.NetworkState);
        Assert.Equal("Clearing", snap.NetworkStatus);
        Assert.Equal(VelaPaymentNodeState.Success, snap.BankState);
        Assert.Equal("Settled", snap.BankStatus);
        Assert.Equal(1284.10m, snap.DisplayedBalance);

        Assert.Equal(1.0, snap.AuthorizationProgress, 3);
        Assert.Equal(1.34, snap.DisplayedAuthSeconds, 2);
        Assert.InRange(snap.SettlementProgress, 0.25, 0.55);

        Assert.True(snap.ChipVisible);
        Assert.Equal(VelaPaymentChipTrack.BottomSettlement, snap.ChipTrack);
        Assert.True(snap.MastercardDropActive > 0.5);
        Assert.True(snap.BottomTrackFill > 0.25);
    }

    [Fact]
    public void Timeline_AtSettledHold_CreditsFullAmountToBalance()
    {
        var snap = VelaPaymentFlowTimeline.Evaluate(8.50, 42.50m, 1284.10m, 1.34);

        Assert.Equal(VelaPaymentFlowStage.Settled, snap.Stage);
        Assert.Equal(VelaPaymentNodeState.Success, snap.BalanceState);
        Assert.Equal(1326.60m, snap.DisplayedBalance);
        Assert.Equal(1.0, snap.AuthorizationProgress, 3);
        Assert.Equal(1.0, snap.SettlementProgress, 3);
        Assert.Equal(1.0, snap.BottomTrackFill, 3);
        Assert.False(snap.ChipVisible);
    }

    [Fact]
    public void Control_SeekAndStepStage_UpdatesStageAndRaisesEvent()
    {
        var flow = new VelaPaymentFlow
        {
            Amount = 42.50m,
            InitialBalance = 1284.10m,
            AutoPlay = false,
        };

        var stages = new List<VelaPaymentFlowStage>();
        flow.StageChanged += (_, s) => stages.Add(s);

        flow.SeekNormalized(6.10 / VelaPaymentFlowTimeline.TotalDuration);
        Assert.Equal(VelaPaymentFlowStage.SettlementClearing, flow.CurrentStage);
        Assert.Contains(VelaPaymentFlowStage.SettlementClearing, stages);

        flow.StepStage(1);
        Assert.Equal(VelaPaymentFlowStage.SettlementFee, flow.CurrentStage);

        flow.StepStage(1);
        Assert.Equal(VelaPaymentFlowStage.Settled, flow.CurrentStage);

        flow.Reset();
        Assert.Equal(VelaPaymentFlowStage.Idle, flow.CurrentStage);
        Assert.Equal(0.0, flow.NormalizedProgress);
        Assert.False(flow.IsPlaying);
    }

    [Fact]
    public void Control_ClampsPlaybackSpeedSafely()
    {
        var flow = new VelaPaymentFlow();
        flow.PlaybackSpeed = 0.01;
        Assert.Equal(0.25, flow.PlaybackSpeed);

        flow.PlaybackSpeed = 99.0;
        Assert.Equal(4.0, flow.PlaybackSpeed);

        flow.PlaybackSpeed = double.NaN;
        Assert.Equal(1.0, flow.PlaybackSpeed);
    }
}
