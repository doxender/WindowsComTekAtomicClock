// ComTekAtomicClock.Service.Sync.SystemTime
//
// P/Invoke wrapper for kernel32!SetSystemTime. Calling this requires
// SE_SYSTEMTIME_NAME privilege; LocalSystem (the account that runs the
// Windows Service per requirements.txt § 2.3) holds it by default, so
// no explicit AdjustTokenPrivileges call is needed.
//
// SetSystemTime takes UTC. The Service computes UTC = system clock
// + offset and passes that in.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ComTekAtomicClock.Service.Sync;

[SupportedOSPlatform("windows")]
internal static class SystemTime
{
    /// <summary>
    /// Set the local machine's UTC clock to <paramref name="utcMoment"/>.
    /// Returns true on success. On failure, the Win32 last-error code
    /// is in <paramref name="win32Error"/>.
    /// </summary>
    public static bool TrySetUtcNow(DateTime utcMoment, out int win32Error)
    {
        if (utcMoment.Kind == DateTimeKind.Local)
            utcMoment = utcMoment.ToUniversalTime();
        else if (utcMoment.Kind == DateTimeKind.Unspecified)
            utcMoment = DateTime.SpecifyKind(utcMoment, DateTimeKind.Utc);

        var st = new SYSTEMTIME
        {
            Year         = (ushort)utcMoment.Year,
            Month        = (ushort)utcMoment.Month,
            DayOfWeek    = (ushort)utcMoment.DayOfWeek,
            Day          = (ushort)utcMoment.Day,
            Hour         = (ushort)utcMoment.Hour,
            Minute       = (ushort)utcMoment.Minute,
            Second       = (ushort)utcMoment.Second,
            Milliseconds = (ushort)utcMoment.Millisecond,
        };

        var ok = SetSystemTimeNative(ref st);
        win32Error = ok ? 0 : Marshal.GetLastWin32Error();
        return ok;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [DllImport("kernel32.dll", EntryPoint = "SetSystemTime", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSystemTimeNative(ref SYSTEMTIME lpSystemTime);
}
