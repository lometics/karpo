// The person, 2026-10-22: "the steam icon is appearing and disappearing again. why is that? is that a
// steam issue? is there a way to fix that?"
//
// Seen live: every three seconds Steam's icon came onto the bar for a moment and went, and with it a
// burst of three to six TaskbarCreated. Explorer's hidden taskbar steps in front of Karpo's tray window
// whenever its tray takes an icon in; Karpo took its place back by asking every app for its icons again
// (Tray.EnsureFirst), and Steam answers that by taking its icon away at once and adding it again about
// three seconds later, which brought Explorer's taskbar in front again. Now the place is taken back
// without a word, and only the first look after an asking may ask once more (TrayFirst).

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayFirstTests
{
    [Fact]
    public void AFirstTrayDoesNothing()
    {
        var front = new TrayFirst();
        Assert.Equal(TrayLook.Nothing, front.Look(first: true));
        front.Asked();
        Assert.Equal(TrayLook.Nothing, front.Look(first: true));
    }

    [Fact]
    public void ATrayPutBehindIsRaisedWithoutAskingTheApps()
    {
        var front = new TrayFirst();
        for (var look = 0; look < 5; look++)
        {
            Assert.Equal(TrayLook.Raise, front.Look(first: false));
        }
    }

    [Fact]
    public void OnlyTheFirstLookAfterAnAskingAsksAgainAndOnlyOnce()
    {
        var front = new TrayFirst();
        front.Asked();
        Assert.Equal(TrayLook.AskAgain, front.Look(first: false));
        // Asking again is no new asking: it can't feed itself.
        Assert.Equal(TrayLook.Raise, front.Look(first: false));
        Assert.Equal(TrayLook.Raise, front.Look(first: false));
    }

    [Fact]
    public void ALookThatFindsTheTrayFirstEndsTheAnswering()
    {
        var front = new TrayFirst();
        front.Asked();
        Assert.Equal(TrayLook.Nothing, front.Look(first: true));
        Assert.Equal(TrayLook.Raise, front.Look(first: false));
    }
}

/// <summary>
/// Steam's answer to TaskbarCreated played through the tray (<c>Tray.Apply</c>, on the tests' own clock)
/// with Explorer's taskbar stepping in front for every icon its tray takes in or lets go, and the dock
/// looking at the tray's place after each (the arrange that follows).
/// </summary>
[Collection(OneTray.Name)]
public sealed unsafe class TraySteamTests : IDisposable
{
    private const uint Add = 0;
    private const uint Delete = 2;

    // Steam's add: NIF_MESSAGE | NIF_ICON | NIF_TIP, uID 1, no GUID.
    private const uint Plain = 0x01 | 0x02 | 0x04;

    // Measured: Steam adds its icon again about 2.8 s after the last TaskbarCreated.
    private const long SteamAddsAfterMs = 2_850;

    private readonly Func<long> realClock = Karpo.Dock.Tray.Clock;
    private readonly HWND owner;
    private long now = 5_000_000;

    public TraySteamTests()
    {
        Karpo.Dock.Tray.Clock = () => now;
        fixed (char* type = "Static")
        {
            owner = CreateWindowExW(0, type, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
        }
        Assert.True(owner != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
    }

    public void Dispose()
    {
        Karpo.Dock.Tray.Clock = realClock;
        Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.Owner == owner);
        DestroyWindow(owner);
    }

    [Fact]
    public void SteamsIconStaysOnTheBarOnceKarpoHasStarted()
    {
        var front = new TrayFirst();
        var (shown, askings) = Play(front.Asked, front.Look);

        // Gone only while Karpo starts (Steam's own three seconds, the icon not yet Karpo's), then there
        // for good; asked at the start and once more after the takeover, never again.
        Assert.True(shown.Skip(100).All(listed => listed), Timeline(shown));
        Assert.Equal(2, askings);
    }

    [Fact]
    public void AskingTheAppsWheneverTheTrayIsPutBehindKeptSteamsIconComingAndGoing()
    {
        // The rule before: every look that found Explorer's taskbar in front asked every app again.
        var (shown, askings) = Play(() => { }, first => first ? TrayLook.Nothing : TrayLook.AskAgain);

        Assert.Contains(false, shown.Skip(100));
        Assert.Contains(true, shown.Skip(100));
        Assert.True(askings > 10, $"{askings} askings");
    }

    /// <summary>
    /// 60 s in steps of 100 ms: whether Steam's icon was on the bar at each, and how many times every app
    /// was asked for its icons. Karpo starts with Steam's icon already in Explorer's tray: it asks, and
    /// its takeover puts Explorer's taskbar in front.
    /// </summary>
    private (List<bool> Shown, int Askings) Play(Action asked, Func<bool, TrayLook> look)
    {
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        // Explorer's tray: whether it holds Steam's icon, and whether its taskbar stands in front of
        // Karpo's tray window, which it does whenever it takes an icon in or lets one go; the dock
        // looks at the tray's place the step after (the arrange that follows).
        var inExplorer = true;
        var explorerInFront = false;
        var lookDue = false;
        long? steamAdds = null;
        var askings = 0;

        void InFront()
        {
            explorerInFront = true;
            lookDue = true;
        }

        void TaskbarCreated()
        {
            askings++;
            // Steam: its icon taken away at once, added again a while after the last asking.
            Karpo.Dock.Tray.Apply(Payload(Delete, 0, HICON.NULL));
            if (inExplorer)
            {
                inExplorer = false;
                InFront();
            }
            steamAdds = now + SteamAddsAfterMs;
        }

        // Karpo starts: every app asked, then Explorer's taskbar hidden, which puts it in front.
        asked();
        TaskbarCreated();
        InFront();

        var shown = new List<bool>();
        for (var step = 0; step < 600; step++)
        {
            now += 100;
            if (steamAdds is { } due && now >= due)
            {
                steamAdds = null;
                Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, Plain, icon)));
                if (!inExplorer)
                {
                    inExplorer = true;
                    InFront();
                }
            }
            else if (lookDue)
            {
                lookDue = false;
                switch (look(!explorerInFront))
                {
                    case TrayLook.Raise:
                        explorerInFront = false;
                        break;
                    case TrayLook.AskAgain:
                        explorerInFront = false;
                        TaskbarCreated();
                        break;
                }
            }
            shown.Add(tray.Icons.Any(listed => listed.Owner == owner));
        }
        return (shown, askings);
    }

    private static string Timeline(List<bool> shown) => string.Concat(shown.Select(listed => listed ? '#' : '.'));

    private Karpo.Dock.Tray.TrayData Payload(uint message, uint flags, HICON icon) => new()
    {
        Signature = 0x3475_3423,
        Message = message,
        Size = (uint)sizeof(Karpo.Dock.Tray.TrayData),
        Hwnd = (uint)(nint)owner,
        Uid = 1,
        Flags = flags,
        Callback = 0x8001,
        Icon = (uint)(nint)icon,
    };
}
