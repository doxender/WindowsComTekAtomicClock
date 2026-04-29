// ComTekAtomicClock.UI.Services.TimeZoneCatalog
//
// Builds a sortable, deduplicated list of IANA time zones for the
// settings popover dropdown (per requirements.txt § 1.2 / § 1.8).
// .NET 8 on Windows uses Windows zone IDs by default in the registry;
// each is mapped to its primary IANA equivalent via
// TimeZoneInfo.TryConvertWindowsIdToIanaId. Result is sorted by UTC
// offset, then by display name.
//
// Step 6 ships a one-shot list (~140 entries — every Windows zone
// plus a few additions). Type-to-search filtering is implemented at
// the ComboBox level. The full ~600-entry IANA catalog can be added
// in a follow-up commit if/when users want more granular zones.

namespace ComTekAtomicClock.UI.Services;

/// <summary>
/// One row in the time-zone dropdown.
/// </summary>
public sealed record TimeZoneOption(string IanaId, string DisplayName, TimeSpan Offset)
{
    /// <summary>
    /// Sort key: UTC offset (minutes) ascending, then DisplayName.
    /// </summary>
    public int OffsetMinutes => (int)Offset.TotalMinutes;
}

public static class TimeZoneCatalog
{
    private static readonly Lazy<IReadOnlyList<TimeZoneOption>> _options =
        new(BuildOptions);

    /// <summary>Sorted, dedup-by-IANA list of time zones.</summary>
    public static IReadOnlyList<TimeZoneOption> All => _options.Value;

    private static IReadOnlyList<TimeZoneOption> BuildOptions()
    {
        var byIana = new Dictionary<string, TimeZoneOption>(StringComparer.OrdinalIgnoreCase);

        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            string ianaId;
            if (tz.HasIanaId)
            {
                ianaId = tz.Id;
            }
            else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var converted))
            {
                ianaId = converted;
            }
            else
            {
                continue;
            }

            if (byIana.ContainsKey(ianaId)) continue;

            var offset = tz.GetUtcOffset(DateTime.UtcNow);
            var sign = offset.Ticks >= 0 ? "+" : "-";
            var abs = offset.Duration();
            var offsetText = $"UTC{sign}{abs.Hours:D2}:{abs.Minutes:D2}";

            // Display name: "(UTC-05:00) America/New_York — Eastern Standard Time"
            // The trailing Windows display name is informative but truncated if very long.
            var winDescr = string.IsNullOrEmpty(tz.DisplayName)
                ? string.Empty
                : $"  ·  {tz.StandardName}";
            var display = $"({offsetText})  {ianaId}{winDescr}";

            byIana[ianaId] = new TimeZoneOption(ianaId, display, offset);
        }

        return byIana.Values
            .OrderBy(o => o.OffsetMinutes)
            .ThenBy(o => o.IanaId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
