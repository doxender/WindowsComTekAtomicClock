# Design — ComTek Atomic Clock (Windows)

This directory holds the visual-design artifacts for the app: theme proposals, mock-ups, and (eventually) iconography and motion specs. The code that consumes these designs lives in `../src/`.

## Themes

Twelve face theme proposals live under [`themes/`](themes/) — six analog (each with an integrated digital readout that can be hidden), four digital-only, and two specialty encodings (BCD-dot binary and hexadecimal). Each is a self-contained SVG. Open [`themes/index.html`](themes/index.html) in any browser for the side-by-side gallery view.

Every theme — regardless of category — exposes the same five user-overridable color slots: **Ring**, **Face**, **Hands**, **Numbers**, **Digital**. See [Per-theme color customization](#per-theme-color-customization) below for the slot-to-element mapping each theme implements.

Every preview is rendered at the same fixed time — **10:08:42** — so the eye can compare them without distraction. (10:08 is the watch-photography standard: hands form a near-symmetric "smile" that frames the brand or numerals; the 42-second hand pulls slightly into the lower-left so it doesn't visually overlap with either main hand.)

| # | Theme | Mood | Encoding |
|---|---|---|---|
| 1 | [Atomic Lab](themes/atomic-lab.svg) | Instrument-grade, project-thematic — **default on first run** | Analog + integrated digital |
| 2 | [Boulder Slate](themes/boulder-slate.svg) | Swiss minimalism, neutral | Analog + integrated digital |
| 3 | [Aero Glass](themes/aero-glass.svg) | Win11 Fluent acrylic, overlay-friendly | Analog + integrated digital |
| 4 | [Cathode](themes/cathode.svg) | Retro CRT phosphor, playful | Analog + integrated digital |
| 5 | [Concourse](themes/concourse.svg) | Departure-board, glanceable | Analog + integrated digital |
| 6 | [Daylight](themes/daylight.svg) | High-contrast, accessibility-first | Analog + integrated digital |
| 7 | [Flip Clock](themes/flip-clock.svg) | Retro 1970s bedroom mechanical | Digital-only (HH:MM big, seconds small) |
| 8 | [Marquee](themes/marquee.svg) | Theater chase bulbs, incandescent | Digital-only |
| 9 | [Slab](themes/slab.svg) | Brutalist concrete, architectural | Digital-only |
| 10 | [Binary](themes/binary.svg) | BCD LED stack, programmer-toy | Binary (BCD per digit) |
| 11 | [Hex](themes/hex.svg) | Programmer terminal, hexadecimal + day-fraction color | Hexadecimal |
| 12 | [Binary Digital](themes/binary-digital.svg) | Pure binary text — H/M/S as 5b/6b/6b binary, magenta terminal | Binary text (digital-only) |

For analog themes (1–6) the integrated digital readout can be hidden per window via a toggle in the tab/window settings, leaving a pure analog face. Themes 7–12 are inherently digital and the toggle is hidden.

### Analog second-hand motion defaults

Per `requirements.txt` § 1.1, each analog theme picks a default cadence for the second hand. The user can override per tab or per window via a `Theme default / Smooth / Stepped` selector in the settings popover.

| # | Theme | Default | Rationale |
|---|---|---|---|
| 1 | Atomic Lab | Smooth | Sub-second sweep matches the NIST-instrument identity |
| 2 | Boulder Slate | Stepped | Faithful to the Mondaine SBB railway-clock cadence |
| 3 | Aero Glass | Smooth | Win11 Fluent motion language is continuous |
| 4 | Cathode | Smooth | Phosphor persistence-of-vision suggests a sweep |
| 5 | Concourse | Smooth | Departure-board / station-clock electromechanical sweep |
| 6 | Daylight | Stepped | Mechanical office wall-clock cadence; emphasizes accessibility (each tick is a discrete event) |

### Theme rationale, in detail

**1. Atomic Lab.** This is the theme that earns the product name. The amber LCD readout, brushed-silver bezel, and `NIST · BOULDER · CO` legend make explicit the connection to the time source the service is talking to. Best as the default if the audience is technical.

**2. Boulder Slate.** A direct nod to the SBB / Mondaine railway clock — arguably the most-recognized clock face in the world. Pure analog. The single-color second hand with the disc at the tip is its signature; the disc represents the railway stop signal that briefly halts at the 12-position before each minute. Best as the default if the audience is broad.

**3. Aero Glass.** Designed specifically for the desktop overlay mode (requirement §1.4). Uses a translucent acrylic disc rather than a solid face so the user's wallpaper shows through subtly. The cyan second hand picks up from Windows 11's accent-color palette and reads cleanly against most wallpapers. The drop shadow lifts the disc visually so it's clearly an applied widget rather than a wallpaper element.

**4. Cathode.** A loving simulation of an old green-phosphor CRT. The Gaussian-blur glow on every stroke, plus the subtle scanline overlay, give it that VFD/oscilloscope feel. Not for everyone, but every clock app benefits from one fun face the user can choose for personality.

**5. Concourse.** Inspired by the orange dot-matrix departure boards in airport and train terminals. Every numeral is shown at every hour position to maximize at-a-glance readability. Best when the clock window is shrunk to a small corner widget — large numerals stay legible at small sizes.

**6. Daylight.** Designed for outdoor / high-ambient-light environments (sunlit office, near a south-facing window) and for users with low vision. The navy-on-cream palette gives ~12:1 contrast ratio, well above WCAG AAA for non-text. Numerals at every hour position, no thin strokes anywhere.

**7. Flip Clock.** A loving copy of the 1970s Twemco / Solari / Copal flip clocks that sat on every nightstand. White tiles with bold black sans-serif digits, the iconic horizontal hinge line through each card, chrome spindle pegs piercing the axis on each side, and — most importantly — visible stacked-tile edges along the left and right of each card so it reads as a physical pile, not a flat rectangle. A thin sliver of the next tile peeks above the front face (the one about to flip down) and the just-flipped tile peeks below. Amber colon pips. HH:MM are the headline; seconds are de-emphasized as a thin label so they don't compete with the big cards. <code>COMTEK · MODEL CT-1971</code> badge on the black plastic case, chrome legs underneath. Animation hook: at runtime, when a digit changes, the card should "flip" — top half rotates down 90deg revealing the new digit's top, bottom half follows. Two distinct sub-animations per minute change.

**8. Marquee.** Old movie theater vibe — yellow incandescent chase bulbs framing a deep red theater border, time spelled out big in glowing serif inside the frame. The `★ NOW SHOWING ★` / `★ ATOMIC TIME ★ FROM BOULDER ★` copy plays with the idea that "right now" is the headline act. A second-monitor decorative theme more than a daily-glance one. Animation hook: bulbs travel a left-to-right brightness wave, like a real theater.

**9. Slab.** Brutalist Swiss-station / Olivetti vibe — concrete-grey gritty backdrop, oversized black slab-serif HH:MM, single red rule for accent. Inspired by mid-century European station signage and Olivetti / Knoll graphic systems. Strong typographic personality; reads at a glance even from far away despite being digital-only.

**10. Binary.** Six columns of BCD-encoded LED dots: H tens (2 dots, max 2), H ones (4 dots), M tens (3 dots, max 5), M ones (4 dots), S tens (3 dots, max 5), S ones (4 dots). Read top-to-bottom as bit values 8·4·2·1; missing dots in a column mean those bits don't exist for that digit's range. A decoded `10 : 08 : 42` sits below for catch-up. The most "explicitly programmer-fan-club" of the themes. Animation hook: a flicker on each dot that toggles, plus a subtle glow ramp.

**11. Hex.** Two hex encodings on the same screen: each unit (HH, MM, SS) shown as a hex pair `0A:08:2A` with decimal underneath, AND the elapsed fraction of today shown as a 16-bit hex value `0x6C38 / 0xFFFF` which doubles as a literal `#RRGGBB` color swatch. The bar IS today, encoded — it drifts gradually through the spectrum across 24 hours. Programmer terminal chrome (red/yellow/green traffic-light dots in the title bar, blinking cursor) makes it feel like a tiny TUI app.

**12. Binary Digital.** Pure binary text — three labeled rows showing hours, minutes, and seconds as their binary representations (5 bits, 6 bits, 6 bits, MSB first). Decimal decode and a bit-width annotation underneath; faint random-looking binary strings in the lower third for texture; same terminal chrome as the Hex theme but with a magenta-on-deep-purple palette to differentiate. Pairs with theme #10 (Binary, the BCD-dots encoding face) for users who want the binary read-out as text without the BCD dot grid.

## Per-theme color customization

Every theme exposes the same five user-overridable color slots: **Ring**, **Face**, **Hands**, **Numbers**, and **Digital**. The settings UI shows the same five color pickers regardless of which theme is active, and each theme's renderer maps the slots to the closest visual element it has. Settings live per tab and per free-floating window (see `requirements.txt` § 1.1, § 1.2, § 1.8).

| # | Theme | Ring | Face | Hands | Numbers | Digital |
|---|---|---|---|---|---|---|
| 1 | Atomic Lab | brushed silver bezel | navy dial | white hour/min hands | amber 12·3·6·9 numerals | amber LCD readout |
| 2 | Boulder Slate | black bezel | white dial | black baton hands | tick-mark color (no numerals on this theme) | small black digital text below |
| 3 | Aero Glass | dark grey ring outline | translucent acrylic | white hands | white 12·3·6·9 numerals | white digital pill |
| 4 | Cathode | dark phosphor outline | black CRT | green hands (glow) | green numerals | green LCD readout |
| 5 | Concourse | dark grey ring | charcoal dial | orange hands | orange 1–12 numerals | orange dot-matrix readout |
| 6 | Daylight | medium grey ring | cream dial | navy hands | navy 1–12 numerals | navy digital text |
| 7 | Flip Clock | black case + chrome legs | white tile faces | hinge / spindle hardware | black big digits | small `42 SECONDS` footer |
| 8 | Marquee | red theater frame | black inner panel | bulb glow color | yellow time text | yellow header / subtitle text |
| 9 | Slab | top + bottom black accent bars | concrete gritty backdrop | red accent rule | black big HH:MM | small footer caption |
| 10 | Binary | dark border | black field | lit-LED dot color | decoded readout digits | bit-width labels (HOURS / MINUTES / SECONDS) |
| 11 | Hex | terminal chrome bar | dark navy field | secondary cyan accents (cursor, prompt) | primary hex digits | decimal-decode + day-fraction text |
| 12 | Binary Digital | terminal chrome bar | deep purple field | secondary magenta accents (cursor, prompt) | primary big binary digits | row labels + decimal-decode text |

**Theme-specific accents are NOT user-overridable.** Boulder Slate's red second-hand disc stays red. Atomic Lab's `NIST · BOULDER · CO` legend stays amber. Flip Clock's amber colon pips stay amber. Marquee's red theater frame is part of its identity (the "Ring" slot controls it, but the *redness* is what makes it a marquee — letting users tint it sky-blue would break the theme; the slot is honored but the theme will document a recommended hue range in its `ResourceDictionary`). The renderer's job is to apply the user's chosen colors while preserving the theme's visual cohesion.

A "Reset colors to theme defaults" link in the settings popover restores all five slots for the active theme.

## Why SVG and not PNG/JPG

- **Editable as text.** The theme designer can adjust a color hex code or a stroke width with any text editor. No round-trip through Photoshop.
- **Version-controllable diffs.** A two-color tweak shows up as a clean two-line diff in `git`, not as a binary blob.
- **Resolution-independent.** The same source renders crisp on a 4K monitor and on a HiDPI laptop. We never bake in a pixel size we'll regret.
- **Consumable by WPF.** WPF can render SVG directly via the [`Svg.Skia`](https://github.com/wieslawsoltes/Svg.Skia) NuGet package, or we can hand-translate each SVG into a XAML `ResourceDictionary` (the more native approach). Either path keeps the SVGs as the design source of truth.

## How themes will plug into the code

Planned mapping (subject to refinement when we get to the UI project):

```
src/ComTekAtomicClock.UI/Themes/
  AtomicLab.xaml         <-- ResourceDictionary, mirrors atomic-lab.svg (DEFAULT)
  BoulderSlate.xaml
  AeroGlass.xaml
  Cathode.xaml
  Concourse.xaml
  Daylight.xaml
  FlipClock.xaml         <-- digital-only, animated card flip on digit change
  Marquee.xaml           <-- digital-only, animated bulb chase
  Slab.xaml              <-- digital-only
  Binary.xaml            <-- BCD encoding face
  Hex.xaml               <-- hexadecimal encoding face
  BinaryDigital.xaml     <-- digital-only, binary text display (pairs with Binary)
  ThemeManager.cs        <-- runtime theme switcher; persists per-tab + per-window
                              including the five color overrides per active theme
```

Each `.xaml` defines color brushes, font families, and shape templates that the `ClockFaceControl` consumes. Switching themes at runtime is a `ResourceDictionary` swap; no window recreate.

## Adding a new theme

1. Author the SVG under `themes/<name>.svg`. Use 400x400 viewBox, center at (200, 200), bezel outer radius 172, inner radius 160. Set the fixed time to 10:08:42 (hour-hand angle 304.35deg, minute-hand 52.2deg, second-hand 252deg).
2. Add a tile to `themes/index.html` (description + 3-4 swatch hex codes).
3. Add a row to the table in this README.
4. When wiring up to code, mirror the SVG colors and shapes into `src/ComTekAtomicClock.UI/Themes/<Name>.xaml`.

## Out of scope for this directory

Iconography (tray icon, app icon at 16/32/48/256), splash, and motion design will land in sibling subdirectories (`design/icons/`, `design/motion/`) once we get to those. The themes here describe the clock face only.
