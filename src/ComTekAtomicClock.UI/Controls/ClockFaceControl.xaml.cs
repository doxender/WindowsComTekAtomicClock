// ComTekAtomicClock.UI.Controls.ClockFaceControl
//
// Theme-pluggable clock face. The XAML is just an empty Canvas inside
// a Viewbox; all visual content for each theme is built here in
// code-behind by Build<ThemeName>() routines. UpdateClock() reads the
// stored hand transforms and text references to advance the hands and
// the digital readout on every DispatcherTimer tick.
//
// Adding a new theme:
//   1. Add a Build<NewTheme>() method that paints the dial canvas
//      and stores _hourRotate / _minuteRotate / _secondRotate /
//      _digitalReadout / _dateReadout (as appropriate for the theme).
//   2. Wire it into RenderActiveTheme() in the switch on Theme.
//   3. Set the Smooth-vs-Stepped second hand default in the
//      ThemeDefaultIsSmooth() helper in TabViewModel — already
//      authored for all 12 themes per design/README.md.
//
// User color overrides (Ring/Face/Hands/Numbers/Digital from
// requirements.txt § 1.1) will be applied on top of theme defaults
// via ColorOverrides DPs in a follow-up commit. For now each theme
// uses its hardcoded palette.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SettingsTheme = ComTekAtomicClock.Shared.Settings.Theme;

namespace ComTekAtomicClock.UI.Controls;

public partial class ClockFaceControl : UserControl
{
    // ---------------------------------------------------------------
    // DependencyProperties
    // ---------------------------------------------------------------

    public static readonly DependencyProperty TimeZoneProperty =
        DependencyProperty.Register(
            nameof(TimeZone),
            typeof(TimeZoneInfo),
            typeof(ClockFaceControl),
            new PropertyMetadata(TimeZoneInfo.Local));

    public TimeZoneInfo TimeZone
    {
        get => (TimeZoneInfo)GetValue(TimeZoneProperty);
        set => SetValue(TimeZoneProperty, value);
    }

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme),
            typeof(SettingsTheme),
            typeof(ClockFaceControl),
            new PropertyMetadata(SettingsTheme.AtomicLab, OnThemeChanged));

    public SettingsTheme Theme
    {
        get => (SettingsTheme)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Defensive: re-render whenever Theme changes, regardless of
        // whether the control is currently in the loaded state. Earlier
        // this gated on IsLoaded — but if the binding fires the Theme
        // change between DataContext-set and Loaded (a small window
        // during DataTemplate instantiation, plausibly hit by tab
        // recycling in TabablzControl), the change was silently
        // dropped and only the next OnLoaded picked it up. That fits
        // our "Tab 2 shows Flip Clock but Binary Digital selected"
        // symptom: tab is in a state where the DP says BinaryDigital
        // but the visuals are still from a stale render. Removing
        // the IsLoaded guard means the first render may run before
        // layout completes — the Canvas children are positioned by
        // explicit Width/Height + Canvas.SetLeft/SetTop, so layout
        // pass is unnecessary for correctness.
        System.Diagnostics.Trace.WriteLine(
            $"[ClockFaceControl] OnThemeChanged: {e.OldValue} → {e.NewValue}");
        if (d is ClockFaceControl c)
            c.RenderActiveTheme();
    }

    public static readonly DependencyProperty SmoothSecondHandProperty =
        DependencyProperty.Register(
            nameof(SmoothSecondHand),
            typeof(bool),
            typeof(ClockFaceControl),
            new PropertyMetadata(true, OnSmoothSecondHandChanged));

    public bool SmoothSecondHand
    {
        get => (bool)GetValue(SmoothSecondHandProperty);
        set => SetValue(SmoothSecondHandProperty, value);
    }

    private static void OnSmoothSecondHandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockFaceControl c && c._timer is not null)
        {
            c._timer.Interval = (bool)e.NewValue
                ? TimeSpan.FromMilliseconds(50)
                : TimeSpan.FromSeconds(1);
        }
    }

    // ---------------------------------------------------------------
    // Per-theme rendering state
    // ---------------------------------------------------------------

    private RotateTransform? _hourRotate;
    private RotateTransform? _minuteRotate;
    private RotateTransform? _secondRotate;
    private TextBlock? _digitalReadout;
    private TextBlock? _dateReadout;

    /// <summary>
    /// True when <see cref="_dateReadout"/> is plain text on the dial
    /// (no enclosing panel) and we need to re-measure + re-center it
    /// after each <see cref="UpdateClock"/> tick. Boulder Slate and
    /// Daylight set this — they place the date as bare text below the
    /// center pin and the displayed-text width varies day-to-day
    /// (e.g., "MAY 1" vs "DECEMBER 28"), so a one-shot center at
    /// build time leaves it offset whenever the new text is wider or
    /// narrower than the build-time placeholder. Themes that wrap
    /// the date in a Border / panel with HorizontalAlignment="Center"
    /// don't need this flag — the panel auto-centers its content.
    /// </summary>
    private bool _recenterDateReadoutOnUpdate;

    /// <summary>
    /// Per-theme update hook for digital renderers (Flip Clock, Marquee,
    /// Slab, Binary, Hex, Binary Digital) whose visuals don't map onto
    /// the analog hour/minute/second-hand rotation pattern. Each Build*
    /// for those themes assigns a closure here that mutates whatever
    /// elements that theme uses (text blocks, LED ellipses, color
    /// rectangles, etc.). UpdateClock invokes it once per tick after
    /// the analog rotates have been updated. Set back to null when
    /// switching themes (in RenderActiveTheme).
    /// </summary>
    private Action<DateTime>? _digitalUpdater;

    /// <summary>
    /// Theme value that was last fully rendered. Used by the
    /// self-healing tick check in <see cref="UpdateClock"/>: if the
    /// current <see cref="Theme"/> DP value disagrees with this, the
    /// painted visuals are stale and we re-render. Initialized to a
    /// sentinel that no real theme equals so the first tick after
    /// <see cref="OnLoaded"/> always reconciles.
    ///
    /// Exists because the v0.0.9 era reproduced a case where
    /// <c>tabVm.Theme</c> said "BinaryDigital" but the dial was painted
    /// with Flip Clock visuals. Root cause was never isolated to one
    /// of the candidate races (DataContext swap during tab recycling,
    /// silently-thrown render exception under the pre-v0.0.16 bare
    /// code path, late-binding update missing OnPropertyChanged).
    /// Rather than chase the specific race, this self-healing check
    /// makes the symptom impossible: any frame in which the DP and
    /// the render disagree triggers a fresh render on the next tick.
    /// </summary>
    private SettingsTheme? _lastRenderedTheme;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    private DispatcherTimer? _timer;
    private const double Cx = 200.0;
    private const double Cy = 200.0;

    public ClockFaceControl()
    {
        InitializeComponent();
        Loaded            += OnLoaded;
        Unloaded          += OnUnloaded;
        IsVisibleChanged  += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // ApplicationIdle priority is one tier below Background and
        // two tiers below Input. The dispatcher fires this timer ONLY
        // when no Input, Loaded, Render, DataBind, Normal, or
        // Background work is pending — i.e., when the UI thread is
        // genuinely idle. Means click events on the tab strip always
        // pre-empt clock-face redraws; the user no longer needs 5-10
        // clicks to switch tabs because a tick is in progress.
        //
        // Trade-off: under heavy UI activity, ticks can be skipped.
        // For a clock this is benign — UpdateClock reads
        // DateTime.UtcNow on every fire, so a missed tick just means
        // the next visible frame jumps ahead a few ms. No drift, no
        // integration error. (A simulation/game loop would be
        // different; for our case skipping is the correct answer.)
        //
        // Default DispatcherTimer priority is Background, which is
        // one tier ABOVE ApplicationIdle but still below Input. We
        // were getting click drops there because by the time a tick
        // fired (Background), it ran to completion before any Input
        // queued during that tick could process. ApplicationIdle
        // closes that gap — input always wins.
        _timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle)
        {
            Interval = SmoothSecondHand
                ? TimeSpan.FromMilliseconds(50)
                : TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => UpdateClock();

        RenderActiveTheme();
        UpdateClock();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
    }

    /// <summary>
    /// Pause the per-clock animation timer when this tab isn't on
    /// screen (Dragablz's TabablzControl keeps non-selected tabs
    /// loaded but visually collapsed, so OnUnloaded does not fire on
    /// tab switch). With one timer per tab firing at 20 Hz, four
    /// open tabs produce 80 dispatcher ticks per second — enough
    /// background work to starve mouse-click events on the tab strip
    /// (Dan reported needing 5–10 clicks to switch tabs reliably).
    /// Pausing the inactive tabs' timers cuts the load to one
    /// timer's worth and restores first-click responsiveness.
    ///
    /// On becoming visible again, force an immediate <see cref="UpdateClock"/>
    /// so the freshly-shown frame is already correct (otherwise the
    /// user would briefly see stale time / a stale theme until the
    /// next 50 ms tick — and the self-heal added in v0.0.17 only
    /// fires inside UpdateClock).
    /// </summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_timer is null) return;
        if ((bool)e.NewValue)
        {
            UpdateClock();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void RenderActiveTheme()
    {
        var requested = Theme;
        System.Diagnostics.Trace.WriteLine(
            $"[ClockFaceControl] RenderActiveTheme begin: {requested}");

        Dial.Children.Clear();
        _hourRotate = _minuteRotate = _secondRotate = null;
        _digitalReadout = _dateReadout = null;
        _digitalUpdater = null;
        _recenterDateReadoutOnUpdate = false;

        // Stamp the field BEFORE the build, so a re-entrant call (e.g.,
        // theme changes again mid-build via a binding ripple) is
        // detected against the up-to-date value rather than the prior
        // one. If Build* throws, _lastRenderedTheme still reflects what
        // we *attempted* to render — preventing the self-heal in
        // UpdateClock from infinitely retrying a doomed theme. Use the
        // captured `requested` so the switch and the recorded value
        // can't disagree if Theme changes again mid-method.
        _lastRenderedTheme = requested;

        switch (requested)
        {
            case SettingsTheme.AtomicLab:     BuildAtomicLab();     break;
            case SettingsTheme.BoulderSlate:  BuildBoulderSlate();  break;
            case SettingsTheme.AeroGlass:     BuildAeroGlass();     break;
            case SettingsTheme.Cathode:       BuildCathode();       break;
            case SettingsTheme.Concourse:     BuildConcourse();     break;
            case SettingsTheme.Daylight:      BuildDaylight();      break;
            case SettingsTheme.FlipClock:     BuildFlipClock();     break;
            case SettingsTheme.Marquee:       BuildMarquee();       break;
            case SettingsTheme.Slab:          BuildSlab();          break;
            case SettingsTheme.Binary:        BuildBinary();        break;
            case SettingsTheme.Hex:           BuildHex();           break;
            case SettingsTheme.BinaryDigital: BuildBinaryDigital(); break;
            // No fallback: every Theme enum value has its own renderer
            // now. If a future theme is added without its case here,
            // we'd rather fail visibly (blank dial) than silently
            // render the wrong face.
        }

        AddVersionLabel();

        // The "theme: <name>" debug overlay — TEMPORARILY re-enabled
        // for v0.0.9 to diagnose our "Tab 2 shows Flip Clock but
        // Binary Digital selected" report. With the overlay on, we
        // can see at a glance whether RenderActiveTheme is dispatching
        // to the right Build*. Remove again once that bug is closed.
        AddDebugThemeLabel();
    }

    /// <summary>
    /// Test-only overlay (TODO remove for public release): paints the
    /// currently-selected Theme name in small gray text near the
    /// bottom of the dial canvas. Only called for themes that still
    /// fall back to Atomic Lab visuals (the six unimplemented digital
    /// renderers); analog themes don't get the label since they're
    /// rendering correctly per our verification.
    /// </summary>
    private void AddDebugThemeLabel()
    {
        var name = ThemeFriendlyName(Theme);
        var label = new TextBlock
        {
            Text = $"theme: {name}",
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80)),
            TextAlignment = TextAlignment.Center,
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, Cx - label.DesiredSize.Width / 2.0);
        Canvas.SetTop(label, 388);
        Dial.Children.Add(label);
    }

    /// <summary>
    /// Paints the running assembly version in the upper-left of the
    /// dial canvas. Per our spec ("version should be in the clock
    /// background, upper left"), so any build the user is looking at
    /// is identifiable at a glance. Tied to the project's
    /// &lt;Version&gt; in ComTekAtomicClock.UI.csproj — the standing
    /// version-bump rule keeps these in sync.
    /// </summary>
    private void AddVersionLabel()
    {
        var asm = typeof(ClockFaceControl).Assembly;
        var ver = asm.GetName().Version?.ToString(3) ?? "0.0.0";
        var label = new TextBlock
        {
            Text = $"v{ver}",
            FontFamily = new FontFamily("Consolas, Courier New, monospace"),
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0x80, 0x80, 0x80)),
        };
        Canvas.SetLeft(label, 6);
        Canvas.SetTop(label, 4);
        Dial.Children.Add(label);
    }

    private static string ThemeFriendlyName(SettingsTheme t) => t switch
    {
        SettingsTheme.AtomicLab     => "Atomic Lab",
        SettingsTheme.BoulderSlate  => "Boulder Slate",
        SettingsTheme.AeroGlass     => "Aero Glass",
        SettingsTheme.Cathode       => "Cathode",
        SettingsTheme.Concourse     => "Concourse",
        SettingsTheme.Daylight      => "Daylight",
        SettingsTheme.FlipClock     => "Flip Clock",
        SettingsTheme.Marquee       => "Marquee",
        SettingsTheme.Slab          => "Slab",
        SettingsTheme.Binary        => "Binary",
        SettingsTheme.Hex           => "Hex",
        SettingsTheme.BinaryDigital => "Binary Digital",
        _                           => t.ToString(),
    };

    /// <summary>
    /// Read current time in the bound zone and update hand rotations
    /// + digital + date text. Called on every timer tick (50 ms or 1 s
    /// depending on SmoothSecondHand).
    /// </summary>
    private void UpdateClock()
    {
        // Self-healing theme reconciliation. If the Theme DP value
        // disagrees with what we last actually rendered (any of the
        // candidate races could have caused this — DataContext-swap
        // during container recycling, silently-failed render, or a
        // late binding update we missed), force a fresh render. This
        // makes the visible "wrong theme" symptom impossible: at most
        // one tick after the mismatch occurs, the next tick repaints
        // with the correct theme.
        if (_lastRenderedTheme != Theme)
        {
            System.Diagnostics.Trace.WriteLine(
                $"[ClockFaceControl] Self-heal: rendered={_lastRenderedTheme} != Theme={Theme}; re-rendering.");
            RenderActiveTheme();
            // Fall through to update positions/text against the
            // freshly-built elements.
        }

        var nowUtc = DateTime.UtcNow;
        var local  = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZone);

        var hour   = (local.Hour % 12) + (local.Minute / 60.0) + (local.Second / 3600.0);
        var minute = local.Minute + (local.Second / 60.0) + (local.Millisecond / 60_000.0);
        var second = local.Second + (SmoothSecondHand ? local.Millisecond / 1000.0 : 0);

        if (_hourRotate   is not null) _hourRotate.Angle   = hour   * 30.0;
        if (_minuteRotate is not null) _minuteRotate.Angle = minute *  6.0;
        if (_secondRotate is not null) _secondRotate.Angle = second *  6.0;

        if (_digitalReadout is not null)
            _digitalReadout.Text = local.ToString("h:mm:ss tt", CultureInfo.InvariantCulture);

        if (_dateReadout is not null)
        {
            _dateReadout.Text = local
                .ToString("ddd · MMMM d · yyyy", CultureInfo.InvariantCulture)
                .ToUpperInvariant();

            // Re-center after the new text size is known. Themes that
            // place the date as bare canvas text (Boulder Slate,
            // Daylight) need this — their initial center-at-build was
            // computed against a zero-width measure (text not set
            // yet), and even with a placeholder the width varies
            // day-to-day. Themes that wrap the date in a panel skip
            // this flag and rely on the panel's HorizontalAlignment
            // to auto-center.
            if (_recenterDateReadoutOnUpdate)
            {
                _dateReadout.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(_dateReadout, Cx - _dateReadout.DesiredSize.Width / 2.0);
            }
        }

        // Per-theme digital update for renderers without rotating
        // hands (Flip Clock through Binary Digital). See
        // _digitalUpdater field doc for rationale.
        _digitalUpdater?.Invoke(local);
    }

    // ---------------------------------------------------------------
    // Shared builder helpers
    // ---------------------------------------------------------------

    /// <summary>Build a single line element rotated by <paramref name="degrees"/> about the dial center.</summary>
    private static Line MakeRadialLine(
        double rOuter, double rInner, double degrees,
        Brush stroke, double thickness,
        PenLineCap cap = PenLineCap.Square)
    {
        var rad = degrees * Math.PI / 180.0;
        var sin = Math.Sin(rad);
        var cos = Math.Cos(rad);
        return new Line
        {
            X1 = Cx + sin * rOuter,
            Y1 = Cy - cos * rOuter,
            X2 = Cx + sin * rInner,
            Y2 = Cy - cos * rInner,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = cap,
            StrokeEndLineCap   = cap,
        };
    }

    /// <summary>Add 12 hour ticks to the dial.</summary>
    private void AddHourTicks(double rOuter, double rInner, Brush stroke, double thickness, PenLineCap cap = PenLineCap.Square)
    {
        for (var h = 0; h < 12; h++)
            Dial.Children.Add(MakeRadialLine(rOuter, rInner, h * 30.0, stroke, thickness, cap));
    }

    /// <summary>Add 48 minute ticks (the 12 multiples-of-5 are skipped — those are the hour ticks).</summary>
    private void AddMinuteTicks(double rOuter, double rInner, Brush stroke, double thickness, double opacity = 1.0)
    {
        for (var m = 0; m < 60; m++)
        {
            if (m % 5 == 0) continue;
            var line = MakeRadialLine(rOuter, rInner, m * 6.0, stroke, thickness);
            line.Opacity = opacity;
            Dial.Children.Add(line);
        }
    }

    /// <summary>Place a text block centered at (x, y).</summary>
    private static TextBlock MakeNumeral(string text, double x, double y, FontFamily font, double size, Brush fill, FontWeight? weight = null)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = font,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Bold,
            Foreground = fill,
            TextAlignment = TextAlignment.Center,
        };
        // Measure to find width/height for true centering.
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2.0);
        Canvas.SetTop(tb, y - tb.DesiredSize.Height / 2.0);
        return tb;
    }

    /// <summary>
    /// Place a text block at (x, y) with the requested anchor.
    /// SVG-style placement: "y" is treated as the *baseline* of the
    /// text, so text rendered at the same y as a sibling SVG &lt;text&gt;
    /// element lines up. Anchor controls which edge of the text x
    /// refers to (Left = x is the left edge, Center = x is the
    /// horizontal center, Right = x is the right edge).
    /// </summary>
    private enum TextAnchor { Left, Center, Right }
    private static TextBlock MakeText(
        string text, double x, double y,
        FontFamily font, double size, Brush fill,
        FontWeight? weight = null,
        TextAnchor anchor = TextAnchor.Left,
        double opacity = 1.0)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = font,
            FontSize = size,
            FontWeight = weight ?? FontWeights.Normal,
            Foreground = fill,
            Opacity = opacity,
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var w = tb.DesiredSize.Width;
        var h = tb.DesiredSize.Height;

        var left = anchor switch
        {
            TextAnchor.Center => x - w / 2.0,
            TextAnchor.Right  => x - w,
            _                 => x,
        };
        // Approximate baseline-to-top conversion: WPF's Canvas.SetTop
        // sets the top of the element box, but SVG <text y=…> is the
        // baseline. Subtract ~80% of the font size (typical
        // baseline-from-top ratio for sans-serif) so text positions
        // line up with the SVG mock-ups visually.
        Canvas.SetLeft(tb, left);
        Canvas.SetTop (tb, y - size * 0.85);
        return tb;
    }

    /// <summary>
    /// Build a hand: a Line drawn vertical at center (200, 200), with
    /// Y1 below center for the back-overhang and Y2 above center for
    /// the visible length. RotateTransform centers at (200, 200).
    /// </summary>
    private static (Line line, RotateTransform rotate) MakeHand(
        double overhang, double length,
        Brush stroke, double thickness,
        PenLineCap cap = PenLineCap.Round)
    {
        var rotate = new RotateTransform(0, Cx, Cy);
        var line = new Line
        {
            X1 = Cx, Y1 = Cy + overhang,
            X2 = Cx, Y2 = Cy - length,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = cap,
            StrokeEndLineCap   = cap,
            RenderTransform = rotate,
        };
        return (line, rotate);
    }

    /// <summary>Hand drawn as a Rectangle (baton style — Boulder Slate, Daylight).</summary>
    private static (Rectangle rect, RotateTransform rotate) MakeBatonHand(
        double overhang, double length, double width,
        Brush fill, double cornerRadius = 0)
    {
        // Rectangle is positioned in CANVAS coords from
        //   top-left     = (Cx - width/2, Cy - length)   -> i.e. dial center, then up by `length`
        //   bottom-right = (Cx + width/2, Cy + overhang)
        // RotateTransform.CenterX/CenterY is interpreted in the
        // ELEMENT'S OWN local coord space (not the canvas), where the
        // top-left of the rect is (0, 0). The dial center in those
        // local coords is (width/2, length). Earlier this used
        // (Cx, Cy) which made the hands rotate around a point WAY
        // off-canvas and effectively invisible — the bug that left
        // Boulder Slate / Concourse / Daylight showing only their
        // line-based second hand and the center pin.
        var rotate = new RotateTransform(0, width / 2.0, length);
        var rect = new Rectangle
        {
            Width = width,
            Height = length + overhang,
            Fill = fill,
            RadiusX = cornerRadius,
            RadiusY = cornerRadius,
            RenderTransform = rotate,
        };
        Canvas.SetLeft(rect, Cx - width / 2.0);
        Canvas.SetTop(rect,  Cy - length);
        return (rect, rotate);
    }

    // ===============================================================
    // Theme: Atomic Lab
    // ===============================================================

    private void BuildAtomicLab()
    {
        // Brushes
        var bezelBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xE0, 0xE3, 0xE8), 0),
                new GradientStop(Color.FromRgb(0x7C, 0x80, 0x88), 0.5),
                new GradientStop(Color.FromRgb(0x2C, 0x30, 0x38), 1),
            },
        };
        var faceBrush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.35), GradientOrigin = new Point(0.5, 0.35),
            RadiusX = 0.7, RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x1A, 0x2A, 0x4A), 0),
                new GradientStop(Color.FromRgb(0x06, 0x0D, 0x1A), 1),
            },
        };
        var amber       = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x00));
        var hands       = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        var redSecond   = new SolidColorBrush(Color.FromRgb(0xFF, 0x30, 0x30));
        var panelBg     = new SolidColorBrush(Color.FromRgb(0x04, 0x0B, 0x04));

        // Full-canvas backdrop (matches design/themes/atomic-lab.svg's
        // <rect width="400" height="400" fill="url(#bg-al)"/>). Without
        // this, the corners outside the bezel were transparent — the
        // host TabablzControl's white background showed through, and
        // the white ✕ / ? overlay glyphs (chosen for Atomic Lab's dark
        // face) became white-on-white and disappeared. Every other
        // analog renderer already paints a 400x400 backdrop; this was
        // the lone gap.
        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = faceBrush }
            .At(0, 0));
        Dial.Children.Add(new Ellipse { Width = 344, Height = 344, Fill = bezelBrush }
            .At(Cx - 172, Cy - 172));
        Dial.Children.Add(new Ellipse { Width = 320, Height = 320, Fill = faceBrush }
            .At(Cx - 160, Cy - 160));

        AddMinuteTicks(160, 155, amber, 1, 0.55);
        AddHourTicks  (160, 145, amber, 3);

        var consolas = new FontFamily("Consolas, Courier New");
        Dial.Children.Add(MakeNumeral("12", Cx,         Cy - 132, consolas, 22, amber));
        Dial.Children.Add(MakeNumeral("3",  Cx + 132,   Cy,       consolas, 22, amber));
        Dial.Children.Add(MakeNumeral("6",  Cx,         Cy + 132, consolas, 22, amber));
        Dial.Children.Add(MakeNumeral("9",  Cx - 132,   Cy,       consolas, 22, amber));

        var (hourLine,   hRot) = MakeHand(14, 90,  hands,      6);
        var (minuteLine, mRot) = MakeHand(18, 128, hands,      4);
        var (secondLine, sRot) = MakeHand(22, 142, redSecond,  1.6);
        Dial.Children.Add(hourLine);   _hourRotate   = hRot;
        Dial.Children.Add(minuteLine); _minuteRotate = mRot;
        Dial.Children.Add(secondLine); _secondRotate = sRot;

        Dial.Children.Add(new Ellipse { Width = 14, Height = 14, Fill = amber     }.At(Cx - 7,    Cy - 7));
        Dial.Children.Add(new Ellipse { Width =  5, Height =  5, Fill = faceBrush }.At(Cx - 2.5,  Cy - 2.5));

        // Digital readout panel (date + time + NIST line)
        var panel = new Border
        {
            Width = 146, Height = 60,
            Background = panelBg,
            BorderBrush = amber,
            BorderThickness = new Thickness(0.6),
            CornerRadius = new CornerRadius(4),
        };
        Canvas.SetLeft(panel, Cx - 73);
        Canvas.SetTop(panel,  Cy + 34);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 3) };
        var dateTb = new TextBlock { FontFamily = consolas, FontSize = 9, Foreground = amber, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center };
        var timeTb = new TextBlock { FontFamily = consolas, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = amber, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 1) };
        var nistTb = new TextBlock { Text = "NIST · BOULDER · CO", FontFamily = consolas, FontSize = 7, Foreground = amber, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(dateTb);
        stack.Children.Add(timeTb);
        stack.Children.Add(nistTb);
        panel.Child = stack;
        Dial.Children.Add(panel);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
    }

    // ===============================================================
    // Theme: Boulder Slate (Mondaine SBB-inspired)
    // ===============================================================

    private void BuildBoulderSlate()
    {
        var black = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
        var white = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var sbbRed = new SolidColorBrush(Color.FromRgb(0xE3, 0x00, 0x1B));
        var pageBg = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));

        // Page-color backdrop so the white face stands out
        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = pageBg }.At(0, 0));

        Dial.Children.Add(new Ellipse { Width = 348, Height = 348, Fill = black  }.At(Cx - 174, Cy - 174));
        Dial.Children.Add(new Ellipse { Width = 332, Height = 332, Fill = white  }.At(Cx - 166, Cy - 166));

        AddMinuteTicks(160, 152, black, 2);
        AddHourTicks  (160, 138, black, 6);

        // No numerals — Mondaine has none.

        // Hands as solid black batons
        var (hourBaton,   hRot) = MakeBatonHand(14, 100, 10, black);
        var (minuteBaton, mRot) = MakeBatonHand(18, 138,  7, black);
        Dial.Children.Add(hourBaton);   _hourRotate   = hRot;
        Dial.Children.Add(minuteBaton); _minuteRotate = mRot;

        // Second hand: red rod with disc at the tip (the SBB stop
        // signal). The line and the disc need to rotate as a unit
        // around the dial center (Cx, Cy). Wrapping them in a
        // dedicated 400x400 sub-canvas (same dimensions as Dial) lets
        // both children use absolute canvas coords AND share a single
        // RotateTransform on the host. Earlier this re-used one
        // RotateTransform across both the line and the disc — but
        // RotateTransform.Center is interpreted in EACH ELEMENT'S
        // OWN local coord space. The Line's local frame (with its
        // X1/Y1/X2/Y2 expressed in the parent canvas's coords)
        // happens to match Dial's frame, so (Cx, Cy) worked there.
        // The Ellipse, positioned via Canvas.SetLeft/SetTop, has a
        // local origin at its top-left — so (Cx, Cy) in the disc's
        // local frame = canvas (Cx + LeftOffset, Cy + TopOffset),
        // far off in the lower-right of the dial. Result: the disc
        // orbited that off-canvas point in a wide spiral instead of
        // tracking the line. Hosting both inside a sub-canvas whose
        // own local frame matches Dial's eliminates the per-element
        // offset confusion — one RotateTransform, one center, both
        // children rotate together.
        var secondGroup = new Canvas
        {
            Width = 400, Height = 400,
            IsHitTestVisible = false,
        };

        var sLine = new Line
        {
            X1 = Cx, Y1 = Cy + 22, X2 = Cx, Y2 = Cy - 118,
            Stroke = sbbRed, StrokeThickness = 2.5,
        };
        var sDisc = new Ellipse
        {
            Width = 28, Height = 28,
            Fill = sbbRed,
        };
        Canvas.SetLeft(sDisc, Cx - 14);
        Canvas.SetTop(sDisc,  Cy - 132);

        secondGroup.Children.Add(sLine);
        secondGroup.Children.Add(sDisc);

        var sRot = new RotateTransform(0, Cx, Cy);
        secondGroup.RenderTransform = sRot;
        Dial.Children.Add(secondGroup);
        _secondRotate = sRot;

        Dial.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = black }.At(Cx - 5, Cy - 5));

        // Minimal date + time readout below center, no panel
        var helvetica = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif");
        var dateTb = new TextBlock { FontFamily = helvetica, FontSize = 9, FontWeight = FontWeights.Medium, Foreground = black };
        dateTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var timeTb = new TextBlock { FontFamily = helvetica, FontSize = 14, FontWeight = FontWeights.Medium, Foreground = black };
        timeTb.Text = "00:00:00"; // placeholder for measure
        timeTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Canvas.SetLeft(dateTb, Cx - dateTb.DesiredSize.Width / 2.0);
        Canvas.SetTop (dateTb, Cy + 60);
        Canvas.SetLeft(timeTb, Cx - timeTb.DesiredSize.Width / 2.0);
        Canvas.SetTop (timeTb, Cy + 75);
        Dial.Children.Add(dateTb);
        Dial.Children.Add(timeTb);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
        // Bare canvas text (no enclosing centered panel) — UpdateClock
        // re-measures and re-centers each tick.
        _recenterDateReadoutOnUpdate = true;
    }

    // ===============================================================
    // Theme: Aero Glass (Win11 Fluent acrylic)
    // ===============================================================

    private void BuildAeroGlass()
    {
        // Mock desktop-wallpaper backdrop so the translucency reads
        var bg = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x3A, 0x4F, 0x7A), 0),
                new GradientStop(Color.FromRgb(0x5D, 0x7B, 0xA8), 0.5),
                new GradientStop(Color.FromRgb(0x24, 0x3A, 0x5E), 1),
            },
        };
        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));

        // Subtle wallpaper detail
        Dial.Children.Add(new Ellipse { Width = 80,  Height = 80,  Fill = Brushes.White, Opacity = 0.15 }.At(40, 40));
        Dial.Children.Add(new Ellipse { Width = 40,  Height = 40,  Fill = Brushes.White, Opacity = 0.15 }.At(320, 100));
        Dial.Children.Add(new Ellipse { Width = 120, Height = 120, Fill = Brushes.Black, Opacity = 0.15 }.At(0, 260));

        // Acrylic disc
        var acrylic = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0x52, 0xFF, 0xFF, 0xFF), 0),
                new GradientStop(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF), 1),
            },
        };
        var disc = new Ellipse
        {
            Width = 344, Height = 344,
            Fill = acrylic,
            Stroke = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1,
            Effect = new DropShadowEffect { BlurRadius = 14, Direction = 270, ShadowDepth = 4, Opacity = 0.4 },
        };
        Dial.Children.Add(disc.At(Cx - 172, Cy - 172));

        var white = Brushes.White;
        var cyan  = new SolidColorBrush(Color.FromRgb(0x00, 0xB7, 0xFF));

        AddHourTicks(156, 142, white, 3, PenLineCap.Round);

        var segoe = new FontFamily("Segoe UI Variable, Segoe UI, sans-serif");
        Dial.Children.Add(MakeNumeral("12", Cx,         Cy - 132, segoe, 22, white, FontWeights.SemiBold));
        Dial.Children.Add(MakeNumeral("3",  Cx + 132,   Cy,       segoe, 22, white, FontWeights.SemiBold));
        Dial.Children.Add(MakeNumeral("6",  Cx,         Cy + 132, segoe, 22, white, FontWeights.SemiBold));
        Dial.Children.Add(MakeNumeral("9",  Cx - 132,   Cy,       segoe, 22, white, FontWeights.SemiBold));

        var (hLine, hRot) = MakeHand(14, 92,  white, 7, PenLineCap.Round);
        var (mLine, mRot) = MakeHand(18, 128, white, 5, PenLineCap.Round);
        var (sLine, sRot) = MakeHand(22, 140, cyan,  2, PenLineCap.Round);
        Dial.Children.Add(hLine); _hourRotate   = hRot;
        Dial.Children.Add(mLine); _minuteRotate = mRot;
        Dial.Children.Add(sLine); _secondRotate = sRot;

        Dial.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = white }.At(Cx - 6, Cy - 6));
        Dial.Children.Add(new Ellipse { Width = 4,  Height = 4,  Fill = cyan  }.At(Cx - 2, Cy - 2));

        // Translucent dark pill below center with date + time
        var pill = new Border
        {
            Width = 130, Height = 50,
            Background = new SolidColorBrush(Color.FromArgb(0x59, 0, 0, 0)),
            CornerRadius = new CornerRadius(14),
        };
        Canvas.SetLeft(pill, Cx - 65);
        Canvas.SetTop(pill,  Cy + 38);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var dateTb = new TextBlock { FontFamily = segoe, FontSize = 9,  FontWeight = FontWeights.Medium, Foreground = white, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center };
        var timeTb = new TextBlock { FontFamily = segoe, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = white, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(dateTb);
        stack.Children.Add(timeTb);
        pill.Child = stack;
        Dial.Children.Add(pill);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
    }

    // ===============================================================
    // Theme: Cathode (CRT phosphor)
    // ===============================================================

    private void BuildCathode()
    {
        var phosphor      = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66));
        var phosphorBright= new SolidColorBrush(Color.FromRgb(0xA8, 0xFF, 0x8A));
        var phosphorDim   = new SolidColorBrush(Color.FromRgb(0x00, 0xB0, 0x48));
        var crtBg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.65, RadiusY = 0.65,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x0A, 0x18, 0x08), 0),
                new GradientStop(Color.FromRgb(0x00, 0x00, 0x00), 1),
            },
        };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = crtBg }.At(0, 0));

        var glow = new BlurEffect { Radius = 4 };

        Dial.Children.Add(new Ellipse { Width = 344, Height = 344, Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0x3D, 0x18)), StrokeThickness = 3, Fill = Brushes.Transparent }.At(Cx - 172, Cy - 172));

        AddMinuteTicks(160, 153, phosphorDim, 0.8, 0.5);
        AddHourTicks  (160, 145, phosphor,   2.5, PenLineCap.Round);

        var lucida = new FontFamily("Lucida Console, Consolas, monospace");
        foreach (var (text, x, y) in new[]
                 {
                     ("12", Cx,         Cy - 132),
                     ("3",  Cx + 132,   Cy),
                     ("6",  Cx,         Cy + 132),
                     ("9",  Cx - 132,   Cy),
                 })
        {
            var tb = MakeNumeral(text, x, y, lucida, 20, phosphor, FontWeights.Bold);
            tb.Effect = glow.Clone();
            Dial.Children.Add(tb);
        }

        var (hLine, hRot) = MakeHand(14, 90,  phosphor,       5, PenLineCap.Round);
        var (mLine, mRot) = MakeHand(18, 128, phosphor,     3.5, PenLineCap.Round);
        var (sLine, sRot) = MakeHand(22, 142, phosphorBright, 1.4, PenLineCap.Round);
        hLine.Effect = glow.Clone();
        mLine.Effect = glow.Clone();
        sLine.Effect = glow.Clone();
        Dial.Children.Add(hLine); _hourRotate   = hRot;
        Dial.Children.Add(mLine); _minuteRotate = mRot;
        Dial.Children.Add(sLine); _secondRotate = sRot;

        var pin = new Ellipse { Width = 10, Height = 10, Fill = phosphor, Effect = glow.Clone() };
        Dial.Children.Add(pin.At(Cx - 5, Cy - 5));

        // Digital readout: 7-segment-flavored green LCD
        var panel = new Border
        {
            Width = 144, Height = 50,
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x0A, 0x05)),
            BorderBrush = phosphor, BorderThickness = new Thickness(0.5),
            CornerRadius = new CornerRadius(2),
            Opacity = 0.92,
        };
        Canvas.SetLeft(panel, Cx - 72);
        Canvas.SetTop(panel,  Cy + 36);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var dateTb = new TextBlock { FontFamily = lucida, FontSize = 9,  Foreground = phosphor, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center, Effect = glow.Clone() };
        var timeTb = new TextBlock { FontFamily = lucida, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = phosphor, HorizontalAlignment = HorizontalAlignment.Center, Effect = glow.Clone() };
        stack.Children.Add(dateTb);
        stack.Children.Add(timeTb);
        panel.Child = stack;
        Dial.Children.Add(panel);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
    }

    // ===============================================================
    // Theme: Concourse (departure-board / station-clock)
    // ===============================================================

    private void BuildConcourse()
    {
        var orange     = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
        var orangeText = new SolidColorBrush(Color.FromRgb(0xFF, 0xA4, 0x30));
        var charcoal   = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F));
        var ringGray   = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
        var ringStroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
        var bg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.65, RadiusY = 0.65,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x20, 0x20, 0x20), 0),
                new GradientStop(Color.FromRgb(0x0C, 0x0C, 0x0C), 1),
            },
        };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));
        Dial.Children.Add(new Ellipse   { Width = 344, Height = 344, Fill = ringGray, Stroke = ringStroke, StrokeThickness = 2 }.At(Cx - 172, Cy - 172));
        Dial.Children.Add(new Ellipse   { Width = 320, Height = 320, Fill = charcoal }.At(Cx - 160, Cy - 160));

        AddHourTicks(160, 142, orange, 4);

        // All 12 numerals in big bold orange
        var bebas = new FontFamily("Bebas Neue, DIN Alternate, Impact, sans-serif");
        for (var h = 1; h <= 12; h++)
        {
            var rad = h * 30 * Math.PI / 180.0;
            var x = Cx + Math.Sin(rad) * 122;
            var y = Cy - Math.Cos(rad) * 122;
            Dial.Children.Add(MakeNumeral(h.ToString(), x, y, bebas, 28, orange, FontWeights.Bold));
        }

        // Thick rectangular hands
        var (hRect, hRot) = MakeBatonHand(0, 86, 10, orange, 1.5);
        var (mRect, mRot) = MakeBatonHand(0, 122, 8, orange, 1.5);
        Dial.Children.Add(hRect); _hourRotate   = hRot;
        Dial.Children.Add(mRect); _minuteRotate = mRot;

        var (sLine, sRot) = MakeHand(22, 138, Brushes.White, 1.8, PenLineCap.Round);
        Dial.Children.Add(sLine); _secondRotate = sRot;

        Dial.Children.Add(new Ellipse { Width = 16, Height = 16, Fill = orange   }.At(Cx - 8, Cy - 8));
        Dial.Children.Add(new Ellipse { Width = 6,  Height = 6,  Fill = charcoal }.At(Cx - 3, Cy - 3));

        // Departure-board readout
        var panel = new Border
        {
            Width = 156, Height = 56,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x0D, 0x00)),
            BorderBrush = orange, BorderThickness = new Thickness(0.6),
            CornerRadius = new CornerRadius(3),
        };
        Canvas.SetLeft(panel, Cx - 78);
        Canvas.SetTop(panel,  Cy + 34);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var dateTb = new TextBlock { FontFamily = bebas, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = orangeText, Opacity = 0.85, HorizontalAlignment = HorizontalAlignment.Center };
        var timeTb = new TextBlock { FontFamily = bebas, FontSize = 24, FontWeight = FontWeights.Bold,    Foreground = orangeText, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(dateTb);
        stack.Children.Add(timeTb);
        panel.Child = stack;
        Dial.Children.Add(panel);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
    }

    // ===============================================================
    // Theme: Daylight (high-contrast / accessibility)
    // ===============================================================

    private void BuildDaylight()
    {
        var navy        = new SolidColorBrush(Color.FromRgb(0x00, 0x33, 0x66));
        var orangeRed   = new SolidColorBrush(Color.FromRgb(0xE8, 0x4A, 0x1A));
        var creamWhite  = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var grayRing    = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        var bg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.4), GradientOrigin = new Point(0.5, 0.4),
            RadiusX = 0.7, RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFF, 0xFD, 0xF5), 0),
                new GradientStop(Color.FromRgb(0xFF, 0xF3, 0xD6), 1),
            },
        };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));
        Dial.Children.Add(new Ellipse   { Width = 344, Height = 344, Fill = creamWhite, Stroke = grayRing, StrokeThickness = 2 }.At(Cx - 172, Cy - 172));
        Dial.Children.Add(new Ellipse   { Width = 320, Height = 320, Fill = creamWhite }.At(Cx - 160, Cy - 160));

        AddMinuteTicks(160, 153, navy, 1.2);
        AddHourTicks  (160, 145, navy, 3.5);

        // All 12 numerals in bold navy
        var inter = new FontFamily("Inter, Segoe UI, sans-serif");
        for (var h = 1; h <= 12; h++)
        {
            var rad = h * 30 * Math.PI / 180.0;
            var x = Cx + Math.Sin(rad) * 124;
            var y = Cy - Math.Cos(rad) * 124;
            Dial.Children.Add(MakeNumeral(h.ToString(), x, y, inter, 22, navy, FontWeights.Bold));
        }

        var (hRect, hRot) = MakeBatonHand(0, 90,  9, navy, 1.5);
        var (mRect, mRot) = MakeBatonHand(0, 128, 7, navy, 1.5);
        Dial.Children.Add(hRect); _hourRotate   = hRot;
        Dial.Children.Add(mRect); _minuteRotate = mRot;

        var (sLine, sRot) = MakeHand(22, 140, orangeRed, 2, PenLineCap.Round);
        Dial.Children.Add(sLine); _secondRotate = sRot;

        Dial.Children.Add(new Ellipse { Width = 12, Height = 12, Fill = navy      }.At(Cx - 6, Cy - 6));
        Dial.Children.Add(new Ellipse { Width = 4,  Height = 4,  Fill = orangeRed }.At(Cx - 2, Cy - 2));

        // Plain navy text below center, no panel
        var dateTb = new TextBlock { FontFamily = inter, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = navy };
        var timeTb = new TextBlock { FontFamily = inter, FontSize = 18, FontWeight = FontWeights.Bold,    Foreground = navy };
        timeTb.Text = "00:00:00";
        dateTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        timeTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(dateTb, Cx - dateTb.DesiredSize.Width / 2.0);
        Canvas.SetTop (dateTb, Cy + 60);
        Canvas.SetLeft(timeTb, Cx - timeTb.DesiredSize.Width / 2.0);
        Canvas.SetTop (timeTb, Cy + 76);
        Dial.Children.Add(dateTb);
        Dial.Children.Add(timeTb);

        _digitalReadout = timeTb;
        _dateReadout    = dateTb;
        // Bare canvas text (no enclosing centered panel) — UpdateClock
        // re-measures and re-centers each tick. Same fix as Boulder
        // Slate; Dan flagged the date stuck right-of-center on this
        // theme because the build-time measure was against an empty
        // dateTb (Text not yet set), so Canvas.SetLeft was at Cx-0
        // instead of Cx-half-of-rendered-width.
        _recenterDateReadoutOnUpdate = true;
    }

    // ===============================================================
    // Theme: Flip Clock (Twemco / Solari nightstand mechanical)
    // ===============================================================

    private void BuildFlipClock()
    {
        // Brushes
        var nightstandBg = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x5A, 0x3D, 0x22), 0),
                new GradientStop(Color.FromRgb(0x2A, 0x1D, 0x10), 1),
            },
        };
        var caseBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x2A, 0x2A, 0x2A), 0),
                new GradientStop(Color.FromRgb(0x0A, 0x0A, 0x0A), 0.5),
                new GradientStop(Color.FromRgb(0x1A, 0x1A, 0x1A), 1),
            },
        };
        var chromeBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xCC, 0xCC, 0xCC), 0),
                new GradientStop(Color.FromRgb(0x88, 0x88, 0x88), 0.5),
                new GradientStop(Color.FromRgb(0x44, 0x44, 0x44), 1),
            },
        };
        var cardTopBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFF, 0xFF, 0xFF), 0),
                new GradientStop(Color.FromRgb(0xF4, 0xF1, 0xE8), 1),
            },
        };
        var cardBotBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xF0, 0xED, 0xE2), 0),
                new GradientStop(Color.FromRgb(0xDC, 0xD8, 0xCA), 1),
            },
        };
        var cardEdge   = new SolidColorBrush(Color.FromRgb(0xA8, 0xA3, 0x9A));
        var hingeDark  = new SolidColorBrush(Color.FromRgb(0x5A, 0x56, 0x4C));
        var hingeLight = new SolidColorBrush(Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));
        var amber      = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x4A));
        var digitFill  = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
        var labelFill  = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        var brandFill  = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

        // Backdrop
        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = nightstandBg }.At(0, 0));

        // Chrome legs
        Dial.Children.Add(new Rectangle { Width = 22, Height = 14, Fill = chromeBrush, RadiusX = 2, RadiusY = 2 }.At(78,  316));
        Dial.Children.Add(new Rectangle { Width = 22, Height = 14, Fill = chromeBrush, RadiusX = 2, RadiusY = 2 }.At(300, 316));

        // Clock case + inner display recess
        Dial.Children.Add(new Rectangle
        {
            Width = 328, Height = 244, Fill = caseBrush,
            Stroke = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)), StrokeThickness = 1.5,
            RadiusX = 14, RadiusY = 14,
        }.At(36, 78));
        Dial.Children.Add(new Rectangle
        {
            Width = 300, Height = 186, Fill = new SolidColorBrush(Color.FromRgb(0x1C, 0x1A, 0x16)),
            RadiusX = 6, RadiusY = 6,
        }.At(50, 98));

        // Four flip tiles. Each tile is 60x138 anchored at (tileLeft, 122);
        // a TextBlock for the digit overlays the front face.
        var helvetica = new FontFamily("Segoe UI Variable, Segoe UI, Arial, sans-serif");
        var tileLefts = new[] { 64.0, 132.0, 208.0, 276.0 };
        var digitTextBlocks = new TextBlock[4];

        for (var i = 0; i < 4; i++)
        {
            var left = tileLefts[i];

            // Top half + bottom half (the seam fold)
            Dial.Children.Add(new Rectangle
            {
                Width = 60, Height = 69, Fill = cardTopBrush,
                Stroke = cardEdge, StrokeThickness = 0.5,
                RadiusX = 5, RadiusY = 3,
            }.At(left, 122));
            Dial.Children.Add(new Rectangle
            {
                Width = 60, Height = 69, Fill = cardBotBrush,
                Stroke = cardEdge, StrokeThickness = 0.5,
                RadiusX = 5, RadiusY = 3,
            }.At(left, 122 + 69));

            // Hinge / fold line
            Dial.Children.Add(new Line
            {
                X1 = left, Y1 = 122 + 69, X2 = left + 60, Y2 = 122 + 69,
                Stroke = hingeDark, StrokeThickness = 0.7,
            });
            Dial.Children.Add(new Line
            {
                X1 = left, Y1 = 122 + 70, X2 = left + 60, Y2 = 122 + 70,
                Stroke = hingeLight, StrokeThickness = 0.5,
            });

            // Spindle pegs
            foreach (var px in new[] { left - 3, left + 63 })
            {
                Dial.Children.Add(new Ellipse
                {
                    Width = 6.4, Height = 6.4, Fill = chromeBrush,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)), StrokeThickness = 0.4,
                }.At(px - 3.2, 122 + 69 - 3.2));
                Dial.Children.Add(new Ellipse
                {
                    Width = 2, Height = 2, Fill = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                }.At(px - 1, 122 + 69 - 1));
            }

            // Digit text — placeholder, updated each tick by _digitalUpdater
            var digitTb = MakeText("0", left + 30, 122 + 105, helvetica, 78, digitFill,
                                   FontWeights.Black, TextAnchor.Center);
            Dial.Children.Add(digitTb);
            digitTextBlocks[i] = digitTb;
        }

        // Amber colon dots between hours and minutes
        Dial.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = amber }.At(196.5, 162.5));
        Dial.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = amber }.At(196.5, 212.5));

        // ":SS SECONDS" line + date readout (day · month · day-of-month
        // — parity with the analog faces, which all carry a date line
        // beneath their digital readout) + COMTEKGLOBAL badge.
        var secondsTb = MakeText(": 00 SECONDS", 200, 274, helvetica, 10, labelFill,
                                 FontWeights.Medium, TextAnchor.Center);
        Dial.Children.Add(secondsTb);
        var dateTb = MakeText("THU · APRIL 30 · 2026", 200, 292, helvetica, 11, labelFill,
                              FontWeights.Medium, TextAnchor.Center);
        Dial.Children.Add(dateTb);
        Dial.Children.Add(MakeText("COMTEKGLOBAL · MODEL CT-1971", 200, 312, helvetica, 9, brandFill,
                                   FontWeights.Normal, TextAnchor.Center));

        _digitalUpdater = local =>
        {
            var (hour12, ampm) = To12HourParts(local);
            digitTextBlocks[0].Text = (hour12 / 10).ToString(CultureInfo.InvariantCulture);
            digitTextBlocks[1].Text = (hour12 % 10).ToString(CultureInfo.InvariantCulture);
            digitTextBlocks[2].Text = (local.Minute / 10).ToString(CultureInfo.InvariantCulture);
            digitTextBlocks[3].Text = (local.Minute % 10).ToString(CultureInfo.InvariantCulture);
            // AM/PM next to the seconds line — Flip Clock's tile faces
            // are HH:MM only by design, so the AM/PM marker rides
            // alongside the auxiliary seconds label.
            secondsTb.Text = $": {local.Second:D2} SECONDS · {ampm}";
            dateTb.Text    = local.ToString("ddd · MMMM d · yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        };
    }

    // ===============================================================
    // Theme: Marquee (theater chase bulbs)
    // ===============================================================

    private void BuildMarquee()
    {
        var theaterRed   = new SolidColorBrush(Color.FromRgb(0x7A, 0x18, 0x18));
        var darkRedLine  = new SolidColorBrush(Color.FromRgb(0x3A, 0x0A, 0x0A));
        var stageRedLine = new SolidColorBrush(Color.FromRgb(0x3A, 0x10, 0x10));
        var bulbAmber    = new SolidColorBrush(Color.FromRgb(0xFF, 0xC9, 0x40));
        var stageBg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.6, RadiusY = 0.6,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x1A, 0x0A, 0x0A), 0),
                new GradientStop(Color.FromRgb(0x08, 0x04, 0x04), 1),
            },
        };
        var bulbBrush = new RadialGradientBrush
        {
            Center = new Point(0.38, 0.32), GradientOrigin = new Point(0.38, 0.32),
            RadiusX = 0.65, RadiusY = 0.65,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFF, 0xF8, 0xD8), 0),
                new GradientStop(Color.FromRgb(0xFF, 0xC9, 0x40), 0.55),
                new GradientStop(Color.FromRgb(0xA0, 0x60, 0x10), 1),
            },
        };
        var glow = new BlurEffect { Radius = 4 };

        // Outer red theater frame
        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = theaterRed }.At(0, 0));
        Dial.Children.Add(new Rectangle
        {
            Width = 372, Height = 372, Fill = Brushes.Transparent,
            Stroke = darkRedLine, StrokeThickness = 2,
        }.At(14, 14));
        // Inner stage panel
        Dial.Children.Add(new Rectangle
        {
            Width = 332, Height = 332, Fill = stageBg,
            Stroke = stageRedLine, StrokeThickness = 2,
            RadiusX = 4, RadiusY = 4,
        }.At(34, 34));

        // Chase bulbs around the inner border. Pre-laid-out positions
        // mirroring the SVG (top + bottom rows of 9; left + right
        // columns of 7, skipping corners).
        var bulbPositions = new (double x, double y)[]
        {
            (58, 58), (92, 58), (126, 58), (160, 58), (194, 58), (228, 58), (262, 58), (296, 58), (330, 58),
            (58, 342), (92, 342), (126, 342), (160, 342), (194, 342), (228, 342), (262, 342), (296, 342), (330, 342),
            (58, 92), (58, 126), (58, 160), (58, 194), (58, 228), (58, 262), (58, 296),
            (330, 92), (330, 126), (330, 160), (330, 194), (330, 228), (330, 262), (330, 296),
        };
        foreach (var (bx, by) in bulbPositions)
        {
            Dial.Children.Add(new Ellipse
            {
                Width = 12, Height = 12, Fill = bulbBrush,
                Effect = glow.Clone(),
            }.At(bx - 6, by - 6));
        }

        // "★ NOW SHOWING ★" header
        var bebas = new FontFamily("Bebas Neue, DIN Alternate, Impact, Arial Black, sans-serif");
        Dial.Children.Add(MakeText("★ NOW SHOWING ★", 200, 120, bebas, 20, bulbAmber,
                                   FontWeights.Bold, TextAnchor.Center));

        // Big time
        var timeTb = MakeText("00:00:00", 200, 232, bebas, 64, bulbAmber,
                              FontWeights.Black, TextAnchor.Center);
        timeTb.Effect = glow.Clone();
        Dial.Children.Add(timeTb);

        // Subtitle
        Dial.Children.Add(MakeText("★ ATOMIC TIME ★ FROM BOULDER ★", 200, 282, bebas, 13, bulbAmber,
                                   FontWeights.Medium, TextAnchor.Center, opacity: 0.85));

        // Date line (parity with the analog faces — day/month/dom/year).
        var marqueeDateTb = MakeText("WED · APRIL 30 · 2026", 200, 308, bebas, 12, bulbAmber,
                                     FontWeights.Medium, TextAnchor.Center, opacity: 0.85);
        Dial.Children.Add(marqueeDateTb);

        _digitalUpdater = local =>
        {
            timeTb.Text = local.ToString("h:mm:ss tt", CultureInfo.InvariantCulture);
            marqueeDateTb.Text = local.ToString("ddd · MMMM d · yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        };

        // TODO Marquee: chase-bulb brightness wave (left-to-right
        // animation) and per-bulb intensity, per design/README.md.
    }

    // ===============================================================
    // Theme: Slab (brutalist concrete digital)
    // ===============================================================

    private void BuildSlab()
    {
        var concrete   = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xD4, 0xD0, 0xC5), 0),
                new GradientStop(Color.FromRgb(0xA8, 0xA2, 0x98), 1),
            },
        };
        var inkBlack   = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        var slabBlack  = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A));
        var accentRed  = new SolidColorBrush(Color.FromRgb(0xCC, 0x2A, 0x1A));
        var dateGray   = new SolidColorBrush(Color.FromRgb(0x3A, 0x36, 0x30));

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = concrete }.At(0, 0));

        // Top + bottom accent bars + red diagonal
        Dial.Children.Add(new Rectangle { Width = 320, Height = 6, Fill = inkBlack }.At(40, 60));
        Dial.Children.Add(new Rectangle { Width = 320, Height = 6, Fill = inkBlack }.At(40, 338));
        Dial.Children.Add(new Rectangle { Width = 60,  Height = 3, Fill = accentRed }.At(40, 76));

        var slabFont = new FontFamily("Rockwell, Roboto Slab, Cambria, serif");
        var contextTb = MakeText("ATOMIC · TIME · LOCAL", 40, 100, slabFont, 11, inkBlack,
                                 FontWeights.Bold, TextAnchor.Left);
        Dial.Children.Add(contextTb);

        var bigTimeTb = MakeText("10:08", 200, 240, slabFont, 100, slabBlack,
                                 FontWeights.Black, TextAnchor.Center);
        Dial.Children.Add(bigTimeTb);

        var secondsTb = MakeText("42″", 200, 290, slabFont, 36, accentRed,
                                 FontWeights.Bold, TextAnchor.Center);
        Dial.Children.Add(secondsTb);

        var dateTb = MakeText("SATURDAY · 1 JANUARY 2026", 200, 328, slabFont, 10, dateGray,
                              FontWeights.Medium, TextAnchor.Center);
        Dial.Children.Add(dateTb);

        _digitalUpdater = local =>
        {
            // 12-hour: big time stays h:mm without AM/PM marker
            // (the brutalist serif is intentionally minimalist;
            // adding " AM" at font-100 wouldn't fit). AM/PM rides
            // alongside the seconds line at font-36.
            bigTimeTb.Text = local.ToString("h:mm", CultureInfo.InvariantCulture);
            secondsTb.Text = $"{local:ss}″ {local:tt}";
            dateTb.Text    = local.ToString("dddd · d MMMM yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
            // Display the bound TimeZone's standard offset abbreviation
            // in the upper-left context strip — falls back to a generic
            // label if no abbreviation is reasonable.
            var tzAbbrev = local.IsDaylightSavingTime() ? "DST" : "STD";
            contextTb.Text = $"ATOMIC · TIME · {tzAbbrev}";
        };
    }

    // ===============================================================
    // Theme: Binary (BCD LED stack)
    // ===============================================================

    private void BuildBinary()
    {
        var bg          = new SolidColorBrush(Color.FromRgb(0x08, 0x08, 0x08));
        var gridLine    = new SolidColorBrush(Color.FromRgb(0x1A, 0x08, 0x08));
        var bitLabel    = new SolidColorBrush(Color.FromRgb(0x55, 0x30, 0x30));
        var headLabel   = new SolidColorBrush(Color.FromRgb(0xFF, 0x55, 0x55));
        var groupLabel  = new SolidColorBrush(Color.FromRgb(0xAA, 0x30, 0x30));
        var litRed      = new SolidColorBrush(Color.FromRgb(0xFF, 0x30, 0x30));
        var litBrush = new RadialGradientBrush
        {
            Center = new Point(0.38, 0.32), GradientOrigin = new Point(0.38, 0.32),
            RadiusX = 0.65, RadiusY = 0.65,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0xFF, 0xC4, 0xC4), 0),
                new GradientStop(Color.FromRgb(0xFF, 0x30, 0x30), 0.5),
                new GradientStop(Color.FromRgb(0x7A, 0x08, 0x08), 1),
            },
        };
        var unlitBrush = new RadialGradientBrush
        {
            Center = new Point(0.38, 0.32), GradientOrigin = new Point(0.38, 0.32),
            RadiusX = 0.65, RadiusY = 0.65,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x3A, 0x0A, 0x0A), 0),
                new GradientStop(Color.FromRgb(0x18, 0x04, 0x04), 1),
            },
        };
        var glow = new BlurEffect { Radius = 6 };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));

        // Faint horizontal grid lines separating the dot area
        Dial.Children.Add(new Line { X1 = 40, Y1 = 120, X2 = 380, Y2 = 120, Stroke = gridLine, StrokeThickness = 0.5 });
        Dial.Children.Add(new Line { X1 = 40, Y1 = 265, X2 = 380, Y2 = 265, Stroke = gridLine, StrokeThickness = 0.5 });

        var mono = new FontFamily("Cascadia Code, Consolas, Lucida Console, monospace");

        // Title
        Dial.Children.Add(MakeText("BINARY CLOCK", 200, 60, mono, 16, headLabel,
                                   FontWeights.Bold, TextAnchor.Center));

        // Date line (parity with the analog faces — day/month/dom/year).
        // Sits between the title and the LED grid.
        var binaryDateTb = MakeText("MON · APRIL 30 · 2026", 200, 90, mono, 11, headLabel,
                                    FontWeights.Normal, TextAnchor.Center, opacity: 0.7);
        Dial.Children.Add(binaryDateTb);

        // Bit-value labels (8/4/2/1, right-aligned to x=42)
        Dial.Children.Add(MakeText("8", 42, 155, mono, 11, bitLabel, FontWeights.Normal, TextAnchor.Right));
        Dial.Children.Add(MakeText("4", 42, 195, mono, 11, bitLabel, FontWeights.Normal, TextAnchor.Right));
        Dial.Children.Add(MakeText("2", 42, 235, mono, 11, bitLabel, FontWeights.Normal, TextAnchor.Right));
        Dial.Children.Add(MakeText("1", 42, 275, mono, 11, bitLabel, FontWeights.Normal, TextAnchor.Right));

        // 6 columns of LEDs with per-column bit set.
        // (x position, bits-to-show array of (bitValue, y))
        // Hour-tens has only bits 2,1 (max value 2); minute-tens / second-tens have 4,2,1 (max 5);
        // ones digits have 8,4,2,1.
        var columns = new (double cx, (int bit, double cy)[] dots)[]
        {
            (78,  new (int, double)[] { (2, 230), (1, 270) }),                                 // H tens (0-2)
            (128, new (int, double)[] { (8, 150), (4, 190), (2, 230), (1, 270) }),             // H ones (0-9)
            (198, new (int, double)[] { (4, 190), (2, 230), (1, 270) }),                       // M tens (0-5)
            (248, new (int, double)[] { (8, 150), (4, 190), (2, 230), (1, 270) }),             // M ones (0-9)
            (318, new (int, double)[] { (4, 190), (2, 230), (1, 270) }),                       // S tens (0-5)
            (368, new (int, double)[] { (8, 150), (4, 190), (2, 230), (1, 270) }),             // S ones (0-9)
        };

        // Track every dot together with its column-index and bit-value
        // so the per-tick updater can flip the right ones lit/unlit.
        var allDots = new List<(int colIndex, int bit, Ellipse ellipse)>();
        for (var ci = 0; ci < columns.Length; ci++)
        {
            var (cx, dots) = columns[ci];
            foreach (var (bit, cy) in dots)
            {
                var dot = new Ellipse { Width = 22, Height = 22, Fill = unlitBrush };
                Canvas.SetLeft(dot, cx - 11);
                Canvas.SetTop (dot, cy - 11);
                Dial.Children.Add(dot);
                allDots.Add((ci, bit, dot));
            }
        }

        // Group labels HOURS / MINUTES / SECONDS
        Dial.Children.Add(MakeText("HOURS",   103, 304, mono, 10, groupLabel, FontWeights.Normal, TextAnchor.Center));
        Dial.Children.Add(MakeText("MINUTES", 223, 304, mono, 10, groupLabel, FontWeights.Normal, TextAnchor.Center));
        Dial.Children.Add(MakeText("SECONDS", 343, 304, mono, 10, groupLabel, FontWeights.Normal, TextAnchor.Center));

        // Decoded readout (also auto-updates) and footer
        var decodedTb = MakeText("00 : 00 : 00", 200, 350, mono, 22, litRed,
                                 FontWeights.Bold, TextAnchor.Center);
        decodedTb.Effect = glow.Clone();
        Dial.Children.Add(decodedTb);
        Dial.Children.Add(MakeText("read top→bottom · 8·4·2·1 BCD per column",
                                   200, 375, mono, 9, bitLabel, FontWeights.Normal, TextAnchor.Center));

        _digitalUpdater = local =>
        {
            // Binary BCD stays 24-hour (programmer/encoder theme;
            // matching the decimal range to the bit-widths is the
            // point). H tens column needs 2 dots to express 0-23.
            var digitsByCol = new[]
            {
                local.Hour   / 10, local.Hour   % 10,
                local.Minute / 10, local.Minute % 10,
                local.Second / 10, local.Second % 10,
            };
            foreach (var (colIndex, bit, ellipse) in allDots)
            {
                var lit = (digitsByCol[colIndex] & bit) != 0;
                ellipse.Fill = lit ? litBrush : unlitBrush;
                ellipse.Effect = lit ? glow.Clone() : null;
            }
            decodedTb.Text = local.ToString("HH : mm : ss", CultureInfo.InvariantCulture);
            binaryDateTb.Text = local.ToString("ddd · MMMM d · yyyy", CultureInfo.InvariantCulture).ToUpperInvariant();
        };
    }

    // ===============================================================
    // Theme: Hex (programmer hexadecimal terminal)
    // ===============================================================

    private void BuildHex()
    {
        var bg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.4), GradientOrigin = new Point(0.5, 0.4),
            RadiusX = 0.7, RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x0C, 0x18, 0x28), 0),
                new GradientStop(Color.FromRgb(0x02, 0x08, 0x12), 1),
            },
        };
        var titleBar  = new SolidColorBrush(Color.FromRgb(0x00, 0x08, 0x14));
        var cyan      = new SolidColorBrush(Color.FromRgb(0x5F, 0xE2, 0xFF));
        var cyanBright= new SolidColorBrush(Color.FromRgb(0xA0, 0xEE, 0xFF));
        var dimCyan   = new SolidColorBrush(Color.FromRgb(0x3A, 0x8A, 0xAA));
        var trafficR  = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
        var trafficY  = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x30));
        var trafficG  = new SolidColorBrush(Color.FromRgb(0x50, 0xCC, 0x60));
        var glow = new BlurEffect { Radius = 3 };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));
        Dial.Children.Add(new Rectangle { Width = 400, Height = 32, Fill = titleBar }.At(0, 0));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficR, Opacity = 0.7 }.At(10, 12));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficY, Opacity = 0.7 }.At(26, 12));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficG, Opacity = 0.7 }.At(42, 12));

        var mono = new FontFamily("Cascadia Code, Consolas, Lucida Console, monospace");
        Dial.Children.Add(MakeText("comtekglobal :: hex_clock.exe", 200, 21, mono, 11, cyan,
                                   FontWeights.Normal, TextAnchor.Center, opacity: 0.6));

        Dial.Children.Add(MakeText("// time encoded as hexadecimal (per unit)",
                                   40, 80, mono, 12, cyan, FontWeights.Normal, TextAnchor.Left, opacity: 0.55));

        // Big hex digits HH:MM:SS in two-hex-digits-per-unit
        var hexTb = MakeText("00:00:00", 200, 190, mono, 56, cyan,
                             FontWeights.Bold, TextAnchor.Center);
        hexTb.Effect = glow.Clone();
        Dial.Children.Add(hexTb);

        // Section labels
        Dial.Children.Add(MakeText("HOURS",   80,  216, mono, 10, dimCyan, FontWeights.Normal, TextAnchor.Center));
        Dial.Children.Add(MakeText("MINUTES", 200, 216, mono, 10, dimCyan, FontWeights.Normal, TextAnchor.Center));
        Dial.Children.Add(MakeText("SECONDS", 320, 216, mono, 10, dimCyan, FontWeights.Normal, TextAnchor.Center));

        // Per our spec: drop the decimal-time decode line; instead show
        // the day-of-week, day-of-month, and month name as their
        // hex-ASCII codes (programmer-terminal vibe). Day-fraction
        // and the day-as-color block stay — those are still
        // legitimately "hex".
        var dowTb = MakeText("// dow:   00 00 00", 40, 244, mono, 12, cyan,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(dowTb);
        var domTb = MakeText("// dom:   00 00", 40, 260, mono, 12, cyan,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(domTb);
        var monTb = MakeText("// month: 00 00 00", 40, 276, mono, 12, cyan,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(monTb);
        // Year as hex ASCII for parity with the rest of the date breakdown.
        var yearTb = MakeText("// year:  00 00 00 00", 40, 290, mono, 12, cyan,
                              FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(yearTb);

        var dayTb = MakeText("// day-frac: 0x0000 / 0xFFFF (0.0% elapsed)", 40, 308, mono, 12, cyan,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.7);
        Dial.Children.Add(dayTb);

        var swatch = new Rectangle { Width = 320, Height = 14, Fill = Brushes.Black, RadiusX = 2, RadiusY = 2, Opacity = 0.85 };
        Canvas.SetLeft(swatch, 40); Canvas.SetTop(swatch, 322);
        Dial.Children.Add(swatch);

        var swatchHexTb = MakeText("// the bar above is #0000FF — today, encoded as a color",
                                   40, 350, mono, 11, cyan,
                                   FontWeights.Normal, TextAnchor.Left, opacity: 0.55);
        Dial.Children.Add(swatchHexTb);

        Dial.Children.Add(MakeText("$ _", 40, 378, mono, 14, cyanBright, FontWeights.Normal, TextAnchor.Left));

        _digitalUpdater = local =>
        {
            // Hex stays 24-hour (programmer/encoder theme; full hour
            // range 0-23 maps to 00-17 hex cleanly). HH:MM:SS each
            // rendered as 2-digit hex of the decimal value. So
            // 10:08:42 -> 0A:08:2A; 23:59:59 -> 17:3B:3B.
            hexTb.Text = $"{local.Hour:X2}:{local.Minute:X2}:{local.Second:X2}";

            // Day-of-week / day-of-month / month name shown as their
            // hex-ASCII codes followed by the friendly form. e.g.
            //   // dow:   54 48 55  (THU)
            //   // dom:   32 39     (29)
            //   // month: 41 50 52 49 4C  (APRIL)
            var dow   = local.ToString("ddd",  CultureInfo.InvariantCulture).ToUpperInvariant();
            var dom   = local.Day.ToString(CultureInfo.InvariantCulture);
            var month = local.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            var year = local.Year.ToString(CultureInfo.InvariantCulture);
            dowTb.Text  = $"// dow:   {ToHexAscii(dow)}  ({dow})";
            domTb.Text  = $"// dom:   {ToHexAscii(dom)}  ({dom})";
            monTb.Text  = $"// month: {ToHexAscii(month)}  ({month})";
            yearTb.Text = $"// year:  {ToHexAscii(year)}  ({year})";

            // Day fraction: seconds elapsed since midnight, scaled to
            // 0–0xFFFF. The 16-bit value drives a hex string + colour.
            var secsSinceMidnight = local.Hour * 3600 + local.Minute * 60 + local.Second;
            var fraction = secsSinceMidnight / 86400.0;
            var dayU16 = (ushort)Math.Min(0xFFFF, Math.Round(fraction * 0xFFFF));
            var pct = fraction * 100.0;
            dayTb.Text = $"// day-frac: 0x{dayU16:X4} / 0xFFFF ({pct:F1}% elapsed)";

            // Color = (dayU16 >> 8, dayU16 & 0xFF, 0xFF).
            var r = (byte)(dayU16 >> 8);
            var g = (byte)(dayU16 & 0xFF);
            var swatchColor = Color.FromRgb(r, g, 0xFF);
            swatch.Fill = new SolidColorBrush(swatchColor);
            swatchHexTb.Text = $"// the bar above is #{r:X2}{g:X2}FF — today, encoded as a color";
        };
    }

    /// <summary>
    /// Convert a 24-hour <see cref="DateTime"/> to its 12-hour pair
    /// (hour in 1..12, "AM"/"PM" string). Single source of truth for
    /// the 12-hour-default rule across every digital readout — analog
    /// faces' integrated digital readouts use a `"h:mm:ss tt"` format
    /// string directly; encoded themes (Binary, Hex, Binary Digital)
    /// need the integer hour separately so they can BCD/hex/binary it,
    /// then surface the AM/PM marker as a sibling label.
    /// </summary>
    private static (int Hour12, string AmPm) To12HourParts(DateTime local)
    {
        var h12 = local.Hour % 12;
        if (h12 == 0) h12 = 12;
        return (h12, local.Hour < 12 ? "AM" : "PM");
    }

    /// <summary>
    /// Render an ASCII string as space-separated 2-digit hex codes —
    /// "MON" -> "4D 4F 4E". Used by the Hex theme for date encoding.
    /// </summary>
    private static string ToHexAscii(string s) =>
        string.Join(" ", s.Select(c => ((int)c).ToString("X2", CultureInfo.InvariantCulture)));

    /// <summary>
    /// Render an ASCII string as space-separated 8-bit binary codes —
    /// "MON" -> "01001101 01001111 01001110". Used by Binary Digital.
    /// </summary>
    private static string ToBinAscii(string s) =>
        string.Join(" ", s.Select(c => Convert.ToString((int)c, 2).PadLeft(8, '0')));

    // ===============================================================
    // Theme: Binary Digital (pure binary text terminal)
    // ===============================================================

    private void BuildBinaryDigital()
    {
        var bg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.4), GradientOrigin = new Point(0.5, 0.4),
            RadiusX = 0.7, RadiusY = 0.7,
            GradientStops =
            {
                new GradientStop(Color.FromRgb(0x1A, 0x08, 0x30), 0),
                new GradientStop(Color.FromRgb(0x08, 0x04, 0x14), 1),
            },
        };
        var titleBar  = new SolidColorBrush(Color.FromRgb(0x08, 0x00, 0x0A));
        var magenta   = new SolidColorBrush(Color.FromRgb(0xFF, 0x5C, 0xD0));
        var magentaBr = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0xE8));
        var dimMagenta= new SolidColorBrush(Color.FromRgb(0x88, 0x33, 0x66));
        var trafficR  = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
        var trafficY  = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x30));
        var trafficG  = new SolidColorBrush(Color.FromRgb(0x50, 0xCC, 0x60));
        var glow = new BlurEffect { Radius = 3 };

        Dial.Children.Add(new Rectangle { Width = 400, Height = 400, Fill = bg }.At(0, 0));
        Dial.Children.Add(new Rectangle { Width = 400, Height = 32, Fill = titleBar }.At(0, 0));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficR, Opacity = 0.7 }.At(10, 12));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficY, Opacity = 0.7 }.At(26, 12));
        Dial.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = trafficG, Opacity = 0.7 }.At(42, 12));

        var mono = new FontFamily("Cascadia Code, Consolas, Lucida Console, monospace");
        Dial.Children.Add(MakeText("comtekglobal :: bin_clock.exe", 200, 21, mono, 11, magenta,
                                   FontWeights.Normal, TextAnchor.Center, opacity: 0.7));
        Dial.Children.Add(MakeText("// time encoded as binary text (per unit)",
                                   40, 80, mono, 12, magenta,
                                   FontWeights.Normal, TextAnchor.Left, opacity: 0.55));

        // Three big lines, each "<H|M|S> bbbbbbbb". Binary Digital
        // stays 24-hour (programmer/encoder theme): hour fits in
        // 5 bits (max 23 = 10111).
        var hPrefix = MakeText("H", 80, 138, mono, 30, dimMagenta, FontWeights.Bold, TextAnchor.Left);
        Dial.Children.Add(hPrefix);
        var hBitsTb = MakeText("00000", 120, 138, mono, 30, magenta, FontWeights.Bold, TextAnchor.Left);
        hBitsTb.Effect = glow.Clone();
        Dial.Children.Add(hBitsTb);

        var mPrefix = MakeText("M", 80, 180, mono, 30, dimMagenta, FontWeights.Bold, TextAnchor.Left);
        Dial.Children.Add(mPrefix);
        var mBitsTb = MakeText("000000", 120, 180, mono, 30, magenta, FontWeights.Bold, TextAnchor.Left);
        mBitsTb.Effect = glow.Clone();
        Dial.Children.Add(mBitsTb);

        var sPrefix = MakeText("S", 80, 222, mono, 30, dimMagenta, FontWeights.Bold, TextAnchor.Left);
        Dial.Children.Add(sPrefix);
        var sBitsTb = MakeText("000000", 120, 222, mono, 30, magenta, FontWeights.Bold, TextAnchor.Left);
        sBitsTb.Effect = glow.Clone();
        Dial.Children.Add(sBitsTb);

        // Per our spec: drop the decimal decode line; show the day-of-week,
        // day-of-month, and month name as 8-bit binary ASCII codes
        // (mirrors the Hex theme's hex-ASCII treatment).
        var dowTb = MakeText("// dow:   00000000", 40, 254, mono, 11, magenta,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(dowTb);
        var domTb = MakeText("// dom:   00000000", 40, 270, mono, 11, magenta,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(domTb);
        var monTb = MakeText("// mon:   00000000", 40, 286, mono, 11, magenta,
                             FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(monTb);
        // Year as 8-bit binary ASCII codes for parity with the rest
        // of the date breakdown. 4 chars × 8 bits = 32 binary digits
        // — long line but fits at font 11.
        var yearTb = MakeText("// yr:    00000000 00000000 00000000 00000000", 40, 302, mono, 11, magenta,
                              FontWeights.Normal, TextAnchor.Left, opacity: 0.75);
        Dial.Children.Add(yearTb);

        Dial.Children.Add(MakeText("// widths: 5b hour · 6b min · 6b sec · MSB first",
                                   40, 320, mono, 10, dimMagenta, FontWeights.Normal, TextAnchor.Left));

        // Decorative noise rows (static — same as the SVG's faux bit
        // grid; doesn't reflect time)
        Dial.Children.Add(MakeText("11010 · 01100 · 11100 · 01000 · 10101 · 10010 · 00111 · 11001",
                                   40, 340, mono, 9, magenta, FontWeights.Normal, TextAnchor.Left, opacity: 0.18));
        Dial.Children.Add(MakeText("01001 · 11011 · 00100 · 11110 · 01010 · 10000 · 11000 · 00101",
                                   40, 354, mono, 9, magenta, FontWeights.Normal, TextAnchor.Left, opacity: 0.18));

        Dial.Children.Add(MakeText("$ _", 40, 380, mono, 14, magentaBr,
                                   FontWeights.Normal, TextAnchor.Left));

        _digitalUpdater = local =>
        {
            // 24-hour (programmer/encoder theme). Hour fits in 5 bits
            // (max 23 = 0b10111), minutes / seconds fit in 6 bits
            // (max 59 = 0b111011). Convert with leading zeros so the
            // width is fixed.
            hBitsTb.Text = Convert.ToString(local.Hour,   2).PadLeft(5, '0');
            mBitsTb.Text = Convert.ToString(local.Minute, 2).PadLeft(6, '0');
            sBitsTb.Text = Convert.ToString(local.Second, 2).PadLeft(6, '0');

            // Day / date / month as 8-bit binary ASCII codes followed
            // by the friendly form. Lengths get long fast (8 bits ×
            // n chars + spaces), so day-of-week and month abbreviate
            // to 3 letters (THU / APR) and day-of-month to 1–2 chars.
            var dow   = local.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();
            var dom   = local.Day.ToString(CultureInfo.InvariantCulture);
            var month = local.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            var year = local.Year.ToString(CultureInfo.InvariantCulture);
            dowTb.Text  = $"// dow: {ToBinAscii(dow)} ({dow})";
            domTb.Text  = $"// dom: {ToBinAscii(dom)} ({dom})";
            monTb.Text  = $"// mon: {ToBinAscii(month)} ({month})";
            yearTb.Text = $"// yr:  {ToBinAscii(year)} ({year})";
        };
    }
}

// ---------------------------------------------------------------
// Tiny extension to chain .At(x, y) onto Canvas children for
// readability — the equivalent of two Canvas.SetLeft / SetTop calls.
// ---------------------------------------------------------------

internal static class CanvasPlacementExtensions
{
    public static T At<T>(this T element, double x, double y) where T : UIElement
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop (element, y);
        return element;
    }
}
