// Regression test for the v1.1.7 fix: App.OnDispatcherUnhandledException
// must recognize the benign DWM "composition disabled" COMException
// (HRESULT 0x80263001) and recover it silently, while still treating every
// other exception as a real fault. We test the pure classifier directly.

using System.Runtime.InteropServices;
using ComTekAtomicClock.UI;

namespace ComTekAtomicClock.Tests;

public class AppExceptionHandlingTests
{
    private const int DwmCompositionDisabled = unchecked((int)0x80263001);
    private const int EFail                  = unchecked((int)0x80004005);

    [Fact]
    public void CompositionDisabledComException_IsBenign()
    {
        var ex = new COMException("desktop composition is disabled", DwmCompositionDisabled);
        Assert.True(App.IsBenignCompositionGlitch(ex));
    }

    [Fact]
    public void OtherComException_IsNotBenign()
    {
        var ex = new COMException("generic failure", EFail);
        Assert.False(App.IsBenignCompositionGlitch(ex));
    }

    [Fact]
    public void NonComException_IsNotBenign() =>
        Assert.False(App.IsBenignCompositionGlitch(new InvalidOperationException("boom")));

    [Fact]
    public void NonComException_EvenWithMatchingHResult_IsNotBenign()
    {
        // A plain Exception carrying the same HResult must NOT be swallowed —
        // only the COMException from the DWM glass-frame path is benign.
        var ex = new Exception("not a COM error") { HResult = DwmCompositionDisabled };
        Assert.False(App.IsBenignCompositionGlitch(ex));
    }
}
