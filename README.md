# ComTek Atomic Clock — Windows

A Windows desktop clock that synchronizes to the NIST atomic clock in Boulder, CO and displays multiple time zones with both digital and analog faces.

## Features

- Digital and analog clock faces (user-selectable per window)
- Tabbed multi-time-zone view in the main app
- Multiple independent clock windows, each pinned to a different location/time zone
- Optional always-on-desktop overlay (transparent, click-through window)
- Background Windows Service that periodically syncs the system clock to NIST Boulder via SNTP
- System tray integration

## Architecture

Three-project Visual Studio solution targeting **.NET 8** on Windows 10/11. Built with Visual Studio 2026 (Visual Studio 2022 also works — the project files are forward/backward compatible).

| Project | Type | Runs as | Purpose |
|---|---|---|---|
| `ComTekAtomicClock.UI` | WPF App | Current user | Tray icon, tabbed UI, clock windows, desktop overlay |
| `ComTekAtomicClock.Service` | Worker Service | LocalSystem | SNTP query to `time.nist.gov`, calls `SetSystemTime`, schedules periodic sync |
| `ComTekAtomicClock.Shared` | Class Library | n/a | NTP packet, IPC contracts, shared models |

The UI and Service communicate over a named pipe. The Service is the only component with rights to change the system clock.

## Prerequisites

- Windows 10 (1809+) or Windows 11
- [Visual Studio 2026](https://visualstudio.microsoft.com/) (or 2022) — Community edition is fine. Install the **.NET desktop development** workload.
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git

## Build & run

```pwsh
# from this directory, after the solution is scaffolded
dotnet restore
dotnet build -c Debug

# run the UI (foreground)
dotnet run --project ComTekAtomicClock.UI

# install the service (admin shell required)
sc.exe create ComTekAtomicClockSvc binPath= "<full path>\ComTekAtomicClock.Service.exe" start= auto
sc.exe start ComTekAtomicClockSvc
```

## NIST / NTP usage notes

- Default sync server: `time.nist.gov` (anycast to the Boulder cluster).
- NIST asks SNTP clients to poll **no more than once every 4 seconds** per server. The service defaults to **hourly** sync, which is well within the published guidance.
- All NIST time servers are publicly available; no API key required. See <https://tf.nist.gov/tf-cgi/servers.cgi>.

## Repository layout

```
ComTekAtomicClock/
  windows/                       <-- this repo (remote: WindowsComTekAtomicClock)
    README.md
    requirements.txt             functional & non-functional requirements
    .gitignore
    src/                         (added when solution is scaffolded)
```

The parent `ComTekAtomicClock/` folder is reserved as an umbrella for future per-platform repos (e.g. `mac/`, `linux/`).

## Git remote

The remote repository will be named **`WindowsComTekAtomicClock`**. To attach once it exists:

```pwsh
git remote add origin https://github.com/<owner>/WindowsComTekAtomicClock.git
git push -u origin master
```

## License

[MIT](LICENSE) © 2026 Daniel V. Oxender.
