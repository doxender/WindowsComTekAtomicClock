# Changelog

All notable changes to ComTek Atomic Clock (Windows) are tracked here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). The patch number is bumped on every shipped change per the project's standing version-bump rule, with the problem and solution noted under the matching version header below.

## [1.1.6] - 2026-05-03 — Installer: wipe persisted settings on install (factory-reset every Setup.exe)

Per Dan: *"the defaults for the clock should reset on each install."* Two issues sat behind this:

1. **CaptJohn was opening with Hora Chapín ON** even though the code default is `false` (regular time). Cause: a prior test install had toggled it on, the toggle persisted to `%APPDATA%\ComTekAtomicClock\settings.json`, and every subsequent Setup.exe read that persisted `true` back. The bug was in the persistence flow, not the default.
2. **No standing way to "start clean"** between test installs. Forcing testers (Dan) to manually delete the JSON files between runs was friction.

### Change

`windows/tools/installer.iss` gains an `[InstallDelete]` section that runs at install time (before the new build's files are copied) and removes:

- `%APPDATA%\ComTekAtomicClock\settings.json` — the per-user UI state (tabs, themes, time format, second-hand override, CaptJohn Hora Chapín toggle, …).
- `%ProgramData%\ComTekAtomicClock\service.json` — the per-machine service config (time-source pool, sync interval, large-offset confirmation).

Both files are recreated at first launch with the current code's baked-in defaults — `Theme.AtomicLab`, `TimeSource.Boulder`, `CaptJohnHoraChapin = false`, etc. The CaptJohn theme therefore opens with `Hora Chapín OFF` (regular time) on every fresh install.

Note: the installed tree under `%ProgramFiles%\ComTekAtomicClock\…` is **not** touched by this — `InstallDelete` runs before the `Files` section copies the new build, and the wipe only targets user-data files outside the install tree.

### Tradeoff

End users lose any per-tab customization (their list of tabs, theme picks, time-zone tabs) on every Setup.exe upgrade. For an alpha / beta in active iteration this is the right call — testers always see what new defaults look like. Worth revisiting before a public-distribution v1.x release; at that point we'd narrow the scope to migration-incompatible schema changes only, or add a "keep my settings" checkbox on the install wizard.

### Files touched

- `windows/tools/installer.iss` — new `[InstallDelete]` section; `MyAppVersion` 1.1.5 → 1.1.6.
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.5 → 1.1.6.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.
- `windows/SPEC.md` v2.6 → v2.7: front-matter changelog row + a new bullet under §20 install flow noting the wipe-on-install behavior.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.6-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped.

## [1.1.5] - 2026-05-03 — CaptJohn: Hora Chapín jitter syncs to real time on the hour AND half hour

Per Dan: *"in 'hora Chapin' mode, the minut hand should sync to the current time on the hour and half hour."*

The v1.1.4 sync rule was "only at noon" (`local.Hour == 12 && local.Minute == 0`) — between syncs the lazy hand could drift very far from reality. v1.1.5 narrows the gap by syncing twice per hour: at `:00` and at `:30`. On those minute ticks the jitter snaps to the actual minute (0 or 30); on every other minute tick the walk takes a normal ±3 random step.

Net effect: the lazy bar-clock hand wanders noticeably between syncs but never drifts more than ~ ±15 minutes from real time before it's pulled back. Reads as "approximately the right time, with character" rather than "totally unmoored."

### Files touched

- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — `_digitalUpdater` jitter sync condition: `local.Hour == 12 && local.Minute == 0` → `local.Minute == 0 || local.Minute == 30`. Snap value: was hard-coded `0`, now `local.Minute` (which is 0 or 30 by definition of the branch).
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.4 → 1.1.5.
- `windows/tools/installer.iss` — `MyAppVersion` 1.1.4 → 1.1.5.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.
- `windows/SPEC.md` v2.5 → v2.6: §10 Theme #7 "Lazy" jitter minute hand row sync rule updated.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.5-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped.

## [1.1.4] - 2026-05-03 — CaptJohn: jitter starts at current minute, syncs only at noon; demos play out 11:55 → 12:05 at real speed

Two related fixes after Dan's v1.1.3 testing.

### 1. Jitter starts at the current minute and syncs only at noon

Per Dan: *"the captjohn hour capin min hand jitter should start out at the current time when started and sync up with real time at noon."*

- On `BuildCaptJohn`, the random-walk seed is now the current local minute instead of 0. The jitter hand's initial visible position matches the real time the moment the theme renders, so the user doesn't see the lazy hand snap from "12" to its walk position on the first minute tick.
- The walk's "even things up" sync condition was `local.Minute == 0` (every top-of-hour). Now it's `local.Hour == 12 && local.Minute == 0` — only the noon transition resets the displayed minute to 0. Every other hour boundary just gets a normal ±3 random walk step.
- Walk also now runs unconditionally (was previously gated by `showJitter`). Visibility is still gated by state — the walk advances even while the hand is hidden so the displayed minute stays roughly current if the user later toggles Hora Chapín ON.

### 2. Almuerzo / Fini demos play out at real speed from 11:55 → 12:05 (or 16:55 → 17:05)

Per Dan: *"If the almurtzo or fini demo buttons are clicked they should set the hour and min hands at 5 min to either 12 or 5 PM (the time the normal flash activity would take place) and proceed as they normally would if not checked. That is, stop flashing at 5 min after the hour."*

- Earlier passes pinned `local` to a constant (12:00:00 / 17:00:00). The flash continued indefinitely until the user clicked the radio off — that wasn't a faithful demo of the noon / 5 PM event, just a frozen single moment.
- v1.1.4 instead establishes a demo-start checkpoint on activation (`_captJohnDemoStartLocal`) and remaps `local` as `demoBase + (real - checkpoint)`, where `demoBase` is today at 11:55:00 (Almuerzo) or 16:55:00 (Fini). Real elapsed time advances demo time at the same rate.
- Result: clicking Almuerzo at any moment makes the clock instantly read 11:55:00, the "12" appears, all three flash on/off; one real minute later the clock reads 11:56:00; … five real minutes later it reads 12:00:00 (the apex of the event); ten real minutes later it reads 12:05:00 and the flash window closes naturally. The demo radio stays checked but no flashing happens — exactly what the clock would do on its own at noon if no demo were active.
- Click the radio off then on to restart the play-out at 11:55:00. Switching between Almuerzo and Fini also resets the checkpoint via the `OnCaptJohnDemoModeChanged` DP callback.
- Demo time also drives the analog hand rotations (`_hourRotate.Angle` / `_minuteRotate.Angle` / `_secondRotate.Angle`) — the outer `UpdateClock` already set them against real time on the same dispatcher tick; `_digitalUpdater` now overwrites them with demo-time values when a demo is active.

### Files touched

- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — new `_captJohnDemoStartLocal` field; `OnCaptJohnDemoModeChanged` DP callback wired into `CaptJohnDemoModeProperty`; `BuildCaptJohn` initializes the jitter at `nowLocal.Minute` and sets the rotate transform's initial angle accordingly; `_digitalUpdater` rewritten with the demo-time mapping + noon-only sync.
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.3 → 1.1.4.
- `windows/tools/installer.iss` — `MyAppVersion` 1.1.3 → 1.1.4.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.
- `windows/SPEC.md` v2.4 → v2.5: §10 Theme #7 — Per-tick state, Demo time-pinning, and Demo persistence rows rewritten to reflect the v1.1.4 model.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.4-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped.

## [1.1.3] - 2026-05-03 — CaptJohn: flash-window state machine corrected + demos persist + Hora Chapín hands at 10%

Three coordinated fixes after Dan smoke-tested v1.1.2:

1. **Almuerzo / Fini demos didn't behave correctly.** Per Dan's spec: "At 5 minutes to 12:00 PM the 12 becomes visible and for the next 10 minutes the clock is accurate, shows both hour and minute hands and all three flash on and off every 10 seconds. … The demo buttons set the clock to 12 and 5 respectively and demonstrate the visual effects until the demo button is clicked off." The v1.1.2 state machine was muddled — numerals only fired under Hora Chapín, real hands kept their lazy-mode opacity even during the flash window, and demos cleared when the menu closed. Rewrote the state machine to make the three states clearly distinct.
2. **"Bring up the transparency to 90%" meant 90% transparent (= 10% opaque).** I'd misread it in v1.1.2 as 90% opacity (`0.9`), so the real hands were effectively at full strength behind the jitter hand — defeating the lazy bar-clock concept. Corrected to `0.1`.
3. **Demos persist until clicked off**, per the explicit spec. Earlier versions cleared on `ContextMenu.Closed`.

### New CaptJohn `_digitalUpdater` state machine

Three high-level states, each visually distinct:

- **State A — Flash window** (real-time 11:55–12:05 OR 16:55–17:05, OR any time a demo is pinned). Hour, minute, and second hands plus the relevant numeral flash on/off together at 5 s on / 5 s off. Off-frames hide all four. Only the noon-side numeral ("12") flashes during the noon window; only the 5-side numeral ("5") flashes during the 5 PM window — the v1.1.2 code had "12" appearing during the 5 PM window too. Jitter hand suppressed entirely during the flash so the real time isn't obscured.
- **State B — Hora Chapín ON, outside flash window.** Lazy bar-clock mode: jitter hand at 100% on top, real hour + minute at **10% opacity** (was 90% in v1.1.2 — wrong direction), second hand hidden, numerals hidden.
- **State C — Hora Chapín OFF, outside flash window.** Regular numberless clock face: hour, minute, second hands at 100%, no jitter, no numerals.

Demo-mode pinning forces the state machine into A unconditionally by overriding `local` to today at 12:00:00 (Almuerzo) or 17:00:00 (Fini); the wrap-safe distance-to-target arithmetic then resolves to 0 and the flash window matches every tick.

### Demo persistence

- Both Almuerzo and Fini menu items now have `StaysOpenOnClick="True"`. Clicking them toggles the radio without dismissing the popup.
- The `ContextMenu.Closed` handler that cleared `CaptJohnDemoMode` was removed from both `MainWindow.xaml.cs` and `FloatingClockWindow.xaml.cs`. The XAML markup `Closed="JollyRogerMenu_Closed"` was removed from both windows.
- Mutex between the two demos is still automatic — `IsAlmuerzoActive` / `IsFiniActive` setters on `TabViewModel` route through the single `CaptJohnDemoMode` string, so picking Fini while Almuerzo is checked auto-unchecks Almuerzo.
- Switching off CaptJohn (selecting any other theme) still clears any active demo (defensive — the Jolly Roger button is hidden on other themes, so there's no UI to clear it through).

### Files touched

- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — `_digitalUpdater` rewritten around the three explicit states; jitter walk only advances when state B is active.
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml` — Almuerzo / Fini items get `StaysOpenOnClick`; Closed handler removed; explanatory comment added.
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml.cs` — `JollyRogerMenu_Closed` body deleted (replaced with a comment pointing at the spec change).
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml` — same XAML changes mirrored.
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` — same handler removal.
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.2 → 1.1.3.
- `windows/tools/installer.iss` — `MyAppVersion` 1.1.2 → 1.1.3.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.
- `windows/SPEC.md` v2.3 → v2.4: §10 Theme #7 — Default Hora Chapín state, Real hour / minute hand opacity, "12" / "5" numeral visibility rules, and the demo-persistence rule all rewritten to match the v1.1.3 state machine.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.3-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped per Dan's running directive.

## [1.1.2] - 2026-05-03 — CaptJohn: real hand opacity 7.5% → 90% in Hora Chapín ON

Per Dan after smoke-testing v1.1.1: with Hora Chapín ON the real hour/minute hands at 7.5% were too faint to read. Bumped the baseline to 90%, keeping the 100% bump during the noon / 5 PM flash windows. The lazy/jittered novelty still reads — the jitter hand is full-black ink at 100% on top of the parchment — but the real time is now legible behind it.

- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — `BuildCaptJohn` `_digitalUpdater`: `realOpacity` baseline `0.075` → `0.9` (Hora Chapín ON branch). Flash-window peak still 1.0; second-hand visibility unchanged. Hora Chapín OFF branch unaffected (100% always).
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.1 → 1.1.2.
- `windows/tools/installer.iss` — `MyAppVersion` 1.1.1 → 1.1.2.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.
- `windows/SPEC.md` v2.2 → v2.3: §10 Theme #7 — Default Hora Chapín state row updated; "real hands at 7.5% baseline" → "90% baseline" everywhere it appeared.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.2-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped.

## [1.1.1] - 2026-05-03 — CaptJohn: Jolly Roger overlay + Hora Chapín default OFF

Patch on top of v1.1.0. Two issues Dan flagged after smoke-testing the v1.1.0 build:

1. **Jolly Roger overflow menu was missing from the Captain John's face.** The v1.1.0 commit had wired the theme renderer (BuildCaptJohn, jitter random walk, flash windows) but not the user-facing Jolly Roger button that lets you toggle Hora Chapín on/off and trigger the Almuerzo / Fini demos. v1.1.1 adds it.
2. **Default render should be Hora Chapín OFF**, not ON. The novelty mode is opt-in.

### What v1.1.1 changes

- New ☠ Jolly Roger overlay button in the lower-left of the clock face area, visible **only on the CaptJohn theme** (Visibility bound to `TabViewModel.IsCaptJohnTheme` via `BooleanToVisibilityConverter`). 44×44 with a 36 px glyph; foreground inherits `OverlayGlyphBrush` so it's near-black on the parchment backdrop.
- Click opens a `ContextMenu` with three items:
  - **Hora Chapín** (checkable, persistent, two-way bound to `CaptJohnHoraChapin`) — `StaysOpenOnClick` so the user can flip it without dismissing the menu.
  - **Almuerzo (12:00 demo)** (checkable, momentary, two-way to `IsAlmuerzoActive`) — pins the rendered time to today at 12:00:00 so the noon flash window fires continuously.
  - **Fini (5:00 PM demo)** (checkable, momentary, two-way to `IsFiniActive`) — pins to 17:00:00 so the 5 PM window fires continuously.
- `ContextMenu.Closed` clears the demo state so demos never outlive the user's open popup. Hora Chapín is unaffected by close (persistent toggle).
- `CaptJohnHoraChapinEnabled` DependencyProperty default flipped from `true` to **`false`** (regular numberless face is the default; jitter is opt-in).
- `TabSettings.CaptJohnHoraChapin` (bool, default false) added to `settings.json` schema so the toggle persists per-tab across restarts.
- `TabViewModel` exposes `IsCaptJohnTheme`, `CaptJohnHoraChapin`, `CaptJohnDemoMode`, `IsAlmuerzoActive`, `IsFiniActive` for the binding stack.
- Switching a tab off CaptJohn auto-clears any active demo (defensive — there's no Jolly Roger panel on the new theme to clear it through).
- Same overlay added to `FloatingClockWindow` so floating CaptJohn clocks get the same controls.

### Files touched

- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — two new DPs (`CaptJohnHoraChapinEnabled`, `CaptJohnDemoMode`); `_digitalUpdater` rewritten to honor them (demo time-pin, Hora-Chapín-off branch sets jitter hand opacity 0 and real-hand opacity 1.0).
- `windows/src/ComTekAtomicClock.Shared/Settings/SettingsModel.cs` — `TabSettings.CaptJohnHoraChapin` field.
- `windows/src/ComTekAtomicClock.UI/ViewModels/TabViewModel.cs` — new properties + auto-clear-demo on theme switch.
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml` — Jolly Roger overlay button + ContextMenu in the tab-content `DataTemplate`; CaptJohn DPs added to the `ClockFaceControl` element.
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml.cs` — `JollyRogerButton_Click`, `JollyRogerMenu_Closed` handlers.
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml` — `BoolToVis` resource; CaptJohn DPs on `ClockFaceControl`; Jolly Roger button.
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` — same handlers.
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.1.0 → 1.1.1.
- `windows/tools/installer.iss` — `MyAppVersion` 1.1.0 → 1.1.1.
- `windows/CHANGELOG.md` (this entry).
- `windows/CONTEXT.md` — current-version line bumped.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

### Distribution

`release/ComTekAtomicClock-v1.1.1-Setup.exe` rebuilt via Inno Setup. Self-contained zip skipped per Dan's directive (he's testing the installer, not the standalone deploy).

## [1.1.0] - 2026-05-03 — Hand-length feature pass + Captain John's Marina theme (CaptJohn)

Feature release. Combines the hand-length tuning across every analog face with the new **Captain John's Marina** (CaptJohn) theme — the 13th theme overall and the 7th in the analog cluster. The theme ships fully wired runtime in this release: `Theme.CaptJohn` enum value, `BuildCaptJohn` renderer in `ClockFaceControl`, gallery entry, per-tab persistence, jitter-and-flash demo state in the per-tick callback. Earlier CaptJohn drafts targeted "design pre-stage now, runtime in v1.1.x"; the runtime work was folded into v1.1.0 instead so 1.1.0 is the cohesive feature release.

### Hand-length pass (visible runtime change)

Dan, while iterating the design of the new `CaptJohn` (Captain John's Marina) theme, asked for a clearer length distinction between the hour and minute hands — minute hand longer, hour hand shorter, by about a half-inch difference. Then asked for the same change applied to "all faces, if they aren't already different lengths." All 6 of v1.0.0's analog faces had different hour/minute lengths but the differences were modest (36–38 px ≈ 3/8 inch).

Across **every analog face** (Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight), shortened the hour hand by 24 px (1/4″ at 96 DPI) and lengthened the minute hand by 24 px (1/4″). Net effect: minute hand is now 84–86 px longer than the hour hand on every analog theme — a clearly visible difference in line with traditional clock proportions where the minute hand reaches near the dial edge while the hour hand stops well short.

| Theme | Hour: was → now | Minute: was → now | Second (unchanged) |
|---|---|---|---|
| Atomic Lab | 90 → **66** | 128 → **152** | 142 |
| Boulder Slate (Mondaine batons) | 100 → **76** | 138 → **162** | (special lollipop, unchanged) |
| Aero Glass | 92 → **68** | 128 → **152** | 140 |
| Cathode | 90 → **66** | 128 → **152** | 142 |
| Concourse | 86 → **62** | 122 → **146** | 138 |
| Daylight | 90 → **66** | 128 → **152** | 140 |

Hand thicknesses, colors, and second-hand lengths are unchanged — only hour and minute lengths move. Digital-only and encoder themes (Flip Clock / Marquee / Slab / Binary / Hex / Binary Digital) have no analog hands and are unaffected.

### CaptJohn theme — runtime ships in this release

The CaptJohn theme uses the same minute-longer-than-hour proportions as the rest of the analog cluster (66 / 152) and adds a new `bordeauxMid` minute-hand color (`#641414`) sitting between its dark hour bordeaux (`#4A0F0F`) and the bright numeral red (`#7B1616`) — three distinguishable shades.

Runtime additions:

- `Theme.CaptJohn` enum value (between `Daylight` and `FlipClock`) in `Shared/Settings/SettingsModel.cs`.
- `BuildCaptJohn()` (~250 LOC) in `Controls/ClockFaceControl.xaml.cs` — parchment radial backdrop, brass ring, cream face, Captain John's logo painted into an ellipse-clipped layer at 40% opacity, "The Busted Flush" caption (Monotype Corsiva 13 px italic at 40% sepia), Hora Chapín jitter hand at 152 px ink black, hidden Cinzel "12" / "5" numerals at radius 130, real hour / minute / second hands at 66 / 152 / 138 px, center pin.
- Per-tick `_digitalUpdater` callback drives the jitter random walk (±3 minute step per real-minute click, sync only at top of hour) and the 5 s on / 5 s off flash window for the noon and 5 PM demo modes.
- `ThemeCatalog` entry "Captain John's" between Daylight and Flip Clock, gallery preview pulls `Assets/themes/captjohn-mockup.png`.
- `TabViewModel.IsDarkTheme` returns `false` for CaptJohn (parchment = light backdrop → near-black overlay glyphs).

Asset additions (also serve as the single source of truth for the design):

- `windows/design/themes/captjohn-mockup-hora.png` — Hora Chapín ON (default, lazy mode with jitter)
- `windows/design/themes/captjohn-mockup-hora-off.png` — Hora Chapín OFF (regular numberless clock)
- `windows/design/themes/captjohn-mockup-almuerze.png` — Almuerzo demo (12:00 noon flash)
- `windows/design/themes/captjohn-mockup-fini.png` — Fini demo (5:00 PM flash)
- `windows/design/themes/captjohn-mockup-menu-open.png` — Jolly Roger overflow menu open (Hora Chapín checkbox + Almuerzo / Fini radios)
- `windows/design/themes/captjohn-mockup.png` — alias for the default Hora Chapín state
- `windows/design/fonts/Cinzel-Variable.ttf` — OFL-licensed Cinzel variable font (125 KB) used for Cinzel Bold numerals on the CaptJohn face when 12 / 5 flash during demo windows
- `windows/src/ComTekAtomicClock.UI/Assets/JohnsMarina-logo.jpg` — Captain John's Marina source logo (168 × 197) used as the dial backdrop at 40% opacity
- `windows/src/ComTekAtomicClock.UI/Assets/JohnsMarina-logo-circle.jpg` — pre-padded 270 × 270 white-cornered version (so the original image's diagonal fits inside the inscribed circle of the 320 face)

The CaptJohn theme delivers: parchment-and-brass palette, lazy "Hora Chapín" minute hand with random ±3 minute jitter (sync only at top of hour), real-time hour and minute hands at 7.5% opacity in the default Hora Chapín mode, "Almuerzo" demo pinning to 12:00 noon with all hands + numerals flashing at 5 s cadence, "Fini" demo pinning to 5:00 PM with 12 + 5 numerals flashing, "The Busted Flush" caption in Monotype Corsiva 13 px italic at 40% sepia (matching logo opacity), and the Jolly Roger ☠ overflow button (60 px) in the lower-left whose popout panel hosts the Hora Chapín checkbox + Almuerzo / Fini demo radios. The popout-panel UI scaffolding lands here as runtime hooks; the full Jolly-Roger overflow visual + click handler ships in a v1.1.x follow-up — for v1.1.0, the theme is selectable from the gallery and renders correctly in its default Hora Chapín ON state.

### Files touched

- `windows/src/ComTekAtomicClock.Shared/Settings/SettingsModel.cs` — `Theme.CaptJohn` enum value added
- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — six analog `Build*` methods updated for the hand-length pass; new `BuildCaptJohn()` renderer (~250 LOC); per-tick jitter + flash logic in the digital-updater callback
- `windows/src/ComTekAtomicClock.UI/ViewModels/ThemeCatalog.cs` — new "Captain John's" gallery entry between Daylight and Flip Clock
- `windows/src/ComTekAtomicClock.UI/ViewModels/TabViewModel.cs` — `IsDarkTheme` returns `false` for CaptJohn (parchment is light → near-black overlay glyphs)
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 1.0.0 → 1.1.0; `Resource` entries for `JohnsMarina-logo.jpg`, `Cinzel-Variable.ttf`, `captjohn-mockup.png`
- `windows/tools/installer.iss` — `MyAppVersion` 1.0.0 → 1.1.0
- `windows/src/ComTekAtomicClock.UI/Assets/JohnsMarina-logo.jpg` (new)
- `windows/src/ComTekAtomicClock.UI/Assets/JohnsMarina-logo-circle.jpg` (new)
- `windows/design/fonts/Cinzel-Variable.ttf` (new — OFL Cinzel variable font)
- `windows/design/themes/captjohn-mockup-*.png` (new — 6 mockup states)
- `windows/SPEC.md` v2.0 → v2.1 (front matter; CaptJohn full per-element spec inserted as Theme #7 in §10; #8–13 renumbered; per-theme hand rows; end-of-doc baseline)
- `windows/CONTEXT.md` (current version line + session log entry + repo state)
- `windows/TODO.md` (CaptJohn entry with full design lock-in detail; new "Custom" theme entry — user-uploaded background image)
- `windows/CHANGELOG.md` (this entry)

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Release` → 0 errors, 0 warnings.

## [1.0.0] - 2026-05-03 — First stable release

Symbolic milestone. Code jumps 0.0.39 → 1.0.0. Functionally, this is v0.0.39 with a version stamp and a consolidated backlog doc — no runtime change, but the version leap declares the **core feature set is stable enough for end-user distribution**.

### What's stable in 1.0

- **NIST stratum-1 sync** (Boulder pool — anycast `time.nist.gov` plus 10 named servers across Gaithersburg MD + Fort Collins CO).
- **NTP.br stratum-1 sync** (Brazil pool — `a.ntp.br` anycast plus 5 GPS-disciplined servers in São Paulo). User picks via Settings → Time source. Atomic Lab face's NIST-panel subtitle and per-face source label both update dynamically.
- **12 themable clock faces** (6 analog: Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight; 4 digital-only: Flip Clock, Marquee, Slab, Binary Digital; 2 encoders: Binary, Hex). All faces show day-of-week + date-of-month + month + year. Atomic Lab is the default theme; per-tab theme choice persists.
- **Chrome-style tabs** via native WPF `TabControl` (Dragablz removed in v0.0.33). Active 19pt Bold / inactive 9pt; right-click → Tab Settings dialog directly; `+ New tab` / `+ New window` toolbar buttons. Single-click selection is reliable.
- **Per-tab IANA time zones** (~140 from Windows zones — full ~600-entry IANA catalog still on the roadmap).
- **Free-floating clock windows** spawned by `+ New window` or "Open in new window" right-click. Each hosts one clock; single `⋯` overlay button hosts Settings… / Themes… / Bring back into tabs / Help… / About…. Settings dialog centers over the originating window (v0.0.39 fix).
- **Three-process architecture** with named-pipe IPC (`ComTekAtomicClock.UiToService`, length-prefixed JSON, schema-versioned, ACL-restricted to interactive users + LocalSystem).
- **Service lifecycle:** UI starts the service on launch and stops on exit (`start= demand` mode is the alpha simplification; `auto` for headless operation is on the roadmap).
- **Settings persistence:** `%APPDATA%\ComTekAtomicClock\settings.json` (per-user) + `%ProgramData%\ComTekAtomicClock\service.json` (per-machine). Atomic write, corrupt-file recovery, JsonExtensionData forward-compat.
- **Three exception handlers** (Dispatcher / AppDomain / TaskScheduler) wired in App constructor.

### What's deferred to post-1.0 — see `windows/TODO.md`

~38 open items (2 active queue, 5 Phase 2 magnetic-snap, 29 Phase 3+ Planned, 3 doc-cleanups). Highlights of what 1.0 doesn't yet have:

- Timer + Countdown modes (in active queue)
- Magnetic-snap floating windows (Phase 2)
- System tray icon
- Five-slot color overrides
- Time-format selector UI (Auto / 12h / 24h)
- Confirm-large-offset toast flow
- Authenticode code-signing of Setup.exe (SmartScreen warns until then)
- MSIX packaging + GitHub Pages publish pipeline
- ARM64 build target

`windows/TODO.md` is the day-to-day operational view; `SPEC.md` §21 retains the spec-grade Implemented/Planned matrix for reference.

### Why jump 0.0.x → 1.0.0 (not 0.1.0)

The 0.0.x churn reflected an iterative bug-fix cycle (v0.0.14 → v0.0.39, ~25 patches) where each release fixed something specific. The core was working from ~v0.0.20-ish; the patches were polish. Stamping 1.0 doesn't claim feature-complete — it claims "we're confident enough to put this in front of users with a clean version number on the title bar." Future feature drops will bump the minor (1.1, 1.2, …); fixes bump patch (1.0.1, 1.0.2, …) per `feedback_version_bump_on_change.md`.

### Files touched in this commit

- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — `<Version>` 0.0.39 → 1.0.0; `<AssemblyVersion>` 0.0.39.0 → 1.0.0.0; `<FileVersion>` 0.0.39.0 → 1.0.0.0
- `windows/tools/installer.iss` — `MyAppVersion` 0.0.36 → 1.0.0 (had been stale; now matches the bumped csproj)
- `windows/TODO.md` — **NEW.** Consolidated backlog from `CONTEXT.md` "Pending" + `SPEC.md` §21 "Planned" into one numbered, grouped, "recently shipped"-trail-tracking doc.
- `windows/CONTEXT.md` — replaced the inline backlog dump with a one-paragraph pointer to `TODO.md`; updated repo state header to v1.0.0.
- `windows/SPEC.md` — front matter doc version 1.4 → 2.0; added v1.4 → v2.0 changelog row; §21 Planned table prefaced with a "TODO.md is the operational view" callout; end-of-doc baseline bumped.
- `windows/CHANGELOG.md` — this entry.

### Build verification

`dotnet build src/ComTekAtomicClock.UI -c Debug` → 0 errors, 0 warnings.

### Distribution

Build the v1.0.0 install package by re-running the publish + ISCC pipeline (see SPEC.md §20). Output:

```
release\ComTekAtomicClock-v1.0.0-Setup.exe                     (~97 MB)
release\ComTekAtomicClock-v1.0.0-win-x64-self-contained.zip    (~135 MB)
```

Per the narrowed Friday rule (`feedback_no_friday_ship.md`), routine dev pushes to `master` are free on weekends; **publishing a tagged release for customers** is the action that holds till Monday. The Setup.exe sitting in `release/` is fine; `git push origin v1.0.0` (the customer-facing event) waits for explicit override.

## [0.0.39] - 2026-05-03 — Settings / Themes dialog: center over the originating window

**Problem:** Dan: *"Can we fix the settings appearing over the original clock face instead of the one on which we clicked the settings button on?"* When the user opened Settings (or Themes…) from a `FloatingClockWindow`'s `⋯` menu, the dialog appeared centered on the main window, even if the floating window was on a different monitor or far from the main window. Disorienting.

**Root cause:** `MainWindowViewModel.OpenTabSettingsCore` and `OpenThemesPickerFor` hardcoded `Owner = Application.Current?.MainWindow` on the dialogs. The Owner determines `WindowStartupLocation="CenterOwner"` placement.

**Solution:** Added explicit-owner overloads:

- `MainWindowViewModel.OpenTabSettingsForOwner(TabViewModel, Window?)` → forwards to the existing `OpenTabSettingsCore`, now taking a Window? parameter that becomes the dialog's Owner. Falls back to MainWindow if null.
- `MainWindowViewModel.OpenThemesPickerForOwner(TabViewModel, Window?)` → same shape, splits the existing handler into a public-explicit + private-core pair (`OpenThemesPickerCore`).
- `FloatingClockWindow.SettingsMenuItem_Click` now calls `mainVm.OpenTabSettingsForOwner(_tab, owner: this)`.
- `FloatingClockWindow.ThemesMenuItem_Click` now calls `mainVm.OpenThemesPickerForOwner(_tab, owner: this)`.

The existing `OpenTabSettingsForCommand` / `OpenThemesPickerForCommand` `RelayCommand` paths (used by the in-tab right-click and the in-tab `?` overlay) keep their previous behavior — Owner falls back to MainWindow, which is correct for those callers.

Help… and About… on the floating-window menu were already correct (`Owner = this` set inline in the `*MenuItem_Click` handlers; no command routing).

**Files touched:** `windows/src/ComTekAtomicClock.UI/ViewModels/MainWindowViewModel.cs` (Owner-aware overloads + Core split for the Themes path), `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` (Settings + Themes menu handlers route to the explicit-owner methods), `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` (0.0.38 → 0.0.39), `windows/SPEC.md` (§13 dialog-Owner notes), `windows/CONTEXT.md` (session log + repo state, queue item discharged), `windows/CHANGELOG.md` (this entry).

## [0.0.38] - 2026-05-03 — Daylight + Boulder Slate: time readout recenters on each tick (alignment fix)

**Problem:** Dan: *"On at least the daylight theme, the day is not centered above the digital time. They should be centered over each other on all screens."*

Root cause in `ClockFaceControl.UpdateClock`: the date `TextBlock` was being re-measured + re-centered on every tick (since v0.0.24's `_recenterDateReadoutOnUpdate` flag), but the **time** `TextBlock` was placed once at build with a placeholder string (`"00:00:00"`) and never re-centered. As actual time strings of varying width replaced the placeholder tick-by-tick (`"1:02:03 AM"` is narrower than `"12:34:56 PM"`), the time's visual center drifted while the date stayed pinned at `Cx`. Result: date and time visibly misaligned on every theme that uses bare canvas text instead of a panel-wrapped readout (Boulder Slate + Daylight).

**Solution:** Extended the recenter block in `UpdateClock` to recenter **both** `_dateReadout` and `_digitalReadout` when the flag is set. Both elements now share the same measure-then-center logic, so they line up at `Cx` every tick regardless of rendered string widths.

**Renamed** the flag from `_recenterDateReadoutOnUpdate` → `_recenterTextReadoutsOnUpdate` to reflect the broader scope. Doc comment updated to explain the rename and the underlying drift mechanism.

**Themes affected (positive):** Boulder Slate, Daylight. **Themes unaffected:** the other 10 — Atomic Lab / Aero Glass / Cathode / Concourse wrap their readouts in panels with `HorizontalAlignment="Center"` (auto-centered, don't set the flag); Flip Clock / Marquee / Slab / Binary / Hex / Binary Digital position their text via custom Canvas math that doesn't use this code path.

**Files touched:** `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` (4 sites: field rename + UpdateClock recenter block + 2 setters in BuildBoulderSlate / BuildDaylight), `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` (0.0.37 → 0.0.38), `windows/SPEC.md` (§10 Daylight / Boulder Slate "Date centering" notes), `windows/CONTEXT.md` (session log), `windows/CHANGELOG.md` (this entry).

## [0.0.37] - 2026-05-03 — TabSettingsDialog height +100 px (Time Source group fit)

**Problem:** Dan's first run of v0.0.36 with the new Time Source radio group: *"the settings screen is about an inch too short."* The v0.0.36 patch added three rows to the dialog (Time source label + RadioButton group with two two-line items + explanatory subtitle ≈ 120 px of new content) inside a fixed-size `NoResize` window that was already tightly packed at 540 px. The Save / Cancel button row was clipping below the visible area on Dan's display.

**Solution:** Bumped `Height` 540 → 640 in `TabSettingsDialog.xaml`. Width unchanged. Adds about an inch (96 px at 96 DPI + 4 px slack) of vertical room — comfortably accommodates the Time Source group plus a small breathing margin around the Save row.

**Files touched:** `windows/src/ComTekAtomicClock.UI/Dialogs/TabSettingsDialog.xaml`, `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` (0.0.36 → 0.0.37), `windows/SPEC.md` (§6 windows table + §13 dialog properties), `windows/CONTEXT.md` (session log), `windows/CHANGELOG.md`.

## [0.0.36 release tooling] — Inno Setup installer added (2026-05-03)

Documentation/tooling addition; no runtime code changed, no version bump.

Added `windows/tools/installer.iss` — an Inno Setup script that wraps the self-contained `dotnet publish` output into a single `Setup.exe` (~97 MB, LZMA2/max compression). The installer bundles all three projects (UI / Service / Installer) plus the supporting docs (LICENSE, CHANGELOG, README, SPEC, INSTALL), shows the MIT license on first screen, defaults to `%ProgramFiles%\ComTekAtomicClock\`, runs `ServiceInstaller.exe install` post-copy to register the Windows Service with SCM, creates Start Menu (and optional desktop) shortcuts, and registers an Add/Remove Programs uninstall entry that runs `ServiceInstaller.exe uninstall` before file removal.

Supersedes the bare zip distribution as the "give it to a non-technical user" path. The zip remains available in `release/` for portable / no-installer use cases.

Build: `winget install JRSoftware.InnoSetup` (one-time), then `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" tools\installer.iss` after each `dotnet publish` round-trip. SPEC.md §20 carries the full step-by-step.

Future: bump `MyAppVersion` in `installer.iss` when the project version bumps. Authenticode signing of the installer + bundled exes is still on the v1.0 roadmap (SmartScreen will warn on first run until then).

## [0.0.36] - 2026-05-01 — Time source picker (Boulder + Brazil); per-face source label; dynamic NIST badge

**Problem:** Until v0.0.35 the app was hardcoded to the NIST stratum-1 pool out of Boulder, CO. Dan testing from / building for South-American users would see ~150-300 ms of trans-equatorial RTT on every sync against NIST. NTP.br (operated by NIC.br in São Paulo) is the regional equivalent and a much better fit.

**Solution:** Added a Time Source picker to the Settings dialog with two choices:

| Source | Operator | Anycast | Pool |
|---|---|---|---|
| **Boulder** (default) | NIST | `time.nist.gov` | 10 stratum-1 servers across Gaithersburg MD + Fort Collins CO |
| **Brazil** | NIC.br / NTP.br | `a.ntp.br` | 5 GPS-disciplined stratum-1 servers in São Paulo BR |

**Per-face source label.** Every clock face now shows a single-word `BOULDER` or `BRASIL` header at the top-center (Cascadia Code 11pt SemiBold, warm-amber `#FFCC00` at ~70% opacity — instrument-label vibes that play on dark + light theme backgrounds). Painted by a uniform `AddSourceLabel` helper called from `RenderActiveTheme` so it's automatically present on all 12 themes without per-theme placement work.

**Atomic Lab dynamic badge.** The face's NIST-panel subtitle was hardcoded `"NIST · BOULDER · CO"`. v0.0.36 makes it a `TimeSourceBadge` DependencyProperty that returns:

- `"NIST · BOULDER · CO"` when source = Boulder
- `"NTP.BR · SÃO PAULO · BR"` when source = Brazil

Same parallel city·state·country structure on both. Atomic Lab face rebuilds when the badge changes.

### Persistence + Service catch-up

- `GlobalSettings.TimeSource` (UI's view) — written to `settings.json` so the on-face label / Atomic Lab badge persist.
- `ServiceConfig.TimeSource` (Service's view) — written to `service.json` by the dialog Save handler so the Service picks the correct pool.
- The TabSettings dialog Save now writes both files in one go and exposes a `ChosenTimeSource` to `MainWindowViewModel` so it can mirror the choice into the in-memory `AppSettings.Global` and raise `PropertyChanged(TimeSource/Label/Badge)` to refresh all open clock faces (including floating windows).
- **Service catch-up:** the Service re-reads `service.json` on each sync iteration. Switching source takes effect on the next sync (within the configured frequency, default 12 h). A force-resync IPC trigger is on the Phase-2 Planned list.

### Files changed

- `windows/src/ComTekAtomicClock.Shared/Settings/SettingsModel.cs` — added `TimeSource` enum (Boulder / Brazil); added `GlobalSettings.TimeSource` and `ServiceConfig.TimeSource` properties (default Boulder, backward-compat with v0.0.35-and-earlier on-disk settings)
- `windows/src/ComTekAtomicClock.Service/Sync/NistPool.cs` — **DELETED**
- `windows/src/ComTekAtomicClock.Service/Sync/TimeSourcePool.cs` — **NEW**: multi-source pool registry with `BoulderAnycast`/`BoulderStratumOne` (10 NIST servers, unchanged) and `BrazilAnycast`/`BrazilStratumOne` (5 NTP.br servers); `GetWalkOrder(source, primary)`, `IsKnownHost(source, hostname)`, `IsKnownHostAcrossSources(hostname)` accessors
- `windows/src/ComTekAtomicClock.Service/Sync/SyncWorker.cs` — switched from `NistPool.GetWalkOrder` to `TimeSourcePool.GetWalkOrder(config.TimeSource, primary)`; primary fallback uses `TimeSourcePool.GetAnycast(source)`; log messages parameterized on `{Source}`
- `windows/src/ComTekAtomicClock.UI/Dialogs/TabSettingsDialog.xaml` — added Time Source radio group below Sync frequency in the "ALL CLOCKS ON THIS PC" section; two-line item layout with primary label + small subtitle showing operator/anycast hostname
- `windows/src/ComTekAtomicClock.UI/Dialogs/TabSettingsDialog.xaml.cs` — load TimeSource from `_serviceConfig` on dialog open; on Save, capture choice → write `service.json` once (sync-frequency + time-source bundled); expose `ChosenTimeSource` to caller
- `windows/src/ComTekAtomicClock.UI/ViewModels/MainWindowViewModel.cs` — added `TimeSource`, `TimeSourceLabel`, `TimeSourceBadge` properties (read from `_settings.Global.TimeSource`); `OpenTabSettingsCore` now mirrors `dlg.ChosenTimeSource` into `_settings.Global.TimeSource` and raises PropertyChanged for the three properties
- `windows/src/ComTekAtomicClock.UI/Controls/ClockFaceControl.xaml.cs` — added `TimeSourceLabelProperty` + `TimeSourceBadgeProperty` DependencyProperties; both trigger full theme rebuild on change; new `AddSourceLabel()` helper called from `RenderActiveTheme()` paints `TimeSourceLabel` at top-center on every theme; Atomic Lab's hardcoded `"NIST · BOULDER · CO"` text replaced with `TimeSourceBadge` property read
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml` — `<controls:ClockFaceControl>` in the tab DataTemplate gets two new bindings: `TimeSourceLabel` and `TimeSourceBadge` via `RelativeSource AncestorType=Window` to the MainWindowViewModel
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml` — named the `ClockFaceControl` `x:Name="ClockFace"` for code-behind access
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` — code-behind subscribes to `MainWindowViewModel.PropertyChanged` for TimeSource/Label/Badge; pushes values to `ClockFace.TimeSourceLabel`/`TimeSourceBadge` on construction and on change; unsubscribes on `Closed`
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 0.0.35 → 0.0.36
- `windows/SPEC.md` v1.3 → v1.4 (front matter, §4 Service NIST pool → multi-source pool, §5 Settings model TimeSource fields, §10 themes — Atomic Lab badge note + universal source label, §13 Settings dialog new field, §21 status — Brazil source moves to Implemented)
- `windows/CONTEXT.md` (session log entry, repo state line)
- `windows/CHANGELOG.md` (this entry)

### Build verification

`dotnet build src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj -c Debug` → 0 `error CS####` or `error MC####` (compilation clean). 4 MSB3021/MSB3027 file-copy errors expected because Dan's running v0.0.34 instance has `Shared.dll` locked — close + rebuild + F5 to deploy v0.0.36.

## [0.0.35] - 2026-05-01 — FloatingClockWindow: single ⋯ overlay button replaces ✕ + ? pair

**Problem:** Dan's first-run testing of v0.0.34's floating windows surfaced two issues:

1. The `✕` overlay button on the clock face was redundant — the OS title-bar's `×` already closes the window. Two close buttons within a few pixels of each other.
2. The Settings / Themes / Bring back into tabs items were buried under the `?` overlay, which was confusing — `?` reads as "Help" not "Settings", and even with the menu items there, discoverability was poor. Dan: *"is there any way to change the tz and face on the new window?"*

**Solution:** Removed both the `✕` and `?` overlay buttons. Replaced with a **single `⋯` "more options" button** at the same top-right position, using `<ui:SymbolIcon Symbol="MoreHorizontal20"/>` — the same Fluent three-dot affordance Windows 11 uses across the shell. All 5 menu items hang off this single button:

- **Settings…** (renamed from "Tab settings…" — "tab" is wrong on a window)
- **Themes…**
- **Bring back into tabs**
- **Help…**
- **About…**

Foreground stays bound to `TabViewModel.OverlayGlyphBrush` so the icon reads white on dark themes / near-black on light themes per the existing per-theme luminance map.

### Files touched

- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml` — removed `✕` Button + `?` Button; added single `⋯` Button with `ui:SymbolIcon` and the 5-item ContextMenu
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` — removed `CloseWindowButton_Click` and `HelpButton_Click`; added `MoreOptionsButton_Click`; renamed `TabSettingsMenuItem_Click` → `SettingsMenuItem_Click`
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 0.0.34 → 0.0.35
- `windows/SPEC.md` v1.2 → v1.3 (front matter, §6 Floating clock window subsection rewritten, end-of-doc)
- `windows/CONTEXT.md` (session log entry, repo state line)
- `windows/CHANGELOG.md` (this entry)

### Build verification

`dotnet build src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj -c Debug` → compilation clean (no `error CS####` or `error MC####`). Post-compile file copy errored because Dan's running v0.0.34 instance had `Shared.dll` locked — close the running clock and rebuild to deploy v0.0.35.

## [0.0.34] - 2026-05-01 — First-run polish: toolbar contrast + remove tab right-click menu

Two fixes from Dan's first-run feedback on v0.0.33.

### Fix 1: Toolbar button contrast

**Problem:** The "+ New tab" and "+ New window" buttons in the new tab toolbar were nearly invisible. The plain `<Button>` controls picked up the WPF-UI Dark theme dictionary merged in `App.xaml` and rendered as low-contrast (near-white text on near-white toolbar background, depending on the inherited brushes). Functional but unusable.

**Solution:** Switched both toolbar buttons from plain `<Button>` to `<ui:Button Appearance="Secondary">` with an explicit `Foreground="#0A0A0A"` to guarantee dark-on-light contrast against the `#F5F5F5` toolbar background. Same Fluent idiom used elsewhere in the app (the §1.9 banner uses `ui:Button`). Padding bumped from `8,2` to `10,3` for a slightly more substantial click target.

### Fix 2: Remove right-click context menu on tabs

**Problem:** Dan: *"right click on tab still brings up two menu options. Remove that."* The v0.0.33 two-item ContextMenu (Tab settings… / Open in new window) reintroduced the v0.0.23-era behavior Dan had previously rejected: he wants right-click on a tab header to directly open the Tab Settings dialog, no intermediate menu.

**Solution:** Removed the `ContextMenu` Setter from the `TabItem` `ItemContainerStyle`. Added a `PreviewMouseRightButtonDown` handler `TabItem_PreviewRightButtonDown` that opens `OpenTabSettingsForCommand` directly and marks the event handled. The "Open in new window" migration affordance (which lived on that menu in v0.0.33) moved to the "?" overlay menu on the clock face — alongside Themes…/Help…/About… — so the affordance is preserved without cluttering the tab strip.

### Files touched

- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml` — toolbar buttons converted to `ui:Button`; tab `ContextMenu` Setter removed; `PreviewMouseRightButtonDown` EventSetter added; "Open in new window" item added to the "?" overlay menu
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml.cs` — removed `TabContextSettings_Click` and `TabContextOpenInNewWindow_Click`; added `TabItem_PreviewRightButtonDown` and `OpenInNewWindowMenuItem_Click`
- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` — version 0.0.33 → 0.0.34
- `windows/CHANGELOG.md` (this entry)
- `windows/CONTEXT.md` (session log)
- `windows/SPEC.md` (§7 per-tab interactions table; §8 TabItem defaults)

## [0.0.33] - 2026-05-01 — Dropped Dragablz; native WPF TabControl; tear-away removed

**Problem:** Dragablz `0.0.3.234` was the root cause of nearly every tab-related bug fought across v0.0.14..v0.0.32 (eight iterations of tab-header refresh code, the `NotImplementedException` crash on `CollectionChanged.Replace` at v0.0.14, the click/drag classifier swallowing single clicks at v0.0.20, the imperative `SetTabHeaderInAllDisplays` walker at v0.0.32, headers rendered in a separate visual subtree forcing whole-window enumeration). Library has had no meaningful release in ~3-4 years; carrying an unmaintained dependency that bleeds maintenance time. Combined with Dan's stated direction toward magnetic-snap floating windows (which makes tear-away a less central feature anyway), the right move was a clean swap.

**Solution:** Replaced `Dragablz.TabablzControl` with the BCL `System.Windows.Controls.TabControl` + a flat-rectangular `ControlTemplate`-replaced `TabItem` `ItemContainerStyle` that reproduces the v0.0.32 visuals (19pt Bold active / 9pt inactive / `#FFFFFF` selected / `#E8E8E8` inactive / `#F5F5F5` hover / `#808080` selected border). Tear-away gesture removed entirely; explicit "+ New tab" / "+ New window" toolbar buttons above the strip + "Open in new window" / "Bring back into tabs" right-click commands replace it. `FloatingClockWindow` simplified to a single-clock window (no internal tab strip). Magnetic snap on floating windows added to Phase-2 Planned (SPEC.md §21).

### Removed (Dragablz workaround lineage)

- `Dragablz 0.0.3.234` package reference in `ComTekAtomicClock.UI.csproj`.
- `Services/AppInterTabClient.cs` — Dragablz `IInterTabClient` (tear-off mechanism).
- `MainWindow.SetTabHeaderInAllDisplays` + `EnumerateVisualDescendants` walker (the v0.0.32 imperative tab-name refresh).
- `Tag="TabHeaderText"` markup convention on `ItemTemplate` `TextBlock`s.
- The Dragablz-specific Thumb-stripping `Style.Resources` block in `MainWindow.xaml` and `FloatingClockWindow.xaml`.
- `TabItem_PreviewMouseLeftButtonDown` click-rescue handler (the v0.0.20 single-click reliability fix).
- The multi-strategy `TryFindTabFromContextMenuItem` helper (3 strategies, only needed because Dragablz's PlacementTarget walk was sometimes unreliable).

### Restored

- `OnPropertyChanged(nameof(Label))` in `TabViewModel.TimeZoneId` setter. Native WPF `TabControl` honors `PropertyChanged` on `ItemTemplate` bindings reliably, so `{Binding Label}` re-renders the tab header automatically. The v0.0.32 "two-event rule" is **superseded** — that was specifically a Dragablz workaround.

### Added (explicit tabs ↔ windows commands replacing tear-away)

- `MainWindowViewModel.OpenInNewWindowCommand` — right-click on a tab → "Open in new window" → migrates the tab to a new `FloatingClockWindow`.
- `MainWindowViewModel.NewClockWindowCommand` — "+ New window" toolbar button → spawns a `FloatingClockWindow` with a fresh clock (no migration, no main-strip presence).
- `MainWindowViewModel.BringWindowIntoTabsCommand` — "?" overlay menu on a floating window → "Bring back into tabs" → re-attaches the `TabViewModel` to `Tabs` and closes the window.
- `_openFloatingWindows : List<FloatingClockWindow>` registry tracking open floating windows so `BringWindowIntoTabsCommand` can find the right window.
- `SpawnFloatingWindow(TabViewModel)` helper that wires `Closed` → purge persistence if the user X'd out (rather than bringing the clock back into tabs).
- New toolbar `Border` above the tab strip in `MainWindow.xaml` hosting the two "+" buttons (background `#F5F5F5`, bottom border `#C0C0C0`, padding `8,4`).
- Per-tab `ContextMenu` (two items: Tab settings… / Open in new window) — note: distinct from the v0.0.23..v0.0.26 multi-item ContextMenu that was deliberately removed.
- `FloatingClockWindow` constructor now takes a `TabViewModel` and sets it as `DataContext`. Exposes `Tab` property for `BringWindowIntoTabsCommand`.
- `BringIntoTabsMenuItem_Click` handler on `FloatingClockWindow`.

### Notes

- Floating-window position persistence (X/Y/Width/Height across restart) is **NOT** in v0.0.33 — moved to Phase-2 alongside snap.
- The closing-the-floating-window-X behavior is a "delete this clock" action, NOT a "minimize to tabs" action. To keep the clock, use "Bring back into tabs" first, or close it via the per-tab close path while it's in the strip. This is documented in `FloatingClockWindow.xaml.cs:CloseWindowButton_Click`.
- The "+ New window" button creates a tab in `_settings.Tabs` (so the clock survives restart even if the user never brings it into tabs) but does NOT add a `TabViewModel` to the main window's `Tabs` collection (avoiding dual presence).

### Files touched

- `windows/src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj` (Dragablz removed, version 0.0.32 → 0.0.33)
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml` (full tab-strip rewrite + new toolbar)
- `windows/src/ComTekAtomicClock.UI/MainWindow.xaml.cs` (~150 LOC removed, 2 handlers added)
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml` (single-clock layout)
- `windows/src/ComTekAtomicClock.UI/FloatingClockWindow.xaml.cs` (rewritten)
- `windows/src/ComTekAtomicClock.UI/Services/AppInterTabClient.cs` (DELETED)
- `windows/src/ComTekAtomicClock.UI/ViewModels/TabViewModel.cs` (Label PropertyChanged restored)
- `windows/src/ComTekAtomicClock.UI/ViewModels/MainWindowViewModel.cs` (3 new commands, registry, spawn helper)
- `windows/SPEC.md` (v1.0 → v1.1; sections 3, 6, 7, 8, 17, 21, 22 updated)
- `windows/CONTEXT.md` (new "Why we dropped Dragablz" decision entry; superseded standing decisions; Phase-2 snap added; session log entry)
- `windows/CHANGELOG.md` (this entry)

### Build verification

`dotnet build src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj -c Debug` → 0 warnings, 0 errors.

## [0.0.32] - 2026-04-30

### Changed (architecture refactor — tab name is now set imperatively, two events only)

Per Dan's spec after v0.0.31 *still* didn't reliably update the tab header on TZ change: "Refactor all of the code that sets the tab name. Make sure it is changed only on two events. On Load and when the change dialog closes. When the change dialog box closes, it should read the current value of the combo box and change that tab to the correct name on all instances of the display client."

- **Abandoned the PropertyChanged-cascade approach entirely.** v0.0.21 (surgical `BindingExpression.UpdateTarget`), v0.0.31 (two-phase sync + ApplicationIdle dispatch), and the original `OnPropertyChanged(nameof(Label))` from the `TimeZoneId` setter all relied on Dragablz's tab strip honoring source-property changes. It doesn't, reliably. Tear-off and restart always worked because both create *fresh* containers where the `ItemTemplate`'s `{Binding Label}` reads the source from scratch — there's no cached binding to refresh.
- **The two events:**
  1. **Load** — when the `ItemTemplate` instantiates a header `TextBlock` for a tab (initial app start, new tab via `+`, tear-off into a floating window, drag back to the main window), the `{Binding Label}` reads the source freshly. Automatic, no code involved.
  2. **Dialog close** — `MainWindowViewModel.OpenTabSettingsCore` now calls a new `MainWindow.SetTabHeaderInAllDisplays(tab)` after `TabSettingsDialog` returns Save. That method walks **every Application window** (main + every torn-off `FloatingClockWindow`), finds every `TextBlock` tagged `Tag="TabHeaderText"` whose `DataContext` is the changed tab, and sets `Text` directly to `tab.Label`. No binding cascade, no timing-sensitive dispatch. Bypasses Dragablz's tab-strip rendering layer entirely.
- **The `Tag="TabHeaderText"`** attribute on the `ItemTemplate`'s `TextBlock` (added in `MainWindow.xaml` and `FloatingClockWindow.xaml`) identifies the right `TextBlock` even after the binding gets disconnected by an earlier direct `Text` set. It's the only thing that survives unchanged across both the binding-active and binding-disconnected states of the `TextBlock`.
- **`TabViewModel.TimeZoneId` setter no longer fires `OnPropertyChanged(nameof(Label))`.** Per the two-event rule, that cascade is off-spec. `Label`'s getter still computes the correct value when read, so the Load-case binding works.
- Removed `MainWindow.RefreshTabHeader` and `DoRefreshTabHeaderNow` (the v0.0.21..v0.0.31 lineage).
- `Trace.WriteLine` from `SetTabHeaderInAllDisplays` reports `[MainWindow] SetTabHeaderInAllDisplays("Kiev"): set N TextBlock(s) across M window(s)` — `N=0` would diagnose a regression immediately.

### Doc audit (per pre-merge rule)

- `README.md`, `Dialogs/HelpDialog.xaml`, `requirements.txt` — no change. None describe tab-header refresh internals; the user-facing experience is "the new city shows up in the header without tear-off, every time."

## [0.0.31] - 2026-04-30

### Fixed

- **Tab header still didn't reliably update when the user changed a tab's timezone in Settings** (Dan's v0.0.30 testing: "It does change on tearoff and it does reflect the correct city when restarted"). The v0.0.21 surgical refresh worked most of the time but was timing-sensitive — it ran synchronously the instant `TabSettingsDialog.ShowDialog()` returned, when the visual tree could still be mid-update from the dialog tearing down. Either the `TextBlock` walk found nothing or `UpdateTarget()` ran against a stale binding state.
  - *Solution:* Two-phase refresh. `RefreshTabHeader` now (a) calls the existing walk + `UpdateTarget()` synchronously to catch the common case, AND (b) queues a second pass via `Dispatcher.BeginInvoke(..., ApplicationIdle)` to run after every higher-priority dispatcher item has settled. By that point, the visual tree is guaranteed to be in its final post-dialog-close state. Cheap (one extra walk in the rare-case path), bulletproof.
  - Phase logged via `Trace.WriteLine`: `[MainWindow] DoRefreshTabHeaderNow(phase 1 (sync)): tab="Kiev", refreshed N TextBlock(s)` then `phase 2 (idle): refreshed N`. If the symptom recurs, the trace stream tells us whether phase 1 missed and phase 2 caught it (timing was the cause), or whether both refreshed 0 (deeper structural problem). (`MainWindow.xaml.cs`.)

### Doc audit (per pre-merge rule)

- `README.md`, `Dialogs/HelpDialog.xaml`, `requirements.txt` — no change. None describe tab-header refresh behavior; the user-facing experience after this fix is just "the new city shows up in the header without tear-off."

## [0.0.30] - 2026-04-30

### Changed

- **Inactive tab font size 13 → 9** (−4 pt). Per Dan: "make the inactive tabs city names 4 points smaller." Combined with the active tab at 19 / Bold (v0.0.29), the active tab is now roughly 2× the size of the inactive ones — selected vs. unselected reads at a glance. Applied to both `MainWindow.xaml`'s and `FloatingClockWindow.xaml`'s tab strips. Implementation: changed the base `FontSize` setter in the `DragablzItem` `ItemContainerStyle`; the `IsSelected=True` trigger continues to override to 19 when a tab is selected.

### Doc audit (per pre-merge rule)

- `README.md`, `Dialogs/HelpDialog.xaml`, `requirements.txt` — no change. None describe tab-strip typography.

## [0.0.29] - 2026-04-30

### Changed

- **Active-tab font size 17 → 19** (+2 pt from v0.0.28's setting). Now the active tab is 6 pt larger than inactive (base 13). Per Dan: "Add two more points to the active tab font." Applied to both `MainWindow.xaml` and `FloatingClockWindow.xaml` for consistency.

### Doc audit (per pre-merge rule)

- `README.md`, `Dialogs/HelpDialog.xaml`, `requirements.txt` — no change. None describe tab-strip typography.

## [0.0.28] - 2026-04-30

### Changed

- **Active tab is now Bold + 4 pt larger than inactive tabs** (font size 13 → 17 on the selected `DragablzItem`, font weight `SemiBold` → `Bold`). Per Dan's call. Combined with the existing white-background + darker-border styling, the selected tab now reads unambiguously even on a tab strip with several open. Applied to both `MainWindow.xaml`'s tab strip and `FloatingClockWindow.xaml`'s tab strip for consistency. (`MainWindow.xaml`, `FloatingClockWindow.xaml`.)

### Doc audit (per pre-merge rule)

- `README.md`, `Dialogs/HelpDialog.xaml`, `requirements.txt` — no change. None of them describe tab-strip typography.

## [0.0.27] - 2026-04-30

### Changed

- **Digital readouts on the analog faces and the prose-style digital faces default to 12-hour time with AM/PM** (was 24-hour). The encoder-style digital faces (Binary, Hex, Binary Digital) **remain 24-hour** — Dan's call: "that doesn't apply to Binary, Hex or Binary Digital. They will always show military time." For those themes the bit-width math (5b hour fits 0-23, 6b min/sec fit 0-59) is the point of the design and 24-hour keeps the encoding clean.
  - **`UpdateClock` shared format** flipped from `"HH:mm:ss"` → `"h:mm:ss tt"` — single-line change covers all six analog faces (Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight). Now reads `"10:08:42 AM"` / `"1:23:45 PM"`.
  - **Flip Clock** — already showed 12-hour values on the digit tiles via the existing `hour % 12` math; added `· AM`/`· PM` suffix to the `: SS SECONDS` line so the marker is unambiguous (Flip Clock's design intentionally shows HH:MM only on the cards, no room for AM/PM there).
  - **Marquee** — `"HH:mm:ss"` → `"h:mm:ss tt"` for the big glowing center text.
  - **Slab** — big time stays `h:mm` without AM/PM (the brutalist serif is intentionally minimalist; adding " AM" at font-100 wouldn't fit). AM/PM rides on the seconds line: `42″ AM`.
  - **Binary, Hex, Binary Digital** — unchanged from v0.0.26: still 24-hour (`HH:mm:ss` / `local.Hour:X2` / 5-bit hour).

### Added

- **`To12HourParts(DateTime)` helper** — single source of truth for the (hour-1-12, "AM"/"PM") split. Used by Flip Clock today; available for any future renderer that opts into 12-hour display.

### Doc audit (per pre-merge rule)

- `README.md` — no change. Time-format isn't called out as a feature.
- `Dialogs/HelpDialog.xaml` — no change. Help doesn't make claims about 12 vs 24 hour format.
- `requirements.txt` — § 1.1 already permits user override of time format (Auto / Hour12 / Hour24); the `TimeFormat` enum exists in `SettingsModel.cs` already. This commit doesn't expose the user-override UI yet — it just changes the per-face *default*. Encoder themes keep 24-hour as their hardcoded design intent. No spec change needed.

## [0.0.26] - 2026-04-30

### Removed

- **Right-click context menu on tab headers** (the menu with `Tab settings…` / `Close tab`) per Dan's call. The functionality remains fully reachable via:
  - Double-click tab header → Tab settings
  - Ctrl+, → Tab settings
  - Per-tab `✕` button on the tab header → close
  - `✕` overlay on the clock face → close the tab
  - The right-click menu was redundant with all of the above.
  - Implementation: dropped the `<Setter Property="ContextMenu">` block from the `DragablzItem` `ItemContainerStyle` in `MainWindow.xaml`. Removed the orphaned `TabContextSettings_Click` and `TabContextClose_Click` handlers from `MainWindow.xaml.cs`. Kept `TryFindTabFromContextMenuItem` — still used by the `?`-overlay menu (Themes / Help / About).

### Doc audit (per pre-merge rule)

- `README.md` — no change. The right-click menu wasn't called out as a feature.
- `Dialogs/HelpDialog.xaml` — `Edit a tab` mentioned right-click as one of three ways to reach Tab settings. Updated to drop that (kept double-click + Ctrl+,).

## [0.0.25] - 2026-04-30

### Documentation

Pre-merge doc-audit pass per the new standing rule (Dan, 2026-04-30): "update all documentation always for all projects before a merge and push."

- **`README.md` Features list** reorganized to separate shipped features from planned ones. Shipped: 12 themes, Chrome-style tabs with tear-off, multi-window, NIST sync (12h default, 6/12/24 selectable), live last-sync display, per-tab/per-machine settings persistence. Planned (explicitly called out, was previously claimed as shipped): always-on-desktop overlay (`requirements.txt` § 1.4), system tray, MSIX packaging (§ 2.7), Flip Clock + Marquee animation hooks.
- **`README.md` NIST/NTP usage notes** — the "service defaults to **hourly** sync" line was stale since v0.0.13 changed the default to 12 hours. Updated to mention the 6 / 12 / 24 user-selectable cadence too.
- **`Dialogs/HelpDialog.xaml`** — three sections updated:
  - "Edit a tab" — now notes the Settings dialog's two-section layout (THIS TAB + ALL CLOCKS ON THIS PC).
  - New "Pick a different theme" section — points users at the `?` overlay → Themes… gallery (added in v0.0.x but never made it to Help text).
  - "The time-sync service" — sync cadence updated to 12h default + 6/12/24 selector.
  - "Status bar" — "Last sync readout will surface in a future update" replaced with a description of the live readout (which actually shipped in v0.0.11) plus the +/− sign convention for clock corrections.
- **`design/README.md`** reviewed; no change (it describes design *intent*, current).

Standing version-bump rule: v0.0.24 → v0.0.25.

## [0.0.24] - 2026-04-30

### Fixed

- **Daylight (and Boulder Slate) date readout sat right-of-center.** Both themes place the date as bare canvas text (no enclosing panel). Build-time code created the `TextBlock`, called `Measure` on it *before* setting `Text`, then `Canvas.SetLeft(dateTb, Cx - DesiredSize.Width / 2.0)` — but `DesiredSize.Width` was 0 because the text wasn't set yet, so `Canvas.SetLeft` got `Cx - 0 = 200` (the dial center). When `UpdateClock` later wrote the actual text, it rendered starting at x=200 and extending rightward — visually offset from the dial center by half the text width. Got noticeable on Daylight after v0.0.22 widened the date format from `"ddd · MMMM d"` to `"ddd · MMMM d · yyyy"` (extra 8 chars).
  - *Solution:* New `_recenterDateReadoutOnUpdate` flag, set true by Boulder Slate and Daylight. `UpdateClock` re-measures and re-centers the `_dateReadout` after writing each tick's new text. Cost: one Measure call per tick on those two themes (cheap; the TextBlock is tiny). Themes that wrap their date in a `Border` panel (Atomic Lab, Aero Glass, Cathode, Concourse) skip the flag — the panel's `HorizontalAlignment="Center"` auto-centers the inner content. (`Controls/ClockFaceControl.xaml.cs`.)

## [0.0.23] - 2026-04-30

### Fixed

- **Right-click on a tab → "Tab settings…" menu item didn't open the Settings dialog.** Same handler reaches both the Tab settings and Close tab menu items; the original `TryGetTabFromContextMenuClick` helper walked from `MenuItem` → `LogicalTreeHelper.GetParent` → `ContextMenu` → `PlacementTarget` → `DataContext`. In some Dragablz scenarios that walk silently failed (returns null at one of the steps; handler quietly does nothing) — this is the same family of "context menu in a Setter.Value behaves subtly different from when declared inline" issue that bit the v0.0.16 RefreshTabHeader walk.
  - *Solution:* Renamed and rewrote the helper as `TryFindTabFromContextMenuItem`, which now tries three strategies in order:
    1. `MenuItem.DataContext` directly — works when DataContext propagates from the ContextMenu down to the MenuItem (which is the normal WPF behavior).
    2. Walk up to ContextMenu, then `PlacementTarget.DataContext` — the original strategy.
    3. `ContextMenu.DataContext` directly — sometimes set even when PlacementTarget isn't.
  - Each click now logs the outcome via `Trace.WriteLine` ("strategy 1: MenuItem.DataContext" / "strategy 2: PlacementTarget=..." / "all strategies failed"), so any future regression is immediately diagnosable without code changes. Old `TryGetTabFromContextMenuClick` name kept as a thin alias so the `?`-overlay handlers (Themes / Help / About) keep compiling. (`MainWindow.xaml.cs`.)

## [0.0.22] - 2026-04-30

### Changed

- **All twelve clock faces now show day-of-week, day-of-month, month, AND year.** Audit triggered by Dan: "Binary clock does not have day of week, date of month, month, year. Go through all faces, make sure that they are consistent on those four elements."
  - **Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight** (6 analog) — shared `UpdateClock` date format upgraded from `"ddd · MMMM d"` → `"ddd · MMMM d · yyyy"`. One-line change covers all six. (e.g., `WED · APRIL 30 · 2026`.)
  - **Flip Clock** — same upgrade in its `_digitalUpdater` closure (it has its own `dateTb`, not the shared `_dateReadout`).
  - **Marquee** — was missing all four. Added a date line below the "★ ATOMIC TIME ★ FROM BOULDER ★" subtitle at y=308 in the same Bebas Neue typeface but smaller (font 12 vs 13 for the subtitle). Format `WED · APRIL 30 · 2026`.
  - **Slab** — already had all four (`"dddd · d MMMM yyyy"` → `SATURDAY · 30 APRIL 2026`). No change.
  - **Binary** — was missing all four. Added a date line at y=90 between the "BINARY CLOCK" title and the LED grid, in the same dim-red palette. Format `WED · APRIL 30 · 2026`.
  - **Hex** — already had dow/dom/month encoded as hex ASCII; was missing year. Added a `// year: 32 30 32 36 (2026)` line between month and the day-fraction section. Other content shifted down 6 px to accommodate.
  - **Binary Digital** — already had dow/dom/month encoded as 8-bit binary ASCII; was missing year. Added a `// yr: 00110010 00110000 00110010 00110110 (2026)` line between month and the widths annotation. Other content shifted down 8 px to accommodate.
  - All four elements (day name, day-of-month, month name, year) now appear on every face. Each theme's typography stays in character (full vs abbreviated month names, encoded vs plain text) — only the data set is now consistent.

## [0.0.21] - 2026-04-30

### Fixed

- **Tab header still didn't refresh after Settings save (regression / latent bug exposed once single-click was fixed in v0.0.20).** v0.0.16 added `RefreshTabHeader` to fix this, but it had a subtle bug: the visual-tree walk started from `MainTabs.ItemContainerGenerator.ContainerFromItem(tab)` — the `DragablzItem` container — and Dragablz renders the tab-strip header in a *separate* visual subtree (a tab-strip panel that's a sibling of the items panel, not a descendant of the container). The walk never found the header `TextBlock`, `UpdateTarget()` was called on nothing, and the function quietly did nothing. The bug had been latent through v0.0.16–v0.0.20 because the original symptom was masked by the click-routing bug (Dan couldn't easily reproduce because tab clicks themselves were broken).
  - *Solution:* Widen the visual-tree walk to start from `MainTabs` itself (the whole `TabablzControl`), so both the items panel AND the tab-strip panel are traversed. Filter by `DataContext == tab` so we refresh only the changed tab's header — multiple `TextBlock`s share the same `{Binding Label}` ItemTemplate (one per tab) and we don't want to disturb the others. Also added `Trace.WriteLine` at start + with the refresh count, so any future regression is visible in the trace stream. (`MainWindow.xaml.cs` `RefreshTabHeader`.)

## [0.0.20] - 2026-04-30

### Fixed

- **Tab single-click selection unreliable while double-click always worked.** Dan's diagnostic observation: "It misses the single click, but always picks up on the double click. They must go through different handlers." Spot-on — and changes the diagnosis from a load problem to a routing problem.
  - *Root cause:* Single-click selection went through Dragablz's intrinsic click/drag classifier, which appears to misclassify short clicks as drag-starts that never complete (mouse-up arrives before the drag threshold is exceeded, but the classifier doesn't fall back to "this was a click — select the tab"). Double-clicks went through WPF's `MouseDoubleClick` event handled by our own `TabItem_DoubleClick` (a separate code path that bypasses Dragablz's classifier), so they were unaffected. Earlier commits — Width=0 Thumb (v0.0.4), pause hidden-tab timers (v0.0.18), `ApplicationIdle` priority (v0.0.19) — all addressed *load* hypotheses; this is a routing one and they didn't help (though we keep them as defense in depth).
  - *Solution:* New `TabItem_PreviewMouseLeftButtonDown` handler hooked via `EventSetter` in the `DragablzItem` `ItemContainerStyle`. `Preview` events tunnel DOWN through the visual tree before the regular bubbling-up event reaches Dragablz's classifier — so the handler fires *first*. It sets `MainWindowViewModel.SelectedTab` to the clicked item unconditionally. We deliberately do **NOT** mark `e.Handled = true` so Dragablz still receives the event and can run its drag-tear gesture detection on subsequent `MouseMove`. Same pattern mirrored into `FloatingClockWindow` for torn-away tabs. (`MainWindow.xaml`, `MainWindow.xaml.cs`, `FloatingClockWindow.xaml`, `FloatingClockWindow.xaml.cs`.)

## [0.0.19] - 2026-04-30

### Fixed

- **Tab clicks still intermittent (1-5 needed) on three open tabs after v0.0.18.** v0.0.18 paused timers on hidden tabs (cut load from N×20Hz to 1×20Hz), but the visible tab's tick handler was still doing enough per-frame work — especially on heavy digital themes (Hex sets 7+ TextBlock.Text properties + creates a SolidColorBrush per tick; Binary mutates 20 ellipse Fills + Effects per tick) — to keep the dispatcher busy enough that mouse-down/up routing through the tab strip got disrupted, and `DispatcherTimer.Background` (the default priority) was just one tier below `Input` so a tick already in progress would block click delivery until the tick completed.
  - *Solution:* Drop `DispatcherTimer` priority from default `Background` to `ApplicationIdle` — one tier lower than Background, two tiers below Input. The timer now fires *only when the UI thread is genuinely idle*; any pending input event always pre-empts the clock-face redraw. Single tab clicks register first-try.
  - *Trade-off:* under heavy UI activity, ticks can be skipped. For a clock this is benign — `UpdateClock` reads `DateTime.UtcNow` afresh on every fire, so a skipped tick just means the next visible frame jumps ahead a few ms. No drift, no integration error.
  - Per Dan's framing: he asked for the "wait for other events" pattern from his fast-loop work — `ApplicationIdle` priority is the WPF-native expression of that idea.
  - (`Controls/ClockFaceControl.xaml.cs` `OnLoaded`.)

## [0.0.18] - 2026-04-30

### Fixed

- **Tab clicks intermittent — sometimes 5–10 clicks needed to switch tabs.** Persistent symptom from v0.0.4 onward; Width=0 on the Dragablz Thumb fixed click-stealing on the active tab but didn't fix the underlying load.
  - *Root cause:* Dragablz's `TabablzControl` keeps non-selected tabs **loaded but visually collapsed** — `Unloaded` doesn't fire on tab switch. So with four open tabs, four `ClockFaceControl` instances each ran their own `DispatcherTimer` at 20 Hz (smooth analog) or 1 Hz (stepped/digital). Total: ~80 dispatcher ticks per second doing measure/arrange/text work, even though three of them paint into hidden visual subtrees the user can't see. `DispatcherTimer` defaults to `Background` priority which is supposed to yield to `Input` events, but with the dispatcher queue this full, mouse-down/mouse-up routing through the tab strip got disrupted enough that single clicks landed unreliably.
  - *Solution:* `ClockFaceControl` now subscribes to `IsVisibleChanged`. When the tab becomes invisible (Dragablz tab switch), the per-clock timer stops. When it becomes visible again, the timer restarts AND `UpdateClock` runs immediately so the freshly-shown frame is already current (otherwise a 50 ms-to-1 s window of stale display, and the v0.0.17 self-heal would only catch a theme mismatch on the next tick). Cuts dispatcher load from N timers × 20 Hz to 1 timer × 20 Hz. (`Controls/ClockFaceControl.xaml.cs`.)
  - *Compat:* `OnLoaded`/`OnUnloaded` retained — they cover the case where a tab is fully removed (closed, or app shutdown). `IsVisibleChanged` covers the visible/hidden transitions while still loaded. Both paths are safe to fire and the timer's null-check + Start/Stop are idempotent.

## [0.0.17] - 2026-04-30

### Fixed

- **Tab 2 theme/render mismatch ("shows Flip Clock with Binary Digital selected") closed via self-healing reconciliation in the tick loop.**
  - *Background:* Carry-over symptom from v0.0.7-era reproduction. `tabVm.Theme` says `BinaryDigital` (Tab Settings dialog confirms), but the dial is painted with Flip Clock visuals. Three candidate root causes were considered: (1) `OnThemeChanged` not firing during a `DataContext` swap in container recycling — v0.0.9 dropped the `IsLoaded` guard which addressed one such race but couldn't be proven to address all; (2) `RenderActiveTheme` throwing silently — possible under the pre-v0.0.16 unhandled-exception code path; (3) a late-binding update missing `OnPropertyChanged`. Couldn't isolate which one in repro from a code read.
  - *Solution — defense in depth, not race-hunting:* New `_lastRenderedTheme` field tracks what the dial was last actually painted with. `UpdateClock` (the dispatcher tick — 50 ms / 1 s depending on smooth-vs-stepped) compares the current `Theme` DP value against `_lastRenderedTheme` and, if they differ, re-renders before computing hand positions. Cost is one enum compare per tick; benefit is that **any path that produced the mismatch self-corrects within one tick**. `RenderActiveTheme` stamps `_lastRenderedTheme` *before* the build-switch using a captured `requested` value, so a re-entrant theme change mid-build can't desync the field, and a thrown `Build*` doesn't leave the heal logic looping forever on an unrenderable theme.
  - *Diagnostic instrumentation:* Added `Trace.WriteLine` (works in Release, unlike `Debug.WriteLine`) at `OnThemeChanged` and at the top of `RenderActiveTheme`. If the symptom recurs, the trace stream shows whether the DP changed and whether render fired — narrows the root cause without re-enabling the visible debug overlay.
  - *Debug overlay still on for verification.* The `theme: <name>` text at the bottom of every face stays in place this round so Dan can confirm the fix on F5. Comes off in v0.0.18 once verified. (`Controls/ClockFaceControl.xaml.cs`.)

## [0.0.16] - 2026-04-30

### Added

- **Top-level exception handling done correctly.** v0.0.15's handler was subscribed in `OnStartup` *after* `base.OnStartup` — too late, since `base.OnStartup` is what processes the `StartupUri="MainWindow.xaml"` and constructs MainWindow. Anything that threw during MainWindow's XAML parse, `InitializeComponent`, or `Loaded` handler escaped before our handler was hooked, and the process exited silently.
  - Moved subscription to `App`'s constructor — runs before any WPF code, including before `base.OnStartup`. Now everything from MainWindow construction onward is covered.
  - Added two more escape hatches: `AppDomain.CurrentDomain.UnhandledException` (non-dispatcher-thread exceptions — background tasks, finalizers, native callbacks) and `TaskScheduler.UnobservedTaskException` (fire-and-forget Tasks like `IpcClient.TryFetchLastSyncAsync` invoked via `_ = ...`). All three handlers funnel into a single `ShowExceptionDialog` that pops a `MessageBox` with exception type + message + first 6 stack frames. Non-fatal handlers also keep the app running (`e.Handled = true` / `e.SetObserved()`). (`App.xaml.cs`.)
  - If this had been in place earlier, v0.0.14's silent crash would have surfaced as a visible `MessageBox` showing `NotImplementedException: Replace not implemented yet` from Dragablz, and Dan wouldn't have needed Event Viewer at all.

### Fixed

- **Tab header now refreshes after changing TZ in Settings — without crashing.** v0.0.13's bug returns: PropertyChanged on `TabViewModel.Label` fires correctly, but Dragablz's TabablzControl in this version doesn't propagate it to the rendered tab strip; tearing the tab off was the only thing that updated it. v0.0.14 tried to force a re-template via `Tabs[idx] = tab` (`CollectionChanged.Replace`) and crashed with `Dragablz.TabablzControl.OnItemsChanged: NotImplementedException — Replace not implemented yet`.
  - *Solution:* `MainWindow.RefreshTabHeader(tab)` walks the existing tab container's visual tree, finds the `TextBlock` whose `Text` binding targets `Label`, and calls `BindingExpression.UpdateTarget()` to force the binding to re-pull from source. No collection mutation, no container recycling, no Dragablz `Replace` path. Followed by `UpdateLayout()` on the container so a longer label (e.g., "UTC" → "Europe/Kiev") doesn't get clipped at the old width.
  - `OpenTabSettingsCore` now calls this after the dialog returns Save instead of touching the `Tabs` collection. (`MainWindow.xaml.cs`, `ViewModels/MainWindowViewModel.cs`.)

### Known dev-env issue (punted)

- Running the Service from `dotnet run` (or VS multi-project F5) under your user account — even elevated — fails with `SetSystemTime: Win32 error 1314 (ERROR_PRIVILEGE_NOT_HELD)`. The Service queries NIST successfully but can't apply the correction because `SE_SYSTEMTIME_NAME` isn't enabled in the user-account token even when admin. Production install via `ServiceInstaller` runs as `LocalSystem` and is unaffected. Fix would be `AdjustTokenPrivileges` in `SystemTime.cs` to enable the privilege; deferred per Dan's call.

## [0.0.15] - 2026-04-30

### Fixed (regression from v0.0.14)

- **Silent process exit when changing a tab's timezone in the Settings dialog (Release build).** v0.0.14 added `Tabs[idx] = tab` to `OpenTabSettingsCore` as a workaround for the tab-header-not-refreshing bug — assigning a tab back to its same `ObservableCollection` slot fires `CollectionChanged.Replace`, which was supposed to make Dragablz re-template the header in place. It worked in Debug but in Release the path threw an unhandled exception and the process vanished without surfacing anything visible to the user. The `Replace`-on-the-currently-selected-item case isn't handled gracefully by Dragablz's container management. **Reverted v0.0.14**: dropped the `Tabs[idx] = tab` line and removed the matching `Replace` early-return in `OnTabsCollectionChanged`. The original tab-header-not-refreshing bug returns (label only updates after tear-off-and-back, or after restarting the app); next workaround attempt will skip the collection mutation entirely. (`ViewModels/MainWindowViewModel.cs`.)

### Added

- **`DispatcherUnhandledException` handler in `App.OnStartup`.** v0.0.14's silent crash was invisible because no debugger was attached and Release WPF doesn't pop a default crash dialog. Now any unhandled UI-thread exception surfaces a `MessageBox` showing exception type + message + first 6 stack frames, and marks `e.Handled = true` so a single recoverable mishap doesn't take the app down. Full stack still goes to `Debug.WriteLine` for anyone attached. (`App.xaml.cs`.)

### Carried over from v0.0.13 (still pending)

- **Tab header doesn't refresh after changing TZ in Settings.** Workaround: tear the tab off and back. Permanent fix queued — next attempt will use `BindingExpression.UpdateTarget()` on the bound `TextBlock` directly (no collection mutation, no Dragablz re-template).
- Sync-frequency dropdown (`v0.0.13`) and live last-sync display (`v0.0.11`) still un-merged on this branch.

## [0.0.14] - 2026-04-30

### Fixed

- **Tab header didn't show the new city after changing the timezone in Settings — only tearing the tab off into a floating window made the new label appear.**
  - *Problem:* `TabViewModel.TimeZoneId` setter raises `PropertyChanged` for `Label`, and the main window's `ItemTemplate` is `<TextBlock Text="{Binding Label}"/>` — a standard reactive binding that *should* refresh. Tearing off worked because the floating window re-templated the item in a fresh container, evaluating `Label` from scratch. The original `TabablzControl` in this Dragablz version apparently caches its rendered header per item and doesn't re-read on `PropertyChanged`. Reproducible on every new tab created via the `+` button (and likely on existing tabs too — same code path).
  - *Solution:* After the Settings dialog returns Save, `MainWindowViewModel.OpenTabSettingsCore` now assigns the edited tab back to its same slot in the `Tabs` `ObservableCollection` (`Tabs[idx] = tab`). That fires `CollectionChanged.Replace`, which WPF treats as "re-template this item" — exactly the kick Dragablz needs. `OnTabsCollectionChanged` short-circuits on `Replace` so persistence (`_settings.Tabs` ordering, `settings.json`) is unaffected. The earlier observation about analog faces' tab labels working "by accident" probably came from the load-from-disk path naturally re-templating during initial render — the bug only surfaced for in-session edits. (`ViewModels/MainWindowViewModel.cs`.)

## [0.0.13] - 2026-04-30

### Added

- **Sync-frequency dropdown in the Settings dialog** with three choices: Every 6 hours / Every 12 hours / Every 24 hours. Lives in the existing per-tab Settings dialog under a new clearly-labeled `ALL CLOCKS ON THIS PC` section so users know it's machine-wide and not per-tab. Persists to `%ProgramData%\ComTekAtomicClock\service.json`; the Service re-reads on each sync loop iteration (no restart needed).
  - *Why this dialog:* avoids adding a new "Preferences" entry point on the `?` overlay menu (per Dan: "I don't want to add a new settings button"). The two scopes are visually separated by section headings (`THIS TAB` vs `ALL CLOCKS ON THIS PC`) and a one-sentence subtext on the machine-wide section reinforces that the choice applies to every tab and to the system clock itself.
  - *No IPC needed:* `ServiceInstaller` already grants Authenticated Users `Modify` on `%ProgramData%\ComTekAtomicClock\` (an existing ACL grant from the install flow), so the unprivileged UI process can write `service.json` directly. The Service's `SyncWorker.LoadIntervalFromConfig` already re-reads on every loop iteration and clamps to `[15 min, 24 hr]` per `requirements.txt § 1.5`. End-to-end change is UI-only.
  - *Round-trip behavior:* on dialog open, the stored interval is matched against the three offered choices and the closest is pre-selected — so a power user who hand-edited `service.json` to 1 hour sees the dropdown sit on `Every 6 hours` (the closest of the three). Saving overwrites with the chosen canonical value.
  - *Failure mode:* if the directory doesn't exist yet (Service never installed) or write is denied, per-tab fields still save and a non-blocking warning explains why the machine-wide save was skipped. Will retry next time the dialog opens.

### Changed

- **Settings dialog title `Tab settings` → `Settings`**, since it now mixes per-tab and machine-wide scopes. The menu items that *open* the dialog (double-click tab header, right-click → `Tab settings…`, Ctrl+,) keep their wording — the gesture is still per-tab. (`Dialogs/TabSettingsDialog.xaml`.)
- **Default `ServiceConfig.SyncInterval` 1 hour → 12 hours.** Matches the new dropdown's middle option. Existing installs that already have a `service.json` keep whatever interval they had; only fresh installs (or installs where `service.json` is absent) pick up the new default. Hourly is still a valid hand-edited value and the Service honors it. (`Shared/Settings/SettingsModel.cs`.)

### Architecture note for future readers

The dialog now writes two files on Save: `%APPDATA%\ComTekAtomicClock\settings.json` (per-user, per-tab settings via the existing `TabViewModel` path) and `%ProgramData%\ComTekAtomicClock\service.json` (machine-wide, via `SettingsStore.SaveServiceConfig`). Cancel discards both. The two writes are independent — if the machine-wide write fails, per-tab still succeeds — which is intentional since the failure modes are different (machine-wide can fail when the Service isn't installed; per-tab can't).

## [0.0.12] - 2026-04-30

### Changed

- **Clock-face brand text "ComTek" → "ComTekGlobal"** on the three faces that paint the wordmark. Case style preserved: all-caps on the Flip Clock badge (`COMTEK · MODEL CT-1971` → `COMTEKGLOBAL · MODEL CT-1971`), lowercase on the Hex and Binary Digital terminal title bars (`comtek :: hex_clock.exe` → `comtekglobal :: hex_clock.exe`, `comtek :: bin_clock.exe` → `comtekglobal :: bin_clock.exe`). Window titles, dialog titles, service identifiers, namespace names, and the user-facing copyright line in `AboutDialog` are unchanged — Dan's request was scoped to clock-face text. (`Controls/ClockFaceControl.xaml.cs`.)

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
