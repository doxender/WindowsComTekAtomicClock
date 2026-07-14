I have a thorough picture now. Let me produce the audit report.

# Code vs Spec — ComTekAtomicClock Audit

## Summary

The spec describes a feature-rich v1 (system tray, desktop overlay, color overrides, large-offset confirmation toast, MSIX packaging, telemetry, etc.); the code is a working alpha (v0.0.32, the `<Version>` in csproj) implementing the core: 12 face themes, tabbed time-zones, named-pipe IPC, Worker Service NIST sync, and a privileged installer helper. The biggest categorical gaps: per-theme color overrides aren't wired (only `ColorOverrides` POCO exists; renderers ignore them), the Sync server / Confirm-large-offset / Show-digital-readout / Time-format / Second-hand-motion override / Use-same-theme toggles are all spec'd but missing from the Settings UI, and there is no system tray, no desktop overlay, no toast confirmation, no telemetry, no MSIX. The spec contradicts itself on theme count ("eleven" in the prose vs. twelve themes named in the table) and on default sync interval (spec § 1.5 says hourly; the in-code default for `service.json` is 12 h and the only Settings dialog choices are 6/12/24 h). A handful of strings/scopes drift (Service start mode `demand` vs. spec `auto`; SDDL grants Authenticated Users, not Interactive Users; many themes show a date line the spec doesn't ask for; the Marquee theme has no time-format awareness, etc.).

---

## Drift (code disagrees with spec)

### D-1 · Service start mode is `demand`, spec says `auto`

- Spec § 1.6 (line 233): *"Service start mode: Automatic."*
- Spec § 1.9 (line 327–330): the helper command line is documented as `sc.exe create … start= auto`.
- Code: `ComTekAtomicClock.ServiceInstaller/Program.cs:281` uses **`start= demand`**, with the explicit comment *"on-demand start mode (NOT auto-start at boot). The UI starts the service on app launch and stops it on app exit."*
- App enforces this lifecycle: `App.xaml.cs:62 TryStartService()` on `OnStartup`, `:67 TryStopService()` on `OnExit`.

### D-2 · Default sync interval is 12 h (and 1 h is not even an option), spec says 1 h with 15 min – 24 h range

- Spec § 1.5 (line 187): *"Default sync interval: hourly. User-configurable between 15 minutes and 24 hours."*
- Spec § 1.10 (line 351): *"Sync interval = hourly. Sync server = `time.nist.gov`."*
- Code split-brain:
  - `Shared/Settings/SettingsModel.cs:170` — `GlobalSettings.SyncInterval = TimeSpan.FromHours(1)` (matches spec).
  - `Shared/Settings/SettingsModel.cs:247` — `ServiceConfig.SyncInterval = TimeSpan.FromHours(12)` with comment *"Default 12 hours (the middle of the three choices we offer in the Settings dialog: every 6 / 12 / 24 hours)."*
  - `Shared/Settings/SettingsStore.cs:143` first-run `CreateDefaultAppSettings` writes `SyncInterval = TimeSpan.FromHours(1)`.
  - `Service/Sync/SyncWorker.cs:206-209` clamps to `[15 min, 24 h]` (matches spec).
  - `Dialogs/TabSettingsDialog.xaml:146-148` — only 6/12/24 h are user-selectable; no 15-min, 30-min, 1-h, or other choices.

### D-3 · Pipe ACL grants Authenticated Users, spec says Interactive users

- Spec § 2.4 (line 391): *"Authentication: pipe ACL restricts access to interactive users on the local machine."*
- Code: `Service/Ipc/IpcServer.cs:126-130` adds `WellKnownSidType.InteractiveSid` to the pipe ACL — matches spec.
- But the **service ACL** in `ServiceInstaller/Program.cs:296-299` (set via `sc.exe sdset`) grants:
  - `AU` = **Authenticated Users**, not Interactive.
  - The comment confirms this is intentional (so the unprivileged UI can `sc.exe start`/`stop` across logon sessions). The pipe ACL still uses Interactive — the drift is on the service object, not the pipe.

### D-4 · Service start mode lifecycle — UI controls it, spec describes a system service

- Spec § 1.6 (line 232): *"System clock synchronization shall run as a Windows Service so that it continues without an interactive user session."*
- Code: `App.xaml.cs:62-69` starts the service on app launch and stops it on app exit. The service does **not** run when no user is signed in / no UI is open. This contradicts the headline value proposition of § 1.6. (Reinforced by the `start= demand` mode in D-1, the design comment, and HelpDialog.xaml:80 which explicitly tells users *"It only runs while the clock app is open — it auto-starts when you launch the app and stops when you close it."*)

### D-5 · Service ConfirmLargeOffset / SyncNow flow stubs

- Spec § 1.7 (line 188): *"Manual `Sync now` action shall be available from the tray menu."*
- Spec § 2.5 (lines 396-414) describes the `[Apply][Skip]` toast flow including 30-s timeout.
- Code:
  - `Service/Ipc/IpcRequestHandler.cs:60-69` — `SyncNowRequest` returns the cached snapshot rather than triggering a sync.
  - `Service/Ipc/IpcRequestHandler.cs:71-79` — `ConfirmLargeOffsetResponse` is logged-only with comment *"confirmation flow not yet implemented."*
  - `Service/Sync/SyncWorker.cs:18-20` (header comment) — large corrections are **applied immediately** even if the user opted in; the toast path is missing.

### D-6 · Default tab time zone for newly-added tabs is "UTC", spec says system-local

- Spec § 1.10 (line 348): *"One tab bound to the system's local IANA time zone."* (First-run case is correctly handled: `SettingsStore.cs:134` `ResolveLocalIanaId()`.)
- But every tab added later defaults to UTC: `MainWindowViewModel.cs:450 TimeZoneId = "UTC"` and `HelpDialog.xaml:51` *"New tabs default to UTC and Atomic Lab."*
- The spec doesn't explicitly mandate the new-tab default; arguably a CODE-ONLY divergence rather than DRIFT, but the help text contradicts the spirit of "discovery happens through use."

### D-7 · Sync-server input validation not enforced

- Spec § 1.5 (lines 224-228): *"The user-configurable `Sync server` setting accepts only hostnames within the NIST pool. Non-NIST hostnames are rejected at the input field…"*
- Code:
  - `NistPool.IsKnownNistHost()` exists at `NistPool.cs:75-82` ready to validate.
  - But `TabSettingsDialog.xaml` exposes no Sync-server input at all (only sync frequency). No input field, no validation.
  - `SyncWorker.cs:82-84` falls back to anycast if a non-NIST value somehow ended up in `service.json`, but never rejects it at write time.

### D-8 · Atomic Lab "Show digital readout" cannot be hidden in the UI

- Spec § 1.1 (lines 92-96): *"For analog themes (category a), the integrated digital readout shall be independently hideable per clock window or per tab… The toggle is hidden for themes in categories (b) and (c)…"*
- Spec § 1.2 (lines 137-139): *"Show digital readout" toggle. Visible only when the active theme is in category (a)…*
- Code:
  - The setting exists: `TabSettings.ShowDigitalReadout = true` at `SettingsModel.cs:119`, and `TabViewModel.ShowDigitalReadout` at `TabViewModel.cs:148`.
  - `ClockFaceControl.xaml.cs` does not reference `ShowDigitalReadout` anywhere; readouts are always painted.
  - `TabSettingsDialog.xaml` exposes no toggle for it.

### D-9 · Clock face always renders 12-hour readout regardless of TimeFormat setting

- Spec § 1.1 (lines 98-118): an entire sub-section on `Auto` / `12h` / `24h` rendering, including the noon-numeral-flip behavior on analog faces.
- Code:
  - `TabSettings.TimeFormat`, `TimeFormatMode {Auto, TwelveHour, TwentyFourHour}` exist (`SettingsModel.cs:122, 47-55`).
  - But `ClockFaceControl.xaml.cs` has no `TimeFormat` DependencyProperty and no consumer for it. Format strings are hardcoded:
    - Atomic Lab `:410`: `"h:mm:ss tt"` (always 12-hour).
    - Aero Glass / Boulder Slate / Cathode / Concourse / Daylight: same shared `"h:mm:ss tt"` format via `_digitalReadout`.
    - Flip Clock `:1261-1264, 1268`: always `To12HourParts(local)` + AM/PM appended.
    - Slab `:1420-1421`: `"h:mm"` + ` tt`.
    - Marquee `:1362`: `"h:mm:ss tt"`.
    - Binary `:1550`: `"HH : mm : ss"` (always 24-h, hard-coded, with comment *"Binary BCD stays 24-hour…matching the decimal range to the bit-widths is the point."*) — for encoding themes this matches the spec rationale.
    - Hex `:1644`: always 24-h hex.
    - Binary Digital `:1804-1806`: always 24-h.
  - There is no implementation of the analog noon-flip rule.
  - `TabSettingsDialog.xaml` exposes no Time-format selector.

### D-10 · Second-hand-motion override exists in model but no UI control

- Spec § 1.2 (lines 161-164): *"Second-hand motion override: `Theme default` / `Smooth` / `Stepped`. Default is `Theme default`. Visible only on analog themes…"*
- Code: `TabSettings.SecondHandMotionOverride` (`SettingsModel.cs:125`) and `TabViewModel.SmoothSecondHand` resolution (`TabViewModel.cs:134-145`) exist, but `TabSettingsDialog.xaml` exposes no selector. Override is reachable only by hand-editing settings.json.

### D-11 · Per-theme color customization (the five slots) is unimplemented

- Spec § 1.1 (lines 60-88) describes Ring/Face/Hands/Numbers/Digital slot overrides — a substantial feature.
- Spec § 1.2 (lines 140-156) describes the HSV-wheel + RGB sliders + hex input + eyedropper picker UI, plus a "Reset colors to theme defaults" link.
- Code:
  - `ColorOverrides` POCO exists (`SettingsModel.cs:81-94`).
  - `TabSettings.Colors = new()` is wired in (`SettingsModel.cs:128`).
  - `ClockFaceControl.xaml.cs` header comment (lines 18-21) acknowledges: *"User color overrides … will be applied on top of theme defaults via ColorOverrides DPs in a follow-up commit. For now each theme uses its hardcoded palette."*
  - No DependencyProperty for ColorOverrides on the control.
  - No color-picker UI in `TabSettingsDialog.xaml`.
  - No "Reset colors" link.

### D-12 · Service-absent on-face warning text is not implemented

- Spec § 1.9 (lines 282-305) describes the `"SERVICE NOT RUNNING"` text injected into the digital readout / primary big text / decoded readout, drawn in the theme's Ring slot color.
- Code: no occurrence of the literal string `"SERVICE NOT RUNNING"` in `ClockFaceControl.xaml.cs` or anywhere under `src/`. Faces always render time. Only the `MainWindow.xaml:52-76` banner (and `ServiceStatusText` in the status bar) signals the missing service.

### D-13 · Spec-only sync-correction flow vs. apply-immediately-and-warn

- Spec § 2.5 (lines 396-418): Confirmation flow with toast, 30-s timeout, response-driven Apply.
- Code: `SyncWorker.cs:117-123` applies the correction unconditionally and just logs a Warning when `>= threshold`. No IPC outbound notification on large offsets.

### D-14 · Theme catalog header comment misclassifies counts

- Spec § 1.1 (lines 21-42) groups: 6 analog (a) + 4 digital-only (b) + 2 specialty (c) = **12** total. This matches the `Theme` enum (`SettingsModel.cs:28-42`) and `ThemeCatalog.All` (`ThemeCatalog.cs:35-49`).
- Code: `ThemeCatalog.cs:32-34` comment claims *"6 analog, 3 digital, 2 specialty encodings, 1 binary digital"* — the mental model groups Binary Digital separately. The enum and ordered list are correct; only the comment is off.

### D-15 · Banner button label drift

- Spec § 1.9 (lines 311-316):
  - Not installed: *"Install and start the time-sync service"*
  - Installed but stopped: *"Start the time-sync service"*
- Code (`MainWindowViewModel.cs:151-156`):
  - NotInstalled → `"Install and start the time-sync service"` ✓
  - InstalledNotRunning → `"Start the time-sync service"` ✓
- MATCH on labels, but the spec also says the banner shows *"with a button to enable the Service"* (singular) — the code correctly hides the banner when running. No drift here, just confirming match.

### D-16 · Tabs-context-menu removed; spec implicitly mentions right-click

- Spec § 1.2 (line 165): *"The user can add, rename, reorder, and remove tabs."*
- Spec doesn't mandate a right-click menu, but `HelpDialog.xaml:72` (the in-app help text) tells users they can *"right-click the tab and choose `Close tab`"* — and the right-click ContextMenu was deliberately **removed** in v0.0.26 (`MainWindow.xaml:267-275` comment). Help text now drifts from the actual behavior.

### D-17 · Tab rename UI is not exposed

- Spec § 1.2 (line 165): *"The user can add, rename, reorder, and remove tabs."*
- Code: `TabViewModel.Label` setter exists, and the model field `TabSettings.Label` exists. But `TabSettingsDialog.xaml` provides no rename input — the label is auto-derived from `TimeZoneId` via `DeriveLabelFromIanaId` (`TabViewModel.cs:212-218`). Renaming is not user-reachable.

### D-18 · Spec mandates IANA full catalog (~600); code ships ~140 from Windows zones

- Spec § 1.2 (lines 124-126): *"`TimeZoneInfo.FindSystemTimeZoneById` which accepts IANA IDs natively on Windows."*
- Spec § 1.2 (line 130): *"~600 zones."*
- Code: `TimeZoneCatalog.cs:11-13` header comment: *"Step 6 ships a one-shot list (~140 entries — every Windows zone plus a few additions). The full ~600-entry IANA catalog can be added in a follow-up commit if/when users want more granular zones."* — explicit shipping decision, but a documented gap vs. spec.

### D-19 · ServiceInstaller does NOT set ACLs to "interactive Users" — uses "AuthenticatedUsers"

- Spec § 2.10 (lines 515-520): *"The MSIX installer (and the development-time `ComTekAtomicClock.ServiceInstaller` per § 1.9) creates `%ProgramData%\ComTekAtomicClock\` with NTFS ACLs that grant all interactive Users read/write…"*
- Code: `ServiceInstaller/Program.cs:261` uses `WellKnownSidType.AuthenticatedUserSid` (Authenticated Users includes domain users; Interactive is narrower).

### D-20 · Sample sync-status format strings disagree

- Spec implies strings like `"Last sync: 12s ago (corrected −8.7 ms)"` (`MainWindow.xaml:93` reproduces this verbatim from the help corpus).
- Code `MainWindowViewModel.cs:282-293 FormatDrift` uses **the U+2212 minus sign `−` for positive offsets and `+` for negative offsets** — i.e., the sign printed is the *correction direction*, not the *offset sign*. This matches `MainWindow.xaml:93`'s example but inverts naïve expectations and is worth flagging because the spec doesn't itself spell out the sign convention.

### D-21 · `MainWindow.xaml` inactive-tab font size comment is stale

- Code `MainWindow.xaml:238-244` comment claims *"base FontSize=13"* for the active-tab calculation but the actual setter at `:244` is `FontSize=9`. The +6pt math in the comment doesn't match the code (active = 19, inactive = 9, delta = 10pt). Internal-doc drift.

---

## Code-only (in code, not in spec)

Grouped by topic. The spec stops at the requirement level; the code makes hundreds of visual choices below that.

### Per-theme visual specs (none of the below are in `requirements.txt`)

#### Theme 1 — Atomic Lab (`BuildAtomicLab` `:607-692`)

- Backdrop: 400×400 rectangle filled with `faceBrush` (radial gradient: `#1A2A4A` center 0.5,0.35 → `#060D1A` outer at radius 0.7).
- Bezel ring: 344×344 ellipse, linear gradient top-down `#E0E3E8` → `#7C8088` (mid) → `#2C3038`.
- Inner face: 320×320 ellipse, same `faceBrush`.
- Tick marks via `AddMinuteTicks(160, 155, amber, 1, 0.55)` and `AddHourTicks(160, 145, amber, 3)`. Amber = `#FFB000`.
- Numerals (4): "12"/"3"/"6"/"9" at radius 132 from center, font `"Consolas, Courier New"` size 22, color amber, default `FontWeights.Bold`.
- Hour hand: line, overhang 14 / length 90, white `#F5F5F5`, thickness 6.
- Minute hand: line, overhang 18 / length 128, white, thickness 4.
- Second hand: line, overhang 22 / length 142, red `#FF3030`, thickness 1.6.
- Center pin: 14×14 amber ellipse + 5×5 inner faceBrush ellipse.
- Digital panel: Border 146×60, fill `#040B04`, BorderBrush amber, BorderThickness 0.6, CornerRadius 4. Positioned `Cx-73, Cy+34`. Inside: StackPanel with date (Consolas 9, amber, opacity 0.85), time (Consolas 20, Bold, amber), and literal text `"NIST · BOULDER · CO"` (Consolas 7, amber, opacity 0.7).
- Date format: `"ddd · MMMM d · yyyy"` upper-cased.
- Time format: `"h:mm:ss tt"` (always 12-h).

#### Theme 2 — Boulder Slate (`BuildBoulderSlate` `:698-791`)

- Pre-canvas backdrop: 400×400 `#F5F5F5` (page color).
- Outer black ring: 348×348 ellipse `#0A0A0A`.
- Inner white face: 332×332 `#FFFFFF`.
- Ticks: minute(160,152) black thickness 2; hour(160,138) black thickness 6.
- No numerals.
- Hour hand: black baton, overhang 14 / length 100 / width 10, no corner radius.
- Minute hand: black baton, overhang 18 / length 138 / width 7.
- Second-hand group (a 400×400 sub-canvas): SBB-style `#E3001B` red rod (thickness 2.5, from `Cy+22` to `Cy-118`) + 28×28 disc at `Cy-132` (lined up at the tip).
- Center pin: 10×10 black.
- Date + time as bare text below center, font `"Segoe UI Variable, Segoe UI, sans-serif"`, FontWeight Medium, both black; date FontSize 9, time FontSize 14.
- `_recenterDateReadoutOnUpdate = true` so the date re-centers per tick.
- (Code-only — no canvas background backdrop spec'd by requirements.)

#### Theme 3 — Aero Glass (`BuildAeroGlass` `:797-877`)

- Wallpaper-mock backdrop: 400×400 linear gradient `#3A4F7A` → `#5D7BA8` (mid) → `#243A5E`.
- Decorative wallpaper detail: 80×80 white ellipse at (40,40) opacity 0.15; 40×40 white at (320,100) opacity 0.15; 120×120 black at (0,260) opacity 0.15.
- Acrylic disc: 344×344 ellipse, fill linear gradient `#52FFFFFF` → `#24FFFFFF` (alpha-prefixed), stroke `#8CFFFFFF` thickness 1, DropShadow blur 14, direction 270, depth 4, opacity 0.4.
- Hour ticks only (12), `AddHourTicks(156, 142, white, 3, Round)`.
- Numerals (4): font `"Segoe UI Variable, Segoe UI, sans-serif"` size 22, white, FontWeight SemiBold.
- Hands round-cap: hour len 92 thickness 7 white, minute len 128 thickness 5 white, second len 140 thickness 2 cyan `#00B7FF`.
- Center pin: 12 white + 4 cyan.
- Digital pill: 130×50 Border, background `#59000000` (alpha translucent), CornerRadius 14. Date Segoe size 9 Medium opacity 0.85; time Segoe size 17 SemiBold.

#### Theme 4 — Cathode (`BuildCathode` `:883-956`)

- Background: 400×400 radial gradient `#0A1808` (center) → `#000000`.
- Phosphor green: `#00FF66`. Bright: `#A8FF8A`. Dim: `#00B048`.
- Outer ring: 344×344 ellipse stroked `#003D18` thickness 3, transparent fill.
- Ticks: minute(160,153) `#00B048` thickness 0.8 opacity 0.5; hour(160,145) phosphor thickness 2.5 round.
- Numerals: font `"Lucida Console, Consolas, monospace"` size 20 Bold, phosphor green, with cloned `BlurEffect{Radius=4}` applied.
- Hands: green, hour 90/5 round, minute 128/3.5 round, second-bright 142/1.4 round; all glow.
- Center pin: 10 phosphor with glow.
- Digital panel 144×50, fill `#000A05`, BorderBrush phosphor 0.5, CornerRadius 2, opacity 0.92. Date Lucida size 9 phosphor opacity 0.85 + glow; time Lucida size 22 Bold phosphor + glow.

#### Theme 5 — Concourse (`BuildConcourse` `:962-1028`)

- Background: 400×400 radial gradient `#202020` → `#0C0C0C`.
- Outer ring: 344×344 ellipse fill `#181818`, stroke `#3A3A3A` thickness 2.
- Inner face: 320×320 charcoal `#0F0F0F`.
- Hour ticks only (160,142) orange `#FF8C00` thickness 4.
- Numerals (1–12): font `"Bebas Neue, DIN Alternate, Impact, sans-serif"` size 28 Bold, orange `#FF8C00`, placed on radius 122.
- Hour baton 86/10 orange CR 1.5; minute baton 122/8 orange CR 1.5.
- Second hand: white line len 138 thickness 1.8 round.
- Center pin: 16 orange + 6 charcoal.
- Digital panel 156×56, fill `#1A0D00`, BorderBrush orange 0.6, CornerRadius 3. Date Bebas size 11 SemiBold `#FFA430` opacity 0.85; time Bebas size 24 Bold `#FFA430`.

#### Theme 6 — Daylight (`BuildDaylight` `:1034-1101`)

- Background: 400×400 radial `#FFFDF5` → `#FFF3D6` (high-contrast cream).
- Outer ring: 344×344 cream stroked `#9A9A9A` thickness 2.
- Inner: 320×320 cream `#FFFFFF`.
- Ticks: minute(160,153) navy `#003366` thickness 1.2; hour(160,145) navy thickness 3.5.
- Numerals (1–12) at radius 124, font `"Inter, Segoe UI, sans-serif"` size 22 Bold navy.
- Hands: hour baton 90/9 navy CR 1.5, minute baton 128/7 navy CR 1.5, second line len 140 thickness 2 round, color `#E84A1A` orange-red.
- Center pin: 12 navy + 4 orange-red.
- Date + time bare text below center: date `inter` size 11 SemiBold navy at Cy+60; time `inter` size 18 Bold navy at Cy+76.
- `_recenterDateReadoutOnUpdate = true`.

#### Theme 7 — Flip Clock (`BuildFlipClock` `:1107-1271`)

- Backdrop nightstand: 400×400 linear gradient `#5A3D22` → `#2A1D10`.
- Two chrome legs: 22×14 each at (78,316) and (300,316), CR 2, gradient `#CCCCCC`→`#888888`(0.5)→`#444444`.
- Case: 328×244 rounded rectangle (CR 14) at (36,78), fill linear `#2A2A2A`→`#0A0A0A`(0.5)→`#1A1A1A`, stroke `#3A3A3A` thickness 1.5.
- Inner display recess: 300×186 fill `#1C1A16`, CR 6, at (50,98).
- Four flip tiles at left positions {64,132,208,276}, each 60×138 split into top half 60×69 (gradient `#FFFFFF`→`#F4F1E8`) and bottom half 60×69 (gradient `#F0EDE2`→`#DCD8CA`), CR 5×3, stroke `#A8A39A`. Hinge dark line `#5A564C` thickness 0.7 + light line `#FFFFFFB3` thickness 0.5 just below seam.
- Per-tile spindle pegs: 6.4×6.4 chrome ellipse + 2×2 dark `#222222` inner ellipse on each side.
- Digit text: font `"Segoe UI Variable, Segoe UI, Arial, sans-serif"` size 78, FontWeight Black, fill `#0A0A0A`, centered on each tile.
- Amber colon dots: two 7×7 amber `#FFB84A` at (196.5,162.5) and (196.5,212.5).
- "Seconds" label below tiles: `": 00 SECONDS"` at (200,274) Helvetica size 10 Medium `#AAAAAA`. Live updater appends the AM/PM suffix: `$": {Second:D2} SECONDS · {ampm}"`.
- Date line at (200,292) size 11 Medium `#AAAAAA`, format `"ddd · MMMM d · yyyy"` upper.
- Brand line: `"COMTEKGLOBAL · MODEL CT-1971"` at (200,312) size 9 normal `#888888`.
- TODO at `:1366-1367` re: actual card-flip animation — explicit comment "TODO Marquee/FlipClock chase-bulb / flip animation".

#### Theme 8 — Marquee (`BuildMarquee` `:1277-1368`)

- Outer red theater frame: 400×400 fill `#7A1818`.
- Inner border: 372×372 transparent rectangle stroke `#3A0A0A` thickness 2 at (14,14).
- Inner stage panel: 332×332 rounded (CR 4) at (34,34), fill radial gradient `#1A0A0A`→`#080404`, stroke `#3A1010` thickness 2.
- 32 chase bulbs (top row 9 + bottom row 9 + left col 7 + right col 7, skip corners) at hardcoded positions (`bulbPositions` list), each 12×12 with a radial gradient bulb fill `#FFF8D8`(0,0.38,0.32)→`#FFC940`(0.55)→`#A06010` and a `BlurEffect{Radius=4}` glow.
- Header `"★ NOW SHOWING ★"` at (200,120) font `"Bebas Neue, DIN Alternate, Impact, Arial Black, sans-serif"` size 20 Bold amber `#FFC940`.
- Big time at (200,232) Bebas size 64 Black amber + glow.
- Subtitle `"★ ATOMIC TIME ★ FROM BOULDER ★"` at (200,282) size 13 Medium amber opacity 0.85.
- Date line at (200,308) size 12 Medium amber opacity 0.85.
- Always 12-h `"h:mm:ss tt"` format (no Auto/24h support).
- TODO at `:1366-1367` for chase-bulb wave animation.

#### Theme 9 — Slab (`BuildSlab` `:1374-1429`)

- Backdrop: 400×400 linear gradient concrete `#D4D0C5`→`#A8A298`.
- Top accent bar: 320×6 black `#1A1A1A` at (40,60).
- Bottom accent bar: 320×6 black at (40,338).
- Red diagonal accent: 60×3 `#CC2A1A` at (40,76).
- Slab font chain: `"Rockwell, Roboto Slab, Cambria, serif"`.
- Context line `"ATOMIC · TIME · LOCAL"` at (40,100) size 11 Bold black, left-anchored. Live updated to `"ATOMIC · TIME · DST"` or `"ATOMIC · TIME · STD"` based on `IsDaylightSavingTime()`.
- Big time at (200,240) size 100 Black `#0A0A0A`, format `"h:mm"` (no seconds, no AM/PM at this size).
- Seconds + AM/PM at (200,290) size 36 Bold `#CC2A1A` red, format `"{ss}″ {tt}"`.
- Date at (200,328) size 10 Medium gray `#3A3630`, format `"dddd · d MMMM yyyy"` upper.

#### Theme 10 — Binary (`BuildBinary` `:1435-1553`)

- Background `#080808`.
- Two faint horizontal grid lines `#1A0808` thickness 0.5 at y=120 and y=265.
- Mono font: `"Cascadia Code, Consolas, Lucida Console, monospace"`.
- Title `"BINARY CLOCK"` at (200,60) size 16 Bold `#FF5555`.
- Date line at (200,90) size 11 normal `#FF5555` opacity 0.7.
- Bit-value labels (right-aligned to x=42) `"8"/"4"/"2"/"1"` at y={155,195,235,275} size 11 `#553030`.
- 6 columns of LEDs at x={78,128,198,248,318,368} with bit-positioning per column (columns 1/3/5 have 3 dots; columns 2/4/6 have 4; column 0 has 2). Each dot is 22×22 ellipse, lit fill is radial gradient `#FFC4C4`→`#FF3030`→`#7A0808` with BlurEffect{Radius=6}; unlit fill is `#3A0A0A`→`#180404`.
- Group labels HOURS / MINUTES / SECONDS at y=304 size 10 normal `#AA3030`.
- Decoded readout at (200,350) size 22 Bold `#FF3030` + glow, format `"HH : mm : ss"` (always 24-h).
- Footer `"read top→bottom · 8·4·2·1 BCD per column"` at (200,375) size 9 `#553030`.

#### Theme 11 — Hex (`BuildHex` `:1559-1675`)

- Background: 400×400 radial `#0C1828` (0.5,0.4) → `#020812`.
- Title bar 400×32 `#000814` with three traffic-light dots: 8×8 each at (10,12),(26,12),(42,12), opacity 0.7, colors `#FF5050`/`#FFAA30`/`#50CC60`.
- Window title `"comtekglobal :: hex_clock.exe"` at (200,21) Cascadia size 11 cyan `#5FE2FF` opacity 0.6.
- Comment line `"// time encoded as hexadecimal (per unit)"` at (40,80) size 12 cyan opacity 0.55.
- Big hex digits at (200,190) size 56 Bold cyan + glow, format `$"{Hour:X2}:{Minute:X2}:{Second:X2}"` (always 24-h).
- HOURS/MINUTES/SECONDS labels at x={80,200,320}, y=216, size 10, dim cyan `#3A8AAA`.
- Hex-ASCII date breakdown lines (40,244)/(40,260)/(40,276)/(40,290) for dow/dom/month/year, all size 12 cyan opacity 0.75.
- Day-fraction line at (40,308) size 12 cyan opacity 0.7: `// day-frac: 0xNNNN / 0xFFFF (PCT% elapsed)`.
- Color swatch: 320×14 rectangle CR 2 opacity 0.85 at (40,322), fill = `Color.FromRgb(dayU16>>8, dayU16&0xFF, 0xFF)`.
- Swatch description line `"// the bar above is #RRGGBB — today, encoded as a color"` at (40,350) size 11 cyan opacity 0.55.
- Prompt `"$ _"` at (40,378) size 14 bright cyan `#A0EEFF`.

#### Theme 12 — Binary Digital (`BuildBinaryDigital` `:1711-1821`)

- Background: 400×400 radial `#1A0830` (0.5,0.4) → `#080414`.
- Title bar 400×32 `#08000A` + same 3 traffic-light dots as Hex.
- Window title `"comtekglobal :: bin_clock.exe"` at (200,21) Cascadia size 11 magenta `#FF5CD0` opacity 0.7.
- Comment line `"// time encoded as binary text (per unit)"` at (40,80) size 12 magenta opacity 0.55.
- Three labeled rows: each with a dim-magenta prefix at x=80 (`H`/`M`/`S`, size 30 Bold) and a primary-magenta bits text at x=120 size 30 Bold magenta + glow. Padded to 5 / 6 / 6 bits respectively. Y at 138/180/222.
- Binary-ASCII date breakdown rows at y=254/270/286/302 (dow/dom/mon/yr), size 11 magenta opacity 0.75.
- Annotation `"// widths: 5b hour · 6b min · 6b sec · MSB first"` at (40,320) size 10 dim magenta.
- Two decorative noise rows (static, doesn't update) at y=340/354 size 9 magenta opacity 0.18.
- Prompt `"$ _"` at (40,380) size 14 bright `#FFAAE8`.

#### Per-theme version + theme-name overlays (test-only)

- `AddVersionLabel()` `:341-355` paints `v0.0.32` (or current `<Version>`) at (6,4) — Consolas 9, opacity ~`#90808080`. Comment notes this matches an internal spec *"version should be in the clock background, upper left"* — not in `requirements.txt`.
- `AddDebugThemeLabel()` `:316-331` paints `theme: <FriendlyName>` near (Cx, 388) — Consolas 9, semi-transparent gray. Code comment notes this is *"TEMPORARILY re-enabled for v0.0.9"* and TODO to remove for public release.

#### Date line on every theme

- Spec describes Atomic Lab's panel having `NIST · BOULDER · CO`, but doesn't ask for a date readout on most themes. Code adds a `"ddd · MMMM d · yyyy"` upper-cased date row to **every** theme: AtomicLab/BoulderSlate/AeroGlass/Cathode/Concourse/Daylight/FlipClock/Marquee/Slab/Binary; Hex and BinaryDigital surface the date as hex-ASCII / bin-ASCII rows.
- The clock-face self-comment justifies it: *"Date line (parity with the analog faces — day/month/dom/year)."* Not requirements-derived.

### Tab strip styling (MainWindow.xaml + FloatingClockWindow.xaml)

- `Background="White"`, `BorderBrush="#C0C0C0"`, `Padding="0"` on outer TabablzControl.
- Style.Resources strips Thumb to invisible: `OverridesDefaultStyle=True`, transparent Background/BorderBrush, Width=MinWidth=0, empty Border template (rationale comment lines 192-213 about yellow accent / dark-active bug).
- Default DragablzItem: `Background=White`, `Foreground=Black`, `BorderBrush=#C0C0C0`, `BorderThickness=1,1,1,0`, `Padding=10,4`.
- Inactive base `FontSize=9` (was 13; v0.0.28 dropped to 9 per comment :238-244).
- `FocusVisualStyle={x:Null}`.
- `IsSelected=True`: `Background=#FFFFFF`, `BorderBrush=#808080`, `FontWeight=Bold`, `FontSize=19`.
- `IsSelected=False`: `Background=#E8E8E8`, `BorderBrush=#C0C0C0`.
- `IsMouseOver=True`: `Background=#F5F5F5`.
- ContextMenu was deliberately removed (`MainWindow.xaml:267-275`).
- ItemTemplate: TextBlock with `Tag="TabHeaderText"`, `Text={Binding Label}`, `VerticalAlignment=Center`. Tag is used for imperative tab-header refresh (`MainWindow.xaml.cs:84-115 SetTabHeaderInAllDisplays`).
- `MouseDoubleClick` opens TabSettings (`MainWindow.xaml:246-247`); `PreviewMouseLeftButtonDown` forces selection to bypass Dragablz click/drag classifier.

### Window / dialog sizes

| Window | Size | Min size | ResizeMode | Backdrop |
|---|---|---|---|---|
| MainWindow | 560×640 | 380×420 | (default) | Mica / Round |
| FloatingClockWindow | 500×500 | 320×320 | (default) Manual startup | Mica / Round |
| TabSettingsDialog | 560×540 | — | NoResize | Mica / Round |
| AboutDialog | 520×430 | — | NoResize | Mica / Round |
| HelpDialog | 560×540 | — | NoResize | Mica / Round |
| ThemesDialog | 820×720 | — | (default — resizable) | Mica / Round |

- Spec § 1.3 (line 172) says window state persists across restarts. Code: not implemented; floating windows are spawned with default size, Manual startup, no persistence wired to `_settings.Windows`.

### Dialog visual / typographic detail (CODE-ONLY in entirety — spec is silent)

- Whole "amber + dark-cream" palette: `#FFB000` for headings, `#A8A39A` for sub-labels, `#FFB000` for save buttons, `#0A0A0A` for save-button text, `#040B04` panel background on About, `#7A3A1A` ALPHA badge, `#A0E0FF` for clickable links, etc.
- About dialog has explicit "ALPHA" badge: `#7A3A1A` background, `#FFB000` border, `#FFF5D8` text.
- Settings dialog uses single field for "Sync frequency" (only the global section is exposed; "ALL CLOCKS ON THIS PC" sub-label).
- Help dialog includes an entire on-disk help corpus (tabs, time-zones, sync, status bar) — much of it duplicates `requirements.txt` text but in user-friendly prose.

### Settings model fields not in spec

- `TabSettings.Id : string Guid` (`SettingsModel.cs:107`) — stable identifier for tabs across reorders, code-only.
- `TabSettings.Label : string?` (`:110`) — optional user override of the auto-derived city label.
- `[JsonExtensionData] UnknownFields : Dictionary<string, JsonElement>?` on every record — implements forward-compat per spec § 2.10 line 495 generally, but this concrete mechanism is code-only.
- `WindowSettings : TabSettings` (`:143-155`) extends with `X, Y, Width, Height, AlwaysOnTop, OverlayMode`. `Width=Height=320` defaults. Spec mentions per-window position/size/on-top/overlay (§ 1.3, § 1.8) but doesn't fix defaults.

### Atomic write detail (matches spec but specific implementation)

- `SettingsStore.cs:202-217 WriteAtomic`: write to `path + ".tmp"`, `Flush(true)`, `File.Move(tmp, path, overwrite: true)`. Matches § 2.10 spec verbatim.
- Plus: corrupt file recovery at `:107-113` — JSON parse failure renames `path + ".broken-{unix-ts}"` and resets to defaults. Code-only resilience.

### IPC contract — concrete shapes

- `PipeNames.UiToService = "ComTekAtomicClock.UiToService"` (`IpcContract.cs:28`).
- `IpcSchema.CurrentVersion = 1` (`:38`).
- `IpcMessageType` enum: SyncNowRequest, SyncNowResponse, LastSyncStatusRequest, LastSyncStatusResponse, ConfirmLargeOffsetRequest, ConfirmLargeOffsetResponse, StatusChangedNotification.
- `IpcEnvelope(int SchemaVersion, IpcMessageType Type, string PayloadJson)`.
- `SyncStatus(DateTimeOffset AttemptedAtUtc, bool Success, string? ServerHost, double? OffsetSeconds, string? ErrorMessage)`.
- `ConfirmLargeOffsetRequest(string ServerHost, double OffsetSeconds, DateTimeOffset DetectedAtUtc)`.
- `ConfirmLargeOffsetResponse(bool Apply)`.
- Wire format: `[4 bytes little-endian int32 length][N bytes UTF-8 JSON]`, `MaxPayloadBytes = 1 MiB` (`IpcWireFormat.cs:21`).
- Pipe instances: max 4 server instances (`IpcServer.cs:143`), 64 KiB in/out buffers, async, byte mode.

### Service behavior — concrete

- SNTP packet: 48 bytes, `LI=0|VN=4|Mode=3` (`0x23`), `NtpEpoch = 1900-01-01Z` (`NtpPacket.cs:31-32`).
- Per-server timeout: 5 s (`SyncWorker.cs:41`).
- NIST minimum poll interval: 4 s between failed servers (`SyncWorker.cs:44, :144`).
- NTP port 123 (`:38`).
- First sync runs immediately on service start (`:58`) before the first interval delay.
- Service NTP validation: `mode != 4` rejected, `vn` must be 3 or 4, leap indicator 3 (server unsynchronized) rejected, stratum must be in 1..15.
- `SystemTime.cs` P/Invokes `kernel32!SetSystemTime` with full SYSTEMTIME struct.
- Event log source name: `EventLogSource = "ComTekAtomicClock"` (`SettingsModel.cs:250`).
- `ServiceName = "ComTekAtomicClockSvc"` (`Service/Program.cs:12`, `App.xaml.cs:25`, `ServiceInstaller/Program.cs:40`, `ServiceStateChecker.cs:28`).

### ServiceInstaller specifics

- Display name: `"ComTek Atomic Clock — time sync"` (`Program.cs:41`).
- ProgramData ACLs: `AuthenticatedUsers` get Modify+ReadAndExecute+Synchronize, with ContainerInherit+ObjectInherit (`:261-269`).
- Service ACL via `sc.exe sdset` (`:296-300`):
  - `AU` — `CCLCSWRPWPLOCRRC` (query/start/stop/interrogate/read).
  - `BA` — `CCDCLCSWRPWPDTLOCRSDRCWDWO` (full).
  - `SY` — `CCLCSWLOCRRC` (read+interrogate).
- Uninstall: stops service (waiting up to 30 s), `sc.exe delete`, removes %ProgramData% dir, optionally removes %APPDATA% with `--purge-user-data`.
- `--service-exe <path>` arg overrides sibling-exe lookup.
- App manifest level: `requireAdministrator`, `uiAccess=false`.
- Supports OS GUID `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}` (Win10).

### App lifecycle & error-handling

- `App.xaml.cs:53-57` constructor subscribes to:
  - `DispatcherUnhandledException` (UI thread)
  - `AppDomain.CurrentDomain.UnhandledException`
  - `TaskScheduler.UnobservedTaskException`
- All three pop a `MessageBox.Show` with trimmed top-6-frame stack trace.
- TryStartService waits up to 10 s for `Running`; TryStopService waits up to 5 s.

### MainWindowViewModel cadence

- Service-state poll: 4 s (`MainWindowViewModel.cs:78`).
- Last-sync-text re-render: 1 s timer.
- Live IPC `LastSyncStatusRequest` cadence: every 5th tick = ~5 s (`:45`), gated on `ServiceState == Running`.
- Quick-poll after install/uninstall: 10× at 1 s.
- `LastSyncText` formatter strings:
  - `"Last sync: pending…"`, `"Last sync: service not running"`, `"Last sync: just now"`, `"Last sync: 12s ago (corrected −8.7 ms)"`, `"Last sync failed: <reason>"`.
  - `FormatDrift` uses U+2212 minus sign for "fast→corrected back" (positive offset) and `+` for "slow→corrected forward" (negative offset).

### Helpers / utilities

- `RelayCommand` — minimal ICommand wrapper (`Services/RelayCommand.cs`).
- `TimeZoneCatalog.All` — Lazy<list> built from `TimeZoneInfo.GetSystemTimeZones()` mapped to IANA via `TryConvertWindowsIdToIanaId`. ~140 entries on stock Windows; sorted by UTC offset then IANA id; display format `"(UTC±hh:mm)  America/New_York  ·  Eastern Standard Time"`.
- `ServiceStateChecker.Check()` — best-effort enumeration; returns `Running` for both `Running` and `StartPending` SCM states.
- `ExePaths.FindService / FindServiceInstaller` — first sibling of UI exe, then dev-time `…/src/<project>/bin/{Debug,Release}/net8.0-windows/<exe>`.
- `AppInterTabClient` — Dragablz `IInterTabClient`: every tear-off creates a new `FloatingClockWindow`; `TabEmptiedResponse.CloseWindowOrLayoutBranch`.

### Theme-default-is-smooth table (TabViewModel.cs:220-232)

| Theme | Smooth default? |
|---|---|
| AtomicLab | true |
| BoulderSlate | false (Mondaine cadence) |
| AeroGlass | true |
| Cathode | true |
| Concourse | true |
| Daylight | false |
| All non-analog (FlipClock/Marquee/Slab/Binary/Hex/BinaryDigital) | true (default fallback) |

### IsDarkTheme luminance map (TabViewModel.cs:87-102) — drives ✕ / ? glyph color

(7 dark, 3 light by code; matches design/README.md narrative.) Atomic Lab/AeroGlass/Cathode/Concourse/Marquee/Slab/Binary/Hex/BinaryDigital → dark; BoulderSlate/Daylight/FlipClock → light.

### Resources / packaging

- Both `ComTekAtomicClock.UI.csproj` and `ComTekAtomicClock.Service.csproj` target `net8.0-windows`. `Shared` targets `net8.0`. `ServiceInstaller` targets `net8.0-windows`.
- Versions: UI `<Version>0.0.32</Version>` / `<AssemblyVersion>0.0.32.0</AssemblyVersion>` / `<FileVersion>0.0.32.0</FileVersion>` (mid-alpha; spec doesn't pin).
- App icon: `Assets\AppIcon.ico` (linked as both `<ApplicationIcon>` and `<Resource>`).
- 12 SVG `<Resource>` Includes link the canonical design SVGs from `..\..\design\themes\` into the assembly (`ComTekAtomicClock.UI.csproj:45-56`).
- WPF-UI 4.2.1, Dragablz 0.0.3.234, SharpVectors.Reloaded 1.8.5, System.ServiceProcess.ServiceController 8.0.0 — none of these specific packages are in `requirements.txt`.
- Service uses `Microsoft.Extensions.Hosting` 8.0.0, `Microsoft.Extensions.Hosting.WindowsServices` 8.0.0, `System.IO.Pipes.AccessControl` 5.0.0.
- App.xaml (`Application.Resources`) merges WPF-UI `ThemesDictionary Theme="Dark"` and `ControlsDictionary` — choice not in spec.

---

## Spec-only (in spec, not in code)

### S-1 · System tray icon (§ 1.7) — completely absent

- *"The UI shall provide a tray icon with: open main window, sync now, last-sync status, settings, exit."*
- *"Closing the last visible window shall minimize to tray, not terminate the UI process."*
- Code: closing last tab calls `Application.Current?.Shutdown()` (`MainWindowViewModel.cs:413`). No tray icon.

### S-2 · Desktop overlay mode (§ 1.4) — model-only

- `WindowSettings.OverlayMode` exists in the model (`SettingsModel.cs:154`) but no rendering for borderless / transparent / click-through / desktop-pinned mode in any window XAML. No spawn UI.

### S-3 · Multiple simultaneous clock windows with persistence (§ 1.3) — partial

- Floating windows do exist (Dragablz tear-away creates a `FloatingClockWindow`), and `WindowSettings` model exists. But: persistence to `_settings.Windows` is not wired (the floating window doesn't append to `_settings.Windows` nor restore on launch; `MainWindowViewModel` never enumerates `_settings.Windows`).

### S-4 · Toast confirmation flow for large sync offsets (§ 2.5) — unimplemented

- No `ToastNotification` reference anywhere. No outbound `ConfirmLargeOffsetRequest` from Service. No 30-s timeout. The IPC handler simply logs and ignores `ConfirmLargeOffsetResponse`.

### S-5 · Five-slot color overrides (§ 1.1, § 1.2) — model-only

- `ColorOverrides` POCO exists; renderers ignore it. UI has no color pickers. (Already covered in DRIFT D-11; restated here for completeness.)

### S-6 · Eyedropper / HSV wheel / RGB sliders / hex input (§ 1.2)

- Not present.

### S-7 · "Reset colors to theme defaults" link (§ 1.2)

- Not present.

### S-8 · Time format selector (Auto / 12h / 24h) UI (§ 1.1, § 1.2, § 1.8)

- Model has `TimeFormatMode`, but no UI control. (See DRIFT D-9.)

### S-9 · Second-hand motion override (Theme default / Smooth / Stepped) UI (§ 1.2)

- Model has `SecondHandMotion`. No UI control. (See DRIFT D-10.)

### S-10 · Show-digital-readout toggle UI for analog themes (§ 1.2)

- Model has `ShowDigitalReadout`. No UI control. (See DRIFT D-8.)

### S-11 · "Use the same theme across all tabs" toggle (§ 1.8)

- Model has `GlobalSettings.UseSameThemeAcrossAllTabs`. No UI control, no propagation logic.

### S-12 · "Start with Windows" toggle (§ 1.8)

- Model has `GlobalSettings.StartWithWindows`. No UI control, no shell-startup integration.

### S-13 · "Confirm large sync corrections" toggle UI (§ 1.8)

- Model exists. No UI. (See DRIFT D-5.)

### S-14 · "Send anonymous crash reports" / "Send anonymous usage stats" (§ 1.8, § 2.11)

- Model has both bools. No telemetry endpoint, no consent dialog, no `PRIVACY.md`, no TLS pipeline. Pre-public-release blocker per § 2.11 (line 578).

### S-15 · 24-hour analog numeral flip behavior (§ 1.1 lines 107-118)

- Spec describes positions 1–11 swapping to 13–23 from noon→midnight; position 12 fixed; sparse themes flip 3↔15, 6↔18, 9↔21. Code never inspects time format and writes hardcoded `"12"`, `"3"`, `"6"`, `"9"` (or 1–12) numerals at build time only.

### S-16 · `service.json` is not actually populated by the UI

- Spec § 2.10 (line 511): *"The UI writes this file whenever those settings change."*
- Code: `SettingsStore.SaveServiceConfig(ServiceConfig)` exists (`SettingsStore.cs:193-196`) but no UI code calls it. The `TabSettingsDialog` "Sync frequency" picker writes to its in-memory model but never persists to `service.json` (`TabSettingsDialog.xaml.cs` not read here, but no caller of `SaveServiceConfig` was found anywhere — verified by grepping the entire `src/`).

### S-17 · Schema migration (§ 2.10 line 494) — partial

- `SchemaVersion` int exists on `AppSettings` and `ServiceConfig`. `[JsonExtensionData]` preserves unknown fields. But there's no version-detect / migration code; reading a future v2 file silently treats it as v1.

### S-18 · MSIX packaging + .appinstaller manifest (§ 2.7) — absent

- No MSIX-related files in the tree (`AppxManifest.xml`, `.wapproj`, `Package.appxmanifest`, `.appinstaller`). No GitHub Pages publishing pipeline. No Authenticode signing config.

### S-19 · ARM64 build (§ 2.1) — not configured

- csproj target frameworks are `net8.0-windows` only; no `<Platforms>` or `<RuntimeIdentifiers>` for `win-arm64`. (Buildable as ARM64, but not declared.)

### S-20 · Pipe schema versioning behavior (§ 2.4)

- `IpcServer.cs:94-99` only **logs** a warning when schema version differs and processes anyway. Spec's *"versioned message contract"* phrasing isn't more specific, so this likely matches intent — restated here for visibility.

### S-21 · Tab rename UI (§ 1.2 line 165)

- The `Label` field exists; no UI to set it. (See DRIFT D-17.)

### S-22 · Sync server selector UI (§ 1.5, § 1.8)

- `SyncServer` field exists in both `GlobalSettings` and `ServiceConfig`. No selector in `TabSettingsDialog`. (See DRIFT D-7.)

### S-23 · Banner button "Service running" suppression — implemented, but spec says "(banner is not shown in this state)"

- Already implemented in `MainWindowViewModel.BannerVisible` (`:142`). Match.

### S-24 · "Always-on-top" per-window (§ 1.8)

- `WindowSettings.AlwaysOnTop` field exists. `FloatingClockWindow.xaml` has no `Topmost` binding to it.

### S-25 · Per-tab "show digital readout" hidden for non-analog themes (§ 1.2)

- N/A while D-8 stands; no UI to begin with.

### S-26 · NIST poll interval ≥ 4 s/server *across the periodic interval* (§ 1.5 line 191-193)

- Code honors the within-attempt rule (`SyncWorker.cs:144`) but not explicitly the across-the-periodic-interval rule. With clamp `[15 min, 24 h]` the periodic floor is 15 min, comfortably above 4 s, so the global rule is met by virtue of the lower-bound clamp — but not as a deliberate test.

### S-27 · `WindowSettings` defaults (§ 1.3, § 1.8) and persistence

- Defaults are 320×320 in code (`SettingsModel.cs:147-148`); spec doesn't fix the value. Persistence not wired.

### S-28 · Crash report contents / scrubbing (§ 2.11 lines 533-545) — N/A while telemetry absent.

---

## Match (code agrees with spec)

Brief, section-level summaries.

- **§ 1.1 base theme list (12 themes).** `Theme` enum and `ThemeCatalog.All` enumerate all 12 in the documented order; the gallery dialog renders them. (Drift caveats: the spec says "eleven" in line 17 — see internal-arithmetic section below.)
- **§ 1.1 Atomic Lab as default on first run.** `SettingsStore.CreateDefaultAppSettings()` and `GlobalSettings.DefaultTheme = Theme.AtomicLab` and `TabSettings.Theme = Theme.AtomicLab`.
- **§ 1.1 Per-theme smooth/stepped second-hand defaults.** `TabViewModel.ThemeDefaultIsSmooth` table matches design/README.md.
- **§ 1.5 SNTP/v4 over UDP/123.** `NtpPacket.BuildClientRequest` builds 48 bytes with VN=4 mode=3; `SyncWorker` sends to port 123.
- **§ 1.5 NIST primary `time.nist.gov` + 10-server pool.** `NistPool.Anycast = "time.nist.gov"`; `StratumOnePool` lists exactly the 10 servers in the spec (5 `-g`, 5 `-wwv`).
- **§ 1.5 Pool walk: primary first, then randomized rest; per-server poll ≥ 4 s.** `NistPool.GetWalkOrder` + `SyncWorker.MinPerServerPoll`.
- **§ 1.5 No non-NIST fallback.** SyncWorker walks only the pool; on total failure logs Warning and returns.
- **§ 1.5 sync interval clamp 15 min – 24 h.** `SyncWorker.LoadIntervalFromConfig`. (DRIFT D-2 still applies on the *default* and the *user choices*.)
- **§ 1.6 Worker Service / windows-service hosting.** `Program.cs:10-13 AddWindowsService(ServiceName="ComTekAtomicClockSvc")`. (DRIFT D-1 on start mode.)
- **§ 1.9 Service-detection states + helper exit codes.** `ServiceStateChecker` returns the 3 states; `ServiceInstaller.Program` mirrors them; banner button labels match.
- **§ 1.9 Helper UAC consent.** `ServiceLauncher.LaunchHelper` sets `UseShellExecute=true` and `Verb="runas"`. App manifest sets `requireAdministrator`.
- **§ 1.9 First-run banner + non-elevated UI.** UI runs as standard user; banner appears when service != Running.
- **§ 1.10 First run defaults.** `SettingsStore.CreateDefaultAppSettings` builds: 1 tab at local IANA, AtomicLab, Auto format, hourly sync. (Settings file is also written on first run.)
- **§ 2.1 Win10 1809+ / Win11 declared in manifest** (`app.manifest:23` references the Win10 GUID).
- **§ 2.2 .NET 8 / WPF / Worker / shared library.** All four csproj target net8.0-windows (or net8.0 for Shared); UI uses WPF; Service uses Worker template.
- **§ 2.3 UI standard user; Service LocalSystem.** UI manifest is implicit asInvoker; Service runs LocalSystem (default for windows services). ServiceInstaller is the only elevated piece.
- **§ 2.4 Named pipe + ACL.** `PipeNames.UiToService` constant; `IpcServer` builds a `PipeSecurity` with InteractiveSid + LocalSystemSid.
- **§ 2.5 Event-Log entries for sync attempts / offsets / failures.** `SyncWorker` uses `ILogger` with Information for normal sync, Warning for large corrections / total-pool-failure.
- **§ 2.6 SNTP packet validation.** `NtpPacket.ParseResponse` rejects malformed responses (mode/version/leap/stratum).
- **§ 2.9 Copyright + MIT.** `AboutDialog.xaml` shows the MIT text and "© 2026 Daniel V. Oxender." `LICENSE` at repo root (separate file).
- **§ 2.10 Settings.json paths + atomic write + JsonExtensionData.** All match spec (`SettingsStore`).
- **§ 2.10 Service.json at `%ProgramData%\ComTekAtomicClock\service.json`.** Matches.
- **§ 2.10 Service falls back to hardcoded defaults until UI writes.** `LoadServiceConfig` returns `new ServiceConfig()` if file missing. (But D-2 / S-16 affect what those defaults are and whether the UI ever actually writes.)
- **§ 3 Out-of-scope items** (cross-platform, hardware refs, NTS, cloud sync). Code has none of these — by definition matches the omission.

---

## Notable internal-arithmetic mismatches in the spec itself

These are **spec ↔ spec** contradictions (not code↔spec). Worth flagging so a future revision of `requirements.txt` can clean them up.

### M-1 · Eleven vs. twelve themes (§ 1.1)

- Line 17: *"The product ships with **eleven** face themes (subject to revision)…"*
- Lines 21-42 then list **twelve** themes:
  - (a) Analog: AtomicLab, BoulderSlate, AeroGlass, Cathode, Concourse, Daylight = **6**
  - (b) Digital-only: FlipClock, Marquee, Slab, BinaryDigital = **4**
  - (c) Specialty: Binary, Hex = **2**
  - Total: **12**.
- Code: 12. Spec prose count says 11; spec table lists 12. Code matches the table.

### M-2 · Default sync interval — "hourly" (§ 1.5, § 1.10) vs. UI choices "6/12/24" (D-2)

- This is a code/spec mismatch (D-2), but it's also internally consistent in spec: § 1.5 line 187 says "hourly", § 1.10 line 351 says "hourly". Spec is consistent; code drifts.

### M-3 · "Settings popover" terminology (§ 1.2) vs. dialog implementation

- Spec § 1.2 line 127 says *"Clicking it opens a **popover**…"*; § 1.2 line 167-169 / § 1.8 use the same term. Code uses a modal **dialog** (`TabSettingsDialog`). This is a UI-style mismatch (popover ≠ dialog) but spec doesn't itself contradict — only with code.

### M-4 · `Confirm large sync corrections` default (§ 1.8 line 261) — "default OFF" — matches code.

- Spec is internally consistent here. Just confirming.

### M-5 · Service display name (§ 1.9 line 329)

- Spec quotes `DisplayName= "ComTek Atomic Clock — time sync"` — code matches at `ServiceInstaller/Program.cs:41`. (Note the em-dash; both spec and code use U+2014.)

### M-6 · Service start mode internal contradiction (§ 1.6 vs § 1.9 vs design)

- § 1.6 says Automatic.
- § 1.9 line 327-330 also shows `start= auto` in the documented helper command.
- Code uses `start= demand` deliberately (D-1). Spec is internally consistent on `auto`; code disagrees.

### M-7 · Banner button "Service installed but stopped" — spec describes calling only `sc.exe start ComTekAtomicClockSvc` (§ 1.9 line 332-334). Code matches: when `state==InstalledStopped`, only `StartService` is called.

### M-8 · Right-click context menu (in HelpDialog text) vs. removal in v0.0.26

- `requirements.txt` doesn't mandate right-click; but `HelpDialog.xaml:72` mentions it as a feature — and `MainWindow.xaml:267-275` documents its removal. So spec ↔ in-app help drift, not requirements drift.

---

## Files referenced (absolute paths)

- `C:\ComputerSource\ComTekAtomicClock\windows\requirements.txt`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Controls\ClockFaceControl.xaml.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\MainWindow.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\MainWindow.xaml.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\FloatingClockWindow.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\FloatingClockWindow.xaml.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\App.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\App.xaml.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Dialogs\TabSettingsDialog.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Dialogs\AboutDialog.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Dialogs\AboutDialog.xaml.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Dialogs\HelpDialog.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Dialogs\ThemesDialog.xaml`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\ViewModels\MainWindowViewModel.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\ViewModels\TabViewModel.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\ViewModels\ThemeCatalog.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\IpcClient.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\ServiceLauncher.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\ServiceStateChecker.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\TimeZoneCatalog.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\ExePaths.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\AppInterTabClient.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\Services\RelayCommand.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.UI\ComTekAtomicClock.UI.csproj`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Shared\Settings\SettingsModel.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Shared\Settings\SettingsStore.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Shared\Ipc\IpcContract.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Shared\Ipc\IpcWireFormat.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Program.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Sync\NistPool.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Sync\NtpPacket.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Sync\SyncStateProvider.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Sync\SyncWorker.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Sync\SystemTime.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Ipc\IpcRequestHandler.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.Service\Ipc\IpcServer.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.ServiceInstaller\Program.cs`
- `C:\ComputerSource\ComTekAtomicClock\windows\src\ComTekAtomicClock.ServiceInstaller\app.manifest`