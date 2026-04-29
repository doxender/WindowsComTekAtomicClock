// ComTekAtomicClock.Shared.Settings
//
// Data model for the per-user UI settings file (settings.json) and the
// per-machine Service config file (service.json), both under
// requirements.txt § 1.8 and § 2.10. Pure POCOs with System.Text.Json
// attributes; no I/O. The reader/writer lives in SettingsStore.
//
// Forward-compatibility: every record carries a [JsonExtensionData]
// dictionary that captures unknown fields on read and round-trips them
// on write, so a settings.json written by a newer app version does not
// lose data when read by an older version.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComTekAtomicClock.Shared.Settings;

// ---------------------------------------------------------------------
// Enums shared by both files
// ---------------------------------------------------------------------

/// <summary>
/// All face themes shipped in v1. Stored as the JSON string form of
/// the enum name (camelCase) via JsonStringEnumConverter — never the
/// integer ordinal — so reordering this enum in source does not break
/// existing on-disk settings.
/// </summary>
public enum Theme
{
    AtomicLab,
    BoulderSlate,
    AeroGlass,
    Cathode,
    Concourse,
    Daylight,
    FlipClock,
    Marquee,
    Slab,
    Binary,
    Hex,
    BinaryDigital,
}

/// <summary>
/// Time format selector per requirements.txt § 1.1.
/// </summary>
public enum TimeFormatMode
{
    /// <summary>Follow the Windows locale's short-time format.</summary>
    Auto,
    /// <summary>Force 12-hour rendering (with AM/PM).</summary>
    TwelveHour,
    /// <summary>Force 24-hour rendering (with the analog numeral flip past noon).</summary>
    TwentyFourHour,
}

/// <summary>
/// Per-tab/per-window override of the analog second-hand cadence.
/// Per requirements.txt § 1.1.
/// </summary>
public enum SecondHandMotion
{
    /// <summary>Use the active theme's default (smooth or stepped).</summary>
    ThemeDefault,
    /// <summary>Force sub-second sweep regardless of the theme's default.</summary>
    Smooth,
    /// <summary>Force once-per-second tick regardless of the theme's default.</summary>
    Stepped,
}

// ---------------------------------------------------------------------
// User-overridable color slots (per requirements.txt § 1.1, § 1.2)
// ---------------------------------------------------------------------

/// <summary>
/// The five user-overridable color slots that every theme exposes:
/// Ring, Face, Hands, Numbers, Digital. Each is a hex color string
/// (`#RRGGBB` or `#RRGGBBAA`) or null if the user has not overridden
/// that slot — in which case the theme's default is used.
/// </summary>
public sealed class ColorOverrides
{
    public string? Ring    { get; set; }
    public string? Face    { get; set; }
    public string? Hands   { get; set; }
    public string? Numbers { get; set; }
    public string? Digital { get; set; }

    /// <summary>True if every slot is null — i.e., theme defaults everywhere.</summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Ring is null && Face is null && Hands is null &&
        Numbers is null && Digital is null;
}

// ---------------------------------------------------------------------
// Per-tab settings
// ---------------------------------------------------------------------

/// <summary>
/// State of a single tab in the main window's tabbed view.
/// Per requirements.txt § 1.2 + § 1.8.
/// </summary>
public class TabSettings
{
    /// <summary>Stable GUID; survives renames and reorders.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Optional user-set tab label. If null, derived from TimeZoneId.</summary>
    public string? Label { get; set; }

    /// <summary>IANA time-zone ID, e.g. `America/New_York`.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Active theme on this tab.</summary>
    public Theme Theme { get; set; } = Theme.AtomicLab;

    /// <summary>Show the integrated digital readout on analog themes? (Ignored for non-analog.)</summary>
    public bool ShowDigitalReadout { get; set; } = true;

    /// <summary>12h / 24h / Auto.</summary>
    public TimeFormatMode TimeFormat { get; set; } = TimeFormatMode.Auto;

    /// <summary>Override of the second-hand motion cadence; Theme default by default.</summary>
    public SecondHandMotion SecondHandMotionOverride { get; set; } = SecondHandMotion.ThemeDefault;

    /// <summary>Per-tab color overrides; null entries fall back to theme defaults.</summary>
    public ColorOverrides Colors { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

// ---------------------------------------------------------------------
// Per-window settings (free-floating clock windows from § 1.3)
// ---------------------------------------------------------------------

/// <summary>
/// State of a free-floating clock window spawned outside the main
/// tabbed view. Inherits everything a tab has plus geometry and
/// always-on-top / overlay behavior. Per requirements.txt § 1.3 + § 1.8.
/// </summary>
public sealed class WindowSettings : TabSettings
{
    public double X      { get; set; }
    public double Y      { get; set; }
    public double Width  { get; set; } = 320;
    public double Height { get; set; } = 320;

    /// <summary>Pinned above all other windows.</summary>
    public bool AlwaysOnTop { get; set; }

    /// <summary>Desktop overlay mode: borderless, transparent, pinned to desktop layer (§ 1.4).</summary>
    public bool OverlayMode { get; set; }
}

// ---------------------------------------------------------------------
// Global app settings
// ---------------------------------------------------------------------

/// <summary>
/// Application-wide settings that don't apply per-tab/per-window.
/// Per requirements.txt § 1.8.
/// </summary>
public sealed class GlobalSettings
{
    public string SyncServer { get; set; } = "time.nist.gov";

    /// <summary>Sync interval; default 1 hour, range 15 min – 24 h (validated at write).</summary>
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>If true, large-offset corrections require user confirmation via toast (§ 2.5).</summary>
    public bool ConfirmLargeSyncCorrections { get; set; } = false;

    /// <summary>Threshold (seconds) above which a correction is "large" — default 5.</summary>
    public int LargeOffsetThresholdSeconds { get; set; } = 5;

    /// <summary>Auto-start the UI when the user signs in.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>True after the §1.9 install flow has succeeded at least once.</summary>
    public bool ServiceInstalled { get; set; } = false;

    /// <summary>When ON, theme + color changes on any tab propagate to all tabs.</summary>
    public bool UseSameThemeAcrossAllTabs { get; set; } = false;

    /// <summary>Default theme for newly-created tabs / windows.</summary>
    public Theme DefaultTheme { get; set; } = Theme.AtomicLab;

    /// <summary>Per § 2.11 — opt-in.</summary>
    public bool SendAnonymousCrashReports { get; set; } = false;

    /// <summary>Per § 2.11 — opt-in.</summary>
    public bool SendAnonymousUsageStats { get; set; } = false;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

// ---------------------------------------------------------------------
// Top-level settings.json shape (per-user)
// ---------------------------------------------------------------------

/// <summary>
/// Top-level shape of <c>%APPDATA%\ComTekAtomicClock\settings.json</c>
/// per requirements.txt § 2.10. Versioned via SchemaVersion.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Schema version. Bumped whenever the JSON shape changes incompatibly.</summary>
    public int SchemaVersion { get; set; } = 1;

    public GlobalSettings Global { get; set; } = new();

    /// <summary>Tabs in display order. Empty list means "no tabs" (won't happen in practice).</summary>
    public List<TabSettings> Tabs { get; set; } = new();

    /// <summary>Free-floating clock windows currently spawned (§ 1.3).</summary>
    public List<WindowSettings> Windows { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}

// ---------------------------------------------------------------------
// Per-machine Service config shape (service.json)
// ---------------------------------------------------------------------

/// <summary>
/// Top-level shape of <c>%ProgramData%\ComTekAtomicClock\service.json</c>
/// per requirements.txt § 2.10. Written by the UI, read by the Service.
/// Until the UI writes it the first time, the Service uses the hardcoded
/// defaults baked into this class.
/// </summary>
public sealed class ServiceConfig
{
    public int SchemaVersion { get; set; } = 1;

    public string SyncServer { get; set; } = "time.nist.gov";
    public TimeSpan SyncInterval { get; set; } = TimeSpan.FromHours(1);
    public bool ConfirmLargeSyncCorrections { get; set; } = false;
    public int LargeOffsetThresholdSeconds { get; set; } = 5;
    public string EventLogSource { get; set; } = "ComTekAtomicClock";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; set; }
}
