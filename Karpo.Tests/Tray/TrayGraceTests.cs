// The person, 2026-10-08: "the whatsapp tray icon sometimes disappears and appears again".
//
// WhatsApp's packaged app draws its icon with H.NotifyIcon, which answers every TaskbarCreated (and
// every re-creation of its icon) by taking the icon away and making it again: NIM_DELETE twice, an
// add with NIS_HIDDEN, its version, then a change that shows it (H.NotifyIcon's TaskbarIcon.
// OnTaskbarCreated, TrayIcon.Create). Karpo announces TaskbarCreated when it starts, when its icon
// size changes and (until §273) whenever Explorer's taskbar had come in front of its tray, and
// the dock read the icons between those calls: the icon went and came back. An icon taken away,
// hidden by its app or left by a window that went now stays where it stands for a moment
// (TrayPictures.GraceMs) first. Measured on a clock of the tests' own (Tray.Clock).

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.TrayIcons;

/// <summary>
/// The tray's icons (<c>Tray.Entries</c>) and its clock (<c>Tray.Clock</c>) are one for the whole
/// process, as the spy's window procedure needs them: every class that adds icons or reads
/// <c>Tray.Icons</c> runs here, alone. Two of them side by side changed one list at once, and
/// <c>Tray.Icons</c> read an icon that was half gone (a NullReferenceException in Promoted).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OneTray
{
    public const string Name = "one tray";
}

[Collection(OneTray.Name)]
public sealed unsafe class TrayGraceTests : IDisposable
{
    private const uint Add = 0;
    private const uint Modify = 1;
    private const uint Delete = 2;
    private const uint SetVersion = 4;

    // H.NotifyIcon's add: NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_STATE | NIF_GUID, state NIS_HIDDEN.
    private const uint Created = 0x01 | 0x02 | 0x04 | 0x08 | 0x20;

    // A plain add: NIF_MESSAGE | NIF_ICON | NIF_TIP, no state, no GUID.
    private const uint Plain = 0x01 | 0x02 | 0x04;

    private const uint State = 0x08 | 0x20;
    private const uint ByGuid = 0x20;

    private readonly Func<long> realClock = Karpo.Dock.Tray.Clock;
    private readonly List<HWND> owners = [];
    private readonly List<Guid> guids = [];
    private long now = 1_000_000;

    public TrayGraceTests() => Karpo.Dock.Tray.Clock = () => now;

    public void Dispose()
    {
        Karpo.Dock.Tray.Clock = realClock;
        Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid is { } guid && guids.Contains(guid) || owners.Contains(entry.Owner));
        foreach (var owner in owners)
        {
            DestroyWindow(owner);
        }
    }

    [Fact]
    public void WhatsAppsDeleteAndAddOnTaskbarCreatedNeverTakesItsIconOffTheBar()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);

        // Made and shown, as H.NotifyIcon makes it.
        MakeAsHNotifyIcon(owner, guid, icon);
        var before = Assert.Single(tray.Icons, listed => listed.Guid == guid);

        // TaskbarCreated: taken away twice, added hidden, its version, shown. Read after every call.
        now += 10;
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, owner, ByGuid, 0, HICON.NULL, guid)));
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, owner, ByGuid, 0, HICON.NULL, guid)));
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        now += 10;
        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Created, 0x8001, icon, guid, state: 1, mask: 1)));
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(SetVersion, HWND.NULL, ByGuid, 0, HICON.NULL, guid, version: 4)));
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, State, 0, HICON.NULL, guid, state: 0, mask: 1)));

        // Long after: still there, the same icon, as it was before.
        now += 10 * TrayPictures.GraceMs;
        var after = Assert.Single(tray.Icons, listed => listed.Guid == guid);
        Assert.Equal(before.Key, after.Key);
        Assert.Equal(before.Identity, after.Identity);
        Assert.Equal(4u, after.Version);
        var entry = Assert.Single(Karpo.Dock.Tray.Entries, entry => entry.IconGuid == guid);
        Assert.False(entry.Deleted);
        Assert.False(entry.Hidden);
        Assert.Null(entry.HiddenAt);
    }

    [Fact]
    public void AnIconTakenAwayStaysForTheGraceAndThenGoes()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
        MakeAsHNotifyIcon(owner, guid, icon);

        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, owner, ByGuid, 0, HICON.NULL, guid)));
        now += TrayPictures.GraceMs - 1;
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        now += 1;
        Assert.DoesNotContain(tray.Icons, listed => listed.Guid == guid);
        Assert.DoesNotContain(Karpo.Dock.Tray.Entries, entry => entry.IconGuid == guid);
    }

    [Fact]
    public void AChangeToAnIconTakenAwayIsRefusedAndMakesNoSecondIcon()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        MakeAsHNotifyIcon(owner, guid, icon);

        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, owner, ByGuid, 0, HICON.NULL, guid)));
        // Refused, so its app adds it again; Explorer has none either.
        Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, Created & ~0x08u, 0x8001, icon, guid)));
        var entry = Assert.Single(Karpo.Dock.Tray.Entries, entry => entry.IconGuid == guid);
        Assert.True(entry.Deleted);
        // Its add brings it back.
        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Plain | ByGuid, 0x8001, icon, guid)));
        Assert.False(entry.Deleted);
        Assert.Same(entry, Assert.Single(Karpo.Dock.Tray.Entries, found => found.IconGuid == guid));
    }

    [Fact]
    public void AnIconItsAppHidesStaysForTheGraceAndComesBackWhenShown()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
        MakeAsHNotifyIcon(owner, guid, icon);

        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, State, 0, HICON.NULL, guid, state: 1, mask: 1)));
        now += TrayPictures.GraceMs / 2;
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        now += TrayPictures.GraceMs;
        Assert.DoesNotContain(tray.Icons, listed => listed.Guid == guid);
        // Still the tray's, hidden: shown again, it is back at once.
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, State, 0, HICON.NULL, guid, state: 0, mask: 1)));
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
    }

    [Fact]
    public void AnIconAddedHiddenNeverShows()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);

        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Created, 0x8001, icon, guid, state: 1, mask: 1)));

        Assert.DoesNotContain(tray.Icons, listed => listed.Guid == guid);
        Assert.Null(Assert.Single(Karpo.Dock.Tray.Entries, entry => entry.IconGuid == guid).HiddenAt);
    }

    [Fact]
    public void AnIconWhoseWindowWentStaysForTheGraceAndThenGoes()
    {
        var owner = Owner();
        var guid = Guid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
        MakeAsHNotifyIcon(owner, guid, icon);

        DestroyWindow(owner);
        Assert.Single(tray.Icons, listed => listed.Guid == guid);
        now += TrayPictures.GraceMs;
        Assert.DoesNotContain(tray.Icons, listed => listed.Guid == guid);
    }

    [Fact]
    public void AnIconAddedAgainFromANewWindowTakesTheOldOnesPlaceAtOnce()
    {
        // No GUID: the window and the uID name the icon, so the app's new window makes a new one.
        // The old one, kept for the grace, goes the moment its successor is there, not after it.
        var before = Owner();
        var after = Owner();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, before, Plain, 0x8001, icon, System.Guid.Empty)));
        var first = Assert.Single(tray.Icons, listed => listed.Owner == before);

        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, before, 0, 0, HICON.NULL, System.Guid.Empty)));
        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, after, Plain, 0x8001, icon, System.Guid.Empty)));

        var listed = tray.Icons.Where(listed => listed.Owner == before || listed.Owner == after).ToList();
        var only = Assert.Single(listed);
        Assert.Equal(after, only.Owner);
        Assert.Equal(first.Identity, only.Identity);
    }

    [Theory]
    [InlineData(0L, true, TrayPictures.GraceMs)]
    [InlineData(1499L, true, 1L)]
    [InlineData(1500L, false, 0L)]
    [InlineData(9000L, false, 0L)]
    public void TheGraceIsAMomentLong(long since, bool kept, long left)
    {
        Assert.Equal(kept, TrayPictures.Kept(100, 100 + since));
        Assert.Equal(left, TrayPictures.Left(100, 100 + since));
    }

    /// <summary>Made and shown as H.NotifyIcon makes an icon: an add, hidden; its version; shown.</summary>
    private void MakeAsHNotifyIcon(HWND owner, Guid guid, HICON icon)
    {
        Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Created, 0x8001, icon, guid, state: 1, mask: 1)));
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(SetVersion, HWND.NULL, ByGuid, 0, HICON.NULL, guid, version: 4)));
        Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, State, 0, HICON.NULL, guid, state: 0, mask: 1)));
    }

    private Guid Guid()
    {
        var guid = System.Guid.NewGuid();
        guids.Add(guid);
        return guid;
    }

    private static Karpo.Dock.Tray.TrayData Payload(
        uint message, HWND owner, uint flags, uint callback, HICON icon, Guid guid, uint version = 0, uint state = 0, uint mask = 0)
    {
        return new Karpo.Dock.Tray.TrayData
        {
            Signature = 0x3475_3423,
            Message = message,
            Size = (uint)sizeof(Karpo.Dock.Tray.TrayData),
            Hwnd = (uint)(nint)owner,
            Uid = 7,
            Flags = flags,
            Callback = callback,
            Icon = (uint)(nint)icon,
            State = state,
            StateMask = mask,
            Version = version,
            Guid = guid,
        };
    }

    /// <summary>A window of this test's own that owns the icons, so the tray keeps them.</summary>
    private HWND Owner()
    {
        fixed (char* type = "Static")
        {
            var window = CreateWindowExW(0, type, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            owners.Add(window);
            return window;
        }
    }
}
