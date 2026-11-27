# Vela identity assets

The mark combines two swept, sail-like planes into an open V, with restrained glass shading in violet and ice blue on an ink-colored tile.

## Files

| File | Use |
| --- | --- |
| `vela-icon-source.png` | Original generated artwork, 1254 × 1254, with alpha |
| `vela-icon.png` | 256 × 256 PNG for README, NuGet, and Gallery |
| `vela-icon.ico` | Windows application icon, containing the 256 × 256 PNG |

The browser's `wwwroot/vela-icon.png` is a copy of the 256 px asset; `wwwroot/favicon.png` is a 32 px derivative. The desktop Gallery embeds the shared PNG through a linked Avalonia resource.

The icon was generated with the built-in image generation tool. The original is preserved. Delivery sizes use macOS `sips`; the ICO wraps the PNG without changing its pixels. These assets follow the repository's [MIT license](../../LICENSE).

## Generation prompt

```text
Use case: logo-brand.
Asset type: production project icon for Vela.Avalonia, an Avalonia UI component library focused on spring motion, refined themes and glass surfaces. It will be used at the top of its GitHub README, as a NuGet package icon and in its component gallery.
Primary request: create ONE distinctive, beautifully restrained app icon. A bold open V monogram formed from two smooth swept sail-like planes; the left plane is shorter and bends inward, the right plane is taller and rises with a gentle taper. A crisp negative-space channel separates the planes. Its silhouette should communicate controlled motion and remain recognizable at 32px.
Composition: one centered icon, straight-on, square canvas, no perspective tilt. A deep ink rounded-square tile occupies about 88 percent of the canvas. The V fills about 65 percent of the tile. Transparent pixels outside the tile.
Style: precise contemporary software identity; vector-like broad geometric surfaces, a small amount of optical-glass depth and finely restrained edge light. Clean, intentional, premium, legible. No photographic scene.
Palette: the V uses cool periwinkle-violet and pale ice-blue, drawing on the project's Violet theme; deep near-black navy tile. Keep the contrast strong.
Constraints: only this single V mark on its tile, no words, no letters besides the V-shaped abstract symbol, no caption, no device mockup, no watermark. No stars, sparkles, orbit rings, extra emblems, tiny decoration, excessive glow, floating fragments, or drop shadow outside the tile. Actual transparent background.
```

## Regenerate delivery sizes on macOS

Run from the repository root:

```bash
sips -z 256 256 assets/branding/vela-icon-source.png --out assets/branding/vela-icon.png
sips -z 32 32 assets/branding/vela-icon-source.png --out samples/Vela.Avalonia.Gallery.Browser/wwwroot/favicon.png
cp assets/branding/vela-icon.png samples/Vela.Avalonia.Gallery.Browser/wwwroot/vela-icon.png
```

Wrap the 256 px PNG in an ICO container:

```javascript
// Run with: node --input-type=module
import fs from 'node:fs';

const png = fs.readFileSync('assets/branding/vela-icon.png');
const header = Buffer.alloc(22);
header.writeUInt16LE(1, 2); // ICO type
header.writeUInt16LE(1, 4); // One image
// Width and height bytes stay 0: the ICO encoding for 256 px.
header.writeUInt16LE(1, 10); // Color planes
header.writeUInt16LE(32, 12); // Bits per pixel
header.writeUInt32LE(png.length, 14);
header.writeUInt32LE(22, 18); // Image data offset
fs.writeFileSync('assets/branding/vela-icon.ico', Buffer.concat([header, png]));
```

