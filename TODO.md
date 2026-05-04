# ComTekAtomicClock — TODO / Backlog

Single source of truth for open work. Pulls together what was scattered across `CONTEXT.md` "Pending" and `SPEC.md` §21 "Planned." Keep this current as items land or new ones appear.

| | |
|---|---|
| **Last refreshed** | 2026-05-03 |
| **Current version** | v1.0.0 |
| **Open items** | ~38 (2 active queue + 5 Phase 2 + 29 Phase 3+ + 3 doc cleanups) |

> Companion documents:
> - `SPEC.md` — authoritative point-in-time spec; every TODO item has a §-reference back into the spec.
> - `CONTEXT.md` — running decisions / constraints / gotchas log.
> - `CHANGELOG.md` — per-version problem/solution history.

---

## Active queue (highest priority — Dan's direct asks)

| # | Item | Target | Notes |
|---|---|---|---|
| 1 | **CaptJohn theme** — Captain John's Marina (Pirate Bar, Rio Dulce). Lazy "Hora Chapín" jitter mode (random ±3 min, sync at top of hour) + momentary flash demos at noon/5pm. Parchment + brass + bordeaux palette. Logo at 40%, Cinzel numerals, Monotype Corsiva caption "The Busted Flush", Jolly Roger overflow menu. | **v1.1.x** | **Menu UX semantics (per Dan, 2026-05-03):** `Hora Chapín` is a **persistent mode** — checkbox toggle; stays as set after the menu closes; renders the lazy jitter hand + faint real hands (or, if unchecked, a regular numberless clock). `Almuerzo` and `Fini` are **momentary demo buttons** — clicking either runs the demo flash animation **only while the menu is open**; closing the menu (clicking anywhere outside the popout) ends the demo and returns to whatever live mode Hora Chapín is currently set to. They are NOT radios — should not show selection state after the menu closes. Suggested rendering: text-link or button-styled rows (no bullet) so the user reads them as actions, not selectable options. Mockups locked in `windows/design/themes/captjohn-mockup-*.png` (the radio-bullet rendering shown in the menu-open mockup is **out of date** — needs update to button-style rows during wire-up). Asset: `JohnsMarina-logo.jpg`. Font: `windows/design/fonts/Cinzel-Variable.ttf` (OFL, 125 KB). Wire-up requires: `Theme.CaptJohn` enum entry; `BuildCaptJohn` renderer; mode-state machine for Hora-ON/OFF (persistent) + Almuerzo / Fini momentary demos (active only while menu open); Jolly Roger overlay button + popout panel in `ClockFaceControl`; per-tab persistence of `HoraChapinOn` flag (NOT the demo state); 5 s flash cadence; custom hand-length deltas (hour 66, min 152) inherited from the v1.1.0 length pass. **SPEC.md §10 placement: group with the analog cluster — insert as Theme #7 after Daylight, ahead of the digital-only group (Flip Clock, Marquee, Slab, Binary Digital). Bumps the analog count from 6 → 7 and the total from 12 → 13.** **Two overlay buttons (per Dan, 2026-05-03):** (1) **`⋯` standard app-overflow menu in the TOP-RIGHT** — Settings / Themes / Help / About — same `MoreHorizontal20` glyph the rest of the app uses on its overflow menus (matches `FloatingClockWindow` v0.0.35 pattern); (2) **Jolly Roger ☠ button in the BOTTOM-LEFT** — theme-specific Hora Chapín toggle + Almuerzo / Fini demo triggers. The two are distinct: the top-right is "app stuff" (per all themes); the bottom-left is "Captain John's stuff" (CaptJohn-only). The bottom-left mockups have the Jolly Roger; the top-right `⋯` button is **not yet shown in the mockups** — needs to render at `(376, 24)` as a 22-px circular button using the existing `OverlayGlyphBrush` (white on dark themes, near-black on light) for parity with the other 12 themes. |
| 2 | **"Custom" theme — user-uploaded background image** | tbd (post-v1.1) | Generalizes the CaptJohn pattern: any user image becomes the dial backdrop. UX: theme picker has a "Custom…" entry; picking it opens an OpenFileDialog (.jpg / .png / .bmp); selected image is auto-padded with white so the diagonal fits the inscribed circle; saved to `%APPDATA%\ComTekAtomicClock\custom-bg\<tab-guid>.png`; rendered at user-tunable opacity (default 40%) under standard ticks/hands. Per-tab — each tab can have its own custom backdrop. Need: `Theme.Custom` enum value; `TabSettings.CustomBackgroundPath : string?` + `TabSettings.CustomBackgroundOpacity : double` (0..1); image-prep helper (load → measure diagonal → pad to square → save); BuildCustom renderer that loads from disk and composites; opacity slider + "Pick image…" button in the Tab Settings dialog. Edge cases: missing file (theme falls back to plain face), oversized image (downscale to 600 px max diagonal), HEIC / WebP (skip; pop a "JPG/PNG/BMP only" toast). |
| 3 | **Timer mode** | tbd (post-v1.1) | Stopwatch / elapsed-time per tab/window, alongside the always-on clock. New `TabSettings.Mode` enum (`Clock` / `Timer` / `Countdown`), per-mode renderer paths in `ClockFaceControl`. Atomic Lab is the natural anchor; other themes opt in. |
| 4 | **Countdown mode** | tbd (post-v1.1) | User sets a target duration; face counts down. Same `Mode` enum extension. Needs an alarm/notification when zero hits. |

---

## Phase 2 — magnetic snap floating windows

Estimated 280–390 LOC, 1–2 days. Design notes in `CONTEXT.md` "Why we dropped Dragablz" + Phase-2 sketch.

| # | Item |
|---|---|
| 3 | `Behaviors/WindowSnap.cs` — `HwndSource.AddHook` on `WM_MOVING` / `WM_WINDOWPOSCHANGING`; mutate proposed RECT to snap when within ~12 px of another `FloatingClockWindow` edge |
| 4 | `Services/SnapGroupRegistry.cs` — track which windows are currently snap-grouped |
| 5 | Visual feedback during drag — edge highlight or ghost outline when a snap is imminent |
| 6 | "Enable snap" toggle in Settings, default OFF until proven stable |
| 7 | `WindowSettings.SnapGroupId : Guid?` + `SnapEdge : enum` model fields so groups reconstitute on app restart |

---

## Phase 3+ — larger features still Planned

### Tray (audit S-1)

| # | Item |
|---|---|
| 8 | System tray icon: open / sync now / last status / settings / exit |
| 9 | Closing last window → minimize to tray (vs. current `Application.Shutdown`) |

### Floating windows

| # | Item |
|---|---|
| 10 | Position persistence (X/Y/W/H across app restart) |
| 11 | `Window.Topmost` bound to `WindowSettings.AlwaysOnTop` |
| 12 | Desktop overlay mode — borderless, transparent, click-through, desktop layer |

### Sync flow

| # | Item |
|---|---|
| 13 | Confirm-large-offset toast `[Apply][Skip]` with 30 s timeout (audit S-4 / D-5 / D-13) |
| 14 | True `SyncNowRequest` force-resync (currently returns cached snapshot — D-5) |
| 15 | Sync server selector UI (named NIST + NTP.br pool members) + `IsKnownHost` validation (S-22 / D-7) |

### Dialog UI fields whose model exists but has no UI yet

| # | Item |
|---|---|
| 16 | Tab rename input (`TabSettings.Label` exists; no input control) |
| 17 | Time format selector (Auto / 12h / 24h) — `TimeFormatMode` enum exists |
| 18 | Show-digital-readout toggle for analog themes — `ShowDigitalReadout` exists |
| 19 | Second-hand motion override (ThemeDefault / Smooth / Stepped) — `SecondHandMotion` exists |
| 20 | Five-slot color overrides (Ring / Face / Hands / Numbers / Digital) + HSV wheel / RGB sliders / hex input / eyedropper / "Reset colors" link — `ColorOverrides` POCO exists |
| 21 | "Use the same theme across all tabs" toggle + propagation logic — `UseSameThemeAcrossAllTabs` exists |
| 22 | "Start with Windows" toggle + shell-startup integration |
| 23 | "Confirm large sync corrections" toggle |
| 24 | Telemetry opt-in toggles + endpoint + consent dialog + scrubbing pipeline |

### Renderer

| # | Item |
|---|---|
| 25 | 24-hour numeral-flip on analog faces (1–11 ↔ 13–23 across noon, position 12 fixed) — S-15 / D-9 |
| 26 | Five-slot color overrides actually applied per theme (renderers ignore today) — D-11 |
| 27 | "SERVICE NOT RUNNING" on-face warning text in Ring color — D-12 |
| 28 | Card-flip animation on Flip Clock (TODO comment in `ClockFaceControl`) |
| 29 | Chase-bulb wave animation on Marquee (TODO comment in `ClockFaceControl`) |

### Plumbing / catalog / packaging

| # | Item |
|---|---|
| 30 | Full IANA tz catalog (~600 zones, currently ~140 from Windows zones) — D-18 |
| 31 | Settings schema version-detect-and-migrate code path |
| 32 | MSIX + `.appinstaller` + GitHub Pages publish pipeline |
| 33 | ARM64 build target + RID config |
| 34 | Service start mode → `auto` for true headless operation (currently `demand`, alpha simplification) — D-1 / D-4 |
| 35 | Service-installer ACLs → `WellKnownSidType.InteractiveSid` (currently `AuthenticatedUserSid`) — D-19 |
| 36 | **Authenticode code-signing** of Setup.exe + bundled exes — kills the SmartScreen "unknown publisher" warning |

### Privacy / docs

| # | Item |
|---|---|
| 37 | `PRIVACY.md` + TLS-pinned telemetry endpoint + scrubbing pipeline — pre-public-release blocker per legacy spec § 2.11 |
| 38 | Help corpus refresh — remove the right-click → Close tab line (was removed in v0.0.26 but help text still mentions it) |

---

## Tiny doc/code follow-ups (cheap polish, can land alongside any commit)

| # | Item |
|---|---|
| 39 | `ThemeCatalog.cs:32-34` comment misclassifies the 12-theme grouping ("3 digital, 2 specialty, 1 binary digital"). Correct: "6 analog + 4 digital-only + 2 encoder" |
| 40 | `MainWindow.xaml:238-244` comment claims "base FontSize=13" but actual setter is 9; v0.0.32 didn't update the rationale comment |
| 41 | `HelpDialog.xaml:72` mentions right-click → Close tab — that menu was removed in v0.0.26 |

---

## Recently shipped (so a future session sees what's done)

| Item | Resolved in |
|---|---|
| Time-source picker (Boulder + Brazil) + per-face source label + dynamic NIST badge | v0.0.36 |
| Dragablz dropped → native WPF TabControl; tear-away removed | v0.0.33 |
| Tab right-click ContextMenu removed; right-click → Settings dialog directly | v0.0.34 |
| Toolbar contrast fix (`+ New tab` / `+ New window` buttons) | v0.0.34 |
| Floating window single `⋯` overlay button replaces stacked `✕` + `?` pair | v0.0.35 |
| TabSettingsDialog Height +100 px to accommodate Time Source group | v0.0.37 |
| Daylight + Boulder Slate readout alignment fix (date and time both recenter on each tick) | v0.0.38 |
| Settings + Themes dialog Owner — center over originating window, not always MainWindow | v0.0.39 |
| Inno Setup installer (`tools/installer.iss`) | (build tooling commit `1ea3ddc`) |
| Self-contained release zip + Setup.exe | v0.0.36 release tooling |
| First-stable-release version stamp (1.0.0) | v1.0.0 |

---

## How to update this doc

When you ship an item, **move it from the open list above to the "Recently shipped" table** with the version it landed in. Don't just delete — keeping the resolved trail gives a future session immediate context.

When you discover a new item, add it to the appropriate section. Tiny polish items go in the §"doc/code follow-ups" section. Larger features go in the right Phase bucket.

When you start working an item, leave it in place; the CONTEXT.md session log will record you've started.
