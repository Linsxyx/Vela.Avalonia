# Vela.Avalonia

A motion-first, performance-engineered **Avalonia 12** (`net10.0`) desktop UI component library featuring exact analytical spring physics, OKLCH-derived color tokens, zero-flicker live theming, MIT clean-room **SkSL GPU ambient shaders**, optical **Liquid Glass** surfaces (`SkSL` + Avalonia 12 `BackdropEffect`), an **App Rail** hierarchical sidebar shell, an optional frameless **`VelaWindow`**, and interactive Skia vector charts.

- **Targets**: .NET 10 (`net10.0`), Avalonia `12.2.0`, Native AOT compatible (`IsAotCompatible=true`)
- **License**: MIT (100% clean-room implementation)
- **Packages**:
  - **`Vela.Avalonia`** — Core theme, OKLCH tokens, analytical spring physics engine, SkSL ambient shaders, optical Liquid Glass, optional `VelaWindow`, and **50+ controls & primitives**
  - **`Vela.Avalonia.Charts`** — 6 interactive, theme-aware Skia vector data visualization controls

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

Switch accents at runtime with zero visual-tree flicker:

```csharp
var theme = Styles.OfType<VelaTheme>().FirstOrDefault();
if (theme is not null)
    theme.Accent = VelaAccent.Violet; // Neutral, Violet, Blue, Green, Amber, Orange, Coral, Rose
```

### 3. Optional Modern Frameless Window (`VelaWindow`)

`VelaWindow` is 100% opt-in (standard `Window` works everywhere). It seamlessly integrates native macOS traffic lights and provides restrained monochrome caption buttons on Windows & Linux:

```xml
<vela:VelaWindow xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:vela="using:Vela.Avalonia.Controls"
                 x:Class="MyApp.MainWindow"
                 Title="Vela Studio"
                 Width="1180" Height="790">
  <vela:VelaNavigationView BrandTitle="Vela Studio"
                           ShowTopBar="True"
                           PaneVariant="Standard">
    <vela:VelaNavItem Header="Home" Icon="{DynamicResource VelaIconHome}" />
    <vela:VelaNavSectionHeader Header="Workspace" />
    <vela:VelaNavItem Header="Projects" Icon="{DynamicResource VelaIconFolder}" IsExpanded="True">
      <vela:VelaNavItem Header="Design System" />
      <vela:VelaNavItem Header="Desktop Shell" />
    </vela:VelaNavItem>
  </vela:VelaNavigationView>
</vela:VelaWindow>
```

---

## Architecture Highlights

### Exact Analytical Spring Physics (`Vela.Avalonia.Motion`)
- `SpringChannel` solves the exact 2nd-order damped harmonic oscillator ODE (`m·x'' + c·x' + k·(x - target) = 0`) across underdamped, critically damped, and overdamped regimes — unconditionally stable across 60Hz / 120Hz / 144Hz displays.
- Retargeting mid-flight inherits instantaneous velocity seamlessly with zero jumps.
- Respects global `VelaMotionOptions.ReducedMotion` and automatically goes 0% CPU idle at rest.

### Zero-Jank App Rail Navigation (`VelaNavigationView` & `VelaPageDeck`)
- **Hierarchical Tree & Sections**: `VelaNavSectionHeader`, collapsible `VelaNavItem.Children` with 1px vertical guide lines, sticky `FooterItems`, separated profile `Footer`, and automatic `VelaBreadcrumb` TopBar.
- **3 Pane Styles & Dual Modes**: `Standard` / `Floating` / `Frosted` pane variants + `Left` / `Top` display modes + smooth zero-reflow collapse to a 60px icon rail.
- **O(1) Page Caching**: `KeepAlive` / `Lru` / `None` policies via `VelaPageDeck`, lazy `PageFactory`, skipped layout on hidden pages, and `IVelaNavigationAware` lifecycle hooks.

### Clean-Room MIT GPU Shaders (`VelaLiquidGlass` & `VelaShaderBackground`)
- **`VelaLiquidGlass`**: Official Avalonia 12.2 `Visual.BackdropEffect` paired with a clean-room MIT SkSL shader performing rounded-box SDF lens refraction, subtle RGB chromatic dispersion, and Fresnel rim specular highlights — also available as `SurfaceMaterial="LiquidGlass"` on `VelaDialogHost`, `VelaDrawerHost`, `VelaCommandPalette`, and `VelaDockBar`.
- **`VelaShaderBackground`**: Real-time SkSL ambient fluid shader (`Mesh`, `Aurora`, `Silk`) that automatically tracks `VelaTheme` accent & Light/Dark changes with smooth GPU color interpolation.

---

## Component Catalog (58 Controls & Primitives)

| Category | Controls |
| --- | --- |
| **Window & Navigation Shell** | `VelaWindow`, `VelaTrayIcon`, `VelaNavigationView`, `VelaNavItem`, `VelaNavSectionHeader`, `VelaPageDeck`, `VelaTabs`, `VelaTabItem`, `VelaDockBar`, `VelaDockItem`, `VelaFloatingBar`, `VelaCommandPalette` (`⌘K` / `Ctrl+K`), `VelaBreadcrumb`, `VelaPagination`, `VelaStepper` |
| **Actions & Inputs** | `VelaButton`, `VelaCopyButton`, `VelaSplitButton`, `VelaConfirmMorph`, `VelaHoldToConfirm`, `VelaSwitch`, `VelaCheckBox`, `VelaSegmentedControl`, `VelaElasticSlider`, `VelaRulerSlider`, `VelaInput`, `VelaScrubInput`, `VelaTagInput`, `VelaColorPicker`, `VelaBadge` |
| **Selection & Pickers** | `VelaSelect`, `VelaComboBox`, `VelaDropdownButton`, `VelaMenuItem`, `VelaPopover`, `VelaRadioCards`, `VelaRadioCardItem`, `VelaCalendar`, `VelaDatePicker`, Native `ContextMenu` / `MenuItem` |
| **Layout, Settings & Overlays** | `VelaSettingsGroup`, `VelaSettingsRow`, `VelaResizablePanels`, `VelaResizablePane`, `VelaDialogHost`, `VelaDrawerHost`, `VelaToastHost`, `VelaBusyOverlay`, `VelaSpinnerRing`, `VelaAccordion`, `VelaAccordionItem`, `VelaCollapsePanel` |
| **Feedback, Identity & Surfaces** | `VelaLiquidGlass`, `VelaShaderBackground`, `VelaPopupSurface`, `VelaMorphSlotPanel`, `VelaAvatar`, `VelaAvatarGroup`, `VelaEmptyState`, `VelaAnimatedCounter`, `VelaMetricCard`, `VelaProgress`, `VelaSkeleton`, `VelaAlert`, Thin overlay `ScrollBar` & inverted `ToolTip` themes |
| **Charts (`Vela.Avalonia.Charts`)** | `VelaDonutChart`, `VelaLineChart`, `VelaBarChart`, `VelaSparkline`, `VelaGauge`, `VelaActivityHeatmap` |

---

## Live Browser Preview (GitHub Pages / WebAssembly)

This repository includes a WebAssembly head (`samples/Vela.Avalonia.Gallery.Browser`) sharing 100% of the Gallery UI (`MainView`) with the Desktop app, plus `.github/workflows/pages.yml`:

1. Push this repository to GitHub.
2. Open **Settings → Pages → Build and deployment → Source** and choose **GitHub Actions**.
3. Push to `main` (or trigger **Deploy Gallery to GitHub Pages** manually in **Actions**).

---

## Automated NuGet Publishing (Trusted Publishing / OIDC)

`.github/workflows/release.yml` uses **NuGet Trusted Publishing (`NuGet/login@v1`)** via GitHub OIDC — no long-lived API keys to store or rotate. It builds, runs all unit tests, packs `Vela.Avalonia` + `Vela.Avalonia.Charts` (with `.snupkg` symbols), publishes to NuGet.org, and creates a GitHub Release automatically:

1. On [nuget.org](https://www.nuget.org/), open **Account settings → Trusted Publishers → Add a new publisher (GitHub)**:
   - **Owner / Repository**: your GitHub owner and repository name
   - **Workflow File**: `release.yml` (filename only, without `.github/workflows/`)
   - **Environment**: leave blank
2. In your GitHub repo, open **Settings → Secrets and variables → Actions → Variables** and add **`NUGET_USER`** set to your NuGet.org profile username (not your email).
3. Push a semantic version tag:

```bash
git tag v0.1.0
git push origin v0.1.0
```

---

## Local Development

```bash
# Build, run tests, and launch Desktop Gallery
dotnet build
dotnet test
dotnet run --project samples/Vela.Avalonia.Gallery

# Pack Release NuGet packages locally
dotnet pack src/Vela.Avalonia/Vela.Avalonia.csproj -c Release -o ./artifacts
dotnet pack src/Vela.Avalonia.Charts/Vela.Avalonia.Charts.csproj -c Release -o ./artifacts
```

---

## License

MIT © 2026 Vela.Avalonia Contributors. See [LICENSE](LICENSE).
