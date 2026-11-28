using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Vela.Avalonia.Charts;
using Vela.Avalonia.Controls;
using Vela.Avalonia.HeadlessTests.Infrastructure;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

/// <summary>
/// Full-matrix lifecycle leak tests: mounts every major Vela control and chart into a real
/// headless Window, applies templates, renders with Skia, switches Light/Dark and Accent,
/// detaches from the visual tree, and asserts the instance is collected by GC.
/// </summary>
public class ControlLifecycleLeakTests
{
    public static TheoryData<string> CoreControlNames =>
    [
        nameof(VelaAccordion),
        nameof(VelaAlert),
        nameof(VelaAnimatedCounter),
        nameof(VelaAvatar),
        nameof(VelaBadge),
        nameof(VelaBreadcrumb),
        nameof(VelaBusyOverlay),
        nameof(VelaButton),
        nameof(VelaCalendar),
        nameof(VelaCheckBox),
        nameof(VelaColorPicker),
        nameof(VelaComboBox),
        nameof(VelaCommandPalette),
        nameof(VelaConfirmMorph),
        nameof(VelaCopyButton),
        nameof(VelaDataGrid),
        nameof(VelaDatePicker),
        nameof(VelaDialogHost),
        nameof(VelaDockBar),
        nameof(VelaDragList),
        nameof(VelaDrawerHost),
        nameof(VelaDropdownButton),
        nameof(VelaElasticSlider),
        nameof(VelaEmptyState),
        nameof(VelaFloatingBar),
        nameof(VelaGlassCard),
        nameof(VelaGlassMenu),
        nameof(VelaHoldToConfirm),
        nameof(VelaInput),
        nameof(VelaLiquidGlass),
        nameof(VelaListBox),
        nameof(VelaMenuBar),
        nameof(VelaNavigationView),
        nameof(VelaPageDeck),
        nameof(VelaPagination),
        nameof(VelaPaymentFlow),
        nameof(VelaPopover),
        nameof(VelaPopupSurface),
        nameof(VelaProgress),
        nameof(VelaRadioCards),
        nameof(VelaResizablePanels),
        nameof(VelaRulerSlider),
        nameof(VelaScrubInput),
        nameof(VelaSegmentedControl),
        nameof(VelaSelect),
        nameof(VelaSettingsGroup),
        nameof(VelaShaderBackground),
        nameof(VelaSkeleton),
        nameof(VelaSplitButton),
        nameof(VelaStatusBar),
        nameof(VelaStepper),
        nameof(VelaSwitch),
        nameof(VelaTabs),
        nameof(VelaTagInput),
        nameof(VelaToastHost),
        nameof(VelaToolBar),
        nameof(VelaTreeTable),
        nameof(VelaTreeView),
    ];

    public static TheoryData<string> ChartControlNames =>
    [
        nameof(VelaLineChart),
        nameof(VelaBarChart),
        nameof(VelaDonutChart),
        nameof(VelaSparkline),
        nameof(VelaGauge),
        nameof(VelaActivityHeatmap),
    ];

    [AvaloniaTheory]
    [MemberData(nameof(CoreControlNames))]
    public void CoreControl_CollectedAfterMountRenderThemeSwitchAndDetach(string controlName)
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => CreateCoreControl(controlName),
            exerciseThemeAndAccentSwitch: true,
            label: controlName);
    }

    [AvaloniaTheory]
    [MemberData(nameof(ChartControlNames))]
    public void ChartControl_CollectedAfterMountRenderThemeSwitchAndDetach(string chartName)
    {
        HeadlessLeakVerifier.AssertControlCollected(
            () => CreateChartControl(chartName),
            exerciseThemeAndAccentSwitch: true,
            label: chartName);
    }

    private static Control CreateCoreControl(string name) => name switch
    {
        nameof(VelaAccordion) => new VelaAccordion(),
        nameof(VelaAlert) => new VelaAlert { Title = "Alert" },
        nameof(VelaAnimatedCounter) => new VelaAnimatedCounter { Value = 1234.5 },
        nameof(VelaAvatar) => new VelaAvatar { Initials = "VA" },
        nameof(VelaBadge) => new VelaBadge { Content = "New" },
        nameof(VelaBreadcrumb) => new VelaBreadcrumb(),
        nameof(VelaBusyOverlay) => new VelaBusyOverlay { IsBusy = false, Content = new TextBlock { Text = "Content" } },
        nameof(VelaButton) => new VelaButton { Content = "Click" },
        nameof(VelaCalendar) => new VelaCalendar(),
        nameof(VelaCheckBox) => new VelaCheckBox { Content = "Check" },
        nameof(VelaColorPicker) => new VelaColorPicker(),
        nameof(VelaComboBox) => new VelaComboBox(),
        nameof(VelaCommandPalette) => new VelaCommandPalette(),
        nameof(VelaConfirmMorph) => new VelaConfirmMorph { IdleText = "Delete" },
        nameof(VelaCopyButton) => new VelaCopyButton { CopyText = "vela" },
        nameof(VelaDataGrid) => CreatePopulatedDataGrid(),
        nameof(VelaDatePicker) => new VelaDatePicker(),
        nameof(VelaDialogHost) => new VelaDialogHost { Content = new TextBlock { Text = "Host" } },
        nameof(VelaDockBar) => new VelaDockBar(),
        nameof(VelaDragList) => new VelaDragList { DragGroup = "leak-test", ItemsSource = new[] { "A", "B", "C" } },
        nameof(VelaDrawerHost) => new VelaDrawerHost { Content = new TextBlock { Text = "Host" } },
        nameof(VelaDropdownButton) => new VelaDropdownButton { Content = "Menu" },
        nameof(VelaElasticSlider) => new VelaElasticSlider { Minimum = 0, Maximum = 100, Value = 42 },
        nameof(VelaEmptyState) => new VelaEmptyState { Title = "Empty", Description = "Nothing here" },
        nameof(VelaFloatingBar) => new VelaFloatingBar(),
        nameof(VelaGlassCard) => new VelaGlassCard { Content = new TextBlock { Text = "Glass" } },
        nameof(VelaGlassMenu) => new VelaGlassMenu(),
        nameof(VelaHoldToConfirm) => new VelaHoldToConfirm(),
        nameof(VelaInput) => new VelaInput { Text = "Hello" },
        nameof(VelaLiquidGlass) => new VelaLiquidGlass { Width = 240, Height = 120, Content = new TextBlock { Text = "Optical" } },
        nameof(VelaListBox) => new VelaListBox { ItemsSource = new[] { "A", "B", "C" } },
        nameof(VelaMenuBar) => new VelaMenuBar(),
        nameof(VelaNavigationView) => new VelaNavigationView(),
        nameof(VelaPageDeck) => new VelaPageDeck(),
        nameof(VelaPagination) => new VelaPagination { TotalPages = 10, CurrentPage = 2 },
        nameof(VelaPaymentFlow) => new VelaPaymentFlow { Width = 320, Height = 200 },
        nameof(VelaPopover) => new VelaPopover { Content = new TextBlock { Text = "Trigger" } },
        nameof(VelaPopupSurface) => new VelaPopupSurface { Child = new TextBlock { Text = "Popup" } },
        nameof(VelaProgress) => new VelaProgress { Value = 55 },
        nameof(VelaRadioCards) => new VelaRadioCards(),
        nameof(VelaResizablePanels) => new VelaResizablePanels(),
        nameof(VelaRulerSlider) => new VelaRulerSlider { Minimum = 0, Maximum = 100, Value = 30 },
        nameof(VelaScrubInput) => new VelaScrubInput { Value = 50 },
        nameof(VelaSegmentedControl) => new VelaSegmentedControl(),
        nameof(VelaSelect) => new VelaSelect(),
        nameof(VelaSettingsGroup) => new VelaSettingsGroup { Header = "General" },
        nameof(VelaShaderBackground) => new VelaShaderBackground { Width = 320, Height = 200 },
        nameof(VelaSkeleton) => new VelaSkeleton { IsLoading = true, Width = 160, Height = 24 },
        nameof(VelaSplitButton) => new VelaSplitButton { Content = "Save" },
        nameof(VelaStatusBar) => new VelaStatusBar(),
        nameof(VelaStepper) => new VelaStepper(),
        nameof(VelaTagInput) => new VelaTagInput(),
        nameof(VelaTabs) => new VelaTabs(),
        nameof(VelaSwitch) => new VelaSwitch { IsChecked = true },
        nameof(VelaToastHost) => new VelaToastHost(),
        nameof(VelaToolBar) => new VelaToolBar(),
        nameof(VelaTreeTable) => CreatePopulatedTreeTable(),
        nameof(VelaTreeView) => new VelaTreeView(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown core control"),
    };

    private static Control CreateChartControl(string name) => name switch
    {
        nameof(VelaLineChart) => new VelaLineChart
        {
            Width = 480,
            Height = 240,
            Labels = ["Mon", "Tue", "Wed", "Thu", "Fri"],
            Series =
            [
                new VelaLineSeries("s1", "Traffic", [12, 28, 19, 44, 36]),
                new VelaLineSeries("s2", "Conversions", [4, 9, 7, 15, 13]),
            ],
        },
        nameof(VelaBarChart) => new VelaBarChart
        {
            Width = 480,
            Height = 240,
            Data =
            [
                new VelaBarDatum("Q1", 120),
                new VelaBarDatum("Q2", 185),
                new VelaBarDatum("Q3", 142),
                new VelaBarDatum("Q4", 210),
            ],
        },
        nameof(VelaDonutChart) => new VelaDonutChart
        {
            Width = 320,
            Height = 240,
            Data =
            [
                new VelaDonutDatum("a", "Desktop", 64),
                new VelaDonutDatum("b", "Mobile", 26),
                new VelaDonutDatum("c", "Web", 10),
            ],
        },
        nameof(VelaSparkline) => new VelaSparkline
        {
            Width = 180,
            Height = 48,
            Values = [10, 14, 12, 18, 22, 19, 25],
        },
        nameof(VelaGauge) => new VelaGauge
        {
            Width = 180,
            Height = 140,
            Minimum = 0,
            Maximum = 100,
            Value = 72,
            Label = "CPU",
            Unit = "%",
        },
        nameof(VelaActivityHeatmap) => new VelaActivityHeatmap
        {
            Width = 420,
            Height = 140,
            Weeks = 12,
            Entries = Enumerable.Range(0, 60)
                .Select(i => new VelaHeatmapEntry(DateTime.Today.AddDays(-i), i % 6))
                .ToArray(),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown chart control"),
    };

    private static VelaDataGrid CreatePopulatedDataGrid()
    {
        var grid = new VelaDataGrid { Width = 640, Height = 320 };
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((SampleRow)x).Name });
        grid.Columns.Add(new VelaDataGridTextColumn { Header = "Role", ValueSelector = x => ((SampleRow)x).Role });
        grid.ItemsSource = Enumerable.Range(0, 20)
            .Select(i => new SampleRow($"User {i}", i % 2 == 0 ? "Engineer" : "Designer"))
            .ToArray();
        return grid;
    }

    private static VelaTreeTable CreatePopulatedTreeTable()
    {
        var tree = new VelaTreeTable { Width = 640, Height = 320 };
        tree.Columns.Add(new VelaDataGridTextColumn { Header = "Name", ValueSelector = x => ((SampleNode)x).Name });
        tree.ChildrenSelector = x => ((SampleNode)x).Children;
        tree.ItemsSource = new[]
        {
            new SampleNode("Root", [new SampleNode("Child A", []), new SampleNode("Child B", [])]),
        };
        return tree;
    }

    private sealed record SampleRow(string Name, string Role);
    private sealed record SampleNode(string Name, SampleNode[] Children);
}
