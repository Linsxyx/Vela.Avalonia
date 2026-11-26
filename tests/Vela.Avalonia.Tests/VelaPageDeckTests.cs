using Avalonia.Controls;
using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public sealed class VelaPageDeckTests
{
    private sealed class MockPage : Border, IVelaNavigationAware
    {
        public int NavigatedToCount { get; private set; }
        public int NavigatedFromCount { get; private set; }

        public void OnNavigatedTo() => NavigatedToCount++;
        public void OnNavigatedFrom() => NavigatedFromCount++;
    }

    [Fact]
    public void KeepAlive_CachesVisitedPagesAndSkipsReinstantiation()
    {
        var deck = new VelaPageDeck
        {
            PageCacheMode = VelaPageCacheMode.KeepAlive,
            TransitionMode = VelaPageTransitionMode.Instant,
        };

        var factoryCalls = 0;
        var pageA = new MockPage();
        var pageB = new MockPage();

        VelaPageSwitchMetrics? lastMetric = null;
        deck.PageSwitched += (_, m) => lastMetric = m;

        deck.Activate("A", () => { factoryCalls++; return pageA; });
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, pageA.NavigatedToCount);
        Assert.True(pageA.IsVisible);
        Assert.False(lastMetric!.Value.IsCacheHit);

        deck.Activate("B", () => { factoryCalls++; return pageB; });
        Assert.Equal(2, factoryCalls);
        Assert.Equal(1, pageA.NavigatedFromCount);
        Assert.False(pageA.IsVisible);
        Assert.True(pageB.IsVisible);

        // Return to A — must be an O(1) cache hit with zero factory calls
        deck.Activate("A", () => { factoryCalls++; return new MockPage(); });
        Assert.Equal(2, factoryCalls);
        Assert.True(lastMetric!.Value.IsCacheHit);
        Assert.Equal(2, pageA.NavigatedToCount);
        Assert.True(pageA.IsVisible);
        Assert.False(pageB.IsVisible);
        Assert.Equal(2, deck.CachedPageCount);
    }

    [Fact]
    public void LruCache_EvictsLeastRecentlyUsedPage()
    {
        var deck = new VelaPageDeck
        {
            PageCacheMode = VelaPageCacheMode.Lru,
            MaxCachedPages = 2,
            TransitionMode = VelaPageTransitionMode.Instant,
        };

        var createdA = 0;
        deck.Activate("A", () => { createdA++; return new MockPage(); });
        deck.Activate("B", () => new MockPage());
        deck.Activate("C", () => new MockPage());

        // MaxCachedPages = 2 -> "A" was evicted, "B" and "C" remain
        Assert.Equal(2, deck.CachedPageCount);

        // Revisiting A must re-run factory
        deck.Activate("A", () => { createdA++; return new MockPage(); });
        Assert.Equal(2, createdA);
        Assert.Equal(2, deck.CachedPageCount);
    }

    [Fact]
    public void NoneMode_DiscardsOutgoingPageImmediately()
    {
        var deck = new VelaPageDeck
        {
            PageCacheMode = VelaPageCacheMode.None,
            TransitionMode = VelaPageTransitionMode.Instant,
        };

        var createdA = 0;
        deck.Activate("A", () => { createdA++; return new MockPage(); });
        deck.Activate("B", () => new MockPage());
        Assert.Equal(1, deck.CachedPageCount);

        deck.Activate("A", () => { createdA++; return new MockPage(); });
        Assert.Equal(2, createdA);
        Assert.Equal(1, deck.CachedPageCount);
    }

    [Fact]
    public void ClearInactiveCache_KeepsOnlyActivePage()
    {
        var deck = new VelaPageDeck
        {
            PageCacheMode = VelaPageCacheMode.KeepAlive,
            TransitionMode = VelaPageTransitionMode.Instant,
        };

        deck.Activate("A", () => new MockPage());
        deck.Activate("B", () => new MockPage());
        deck.Activate("C", () => new MockPage());
        Assert.Equal(3, deck.CachedPageCount);

        deck.ClearInactiveCache();
        Assert.Equal(1, deck.CachedPageCount);
    }
}
