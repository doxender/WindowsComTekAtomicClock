// ComTekAtomicClock.Shared.Settings.SettingsStore
//
// Read/write helpers for settings.json (per-user, %APPDATA%) and
// service.json (per-machine, %ProgramData%) per requirements.txt § 2.10.
//
// Atomic write pattern:
//   1. Serialize to a sibling .tmp file in the destination directory.
//   2. Flush the tmp file to disk (FileStream.Flush(true)).
//   3. File.Move(tmp, dest, overwrite: true).
// This avoids partial-write corruption on crash or power loss; the
// destination file is either the old version or the new version, never
// a half-written mix.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComTekAtomicClock.Shared.Settings;

public static class SettingsStore
{
    /// <summary>JSON options shared by all reads and writes.</summary>
    private static readonly JsonSerializerOptions JsonOpts = BuildJsonOptions();

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            // Ignore null members on write so absent overrides stay
            // absent in the file rather than serializing as `"ring": null`.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        opts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return opts;
    }

    // -----------------------------------------------------------------
    // Path helpers
    // -----------------------------------------------------------------

    private const string AppFolderName = "ComTekAtomicClock";

    /// <summary>
    /// `%APPDATA%\ComTekAtomicClock\settings.json` — per-user UI
    /// settings. Roaming AppData so the file follows a roaming user
    /// profile.
    /// </summary>
    public static string GetUserSettingsPath() =>
        Path.Combine(GetUserSettingsDir(), "settings.json");

    public static string GetUserSettingsDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);

    /// <summary>
    /// `%ProgramData%\ComTekAtomicClock\service.json` — per-machine
    /// Service config. ProgramData so the LocalSystem Service AND any
    /// interactive user's UI can both reach it.
    /// </summary>
    public static string GetServiceConfigPath() =>
        Path.Combine(GetServiceConfigDir(), "service.json");

    public static string GetServiceConfigDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppFolderName);

    // -----------------------------------------------------------------
    // Per-user settings.json
    // -----------------------------------------------------------------

    /// <summary>
    /// Load <see cref="AppSettings"/> from <see cref="GetUserSettingsPath"/>.
    /// If the file is missing or unreadable, returns first-run defaults
    /// (and writes them to disk in the missing case).
    /// </summary>
    public static AppSettings LoadAppSettings()
    {
        var path = GetUserSettingsPath();
        if (!File.Exists(path))
        {
            var defaults = CreateDefaultAppSettings();
            try
            {
                SaveAppSettings(defaults);
            }
            catch
            {
                // Couldn't write defaults; OK — caller still gets the
                // in-memory defaults and the next save attempt will retry.
            }
            return defaults;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<AppSettings>(stream, JsonOpts)
                   ?? CreateDefaultAppSettings();
        }
        catch (JsonException)
        {
            // Corrupt or truncated file. Don't lose user data: rename
            // the broken file with a timestamp suffix and start fresh.
            var bad = path + ".broken-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            try { File.Move(path, bad); } catch { /* best effort */ }
            return CreateDefaultAppSettings();
        }
    }

    /// <summary>
    /// Atomically save <paramref name="settings"/> to
    /// <see cref="GetUserSettingsPath"/>.
    /// </summary>
    public static void SaveAppSettings(AppSettings settings)
    {
        WriteAtomic(GetUserSettingsPath(), settings);
    }

    /// <summary>
    /// Build first-run defaults per requirements.txt § 1.10:
    ///   - one tab bound to the system's local IANA time zone
    ///   - Atomic Lab theme
    ///   - hourly sync to time.nist.gov
    ///   - all opt-in toggles OFF
    /// </summary>
    public static AppSettings CreateDefaultAppSettings()
    {
        var ianaId = ResolveLocalIanaId();

        var settings = new AppSettings
        {
            SchemaVersion = 1,
            Global = new GlobalSettings
            {
                DefaultTheme = Theme.AtomicLab,
                SyncServer = "time.nist.gov",
                SyncInterval = TimeSpan.FromHours(1),
                LargeOffsetThresholdSeconds = 5,
            },
            Tabs = new List<TabSettings>
            {
                new TabSettings
                {
                    TimeZoneId = ianaId,
                    Theme = Theme.AtomicLab,
                    TimeFormat = TimeFormatMode.Auto,
                }
            },
            Windows = new List<WindowSettings>(),
        };
        return settings;
    }

    // -----------------------------------------------------------------
    // Per-machine service.json
    // -----------------------------------------------------------------

    /// <summary>
    /// Load <see cref="ServiceConfig"/> from
    /// <see cref="GetServiceConfigPath"/>. Falls back to hardcoded
    /// defaults (without writing them) when the file is missing — the
    /// Service starts in this state until the UI pushes a config the
    /// first time.
    /// </summary>
    public static ServiceConfig LoadServiceConfig()
    {
        var path = GetServiceConfigPath();
        if (!File.Exists(path))
            return new ServiceConfig();

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<ServiceConfig>(stream, JsonOpts)
                   ?? new ServiceConfig();
        }
        catch (JsonException)
        {
            return new ServiceConfig();
        }
    }

    /// <summary>
    /// Atomically save <paramref name="config"/> to
    /// <see cref="GetServiceConfigPath"/>.
    /// </summary>
    public static void SaveServiceConfig(ServiceConfig config)
    {
        WriteAtomic(GetServiceConfigPath(), config);
    }

    // -----------------------------------------------------------------
    // Implementation: atomic write
    // -----------------------------------------------------------------

    private static void WriteAtomic<T>(string path, T value)
    {
        var dir = Path.GetDirectoryName(path)
                  ?? throw new InvalidOperationException($"No directory in path: {path}");
        Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        using (var stream = File.Create(tmp))
        {
            JsonSerializer.Serialize(stream, value, JsonOpts);
            // Force flush all the way to disk so the rename below is
            // safe across an unexpected reboot.
            stream.Flush(true);
        }
        File.Move(tmp, path, overwrite: true);
    }

    // -----------------------------------------------------------------
    // Implementation: local time-zone IANA resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Return the local time zone as an IANA ID. On Windows, the local
    /// `TimeZoneInfo.Local.Id` is usually a Windows display name like
    /// `Pacific Standard Time`; .NET 8's
    /// <see cref="TimeZoneInfo.TryConvertWindowsIdToIanaId(string, out string)"/>
    /// gives us the canonical IANA equivalent.
    /// </summary>
    private static string ResolveLocalIanaId()
    {
        var local = TimeZoneInfo.Local;
        if (local.HasIanaId)
            return local.Id;

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana))
            return iana;

        return "UTC";
    }
}
