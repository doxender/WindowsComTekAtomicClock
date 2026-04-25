# Design — ComTek Atomic Clock (Windows)

This directory holds the visual-design artifacts for the app: theme proposals, mock-ups, and (eventually) iconography and motion specs. The code that consumes these designs lives in `../src/`.

## Themes

Six theme proposals live under [`themes/`](themes/). Each is a self-contained SVG showing both the analog face and the digital readout in that theme. Open [`themes/index.html`](themes/index.html) in any browser for a side-by-side gallery view.

Every preview is rendered at the same fixed time — **10:08:42** — so the eye can compare them without distraction. (10:08 is the watch-photography standard: hands form a near-symmetric "smile" that frames the brand or numerals; the 42-second hand pulls slightly into the lower-left so it doesn't visually overlap with either main hand.)

| # | Theme | Mood | Categorization |
|---|---|---|---|
| 1 | [Atomic Lab](themes/atomic-lab.svg) | Instrument-grade, project-thematic | Dark |
| 2 | [Boulder Slate](themes/boulder-slate.svg) | Swiss minimalism, neutral default | Light |
| 3 | [Aero Glass](themes/aero-glass.svg) | Win11 Fluent acrylic, overlay-friendly | Mid (translucent) |
| 4 | [Cathode](themes/cathode.svg) | Retro CRT phosphor, playful | Dark |
| 5 | [Concourse](themes/concourse.svg) | Departure-board, glanceable | Dark |
| 6 | [Daylight](themes/daylight.svg) | High-contrast, accessibility-first | Light |

### Theme rationale, in detail

**1. Atomic Lab.** This is the theme that earns the product name. The amber LCD readout, brushed-silver bezel, and `NIST · BOULDER · CO` legend make explicit the connection to the time source the service is talking to. Best as the default if the audience is technical.

**2. Boulder Slate.** A direct nod to the SBB / Mondaine railway clock — arguably the most-recognized clock face in the world. Pure analog. The single-color second hand with the disc at the tip is its signature; the disc represents the railway stop signal that briefly halts at the 12-position before each minute. Best as the default if the audience is broad.

**3. Aero Glass.** Designed specifically for the desktop overlay mode (requirement §1.4). Uses a translucent acrylic disc rather than a solid face so the user's wallpaper shows through subtly. The cyan second hand picks up from Windows 11's accent-color palette and reads cleanly against most wallpapers. The drop shadow lifts the disc visually so it's clearly an applied widget rather than a wallpaper element.

**4. Cathode.** A loving simulation of an old green-phosphor CRT. The Gaussian-blur glow on every stroke, plus the subtle scanline overlay, give it that VFD/oscilloscope feel. Not for everyone, but every clock app benefits from one fun face the user can choose for personality.

**5. Concourse.** Inspired by the orange dot-matrix departure boards in airport and train terminals. Every numeral is shown at every hour position to maximize at-a-glance readability. Best when the clock window is shrunk to a small corner widget — large numerals stay legible at small sizes.

**6. Daylight.** Designed for outdoor / high-ambient-light environments (sunlit office, near a south-facing window) and for users with low vision. The navy-on-cream palette gives ~12:1 contrast ratio, well above WCAG AAA for non-text. Numerals at every hour position, no thin strokes anywhere.

## Why SVG and not PNG/JPG

- **Editable as text.** The theme designer can adjust a color hex code or a stroke width with any text editor. No round-trip through Photoshop.
- **Version-controllable diffs.** A two-color tweak shows up as a clean two-line diff in `git`, not as a binary blob.
- **Resolution-independent.** The same source renders crisp on a 4K monitor and on a HiDPI laptop. We never bake in a pixel size we'll regret.
- **Consumable by WPF.** WPF can render SVG directly via the [`Svg.Skia`](https://github.com/wieslawsoltes/Svg.Skia) NuGet package, or we can hand-translate each SVG into a XAML `ResourceDictionary` (the more native approach). Either path keeps the SVGs as the design source of truth.

## How themes will plug into the code

Planned mapping (subject to refinement when we get to the UI project):

```
src/ComTekAtomicClock.UI/Themes/
  AtomicLab.xaml         <-- ResourceDictionary, mirrors atomic-lab.svg
  BoulderSlate.xaml
  AeroGlass.xaml
  Cathode.xaml
  Concourse.xaml
  Daylight.xaml
  ThemeManager.cs        <-- runtime theme switcher; persists per-window
```

Each `.xaml` defines color brushes, font families, and shape templates that the `ClockFaceControl` consumes. Switching themes at runtime is a `ResourceDictionary` swap; no window recreate.

## Adding a new theme

1. Author the SVG under `themes/<name>.svg`. Use 400x400 viewBox, center at (200, 200), bezel outer radius 172, inner radius 160. Set the fixed time to 10:08:42 (hour-hand angle 304.35deg, minute-hand 52.2deg, second-hand 252deg).
2. Add a tile to `themes/index.html` (description + 3-4 swatch hex codes).
3. Add a row to the table in this README.
4. When wiring up to code, mirror the SVG colors and shapes into `src/ComTekAtomicClock.UI/Themes/<Name>.xaml`.

## Out of scope for this directory

Iconography (tray icon, app icon at 16/32/48/256), splash, and motion design will land in sibling subdirectories (`design/icons/`, `design/motion/`) once we get to those. The themes here describe the clock face only.
