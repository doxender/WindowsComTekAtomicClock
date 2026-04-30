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
        if (d is ClockFaceControl c && c.IsLoaded)
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

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    private DispatcherTimer? _timer;
    private const double Cx = 200.0;
    private const double Cy = 200.0;

    public ClockFaceControl()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer
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

    private void RenderActiveTheme()
    {
        Dial.Children.Clear();
        _hourRotate = _minuteRotate = _secondRotate = null;
        _digitalReadout = _dateReadout = null;

        switch (Theme)
        {
            case SettingsTheme.AtomicLab:    BuildAtomicLab();    break;
            case SettingsTheme.BoulderSlate: BuildBoulderSlate(); break;
            case SettingsTheme.AeroGlass:    BuildAeroGlass();    break;
            case SettingsTheme.Cathode:      BuildCathode();      break;
            case SettingsTheme.Concourse:    BuildConcourse();    break;
            case SettingsTheme.Daylight:     BuildDaylight();     break;
            // Themes not yet implemented in WPF fall back to Atomic Lab.
            // Their TabViewModel.Theme persistence is unaffected; the
            // user's selection is remembered for when each theme ships.
            // The DEBUG theme label (added below) shows the actual
            // selected theme regardless of fallback, so the user can
            // see which themes still need their renderer.
            default:                          BuildAtomicLab();    break;
        }

        AddVersionLabel();
        AddDebugThemeLabel();
    }

    /// <summary>
    /// Test-only overlay (TODO remove for public release): paints the
    /// currently-selected Theme name in small gray text near the
    /// bottom of the dial canvas, so we can see at a glance whether
    /// the renderer matches the selection. Six of the twelve themes
    /// still fall back to Atomic Lab visuals; with this label visible,
    /// the mismatch is obvious (label says "Slab", visual is Atomic
    /// Lab).
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
    /// dial canvas. Per Dan's spec ("version should be in the clock
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
        var nowUtc = DateTime.UtcNow;
        var local  = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZone);

        var hour   = (local.Hour % 12) + (local.Minute / 60.0) + (local.Second / 3600.0);
        var minute = local.Minute + (local.Second / 60.0) + (local.Millisecond / 60_000.0);
        var second = local.Second + (SmoothSecondHand ? local.Millisecond / 1000.0 : 0);

        if (_hourRotate   is not null) _hourRotate.Angle   = hour   * 30.0;
        if (_minuteRotate is not null) _minuteRotate.Angle = minute *  6.0;
        if (_secondRotate is not null) _secondRotate.Angle = second *  6.0;

        if (_digitalReadout is not null)
            _digitalReadout.Text = local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        if (_dateReadout is not null)
            _dateReadout.Text = local
                .ToString("ddd · MMMM d", CultureInfo.InvariantCulture)
                .ToUpperInvariant();
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
