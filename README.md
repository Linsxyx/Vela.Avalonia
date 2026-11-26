# Vela.Avalonia

A motion-first, performance-engineered **Avalonia 12** (`net10.0`) UI component library featuring exact analytical spring physics, OKLCH-derived color tokens, zero-flicker live theming, optical **Liquid Glass** surfaces (`SkSL` + Avalonia 12 `BackdropEffect`), and interactive Skia vector charts.

- **Targets**: .NET 10 (`net10.0`), Avalonia `12.2.0`, Native AOT compatible (`IsAotCompatible=true`)
- **License**: MIT (100% clean-room implementation)
- **Packages**:
  - `Vela.Avalonia` — Core theme, OKLCH tokens, spring physics engine, optical Liquid Glass, and 35+ controls
  - `Vela.Avalonia.Charts` — 6 interactive, theme-aware Skia vector data visualization controls

---

## Live Browser Preview (GitHub Pages / WebAssembly)

This repository includes a dedicated WebAssembly head (`samples/Vela.Avalonia.Gallery.Browser`) sharing 100% of the Gallery UI (`MainView`) with the native Desktop app, plus an automated `.github/workflows/pages.yml` deployment workflow.

To publish your own live preview on GitHub Pages:

1. Push this repository to GitHub.
2. Open **Settings → Pages → Build and deployment → Source** and choose **GitHub Actions**.
3. Push to `main` (or manually trigger **Deploy Gallery to GitHub Pages** in the **Actions** tab).
4. Visit `https://<your-username>.github.io/<repo-name>/` to interact with the full Gallery directly in the browser.

---

## Quick Start

### 1. Install Packages

```bash
dotnet add package Vela.Avalonia
dotnet add package Vela.Avalonia.Charts # Optional charts package
```

### 2. Register `VelaTheme` in `App.axaml`

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vela="using:Vela.Avalonia"
             x:Class="MyApp.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
    <vela:VelaTheme Accent="Neutral" />
  </Application.Styles>
</Application>
```

Switch themes or accents live with zero visual-tree flicker:

```csharp
var theme = Styles.OfType<VelaTheme>().FirstOrDefault();
if (theme is not null)
    theme.Accent = VelaAccent.Violet; // Neutral, Violet, Blue, Green, Amber, Orange, Coral, Rose
```

---

## Architecture Highlights

### Exact Analytical Spring Physics (`Vela.Avalonia.Motion`)
- `SpringChannel` solves the exact 2nd-order damped harmonic oscillator ODE (`m·x'' + c·x' + k·(x - target) = 0`) across underdamped, critically damped, and overdamped regimes — unconditionally stable across 60Hz/120Hz/144Hz displays.
- Retargeting mid-flight inherits instantaneous velocity seamlessly with zero jumps.
- Respects global `VelaMotionOptions.ReducedMotion`.

### Zero-Jank Page Switching (`VelaPageDeck` & `VelaNavigationView`)
- **Lazy `PageFactory`**: Heavy views are never instantiated at startup.
- **`KeepAlive` / `Lru` / `None` caching**: Returning to visited pages is an **O(1)** visibility toggle (`<0.2ms`) with scroll positions and form state preserved.
- **Zero idle layout cost**: Inactive cached pages are skipped completely during `MeasureOverride` and `ArrangeOverride`.
- **`IVelaNavigationAware`**: Crisp `OnNavigatedTo()` / `OnNavigatedFrom()` lifecycle hooks even when pages remain cached.

### Optical Liquid Glass (`VelaLiquidGlass`)
- Built directly on official Avalonia 12.2 `Visual.BackdropEffect` (`BackdropEffectCache`) with a clean-room MIT SkSL shader performing rounded-box SDF lens refraction, subtle RGB chromatic dispersion, and Fresnel rim specular highlights.
- Available standalone (`VelaLiquidGlass`) and as an opt-in `SurfaceMaterial="LiquidGlass"` on `VelaDialogHost`, `VelaDrawerHost`, and `VelaCommandPalette`.

---

## Component Catalog (42 Controls & Primitives)

| Category | Controls |
| --- | --- |
| **Navigation & Shell** | `VelaNavigationView`, `VelaNavItem`, `VelaPageDeck`, `VelaTabs`, `VelaTabItem`, `VelaFloatingBar`, `VelaCommandPalette` (`⌘K` / `Ctrl+K`) |
| **Core Actions & Inputs** | `VelaButton`, `VelaConfirmMorph`, `VelaSwitch`, `VelaCheckBox`, `VelaSegmentedControl`, `VelaElasticSlider`, `VelaInput`, `VelaScrubInput`, `VelaTagInput`, `VelaBadge` |
| **Selection & Pickers** | `VelaSelect`, `VelaComboBox`, `VelaDropdownButton`, `VelaPopover`, `VelaRadioCards`, `VelaRadioCardItem`, `VelaCalendar`, `VelaDatePicker`, Native `ContextMenu`/`MenuItem` |
| **Overlays & Feedback** | `VelaDialogHost`, `VelaDrawerHost`, `VelaToastHost`, `VelaProgress`, `VelaSkeleton`, `VelaAlert`, `VelaAccordion`, `VelaAccordionItem`, `VelaCollapsePanel` |
| **Surfaces & Primitives** | `VelaLiquidGlass`, `VelaPopupSurface`, `VelaMorphSlotPanel`, `VelaAnimatedCounter`, `VelaMetricCard`, Minimalist `ScrollBar` & `ToolTip` themes |
| **Charts (`Vela.Avalonia.Charts`)** | `VelaDonutChart`, `VelaLineChart`, `VelaBarChart`, `VelaSparkline`, `VelaGauge`, `VelaActivityHeatmap` |

---

## Local Development

### Build, Test & Run Desktop Gallery

```bash
dotnet build
dotnet test
dotnet run --project samples/Vela.Avalonia.Gallery
```

### Run WebAssembly Gallery Locally

Requires the .NET WebAssembly workload (`dotnet workload install wasm-tools`):

```bash
dotnet run --project samples/Vela.Avalonia.Gallery.Browser
```

To produce the static site bundle for hosting:

```bash
dotnet publish samples/Vela.Avalonia.Gallery.Browser/Vela.Avalonia.Gallery.Browser.csproj -c Release
# Static site output:
# samples/Vela.Avalonia.Gallery.Browser/bin/Release/net10.0-browser/publish/wwwroot
```

---

## License

MIT © 2026 Vela.Avalonia Contributors. See [LICENSE](LICENSE).
