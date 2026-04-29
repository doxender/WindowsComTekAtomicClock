# ComTek Atomic Clock — Windows

> [!WARNING]
> **ALPHA — pre-release / not yet tested for public use.**
>
> This project is under active development. Code, behavior, settings format, packaging, and the requirements spec all change without notice between commits. Installing the bundled Windows Service will adjust your system clock against NIST. Do not use this app for any timekeeping that matters to you — production servers, deadline-sensitive work, anything regulated. Wait for the first signed `0.1.0` release tag before relying on it.

A Windows desktop clock that synchronizes to the NIST atomic clock in Boulder, CO and displays multiple time zones with both digital and analog faces.

## Features

- Digital and analog clock faces (user-selectable per window)
- Tabbed multi-time-zone view in the main app
- Multiple independent clock windows, each pinned to a different location/time zone
- Optional always-on-desktop overlay (transparent, click-through window)
- Background Windows Service that periodically syncs the system clock to NIST Boulder via SNTP
- System tray integration

## Architecture

Four-project Visual Studio solution targeting **.NET 8 LTS** on Windows 10/11. Buildable from the CLI alone (`dotnet build`); Visual Studio 2026 (or 2022) is recommended for debugging and the XAML designer but not required to author or build.

| Project | Type | Target | Runs as | Purpose |
|---|---|---|---|---|
| `ComTekAtomicClock.UI` | WPF App | `net8.0-windows` | Current user | Tray icon, tabbed UI, clock windows, desktop overlay |
| `ComTekAtomicClock.Service` | Worker Service | `net8.0` | LocalSystem | SNTP query to NIST stratum-1 pool, calls `SetSystemTime`, schedules periodic sync |
| `ComTekAtomicClock.Shared` | Class Library | `net8.0` | n/a (referenced) | NTP packet types, IPC contracts, shared models |
| `ComTekAtomicClock.ServiceInstaller` | Console (admin) | `net8.0` | LocalSystem (one-shot, via UAC) | Privileged helper that installs/starts the Windows Service when the user clicks the §1.9 banner |

The UI and Service communicate over a named pipe (`ComTekAtomicClock.UiToService`); the contract lives in `ComTekAtomicClock.Shared.Ipc`. The Service is the only component with rights to change the system clock; the ServiceInstaller is the only component that requires admin elevation, and only when the user clicks "Install / start the time-sync service" on the §1.9 banner.

## Prerequisites

- Windows 10 (1809+) or Windows 11
- [Visual Studio 2026](https://visualstudio.microsoft.com/) (or 2022) — Community edition is fine. Install the **.NET desktop development** workload.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

## Build & run

From this directory (`windows/`):

```pwsh
# Restore and build all four projects
dotnet restore
dotnet build -c Debug

# Run the UI in the foreground (during development; debug build)
dotnet run --project src/ComTekAtomicClock.UI

# Run the Service interactively (in dev — does NOT require admin
# while running as a console app; only requires admin when registered
# with the SCM)
dotnet run --project src/ComTekAtomicClock.Service
```

Production install of the Windows Service is handled by the
`ComTekAtomicClock.ServiceInstaller` helper, which the UI's §1.9
banner launches via UAC. For manual install during development:

```pwsh
# From an Admin PowerShell, after `dotnet publish -c Release -r win-x64`:
sc.exe create ComTekAtomicClockSvc binPath= "<full path>\ComTekAtomicClock.Service.exe" start= auto DisplayName= "ComTek Atomic Clock — time sync"
sc.exe start ComTekAtomicClockSvc
```

## NIST / NTP usage notes

- Default sync server: `time.nist.gov` (anycast to the Boulder cluster).
- NIST asks SNTP clients to poll **no more than once every 4 seconds** per server. The service defaults to **hourly** sync, which is well within the published guidance.
- All NIST time servers are publicly available; no API key required. See <https://tf.nist.gov/tf-cgi/servers.cgi>.

## Repository layout

```
ComTekAtomicClock/
└── windows/                              <-- this repo
    ├── ComTekAtomicClock.slnx            (.NET 9+ XML solution format)
    ├── README.md                         (you are here)
    ├── requirements.txt                  (functional + non-functional spec — source of truth)
    ├── LICENSE                           (MIT)
    ├── .gitignore
    ├── design/                           (visual design artifacts)
    │   ├── README.md                     (per-theme rationale + slot mapping)
    │   └── themes/                       (12 .svg + index.html gallery)
    └── src/                              (C# source)
        ├── ComTekAtomicClock.UI/         (WPF app)
        ├── ComTekAtomicClock.Service/    (Worker Service)
        ├── ComTekAtomicClock.Shared/     (class library)
        └── ComTekAtomicClock.ServiceInstaller/   (privileged helper)
```

The parent `ComTekAtomicClock/` folder is reserved as an umbrella for future per-platform repos (e.g. `mac/`, `linux/`).

Future folders, added when needed: `tests/` (xUnit projects mirroring `src/`), `tools/` (build/sign/package scripts), `package/` (MSIX manifest + assets + `.appinstaller` template per § 2.7 of `requirements.txt`).

## Repository remote

```
origin  https://github.com/doxender/WindowsComTekAtomicClock.git
```

## License

[MIT](LICENSE) © 2026 Daniel V. Oxender.
