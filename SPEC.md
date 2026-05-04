# ComTekAtomicClock — Windows Desktop App Specification

| | |
|---|---|
| **Document version** | 2.3 |
| **Date** | 2026-05-03 |
| **Code baseline** | v1.1.2 |
| **Status** | Authoritative — supersedes `requirements.txt` (591 lines, dated 2026-04-25) |
| **Author** | Daniel V. Oxender |
| **v1.3 → v1.4 changes** | Time-source picker added: machine-wide `TimeSource` enum (Boulder / Brazil) selectable from the Settings dialog. Boulder = NIST stratum-1 pool (default, unchanged). Brazil = NTP.br stratum-1 pool (NIC.br / São Paulo). Atomic Lab face's NIST-panel subtitle now dynamic (`"NIST · BOULDER · CO"` ↔ `"NTP.BR · SÃO PAULO · BR"`). Every face shows a single-word `BOULDER` or `BRASIL` header label (warm-amber Cascadia Code 11pt, top-center) via a uniform `AddSourceLabel` helper. See §4, §5, §10, §13, §21; CHANGELOG.md `[0.0.36]`. |
| **v1.2 → v1.3 changes** | `FloatingClockWindow` overlay buttons consolidated: `✕` (redundant with OS title-bar X) + `?` removed; replaced with a single `⋯` (Fluent `SymbolRegular.MoreHorizontal20`) "more options" button hosting all menu items (Settings… / Themes… / Bring back into tabs / Help… / About…). "Tab settings…" renamed to "Settings…" on the floating-window menu. See §6, CHANGELOG.md `[0.0.35]`. |
| **v2.2 → v2.3 changes** | CaptJohn — real hour/minute hand opacity in the Hora Chapín ON branch raised from **7.5% to 90%** (peak still 100% during noon / 5 PM flash). At 7.5% the hands were too faint to read against the parchment + logo backdrop; the lazy/jittered novelty still reads because the jitter hand is full-black ink at 100% on top. See §10 Theme #7; CHANGELOG.md `[1.1.2]`. |
| **v2.1 → v2.2 changes** | CaptJohn — Jolly Roger ☠ overlay button + ContextMenu popup wired in (lower-left, visible only on this theme), hosting **Hora Chapín** (checkable, persistent, two-way bound to `TabSettings.CaptJohnHoraChapin`) and **Almuerzo** / **Fini** momentary demos that pin time to 12:00 / 17:00 while the popup is open and clear on `Closed`. **Default Hora Chapín state flipped to OFF** — the regular numberless face is now the default presentation; the lazy/jittered novelty mode is opt-in. See §10 Theme #7; CHANGELOG.md `[1.1.1]`. |
| **v2.0 → v2.1 changes** | Hand-length pass on every analog face — hour −24 px (≈ 1/4 inch at 96 DPI), minute +24 px. Net effect: minute hand is 84–86 px longer than the hour hand on Atomic Lab / Boulder Slate / Aero Glass / Cathode / Concourse / Daylight (was 36–38 px). Hand thicknesses, colors, and second-hand lengths unchanged. **Plus: shipped the new `Theme.CaptJohn` (Captain John's Marina) — Theme #7 in the analog cluster.** Parchment + brass face, marina logo at 40%, Cinzel-Bold "12"/"5" numerals that flash at noon and 5 PM (5 s on / 5 s off over a ±5 min window), lazy "Hora Chapín" jitter minute hand (random ±3 min, sync at top of hour), real-time hands at 7.5% / 100% opacity tied to flash window. Total theme count 12 → 13. See §10 Theme #7; CHANGELOG.md `[1.1.0]`. (Jolly Roger overlay popup wired in v1.1.1.) |
| **v1.4 → v2.0 changes** | First stable release stamp. Code baseline jumps 0.0.39 → 1.0.0. v0.0.37 (dialog height) + v0.0.38 (Daylight/Boulder Slate readout alignment) + v0.0.39 (dialog Owner — center over originating window) all rolled in. Doc-version 1.4 → 2.0 reflects the symbolic stability rather than further content changes. `windows/TODO.md` introduced as the canonical open-work list (replaces scattered "Pending" / "Planned" lists). See CHANGELOG.md `[1.0.0]`. |
| **v1.1 → v1.2 changes** | First-run polish on v0.0.33: toolbar `+ New tab` / `+ New window` switched to `ui:Button` for contrast; tab right-click ContextMenu removed (right-click now directly opens Tab Settings dialog); "Open in new window" migration moved to the "?" overlay menu on the clock face. See §7 / §8 and CHANGELOG.md `[0.0.34]`. |
| **v1.1 changes** | Dragablz removed (replaced with native WPF `TabControl`); tear-away gesture removed; explicit "+ New window" / "Open in new window" / "Bring back into tabs" commands added; magnetic snap added as Phase-2 Planned. See §22 / §17 / §21; CHANGELOG.md `[0.0.33]`. |

## How to use this document

This is a **regeneration baseline.** A team handed this document plus the design SVGs in `windows/design/themes/` should be able to rebuild the entire Windows desktop app without losing features. It captures the *current state* of the code (v0.0.32) at fine granularity — every theme's font, color, hex code, hand geometry, and layout coordinate is enumerated — and resolves the contradictions accumulated across `requirements.txt` and the iterative v0.0.14→v0.0.32 bug-fix cycle.

**Source-of-truth principle.** Where this spec, the older `requirements.txt`, and the current code disagree, **the code wins**. This document describes the *end state* that exists today, not the journey. v-numbers are cited only where they explain *why* a particular decision was made (e.g. "tab-name imperative refresh, per v0.0.32"). The companion `windows/docs/code-vs-spec-audit.md` records the spec-↔-code drift that motivated this rewrite.

**Implementation status.** Every requirement in this document is tagged in §20 as either:

- **Implemented** — present in current code; reproduce as-is.
- **Planned** — in scope for v1 but not yet coded; do not let regen team drop these.

A fresh implementation that ships only the Implemented items is a faithful regeneration of v0.0.32; ticking off the Planned items is the path to v1.0.

**Companion documents** (read alongside this spec):

```
windows/SPEC.md                          ← this document
windows/CHANGELOG.md                     ← per-version problem/solution log
windows/CONTEXT.md                       ← living decision log (current constraints, gotchas, pending questions)
windows/docs/code-vs-spec-audit.md       ← drift findings between this spec and the legacy requirements.txt
windows/design/themes/*.svg              ← canonical theme preview art (12 files)
windows/requirements.txt                 ← legacy spec (KEEP for history; superseded by this file)
```

---

## §1 — Overview

ComTekAtomicClock is a **Windows desktop atomic-clock app** for Windows 10 (1809+) and Windows 11. Two value propositions, in priority order:

1. **Beautiful, themeable clock display.** A Chrome-style tabbed UI hosts one or more clock faces, each on its own IANA time zone, each rendered in one of **12 user-selectable themes**: 6 analog, 4 digital-only, and 2 specialty encoders (binary, hex). Tabs can be torn off into free-floating windows.
2. **Stratum-1 atomic time sync.** A companion Windows Worker Service queries the NIST stratum-1 NTP pool (`time.nist.gov` anycast plus 10 named servers across Gaithersburg MD and Fort Collins CO), validates the response per RFC 4330 SNTP, and applies the correction to the system clock via `SetSystemTime`.

The two halves communicate via a single named pipe (length-prefixed JSON envelopes) and persist state in two settings files — one per-user, one per-machine.

**Visual identity.** Atomic Lab is the default theme: a black-and-amber lab-bench instrument aesthetic with a `NIST · BOULDER · CO` badge. The product evokes precision instrumentation rather than wallpaper-clock kitsch.

**Non-goals.** Cross-platform (macOS/Linux), GPS/atomic-hardware integration, NTS (RFC 8915), cloud sync, and Microsoft-Store/MSIX delivery are explicitly out of scope for the alpha. MSIX packaging *is* spec'd for v1.0 (Planned, §20).

---

## §2 — Architecture

### Three-process model

```
┌─────────────────────────────┐         ┌──────────────────────────────────┐
│  ComTekAtomicClock.UI.exe   │         │  ComTekAtomicClock.Service.exe   │
│  (WPF / .NET 8 / WinExe)    │  named  │  (Worker Service / .NET 8)       │
│  Standard user, asInvoker   │◀────────┤  LocalSystem, requires           │
│                             │  pipe   │  SE_SYSTEMTIME_NAME              │
│  • Tabs + clock faces       │  IPC    │                                  │
│  • Settings dialog          │         │  • SNTP/NIST pool walk           │
│  • Help / About / Themes    │         │  • SetSystemTime()               │
│  • Service-state banner     │         │  • IPC server (4 pipe instances) │
└──────────┬──────────────────┘         └──────────────────────────────────┘
           │ writes                            ▲ reads
           │                                   │
           ▼                                   │
  ┌──────────────────────┐                   ┌─┴────────────────────────────┐
  │ %APPDATA%\           │                   │ %ProgramData%\               │
  │ ComTekAtomicClock\   │                   │ ComTekAtomicClock\           │
  │ settings.json        │                   │ service.json                 │
  │ (per-user UI state)  │                   │ (per-machine sync config)    │
  └──────────────────────┘                   └──────────────────────────────┘

           ┌──────────────────────────────────────────┐
           │  ComTekAtomicClock.ServiceInstaller.exe  │
           │  (Console / .NET 8, requireAdministrator) │
           │  Invoked once via UAC ("runas") to        │
           │  sc.exe create / sdset / delete the       │
           │  Windows service. Not in the run-time     │
           │  hot path.                                │
           └──────────────────────────────────────────┘
```

### Key architectural decisions

- **Service runs only while UI is open.** UI launches the service on `App.OnStartup` and stops it on `App.OnExit`. This is a deliberate departure from the legacy spec's "Automatic" start mode (see §21 — Resolved Contradictions). Service start mode is set to `demand` by `sc.exe create … start= demand`.
- **All privilege escalation is concentrated in `ServiceInstaller.exe`.** It is the only process with `requireAdministrator` in its manifest. The UI runs as a standard user (`asInvoker`) and never elevates itself; instead it shells the installer with `Verb = "runas"` to trigger a UAC prompt.
- **Settings split.** Per-user UI state (tabs, themes, window positions) lives in `%APPDATA%`; per-machine sync config (sync interval, sync server) lives in `%ProgramData%`. The Service reads `service.json` only; the UI reads/writes both.
- **Forward-compat via `[JsonExtensionData]`.** Every settings record carries a `Dictionary<string, JsonElement>? UnknownFields` so a settings file written by a newer app version is round-tripped without data loss when read by an older version.
- **Atomic settings writes.** `SettingsStore.WriteAtomic` writes to `path + ".tmp"`, calls `Flush(true)`, then `File.Move(tmp, path, overwrite: true)`. Corrupt-on-read files are renamed to `path + ".broken-{unix-ts}"` and the store falls back to defaults.

### Threading model

- **UI thread (Dispatcher).** All XAML / WPF work. Three timers attached to `MainWindowViewModel`:
  - **1 s tick** — re-render the "Last sync: …" status text.
  - **4 s tick** — poll Service Control Manager for service state changes.
  - **5 s tick** (every 5th 1-s tick, gated on `ServiceState == Running`) — IPC `LastSyncStatusRequest` to refresh sync status.
- **Service thread (BackgroundService).** SyncWorker loop runs on `IHostedService` background; sleeps for the configured interval between sync attempts; cancellable on host shutdown.
- **IPC accept loop.** `IpcServer` runs up to 4 concurrent `NamedPipeServerStream` instances, async, byte mode, 64 KiB buffers in/out.

---

## §3 — Technology Stack

### Solution layout

```
windows/ComTekAtomicClock.slnx
└── windows/src/
    ├── ComTekAtomicClock.UI/              ← WPF app (WinExe, net8.0-windows)
    ├── ComTekAtomicClock.Service/         ← Worker Service (Exe, net8.0-windows)
    ├── ComTekAtomicClock.ServiceInstaller/← Console UAC helper (Exe, net8.0-windows)
    └── ComTekAtomicClock.Shared/          ← Settings models + IPC contracts (DLL, net8.0)
```

### Target frameworks

| Project | TFM | Output |
|---|---|---|
| ComTekAtomicClock.UI | `net8.0-windows` | `WinExe`, `<UseWPF>true</UseWPF>` |
| ComTekAtomicClock.Service | `net8.0-windows` | `Exe` |
| ComTekAtomicClock.ServiceInstaller | `net8.0-windows` | `Exe` (`requireAdministrator` manifest) |
| ComTekAtomicClock.Shared | `net8.0` | `DLL` (no Windows-specific APIs) |

`<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` everywhere. C# 12 language level (default for .NET 8 SDK).

### NuGet packages — exact versions

**ComTekAtomicClock.UI:**

```xml
<PackageReference Include="SharpVectors.Reloaded" Version="1.8.5" />
<PackageReference Include="System.ServiceProcess.ServiceController" Version="8.0.0" />
<PackageReference Include="WPF-UI" Version="4.2.1" />
```

> **Tabs use the BCL `System.Windows.Controls.TabControl`**, not a third-party library. Dragablz `0.0.3.234` was removed in v0.0.33 — it was the source of nearly every tab-related bug fought across v0.0.14..v0.0.32 (header refresh PropertyChanged unreliability, click/drag classifier swallowing single clicks, `NotImplementedException` on `CollectionChanged.Replace`, headers in a separate visual subtree). See §17 for the lineage and §22 for the resolved decision.

**ComTekAtomicClock.Service:**

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
<PackageReference Include="System.IO.Pipes.AccessControl" Version="5.0.0" />
```

**ComTekAtomicClock.Shared:** none (uses only BCL `System.Text.Json`).

### Why these specific dependencies

- **WPF-UI 4.2.1** — Fluent / WinUI-styled controls, FluentWindow with Mica backdrop, theme dictionaries (`ThemesDictionary Theme="Dark"` and `ControlsDictionary` merged in `App.xaml`).
- **SharpVectors.Reloaded 1.8.5** — `SvgViewbox` for rendering theme preview SVGs in the Themes gallery dialog at runtime.
- **System.ServiceProcess.ServiceController 8.0.0** — `ServiceController` API for SCM state polling from the UI.

### App icon + theme art

- **`Assets\AppIcon.ico`** — embedded as both `<ApplicationIcon>` (taskbar / Alt-Tab / file explorer) and `<Resource>` (so `Window.Icon` can load via pack URI at runtime). Generated by `windows/tools/build-icon.ps1` from a master PNG; regenerate after design changes.
- **Theme preview SVGs** — 12 files under `windows/design/themes/`, linked into the UI assembly as `<Resource Link="Assets\themes\<name>.svg">` so the design folder remains the single source of truth. Loaded at runtime via `pack://application:,,,/Assets/themes/<name>.svg`.

---

## §4 — Service (`ComTekAtomicClock.Service`)

### Identity

| | |
|---|---|
| **Service name** | `ComTekAtomicClockSvc` (referenced from `Service/Program.cs:12`, `App.xaml.cs:25`, `ServiceInstaller/Program.cs:40`, `ServiceStateChecker.cs:28`) |
| **Display name** | `ComTek Atomic Clock — time sync` (em-dash is U+2014) |
| **Account** | `LocalSystem` (default for Worker Services hosted via `AddWindowsService`) |
| **Start mode** | `demand` (manually started by UI on launch, stopped on exit). NOT `auto` — see §21. |
| **Event Log source** | `ComTekAtomicClock` (configurable via `ServiceConfig.EventLogSource`) |

### Multi-source stratum-1 pool registry (v0.0.36+)

The pool is selected per-machine via `ServiceConfig.TimeSource` (Boulder / Brazil). Defined in `Service/Sync/TimeSourcePool.cs`. Refresh from each operator's published list per release.

#### Boulder — NIST stratum-1 (default)

Refreshed from <https://tf.nist.gov/tf-cgi/servers.cgi>.

| Endpoint | Role |
|---|---|
| `time.nist.gov` | Anycast — load-balances across the whole NIST pool. Default primary for Boulder source. |
| `time-a-g.nist.gov` | Stratum-1, Gaithersburg, MD |
| `time-b-g.nist.gov` | Stratum-1, Gaithersburg, MD |
| `time-c-g.nist.gov` | Stratum-1, Gaithersburg, MD |
| `time-d-g.nist.gov` | Stratum-1, Gaithersburg, MD |
| `time-e-g.nist.gov` | Stratum-1, Gaithersburg, MD |
| `time-a-wwv.nist.gov` | Stratum-1, Fort Collins, CO (WWV/WWVB site) |
| `time-b-wwv.nist.gov` | Stratum-1, Fort Collins, CO |
| `time-c-wwv.nist.gov` | Stratum-1, Fort Collins, CO |
| `time-d-wwv.nist.gov` | Stratum-1, Fort Collins, CO |
| `time-e-wwv.nist.gov` | Stratum-1, Fort Collins, CO |

> Note: NIST's primary atomic clocks (NIST-F1, NIST-F2) live in **Boulder, CO**. The public NTP servers serve their time over IP from Gaithersburg and Fort Collins. The "Boulder" source name comes from the brand identity.

#### Brazil — NIC.br / NTP.br stratum-1

Refreshed from <https://ntp.br/guia-mais-rapida.php>.

| Endpoint | Role |
|---|---|
| `a.ntp.br` | Default primary for Brazil source. NIC.br, São Paulo, BR. |
| `b.ntp.br` | Stratum-1, GPS-disciplined, São Paulo |
| `c.ntp.br` | Stratum-1, GPS-disciplined, São Paulo |
| `d.ntp.br` | Stratum-1, GPS-disciplined, São Paulo |
| `gps.ntp.br` | Stratum-1, explicit GPS-disciplined endpoint |

### Pool-walk algorithm (`TimeSourcePool.GetWalkOrder(source, primary)`)

1. Yield `primary` for the configured `source`.
2. Yield every other server in that source's pool not equal to `primary`, in **randomized order** (Fisher–Yates shuffle, `Random.Shared`).
3. The first server that returns a signature-validated SNTP response wins; the rest are not contacted on that attempt.

There is **no fallback to a different source's pool.** If the entire active pool is unreachable, the worker logs a Warning and returns; the next sync attempt is the next scheduled interval (and the user can switch source via Settings).

The `MinPerServerPoll` of 4 s satisfies both NIST AUP (≥ 4 s) and NTP.br AUP (≥ 1 s).

### SNTP wire protocol (RFC 4330 / NTPv4)

- **Packet:** 48 bytes. First byte = `0x23` = `LI=0 | VN=4 | Mode=3 (client)`. (`Service/Sync/NtpPacket.cs:31-32`)
- **Epoch:** `1900-01-01 00:00:00 UTC` (`NtpEpoch`).
- **Transport:** UDP/123.
- **Per-server timeout:** 5 seconds.
- **Per-server poll floor:** ≥ 4 seconds between failed servers within one attempt (NIST AUP).
- **Response validation** (`NtpPacket.ParseResponse`):
  - `mode != 4` → reject (not a server response).
  - `vn` must be 3 or 4 (we sent 4; some servers respond with 3).
  - Leap indicator `LI == 3` (server unsynchronized) → reject.
  - Stratum must be in `[1, 15]` (0 = unspecified, 16 = unsynchronized).

### Sync cadence

- **First sync:** runs immediately on service start, before the first interval delay.
- **Interval clamp:** `[15 minutes, 24 hours]` enforced by `SyncWorker.LoadIntervalFromConfig`.
- **Default interval:** `12 hours` (`ServiceConfig.SyncInterval`). The UI Settings dialog offers exactly three choices: `6 / 12 / 24 hours`. Hand-edited values in `service.json` outside these choices are still honored, subject to the clamp.

### Setting the system clock

- P/Invoke: `kernel32!SetSystemTime(SYSTEMTIME*)` from `Service/Sync/SystemTime.cs`.
- Required privilege: `SE_SYSTEMTIME_NAME`. Granted to `LocalSystem` by default; in dev environments running the service as a less-privileged account, `SetSystemTime` returns Win32 error 1314 (`ERROR_PRIVILEGE_NOT_HELD`) — known dev limitation, not a bug.
- **Correction is applied unconditionally.** Large-offset confirmation flow (toast `[Apply][Skip]` per legacy spec §2.5) is **Planned**, not Implemented. `SyncWorker` logs a Warning when `|offset| ≥ LargeOffsetThresholdSeconds` (default 5 s) but applies the correction immediately.

### IPC server

| | |
|---|---|
| **Pipe name** | `ComTekAtomicClock.UiToService` (constant `PipeNames.UiToService`) |
| **Schema version** | `1` (constant `IpcSchema.CurrentVersion`) |
| **Wire format** | `[4 bytes little-endian int32 length][N bytes UTF-8 JSON]` (`IpcWireFormat.cs`) |
| **Max payload** | 1 MiB (`IpcWireFormat.MaxPayloadBytes`) |
| **Concurrent server instances** | 4 |
| **Buffer sizes** | 64 KiB in, 64 KiB out |
| **Mode** | async, byte mode |
| **Pipe ACL** | grants `WellKnownSidType.InteractiveSid` + `WellKnownSidType.LocalSystemSid` |

**Schema-version mismatch handling:** if an incoming envelope's `SchemaVersion` differs from `IpcSchema.CurrentVersion`, the server logs a Warning and processes the message anyway. (Versioned-but-permissive contract.)

### IPC message types (`IpcMessageType` enum)

| Direction | Type | Payload |
|---|---|---|
| UI → Svc | `SyncNowRequest` | `{}` (returns cached snapshot today; full re-sync trigger is Planned) |
| Svc → UI | `SyncNowResponse` | `SyncStatus` |
| UI → Svc | `LastSyncStatusRequest` | `{}` |
| Svc → UI | `LastSyncStatusResponse` | `SyncStatus` |
| Svc → UI | `ConfirmLargeOffsetRequest` | `ConfirmLargeOffsetRequest(string ServerHost, double OffsetSeconds, DateTimeOffset DetectedAtUtc)` (Planned — Service does not yet emit) |
| UI → Svc | `ConfirmLargeOffsetResponse` | `ConfirmLargeOffsetResponse(bool Apply)` (logged-only today) |
| Svc → UI | `StatusChangedNotification` | `SyncStatus` |

### `SyncStatus` envelope shape

```csharp
record SyncStatus(
    DateTimeOffset AttemptedAtUtc,
    bool Success,
    string? ServerHost,
    double? OffsetSeconds,
    string? ErrorMessage);
```

### Service-object ACL (set by the installer via `sc.exe sdset`)

The pipe ACL grants Interactive users; the service-object ACL grants **Authenticated Users** start/stop. The wider scope on the service object lets an unprivileged UI process call `sc.exe start` / `sc.exe stop` across logon sessions without re-elevating.

| Trustee | SDDL rights | Meaning |
|---|---|---|
| `AU` (Authenticated Users) | `CCLCSWRPWPLOCRRC` | query / start / stop / interrogate / read |
| `BA` (Built-in Administrators) | `CCDCLCSWRPWPDTLOCRSDRCWDWO` | full |
| `SY` (LocalSystem) | `CCLCSWLOCRRC` | read + interrogate |

---

## §5 — Settings Model

Two JSON files. Both atomic-write (write-tmp / fsync / rename); both `[JsonExtensionData]`-extended for forward compat. Reader/writer in `ComTekAtomicClock.Shared.Settings.SettingsStore`.

### Per-user: `%APPDATA%\ComTekAtomicClock\settings.json`

Top-level shape `AppSettings`:

```jsonc
{
  "SchemaVersion": 1,
  "Global": { ... GlobalSettings ... },
  "Tabs":    [ ... TabSettings, in display order ... ],
  "Windows": [ ... WindowSettings, free-floating clocks ... ]
}
```

#### `GlobalSettings`

| Field | Type | Default | Notes |
|---|---|---|---|
| `SyncServer` | string | `"time.nist.gov"` | Must be a member of the active TimeSource's pool; validation Planned |
| `TimeSource` | enum `TimeSource` | `Boulder` | v0.0.36+. `Boulder` (NIST) or `Brazil` (NTP.br). Mirrors `ServiceConfig.TimeSource`. Drives the on-face source label and Atomic Lab badge text. |
| `SyncInterval` | TimeSpan | `1:00:00` (1 hour) | Range `[15 min, 24 h]`. Note: `ServiceConfig.SyncInterval` defaults to **12 hours** — see §21 |
| `ConfirmLargeSyncCorrections` | bool | `false` | Enables toast confirmation flow (Planned) |
| `LargeOffsetThresholdSeconds` | int | `5` | Above this magnitude, "large" offset behavior triggers |
| `StartWithWindows` | bool | `false` | Auto-start UI on user sign-in (Planned UI) |
| `ServiceInstalled` | bool | `false` | True after first successful install via the helper |
| `UseSameThemeAcrossAllTabs` | bool | `false` | When ON, theme/color changes propagate to all tabs (Planned UI + propagation) |
| `DefaultTheme` | enum `Theme` | `AtomicLab` | Theme assigned to newly-created tabs/windows |
| `SendAnonymousCrashReports` | bool | `false` | Telemetry opt-in (Planned) |
| `SendAnonymousUsageStats` | bool | `false` | Telemetry opt-in (Planned) |

#### `TabSettings`

| Field | Type | Default | Notes |
|---|---|---|---|
| `Id` | string (GUID) | `Guid.NewGuid()` | Stable identifier; survives renames + reorders |
| `Label` | string? | `null` | If null, derived from `TimeZoneId` via `DeriveLabelFromIanaId` (city portion, `_`→space) |
| `TimeZoneId` | string (IANA) | `"UTC"` | New-tab default; first-run tab uses system local |
| `Theme` | enum `Theme` | `AtomicLab` | One of 12 themes (§10) |
| `ShowDigitalReadout` | bool | `true` | Hides digital readout on analog themes (Planned UI; renderer ignores today) |
| `TimeFormat` | enum `TimeFormatMode` | `Auto` | `Auto` / `TwelveHour` / `TwentyFourHour` (Planned UI; renderer hardcodes today — see §11) |
| `SecondHandMotionOverride` | enum `SecondHandMotion` | `ThemeDefault` | `ThemeDefault` / `Smooth` / `Stepped` (Planned UI) |
| `Colors` | `ColorOverrides` | empty | Five color slots — Planned (renderers ignore today) |

#### `WindowSettings : TabSettings` (free-floating clocks)

Inherits all TabSettings fields plus:

| Field | Type | Default |
|---|---|---|
| `X`, `Y` | double | `0`, `0` |
| `Width`, `Height` | double | `320`, `320` |
| `AlwaysOnTop` | bool | `false` (Planned binding to `Window.Topmost`) |
| `OverlayMode` | bool | `false` (Planned: borderless, transparent, click-through, desktop layer) |

#### `ColorOverrides` (the five slots — Planned)

```csharp
class ColorOverrides {
    string? Ring;     // outer bezel
    string? Face;     // inner clock face fill
    string? Hands;    // hour/minute hand fill
    string? Numbers;  // numerals + tick markers
    string? Digital;  // integrated digital readout
}
```

Each slot is `#RRGGBB` or `#RRGGBBAA`, or `null` to inherit theme default. The `IsEmpty` getter returns true when all five are null. POCO exists, persisted; renderers do not yet consume.

### Per-machine: `%ProgramData%\ComTekAtomicClock\service.json`

Top-level shape `ServiceConfig`:

| Field | Type | Default | Notes |
|---|---|---|---|
| `SchemaVersion` | int | `1` | |
| `TimeSource` | enum `TimeSource` | `Boulder` | v0.0.36+. Drives which pool `SyncWorker` walks. Mirror of `GlobalSettings.TimeSource`. |
| `SyncServer` | string | `"time.nist.gov"` | Anycast or any active-source pool member |
| `SyncInterval` | TimeSpan | `12:00:00` (12 h) | Clamped to `[15 min, 24 h]` by `SyncWorker` |
| `ConfirmLargeSyncCorrections` | bool | `false` | (Planned consumer) |
| `LargeOffsetThresholdSeconds` | int | `5` | |
| `EventLogSource` | string | `"ComTekAtomicClock"` | |

The Service falls back to these hardcoded defaults if `service.json` is missing. The UI **is responsible for writing this file** when the user changes sync settings — `SettingsStore.SaveServiceConfig(ServiceConfig)` is implemented; the Settings dialog wiring to call it on Save is **Planned**.

### Schema-evolution policy

- `SchemaVersion` is on `AppSettings` and `ServiceConfig` only — not on individual records.
- `[JsonExtensionData]` is on every record so unknown fields round-trip.
- A real version-detect-and-migrate code path is **Planned**; today, reading a future v2 file silently treats it as v1 (preserving unknown fields via the extension dictionary).

### Atomic-write + corrupt-file recovery

```csharp
// SettingsStore.WriteAtomic(path, json)
File.WriteAllText(path + ".tmp", json);
using (var fs = new FileStream(path + ".tmp", FileMode.Open)) fs.Flush(true);
File.Move(path + ".tmp", path, overwrite: true);

// Read path:
try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new(); }
catch (JsonException) {
    // Corrupt: rename and start over
    File.Move(path, path + $".broken-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
    return CreateDefaultAppSettings();
}
```

---

## §6 — UI Shell

### Windows

| Window | Class | Size | Min | Resize | Backdrop | Notes |
|---|---|---|---|---|---|---|
| Main window | `MainWindow` | 560 × 640 | 380 × 420 | Default | Mica / Round | Tabbed view, banner, status bar |
| Floating clock | `FloatingClockWindow` | 500 × 500 | 320 × 320 | Default | Mica / Round | `WindowStartupLocation=Manual`. Spawned by Dragablz tear-off via `AppInterTabClient` |
| Tab settings | `TabSettingsDialog` | 560 × 640 | — | NoResize | Mica / Round | Modal. Was 540 px tall pre-v0.0.37; bumped +100 to fit the v0.0.36 Time Source radio group cleanly. |
| About | `AboutDialog` | 520 × 430 | — | NoResize | Mica / Round | Modal. Includes "ALPHA" badge (`#7A3A1A` bg, `#FFB000` border, `#FFF5D8` text) |
| Help | `HelpDialog` | 560 × 540 | — | NoResize | Mica / Round | Modal |
| Themes gallery | `ThemesDialog` | 820 × 720 | — | Default (resizable) | Mica / Round | Modal. Renders 12 SVG previews via `SharpVectors.SvgViewbox` |

All windows are `Wpf.Ui.Controls.FluentWindow` with Mica + rounded corners. Default `App.xaml` merges WPF-UI `ThemesDictionary Theme="Dark"` and `ControlsDictionary`.

### Main-window layout

```
┌─────────────────────────────────────────────────────┐
│ ☴  Atomic Lab · ComTek Atomic Clock              ─□×│  ← FluentWindow chrome (Mica)
├─────────────────────────────────────────────────────┤
│ ⚠ Service not running. [Install and start the …]    │  ← Banner — visible only when ServiceState ≠ Running
├─────────────────────────────────────────────────────┤
│ ┌───────────┬─────────┬─────┐                       │
│ │ New York  │ London  │ + ▾ │                       │  ← Dragablz TabablzControl (Chrome-style)
│ ╞═══════════╧═════════╧═════╪═══════════════════════│
│ │                                                   │
│ │           ┌──────────────┐                        │
│ │           │              │                        │
│ │           │  Clock face  │                        │  ← Active tab's ClockFaceControl
│ │           │   (400×400)  │                        │       (one of 12 themes)
│ │           │              │                        │
│ │           └──────────────┘                        │
│ │                                                   │
├─────────────────────────────────────────────────────┤
│ Service: Running    Last sync: 12s ago (corrected −8.7 ms) │  ← Status bar
└─────────────────────────────────────────────────────┘
```

### Banner

Shown when `ServiceState ∈ { NotInstalled, InstalledNotRunning }`; hidden when `Running`. Bound from `MainWindowViewModel.BannerVisible`. Single button label depends on state:

| State | Label |
|---|---|
| `NotInstalled` | `"Install and start the time-sync service"` |
| `InstalledNotRunning` | `"Start the time-sync service"` |
| `Running` | (banner hidden) |

Click handler:
- `NotInstalled` → shells `ComTekAtomicClock.ServiceInstaller.exe install` with `Verb="runas"` (UAC).
- `InstalledNotRunning` → calls `sc.exe start ComTekAtomicClockSvc`.

### Status bar

Two text fields, fed by `MainWindowViewModel`:

- `ServiceStateText` — "Running" / "Stopped" / "Not installed".
- `LastSyncText` — uses formatter `FormatDrift`. Possible strings:
  - `"Last sync: pending…"`
  - `"Last sync: service not running"`
  - `"Last sync: just now"`
  - `"Last sync: 12s ago (corrected −8.7 ms)"` — the U+2212 minus sign means *correction direction* (clock was fast, corrected back); `+` means clock was slow, corrected forward.
  - `"Last sync failed: <reason>"`

### Cadence (`MainWindowViewModel`)

| Tick | Period | Purpose |
|---|---|---|
| Status text | 1 s | Re-render `LastSyncText` ("12s ago" → "13s ago") |
| Service state | 4 s | Poll `ServiceController` for SCM state changes |
| IPC LastSyncStatus | every 5th 1-s tick (≈5 s) | Round-trip `LastSyncStatusRequest` over the named pipe |
| Quick-poll after install/uninstall | 10 × 1 s | Bridge the gap until SCM settles after a state-changing operation |

### Tabs (native `System.Windows.Controls.TabControl`, v0.0.33+)

- **Native WPF TabControl.** `ItemsSource="{Binding Tabs}"`, `SelectedItem="{Binding SelectedTab}"`. Header text is `{Binding Label}` and refreshes on PropertyChanged automatically — no imperative refresh code is needed.
- **No tear-away.** Dragging a tab off the strip is **not a gesture**; the old Dragablz tear-away was removed in v0.0.33 as part of the library swap. Tabs↔windows transitions use **explicit commands** (below).
- **"+ New tab" toolbar button** (above the strip) → `AddTabCommand` adds a fresh tab in-place.
- **"+ New window" toolbar button** (above the strip) → `NewClockWindowCommand` spawns a new `FloatingClockWindow` containing a fresh clock. Does NOT add the tab to the main strip — a dual presence would be confusing.
- **Right-click on a tab header → ContextMenu** with two items:
  - **Tab settings…** → `OpenTabSettingsForCommand`
  - **Open in new window** → `OpenInNewWindowCommand` removes the tab from the strip and spawns a `FloatingClockWindow` for it. Reverse path is on the floating window's "?" menu (see §6 — Floating clock window).
- **Double-click on a tab header** → opens Tab Settings dialog (Excel-style).
- **Single-click reliability** is native — no `PreviewMouseLeftButtonDown` workaround required.
- **Last-tab-closes-app.** `MainWindowViewModel.CloseTab` calls `Application.Current?.Shutdown()` if the closed tab was the last one in the main window AND no floating windows are open. (Planned to change to "minimize to tray" once tray is built — §21.)
- **Tab strip styling** — see §8.

### Floating clock window (`FloatingClockWindow`, v0.0.33+)

A free-floating top-level window hosting **one** clock face. Spawned by:

- "+ New window" toolbar button on the main window → fresh clock.
- "Open in new window" right-click menu item on a tab → migrates that tab.

Its DataContext is the single `TabViewModel` for the hosted clock. The window has:

- A Wpf.Ui `TitleBar` whose `Title="{Binding Label}"` so the OS taskbar shows the city name. Closing via the title bar's `×` removes the clock (and purges its `TabSettings` from `settings.json` — see closing semantics below).
- The same Viewbox-scaled 400×400 clock canvas as a tab.
- A single `⋯` overlay button (Fluent `SymbolRegular.MoreHorizontal20`) in the top-right of the clock area. Clicking opens a ContextMenu with all 5 items:
  - **Settings…** — opens the Tab Settings dialog (Time zone / Theme / Sync frequency)
  - **Themes…** — opens the 12-tile theme gallery
  - **Bring back into tabs** — re-attaches this clock to the main window's tab strip and closes the floating window
  - **Help…** — opens the Help dialog
  - **About…** — opens the About dialog

> **v0.0.34 → v0.0.35 simplification.** Earlier the window had a stacked `✕` (close window) + `?` (menu) pair. The `✕` was redundant with the OS title-bar's close button; the v0.0.35 swap replaces both with a single `⋯` "more options" button. "Tab settings…" was also renamed to "Settings…" on this surface — the word "tab" is wrong on a free-floating window.

### Closing semantics (v0.0.33+)

Closing the window via the OS title-bar's `×` is a **"delete this clock"** action — it removes the underlying `TabSettings` from `settings.json` so the clock doesn't reappear on restart. To keep the clock and just put it away, use **Bring back into tabs** first.

**Bring back into tabs** dispatches to `MainWindowViewModel.BringWindowIntoTabsCommand`, which re-adds the `TabViewModel` to the `Tabs` collection (mirrored to `_settings.Tabs` via `OnTabsCollectionChanged`) and closes the floating window.

**Window-position persistence** (X/Y/Width/Height across app restart) is not yet wired — it's a Phase-2 todo alongside magnetic snap (§21).

---

## §7 — Tab Behavior

### Tab-name refresh — bind, don't walk (per v0.0.33)

Native WPF `TabControl` honors `PropertyChanged` on `ItemTemplate` bindings reliably, so tab headers refresh **automatically** when `TabViewModel.Label` is invalidated. The flow:

1. User opens Tab Settings dialog and changes the time zone.
2. On Save, the dialog has already mutated `TabViewModel.TimeZoneId` via two-way binding.
3. The `TimeZoneId` setter raises `PropertyChanged(nameof(TimeZoneId))`, `PropertyChanged(nameof(TimeZone))`, AND `PropertyChanged(nameof(Label))`.
4. The `ItemTemplate`'s `{Binding Label}` re-reads `TabViewModel.Label`, which derives `"New York"` from `"America/New_York"`.
5. Header re-renders. No imperative walker, no `Tag` attribute, no visual-tree enumeration.

```csharp
// TabViewModel.TimeZoneId setter (v0.0.33)
public string TimeZoneId
{
    get => _settings.TimeZoneId;
    set
    {
        if (_settings.TimeZoneId == value) return;
        _settings.TimeZoneId = value;
        _resolvedTimeZone = ResolveTimeZone(value);
        OnPropertyChanged();
        OnPropertyChanged(nameof(TimeZone));
        OnPropertyChanged(nameof(Label));   // Native TabControl honors this.
    }
}
```

> **Historical note.** v0.0.14..v0.0.31 fought eight iterations to make tab headers refresh under Dragablz. v0.0.32 abandoned PropertyChanged entirely in favor of an imperative `SetTabHeaderInAllDisplays` walk. v0.0.33 dropped Dragablz, restored the cascade, and deleted the imperative walker (~50 LOC). See §17 for the lineage. **Do not regress.**

### Single-click reliability — native (v0.0.33+)

Native `TabControl` handles single-click selection through standard WPF input routing. **No `PreviewMouseLeftButtonDown` workaround is needed** — that was a v0.0.20 patch around Dragablz's click/drag classifier swallowing short clicks. Removed in v0.0.33.

### Per-tab interactions (v0.0.34)

| Gesture | Effect | Handler |
|---|---|---|
| Single-click on tab header | Select tab | Native (no code) |
| Double-click on tab header | Open Tab Settings dialog | `MainWindow.TabItem_DoubleClick` |
| Right-click on tab header | **Directly open Tab Settings dialog** (no intermediate menu) | `MainWindow.TabItem_PreviewRightButtonDown` |
| `✕` overlay on clock face | Close this tab | `MainWindowViewModel.CloseTabCommand` |
| `?` overlay on clock face | Open menu (Themes… / Open in new window / Help… / About…) | `MainWindow.HelpButton_Click` |
| `+ New tab` toolbar button | Add a fresh tab to this window | `MainWindowViewModel.AddTabCommand` |
| `+ New window` toolbar button | Spawn a new floating clock window | `MainWindowViewModel.NewClockWindowCommand` |
| Ctrl+, | Open Tab Settings for SelectedTab | `OpenTabSettingsCommand` |

> **No tab ContextMenu (v0.0.34+).** The brief v0.0.33 two-item right-click menu (Tab settings… / Open in new window) was removed. Right-click now opens the Tab Settings dialog directly (matches the v0.0.23 spec). The "Open in new window" migration affordance moved to the "?" overlay menu on the clock face.

### Per-tab GUID identity

`TabSettings.Id = Guid.NewGuid().ToString()` at creation. The GUID is stable across rename, reorder, theme change, and tear-off. `TabViewModel.Id` exposes it for diagnostics. Do **not** key persistence by tab order or by `TimeZoneId` (both can collide).

### Label derivation from IANA ID

```csharp
static string DeriveLabelFromIanaId(string ianaId)
{
    if (string.IsNullOrWhiteSpace(ianaId)) return "UTC";
    var slash = ianaId.LastIndexOf('/');
    var leaf = slash >= 0 ? ianaId[(slash + 1)..] : ianaId;
    return leaf.Replace('_', ' ');
}
```

Examples: `America/New_York` → `New York`; `Europe/London` → `London`; `UTC` → `UTC`; `Pacific/Auckland` → `Auckland`. User-set `Label` overrides the derivation.

### New-tab defaults

A tab created via the "+" button defaults to `TimeZoneId = "UTC"`, `Theme = AtomicLab`, `TimeFormat = Auto`, `ShowDigitalReadout = true`. (First-run tab on a clean install uses the system's local IANA ID via `SettingsStore.ResolveLocalIanaId`.)

---

## §8 — Tab Strip Styling (v0.0.33+)

Native `System.Windows.Controls.TabControl` in `MainWindow.xaml`. (Floating windows host one clock without a strip — see §6.)

### Container (`TabControl`)

```xml
<TabControl x:Name="MainTabs"
            ItemsSource="{Binding Tabs}"
            SelectedItem="{Binding SelectedTab}"
            Background="White"
            BorderBrush="#C0C0C0"
            Padding="0">
```

### `ItemTemplate` (header text)

```xml
<TabControl.ItemTemplate>
    <DataTemplate>
        <TextBlock Text="{Binding Label}"
                   VerticalAlignment="Center"/>
    </DataTemplate>
</TabControl.ItemTemplate>
```

No `Tag` attribute. No imperative refresh hook. `{Binding Label}` reacts to `PropertyChanged` natively.

### `ItemContainerStyle` — full template replacement

The native `TabItem` ControlTemplate has a curved-corner Aero look that doesn't match the v0.0.32 visuals. v0.0.33 replaces the template with a flat rectangular border:

```xml
<TabControl.ItemContainerStyle>
    <Style TargetType="TabItem">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TabItem">
                    <Border x:Name="TabBorder"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter ContentSource="Header"
                                          VerticalAlignment="Center"
                                          HorizontalAlignment="Center"
                                          TextBlock.FontSize="{TemplateBinding FontSize}"
                                          TextBlock.FontWeight="{TemplateBinding FontWeight}"/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="TabBorder" Property="Panel.ZIndex" Value="1"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <!-- Defaults + state triggers below -->
    </Style>
</TabControl.ItemContainerStyle>
```

The `Panel.ZIndex=1` trigger lifts the active tab so its border overlaps neighbors (matches the v0.0.32 strip's selected-tab visual).

### `TabItem` defaults

| Property | Value |
|---|---|
| `Background` | `#E8E8E8` (inactive base) |
| `Foreground` | `Black` |
| `BorderBrush` | `#C0C0C0` |
| `BorderThickness` | `1,1,1,0` (no bottom border — bottom continues into content) |
| `Padding` | `10,4` |
| `FontSize` | `9` (inactive base) |
| `FocusVisualStyle` | `{x:Null}` |
| `MouseDoubleClick` event | open `TabSettingsDialog` (handler in code-behind) |
| `PreviewMouseRightButtonDown` event (v0.0.34+) | open `TabSettingsDialog` directly (no menu) |
| `ContextMenu` | **not set** (the v0.0.33 two-item menu was removed in v0.0.34) |

### Active vs. inactive state triggers

Reproduces the v0.0.28..v0.0.30 visuals exactly:

| State | Background | BorderBrush | FontWeight | FontSize |
|---|---|---|---|---|
| `IsSelected = True` | `#FFFFFF` | `#808080` | Bold | **19** |
| `IsSelected = False` | `#E8E8E8` | `#C0C0C0` | Normal | **9** |
| `IsMouseOver = True` (hover, inactive) | `#F5F5F5` | (default) | (default) | (default) |

### Tab toolbar (above the strip)

A thin `Border` (background `#F5F5F5`, bottom border `#C0C0C0`, padding `8,4`) docked above the TabControl, hosting two `ui:Button` controls (v0.0.34 — switched from plain `<Button>` for contrast against the WPF-UI Dark theme dictionary merged in `App.xaml`):

```xml
<ui:Button Content="+ New tab"
           Command="{Binding AddTabCommand}"
           Appearance="Secondary"
           Foreground="#0A0A0A"
           Padding="10,3" Margin="0,0,4,0"/>
<ui:Button Content="+ New window"
           Command="{Binding NewClockWindowCommand}"
           Appearance="Secondary"
           Foreground="#0A0A0A"
           Padding="10,3"/>
```

The explicit `Foreground="#0A0A0A"` guarantees dark-on-light contrast against the toolbar's `#F5F5F5` background regardless of theme inheritance. Replaces Dragablz's intrinsic `ShowDefaultAddButton` (which only created tabs in-place — there was no equivalent for "spawn a free-floating clock window").

### `OverlayGlyphBrush` (✕ and ? glyphs)

Each clock-face area shows a per-tab `✕` close-button and `?` help-button glyph in the upper-right. The brush re-evaluates whenever `Theme` changes, keying off the per-theme luminance class:

```csharp
public Brush OverlayGlyphBrush => IsDarkTheme(Theme) ? Brushes.White : _darkGlyph;
private static readonly Brush _darkGlyph = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));
```

Per-theme dark/light classification in §10.

### Removed: ContextMenu

A right-click context menu existed in earlier versions; it was deliberately removed in v0.0.26 (the menu items duplicated dialog actions and the popup behavior was inconsistent with Dragablz drag detection). Right-click is now wired to "open settings dialog" per §7.

### Last-tab bug-fix lineage

Tab-header refresh after Settings Save went through eight iterations before settling on the v0.0.32 design. Do not regress to a binding cascade: it is documented as unreliable for Dragablz `ItemTemplate` updates.

| Version | Approach | Result |
|---|---|---|
| v0.0.14 | First attempt | Dragablz `NotImplementedException` on `CollectionChanged.Replace` — silent crash |
| v0.0.16 | `RefreshTabHeader` walking subtree | Wrong subtree; Dragablz renders headers in a separate panel |
| v0.0.21 | Widened scope | Timing-sensitive; visual tree mid-update at sync call |
| v0.0.31 | Two-phase dispatch (sync + ApplicationIdle) | Still flaky |
| **v0.0.32** | **Imperative `SetTabHeaderInAllDisplays` walking every Application window** | **Reliable.** Current design. |

---

## §9 — Clock-Face Renderer Architecture

### `ClockFaceControl` (UserControl)

Single XAML control rendered into the active tab's content area. Hosts:

- A `Canvas` of fixed size **400 × 400** as the rendering surface (logical pixels; WPF scales for DPI).
- A `DispatcherTimer` (50 ms by default; per-theme cadence override possible) that re-issues the live updates: hand angles, digital readout text, date strip, etc.
- `Build<ThemeName>()` per-theme constructor methods that paint the static visual elements once on theme change.

### Theme dispatch

```csharp
void OnThemeChanged() {
    canvas.Children.Clear();
    switch (Theme) {
        case Theme.AtomicLab:    BuildAtomicLab();    break;
        case Theme.BoulderSlate: BuildBoulderSlate(); break;
        case Theme.AeroGlass:    BuildAeroGlass();    break;
        case Theme.Cathode:      BuildCathode();      break;
        case Theme.Concourse:    BuildConcourse();    break;
        case Theme.Daylight:     BuildDaylight();     break;
        case Theme.FlipClock:    BuildFlipClock();    break;
        case Theme.Marquee:      BuildMarquee();      break;
        case Theme.Slab:         BuildSlab();         break;
        case Theme.Binary:       BuildBinary();       break;
        case Theme.Hex:          BuildHex();          break;
        case Theme.BinaryDigital:BuildBinaryDigital();break;
    }
    AddVersionLabel();         // upper-left v0.0.32 watermark
    AddDebugThemeLabel();      // bottom-center "theme: <name>" (TODO: remove for public)
}
```

### Shared helpers

- `AddMinuteTicks(centerX, centerY, brush, thickness, opacity)` — 60 minute marks around the dial.
- `AddHourTicks(centerX, centerY, brush, thickness)` — 12 hour marks.
- `_recenterDateReadoutOnUpdate` (bool) — set per-theme; when true, the date `TextBlock` is re-laid-out each tick to keep horizontally centered as the string width changes.
- `To12HourParts(local)` — returns `(int hour12, int minute, int second, string ampm)` tuple for digital readouts.

### `AddVersionLabel` and `AddDebugThemeLabel`

- **Version watermark.** Paints `v0.0.32` (or current `<Version>`) at canvas position (6, 4) in Consolas 9pt, color ~`#90808080`. Comment: *"version should be in the clock background, upper left"*. Always present in alpha; reconsider for public release.
- **Debug theme label.** Paints `theme: <FriendlyName>` near (Cx, 388) in Consolas 9pt, semi-transparent gray. Marked TODO to remove before public release.

### Backdrop (the canvas-level fill)

Each theme's `Build*` method begins by painting a 400 × 400 rectangle at (0, 0) with the theme's backdrop fill (solid color, linear gradient, or radial gradient). The clock face is then layered on top.

---

## §10 — Themes (13 total — v1.1.0+)

Categories per the `Theme` enum and `ThemeCatalog.All` (in this order):

| # | Theme name (enum) | Category | Friendly name | Default theme? |
|---|---|---|---|---|
| 1 | `AtomicLab` | Analog | Atomic Lab | **YES** |
| 2 | `BoulderSlate` | Analog | Boulder Slate | |
| 3 | `AeroGlass` | Analog | Aero Glass | |
| 4 | `Cathode` | Analog | Cathode | |
| 5 | `Concourse` | Analog | Concourse | |
| 6 | `Daylight` | Analog | Daylight | |
| 7 | `CaptJohn` | Analog (v1.1.0+) | Captain John's | |
| 8 | `FlipClock` | Digital-only | Flip Clock | |
| 9 | `Marquee` | Digital-only | Marquee | |
| 10 | `Slab` | Digital-only | Slab | |
| 11 | `Binary` | Specialty (encoder) | Binary | |
| 12 | `Hex` | Specialty (encoder) | Hex | |
| 13 | `BinaryDigital` | Digital-only (encoder) | Binary Digital | |

**Date strip uniformity (per v0.0.22).** Every theme renders a date with day-of-week, date-of-month, month, AND year. Most analog/digital themes use the format string `"ddd · MMMM d · yyyy"` upper-cased (e.g. `MON · APRIL 26 · 2026`). The two encoder themes (Hex, Binary) render the same four parts in their respective encodings. Boulder Slate and Daylight center the date below the clock face (per v0.0.24).

**Universal source label (per v0.0.36).** Every theme also surfaces the active time source via `AddSourceLabel()`, called from `RenderActiveTheme()` after the per-theme `Build*`. Single word — `BOULDER` or `BRASIL` — painted at top-center of the canvas (Cx=200, y=10), Cascadia Code 11pt SemiBold, color `#FFCC00` at ~70% opacity. Uniform across all 12 themes; reads on every backdrop (warm-amber works on dark phosphor green, light cream, charcoal, theater red, etc.).

**Per-theme luminance class** (drives `OverlayGlyphBrush`):

| Theme | Class | Glyph color |
|---|---|---|
| AtomicLab, AeroGlass, Cathode, Concourse, Marquee, Slab, Binary, Hex, BinaryDigital | dark | white `#FFFFFF` |
| BoulderSlate, Daylight, CaptJohn, FlipClock | light | near-black `#101010` |

### Theme #1 — Atomic Lab (`BuildAtomicLab`)

The default. Black-and-amber lab-bench instrument aesthetic with a NIST badge.

| Element | Spec |
|---|---|
| **Backdrop** | 400×400 rectangle filled with `faceBrush` — radial gradient: `#1A2A4A` center (0.5, 0.35) → `#060D1A` outer at radius 0.7 |
| **Bezel ring** | 344×344 ellipse, linear gradient top-down `#E0E3E8` → `#7C8088` (mid) → `#2C3038` |
| **Inner face** | 320×320 ellipse, same `faceBrush` as backdrop |
| **Minute ticks** | `AddMinuteTicks(160, 155, amber, 1, 0.55)`. Amber = `#FFB000` |
| **Hour ticks** | `AddHourTicks(160, 145, amber, 3)` |
| **Numerals** | 4: `12 / 3 / 6 / 9` at radius 132 from center. Font `"Consolas, Courier New"`, size 22, color amber, FontWeight Bold |
| **Hour hand** (v1.1.0+) | line, overhang 14 / length **66** (was 90), white `#F5F5F5`, thickness 6 |
| **Minute hand** (v1.1.0+) | line, overhang 18 / length **152** (was 128), white, thickness 4 |
| **Second hand** | line, overhang 22 / length 142, red `#FF3030`, thickness 1.6 |
| **Center pin** | 14×14 amber ellipse + 5×5 inner faceBrush ellipse |
| **Digital panel** | Border 146×60, fill `#040B04`, BorderBrush amber, BorderThickness 0.6, CornerRadius 4. Position `(Cx-73, Cy+34)` |
| **Digital panel content (StackPanel)** | • Date: Consolas 9pt, amber, opacity 0.85, format `"ddd · MMMM d · yyyy"` upper • Time: Consolas 20pt, Bold, amber, format `"h:mm:ss tt"` (always 12-h, today) • Subtitle: Consolas 7pt, amber, opacity 0.7, **`TimeSourceBadge` DP value** (`"NIST · BOULDER · CO"` for Boulder source, `"NTP.BR · SÃO PAULO · BR"` for Brazil source — v0.0.36+; rebuilt on TimeSource change) |
| **Smooth-second default** | `true` |
| **Luminance class** | dark (white glyphs) |

### Theme #2 — Boulder Slate (`BuildBoulderSlate`)

Mondaine-inspired Swiss railway clock. Cream backdrop, white face, red lollipop second hand.

| Element | Spec |
|---|---|
| **Pre-canvas backdrop** | 400×400 `#F5F5F5` (page color) |
| **Outer black ring** | 348×348 ellipse `#0A0A0A` |
| **Inner white face** | 332×332 ellipse `#FFFFFF` |
| **Minute ticks** | `(160, 152)` black thickness 2 |
| **Hour ticks** | `(160, 138)` black thickness 6 |
| **Numerals** | none |
| **Hour hand** (v1.1.0+) | black baton, overhang 14 / length **76** (was 100) / width 10, no corner radius |
| **Minute hand** (v1.1.0+) | black baton, overhang 18 / length **162** (was 138) / width 7 |
| **Second hand** (special — SBB lollipop) | a 400×400 sub-canvas containing: red rod `#E3001B` thickness 2.5, from `(Cx, Cy+22)` to `(Cx, Cy-118)` + 28×28 disc at `(Cx, Cy-132)` lined up with the rod tip |
| **Center pin** | 10×10 black |
| **Date + time below center** | bare text. Font `"Segoe UI Variable, Segoe UI, sans-serif"`, FontWeight Medium, both black. Date FontSize 9, time FontSize 14. Format `"ddd · MMMM d · yyyy"` upper for date, `"h:mm:ss tt"` for time |
| **Readout centering** | `_recenterTextReadoutsOnUpdate = true` so **both** date and time re-center per tick (date per v0.0.24, time added v0.0.38). They share the same Cx so they're vertically stacked center-on-center. |
| **Smooth-second default** | `false` (Mondaine cadence — second hand pauses at 12 each minute on real Mondaines; current renderer steps without pause) |
| **Luminance class** | light (near-black `#101010` glyphs) |

### Theme #3 — Aero Glass (`BuildAeroGlass`)

Windows 7-era acrylic over a wallpaper-mock backdrop.

| Element | Spec |
|---|---|
| **Wallpaper backdrop** | 400×400 linear gradient `#3A4F7A` → `#5D7BA8` (mid) → `#243A5E` |
| **Decorative wallpaper detail** | 80×80 white ellipse at (40, 40) opacity 0.15; 40×40 white at (320, 100) opacity 0.15; 120×120 black at (0, 260) opacity 0.15 |
| **Acrylic disc** | 344×344 ellipse, fill linear gradient `#52FFFFFF` → `#24FFFFFF` (alpha-prefixed), stroke `#8CFFFFFF` thickness 1, DropShadowEffect blur 14, direction 270, depth 4, opacity 0.4 |
| **Hour ticks** | only (12), `AddHourTicks(156, 142, white, 3, Round)`. No minute ticks. |
| **Numerals** | 4 (`12 / 3 / 6 / 9`), font `"Segoe UI Variable, Segoe UI, sans-serif"` size 22, white, FontWeight SemiBold |
| **Hour hand** (v1.1.0+) | round-cap, length **68** (was 92), thickness 7, white |
| **Minute hand** (v1.1.0+) | round-cap, length **152** (was 128), thickness 5, white |
| **Second hand** | round-cap, length 140, thickness 2, cyan `#00B7FF` |
| **Center pin** | 12 white + 4 cyan |
| **Digital pill** | 130×50 Border, background `#59000000` (alpha-translucent), CornerRadius 14 |
| **Digital pill content** | Date Segoe size 9 Medium opacity 0.85; time Segoe size 17 SemiBold |
| **Smooth-second default** | `true` |
| **Luminance class** | dark (white glyphs) |

### Theme #4 — Cathode (`BuildCathode`)

CRT phosphor-green terminal nostalgia. Glow on every element.

| Element | Spec |
|---|---|
| **Background** | 400×400 radial gradient `#0A1808` (center) → `#000000` |
| **Phosphor palette** | green `#00FF66`; bright `#A8FF8A`; dim `#00B048` |
| **Outer ring** | 344×344 ellipse, stroke `#003D18` thickness 3, transparent fill |
| **Minute ticks** | `(160, 153)` `#00B048` thickness 0.8 opacity 0.5 |
| **Hour ticks** | `(160, 145)` phosphor thickness 2.5 round |
| **Numerals** | 4, font `"Lucida Console, Consolas, monospace"` size 20 Bold, phosphor green, with cloned `BlurEffect{Radius=4}` |
| **Hour hand** (v1.1.0+) | phosphor, length **66** (was 90) thickness 5 round, glow |
| **Minute hand** (v1.1.0+) | phosphor, length **152** (was 128) thickness 3.5 round, glow |
| **Second hand** | bright phosphor `#A8FF8A`, length 142 thickness 1.4 round, glow |
| **Center pin** | 10 phosphor with glow |
| **Digital panel** | 144×50, fill `#000A05`, BorderBrush phosphor 0.5, CornerRadius 2, opacity 0.92 |
| **Digital panel content** | Date Lucida size 9 phosphor opacity 0.85 + glow; time Lucida size 22 Bold phosphor + glow, format `"h:mm:ss tt"` |
| **Smooth-second default** | `true` |
| **Luminance class** | dark (white glyphs) |

### Theme #5 — Concourse (`BuildConcourse`)

Train-station departure-board aesthetic. Charcoal face, orange ink.

| Element | Spec |
|---|---|
| **Background** | 400×400 radial gradient `#202020` → `#0C0C0C` |
| **Outer ring** | 344×344 ellipse fill `#181818`, stroke `#3A3A3A` thickness 2 |
| **Inner face** | 320×320 charcoal `#0F0F0F` |
| **Hour ticks** | only `(160, 142)` orange `#FF8C00` thickness 4. No minute ticks. |
| **Numerals** | 1–12 at radius 122, font `"Bebas Neue, DIN Alternate, Impact, sans-serif"` size 28 Bold, orange `#FF8C00` |
| **Hour hand** (v1.1.0+) | baton, length **62** (was 86) width 10, orange, CornerRadius 1.5 |
| **Minute hand** (v1.1.0+) | baton, length **146** (was 122) width 8, orange, CornerRadius 1.5 |
| **Second hand** | white line, length 138 thickness 1.8 round |
| **Center pin** | 16 orange + 6 charcoal |
| **Digital panel** | 156×56, fill `#1A0D00`, BorderBrush orange 0.6, CornerRadius 3 |
| **Digital panel content** | Date Bebas size 11 SemiBold `#FFA430` opacity 0.85; time Bebas size 24 Bold `#FFA430` |
| **Smooth-second default** | `true` |
| **Luminance class** | dark (white glyphs) |

### Theme #6 — Daylight (`BuildDaylight`)

High-contrast cream-and-navy. Outdoor / readability theme.

| Element | Spec |
|---|---|
| **Background** | 400×400 radial gradient `#FFFDF5` → `#FFF3D6` (high-contrast cream) |
| **Outer ring** | 344×344 cream stroked `#9A9A9A` thickness 2 |
| **Inner face** | 320×320 white `#FFFFFF` |
| **Minute ticks** | `(160, 153)` navy `#003366` thickness 1.2 |
| **Hour ticks** | `(160, 145)` navy thickness 3.5 |
| **Numerals** | 1–12 at radius 124, font `"Inter, Segoe UI, sans-serif"` size 22 Bold, navy |
| **Hour hand** (v1.1.0+) | baton, length **66** (was 90) width 9, navy, CornerRadius 1.5 |
| **Minute hand** (v1.1.0+) | baton, length **152** (was 128) width 7, navy, CornerRadius 1.5 |
| **Second hand** | line, length 140 thickness 2 round, color `#E84A1A` orange-red |
| **Center pin** | 12 navy + 4 orange-red |
| **Date + time below center** | bare text. Date `Inter` size 11 SemiBold navy at `(Cx, Cy+60)`; time `Inter` size 18 Bold navy at `(Cx, Cy+76)` |
| **Readout centering** | `_recenterTextReadoutsOnUpdate = true` — both date and time re-center per tick on the same Cx (date per v0.0.24, time added v0.0.38 to fix Dan's "the day is not centered above the digital time" report) |
| **Smooth-second default** | `false` |
| **Luminance class** | light (near-black `#101010` glyphs) |

### Theme #7 — Captain John's (`BuildCaptJohn`) — v1.1.0+

Hand-drawn parchment-and-brass face featuring the marina's logo at 40% opacity. Lazy "Hora Chapín" jitter minute hand drifts ±3 minutes each tick, syncing back to 12 at the top of every hour. Around noon and 5 PM the face wakes up: real hands flash to 100% opacity and the bordeaux Cinzel-Bold "12" (and "5" at 5 PM) flash on top — 5 s on, 5 s off — over a 10-minute window centered on the hour mark.

| Element | Spec |
|---|---|
| **Backdrop** | 400×400 radial gradient parchment `#F5E9D0` (center, 0.5/0.5) → `#E8D7B2` (radius 0.7) |
| **Brass ring** | 344×344 ellipse, no fill, stroke `#B8924E` thickness 2 |
| **Inner face** | 320×320 ellipse, fill `#FCF4E0` (warm cream) |
| **Logo backdrop** | `pack://application:,,,/Assets/JohnsMarina-logo.jpg`, scaled so the 168×197 source's diagonal fits inside the inscribed circle of the 320 face with 4% safety margin (scale ≈ 1.286). Centered on `(Cx, Cy)`, opacity 0.40, clipped to face circle via `EllipseGeometry(radius=160)` |
| **Caption** | "The Busted Flush" — Monotype Corsiva (fallback Segoe Script / cursive) Italic 13 px, sepia `#3C281C` at alpha 0x66 (40%), centered horizontally, top y=283 |
| **Hour ticks** | none (face is numberless except during demo flash windows) |
| **Minute ticks** | none |
| **Numerals** | none normally. **"12"** (Cinzel-Variable Bold 38 px, bordeaux `#7B1616`) at top (Cx, Cy−130) flashes during the noon AND 5 PM windows. **"5"** at 5 o'clock (`Cx + 130·sin(150°)`, `Cy − 130·cos(150°)`) flashes only during the 5 PM window |
| **Real hour hand** | `MakeHand(overhang 14, length 66)`, dark bordeaux `#4A0F0F`, thickness 9, round-cap. In Hora Chapín ON: opacity 0.9 baseline (v1.1.2 — was 0.075 in v1.1.0/1.1.1; raised because the hands were too faint to read), flashes to 1.0 during flash windows. In Hora Chapín OFF: 1.0 always. |
| **Real minute hand** | `MakeHand(overhang 18, length 152)`, mid bordeaux `#641414`, thickness 5, round-cap. In Hora Chapín ON: opacity 0.9 baseline, flashes to 1.0. In Hora Chapín OFF: 1.0 always. |
| **Real second hand** | `MakeHand(overhang 22, length 138)`, mid bordeaux `#641414`, thickness 2, round-cap. Default opacity 0 (hidden); visible only during flash on-frames |
| **"Lazy" jitter minute hand** | Line, overhang 18 / length 152, ink black `#1A1A1A`, thickness 7, round-cap. Always rendered on top of the real minute hand at 100% opacity. Position is the displayed minute computed by the per-tick state machine (random walk, ±3 min per minute tick, hard reset to 0 at the top of each hour) |
| **Center pin** | 12×12 ink black ellipse + 4×4 pirate gold `#D4A547` ellipse |
| **Smooth-second default** | `true` (the second hand is hidden most of the time; smooth motion is fluid for the rare flash windows) |
| **Luminance class** | **light** (parchment / cream — overlay glyphs render near-black `#101010`) |
| **Per-tick state** | `_captJohnJitterMinute` (displayed minute), `_captJohnJitterLastTickRealMinute` (so the random walk advances exactly once per real minute), `_captJohnRng` (process-shared `Random`) |
| **Flash window** | `WrappedAbsDiff(minutesSinceMidnight, target)` ≤ 5 minutes from either 720 (noon) or 1020 (5 PM). Wraparound-safe so the window crosses midnight cleanly even though neither target does. |
| **Flash cadence** | `(local.Second / 5) % 2 == 0` → 5 s on / 5 s off |
| **Date display** | (intentionally absent — CaptJohn is a "lazy bar clock" not a daily-info dashboard. The other 11 themes show the day/date/month/year strip per v0.0.22; CaptJohn explicitly opts out for the visual quiet) |
| **Default Hora Chapín state** | **OFF** (v1.1.1+) — regular numberless clock face: real hands at 100% opacity, jitter hand hidden, no flash logic. Hora Chapín is opt-in novelty, not the default presentation. Persisted per-tab in `TabSettings.CaptJohnHoraChapin`. |
| **Jolly Roger overlay (v1.1.1+)** | ☠ button (44×44 with 36 px glyph) in the bottom-left of the 400×400 logical canvas. Visibility bound to `TabViewModel.IsCaptJohnTheme` via `BooleanToVisibilityConverter` so it only renders on this theme. Click opens a `ContextMenu` (placement above the button) with three checkable items: **Hora Chapín** (`StaysOpenOnClick`, two-way to `CaptJohnHoraChapin`), **Almuerzo (12:00 demo)**, **Fini (5:00 PM demo)**. Demos are momentary — `ContextMenu.Closed` resets `CaptJohnDemoMode` to empty so demo time-pinning never outlives the user's interaction. Hora Chapín toggle is persistent. Hosted on both `MainWindow` (per-tab) and `FloatingClockWindow`. |
| **Demo time-pinning** | When `CaptJohnDemoMode == "Almuerzo"`, `_digitalUpdater` overrides `local` to today at 12:00:00; `"Fini"` → 17:00:00. The flash-window logic then fires unconditionally because `WrappedAbsDiff` is 0. |

> **Note on the analog cluster.** With CaptJohn slotted in as Theme #7, the analog cluster grows from 6 → 7 themes, and the total theme count from 12 → 13. Themes #8–13 below were #7–12 in v1.0.x.

### Theme #8 — Flip Clock (`BuildFlipClock`)

1971-era split-flap nightstand clock with chrome legs and a brand line.

| Element | Spec |
|---|---|
| **Backdrop nightstand** | 400×400 linear gradient `#5A3D22` → `#2A1D10` |
| **Two chrome legs** | 22×14 each at (78, 316) and (300, 316), CornerRadius 2, gradient `#CCCCCC` → `#888888` (0.5) → `#444444` |
| **Case** | 328×244 rounded rectangle (CR 14) at (36, 78), fill linear `#2A2A2A` → `#0A0A0A` (0.5) → `#1A1A1A`, stroke `#3A3A3A` thickness 1.5 |
| **Inner display recess** | 300×186 fill `#1C1A16`, CR 6, at (50, 98) |
| **Four flip tiles** | left positions {64, 132, 208, 276}, each 60×138 split into top half 60×69 (gradient `#FFFFFF` → `#F4F1E8`) and bottom half 60×69 (gradient `#F0EDE2` → `#DCD8CA`), CR 5×3, stroke `#A8A39A`. Hinge dark line `#5A564C` thickness 0.7 + light line `#FFFFFFB3` thickness 0.5 just below seam |
| **Per-tile spindle pegs** | 6.4×6.4 chrome ellipse + 2×2 dark `#222222` inner ellipse on each side |
| **Digit text** | font `"Segoe UI Variable, Segoe UI, Arial, sans-serif"` size 78, FontWeight Black, fill `#0A0A0A`, centered on each tile. Format: `To12HourParts(local)` (always 12-h with AM/PM appended below) |
| **Amber colon dots** | two 7×7 amber `#FFB84A` at (196.5, 162.5) and (196.5, 212.5) |
| **Seconds label** | `": 00 SECONDS"` at (200, 274) Helvetica size 10 Medium `#AAAAAA`. Live updater: `$": {Second:D2} SECONDS · {ampm}"` |
| **Date line** | at (200, 292) size 11 Medium `#AAAAAA`, format `"ddd · MMMM d · yyyy"` upper |
| **Brand line** | `"COMTEKGLOBAL · MODEL CT-1971"` at (200, 312) size 9 normal `#888888` |
| **Smooth-second default** | `true` (no analog second hand to debate) |
| **Luminance class** | light (near-black `#101010` glyphs) |
| **Card-flip animation** | TODO marker at `:1366-1367` — actual flip animation not yet implemented |

### Theme #9 — Marquee (`BuildMarquee`)

Theater marquee with chase bulbs and big amber show-time digits. Always 12-h.

| Element | Spec |
|---|---|
| **Outer red theater frame** | 400×400 fill `#7A1818` |
| **Inner border** | 372×372 transparent rectangle stroke `#3A0A0A` thickness 2 at (14, 14) |
| **Inner stage panel** | 332×332 rounded (CR 4) at (34, 34), fill radial gradient `#1A0A0A` → `#080404`, stroke `#3A1010` thickness 2 |
| **32 chase bulbs** | top row 9 + bottom row 9 + left col 7 + right col 7, skip corners. Hardcoded positions list `bulbPositions`. Each 12×12 with radial-gradient fill `#FFF8D8` (0, 0.38, 0.32) → `#FFC940` (0.55) → `#A06010` and a `BlurEffect{Radius=4}` glow |
| **Header** | `"★ NOW SHOWING ★"` at (200, 120) font `"Bebas Neue, DIN Alternate, Impact, Arial Black, sans-serif"` size 20 Bold amber `#FFC940` |
| **Big time** | at (200, 232) Bebas size 64 Black amber + glow. Format `"h:mm:ss tt"` (always 12-h, no Auto/24h support) |
| **Subtitle** | `"★ ATOMIC TIME ★ FROM BOULDER ★"` at (200, 282) size 13 Medium amber opacity 0.85 |
| **Date line** | at (200, 308) size 12 Medium amber opacity 0.85, format `"ddd · MMMM d · yyyy"` upper |
| **Smooth-second default** | `true` (digital — moot) |
| **Luminance class** | dark (white glyphs) |
| **Chase-bulb wave animation** | TODO marker at `:1366-1367` — wave animation not yet implemented |

### Theme #10 — Slab (`BuildSlab`)

Brutalist concrete with a single huge time figure. Slab serif type.

| Element | Spec |
|---|---|
| **Backdrop** | 400×400 linear gradient concrete `#D4D0C5` → `#A8A298` |
| **Top accent bar** | 320×6 black `#1A1A1A` at (40, 60) |
| **Bottom accent bar** | 320×6 black at (40, 338) |
| **Red diagonal accent** | 60×3 `#CC2A1A` at (40, 76) |
| **Slab font chain** | `"Rockwell, Roboto Slab, Cambria, serif"` |
| **Context line** | `"ATOMIC · TIME · LOCAL"` at (40, 100) size 11 Bold black, left-anchored. Live updated to `"ATOMIC · TIME · DST"` or `"ATOMIC · TIME · STD"` based on `IsDaylightSavingTime()` |
| **Big time** | at (200, 240) size 100 Black `#0A0A0A`, format `"h:mm"` (no seconds, no AM/PM at this size) |
| **Seconds + AM/PM** | at (200, 290) size 36 Bold `#CC2A1A` red, format `"{ss}″ {tt}"` (the prime mark is U+2033) |
| **Date** | at (200, 328) size 10 Medium gray `#3A3630`, format `"dddd · d MMMM yyyy"` upper (note: full weekday name on Slab, distinct from other themes) |
| **Smooth-second default** | `true` |
| **Luminance class** | dark (white glyphs — though backdrop is light, the dark accents push the perceived class) |

### Theme #11 — Binary (`BuildBinary`)

Six-column BCD board with red LEDs. Always 24-h (matches the bit-width rationale).

| Element | Spec |
|---|---|
| **Background** | 400×400 `#080808` |
| **Faint horizontal grid lines** | two `#1A0808` thickness 0.5 at y=120 and y=265 |
| **Mono font** | `"Cascadia Code, Consolas, Lucida Console, monospace"` |
| **Title** | `"BINARY CLOCK"` at (200, 60) size 16 Bold `#FF5555` |
| **Date line** | at (200, 90) size 11 normal `#FF5555` opacity 0.7, format `"ddd · MMMM d · yyyy"` upper |
| **Bit-value labels** | right-aligned to x=42, `"8" / "4" / "2" / "1"` at y={155, 195, 235, 275} size 11 `#553030` |
| **6 columns of LEDs** | x positions {78, 128, 198, 248, 318, 368} with bit-positioning per column. Columns 1, 3, 5 (zero-indexed: H-tens, M-tens, S-tens) have 3 dots; columns 2, 4, 6 (H-ones, M-ones, S-ones) have 4 dots; column 0 has 2 dots. Each dot 22×22 ellipse |
| **Lit dot fill** | radial gradient `#FFC4C4` → `#FF3030` → `#7A0808` with `BlurEffect{Radius=6}` |
| **Unlit dot fill** | `#3A0A0A` → `#180404` |
| **Group labels** | HOURS / MINUTES / SECONDS at y=304 size 10 normal `#AA3030` |
| **Decoded readout** | at (200, 350) size 22 Bold `#FF3030` + glow, format `"HH : mm : ss"` (always 24-h) |
| **Footer** | `"read top→bottom · 8·4·2·1 BCD per column"` at (200, 375) size 9 `#553030` |
| **Smooth-second default** | `true` (no analog hand) |
| **Time format default** | **24-h always** (per v0.0.27 correction — encoders never honor 12-h) |
| **Luminance class** | dark (white glyphs) |

### Theme #12 — Hex (`BuildHex`)

Mock Linux terminal showing time as hex bytes, with a synesthetic day-as-color swatch. Always 24-h.

| Element | Spec |
|---|---|
| **Background** | 400×400 radial gradient `#0C1828` (0.5, 0.4) → `#020812` |
| **Title bar** | 400×32 `#000814` with three traffic-light dots: 8×8 each at (10, 12), (26, 12), (42, 12), opacity 0.7, colors `#FF5050` / `#FFAA30` / `#50CC60` |
| **Window title** | `"comtekglobal :: hex_clock.exe"` at (200, 21) Cascadia size 11 cyan `#5FE2FF` opacity 0.6 |
| **Comment line** | `"// time encoded as hexadecimal (per unit)"` at (40, 80) size 12 cyan opacity 0.55 |
| **Big hex digits** | at (200, 190) size 56 Bold cyan + glow, format `$"{Hour:X2}:{Minute:X2}:{Second:X2}"` (always 24-h) |
| **HOURS/MINUTES/SECONDS labels** | at x={80, 200, 320}, y=216, size 10, dim cyan `#3A8AAA` |
| **Hex-ASCII date breakdown** | four lines at (40, 244) / (40, 260) / (40, 276) / (40, 290) for day-of-week / date-of-month / month / year, all size 12 cyan opacity 0.75 |
| **Day-fraction line** | at (40, 308) size 12 cyan opacity 0.7: `// day-frac: 0xNNNN / 0xFFFF (PCT% elapsed)` |
| **Color swatch** | 320×14 rectangle CR 2 opacity 0.85 at (40, 322), fill = `Color.FromRgb(dayU16>>8, dayU16 & 0xFF, 0xFF)` — i.e., today's elapsed-fraction encoded as #RRGGBB with R = high byte, G = low byte, B = 0xFF |
| **Swatch description** | `"// the bar above is #RRGGBB — today, encoded as a color"` at (40, 350) size 11 cyan opacity 0.55 |
| **Prompt** | `"$ _"` at (40, 378) size 14 bright cyan `#A0EEFF` |
| **Smooth-second default** | `true` |
| **Time format default** | **24-h always** (per v0.0.27 correction) |
| **Luminance class** | dark (white glyphs) |

### Theme #13 — Binary Digital (`BuildBinaryDigital`)

Pure-text binary clock in magenta, in a mock terminal window. Always 24-h.

| Element | Spec |
|---|---|
| **Background** | 400×400 radial gradient `#1A0830` (0.5, 0.4) → `#080414` |
| **Title bar** | 400×32 `#08000A` + same 3 traffic-light dots as Hex |
| **Window title** | `"comtekglobal :: bin_clock.exe"` at (200, 21) Cascadia size 11 magenta `#FF5CD0` opacity 0.7 |
| **Comment line** | `"// time encoded as binary text (per unit)"` at (40, 80) size 12 magenta opacity 0.55 |
| **Three labeled rows** | each with a dim-magenta prefix at x=80 (`H` / `M` / `S`, size 30 Bold) and a primary-magenta bits text at x=120 size 30 Bold magenta + glow. Padded to 5 / 6 / 6 bits respectively. Y at 138 / 180 / 222 |
| **Binary-ASCII date breakdown** | four rows at y=254 / 270 / 286 / 302 (dow / dom / mon / yr), size 11 magenta opacity 0.75 |
| **Annotation** | `"// widths: 5b hour · 6b min · 6b sec · MSB first"` at (40, 320) size 10 dim magenta |
| **Two decorative noise rows** | (static, doesn't update) at y=340 / 354 size 9 magenta opacity 0.18 |
| **Prompt** | `"$ _"` at (40, 380) size 14 bright `#FFAAE8` |
| **Smooth-second default** | `true` |
| **Time format default** | **24-h always** (per v0.0.27 correction) |
| **Luminance class** | dark (white glyphs) |

---

## §11 — Time-format Defaults

`TabSettings.TimeFormat` is a `TimeFormatMode` enum: `Auto`, `TwelveHour`, `TwentyFourHour`. Default per tab is `Auto`. Today, the renderer ignores the field and uses hardcoded format strings per theme — full UI + renderer wiring is **Planned** (§20).

**Per v0.0.27 (Dan's correction):** the three encoder themes — **Binary, Hex, BinaryDigital** — always render 24-hour time and never honor a 12-hour override. The remaining nine themes default to 12-hour rendering when `Auto` is selected.

| Theme | Default rendering today | Honor `TimeFormat` override (Planned) |
|---|---|---|
| AtomicLab | 12-h `"h:mm:ss tt"` | yes |
| BoulderSlate | 12-h `"h:mm:ss tt"` | yes |
| AeroGlass | 12-h `"h:mm:ss tt"` | yes |
| Cathode | 12-h `"h:mm:ss tt"` | yes |
| Concourse | 12-h `"h:mm:ss tt"` | yes |
| Daylight | 12-h `"h:mm:ss tt"` | yes |
| FlipClock | 12-h via `To12HourParts` | yes |
| Marquee | 12-h `"h:mm:ss tt"` | yes |
| Slab | 12-h `"h:mm"` + AM/PM under | yes |
| Binary | 24-h `"HH : mm : ss"` | **NO — always 24-h** |
| Hex | 24-h hex bytes | **NO — always 24-h** |
| BinaryDigital | 24-h binary bits | **NO — always 24-h** |

### 24-hour numeral-flip on analog faces (Planned)

Legacy spec describes positions 1–11 swapping to 13–23 from noon→midnight, with position 12 fixed (sparse themes flip 3↔15, 6↔18, 9↔21). The current renderer never inspects time format and writes hardcoded numerals at theme-build time only. This behavior is **Planned**.

---

## §12 — Second-hand Motion

`TabSettings.SecondHandMotionOverride` is a `SecondHandMotion` enum: `ThemeDefault`, `Smooth`, `Stepped`. Default per tab is `ThemeDefault`.

The resolved boolean `TabViewModel.SmoothSecondHand` consumed by the renderer:

```csharp
public bool SmoothSecondHand => SecondHandMotionOverride switch
{
    SecondHandMotion.Smooth   => true,
    SecondHandMotion.Stepped  => false,
    _                         => ThemeDefaultIsSmooth(Theme),
};
```

### Per-theme default

| Theme | `ThemeDefaultIsSmooth` |
|---|---|
| AtomicLab | `true` |
| BoulderSlate | `false` (Mondaine cadence) |
| AeroGlass | `true` |
| Cathode | `true` |
| Concourse | `true` |
| Daylight | `false` |
| FlipClock | `true` (no analog hand — moot) |
| Marquee | `true` (no analog hand — moot) |
| Slab | `true` (no analog hand — moot) |
| Binary | `true` (no analog hand — moot) |
| Hex | `true` (no analog hand — moot) |
| BinaryDigital | `true` (no analog hand — moot) |

UI control to toggle the override is **Planned**. Override is reachable today only by hand-editing `settings.json`.

---

## §13 — Tab Settings Dialog (`TabSettingsDialog`)

Modal dialog opened by:

- Double-click on a tab header.
- Right-click on a tab header (per v0.0.23 — replaces the removed context menu).
- (Future) ⚙ gear button in the upper-right of the clock-face area.

### Window properties

| Property | Value |
|---|---|
| Size | 560 × 640 |
| `ResizeMode` | `NoResize` |
| `WindowStartupLocation` | `CenterOwner` |
| Backdrop | Mica / Round (FluentWindow) |

### Fields exposed today (Implemented)

| Section | Field | Control | Bound to |
|---|---|---|---|
| THIS TAB | Time zone | ComboBox of `TimeZoneCatalog.All` (~140 entries) | `TabViewModel.TimeZoneId` |
| THIS TAB | Theme | ComboBox of `ThemeCatalog.All` (12 themes) | `TabViewModel.Theme` |
| ALL CLOCKS ON THIS PC | Sync frequency | ComboBox: 6 / 12 / 24 hours | `ServiceConfig.SyncInterval` (persisted to `service.json` on Save — v0.0.36+) |
| ALL CLOCKS ON THIS PC | Time source (v0.0.36+) | RadioButton group: **Boulder** (NIST) / **Brazil** (NTP.br) | `ServiceConfig.TimeSource` and (mirrored) `GlobalSettings.TimeSource` |

### Fields Planned (model exists, no UI)

- Per-tab Label (rename) — model has `TabSettings.Label`, no input control
- Time format selector (Auto / 12h / 24h)
- Show digital readout toggle (visible only on analog themes)
- Second-hand motion override (Theme default / Smooth / Stepped)
- Five-slot color overrides (Ring / Face / Hands / Numbers / Digital) with HSV wheel + RGB sliders + hex input + eyedropper + "Reset colors to theme defaults" link
- Sync server selector (NIST pool members + anycast)
- "Confirm large sync corrections" toggle
- "Use the same theme across all tabs" toggle
- "Start with Windows" toggle
- "Send anonymous crash reports" / "Send anonymous usage stats" toggles

### OK / Cancel semantics

- **OK / Save.** Dialog returns `true`. Caller (`MainWindowViewModel.OpenTabSettingsCore`) calls `PersistAfterDialog()` to write `settings.json`, then calls `MainWindow.SetTabHeaderInAllDisplays(tab)` to refresh the tab name imperatively across all open windows (per §7).
- **Cancel.** Dialog returns `false` or `null`. No persistence; in-memory `TabViewModel` is reverted to its pre-dialog state. (The dialog uses a clone of the view-model and only commits on Save.)

### Dialog Owner — origin-window-aware (v0.0.39+)

The dialog's `Owner` determines its centering anchor (`WindowStartupLocation="CenterOwner"`). Two entry points:

| Caller | Owner |
|---|---|
| In-tab right-click → "Tab settings…" → `RelayCommand OpenTabSettingsForCommand` | `Application.Current?.MainWindow` (the main window — correct, that's where the tab strip lives) |
| `FloatingClockWindow` `⋯` menu → "Settings…" → `MainWindowViewModel.OpenTabSettingsForOwner(tab, owner: this)` | The floating window itself — dialog centers over the clock the user invoked it from |

Same pattern for the Themes gallery dialog:

| Caller | Owner |
|---|---|
| In-tab `?` overlay → "Themes…" → `RelayCommand OpenThemesPickerForCommand` | MainWindow |
| `FloatingClockWindow` `⋯` menu → "Themes…" → `MainWindowViewModel.OpenThemesPickerForOwner(tab, owner: this)` | The floating window |

The pre-v0.0.39 behavior hardcoded both to MainWindow, which was wrong when the user opened either dialog from a floating window — the dialog would appear on the main window, possibly on a different monitor.

### Visual styling

- Amber + dark-cream palette: `#FFB000` for headings + Save button background, `#0A0A0A` for Save-button text, `#A8A39A` for sub-labels, `#A0E0FF` for clickable links.
- Dialog content uses the WPF-UI `ContentDialog` look but in a `FluentWindow` shell (modal).

---

## §14 — Help Dialog (`HelpDialog`)

Modal 560 × 540 dialog. Opened from the main window's `?` glyph or the About → Help link.

Content (post v0.0.25 + v0.0.26 updates):

- **Tabs** — explains the "+" button, tear-away, double-click for settings. Right-click = settings dialog. (Per v0.0.26 the older "right-click → Close tab" line was removed.)
- **Time zones** — IANA zone selector, label auto-derivation, per-tab independent zone.
- **Sync** — service runs only while UI is open; auto-starts on launch and stops on exit; sync queries NIST stratum-1 pool; default cadence; how to read the status bar.
- **Status bar** — interpretation of `"Last sync: …"` strings including the U+2212 sign convention.
- **Service** — banner button labels, what UAC consent does, where settings.json / service.json live.

### Help corpus example strings (used as test assertions in `MainWindow.xaml`)

```
"Last sync: 12s ago (corrected −8.7 ms)"
"It only runs while the clock app is open — it auto-starts when you launch the app and stops when you close it."
"New tabs default to UTC and Atomic Lab."
```

The `−` is U+2212 (true minus), not U+002D (hyphen-minus). The `—` is U+2014 em-dash.

---

## §15 — About Dialog (`AboutDialog`)

Modal 520 × 430. Includes:

- App name + version (`v0.0.32`).
- "ALPHA" badge: background `#7A3A1A`, border `#FFB000`, text `#FFF5D8`.
- Copyright: `© 2026 Daniel V. Oxender.`
- MIT license text (verbatim from `LICENSE`).
- Link to project home page.
- Build info: target framework `.NET 8.0 (Windows)`, OS GUID `{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}` (Win10 1809+).

---

## §16 — Themes Gallery (`ThemesDialog`)

Modal 820 × 720, **resizable**. Opened from the Tab Settings dialog's "Browse themes" link or from the main window menu.

Shows 12 theme preview cards, each rendering one of the canonical SVGs from `windows/design/themes/<name>.svg` via `SharpVectors.SvgViewbox` resolved through pack URI `pack://application:,,,/Assets/themes/<name>.svg`. Selecting a card sets the active tab's `Theme` and closes the dialog.

The SVGs are linked (not copied) into the UI assembly so the design folder remains the single source of truth — touching an SVG there updates the gallery without rebuilding any artwork.

---

## §17 — Exception Handling

Three unhandled-exception handlers are subscribed in the **`App` constructor** (NOT in `OnStartup` — must run before `MainWindow` is constructed). All three pop a `MessageBox.Show` with the trimmed top-6-frame stack trace.

```csharp
// App.xaml.cs — constructor
public App()
{
    DispatcherUnhandledException += OnDispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnhandledException;
}
```

| Handler | Source thread | Set `e.Handled = true`? |
|---|---|---|
| `Application.DispatcherUnhandledException` | UI dispatcher | `true` (keep app alive) |
| `AppDomain.CurrentDomain.UnhandledException` | Any non-UI thread | n/a (process is terminating; show message anyway) |
| `TaskScheduler.UnobservedTaskException` | TPL finalizer | `e.SetObserved()` |

### Why in the constructor

Subscribing in `OnStartup` is too late — `base.OnStartup(e)` constructs `MainWindow`, and any exception in the MainWindow constructor would escape the unhandled-exception net. v0.0.15 hit this exact bug; v0.0.16 fixed it by moving subscription to the constructor.

### Service-side exception handling

`ComTekAtomicClock.Service.Program.cs` uses the standard `Microsoft.Extensions.Hosting` `Host.CreateDefaultBuilder` exception flow plus `ILogger<SyncWorker>` for structured logging. Sync failures are logged at Warning; total-pool-failure is Warning; large corrections are Warning.

### Event Log

Service writes to the Windows Event Log via `EventLog` source `ComTekAtomicClock` (configurable via `ServiceConfig.EventLogSource`). The source is registered automatically the first time the service runs as LocalSystem.

---

## §18 — Known Constraints, Quirks, and Gotchas

This section captures non-obvious facts that have bitten previous iterations. **Read before refactoring.**

### Dragablz lineage (resolved in v0.0.33 — retained as cautionary history)

Dragablz `0.0.3.234` was the source of nearly every tab-related bug fought in v0.0.14..v0.0.32. **The library was removed in v0.0.33** in favor of the BCL `System.Windows.Controls.TabControl`. The four quirks below are no longer live but are preserved here as a "do not re-introduce" record. If a future session is tempted to bring Dragablz (or any tear-away tab library) back, **read these first**.

1. **`CollectionChanged.Replace` was unimplemented.** `Dragablz.TabablzControl.OnItemsChanged` threw `NotImplementedException` for `Replace` actions. Mutating an `ObservableCollection<TabViewModel>` in a way that produced a `Replace` event silently crashed the UI (caught by the dispatcher handler in §17 / `App` constructor handlers). Native `TabControl` does not have this limitation.
2. **Click/drag classifier swallowed single clicks.** Short clicks on a tab header were sometimes misclassified as drag-starts, dropping `IsSelected`. v0.0.20 added a `PreviewMouseLeftButtonDown` handler to force-select before Dragablz saw the event. Native `TabControl` selects on click reliably; the rescue handler is gone.
3. **`ItemTemplate` PropertyChanged unreliability.** Headers did not reliably refresh when a bound property changed on the underlying view-model. v0.0.32 worked around it with `MainWindow.SetTabHeaderInAllDisplays(tab)` walking every Application window. Native `TabControl` honors `PropertyChanged` automatically; the walker is gone.
4. **Headers rendered in a separate visual subtree.** `VisualTreeHelper.GetChild` from the `TabablzControl` did not reach header `TextBlock`s through the item-container path; the imperative walk had to enumerate the entire window's visual descendants and key on `Tag == "TabHeaderText"`. Native `TabControl` doesn't split the tree this way.

### Settings-write gotchas

5. **Tab order is not preserved by reference.** Reordering tabs in the strip writes a new `Tabs` list to `settings.json`; reading back relies on `Tab.Id` (GUID) for identity, not order.
6. **Service.json must be written by the UI.** The Service only reads `service.json`; if the UI never writes it, the Service uses the hardcoded default sync interval (12 h). The Settings-dialog wire-up to `SettingsStore.SaveServiceConfig` is **Planned** — see §21 / S-16.
7. **Forward-compat is preserved by `[JsonExtensionData]`, not by version detection.** A v2 settings file read by a v1 binary keeps unknown fields in the `UnknownFields` dictionary and round-trips them on next write. This avoids data loss without requiring migration code.

### Privilege quirks

8. **`SetSystemTime` requires `SE_SYSTEMTIME_NAME`.** LocalSystem has it by default. In dev (running the service as the developer's account), the call returns Win32 error 1314 (`ERROR_PRIVILEGE_NOT_HELD`). This is **expected in dev** — verify by checking the Event Log; do not "fix" by elevating the dev account.
9. **UI must NOT elevate itself.** `requireAdministrator` in the UI manifest would force a UAC prompt on every launch. The UI is `asInvoker`; only `ServiceInstaller.exe` carries `requireAdministrator`. The UI launches the installer with `Verb="runas"` to trigger UAC for that one operation.

### Tooling quirks

10. **PowerShell mangles UTF-8 em-dashes when writing files.** `Out-File` and `Set-Content` with default encoding (UTF-16 LE on PS 5.1) corrupt em-dashes (`—`, U+2014) and minus signs (`−`, U+2212) when other tools read the file as UTF-8. **Use `Write` / `Edit` tool calls for writing docs**, never PowerShell `Out-File`. If PowerShell is unavoidable, use `[System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))`.
11. **Don't restart the UI from inside Visual Studio when running under admin.** F5 from an admin VS instance silently fails to launch the standard-user UI; the service starts but no clock window appears. Run the UI manually from Explorer or from a non-elevated shell.

### Visual-tree quirks

12. **`fe.InvalidateMeasure()` is required after imperative `Text` changes.** Setting `tb.Text = newText` does not always re-lay-out the parent; without `InvalidateMeasure`, the new text may overflow or truncate. `SetTabHeaderInAllDisplays` calls it on the parent `FrameworkElement`.

---

## §19 — Versioning

### Patch-bump-on-every-change rule

Every code change Dan will see (a commit / push / build he tests) must:

1. Bump the version's **patch** component in `ComTekAtomicClock.UI.csproj`. Three properties must be kept in sync:
   ```xml
   <Version>0.0.32</Version>
   <AssemblyVersion>0.0.32.0</AssemblyVersion>
   <FileVersion>0.0.32.0</FileVersion>
   ```
2. Add a problem-and-solution entry to `windows/CHANGELOG.md` under the new version header.
3. Both must be in the **same commit** as the change.

This is a standing rule across all of Dan's projects (see `C:\ComputerSource\Claude\Context\memory\feedback_version_bump_on_change.md`). Trivial fixes (typos, whitespace, dep-only bumps) are exempt.

### CHANGELOG entry shape

```markdown
## v0.0.33 — <one-line title>

**Problem:** <what was wrong, in plain English>

**Solution:** <what changed and why this is the right fix>

**Files touched:** `path/to/file1.cs`, `path/to/file2.xaml`, `windows/CHANGELOG.md`, `windows/SPEC.md`
```

### Doc-update rule

Any code change must update every project doc that describes the area touched (`README.md`, `CHANGELOG.md`, `SPEC.md`, `CONTEXT.md`, in-code module headers, etc.) **in the same branch**. Name the docs touched in the commit message and in the response so Dan can spot-check.

### Pre-merge audit

Before pushing or merging to `master`: run a doc audit to confirm everything material is in `SPEC.md` and `CONTEXT.md`. The standing rule is "Update all documentation always for all projects before a merge and push."

### No merge / push without consent

Stop after committing. Wait for an explicit "merge" / "push" / "ship" instruction before running:

- `git merge` to a tracked branch
- `git push`
- `gh pr merge`
- force-push
- pushing tags

Local-only ops (commit, branch, stash, local tag) remain free. Acknowledge stop state in the response: `branch @ <hash> (vX.Y.Z) — local only, N commits ahead`.

---

## §20 — Build / Install / Run

### Build (developer machine)

```powershell
# From repo root
cd C:\ComputerSource\ComTekAtomicClock\windows

# Restore + build everything
dotnet build ComTekAtomicClock.slnx -c Debug

# Or for release
dotnet build ComTekAtomicClock.slnx -c Release
```

The four projects build in this dependency order (resolved automatically by the solution):

1. `ComTekAtomicClock.Shared` — settings models + IPC contracts (DLL)
2. `ComTekAtomicClock.Service` — references Shared
3. `ComTekAtomicClock.UI` — references Shared
4. `ComTekAtomicClock.ServiceInstaller` — references Shared

### Publish (single-file deployment)

```powershell
# UI
dotnet publish src\ComTekAtomicClock.UI\ComTekAtomicClock.UI.csproj `
    -c Release -r win-x64 --self-contained false -o publish\UI

# Service
dotnet publish src\ComTekAtomicClock.Service\ComTekAtomicClock.Service.csproj `
    -c Release -r win-x64 --self-contained false -o publish\Service

# Installer (used once, then can be deleted from end-user machines)
dotnet publish src\ComTekAtomicClock.ServiceInstaller\ComTekAtomicClock.ServiceInstaller.csproj `
    -c Release -r win-x64 --self-contained false -o publish\Installer
```

### Install the service (end user)

The UI does this automatically via the banner button, but the manual command is:

```cmd
:: Run from an elevated cmd; helper sets ACLs and writes %ProgramData%\ComTekAtomicClock\
ComTekAtomicClock.ServiceInstaller.exe install --service-exe "C:\Program Files\ComTekAtomicClock\ComTekAtomicClock.Service.exe"
```

What it does:

- `sc.exe create ComTekAtomicClockSvc binPath= "<path>" start= demand DisplayName= "ComTek Atomic Clock — time sync"`
- `sc.exe sdset ComTekAtomicClockSvc <SDDL granting AU + BA + SY as documented in §4>`
- Creates `%ProgramData%\ComTekAtomicClock\` with NTFS ACLs granting Authenticated Users `Modify + ReadAndExecute + Synchronize` (ContainerInherit + ObjectInherit).
- Optionally accepts `--service-exe <path>` to override the sibling-exe lookup.
- Exit codes: 0 = success; non-zero = failure with stderr explanation.

### Uninstall

```cmd
ComTekAtomicClock.ServiceInstaller.exe uninstall [--purge-user-data]
```

- Stops the service (waits up to 30 s).
- `sc.exe delete ComTekAtomicClockSvc`.
- Removes `%ProgramData%\ComTekAtomicClock\`.
- Optionally removes `%APPDATA%\ComTekAtomicClock\` with `--purge-user-data`.

### Running locally without installing the service

The UI starts up gracefully when the service is not installed — the banner appears with "Install and start the time-sync service" and clock faces still render. The status bar shows `Service: Not installed` and `Last sync: service not running`.

### Producing a release installer (Setup.exe)

The repo ships an Inno Setup script at `windows/tools/installer.iss` that bundles the self-contained publish output into a single `Setup.exe`. Build steps:

```powershell
# 1. Self-contained publish of all three projects (315 MB output)
cd C:\ComputerSource\ComTekAtomicClock\windows
dotnet publish src\ComTekAtomicClock.UI\ComTekAtomicClock.UI.csproj `
    -c Release -r win-x64 --self-contained true -o release\v0.0.36\UI
dotnet publish src\ComTekAtomicClock.Service\ComTekAtomicClock.Service.csproj `
    -c Release -r win-x64 --self-contained true -o release\v0.0.36\Service
dotnet publish src\ComTekAtomicClock.ServiceInstaller\ComTekAtomicClock.ServiceInstaller.csproj `
    -c Release -r win-x64 --self-contained true -o release\v0.0.36\Installer

# 2. Copy supporting docs into the staging dir
Copy-Item LICENSE,CHANGELOG.md,README.md,SPEC.md,release\v0.0.36\INSTALL.md release\v0.0.36\

# 3. Compile the installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" tools\installer.iss

# Output: release\ComTekAtomicClock-v0.0.36-Setup.exe (~97 MB, LZMA2 max)
```

Inno Setup is a one-time install: `winget install JRSoftware.InnoSetup`. The script does the user-facing install: license screen, install-dir picker (default `%ProgramFiles%\ComTekAtomicClock\`), Start Menu shortcuts, optional desktop shortcut, post-install service registration, and a clean Add/Remove Programs uninstall path that runs `ServiceInstaller.exe uninstall` first.

**Bump `MyAppVersion`** in `installer.iss` whenever the project version bumps — it's currently kept in lockstep manually. (A `.iss`-from-csproj generator is a future possibility but the manual edit is one line.)

---

## §21 — Implementation Status

This is the bridging table from "what `requirements.txt` aspires to" to "what code at v0.0.32 does." A regen team that ships only **Implemented** items has a faithful v0.0.32 reproduction; ticking off the **Planned** items is the path to v1.0.

### Implemented (regen these as-is)

| Area | Item | Notes |
|---|---|---|
| Themes | All 12 themes per §10 | Default Atomic Lab; per-theme defaults match `ThemeCatalog.All` |
| Tabs | Tabbed view (native `TabControl`) | v0.0.33+ — Dragablz removed |
| Tabs | Drag-reorder within strip | Native (no library) |
| Tabs | Tab-name refresh via PropertyChanged on `Label` | v0.0.33+ — restored cascade after dropping Dragablz |
| Tabs | Single-click selection | Native, no rescue handler needed |
| Tabs | Right-click ContextMenu: Tab settings… / Open in new window | v0.0.33+ |
| Tabs | Active 19pt Bold / Inactive 9pt | Per v0.0.28..v0.0.30, §8 |
| Tabs | "+ New tab" toolbar button → AddTabCommand | v0.0.33+ |
| Tabs | "+ New window" toolbar button → spawn FloatingClockWindow | v0.0.33+ |
| Windows | Floating clock window, single clock per window | v0.0.33+ — replaces tear-away |
| Windows | "Open in new window" right-click on tab | v0.0.33+ |
| Windows | "Bring back into tabs" on floating window's "?" menu | v0.0.33+ |
| Service | Worker Service hosted via `AddWindowsService` | `ComTekAtomicClockSvc` |
| Service | Multi-source stratum-1 pool walk: Boulder (NIST, 10 servers + anycast) and Brazil (NTP.br, 5 servers + anycast). Source selectable from Settings dialog. | v0.0.36+ — `TimeSourcePool.GetWalkOrder(source, primary)` |
| UI | Atomic Lab face's NIST-panel subtitle dynamic per TimeSource (`"NIST · BOULDER · CO"` ↔ `"NTP.BR · SÃO PAULO · BR"`) | v0.0.36+ |
| UI | Per-face source label (`BOULDER` / `BRASIL`) at top-center on every theme | v0.0.36+ — `AddSourceLabel()` helper |
| Service | SNTP/RFC 4330 packet build + validation | §4 — `NtpPacket.cs` |
| Service | Pool walk: primary first, then randomized rest | Fisher–Yates |
| Service | Per-server timeout 5 s, per-server poll ≥ 4 s | `SyncWorker.cs` |
| Service | Sync interval clamp `[15 min, 24 h]` | `LoadIntervalFromConfig` |
| Service | First sync runs immediately on start | Before first interval delay |
| Service | `SetSystemTime` via P/Invoke | Requires `SE_SYSTEMTIME_NAME` |
| Service | Event Log source `ComTekAtomicClock` | LocalSystem-registered |
| IPC | Named pipe `ComTekAtomicClock.UiToService` | 4 server instances, 64 KiB buffers |
| IPC | Length-prefixed JSON envelopes (4 B LE int32 + UTF-8) | 1 MiB max |
| IPC | Pipe ACL: InteractiveSid + LocalSystemSid | `IpcServer.cs` |
| IPC | Schema version `1`, version-mismatch logged but processed | Versioned-but-permissive |
| IPC | `LastSyncStatusRequest/Response` round-trip from UI every ~5 s | `MainWindowViewModel` |
| Settings | `%APPDATA%\ComTekAtomicClock\settings.json` (per-user) | Atomic write + corrupt-recovery |
| Settings | `%ProgramData%\ComTekAtomicClock\service.json` (per-machine) | Read by Service; Implemented `SaveServiceConfig` (UI wire-up Planned) |
| Settings | `[JsonExtensionData]` forward-compat | Unknown fields round-trip |
| Settings | `Theme` enum stored as JSON string (camelCase) | Reorder-safe |
| Settings | Per-tab GUID `Id` | Stable across rename / reorder |
| Settings | Auto-derive Label from IANA TimeZoneId | `DeriveLabelFromIanaId` |
| Installer | `ServiceInstaller.exe install / uninstall [--purge-user-data]` | Manifest `requireAdministrator` |
| Installer | `sc.exe create … start= demand` | Per v0.0.32 (NOT auto — see §22) |
| Installer | `sc.exe sdset` granting AU + BA + SY | Per §4 |
| Installer | %ProgramData% NTFS ACL grants Authenticated Users Modify | (NOT Interactive — see §22) |
| UI | FluentWindow + Mica backdrop | WPF-UI 4.2.1 |
| UI | First-run banner when `ServiceState ≠ Running` | `MainWindowViewModel.BannerVisible` |
| UI | Banner button labels match `ServiceState` | `NotInstalled` / `InstalledNotRunning` |
| UI | Banner button → ServiceInstaller.exe via `Verb="runas"` | UAC consent |
| UI | Service-state poll cadence (4 s) | `MainWindowViewModel` |
| UI | Last-sync re-render cadence (1 s) | |
| UI | IPC LastSyncStatus cadence (~5 s, gated on Running) | |
| UI | `Last sync: …` formatter strings + U+2212 sign convention | `FormatDrift` |
| UI | Three unhandled-exception handlers in App constructor | Per §17 |
| UI | Tab settings dialog: Time zone + Theme + Sync frequency fields | (Sync-frequency persistence Planned) |
| UI | Themes gallery dialog with 12 SVG previews | `SharpVectors.SvgViewbox` |
| UI | About dialog with ALPHA badge + MIT license | |
| UI | Help dialog with sections per §14 | |
| UI | Per-theme `OverlayGlyphBrush` (white/near-black) | Re-evaluates on Theme change |
| UI | Date strip on every theme (day-of-week + date + month + year) | Per v0.0.22 |
| UI | Daylight + Boulder Slate centered date | Per v0.0.24 |
| UI | Encoder themes always 24-h | Per v0.0.27 |
| UI | Non-encoder themes default 12-h | Per v0.0.27 |
| Build | All four projects target `net8.0-windows` (Shared = `net8.0`) | |
| Build | Versions: UI `0.0.32` / `0.0.32.0` / `0.0.32.0` | |
| Build | App icon embedded as `<ApplicationIcon>` and `<Resource>` | |
| Build | 12 theme SVGs linked from `windows/design/themes/` | Single source of truth |

### Planned (in scope for v1; not yet coded)

> **For day-to-day work, use `windows/TODO.md`** as the single source of truth — it numbers and groups the open items, includes the tiny polish list, and tracks "recently shipped" so a future session sees the resolved trail. The table below is the spec-grade reference for *what* the items are; `TODO.md` is the operational view.


| Area | Item | Reference |
|---|---|---|
| **Phase 2 (snap)** | **Magnetic-snap floating clock windows** — drag a `FloatingClockWindow` within ~12 px of another clock window's edge → it snaps and joins a "snap group" so they move as a unit. Implementation: `HwndSource.AddHook` on `WM_MOVING` / `WM_WINDOWPOSCHANGING`, mutate proposed RECT before Windows applies the move. Estimated 280-390 LOC, 1-2 days. | v0.0.33 design — see CONTEXT.md |
| **Phase 2 (snap)** | Visual feedback during drag — edge highlight or ghost outline when a snap is imminent | v0.0.33 design |
| **Phase 2 (snap)** | "Enable snap" toggle in Settings (default OFF until proven stable) | v0.0.33 design |
| **Phase 2 (snap)** | `WindowSettings.SnapGroupId : Guid?` + `SnapEdge : enum` model fields so groups reconstitute on restart | v0.0.33 design |
| Tray | System tray icon: open / sync now / last status / settings / exit | S-1 in audit |
| Tray | Closing last window minimizes to tray, doesn't terminate | S-1 |
| Window | Desktop overlay mode: borderless, transparent, click-through, desktop layer | S-2 (`WindowSettings.OverlayMode` exists) |
| Window | Floating-window position persistence (X/Y/Width/Height across restart) | S-3 |
| Window | `Window.Topmost` bound to `WindowSettings.AlwaysOnTop` | S-24 |
| Sync | Confirm-large-offset toast `[Apply][Skip]` with 30 s timeout | S-4, D-5, D-13 |
| Sync | UI persistence of sync-frequency to `service.json` | S-16 |
| Sync | Sync server selector UI (NIST pool members + anycast) | S-22, D-7 |
| Sync | Sync-server input validation against `IsKnownNistHost` | D-7 |
| Sync | True `SyncNowRequest` triggering re-sync (not cached snapshot) | D-5 |
| UI | Tab rename input | S-21, D-17 |
| UI | Five-slot color override pickers (Ring/Face/Hands/Numbers/Digital) | S-5..S-7, D-11 |
| UI | HSV wheel + RGB sliders + hex input + eyedropper | S-6 |
| UI | "Reset colors to theme defaults" link | S-7 |
| UI | Time format selector (Auto / 12h / 24h) | S-8, D-9 |
| UI | Show-digital-readout toggle for analog themes | S-10, D-8 |
| UI | Renderer consumes `ShowDigitalReadout` to hide readout | D-8 |
| UI | Second-hand motion override selector (ThemeDefault / Smooth / Stepped) | S-9, D-10 |
| UI | "Use the same theme across all tabs" toggle + propagation | S-11 |
| UI | "Start with Windows" toggle + shell-startup integration | S-12 |
| UI | "Confirm large sync corrections" toggle | S-13 |
| UI | Telemetry opt-in toggles + endpoint + consent dialog + scrubbing | S-14, S-28 |
| Renderer | 24-hour numeral-flip on analog faces (1–11 ↔ 13–23, 12 fixed) | S-15, D-9 |
| Renderer | Five-slot color overrides applied per theme | D-11 |
| Renderer | "SERVICE NOT RUNNING" on-face warning text in Ring color | D-12 |
| Renderer | Card-flip animation on Flip Clock | TODO at `:1366-1367` |
| Renderer | Chase-bulb wave animation on Marquee | TODO at `:1366-1367` |
| Catalog | Full IANA tz catalog (~600 zones, not the ~140 from Windows zones) | D-18 |
| Settings | Schema version-detect-and-migrate code path | S-17 |
| Packaging | MSIX + `.appinstaller` + GitHub Pages publishing pipeline | S-18 |
| Packaging | ARM64 build target + RID configuration | S-19 |
| Service | Service start mode `auto` for true headless operation | D-1, D-4 (current `demand` is intentional for alpha) |
| Service | Service-installer ACLs use `WellKnownSidType.InteractiveSid` not `AuthenticatedUserSid` | D-19 (legacy spec preference) |
| Help | Help corpus refresh to remove right-click context-menu reference | D-16, M-8 |
| Privacy | `PRIVACY.md` + TLS-pinned telemetry endpoint + scrubbing pipeline | S-14 / §2.11 of legacy spec |
| Window | Window state persistence (size / position / maximized) across restarts | Legacy spec § 1.3 |
| Modes | **Timer** — stopwatch / elapsed-time mode per tab/window, alongside the always-on clock | Queued 2026-05-01 |
| Modes | **Countdown** — user-set target duration, count down to zero with notification | Queued 2026-05-01 |
| ~~Bug — Settings dialog Owner~~ | ~~Hardcoded to MainWindow — should be the originating window when opened from a `FloatingClockWindow`~~ | **Resolved v0.0.39** — see §13 "Dialog Owner — origin-window-aware" |

---

## §22 — Resolved Contradictions

The legacy `requirements.txt` (591 lines, 2026-04-25) accumulated several internal contradictions and several code-↔-spec drifts during the v0.0.14..v0.0.32 iteration cycle. The audit at `windows/docs/code-vs-spec-audit.md` enumerates all 21 DRIFT items, 28 SPEC-ONLY items, and 8 spec-↔-spec contradictions. This section records how **this** spec resolves each one. **The general principle is: code wins.**

### M-1 · Eleven vs. twelve themes

- Legacy spec line 17 says "eleven face themes (subject to revision)."
- Legacy spec lines 21–42 enumerate twelve.
- Code: `Theme` enum has 12 entries; `ThemeCatalog.All` has 12.
- **Resolved:** **Twelve themes.** The legacy prose count was a typo. See §10 for the canonical list and order.

### M-2 / D-2 · Default sync interval

- Legacy spec § 1.5 says "hourly," § 1.10 says "hourly."
- Code: `GlobalSettings.SyncInterval` defaults to 1 h; `ServiceConfig.SyncInterval` defaults to 12 h. UI dialog offers 6 / 12 / 24 h only.
- **Resolved:** **Service default = 12 h** (current code, middle of the three UI choices). UI default for `GlobalSettings` = 1 h is preserved; the field is unused by Service today. Adding 1-hour and 30-minute choices to the UI dialog is **Planned**.

### M-3 · Settings popover vs. dialog

- Legacy spec § 1.2 / § 1.8 says "popover" three times.
- Code: modal `TabSettingsDialog` (FluentWindow, 560 × 540, NoResize, CenterOwner).
- **Resolved:** **Modal dialog.** Popover semantics are not appropriate for the field count and the desktop ergonomics (popovers dismiss too easily). See §13.

### M-6 / D-1 / D-4 · Service start mode

- Legacy spec § 1.6 says "Automatic." § 1.9 documents `start= auto`.
- Code: `start= demand`, with comment *"on-demand start mode (NOT auto-start at boot). The UI starts the service on app launch and stops it on app exit."* HelpDialog confirms this for users.
- **Resolved (alpha):** **`demand` start mode**, service-runs-only-while-UI-is-open. This is a deliberate alpha simplification — it avoids the "service consuming network on machines whose owner uninstalled the app and forgot to remove the service" failure mode. **Migrating to `auto` for v1.0 is Planned.** Until then, the value proposition is shifted from "always-on system service" to "the clock app keeps your system clock accurate while you have it open."

### D-19 · Service ACL (Interactive vs. Authenticated)

- Legacy spec § 2.10 says "interactive Users."
- Code: `WellKnownSidType.AuthenticatedUserSid`.
- **Resolved:** **Authenticated Users** (current code). Authenticated Users is broader (includes domain users), which lets the unprivileged UI start/stop the service across logon sessions on managed corporate machines. Pipe ACL still uses InteractiveSid (narrower), which is correct for IPC.

### D-6 · New-tab default time zone

- Legacy spec § 1.10 says "system local IANA."
- Code: first-run tab uses local IANA; subsequently-added tabs default to `"UTC"`.
- **Resolved:** **First tab uses local; later tabs default to UTC.** Current behavior. Help text updated to state "New tabs default to UTC and Atomic Lab" (already in `HelpDialog.xaml`).

### M-8 / D-16 · Right-click on tabs

- Legacy spec doesn't mandate a context menu but `HelpDialog.xaml` mentioned right-click → Close tab.
- Code v0.0.26 removed the context menu entirely; right-click now opens the Tab Settings dialog (per v0.0.23).
- **Resolved:** **Right-click → Tab Settings dialog. No context menu.** Help text refresh is **Planned** (D-16).

### D-9 · Time-format default for digital readout

- Legacy spec § 1.1 describes `Auto` / `12h` / `24h` selector with noon-flip.
- Code: encoder themes (Binary / Hex / BinaryDigital) hardcode 24-h; non-encoder themes hardcode 12-h. No `TimeFormat` consumer in renderer.
- **Resolved (alpha):** **Encoders always 24-h; non-encoders default 12-h** (per v0.0.27). Renderer + UI to honor `TimeFormat` is **Planned**, with encoders permanently exempt from 12-h overrides.

### D-12 · "SERVICE NOT RUNNING" on-face warning

- Legacy spec § 1.9 describes injection of the warning text into the digital readout / decoded readout in Ring color.
- Code: never renders the string; relies on the main-window banner instead.
- **Resolved:** **Banner is the warning** for now; on-face warning is **Planned**. Banner is unambiguous and doesn't require per-theme rendering work.

### D-14 · ThemeCatalog header-comment count

- Code comment groups themes incorrectly ("3 digital, 2 specialty, 1 binary digital").
- **Resolved:** Group as **6 analog + 4 digital-only + 2 encoder = 12** per §10. Code comment to be corrected on next touch.

### D-21 · MainWindow stale comment about FontSize=13

- Code comment claims "base FontSize=13" but actual setter is 9.
- **Resolved:** **Inactive base = 9, Active = 19, delta = 10pt** (per §8). Comment to be corrected on next touch.

### v0.0.33 · Dragablz vs. native TabControl

- Earlier code + earlier SPEC.md (v1.0) treated Dragablz as a permanent dependency, with the v0.0.32 imperative tab-header refresh as the canonical pattern.
- v0.0.33 dropped Dragablz entirely after Dan identified the library as the root cause of nearly every tab-related bug across v0.0.14..v0.0.32. Tear-away gesture removed; explicit "+ New window" / "Open in new window" / "Bring back into tabs" commands replace it.
- **Resolved:** **Native `System.Windows.Controls.TabControl`** with a flat-rectangular `ControlTemplate`-replaced `TabItem`. Magnetic snap added to Phase-2 Planned (§21).
- Files deleted: `Services/AppInterTabClient.cs`, `MainWindow.SetTabHeaderInAllDisplays`, `EnumerateVisualDescendants`, `Tag="TabHeaderText"` markup convention, `TabItem_PreviewMouseLeftButtonDown` rescue handler, the Dragablz `Style.Resources` Thumb-stripping block.
- Files added: explicit `OpenInNewWindowCommand`, `NewClockWindowCommand`, `BringWindowIntoTabsCommand` on `MainWindowViewModel`; `_openFloatingWindows` registry; `BringIntoTabsMenuItem_Click` handler on `FloatingClockWindow`.

---

## §23 — Glossary

| Term | Definition |
|---|---|
| **anycast** | NIST's load-balancing endpoint `time.nist.gov` that routes to whichever server in the pool is closest/healthiest. Default primary; tried first on every sync. |
| **BCD** | Binary-Coded Decimal. Each decimal digit stored as 4 bits (8-4-2-1). Used by the Binary theme to render hours/minutes/seconds across 6 columns of LEDs. |
| **Dragablz** | Third-party WPF library providing Chrome-style tabbed and torn-away windows. v0.0.3.234. Several known quirks documented in §18. |
| **DST** | Daylight Saving Time. The Slab theme's context line surfaces `IsDaylightSavingTime()` as `STD` / `DST`. |
| **FluentWindow** | WPF-UI's window subclass that supports Mica backdrop and Fluent design. Default for all dialogs and main window. |
| **IANA tz** | Internet Assigned Numbers Authority time-zone identifier — e.g. `America/New_York`, `Europe/London`. Stored as the canonical `TimeZoneId` field; resolved via `TimeZoneInfo.FindSystemTimeZoneById`. |
| **IPC** | Inter-process communication. UI ↔ Service via the named pipe `ComTekAtomicClock.UiToService`. |
| **LocalSystem** | Windows built-in account that services run under by default. Has `SE_SYSTEMTIME_NAME` privilege out of the box. |
| **Mica** | Windows 11 backdrop material — wallpaper-tinted translucent surface. Enabled on all FluentWindows. |
| **Mondaine** | Swiss railway clock design (Boulder Slate's inspiration). Iconic red lollipop second hand that pauses at 12 each minute on real Mondaines. |
| **named pipe** | Windows IPC primitive. Server creates `\\.\pipe\<name>`; clients connect via `NamedPipeClientStream`. ACL'd via `PipeSecurity`. |
| **NIST stratum-1** | Servers directly synchronized to NIST's primary atomic clocks (NIST-F1, NIST-F2 in Boulder, CO). Stratum 1 is the highest tier of NTP hierarchy. |
| **NTP / SNTP** | Network Time Protocol / Simple NTP. SNTP (RFC 4330) is the simpler subset suitable for client-only implementations. |
| **PoCo** | Plain Old C# Object — a class with public properties and no behavior, used as a serialization target. `TabSettings`, `GlobalSettings`, `ServiceConfig` are POCOs. |
| **ProgramData** | Per-machine writable folder, typically `C:\ProgramData\<App>\`. Used for `service.json`. |
| **SCM** | Service Control Manager — the Windows component that runs services. The UI polls it via `ServiceController` for state changes. |
| **SDDL** | Security Descriptor Definition Language — the textual ACL format consumed by `sc.exe sdset`. Example: `D:(A;;CCLCSWRPWPLOCRRC;;;AU)…`. |
| **SE_SYSTEMTIME_NAME** | The Windows privilege required to call `SetSystemTime`. Granted to LocalSystem by default. |
| **stratum** | Tier in the NTP hierarchy. Stratum 0 = atomic clocks themselves; stratum 1 = servers directly attached to stratum 0; stratum 16 = unsynchronized. |
| **U+2014 (—)** | Em-dash. Used in the service display name `ComTek Atomic Clock — time sync` and in user-facing prose throughout the app. **Distinct from U+002D hyphen.** |
| **U+2212 (−)** | Mathematical minus sign. Used in `"Last sync: 12s ago (corrected −8.7 ms)"` to indicate correction direction (positive offset → corrected back). **Distinct from U+002D hyphen.** |
| **UAC** | User Account Control — Windows elevation prompt. Triggered by launching `ServiceInstaller.exe` with `Verb="runas"`. |
| **WPF-UI** | Third-party library providing FluentWindow, ContentDialog, and Fluent-styled controls. v4.2.1. Themes loaded in `App.xaml`. |
| **`%APPDATA%`** | Per-user roaming folder, typically `C:\Users\<user>\AppData\Roaming\<App>\`. Used for `settings.json`. |

---

## End of document

**Document version:** 2.1  
**Code baseline:** v1.1.0  
**Last reviewed:** 2026-05-03

Update this document in the same commit as any change that affects behavior described here. Use `windows/CONTEXT.md` (a separate, faster-moving doc) for ongoing decisions and constraints between formal SPEC revisions.
