using BenchmarkDotNet.Attributes;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Benchmarks;

[MemoryDiagnoser]
public class SpringAndTokenBenchmarks
{
    private readonly SpringChannel _spring = new(0.0, SpringSpec.FromVisualDuration(0.30, bounce: 0.2));
    private readonly VelaObservableList<int> _observableList = new();
    private int[] _payload1K = null!;
    private int[] _payload10K = null!;

    [GlobalSetup]
    public void Setup()
    {
        _payload1K = Enumerable.Range(0, 1_000).ToArray();
        _payload10K = Enumerable.Range(0, 10_000).ToArray();
    }

    [Benchmark(Description = "SpringChannel: 1,000 analytical steps (60Hz)")]
    public double SpringChannel_Step_1000Frames()
    {
        _spring.Jump(0.0);
        _spring.SetTarget(100.0);
        for (var i = 0; i < 1_000; i++)
        {
            if (!_spring.IsAnimating)
                _spring.SetTarget((i & 1) == 0 ? 100.0 : 0.0);
            _spring.Step(1.0 / 60.0);
        }
        return _spring.Value;
    }

    [Benchmark(Description = "VelaAccentPalettes: lookup all accents (Light + Dark)")]
    public int AccentPalettes_LookupAll()
    {
        var checksum = 0;
        for (var i = 0; i < 6; i++)
        {
            var accent = (VelaAccent)i;
            checksum += VelaAccentPalettes.Get(accent, isDark: false).Accent.R;
            checksum += VelaAccentPalettes.Get(accent, isDark: true).Accent.R;
        }
        return checksum;
    }

    [Benchmark(Description = "VelaObservableList.ReplaceAll (1,000 items)")]
    public int ObservableList_ReplaceAll_1K()
    {
        _observableList.ReplaceAll(_payload1K);
        return _observableList.Count;
    }

    [Benchmark(Description = "VelaObservableList.ReplaceAll (10,000 items)")]
    public int ObservableList_ReplaceAll_10K()
    {
        _observableList.ReplaceAll(_payload10K);
        return _observableList.Count;
    }
}
