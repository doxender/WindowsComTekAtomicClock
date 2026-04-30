# Changelog

All notable changes to ComTek Atomic Clock (Windows) are tracked here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). The patch number is bumped on every shipped change per the project's standing version-bump rule, with the problem and solution noted under the matching version header below.

## [0.0.11] - 2026-04-30

### Added

- **Live last-sync display in the status bar.** Replaces the `Last sync: (not yet wired — Step 6d)` placeholder that's been there since the early scaffold.
  - *Problem to solve:* Service has been syncing to NIST hourly since v0.0.x — the named-pipe IPC contract (`PipeNames.UiToService`, `IpcMessageType.LastSyncStatusRequest/Response`, `SyncStatus` payload), the Service-side handler (`LiveIpcRequestHandler` reads from `SyncStateProvider`), and the UI-side client (`Services/IpcClient`) all existed. The UI just wasn't asking. So users had no visible confirmation the service was doing its job until the system clock visibly corrected itself.
  - *Solution:* `MainWindowViewModel` now owns a 1 Hz `DispatcherTimer` plus a held `IpcClient`. Each tick re-formats `LastSyncText` from the cached `SyncStatus` (so the relative-time component "12s ago" updates live without re-querying the pipe). Every 5th tick (≈ 5 s) it ALSO refreshes the cached snapshot via `LastSyncStatusRequest`. Exceptions during the IPC round-trip are swallowed — pipe transients during service start/stop are common — and the connection is dropped so the next tick reconnects fresh. Status bar binds to `LastSyncText`.
  - *Format examples:*
    - `Last sync: just now (corrected −8.7 ms)` (success, sub-second drift; − sign means clock was pulled back)
    - `Last sync: 47m ago (corrected +2.3 s)` (success, multi-second drift; + means pushed forward)
    - `Last sync: pending…` (service running but no sync attempt yet)
    - `Last sync: service not running` (service stopped or not installed)
    - `Last sync failed: <error>` (last attempt errored; message truncated to 60 chars)
  - Files: `ViewModels/MainWindowViewModel.cs` (new `LastSyncText` property, `OnLastSyncTick`, `TryFetchLastSyncAsync`, `FormatLastSync`/`FormatAgo`/`FormatDrift` helpers, `_lastSyncTimer` + `_ipcClient` fields), `MainWindow.xaml` (replaced placeholder TextBlock).
  - Closes the Step-6d carry-over flagged in v0.0.x. Service-push notifications (the `ConfirmLargeOffsetRequest` toast flow per `requirements.txt` § 2.5) remain a separate follow-up — that needs a server-push read-loop on `IpcClient`, not just request/response polling.

## [0.0.10] - 2026-04-30

### Changed

- **Source-code comments depersonalized — replaced "Dan" mentions with "we" / "our".** No "Claude" mentions existed. Touched 13 comments across 7 files: `MainWindow.xaml` (3), `FloatingClockWindow.xaml` (1), `Controls/ClockFaceControl.xaml.cs` (6), `Dialogs/TabSettingsDialog.xaml.cs` (1), `Dialogs/ThemesDialog.xaml.cs` (1), `ViewModels/ThemeCatalog.cs` (1). Behavior unchanged. The user-facing copyright line in `AboutDialog.xaml` ("© 2026 Daniel V. Oxender. All rights reserved.") was intentionally preserved — it's display text, not a code comment, and the legal attribution belongs in product UI. CHANGELOG entries that reference Dan are also preserved as historical audit trail; the rule was about source comments.

## [0.0.9] - 2026-04-30

### Diagnostic build (in response to Dan's v0.0.7 bug reports)

- **Dan reported: "Tab 2 shows Flip Clock, but Binary Digital selected."** A theme→render mismatch where the persisted Theme value disagrees with what's actually painted. Code-read inspection couldn't isolate it. This build adds two probes / hardening steps so the next F5 produces actionable data.

### Changed

- **`OnThemeChanged` no longer guards on `IsLoaded`.** Earlier, if the `Theme` DP changed before the control's `Loaded` event fired (a small window during `DataTemplate` instantiation that's plausibly hit by `TabablzControl`'s tab-content lifecycle), the change was silently dropped and only the next `OnLoaded` picked up the value. That fits the "DP says BinaryDigital, visuals are FlipClock" symptom: tab in a state where binding has updated `Theme` but the render is from a stale earlier dispatch. Removed the gate; `RenderActiveTheme` now runs on every `Theme` change. The Canvas children are absolutely positioned via explicit Width/Height + `Canvas.SetLeft/SetTop`, so an early call (pre-layout-pass) is correct. (`Controls/ClockFaceControl.xaml.cs`.)

- **`theme: <name>` debug overlay TEMPORARILY re-enabled** on every face. Lets Dan see at a glance which theme `RenderActiveTheme` actually dispatched to. If the overlay says `theme: Binary Digital` but the visuals are Flip Clock, we know `BuildBinaryDigital` is at fault; if the overlay says `theme: Flip Clock`, the DP is the one out of sync. Once the mismatch is closed the overlay comes back off (commented or build-flagged). (`RenderActiveTheme` in `ClockFaceControl.xaml.cs`.)

### Open follow-ups still not addressed

- **Tab clicks remain hard to register first-try, especially on Flip Clock.** Width=0 + empty `Template` + `OverridesDefaultStyle=True` on the Thumb is the current state. The next escalation tier (`IsHitTestVisible=False`, `Visibility=Collapsed`) would risk regressing drag-to-tear, which depends on the Thumb being present in some Dragablz code paths. Holding off pending data from the diagnostic overlay above.
- **Flip Clock tab not showing city until tear-off** (carry-over from v0.0.8 follow-ups).

## [0.0.8] - 2026-04-29

### Changed

- **Flip Clock now shows day · month · day-of-month** beneath the seconds line (parity with the analog faces, which all carry a date readout). New `dateTb` is updated each tick from the bound timezone's `local.ToString("ddd · MMMM d")`. Layout shifts: seconds drops from y=276 → y=274; new date line at y=292; COMTEK badge moves from y=304 → y=312. (`Controls/ClockFaceControl.xaml.cs` `BuildFlipClock`.)

- **Hex theme — decimal-time decode line replaced with hex-ASCII encoding of day-of-week, day-of-month, and month name.**
  - *Problem:* Per Dan's spec, the Hex theme is meant to express *every* on-screen value in hex. The "// dec: HH:MM:SS" decode line was an off-theme decimal artefact.
  - *Solution:* Dropped that line. Added three lines showing the day-of-week (`THU`), day-of-month (`29`), and month name (`APRIL`) as space-separated 2-digit hex ASCII codes followed by the friendly form, e.g. `// dow: 54 48 55 (THU)`. The day-fraction line + color swatch are kept (renamed `// day:` → `// day-frac:` for disambiguation). New `ToHexAscii` helper.

- **Binary Digital theme — same treatment** as Hex.
  - *Problem:* Same as Hex — the "// dec: HH:MM:SS" line was off-theme decimal noise.
  - *Solution:* Dropped that line. Added three lines: day-of-week / day-of-month / 3-letter month name as space-separated 8-bit binary ASCII codes plus friendly form, e.g. `// dow: 01010100 01001000 01010101 (THU)`. Day-of-week and month abbreviated to 3 letters so the line lengths stay readable. New `ToBinAscii` helper.

### Open follow-ups (Dan flagged but not yet reproduced/fixed in this commit)

- **Flip Clock tab not showing city until tear-off.** The `MainWindow` ItemTemplate uses `<TextBlock Text="{Binding Label}"/>`, which is reactive on `PropertyChanged` and confirmed working on the analog themes. Theme changes don't affect `Label` so the binding shouldn't differ here. Defer until reproduction; may be a heavy-paint timing artefact on the Flip Clock first render.
- **Flip Clock tabs harder to click first-try than other themes.** Width=0 on the Thumb (v0.0.4 fix) is still in effect. Suspect the heavy first-paint of the Flip Clock canvas (~50+ shape elements) blocks the UI thread briefly and click events are dropped during that window. If reproduction confirms, fix is to move the build off the UI thread (or simplify the Flip Clock element count).

## [0.0.7] - 2026-04-29

### Added

- **Six digital theme renderers** — every `Theme` enum value now has its own `Build*` method; the `default:` fallback in `RenderActiveTheme` is gone. Selecting any of these now shows the right face instead of Atomic Lab visuals (which is what they all rendered as up through v0.0.6).
  - **Flip Clock** (`BuildFlipClock`) — brown nightstand backdrop, chrome legs, dark plastic case with inner well, four flip-tile cards each with two halves + hinge line + chrome spindle pegs, big black sans-serif digits for HH and MM, amber colon dots, ":SS SECONDS" line, "COMTEK · MODEL CT-1971" badge. Static today; the design's flip-card animation hook is a TODO follow-up.
  - **Marquee** (`BuildMarquee`) — outer red theater frame, inner black panel, 32 yellow incandescent chase bulbs around the perimeter (top + bottom rows of 9, left + right columns of 7, corner gaps), "★ NOW SHOWING ★" header, big glowing yellow HH:MM:SS, "★ ATOMIC TIME ★ FROM BOULDER ★" subtitle. Static today; the chase-bulb brightness wave is a TODO follow-up.
  - **Slab** (`BuildSlab`) — concrete-gradient backdrop, top + bottom black accent bars with a small red diagonal highlight, "ATOMIC · TIME · STD/DST" context strip, big slab-serif HH:MM, red 36-pt SS″ seconds, full date footer line.
  - **Binary** (`BuildBinary`) — pure-black backdrop, 6 columns of LED dots (BCD per column: 2 dots H-tens, 4 dots H-ones, 3 dots M-tens, 4 dots M-ones, 3 dots S-tens, 4 dots S-ones), bit-value labels (8/4/2/1) on the left, glow effect on lit LEDs, group labels HOURS/MINUTES/SECONDS, decoded "HH : MM : SS" readout, footer "read top→bottom · 8·4·2·1 BCD per column".
  - **Hex** (`BuildHex`) — terminal-style top bar with three traffic-light circles + window title, "// time encoded as hexadecimal (per unit)" comment, big cyan glowing 2-hex-digit-per-unit HH:MM:SS, decoded decimal line, day-fraction line ("// day: 0xNNNN / 0xFFFF (NN.N% elapsed)"), color swatch where the 16-bit day fraction maps to RGB `(R, G, 0xFF)`, descriptive caption, terminal cursor.
  - **Binary Digital** (`BuildBinaryDigital`) — magenta-on-purple terminal, three big lines for H (5b) / M (6b) / S (6b) MSB-first binary, decoded decimal, width annotation, two static decorative noise rows, terminal cursor.

### Changed

- **`RenderActiveTheme` switch is now exhaustive.** Every `Theme` value maps to a dedicated `Build*` method; if a future theme value is added without its case, the dial renders blank rather than silently falling back to Atomic Lab — visible failure beats invisible mis-attribution.
- **Per-tick update plumbing** generalized: new `Action<DateTime>? _digitalUpdater` field that each digital `Build*` populates with a closure mutating its own elements (digit text blocks, LED ellipses, color swatches). `UpdateClock` invokes it after the analog rotates. Reset in `RenderActiveTheme` when switching faces. Lets renderers without rotating hands hook into the same tick loop without bloating the shared field set.
- **`MakeText` helper** added — SVG-style baseline placement with Left / Center / Right anchor, used by every digital renderer for text positioning. Existing `MakeNumeral` (centers on x AND y) preserved for the analog renderers that already use it.

### Removed

- **Test-only `theme: <name>` debug overlay** is no longer painted on any face. All twelve renderers are now real, so the visibility aid that flagged unimplemented-theme fallbacks isn't needed. The `AddDebugThemeLabel` method is left in place (commented out at the call site) as a future debugging hook.

## [0.0.6] - 2026-04-29

### Removed

- **Test-only `theme: <name>` debug label hidden on all six analog clock faces** (Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight) now that Dan has verified each renderer matches its selection. The label was a visibility aid for catching theme→render mismatches; with the analog set confirmed, it's just clutter.
  - *Kept* on the six unimplemented digital themes (Flip Clock, Marquee, Slab, Binary, Hex, Binary Digital) — those still fall back to Atomic Lab visuals at runtime, and the label flags that mismatch so the gap stays obvious. Drop the `IsAnalogTheme` gate (or the `AddDebugThemeLabel` call entirely) once those six renderers ship and there's no fallback to flag.
  - Implementation: `RenderActiveTheme` now wraps the `AddDebugThemeLabel()` call in `if (!IsAnalogTheme(Theme))`. (`Controls/ClockFaceControl.xaml.cs`.)

## [0.0.5] - 2026-04-29

### Changed

- **Help dialog → "Tear off into its own window" entry** now leads with "works the same way as in Google Chrome" so users have an immediate mental model. Wording change only — the behavior is unchanged. (`Dialogs/HelpDialog.xaml`.)

### Verified

- **All six analog clock faces (Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight) tested by Dan and confirmed working correctly** under v0.0.4 — hands tracking the dial center, second hands not spiraling, X / ? overlay glyphs visible on every theme, tab header text refreshing live on timezone change, tab clicks first-try reliable. Marks the close of the `analog-cleanup` branch's stated scope; ready to merge to `master`.

## [0.0.4] - 2026-04-29

### Fixed

- **Tab header text didn't refresh when the user changed a tab's timezone via TabSettings.**
  - *Problem:* Picking "(UTC+03:00) Europe/Kiev · FLE Standard Time" from the dropdown updated `TabViewModel.TimeZoneId` and raised `PropertyChanged` for `Label`, but the Dragablz tab header kept showing the old "UTC" text. Tearing the tab off into a new floating window made the new label appear, because the new container re-added the item and re-evaluated `ToString()`. Root cause: `DisplayMemberPath="Label"` in this version of Dragablz captures the label once at item-add time and falls back to `object.ToString()`; it doesn't subscribe to `INotifyPropertyChanged`. The prior session had added `ToString() => Label` as the visible-string source — which is also captured once.
  - *Solution:* Removed `DisplayMemberPath` and added an explicit `ItemTemplate` with `<TextBlock Text="{Binding Label}"/>`. A standard WPF binding subscribes to `PropertyChanged` and refreshes the header live. Applied in both `MainWindow.xaml` and `FloatingClockWindow.xaml`.

- **Atomic Lab: ✕ and ? overlay glyphs disappeared.**
  - *Problem:* Atomic Lab was the only analog renderer that didn't paint a 400×400 backdrop — it drew only the bezel + face circles, leaving the corners outside the dial transparent. The hosting `TabablzControl`'s white background showed through. The new `OverlayGlyphBrush` correctly returned white on Atomic Lab (it's a dark-faced theme), but the corner area where the buttons sit was actually white — so white-glyph-on-white-corner = invisible.
  - *Solution:* Added a 400×400 `Rectangle` with the navy gradient at the start of `BuildAtomicLab`, matching the design SVG's `<rect width="400" height="400" fill="url(#bg-al)"/>` that we'd dropped in the WPF port. Now the corners are dark navy and the white glyphs read correctly. (`Controls/ClockFaceControl.xaml.cs`.)

- **Tab clicks unreliable — selecting a tab often took several attempts.**
  - *Problem:* The v0.0.2 yellow-block fix used `OverridesDefaultStyle=True` plus a `Template`-override-with-empty-Border. `OverridesDefaultStyle` removes the default style's `Width` setter — so the empty Thumb stretched to fill its grid cell in the DragablzItem template. Even though it painted nothing, it was still hit-testable across the whole tab and was consuming mouse-down events that should have routed to selection. Drag-detection on the Thumb was eating clicks meant for tab-select.
  - *Solution:* Kept the empty template (still kills the yellow paint and the active-tab dark inner box) but added explicit `Width=0` / `MinWidth=0` setters. Now the Thumb has zero hit-test area; clicks land on the DragablzItem and selection works first-try. Drag-to-tear continues to work because Dragablz's tear-away gesture hooks the DragablzItem's mouse-move, not the Thumb directly. Applied in both `MainWindow.xaml` and `FloatingClockWindow.xaml`.

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
