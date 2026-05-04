; ComTek Atomic Clock — Inno Setup script
;
; Builds a single-file Windows installer (Setup.exe) that bundles the
; v1.0.0 self-contained build (UI + Service + Installer subfolders),
; runs the bundled ServiceInstaller.exe to register the Windows Service
; with SCM, creates Start Menu / optional Desktop shortcuts, and
; registers an Add/Remove Programs entry that cleanly uninstalls the
; service before removing the install directory.
;
; Build:
;   "C:\Users\<you>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" tools\installer.iss
;
; Output:
;   release\ComTekAtomicClock-v1.0.0-Setup.exe
;
; Source layout (must exist before running ISCC — produced by the
; self-contained dotnet publish, see release\v1.0.0\):
;   release\v1.0.0\
;     UI\          ComTekAtomicClock.UI.exe + 268 dependency files
;     Service\     ComTekAtomicClock.Service.exe + 221 dependency files
;     Installer\   ComTekAtomicClock.ServiceInstaller.exe + 191 dependency files
;     LICENSE
;     CHANGELOG.md
;     README.md
;     SPEC.md
;
; Friday/weekend rule: building this artifact is fine. Distributing
; (signing, uploading, releasing) waits till Monday.

#define MyAppName        "ComTek Atomic Clock"
#define MyAppVersion     "1.1.4"
#define MyAppPublisher   "Daniel V. Oxender"
#define MyAppURL         "https://github.com/doxender/WindowsComTekAtomicClock"
#define MyAppExeName     "ComTekAtomicClock.UI.exe"
#define MyAppId          "{{8E0F7A12-BFB3-4FE8-B9A5-48FD50A15A9A}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases

; Default install directory: %ProgramFiles%\ComTekAtomicClock
DefaultDirName={autopf}\ComTekAtomicClock
DefaultGroupName={#MyAppName}

; Architecture: x64 only (matches the dotnet publish -r win-x64 build)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges: install requires admin (writes to Program Files,
; registers a Windows Service via the bundled installer helper).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=

; UX: license + ready/install/finished pages. Skip the directory
; picker by default since most users won't override Program Files.
LicenseFile=..\release\v{#MyAppVersion}\LICENSE
DisableDirPage=auto
DisableProgramGroupPage=yes

; Compression — LZMA2 max gives ~134 MB output for the ~315 MB input,
; matching the standalone zip we already build.
Compression=lzma2/max
SolidCompression=yes

; Output: the installer lands in release\ alongside the zip.
OutputDir=..\release
OutputBaseFilename=ComTekAtomicClock-v{#MyAppVersion}-Setup
SetupIconFile=..\src\ComTekAtomicClock.UI\Assets\AppIcon.ico

; UI niceties.
WizardStyle=modern
ShowLanguageDialog=no

; Uninstall metadata.
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UninstallDisplayIcon={app}\UI\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; The three publish folders, recursive. Source paths are relative to
; the .iss file itself (this script lives in tools\, so ..\release\...).
Source: "..\release\v{#MyAppVersion}\UI\*";        DestDir: "{app}\UI";        Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\release\v{#MyAppVersion}\Service\*";   DestDir: "{app}\Service";   Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\release\v{#MyAppVersion}\Installer\*"; DestDir: "{app}\Installer"; Flags: ignoreversion recursesubdirs createallsubdirs

; Top-level docs the user might want to read.
Source: "..\release\v{#MyAppVersion}\LICENSE";      DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\v{#MyAppVersion}\README.md";    DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\v{#MyAppVersion}\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\release\v{#MyAppVersion}\SPEC.md";      DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu shortcut for the UI (always created).
Name: "{group}\{#MyAppName}"; Filename: "{app}\UI\{#MyAppExeName}"; WorkingDir: "{app}\UI"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; Optional desktop icon, gated by the [Tasks] checkbox.
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\UI\{#MyAppExeName}"; WorkingDir: "{app}\UI"; Tasks: desktopicon

[Run]
; Post-install: register the Windows Service with SCM by invoking the
; bundled ServiceInstaller.exe. The Inno Setup elevation context already
; covers the admin operations sc.exe needs, so no second UAC prompt.
;
; --service-exe points at the freshly-installed Service binary so the
; SCM record gets the right binPath. Failure here is non-fatal — the
; UI still runs, the user just sees the "Install / start the time-sync
; service" banner on first launch.
Filename: "{app}\Installer\ComTekAtomicClock.ServiceInstaller.exe"; \
    Parameters: "install --service-exe ""{app}\Service\ComTekAtomicClock.Service.exe"""; \
    StatusMsg: "Registering the time-sync Windows Service..."; \
    Flags: runhidden

; Optional final-page launch.
Filename: "{app}\UI\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Pre-uninstall: stop + remove the Windows Service before the file
; deletion phase tries to remove its binaries (otherwise SCM has the
; .exe locked and the file rm fails). Use the bundled installer helper's
; uninstall action which handles the SCM teardown gracefully.
;
; runhidden keeps the consolewindow from flashing. The runascurrentuser
; flag is unnecessary — Inno Setup uninstall already runs with the
; privileges that did the install (admin in our case).
Filename: "{app}\Installer\ComTekAtomicClock.ServiceInstaller.exe"; \
    Parameters: "uninstall"; \
    Flags: runhidden

[UninstallDelete]
; Inno Setup deletes [Files]-installed content automatically. These
; two cover state the app may have written that's outside the install
; tree — %ProgramData% is per-machine and the ServiceInstaller's
; uninstall action above already removes it, but having Inno also
; aware of it (in case the helper failed) gives a clean fallback.
Type: filesandordirs; Name: "{commonappdata}\ComTekAtomicClock"

; NOTE: %APPDATA%\ComTekAtomicClock\settings.json is NOT removed by
; the standard uninstall — per-user settings survive uninstall by
; design (matches the ServiceInstaller's behavior, which only removes
; per-user data when --purge-user-data is passed). A user who wants
; a fully clean uninstall can run:
;   ComTekAtomicClock.ServiceInstaller.exe uninstall --purge-user-data
; from a checkout BEFORE running this Inno-generated uninstaller.
