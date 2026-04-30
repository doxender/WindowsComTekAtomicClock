// ComTekAtomicClock.UI.ViewModels.TabViewModel
//
// Wraps a single TabSettings record for binding to the TabControl
// in MainWindow + the per-tab settings popover. Two-way bindings on
// TimeZone, Theme, TimeFormat, SecondHandMotion, ShowDigitalReadout
// flow back to the underlying TabSettings; MainWindowViewModel
// listens on PropertyChanged to know when to persist.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ComTekAtomicClock.Shared.Settings;

namespace ComTekAtomicClock.UI.ViewModels;

public sealed class TabViewModel : INotifyPropertyChanged
{
    private readonly TabSettings _settings;

    public TabViewModel(TabSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _resolvedTimeZone = ResolveTimeZone(settings.TimeZoneId);
    }

    /// <summary>Stable per-tab GUID for settings persistence.</summary>
    public string Id => _settings.Id;

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
            // Note: NO OnPropertyChanged(nameof(Label)) here.
            // Per Dan's two-event rule (v0.0.32) the tab name is
            // set imperatively by MainWindow.SetTabHeaderInAllDisplays
            // when the Settings dialog closes — NOT via a
            // PropertyChanged cascade off TimeZoneId, which Dragablz
            // proved unreliable about honoring (v0.0.21..v0.0.31).
            // Label's getter still computes the right value when
            // read, so the {Binding Label} on freshly-created
            // ItemTemplate TextBlocks (Load case) reads correctly.
        }
    }

    private TimeZoneInfo _resolvedTimeZone;

    /// <summary>Resolved TimeZoneInfo for ClockFaceControl binding.</summary>
    public TimeZoneInfo TimeZone => _resolvedTimeZone;

    public Theme Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme == value) return;
            _settings.Theme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OverlayGlyphBrush));
        }
    }

    /// <summary>
    /// Foreground brush for the per-tab "✕" close-button and "?" help-button
    /// glyphs that sit in the upper-right corner of the clock-face area.
    /// White on dark themes, near-black on light themes — earlier the glyphs
    /// were hardcoded #000 and disappeared on Cathode / Atomic Lab / Aero
    /// Glass / Concourse / etc. (anything with a dark backdrop). Keyed off
    /// Theme so the brush re-evaluates whenever the user switches faces.
    /// </summary>
    public Brush OverlayGlyphBrush =>
        IsDarkTheme(Theme) ? Brushes.White : _darkGlyph;

    private static readonly Brush _darkGlyph =
        new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10));

    /// <summary>
    /// Per-theme background-luminance classification for the glyph-color
    /// decision. Reasoning per theme is in design/README.md (palette
    /// section); update both when adding new themes.
    /// </summary>
    private static bool IsDarkTheme(Theme t) => t switch
    {
        Theme.AtomicLab     => true,   // dark navy face
        Theme.BoulderSlate  => false,  // page cream + white face
        Theme.AeroGlass     => true,   // wallpaper-blue acrylic
        Theme.Cathode       => true,   // CRT black
        Theme.Concourse     => true,   // charcoal
        Theme.Daylight      => false,  // cream/white
        Theme.FlipClock     => false,  // white tile cards
        Theme.Marquee       => true,   // theater red+black
        Theme.Slab          => true,   // brutalist concrete dark
        Theme.Binary        => true,   // BCD board dark
        Theme.Hex           => true,   // terminal dark
        Theme.BinaryDigital => true,   // pure black terminal
        _                   => true,
    };

    public TimeFormatMode TimeFormat
    {
        get => _settings.TimeFormat;
        set
        {
            if (_settings.TimeFormat == value) return;
            _settings.TimeFormat = value;
            OnPropertyChanged();
        }
    }

    public SecondHandMotion SecondHandMotionOverride
    {
        get => _settings.SecondHandMotionOverride;
        set
        {
            if (_settings.SecondHandMotionOverride == value) return;
            _settings.SecondHandMotionOverride = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SmoothSecondHand));
        }
    }

    /// <summary>
    /// Resolved smooth-vs-stepped boolean for ClockFaceControl
    /// binding. Maps the three-way override (ThemeDefault / Smooth /
    /// Stepped) plus the active theme's default to a single bool.
    /// For now Atomic Lab's default is smooth; full per-theme defaults
    /// table is in design/README.md.
    /// </summary>
    public bool SmoothSecondHand
    {
        get
        {
            return SecondHandMotionOverride switch
            {
                SecondHandMotion.Smooth          => true,
                SecondHandMotion.Stepped         => false,
                _                                => ThemeDefaultIsSmooth(Theme),
            };
        }
    }

    public bool ShowDigitalReadout
    {
        get => _settings.ShowDigitalReadout;
        set
        {
            if (_settings.ShowDigitalReadout == value) return;
            _settings.ShowDigitalReadout = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Tab header label. If the user has set an explicit Label, use
    /// that; otherwise derive from the IANA time-zone (use the city
    /// portion of the path, e.g., America/New_York -> New York).
    /// </summary>
    public string Label
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_settings.Label))
                return _settings.Label!;
            return DeriveLabelFromIanaId(_settings.TimeZoneId);
        }
        set
        {
            if (_settings.Label == value) return;
            _settings.Label = string.IsNullOrWhiteSpace(value) ? null : value;
            OnPropertyChanged();
        }
    }

    /// <summary>The underlying TabSettings record (settings.json model).</summary>
    public TabSettings Settings => _settings;

    /// <summary>
    /// Falls back to the tab's <see cref="Label"/> if any view layer ever
    /// renders the view-model directly (notably Dragablz tab headers
    /// through some code paths). Without this override the default
    /// <see cref="object.ToString"/> would print
    /// "ComTekAtomicClock.UI.ViewModels.TabViewModel".
    /// </summary>
    public override string ToString() => Label;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static TimeZoneInfo ResolveTimeZone(string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string DeriveLabelFromIanaId(string ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId)) return "UTC";
        var slash = ianaId.LastIndexOf('/');
        var leaf = slash >= 0 ? ianaId[(slash + 1)..] : ianaId;
        return leaf.Replace('_', ' ');
    }

    private static bool ThemeDefaultIsSmooth(Theme theme) => theme switch
    {
        Theme.AtomicLab     => true,
        Theme.BoulderSlate  => false,
        Theme.AeroGlass     => true,
        Theme.Cathode       => true,
        Theme.Concourse     => true,
        Theme.Daylight      => false,
        // Digital-only / encoding themes don't have an analog second
        // hand to debate; default to smooth for any future analog
        // they might gain.
        _                   => true,
    };
}
