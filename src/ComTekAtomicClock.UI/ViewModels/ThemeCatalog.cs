// ComTekAtomicClock.UI.ViewModels.ThemeCatalog / ThemePreview
//
// Static metadata for the Themes gallery dialog (the "?" -> Themes…
// menu item on each tab's clock face). Each entry pairs a Theme enum
// value with a friendly display name and a pack-URI pointing at the
// canonical SVG mock-up under design/themes/ (linked into the assembly
// via Resource entries in ComTekAtomicClock.UI.csproj).
//
// The display names and ordering match design/themes/index.html so
// the in-app gallery matches the reference page we review against.
// Descriptions are intentionally short — the gallery shows name + image
// only, nothing else.

using ComTekAtomicClock.Shared.Settings;

namespace ComTekAtomicClock.UI.ViewModels;

/// <summary>
/// One entry in the Themes gallery: a <see cref="Shared.Settings.Theme"/>
/// enum value plus its friendly name and the pack-URI of its design
/// SVG. Used as the data context for each tile in
/// <see cref="Dialogs.ThemesDialog"/>.
/// </summary>
public sealed record ThemePreview(Theme Theme, string DisplayName, Uri SvgUri);

public static class ThemeCatalog
{
    private static Uri Pack(string fileName) =>
        new($"pack://application:,,,/Assets/themes/{fileName}");

    /// <summary>
    /// All 13 themes (v1.1.0+) in the order they appear in
    /// design/themes/index.html: 7 analog (Atomic Lab through CaptJohn),
    /// 4 digital-only (Flip Clock, Marquee, Slab, Binary Digital), 2
    /// specialty encoders (Binary, Hex). CaptJohn slots in as Theme #7
    /// of the analog cluster, ahead of the digital-only group.
    /// </summary>
    public static IReadOnlyList<ThemePreview> All { get; } = new[]
    {
        new ThemePreview(Theme.AtomicLab,     "Atomic Lab",          Pack("atomic-lab.svg")),
        new ThemePreview(Theme.BoulderSlate,  "Boulder Slate",       Pack("boulder-slate.svg")),
        new ThemePreview(Theme.AeroGlass,     "Aero Glass",          Pack("aero-glass.svg")),
        new ThemePreview(Theme.Cathode,       "Cathode",             Pack("cathode.svg")),
        new ThemePreview(Theme.Concourse,     "Concourse",           Pack("concourse.svg")),
        new ThemePreview(Theme.Daylight,      "Daylight",            Pack("daylight.svg")),
        // v1.1.0: Captain John's Marina — uses the live mockup PNG as
        // its gallery preview (no SVG hand-illustrated yet; design lives
        // in the rendered mockups under design/themes/captjohn-mockup-*.png).
        new ThemePreview(Theme.CaptJohn,      "Captain John’s", new Uri("pack://application:,,,/Assets/themes/captjohn-mockup.png")),
        new ThemePreview(Theme.FlipClock,     "Flip Clock",          Pack("flip-clock.svg")),
        new ThemePreview(Theme.Marquee,       "Marquee",             Pack("marquee.svg")),
        new ThemePreview(Theme.Slab,          "Slab",                Pack("slab.svg")),
        new ThemePreview(Theme.Binary,        "Binary",              Pack("binary.svg")),
        new ThemePreview(Theme.Hex,           "Hex",                 Pack("hex.svg")),
        new ThemePreview(Theme.BinaryDigital, "Binary Digital",      Pack("binary-digital.svg")),
    };
}
