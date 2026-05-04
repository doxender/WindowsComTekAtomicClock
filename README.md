# ComTek Atomic Clock — Windows

> [!IMPORTANT]
> **v1.1.6 — BETA · Second public release (2026-05-03).** Adds the **Captain John's Marina (CaptJohn)** theme (7th analog face) plus a hand-length pass that improves readability on every other analog face. Also: each Setup.exe wipes persisted settings at install (testing-clarity tradeoff during iteration; will be narrowed before the next public-distribution release). Bug reports: <https://github.com/doxender/WindowsComTekAtomicClock/issues>. The bundled Windows Service adjusts your system clock against NIST or NTP.br stratum-1 servers, so don't rely on this for timekeeping that matters in a regulated context until the patch cycle settles. Code-signing is still on the post-1.0 list — Windows SmartScreen will warn on first launch.

A Windows desktop atomic clock that synchronizes the system clock to NIST (Boulder, CO) or NTP.br (São Paulo, BR) stratum-1 servers, with 13 themable clock faces and a Chrome-style tabbed time-zone view.

## Download v1.1.6

| Asset | Size | Use |
|---|---|---|
| [`ComTekAtomicClock-v1.1.6-Setup.exe`](https://github.com/doxender/WindowsComTekAtomicClock/releases/download/v1.1.6/ComTekAtomicClock-v1.1.6-Setup.exe) | 97 MB | Recommended. Inno Setup installer — license screen, install-dir picker, Start Menu shortcut, Add/Remove Programs entry. |
| [`ComTekAtomicClock-v1.1.6-win-x64-self-contained.zip`](https://github.com/doxender/WindowsComTekAtomicClock/releases/download/v1.1.6/ComTekAtomicClock-v1.1.6-win-x64-self-contained.zip) | 135 MB | Portable — same payload, unzipped. For installs outside Program Files. |

Both are **self-contained** (the .NET 8 runtime is bundled — no separate runtime install). All releases: <https://github.com/doxender/WindowsComTekAtomicClock/releases>.

### Upgrading from v1.0.0

The installer auto-uninstalls a prior v1.0.0 install (matching `AppId`) and resets persisted settings to defaults. Your tabs and theme picks will reset; system-clock sync state lives in the Windows Service and isn't touched.

### SmartScreen warning

The exes are not Authenticode-signed yet. Windows SmartScreen will warn ("Windows protected your PC" / "unknown publisher") on first launch. Click **More info** → **Run anyway**. Code-signing is the highest-priority post-1.0 item.

## Features

- **Stratum-1 atomic time sync** — NIST Boulder pool (default, 10 named servers across Gaithersburg, MD + Fort Collins, CO, plus the `time.nist.gov` anycast) and NIC.br / NTP.br pool (5 GPS-disciplined servers in São Paulo, BR). Switch the source via Settings.
- **13 themable clock faces** — 7 analog (Atomic Lab, Boulder Slate, Aero Glass, Cathode, Concourse, Daylight, **Captain John's Marina** — v1.1+) + 4 digital (Flip Clock, Marquee, Slab, Binary Digital) + 2 encoders (Binary, Hex). Atomic Lab is the default. Picker reachable from the `?` overlay (in-tab) or `⋯` menu (floating window) → **Themes…**.
- **Captain John's Marina** (CaptJohn) — v1.1+ — parchment + brass face with the Rio Dulce marina's "Busted Flush" logo at 40% opacity. The bottom-left ☠ Jolly Roger button opens a popup with three controls: **Hora Chapín** (lazy bar-clock mode — minute hand jitters ±3 min, syncs to real time on every `:00` / `:30`; real hands at 10% opacity behind it); **Almuerzo** (12:00 demo — sets clock to 11:55 and plays the noon flash event over the next 10 minutes); **Fini** (5:00 PM demo — same shape, 16:55 → 17:05). The flash event also fires automatically at real noon and real 5 PM regardless of mode.
- **Chrome-style tabbed time-zone view** — native WPF `TabControl`; right-click a tab → Settings; `+ New tab` / `+ New window` toolbar buttons; per-tab IANA time zone (~140 zones).
- **Free-floating clock windows** — spawn via `+ New window` or per-tab "Open in new window". Each hosts one clock; single `⋯` overlay button hosts Settings / Themes / Bring back into tabs / Help / About.
- **Per-face source label** — single warm-amber `BOULDER` or `BRASIL` header on every theme reflects the active time source at a glance.
- **Background Windows Service** that syncs the system clock via SNTP. Runs only while the UI is open (alpha simplification — `start= demand`). User-selectable cadence: 6 / 12 / 24 hours (default 12).
- **Live last-sync display** in the status bar (`Last sync: 12s ago (corrected −8.7 ms)`). Refreshed every second; IPC fetch every 5 s.
- **Persistent state** — per-user UI state (tabs / themes / time zones / windows) in `%APPDATA%\ComTekAtomicClock\settings.json`; per-machine sync config in `%ProgramData%\ComTekAtomicClock\service.json`. Atomic write, corrupt-file recovery, forward-compat via `[JsonExtensionData]`.

### Deferred to post-1.0

See [TODO.md](TODO.md) for the full ~38-item open list. Headline items:

- Timer + Countdown modes (active queue)
- Magnetic-snap floating windows (Phase 2)
- System tray icon + minimize-to-tray
- Five-slot color overrides (Ring / Face / Hands / Numbers / Digital)
- Time-format selector UI (Auto / 12h / 24h)
- Authenticode code-signing (kills the SmartScreen warning)
- MSIX packaging + GitHub Pages publish pipeline
- ARM64 build target

## Architecture

Four-project .NET 8 solution. Buildable from CLI alone (`dotnet build`); Visual Studio 2022/2026 recommended for debugging + the XAML designer but not required.

| Project | Type | Target | Runs as | Purpose |
|---|---|---|---|---|
| `ComTekAtomicClock.UI` | WPF App | `net8.0-windows` | Current user (asInvoker) | FluentWindow + Mica, tabbed UI, clock faces, dialogs, free-floating windows |
| `ComTekAtomicClock.Service` | Worker Service | `net8.0-windows` | LocalSystem | SNTP query to the active stratum-1 pool, calls `SetSystemTime`, schedules periodic sync, hosts the named-pipe IPC server |
| `ComTekAtomicClock.Shared` | Class Library | `net8.0` | n/a (referenced) | Settings models, `TimeSource` enum, IPC contracts, NTP packet types |
| `ComTekAtomicClock.ServiceInstaller` | Console (admin) | `net8.0-windows` | LocalSystem (one-shot via UAC) | `sc.exe create / sdset / delete` to register the Windows Service; sets %ProgramData% ACLs |

UI ↔ Service IPC over a named pipe (`ComTekAtomicClock.UiToService`) using length-prefixed JSON envelopes. Schema-versioned, ACL-restricted to interactive users + LocalSystem. Contract in `ComTekAtomicClock.Shared.Ipc`.

The Service is the only component with rights to change the system clock; the ServiceInstaller is the only component requiring admin elevation, and only when the user clicks "Install / start the time-sync service" on the first-run banner.

For the full authoritative spec see [SPEC.md](SPEC.md) (~1,650 lines, code-as-ground-truth).

## Build from source

### Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git
- (Optional) [Visual Studio 2026](https://visualstudio.microsoft.com/) or 2022 — Community is fine. Install the **.NET desktop development** workload.
- (Optional, for building the installer) [Inno Setup](https://jrsoftware.org/isinfo.php) — `winget install JRSoftware.InnoSetup`.

### Build

From this directory (`windows/`):

```pwsh
# Restore + build all four projects
dotnet build -c Debug

# Run the UI in the foreground (during development)
dotnet run --project src/ComTekAtomicClock.UI

# Run the Service interactively in dev (does NOT require admin while
# running as a console app; only requires admin when registered with SCM)
dotnet run --project src/ComTekAtomicClock.Service
```

### Production install of the Windows Service (development workflow)

The UI's first-run banner launches `ComTekAtomicClock.ServiceInstaller` via UAC. To install manually after a Release publish:

```pwsh
# After:  dotnet publish -c Release -r win-x64 --self-contained true
ComTekAtomicClock.ServiceInstaller.exe install --service-exe "<full path>\ComTekAtomicClock.Service.exe"
```

(The legacy `sc.exe create … start= auto` shape is wrong for this build — v1.0 uses `start= demand` with the UI managing the service lifecycle.)

### Building the release package

See [SPEC.md §20](SPEC.md) for the full pipeline. The version comes from `tools/installer.iss` (`MyAppVersion`); update both the .csproj `<Version>` and the .iss `MyAppVersion` for a release. Short version, with `$ver` as the version string:

```pwsh
$ver = "1.1.6"   # match the csproj <Version> / installer.iss MyAppVersion

# 1. Self-contained publish of all three projects (~315 MB output)
dotnet publish src\ComTekAtomicClock.UI\ComTekAtomicClock.UI.csproj `
    -c Release -r win-x64 --self-contained true -o release\v$ver\UI
dotnet publish src\ComTekAtomicClock.Service\ComTekAtomicClock.Service.csproj `
    -c Release -r win-x64 --self-contained true -o release\v$ver\Service
dotnet publish src\ComTekAtomicClock.ServiceInstaller\ComTekAtomicClock.ServiceInstaller.csproj `
    -c Release -r win-x64 --self-contained true -o release\v$ver\Installer

# 2. Copy supporting docs into the staging dir
Copy-Item LICENSE,README.md,CHANGELOG.md,SPEC.md release\v$ver\

# 3. Compile the Inno Setup installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" tools\installer.iss
# Output: release\ComTekAtomicClock-v$ver-Setup.exe (~97 MB)
```

> **Heads-up — the installer wipes user settings (v1.1.6+).** Every Setup.exe resets `%APPDATA%\ComTekAtomicClock\settings.json` and `%ProgramData%\ComTekAtomicClock\service.json` at install time, so each install starts from the code's baked-in defaults. Intentional during the alpha/beta iteration cycle (testing clarity); will be narrowed before public-distribution releases. See [CHANGELOG.md `[1.1.6]`](CHANGELOG.md).

## NIST + NTP.br usage notes

- **Boulder source** — anycast `time.nist.gov` (load-balances across the whole NIST pool); 10 named stratum-1 servers across Gaithersburg, MD (`time-{a..e}-g.nist.gov`) and Fort Collins, CO (`time-{a..e}-wwv.nist.gov`). NIST asks for ≤ 1 query / 4 s per server.
- **Brazil source** — `a.ntp.br` anycast + `b/c/d/gps.ntp.br` named servers. NIC.br asks for ≤ 1 query / 1 s per server.
- The service's per-server poll-interval floor is 4 s, satisfying both. Default sync cadence is 12 hours, well within both operators' guidance.
- All servers are publicly available; no API key required. NIST list: <https://tf.nist.gov/tf-cgi/servers.cgi>. NTP.br: <https://ntp.br/guia-mais-rapida.php>.

## Repository layout

```
ComTekAtomicClock/
└── windows/                              <-- this repo
    ├── ComTekAtomicClock.slnx            (.NET 9+ XML solution format)
    ├── README.md                         (you are here)
    ├── SPEC.md                           (authoritative spec — code-as-ground-truth)
    ├── CHANGELOG.md                      (per-version problem/solution log)
    ├── CONTEXT.md                        (running decisions / constraints / gotchas)
    ├── TODO.md                           (consolidated open backlog)
    ├── requirements.txt                  (legacy spec — kept for history; superseded by SPEC.md)
    ├── LICENSE                           (MIT)
    ├── design/                           (visual design artifacts: 12 .svg theme previews + a CaptJohn .png mockup + index.html gallery + Cinzel-Variable.ttf for the CaptJohn numerals)
    ├── docs/
    │   └── code-vs-spec-audit.md         (drift audit between requirements.txt and code)
    ├── tools/
    │   ├── build-icon.ps1                (regenerate Assets/AppIcon.ico from PNG master)
    │   └── installer.iss                 (Inno Setup script for the Setup.exe)
    └── src/                              (C# source)
        ├── ComTekAtomicClock.UI/         (WPF app)
        ├── ComTekAtomicClock.Service/    (Worker Service)
        ├── ComTekAtomicClock.Shared/     (class library)
        └── ComTekAtomicClock.ServiceInstaller/   (privileged helper)
```

The parent `ComTekAtomicClock/` folder is reserved as an umbrella for future per-platform repos (e.g. `mac/`, `linux/`).

## Repository remote

```
origin  https://github.com/doxender/WindowsComTekAtomicClock.git
```

## Reporting issues

<https://github.com/doxender/WindowsComTekAtomicClock/issues> — please include:

- Version (visible upper-left on every clock face: e.g. `v1.1.6`)
- Windows version (`winver`)
- Steps to reproduce
- Expected vs. actual behavior
- Any output from `%ProgramData%\ComTekAtomicClock\` log if a sync issue, or the clock-face screenshot if a UI issue

## License

[MIT](LICENSE) © 2026 Daniel V. Oxender.
