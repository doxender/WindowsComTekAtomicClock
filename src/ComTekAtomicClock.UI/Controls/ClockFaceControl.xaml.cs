// ComTekAtomicClock.UI.Controls.ClockFaceControl
//
// First-cut Atomic Lab analog face. UserControl style for now —
// dedicated templated Control with per-theme ControlTemplates lands
// in the next step when we add Boulder Slate and the digital-only
// themes that need fundamentally different visual structures.
//
// Responsibilities:
//   - Generate 60 minute ticks + 12 hour ticks programmatically
//     (cleaner than 72 hand-written <Line> elements in XAML).
//   - Tick on a DispatcherTimer (50 ms = ~20 fps for a smooth-sweep
//     second hand by default, matching the Atomic Lab "instrument"
//     identity per design/README.md).
//   - Update RotateTransform.Angle on each hand and the digital
//     readout text.
//   - Render time in the bound time zone (defaults to local).

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ComTekAtomicClock.UI.Controls;

public partial class ClockFaceControl : UserControl
{
    // ---------------------------------------------------------------
    // DependencyProperties so each tab can bind its own zone / motion.
    // ---------------------------------------------------------------

    public static readonly DependencyProperty TimeZoneProperty =
        DependencyProperty.Register(
            nameof(TimeZone),
            typeof(TimeZoneInfo),
            typeof(ClockFaceControl),
            new PropertyMetadata(TimeZoneInfo.Local));

    /// <summary>
    /// IANA time zone driving this face. Defaults to local.
    /// Bound from the parent <see cref="ViewModels.TabViewModel"/>.
    /// </summary>
    public TimeZoneInfo TimeZone
    {
        get => (TimeZoneInfo)GetValue(TimeZoneProperty);
        set => SetValue(TimeZoneProperty, value);
    }

    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(
            nameof(Theme),
            typeof(Shared.Settings.Theme),
            typeof(ClockFaceControl),
            new PropertyMetadata(Shared.Settings.Theme.AtomicLab));

    /// <summary>
    /// The active visual theme. Only <c>AtomicLab</c> is rendered in
    /// this commit; the other 11 themes log a debug warning and fall
    /// back to Atomic Lab visuals until each is implemented in
    /// subsequent commits. The value is still persisted per-tab so
    /// the user's selection survives restarts.
    /// </summary>
    public Shared.Settings.Theme Theme
    {
        get => (Shared.Settings.Theme)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public static readonly DependencyProperty SmoothSecondHandProperty =
        DependencyProperty.Register(
            nameof(SmoothSecondHand),
            typeof(bool),
            typeof(ClockFaceControl),
            new PropertyMetadata(true, OnSmoothSecondHandChanged));

    /// <summary>
    /// True for sub-second sweep (~20 fps), false for 1 Hz step.
    /// Atomic Lab's theme default is smooth — see design/README.md.
    /// </summary>
    public bool SmoothSecondHand
    {
        get => (bool)GetValue(SmoothSecondHandProperty);
        set => SetValue(SmoothSecondHandProperty, value);
    }

    private static void OnSmoothSecondHandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClockFaceControl c && c._timer != null)
        {
            c._timer.Interval = (bool)e.NewValue
                ? TimeSpan.FromMilliseconds(50)
                : TimeSpan.FromSeconds(1);
        }
    }

    private readonly DispatcherTimer _timer;

    public ClockFaceControl()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            // 50 ms gives a visually smooth sweep without burning CPU.
            // For "stepped" motion we'll reset to TimeSpan.FromSeconds(1).
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _timer.Tick += OnTimerTick;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildTicks();
        UpdateClock(); // immediate paint so the user doesn't see --:--:-- flash
        _timer.Interval = SmoothSecondHand
            ? TimeSpan.FromMilliseconds(50)
            : TimeSpan.FromSeconds(1);
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e) => UpdateClock();

    /// <summary>
    /// Compute the current time in the bound time zone, derive
    /// hour / minute / second hand angles, and update the visuals.
    /// </summary>
    private void UpdateClock()
    {
        var nowUtc = DateTime.UtcNow;
        var local  = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TimeZone);

        // Fractional positions for smooth motion. The hour hand
        // moves continuously between numbers; the minute hand moves
        // continuously past each minute mark. The second hand
        // sweeps sub-second when SmoothSecondHand is true.
        var hour       = (local.Hour % 12) + (local.Minute / 60.0) + (local.Second / 3600.0);
        var minute     = local.Minute + (local.Second / 60.0) + (local.Millisecond / 60_000.0);
        var second     = local.Second + (SmoothSecondHand ? local.Millisecond / 1000.0 : 0);

        HourHandRotate.Angle   = hour   * 30.0;   //   12 hours -> 360 deg, so 30 deg/hour
        MinuteHandRotate.Angle = minute * 6.0;    //   60 min   -> 360 deg, so  6 deg/min
        SecondHandRotate.Angle = second * 6.0;    //   60 sec   -> 360 deg, so  6 deg/sec

        DigitalReadout.Text = local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        // Date line above the time. Format: WED · APRIL 29
        // (3-letter day · full month · day-of-month). Uppercase to
        // match the NIST · BOULDER · CO legend's typographic register.
        // InvariantCulture for now; localization is on the deferred
        // list in the requirements doc.
        DateReadout.Text = local
            .ToString("ddd · MMMM d", CultureInfo.InvariantCulture)
            .ToUpperInvariant();
    }

    /// <summary>
    /// Programmatically generate the 60 minute ticks (thin amber,
    /// 50% opacity) and 12 hour ticks (thicker amber, full opacity)
    /// per the SVG reference. Anchored to the dial canvas at
    /// (200, 200) center, radius 160 outer edge.
    /// </summary>
    private void BuildTicks()
    {
        const double cx = 200, cy = 200;
        const double rOuter = 160;
        const double rMinuteInner = 155;
        const double rHourInner   = 145;

        var amberBrush = (Brush)Resources["NumbersBrush"];

        // Hour ticks (12)
        for (var hour = 0; hour < 12; hour++)
        {
            var angle = hour * 30 * Math.PI / 180.0;
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);
            var line = new Line
            {
                X1 = cx + sin * rOuter,
                Y1 = cy - cos * rOuter,
                X2 = cx + sin * rHourInner,
                Y2 = cy - cos * rHourInner,
                Stroke = amberBrush,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Square,
                StrokeEndLineCap   = PenLineCap.Square,
            };
            TicksLayer.Children.Add(line);
        }

        // Minute ticks (48 — skipping the multiples of 5 already
        // covered by hour ticks). Thinner, lower opacity.
        for (var minute = 0; minute < 60; minute++)
        {
            if (minute % 5 == 0) continue;
            var angle = minute * 6 * Math.PI / 180.0;
            var sin = Math.Sin(angle);
            var cos = Math.Cos(angle);
            var line = new Line
            {
                X1 = cx + sin * rOuter,
                Y1 = cy - cos * rOuter,
                X2 = cx + sin * rMinuteInner,
                Y2 = cy - cos * rMinuteInner,
                Stroke = amberBrush,
                StrokeThickness = 1,
                Opacity = 0.55,
            };
            TicksLayer.Children.Add(line);
        }
    }
}
