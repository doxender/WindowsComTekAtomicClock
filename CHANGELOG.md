# Changelog

All notable changes to ComTek Atomic Clock (Windows) are tracked here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). The patch number is bumped on every shipped change per the project's standing version-bump rule, with the problem and solution noted under the matching version header below.

## [0.0.3] - 2026-04-29

### Fixed

- **X (close) and ? (help) overlay glyphs invisible on dark themes (Cathode, Atomic Lab, Aero Glass, Concourse, Marquee, Slab, Binary, Hex, Binary Digital).**
  - *Problem:* Glyph foregrounds were hardcoded `#000`. On every theme with a dark backdrop the buttons disappeared (black-on-black on Cathode, black-on-navy on Atomic Lab, etc.). Dan flagged it on Cathode where the contrast is starkest.
  - *Solution:* Added `OverlayGlyphBrush` to `TabViewModel` — returns near-black on light themes (Boulder Slate, Daylight, Flip Clock) and white on every dark theme. Bound the X and ? `Foreground` to it. The brush re-evaluates whenever `Theme` changes (`PropertyChanged` is raised on the Theme setter). (`ViewModels/TabViewModel.cs`, `MainWindow.xaml`, `FloatingClockWindow.xaml`.)

- **X and ? buttons floated way out to the right edge of the window on landscape windows.**
  - *Problem:* Buttons sat in the tab-content `Grid` with `Margin="0,28,28,0"` — anchored 28 px from the *tab content* right edge, not the dial. When the window was wider than the clock face, the dial centered itself in the available space and the buttons drifted to the far-right window margin, disconnected from the clock visually.
  - *Solution:* Wrapped `ClockFaceControl` + the two overlay buttons in a 400×400 `Grid` inside a uniform-stretching `Viewbox`. The 400×400 frame matches the design SVGs and the inner `Canvas`. Buttons now sit at `Margin="0,14,16,0"` and `Margin="0,42,16,0"` *within the dial frame* — they stay anchored to the dial's upper-right corner and scale with it regardless of window aspect or size. (`MainWindow.xaml`, `FloatingClockWindow.xaml`.)

- **Floating windows lost the X and ? buttons after a tear-off.**
  - *Problem:* `FloatingClockWindow.xaml`'s `DataTemplate` only had the `ClockFaceControl` — no overlay buttons at all. Tear a tab off and you couldn't close it (other than via window close) or reach the Themes / Help / About menu.
  - *Solution:* Mirrored the new `MainWindow` template into the floating window's `DataTemplate`, plus the corresponding click handlers in `FloatingClockWindow.xaml.cs`. Themes… re-dispatches through the main VM (`MainWindow.GetViewModel().OpenThemesPickerForCommand`) so the `SettingsStore.SaveAppSettings` path stays single-sourced — the floating window doesn't own the `AppSettings` instance. (`FloatingClockWindow.xaml`, `FloatingClockWindow.xaml.cs`, `MainWindow.xaml.cs`.)

### Added

- **`MainWindow.GetViewModel()`** — internal accessor so sibling windows (notably `FloatingClockWindow`) can dispatch to the main VM without exposing the VM publicly.

### Notes

- Branch `analog-cleanup`. Per-theme visual fidelity audit (each WPF render vs its `design/themes/*.svg`) is queued as a follow-up — Dan to specify scope (renderer-by-renderer fixes vs broader refactor of the duplicated digital-readout panel code).

## [0.0.2] - 2026-04-29

### Fixed

- **Boulder Slate: red second-hand tip disc orbiting a far-off point and spiraling out of frame.**
  - *Problem:* The line and the tip disc shared a single `RotateTransform(0, Cx, Cy)`. `RotateTransform.Center` is interpreted in *each element's own local coord space*, not the parent canvas. The Line uses absolute `X1/Y1/X2/Y2` so its local frame happens to match the dial canvas — `(Cx, Cy)` worked there. The Ellipse, positioned via `Canvas.SetLeft/SetTop`, has its local origin at its top-left, so `(Cx, Cy)` in the disc's frame meant rotating around canvas point `(386, 268)` — far off in the lower-right. The disc orbited that off-canvas point in a wide spiral.
  - *Solution:* Wrap the line + disc in a dedicated 400×400 sub-`Canvas` whose own local frame matches the dial's. One `RotateTransform(0, Cx, Cy)` on the host rotates both children together. (`Controls/ClockFaceControl.xaml.cs` `BuildBoulderSlate`.)

- **Tab strip: yellow blocks left of each tab name and the dark-gray inner highlight on the active tab.**
  - *Problem:* The previous fix tried `Style.Resources` → implicit `Style TargetType="Thumb"` with `Width=0` and transparent brushes. The yellow paint persisted, and the active tab gained a dark inner box — both rendered by the Dragablz default Thumb template using its own brush references that our `Background`/`Width` setters didn't reach. (Yellow when inactive, gray when selected.)
  - *Solution:* Override the Thumb's `Template` property itself with an empty transparent `Border` and set `OverridesDefaultStyle=True`. That bypasses whatever brushes the default template references — there's nothing left to paint. Natural `Width` is preserved so Dragablz's drag-to-tear hit-testing still routes through the Thumb element. (`MainWindow.xaml`, `FloatingClockWindow.xaml`.)

### Added

- **Clock face: assembly version painted upper-left of the dial canvas** (small gray monospace text). Source is `Assembly.GetName().Version` so the painted string can never disagree with the version `csproj` carries. Per Dan's request — at-a-glance identification of which build is running on the desktop. (`Controls/ClockFaceControl.xaml.cs` `AddVersionLabel`.)
- **Clock face: test-only theme-name overlay below the digital readout** (small gray text, e.g. `theme: Boulder Slate`). Six of the twelve themes still fall back to Atomic Lab visuals; with this label visible the mismatch is obvious (label says "Slab", visual is Atomic Lab) so we can audit and prioritise the missing renderers. **TODO:** remove before public release. (`Controls/ClockFaceControl.xaml.cs` `AddDebugThemeLabel`.)
- **Project version field.** `<Version>0.0.2</Version>` (plus `AssemblyVersion`/`FileVersion`) added to `ComTekAtomicClock.UI.csproj`. The standing version-bump rule will tick the patch number on every subsequent change.

### Known limitations carried forward

- Six of twelve themes (**Flip Clock**, **Marquee**, **Slab**, **Binary**, **Hex**, **Binary Digital**) still fall back to Atomic Lab visuals when selected. Settings persistence is correct; only the per-theme renderer is missing.
- Live last-sync display in the status bar isn't wired (Step 6d).
- Floating-window position/size persistence isn't implemented yet.

## [0.0.1] - 2026-04-28

Initial alpha (retroactive version assignment). Prior to this release no version field was tracked in source — `0.0.1` is assigned to the state immediately before the standing version-bump rule was adopted.

### Added

- Four-project .NET 8 LTS solution: `ComTekAtomicClock.UI` (WPF), `ComTekAtomicClock.Service` (Worker Service syncing to NIST Boulder via SNTP), `ComTekAtomicClock.Shared` (class library), `ComTekAtomicClock.ServiceInstaller` (privileged helper).
- Wpf.Ui Fluent chrome (FluentWindow, TitleBar, Mica backdrop) and Dragablz Chrome-style tabs with tear-away into floating windows.
- Six of twelve theme renderers in WPF: Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight.
- §1.9 banner prompting service install/start; status bar showing service state.
- Per-tab settings dialog (timezone + theme), Help dialog, About dialog with uninstall link.
- In-app **Themes** gallery dialog (3×4 SVG tile grid, click to apply) reachable from each tab's `?` overlay.
