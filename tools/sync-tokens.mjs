#!/usr/bin/env node
/**
 * Converts Arc UI's OKLCH + color-mix + relative color foundation tokens
 * into Vela.Avalonia AXAML ResourceDictionaries and C# Accent palette tables.
 */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const tokensOutDir = path.resolve(__dirname, "../src/Vela.Avalonia/Tokens");
fs.mkdirSync(tokensOutDir, { recursive: true });

// ---- CSS Color 4 OKLab / OKLCH <-> sRGB math ----
const clamp01 = (x) => Math.max(0, Math.min(1, x));

function srgbToLinear(c) {
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}
function linearToSrgb(c) {
  return c <= 0.0031308 ? 12.92 * c : 1.055 * Math.pow(c, 1 / 2.4) - 0.055;
}

function oklabToSrgb(L, a, b) {
  const l_ = L + 0.3963377774 * a + 0.2158037573 * b;
  const m_ = L - 0.1055613458 * a - 0.0638541728 * b;
  const s_ = L - 0.0894841775 * a - 1.2914855480 * b;
  const l = l_ * l_ * l_;
  const m = m_ * m_ * m_;
  const s = s_ * s_ * s_;
  const lr = +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
  const lg = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
  const lb = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
  return [clamp01(linearToSrgb(lr)), clamp01(linearToSrgb(lg)), clamp01(linearToSrgb(lb))];
}

function srgbToOklab(r, g, b) {
  const lr = srgbToLinear(r);
  const lg = srgbToLinear(g);
  const lb = srgbToLinear(b);
  const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
  const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
  const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
  return [
    0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
    1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
    0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s,
  ];
}

function oklch(L, C, H, alpha = 1) {
  const rad = (H * Math.PI) / 180;
  const [r, g, b] = oklabToSrgb(L, C * Math.cos(rad), C * Math.sin(rad));
  return { r, g, b, a: alpha };
}

function srgbToOklch({ r, g, b, a = 1 }) {
  const [L, oa, ob] = srgbToOklab(r, g, b);
  const C = Math.hypot(oa, ob);
  const H = C < 1e-6 ? 0 : ((Math.atan2(ob, oa) * 180) / Math.PI + 360) % 360;
  return { L, C, H, a };
}

function hexColor(hex, alpha = 1) {
  const h = hex.replace("#", "");
  const full = h.length === 3 ? h.split("").map((c) => c + c).join("") : h;
  return {
    r: parseInt(full.slice(0, 2), 16) / 255,
    g: parseInt(full.slice(2, 4), 16) / 255,
    b: parseInt(full.slice(4, 6), 16) / 255,
    a: alpha,
  };
}

function rgbaColor(r, g, b, a = 1) {
  return { r: r / 255, g: g / 255, b: b / 255, a };
}

function mixOklab(c1, w1, c2, w2) {
  const [L1, a1, b1] = srgbToOklab(c1.r, c1.g, c1.b);
  const [L2, a2, b2] = srgbToOklab(c2.r, c2.g, c2.b);
  const [r, g, b] = oklabToSrgb(L1 * w1 + L2 * w2, a1 * w1 + a2 * w2, b1 * w1 + b2 * w2);
  return { r, g, b, a: c1.a * w1 + c2.a * w2 };
}

function withAlpha(c, alpha) {
  return { ...c, a: alpha };
}

function toAvaloniaHex(c) {
  const b = (v) => Math.round(clamp01(v) * 255).toString(16).padStart(2, "0").toUpperCase();
  return `#${b(c.a)}${b(c.r)}${b(c.g)}${b(c.b)}`;
}

function deriveSeries(accentRgba, isDark) {
  const { L, C, H } = srgbToOklch(accentRgba);
  const chromatic = C > 0.02 ? 1 : 0;
  const baseL = isDark ? (L * chromatic + 0.73 * (1 - chromatic)) : (L * chromatic + 0.58 * (1 - chromatic));
  const baseC = C * chromatic + 0.15 * (1 - chromatic);
  const baseH = H * chromatic + 250 * (1 - chromatic);
  const clamp = (lo, x, hi) => Math.max(lo, Math.min(hi, x));

  if (!isDark) {
    return [
      oklch(baseL, baseC, baseH),
      oklch(clamp(0.45, baseL - 0.25, 0.76), 0.19, baseH + 57),
      oklch(clamp(0.45, baseL + 0.14, 0.76), 0.18, baseH + 184),
      oklch(clamp(0.45, baseL, 0.76), 0.21, baseH + 292),
    ];
  }
  return [
    oklch(baseL, baseC, baseH),
    oklch(clamp(0.485, baseL - 0.19, 0.665), 0.21, baseH + 66),
    oklch(clamp(0.485, baseL + 0.02, 0.665), 0.21, baseH + 175),
    oklch(clamp(0.485, baseL - 0.1, 0.665), 0.18, baseH + 270),
  ];
}

// ---- Exact semantic tokens (Arc OKLCH + subtle desktop sidebar surface hierarchy) ----
const lightColors = {
  VelaBackground: oklch(0.988, 0, 0),
  VelaSurface: oklch(1.0, 0, 0),
  VelaSurfaceRaised: oklch(1.0, 0, 0),
  VelaSurfaceMuted: oklch(0.968, 0, 0),
  VelaSidebarBackground: oklch(0.994, 0, 0),
  VelaForeground: oklch(0.15, 0, 0),
  VelaTextSecondary: oklch(0.46, 0, 0),
  VelaTextMuted: oklch(0.59, 0, 0),
  VelaBorder: oklch(0.925, 0, 0),
  VelaBorderSubtle: oklch(0.955, 0, 0),
  VelaBorderStrong: oklch(0.82, 0, 0),
  VelaControlTrack: oklch(0.895, 0, 0),
  VelaControlTrackHover: oklch(0.86, 0, 0),
  VelaControlThumb: oklch(1.0, 0, 0),
  VelaToneTeal: oklch(0.50, 0.095, 182),
  VelaToneSky: oklch(0.51, 0.11, 240),
  VelaToneOrchid: oklch(0.52, 0.17, 322),
  VelaToneViolet: oklch(0.51, 0.16, 290),
  VelaToneRose: oklch(0.53, 0.18, 10),
  VelaToneAmber: oklch(0.55, 0.12, 68),
  VelaToneGreen: oklch(0.50, 0.12, 162),
  VelaTickOff: withAlpha(oklch(0.15, 0, 0), 0.32),
  VelaTickOn: withAlpha(oklch(1.0, 0, 0), 0.60),
};
lightColors.VelaSuccess = lightColors.VelaToneGreen;
lightColors.VelaWarning = lightColors.VelaToneAmber;
lightColors.VelaDanger = lightColors.VelaToneRose;
lightColors.VelaInfo = lightColors.VelaToneSky;
lightColors.VelaSuccessSubtle = mixOklab(lightColors.VelaSuccess, 0.11, lightColors.VelaSurface, 0.89);
lightColors.VelaWarningSubtle = mixOklab(lightColors.VelaWarning, 0.12, lightColors.VelaSurface, 0.88);
lightColors.VelaDangerSubtle = mixOklab(lightColors.VelaDanger, 0.11, lightColors.VelaSurface, 0.89);
lightColors.VelaInfoSubtle = mixOklab(lightColors.VelaInfo, 0.11, lightColors.VelaSurface, 0.89);
lightColors.VelaOverlayScrim = oklch(0.10, 0, 0, 0.44);
lightColors.VelaDangerMuted = mixOklab(lightColors.VelaDanger, 0.55, lightColors.VelaSurfaceMuted, 0.45);
lightColors.VelaChartOther = mixOklab(lightColors.VelaForeground, 0.26, lightColors.VelaSurface, 0.74);

const darkColors = {
  VelaBackground: oklch(0.185, 0, 0),
  VelaSurface: oklch(0.215, 0, 0),
  VelaSurfaceRaised: oklch(0.24, 0, 0),
  VelaSurfaceMuted: oklch(0.26, 0, 0),
  VelaSidebarBackground: oklch(0.20, 0, 0),
  VelaForeground: oklch(0.96, 0, 0),
  VelaTextSecondary: oklch(0.77, 0, 0),
  VelaTextMuted: oklch(0.64, 0, 0),
  VelaBorder: oklch(0.29, 0, 0),
  VelaBorderSubtle: oklch(0.25, 0, 0),
  VelaBorderStrong: oklch(0.40, 0, 0),
  VelaControlTrack: oklch(0.33, 0, 0),
  VelaControlTrackHover: oklch(0.37, 0, 0),
  VelaControlThumb: oklch(0.97, 0, 0),
  VelaToneTeal: oklch(0.79, 0.13, 182),
  VelaToneSky: oklch(0.79, 0.095, 236),
  VelaToneOrchid: oklch(0.76, 0.15, 322),
  VelaToneViolet: oklch(0.74, 0.125, 290),
  VelaToneRose: oklch(0.71, 0.155, 8),
  VelaToneAmber: oklch(0.83, 0.12, 78),
  VelaToneGreen: oklch(0.79, 0.14, 162),
  VelaTickOff: withAlpha(oklch(0.96, 0, 0), 0.32),
  VelaTickOn: withAlpha(oklch(0.19, 0, 0), 0.60),
};
darkColors.VelaSuccess = darkColors.VelaToneGreen;
darkColors.VelaWarning = darkColors.VelaToneAmber;
darkColors.VelaDanger = darkColors.VelaToneRose;
darkColors.VelaInfo = darkColors.VelaToneSky;
darkColors.VelaSuccessSubtle = mixOklab(darkColors.VelaSuccess, 0.14, darkColors.VelaSurface, 0.86);
darkColors.VelaWarningSubtle = mixOklab(darkColors.VelaWarning, 0.14, darkColors.VelaSurface, 0.86);
darkColors.VelaDangerSubtle = mixOklab(darkColors.VelaDanger, 0.14, darkColors.VelaSurface, 0.86);
darkColors.VelaInfoSubtle = mixOklab(darkColors.VelaInfo, 0.14, darkColors.VelaSurface, 0.86);
darkColors.VelaOverlayScrim = oklch(0.06, 0, 0, 0.62);
darkColors.VelaDangerMuted = mixOklab(darkColors.VelaDanger, 0.55, darkColors.VelaSurfaceMuted, 0.45);
darkColors.VelaChartOther = mixOklab(darkColors.VelaForeground, 0.26, darkColors.VelaSurface, 0.74);

const accents = [
  {
    name: "Neutral",
    light: {
      accent: oklch(0.33, 0, 0), accentStrong: oklch(0.24, 0, 0), accentSubtle: oklch(0.33, 0, 0, 0.08), accentFg: lightColors.VelaSurface,
      controlOn: oklch(0.23, 0, 0), controlThumbOn: oklch(1.0, 0, 0), controlGlyph: oklch(1.0, 0, 0), controlFill: oklch(0.23, 0, 0),
    },
    dark: {
      accent: oklch(0.80, 0, 0), accentStrong: oklch(0.91, 0, 0), accentSubtle: oklch(0.80, 0, 0, 0.13), accentFg: darkColors.VelaBackground,
      controlOn: oklch(0.93, 0, 0), controlThumbOn: oklch(0.19, 0, 0), controlGlyph: oklch(0.19, 0, 0), controlFill: oklch(0.62, 0, 0),
    },
  },
  {
    name: "Violet",
    light: {
      accent: hexColor("#7747ff"), accentStrong: hexColor("#5528ce"), accentSubtle: rgbaColor(119, 71, 255, 0.11), accentFg: lightColors.VelaSurface,
      controlOn: hexColor("#7747ff"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#7747ff"),
    },
    dark: {
      accent: hexColor("#a78bff"), accentStrong: hexColor("#c7b7ff"), accentSubtle: rgbaColor(119, 71, 255, 0.20), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#8a66ff"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#8a66ff"),
    },
  },
  {
    name: "Blue",
    light: {
      accent: hexColor("#0562ef"), accentStrong: hexColor("#074db7"), accentSubtle: rgbaColor(5, 98, 239, 0.10), accentFg: lightColors.VelaSurface,
      controlOn: hexColor("#0562ef"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#0562ef"),
    },
    dark: {
      accent: hexColor("#6ba3ff"), accentStrong: hexColor("#a4c7ff"), accentSubtle: rgbaColor(5, 98, 239, 0.20), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#3b82f6"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#3b82f6"),
    },
  },
  {
    name: "Green",
    light: {
      accent: hexColor("#0db879"), accentStrong: hexColor("#087c54"), accentSubtle: rgbaColor(13, 184, 121, 0.11), accentFg: hexColor("#102016"),
      controlOn: hexColor("#10a870"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#10a870"),
    },
    dark: {
      accent: hexColor("#53dca6"), accentStrong: hexColor("#93efd0"), accentSubtle: rgbaColor(13, 184, 121, 0.18), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#1fae78"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#1fae78"),
    },
  },
  {
    name: "Amber",
    light: {
      accent: hexColor("#f3ad20"), accentStrong: hexColor("#9c6300"), accentSubtle: rgbaColor(243, 173, 32, 0.14), accentFg: hexColor("#20190d"),
      controlOn: hexColor("#eda11a"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#2a1c03"), controlFill: hexColor("#eda11a"),
    },
    dark: {
      accent: hexColor("#ffcb62"), accentStrong: hexColor("#ffe0a1"), accentSubtle: rgbaColor(243, 173, 32, 0.18), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#e8a52a"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#241803"), controlFill: hexColor("#e8a52a"),
    },
  },
  {
    name: "Orange",
    light: {
      accent: hexColor("#f48120"), accentStrong: hexColor("#aa4700"), accentSubtle: rgbaColor(244, 129, 32, 0.11), accentFg: hexColor("#17130f"),
      controlOn: hexColor("#f07818"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#f07818"),
    },
    dark: {
      accent: hexColor("#ffad5e"), accentStrong: hexColor("#ffd0a0"), accentSubtle: rgbaColor(244, 129, 32, 0.18), accentFg: hexColor("#17130f"),
      controlOn: hexColor("#ef873a"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#ef873a"),
    },
  },
  {
    name: "Coral",
    light: {
      accent: hexColor("#f15f55"), accentStrong: hexColor("#b92c27"), accentSubtle: rgbaColor(241, 95, 85, 0.12), accentFg: hexColor("#21110f"),
      controlOn: hexColor("#ec564b"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#ec564b"),
    },
    dark: {
      accent: hexColor("#ff9386"), accentStrong: hexColor("#ffc1ba"), accentSubtle: rgbaColor(241, 95, 85, 0.18), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#ec6a60"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#ec6a60"),
    },
  },
  {
    name: "Rose",
    light: {
      accent: hexColor("#ed4e9d"), accentStrong: hexColor("#ae2570"), accentSubtle: rgbaColor(237, 78, 157, 0.12), accentFg: hexColor("#250f1b"),
      controlOn: hexColor("#e64494"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#e64494"),
    },
    dark: {
      accent: hexColor("#ff8bc5"), accentStrong: hexColor("#ffc0df"), accentSubtle: rgbaColor(237, 78, 157, 0.18), accentFg: darkColors.VelaBackground,
      controlOn: hexColor("#e457a0"), controlThumbOn: hexColor("#ffffff"), controlGlyph: hexColor("#ffffff"), controlFill: hexColor("#e457a0"),
    },
  },
];

// 1. Emit VelaDesignTokens.axaml
const designTokensAxaml = `<!-- Generated by tools/sync-tokens.mjs -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- Spacing scale -->
  <x:Double x:Key="VelaSpace1">4</x:Double>
  <x:Double x:Key="VelaSpace2">8</x:Double>
  <x:Double x:Key="VelaSpace3">12</x:Double>
  <x:Double x:Key="VelaSpace4">16</x:Double>
  <x:Double x:Key="VelaSpace5">20</x:Double>
  <x:Double x:Key="VelaSpace6">24</x:Double>
  <x:Double x:Key="VelaSpace8">32</x:Double>
  <x:Double x:Key="VelaSpace10">40</x:Double>
  <x:Double x:Key="VelaSpace12">48</x:Double>

  <!-- Corner radii -->
  <CornerRadius x:Key="VelaRadiusItem">12</CornerRadius>
  <CornerRadius x:Key="VelaRadiusControl">18</CornerRadius>
  <CornerRadius x:Key="VelaRadiusControlInner">14</CornerRadius>
  <CornerRadius x:Key="VelaRadiusPanel">24</CornerRadius>
  <CornerRadius x:Key="VelaRadiusSurface">32</CornerRadius>
  <CornerRadius x:Key="VelaRadiusPill">9999</CornerRadius>

  <!-- Control heights -->
  <x:Double x:Key="VelaControlHeightSm">36</x:Double>
  <x:Double x:Key="VelaControlHeightMd">44</x:Double>
  <x:Double x:Key="VelaControlHeightLg">50</x:Double>

  <!-- Typography scale -->
  <x:Double x:Key="VelaTextXs">12</x:Double>
  <x:Double x:Key="VelaTextSm">14</x:Double>
  <x:Double x:Key="VelaTextBase">16</x:Double>
  <x:Double x:Key="VelaTextLg">18</x:Double>
  <x:Double x:Key="VelaTextXl">22</x:Double>
  <x:Double x:Key="VelaText2xl">28</x:Double>
  <x:Double x:Key="VelaText3xl">36</x:Double>
</ResourceDictionary>
`;

// 2. Emit VelaColorTokens.axaml
function emitColorBlock(colors, isDark) {
  const lines = [];
  for (const [key, val] of Object.entries(colors)) {
    const hex = toAvaloniaHex(val);
    lines.push(`      <Color x:Key="${key}Color">${hex}</Color>`);
    lines.push(`      <SolidColorBrush x:Key="${key}Brush" Color="{StaticResource ${key}Color}" />`);
  }

  if (!isDark) {
    lines.push(`      <BoxShadows x:Key="VelaShadowResting">0 1 2 0 #09000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaShadowRaised">0 6 18 0 #11000000, 0 1 3 0 #09000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaShadowFloating">0 20 48 0 #1B000000, 0 3 10 0 #0B000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaControlThumbShadow">0 0 0 0.5 #12000000, 0 1 2 0 #24000000, 0 2 6 0 #0F000000</BoxShadows>`);
  } else {
    lines.push(`      <BoxShadows x:Key="VelaShadowResting">0 1 2 0 #2E000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaShadowRaised">0 8 22 0 #38000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaShadowFloating">0 22 55 0 #57000000</BoxShadows>`);
    lines.push(`      <BoxShadows x:Key="VelaControlThumbShadow">0 0 0 0.5 #4D000000, 0 1 2 0 #59000000, 0 2 6 0 #33000000</BoxShadows>`);
  }
  return lines.join("\n");
}

const colorTokensAxaml = `<!-- Generated by tools/sync-tokens.mjs -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ResourceDictionary.ThemeDictionaries>
    <ResourceDictionary x:Key="Light">
${emitColorBlock(lightColors, false)}
    </ResourceDictionary>
    <ResourceDictionary x:Key="Dark">
${emitColorBlock(darkColors, true)}
    </ResourceDictionary>
  </ResourceDictionary.ThemeDictionaries>
</ResourceDictionary>
`;

// 3. Emit VelaAccentPalettes.g.cs
function csColor(c) {
  return `Color.Parse("${toAvaloniaHex(c)}")`;
}
function emitPaletteRecord(def, isDark) {
  const s = deriveSeries(def.accent, isDark);
  return `new(
            Accent: ${csColor(def.accent)},
            AccentStrong: ${csColor(def.accentStrong)},
            AccentSubtle: ${csColor(def.accentSubtle)},
            AccentForeground: ${csColor(def.accentFg)},
            ControlOn: ${csColor(def.controlOn)},
            ControlThumbOn: ${csColor(def.controlThumbOn)},
            ControlGlyph: ${csColor(def.controlGlyph)},
            ControlFill: ${csColor(def.controlFill)},
            ControlOnSubtle: ${csColor(withAlpha(def.controlOn, 0.10))},
            Series1: ${csColor(s[0])},
            Series2: ${csColor(s[1])},
            Series3: ${csColor(s[2])},
            Series4: ${csColor(s[3])})`;
}

const accentPalettesCs = `// <auto-generated by tools/sync-tokens.mjs />
#nullable enable
using Avalonia.Media;

namespace Vela.Avalonia.Tokens;

public enum VelaAccent
{
${accents.map((a) => `    ${a.name},`).join("\n")}
}

public readonly record struct VelaAccentPalette(
    Color Accent,
    Color AccentStrong,
    Color AccentSubtle,
    Color AccentForeground,
    Color ControlOn,
    Color ControlThumbOn,
    Color ControlGlyph,
    Color ControlFill,
    Color ControlOnSubtle,
    Color Series1,
    Color Series2,
    Color Series3,
    Color Series4);

public static class VelaAccentPalettes
{
    public static VelaAccentPalette Get(VelaAccent accent, bool isDark) => (accent, isDark) switch
    {
${accents.map((a) => `        (VelaAccent.${a.name}, false) => ${emitPaletteRecord(a.light, false)},\n        (VelaAccent.${a.name}, true) => ${emitPaletteRecord(a.dark, true)},`).join("\n")}
        _ => Get(VelaAccent.Neutral, isDark),
    };
}
`;

fs.writeFileSync(path.join(tokensOutDir, "VelaDesignTokens.axaml"), designTokensAxaml);
fs.writeFileSync(path.join(tokensOutDir, "VelaColorTokens.axaml"), colorTokensAxaml);
fs.writeFileSync(path.join(tokensOutDir, "VelaAccentPalettes.g.cs"), accentPalettesCs);
console.log("Generated VelaDesignTokens.axaml, VelaColorTokens.axaml, VelaAccentPalettes.g.cs");
