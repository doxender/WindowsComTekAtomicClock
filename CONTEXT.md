# ComTekAtomicClock — Context

A running log of decisions, constraints, and gotchas for this project. **Update on every material change.** Append-only — reversed decisions get a new entry that references the old one. Standing rule: `C:\ComputerSource\Claude\Context\memory\feedback_per_project_context_doc.md`.

For the formal point-in-time spec see `SPEC.md`. For the per-version changelog see `CHANGELOG.md`. For cross-project history see `C:\ComputerSource\Claude\Context\HISTORY.md`.

| | |
|---|---|
| **Project root** | `C:\ComputerSource\ComTekAtomicClock\windows\` |
| **Solution** | `ComTekAtomicClock.slnx` |
| **Current version** | **v1.0.0** — first stable release |
| **Code-as-ground-truth baseline** | `SPEC.md` v2.0 (2026-05-03) |
| **Open backlog** | `windows/TODO.md` (single source of truth for all open work) |
| **Repo state** | `master` working tree carrying v1.0.0 — pushed/local-only state evolving this session |

## Quick navigation

| Doc | Purpose |
|---|---|
| `SPEC.md` | Authoritative regeneration baseline (23 sections, ~92 KB). Code-as-ground-truth. |
| `CONTEXT.md` | This file. Living decisions/constraints/gotchas. |
| `CHANGELOG.md` | Per-version problem/solution log. |
| `README.md` | User-facing overview. |
| `requirements.txt` | Legacy spec (591 lines, 2026-04-25). **Superseded by SPEC.md.** Kept for history. |
| `docs/code-vs-spec-audit.md` | 706-line drift audit between requirements.txt and current code. Bridge to SPEC.md. |
| `design/themes/*.svg` | Canonical theme preview art (12 SVGs). Linked into UI assembly. |

---

## Current architecture (one-line summary)

Three-process Windows desktop app: **WPF UI** (standard user, asInvoker) + **Worker Service** (LocalSystem, demand start) + **UAC installer helper** (one-shot). UI ↔ Service via named pipe `ComTekAtomicClock.UiToService` with length-prefixed JSON. Settings split: `%APPDATA%\…\settings.json` (per-user) + `%ProgramData%\…\service.json` (per-machine).

---

## Standing decisions (the why behind current code)

### Why we dropped Dragablz (v0.0.33) — **DO NOT RE-INTRODUCE**

Dragablz `0.0.3.234` was the source of nearly every tab-related bug fought across v0.0.14..v0.0.32. **The library was removed in v0.0.33** in favor of the BCL `System.Windows.Controls.TabControl`. Specific failure modes (all live in the v0.0.32 codebase, all gone now):

1. `Dragablz.TabablzControl.OnItemsChanged` threw `NotImplementedException` for `CollectionChanged.Replace` actions (caused the v0.0.14 silent crash).
2. Click/drag classifier swallowed single clicks (required v0.0.20 `PreviewMouseLeftButtonDown` rescue).
3. `ItemTemplate` bindings did not honor `PropertyChanged` reliably for tab headers (eight iterations to land on the v0.0.32 imperative walker).
4. Headers rendered in a separate visual subtree from item content (forced the imperative walker to enumerate all Application windows by Tag).
5. Library has had no meaningful release in ~3-4 years; unmaintained.

**Tear-away gesture** was the headline feature Dragablz provided. It was **also dropped** in v0.0.33, deliberately. Replaced with three explicit commands:

- `+ New tab` toolbar button → `AddTabCommand` (in-place tab)
- `+ New window` toolbar button → `NewClockWindowCommand` (new floating window with fresh clock)
- Right-click on tab → `OpenInNewWindowCommand` (migrate existing tab to new window)
- `?` overlay menu on a floating window → `BringWindowIntoTabsCommand` (reverse migration)

If a future session sees an open issue about tab UX and is tempted to add Dragablz (or any tear-away tab library) back: **STOP. Read this entry first.** The existing native-TabControl + explicit-commands design is the resolution, not a regression. Magnetic snap (Phase 2 — see Pending below) is the path forward for desktop-arrangement use cases, NOT tear-away.

**Files DELETED in v0.0.33:**
- `Services/AppInterTabClient.cs` (Dragablz `IInterTabClient`)
- `MainWindow.SetTabHeaderInAllDisplays` + `EnumerateVisualDescendants` (the v0.0.32 imperative walker)
- The `Tag="TabHeaderText"` markup convention
- The Dragablz `Style.Resources` Thumb-stripping block
- The `TabItem_PreviewMouseLeftButtonDown` click-rescue handler

**Files RESTORED in v0.0.33:**
- `OnPropertyChanged(nameof(Label))` in `TabViewModel.TimeZoneId` setter — native TabControl honors PropertyChanged on ItemTemplate bindings, so the binding cascade works again.

### Service runs only while UI is open (alpha simplification)

- `sc.exe create … start= demand` — NOT `auto`.
- `App.xaml.cs:62 TryStartService()` on `OnStartup`; `:67 TryStopService()` on `OnExit`.
- **Why:** avoids the "uninstalled-but-still-running service" failure mode in alpha. Migrating to `auto` for v1.0 is Planned (SPEC.md §22 / D-1).

### Tab-name refresh is bound, not imperative (per v0.0.33 — supersedes v0.0.32 rule)

- Native WPF `TabControl` honors `PropertyChanged` on `ItemTemplate` bindings reliably.
- `TabViewModel.TimeZoneId` setter raises `PropertyChanged(nameof(Label))`; the `{Binding Label}` re-renders the header automatically.
- The v0.0.32 "two-event imperative refresh" rule is **superseded** — that was a Dragablz workaround. With native TabControl, the binding cascade is the correct, reliable pattern.
- See "Why we dropped Dragablz (v0.0.33)" entry above for the historical context.

### Tab single-click reliability is native (per v0.0.33 — supersedes v0.0.20 rule)

- Native WPF `TabControl` selects on click reliably.
- The v0.0.20 `PreviewMouseLeftButtonDown` rescue handler is **gone** — that was a Dragablz workaround.

### Right-click → Tab Settings dialog, no context menu (per v0.0.23..v0.0.26)

- ContextMenu was deliberately removed in v0.0.26 (`MainWindow.xaml:267-275` comment).
- Right-click is wired to open `TabSettingsDialog` (per v0.0.23).

### Active 19pt Bold / Inactive 9pt tab fonts (per v0.0.28..v0.0.30)

- Dan iterated through several sizes; landed on +10pt active delta with active Bold for clarity at the strip's small height.

### Date strip on every theme (per v0.0.22)

- All 12 themes show day-of-week + date-of-month + month + year.
- Most use format `"ddd · MMMM d · yyyy"` upper.
- Hex and BinaryDigital encode the same four parts in their respective encodings.

### Daylight + Boulder Slate centered date (per v0.0.24)

- `_recenterDateReadoutOnUpdate = true` re-centers the date `TextBlock` per tick as string width changes.

### Encoder themes always 24-h (per v0.0.27)

- Binary, Hex, BinaryDigital ignore any 12-h override. The bit/digit-width rationale is the point of those themes.
- Other 9 themes default to 12-h on `Auto`.

### Three exception handlers in App constructor (per v0.0.16)

- `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`.
- Subscribed in **constructor**, not `OnStartup` (`base.OnStartup` constructs `MainWindow`; subscribing in `OnStartup` is too late).

### Two settings stores, atomic write, JsonExtensionData forward-compat

- `settings.json` (per-user) and `service.json` (per-machine).
- Atomic write: `tmp` + `Flush(true)` + `File.Move(overwrite: true)`.
- Corrupt-on-read renames to `path + ".broken-{unix-ts}"` and falls back to defaults.
- `[JsonExtensionData] UnknownFields` on every record so future schema versions round-trip without data loss.

### Authenticated Users on service ACL (not Interactive)

- `WellKnownSidType.AuthenticatedUserSid` for the service object's start/stop ACL.
- **Why:** lets unprivileged UI start/stop the service across logon sessions on managed corporate machines.
- Pipe ACL still uses `InteractiveSid` (correct narrower scope for IPC).

---

## Known constraints / gotchas

### Dragablz

- **`CollectionChanged.Replace` is unimplemented.** Mutating `ObservableCollection<TabViewModel>` in a way that produces `Replace` will silently crash. Use `Add`/`Remove`/`Move`, never `[index] = newItem`.
- Headers render in a separate visual subtree from item content.
- ItemTemplate bindings don't reliably honor PropertyChanged.

### Tooling

- **PowerShell mangles UTF-8 em-dashes.** Default encoding on PS 5.1 is UTF-16 LE. Use `Write` / `Edit` tool calls for docs. If PS is unavoidable: `[System.IO.File]::WriteAllText($p, $t, [System.Text.UTF8Encoding]::new($false))`.
- **Don't F5 from elevated VS** — silently fails to launch standard-user UI; service starts but no clock window.

### Privilege

- `SetSystemTime` returns Win32 1314 (`ERROR_PRIVILEGE_NOT_HELD`) when service runs as a non-LocalSystem account in dev. **Expected in dev**; verify via Event Log; do not "fix" by elevating dev account.
- UI must be `asInvoker` (no `requireAdministrator`) — only `ServiceInstaller.exe` carries `requireAdministrator`.

### Service

- `start= demand` lifecycle means the service does NOT run when UI is closed. Document this for users; it's an alpha simplification.

---

## Pending / open

### Awaiting Dan's go-ahead (per `feedback_no_merge_push_without_consent.md`)

- **Branch `tab-header-refresh-reliability`** is `local only, 2 commits ahead of master`. Carries v0.0.31 (two-phase dispatch) + v0.0.32 (imperative refresh). Awaiting "merge" / "push" instruction.

### Doc-only follow-ups (no version bump needed)

- `windows/SPEC.md` created 2026-05-01 (this session) — the regeneration baseline Dan asked for.
- `windows/CONTEXT.md` created 2026-05-01 (this session) — per the new standing rule.

### Open backlog → see `TODO.md`

As of v1.0.0 (2026-05-03) the open-work list lives in `windows/TODO.md` as the single source of truth — formerly scattered across this section and `SPEC.md` §21. Highlights:

- **Active queue:** Timer mode, Countdown mode (Dan's direct asks).
- **Phase 2:** magnetic snap on `FloatingClockWindow` (~280–390 LOC, 1–2 days).
- **Phase 3+:** ~29 items spanning tray / floating windows / sync flow / dialog UI / renderer animations / packaging (MSIX + ARM64) / Authenticode signing / privacy.
- **Tiny polish:** 3 stale-comment / help-corpus fixes that can ride alongside any commit.

Read `TODO.md` on session start. Move items from "open" to "Recently shipped" as they land.

---

## Version-bump rule reminder

Every code change Dan will see (commit / push / build he tests) must bump the **patch** in `ComTekAtomicClock.UI.csproj` (`<Version>`, `<AssemblyVersion>`, `<FileVersion>`) AND add a problem/solution entry to `CHANGELOG.md`. Both in the same commit. Trivial fixes exempt. Doc-only changes (like creating SPEC.md / CONTEXT.md) — no version bump.

## Doc-update-on-change rule reminder

Any code change must update every project doc that describes the area touched: `README.md`, `CHANGELOG.md`, `SPEC.md`, **`CONTEXT.md`**, in-code module headers. Same branch. Name the docs touched in the response.

---

## Session log (newest first)

### 2026-05-03 — v1.0.0 BETA framing + README rewrite (post-ship)

After publishing v1.0.0 to GitHub Releases as `isPrerelease: false` with title "First stable release," Dan asked to reframe as **BETA / first public release**. The 1.0 version commits to a stable-feature claim; the BETA tag honestly tells users broad-deployment testing has just started.

**Three label changes** (no code change, no version bump per the doc-only-doesn't-bump rule):

1. `README.md` — full rewrite. New BETA WARNING block at top; Download section with direct asset links; features list cleaned up (12 themes split correctly into 6 analog + 4 digital + 2 encoders; tear-away removed; Boulder/Brazil time source; per-face source label); Architecture table corrected (Service is `net8.0-windows`, not `net8.0`); `start= demand` instead of legacy `start= auto`; pointer to TODO.md / SPEC.md / CHANGELOG.md / CONTEXT.md; explicit "Reporting issues" section.
2. GitHub Release v1.0.0 — flipped `isPrerelease: true` via `gh release edit --prerelease`. Title updated to "v1.0.0 — First public release (BETA)" and notes body opening reframed accordingly.
3. `release/v1.0.0-release-notes.md` — BETA-aware opening line replacing the "first stable release" framing. (The Setup.exe + zip artifacts themselves are unchanged — same SHA-256 hashes.)

**Rationale for v1.0.0 + BETA both:** semver 1.0 means "we commit to this feature surface and won't break it without a major bump." BETA is an orthogonal claim about real-world readiness — "tested locally, not yet in 100 hands." Common pattern; lets the version number stay clean while tempering user expectations.

### 2026-05-03 — v1.0.0: First stable release; consolidated backlog into TODO.md

Symbolic milestone. Code jumps 0.0.39 → 1.0.0. Functionally v0.0.39 + version stamp + new TODO.md — no runtime change, but the version leap declares the core feature set is stable enough for end-user distribution.

What's stable: NIST + NTP.br stratum-1 sync, 12 themable clock faces, native WPF TabControl tabbed UI, free-floating clock windows, three-process architecture, atomic settings persistence, three-handler exception net.

What's deferred: ~38 items in `windows/TODO.md` — timer/countdown (active queue), magnetic snap (Phase 2), tray icon, color overrides, MSIX, Authenticode signing, ARM64.

**Build + ship:**
- `dotnet publish` (Release, win-x64, self-contained) for UI/Service/Installer → `release/v1.0.0/` (315 MB unzipped)
- `tools/installer.iss` parameterized on `MyAppVersion`, ISCC compiled → `release/ComTekAtomicClock-v1.0.0-Setup.exe` (97 MB)
- Portable zip → `release/ComTekAtomicClock-v1.0.0-win-x64-self-contained.zip` (134 MB)
- `git tag -a v1.0.0 a39c7ce` + `git push origin v1.0.0` (Dan's "damn the torpedoes" override of the Sunday rule)
- `gh release create v1.0.0` with both artifacts attached → <https://github.com/doxender/WindowsComTekAtomicClock/releases/tag/v1.0.0>

**Files changed in the v1.0.0 commit (a39c7ce):** csproj (0.0.39 → 1.0.0), tools/installer.iss (parameterized + 1.0.0), TODO.md NEW, CONTEXT.md (replaced backlog dump with TODO.md pointer), SPEC.md (front matter doc 1.4 → 2.0, §21 callout), CHANGELOG.md ([1.0.0] entry).

### 2026-05-03 — v0.0.39: Settings + Themes dialog Owner — center over originating window

Discharged the queued Settings-dialog-Owner bug Dan logged when he hit it on Sunday (was queued in this CONTEXT.md alongside timer/countdown).

Dialogs from `FloatingClockWindow`'s `⋯` menu were hardcoded to `Owner = Application.Current?.MainWindow`, so they centered on the main window — confusing if the floating clock was on a different monitor.

Fix: added explicit-owner overloads `OpenTabSettingsForOwner(TabViewModel, Window?)` and `OpenThemesPickerForOwner(TabViewModel, Window?)` on `MainWindowViewModel`. Each forwards to a private `*Core` method that uses `owner ?? Application.Current?.MainWindow` as the dialog Owner. `FloatingClockWindow.SettingsMenuItem_Click` and `ThemesMenuItem_Click` now call the overloads with `owner: this`.

The in-tab right-click and `?`-overlay paths still go through the existing `RelayCommand` (no owner passed → falls back to MainWindow, which is correct since those originate from the main window). Help/About were already correct (opened inline with `Owner = this` in the floating window's handlers).

**Files changed:** `ViewModels/MainWindowViewModel.cs` (Owner overloads + Core split for Themes path), `FloatingClockWindow.xaml.cs` (Settings + Themes menu handlers), `ComTekAtomicClock.UI.csproj` (0.0.38 → 0.0.39), `SPEC.md` (§13 new Dialog-Owner subsection + §21 queue item struck through), `CONTEXT.md` (this entry + Pending discharge), `CHANGELOG.md` (v0.0.39 entry). Build clean.

**Queue status:** still open — Timer mode, Countdown mode. Settings-Owner discharged.

### 2026-05-03 — v0.0.38: Daylight / Boulder Slate readout-alignment fix

Dan: *"On at least the daylight theme, the day is not centered above the digital time. They should be centered over each other on all screens."*

Diagnosis: `UpdateClock` recentered the date `TextBlock` on every tick (per v0.0.24's flag) but the time `TextBlock` was placed once at build with a `"00:00:00"` placeholder and never recentered. As actual time strings of varying width replaced the placeholder, the time's visual center drifted while the date stayed at Cx. Result: visible misalignment.

Fix: extended the recenter block to recenter **both** date and time. Renamed the flag `_recenterDateReadoutOnUpdate` → `_recenterTextReadoutsOnUpdate` to reflect the broader scope. Affects Boulder Slate and Daylight (the only two themes that set the flag — every other theme uses panel-wrapped readouts that auto-center).

**Files changed:** `Controls/ClockFaceControl.xaml.cs` (1 field rename, 1 reset, 1 read+extend, 2 set sites), `ComTekAtomicClock.UI.csproj` (0.0.37 → 0.0.38), `SPEC.md` (§10 Daylight + Boulder Slate "Readout centering" notes), `CONTEXT.md` (this entry + repo state), `CHANGELOG.md` (v0.0.38 entry).

### 2026-05-03 — v0.0.37: TabSettingsDialog height bumped +100 px

Dan testing v0.0.36 the day after ship: the Settings dialog was clipping the Save row because the new Time Source radio group consumed ~120 px the original 540 px window couldn't accommodate. Bumped `Height` 540 → 640 in `TabSettingsDialog.xaml`. One-line fix; full doc + version pass per the standing rule.

This is the first follow-up commit after the v0.0.36 ship — confirms Phase-1 (Dragablz removal + native TabControl + multi-source) is stable in real use; only the window-sizing oversight needed a touch-up.

### 2026-05-01 — v0.0.36: Time-source picker (Boulder + Brazil); per-face source label; dynamic NIST badge

Dan asked: *"is there any way to add south american time sync options? ... I just want to add to the settings, Which Time Source. We'll allow Boulder or NTP.br — operated by NIC.br (Brazilian Network Information Center). Also make the changes to the atomic clock face to reflect which clock we are using. Also, add Brazil or Boulder to the header on all clock faces just to be cool."*

Three things shipped together:

1. **Backend.** New `TimeSource { Boulder, Brazil }` enum in `Shared/Settings/SettingsModel.cs`. Added to both `GlobalSettings` (UI's view) and `ServiceConfig` (Service's view). Refactored `Service/Sync/NistPool.cs` → `TimeSourcePool.cs` with two pools: Boulder = 10 NIST stratum-1 servers + `time.nist.gov` anycast (unchanged); Brazil = 5 NTP.br servers (`a/b/c/d/gps.ntp.br`) + `a.ntp.br` anycast. `SyncWorker` now walks the active source's pool. `MinPerServerPoll` of 4 s satisfies both NIST AUP (≥ 4 s) and NTP.br AUP (≥ 1 s).
2. **Settings dialog.** Added a Time Source RadioButton group below Sync frequency in the "ALL CLOCKS ON THIS PC" section. Two-line item layout: primary label + small subtitle showing operator + anycast hostname. On Save, writes `service.json` once with both sync-frequency and time-source changes; exposes `ChosenTimeSource` to `MainWindowViewModel.OpenTabSettingsCore` so it can mirror the value into `_settings.Global.TimeSource` and trigger the on-face refresh.
3. **Per-face rendering.** Added `TimeSourceLabelProperty` and `TimeSourceBadgeProperty` DependencyProperties on `ClockFaceControl`. New `AddSourceLabel()` helper paints `TimeSourceLabel` (`BOULDER` or `BRASIL`) at top-center of every theme (Cascadia 11pt SemiBold, warm-amber `#FFCC00` at 70% opacity). Atomic Lab's NIST-panel subtitle text changed from a hardcoded `"NIST · BOULDER · CO"` to `TimeSourceBadge` (returns the parallel `"NTP.BR · SÃO PAULO · BR"` for Brazil). Theme rebuild on TimeSource change is the refresh mechanism. Floating windows wire the values via code-behind (DataContext is TabViewModel, not MainWindowViewModel) by subscribing to `MainWindowViewModel.PropertyChanged`.

**Service catch-up:** Service re-reads `service.json` on each sync iteration. Switching source takes effect on the next sync (within configured frequency, default 12 h). Force-resync IPC trigger remains Phase-2 Planned.

**Files changed:** `Shared/Settings/SettingsModel.cs` (TimeSource enum + props), `Service/Sync/NistPool.cs` DELETED → `TimeSourcePool.cs` NEW, `Service/Sync/SyncWorker.cs`, `UI/Dialogs/TabSettingsDialog.xaml` + `.xaml.cs`, `UI/ViewModels/MainWindowViewModel.cs`, `UI/Controls/ClockFaceControl.xaml.cs` (DPs + AddSourceLabel + Atomic Lab badge), `UI/MainWindow.xaml` (DataTemplate bindings), `UI/FloatingClockWindow.xaml` + `.xaml.cs` (named ClockFace + PropertyChanged subscription), `UI/ComTekAtomicClock.UI.csproj` (0.0.35 → 0.0.36), `SPEC.md` v1.3 → v1.4 (front matter, §4 multi-source pool, §5 TimeSource fields, §10 universal source label, §13 dialog field, §21 status), `CHANGELOG.md` (v0.0.36 entry), `CONTEXT.md` (this entry + repo state).

**Build:** compilation clean (no `error CS####` or `error MC####`); 4 file-copy errors expected because Dan's running v0.0.34 has Shared.dll locked. Close + rebuild + F5 to deploy.

### 2026-05-01 — v0.0.35: FloatingClockWindow single ⋯ overlay button

After v0.0.34 shipped, Dan opened a "+ New window" floating clock and asked *"is there any way to change the tz and face on the new window?"* — confirming the discoverability issue I'd suspected: settings + theme picker were buried under the `?` overlay, and `?` reads as Help not Settings.

Dan's call: remove the `✕` overlay (redundant with OS title-bar X), pick a "hip" glyph that reads as "click here to change something", consolidate all menu items under one button. Picked the modern-Windows-standard three-dot "more options" affordance — `<ui:SymbolIcon Symbol="MoreHorizontal20"/>` — same glyph Windows 11 itself uses across the shell.

**Floating window now has ONE overlay button** (`⋯`, top-right of clock face) with all 5 items:

- Settings… (renamed from "Tab settings…" — "tab" is wrong on a window)
- Themes…
- Bring back into tabs
- Help…
- About…

Main-window tabs unchanged — they still need their `✕` (close-tab) and `?` overlays.

**Files changed:** `FloatingClockWindow.xaml` (single `⋯` Button replaces stacked ✕ + ? pair), `FloatingClockWindow.xaml.cs` (removed `CloseWindowButton_Click` + `HelpButton_Click`; added `MoreOptionsButton_Click`; renamed `TabSettingsMenuItem_Click` → `SettingsMenuItem_Click`), `ComTekAtomicClock.UI.csproj` (0.0.34 → 0.0.35), `SPEC.md` v1.2 → v1.3 (§6 Floating clock window rewritten + closing semantics callout), `CONTEXT.md` (this entry + repo state), `CHANGELOG.md`.

**Build:** compilation clean (no CS or MC errors); post-compile file copy errored because Dan's running v0.0.34 had Shared.dll locked. Close + rebuild + F5 to test v0.0.35.

### 2026-05-01 — v0.0.34: First-run polish — toolbar contrast + remove tab right-click menu

Two small UX fixes from Dan's first-run testing of v0.0.33:

1. **Toolbar button contrast.** Plain `<Button>` controls in the new tab toolbar inherited the WPF-UI Dark theme's brushes and rendered as near-invisible low-contrast on the `#F5F5F5` toolbar background. Switched to `<ui:Button Appearance="Secondary">` with explicit `Foreground="#0A0A0A"`. Same Fluent idiom used by the §1.9 banner.

2. **Tab right-click ContextMenu removed.** Dan: *"right click on tab still brings up two menu options. Remove that."* The v0.0.33 two-item menu reintroduced the v0.0.23-era pattern Dan had previously rejected — he wants right-click to directly open Tab Settings, no menu. Removed the `ContextMenu` Setter; added `PreviewMouseRightButtonDown` handler that opens `OpenTabSettingsForCommand` directly. The "Open in new window" migration affordance moved to the "?" overlay menu on the clock face (alongside Themes / Help / About) so it stays reachable without cluttering the tab strip.

**Files changed:** `MainWindow.xaml` (toolbar buttons → ui:Button, tab ContextMenu removed, PreviewMouseRightButtonDown EventSetter added, "Open in new window" added to ? overlay menu), `MainWindow.xaml.cs` (handlers swapped: removed `TabContextSettings_Click` + `TabContextOpenInNewWindow_Click`; added `TabItem_PreviewRightButtonDown` + `OpenInNewWindowMenuItem_Click`), `ComTekAtomicClock.UI.csproj` (0.0.33 → 0.0.34), `SPEC.md` v1.1 → v1.2 (§7 per-tab interactions table; §8 TabItem defaults + toolbar example), `CHANGELOG.md` (v0.0.34 entry).

**Build:** 0 warnings, 0 errors.

### 2026-05-01 — v0.0.33: Dropped Dragablz; native WPF TabControl; tear-away gone

Dan identified Dragablz as the root cause of nearly every tab-related bug fought across v0.0.14..v0.0.32 (header refresh PropertyChanged unreliability, click/drag classifier swallowing single clicks, NotImplementedException on CollectionChanged.Replace, headers in a separate visual subtree). Picked Option C from the three-paths discussion: replace Dragablz with native `System.Windows.Controls.TabControl`, drop tear-away gesture entirely, add magnetic snap as Phase-2 todo.

**Files changed (all in `windows/src/ComTekAtomicClock.UI/`):**
- `ComTekAtomicClock.UI.csproj` — removed `Dragablz 0.0.3.234` package reference; bumped `<Version>` to 0.0.33.
- `MainWindow.xaml` — replaced `dragablz:TabablzControl` with native `TabControl`; added flat-rectangular `ControlTemplate`-replaced `TabItem`; added "+ New tab" / "+ New window" toolbar above the strip; added per-tab right-click ContextMenu (Tab settings… / Open in new window).
- `MainWindow.xaml.cs` — deleted `SetTabHeaderInAllDisplays`, `EnumerateVisualDescendants`, `TabItem_PreviewMouseLeftButtonDown` rescue handler, multi-strategy `TryFindTabFromContextMenuItem`. Added `TabContextSettings_Click`, `TabContextOpenInNewWindow_Click` handlers.
- `FloatingClockWindow.xaml` — refactored to a single-clock window (no internal tab strip). Title bar shows `{Binding Label}`. "?" overlay menu adds "Tab settings…" / "Themes…" / "Bring back into tabs" / "Help…" / "About…".
- `FloatingClockWindow.xaml.cs` — rewritten. Constructor takes a `TabViewModel`; exposes it via `Tab` property. New `BringIntoTabsMenuItem_Click` handler.
- `Services/AppInterTabClient.cs` — **DELETED** (Dragablz `IInterTabClient`).
- `ViewModels/TabViewModel.cs` — restored `OnPropertyChanged(nameof(Label))` in `TimeZoneId` setter (the v0.0.32 "two-event rule" comment is gone, replaced with v0.0.33 commentary about native TabControl honoring the cascade).
- `ViewModels/MainWindowViewModel.cs` — removed call to `MainWindow.SetTabHeaderInAllDisplays`. Added three commands: `OpenInNewWindowCommand`, `NewClockWindowCommand`, `BringWindowIntoTabsCommand`. Added `_openFloatingWindows` registry. Added `SpawnFloatingWindow` helper that wires `Closed` → purge persistence if the user X'd out (rather than bringing back into tabs).

**Build verified:** `dotnet build src/ComTekAtomicClock.UI/ComTekAtomicClock.UI.csproj -c Debug` → 0 warnings, 0 errors.

**Docs updated in same commit:** `SPEC.md` v1.1 (front matter, §3 tech stack, §6 UI shell, §7 tab behavior, §8 tab strip styling, §17 quirks, §21 implemented/planned, §22 resolved contradictions, end-of-doc), `CONTEXT.md` (this file — new "Why we dropped Dragablz" entry, superseded the v0.0.32 imperative-refresh standing decision, added Phase-2 snap to Planned, added this session-log entry), `CHANGELOG.md` (v0.0.33 entry).

**Phase 2 (deferred):** magnetic snap on `FloatingClockWindow` — implementation sketch in this CONTEXT and SPEC.md §21.

### 2026-05-01 — Created SPEC.md regeneration baseline + CONTEXT.md

- Dan asked for a fresh, exhaustive requirements + design doc to enable from-scratch regeneration without losing features. Two prior subagent attempts stalled (large-Write watchdog kill + filesystem-scope denial).
- Wrote `SPEC.md` (23 sections, 92 KB, 1546 lines) directly from this session, incrementally via Edit calls. Covers all 12 themes at fine granularity (fonts, hex codes, layout coords), the three-process architecture, IPC contract, settings schemas, build/install/run, exception handling, known quirks, full Implemented/Planned status table, and resolved contradictions.
- Created `CONTEXT.md` (this file) per the new standing rule established earlier this session.
- No code changed; no version bump.

### 2026-04-30..05-01 — v0.0.14 → v0.0.32 iteration cycle

Tab-header refresh + tab-click reliability + visual styling pass. Eight iterations to land the v0.0.32 imperative `SetTabHeaderInAllDisplays` design. See CHANGELOG.md for per-version detail.
