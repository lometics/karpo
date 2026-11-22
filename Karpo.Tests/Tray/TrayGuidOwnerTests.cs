// Not all tray icons showed (the person, 2026-10-05: "not all of the tray icons show in the tray icons
// widget"). WhatsApp's packaged app draws its icon with H.NotifyIcon, which adds it with its window and
// a GUID — hidden at first (NIS_HIDDEN) — and then sets its version, makes it visible and changes its
// picture and tooltip by the GUID alone, the window left 0. Karpo's tray took that 0 for the icon's new
// window, and an icon whose window is gone is dropped (Tray.Icons): WhatsApp's icon went the moment it
// was made visible. Recorded from H.NotifyIcon 2.3.0's TrayIconMethods, the version WhatsApp ships.
//
// And the list the bars draw from has every icon the tray has, whatever its state, but the ones hidden
// completely and Windows' update icon where the status icons stand for it.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.TrayIcons;

[Collection(OneTray.Name)]
public sealed unsafe class TrayGuidOwnerTests
{
    private const uint Add = 0;
    private const uint Modify = 1;
    private const uint SetVersion = 4;

    // H.NotifyIcon's add: NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_STATE | NIF_GUID, state NIS_HIDDEN.
    private const uint Created = 0x01 | 0x02 | 0x04 | 0x08 | 0x20;

    // Its changes by GUID alone: the state (NIF_STATE | NIF_GUID), the picture (NIF_ICON | NIF_GUID), the tooltip (NIF_TIP | NIF_GUID).
    private const uint State = 0x08 | 0x20;
    private const uint Picture = 0x02 | 0x20;
    private const uint Tooltip = 0x04 | 0x20;
    private const uint ByGuid = 0x20;

    [Fact]
    public void AGuidIconChangedWithoutItsWindowKeepsItsWindowAndShows()
    {
        var owner = Owner();
        var guid = Guid.NewGuid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        try
        {
            // The add, hidden, with its window and callback: taken and answered as done.
            Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Created, 0x8001, icon, guid, "WhatsApp", state: 1, mask: 1)));
            // Its version, then visible, then a new picture and tooltip: all by the GUID, the window 0.
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(SetVersion, HWND.NULL, ByGuid, 0, HICON.NULL, guid, "", version: 4)));
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, State, 0, HICON.NULL, guid, "", state: 0, mask: 1)));
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, Picture, 0, icon, guid, "")));
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, HWND.NULL, Tooltip, 0, HICON.NULL, guid, "WhatsApp - 3 unread")));

            var entry = Karpo.Dock.Tray.Entries.Single(entry => entry.IconGuid == guid);
            Assert.Equal(owner, entry.Owner);
            Assert.False(entry.Hidden);
            Assert.Equal(4u, entry.Version);

            // Listed, with the window its clicks go to.
            var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
            var shown = Assert.Single(tray.Icons, listed => listed.Guid == guid);
            Assert.Equal(owner, shown.Owner);
            Assert.Equal(0x8001u, shown.CallbackMessage);
            Assert.Equal("WhatsApp - 3 unread", shown.Tip);
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(owner);
        }
    }

    [Fact]
    public void AGuidIconAddedFromANewWindowMovesToIt()
    {
        // The app restarted: the same GUID, added from its new window, is that window's now.
        var before = Owner();
        var after = Owner();
        var guid = Guid.NewGuid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        try
        {
            Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, before, Created, 0x8001, icon, guid, "")));
            Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, after, Created, 0x8002, icon, guid, "")));

            var entry = Karpo.Dock.Tray.Entries.Single(entry => entry.IconGuid == guid);
            Assert.Equal(after, entry.Owner);
            Assert.Equal(0x8002u, entry.Callback);
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(before);
            DestroyWindow(after);
        }
    }

    [Theory]
    [InlineData(0x10, 0x10, false)]
    [InlineData(0x10, 0x20, true)]
    [InlineData(0x10, 0, false)]
    [InlineData(0, 0x20, true)]
    public void OnlyACallNamingAnotherWindowMovesTheIcon(int had, int said, bool moves) =>
        Assert.Equal(moves, Karpo.Dock.Tray.OwnerChanged((HWND)(nint)had, (HWND)(nint)said));

    // --- What the bars draw from ------------------------------------------------------------

    [Fact]
    public void EveryIconTheTrayHasIsListedButTheOnesHiddenCompletely()
    {
        var config = new TrayIconOptions();
        config.States["chosen-bar"] = TrayState.Taskbar;
        config.States["chosen-overflow"] = TrayState.Overflow;
        config.States["chosen-hidden"] = TrayState.Hidden;
        TrayEntry[] icons =
        [
            new("chosen-bar", AppHidden: true),
            new("chosen-overflow", AppHidden: false),
            new("chosen-hidden", AppHidden: false),
            new("promoted", AppHidden: false),
            new("not-promoted", AppHidden: true),
        ];

        var (shown, overflow) = TrayLayout.Split(config, icons);

        Assert.Equal(["chosen-bar", "promoted"], shown.Select(icon => icon.Identity));
        Assert.Equal(["chosen-overflow", "not-promoted"], overflow.Select(icon => icon.Identity));
    }

    [Fact]
    public void WindowsUpdateIconIsTheOnlyOneTheStatusIconsTake()
    {
        string[] paths =
        [
            @"C:\Program Files\WindowsApps\5319275A.WhatsAppDesktop_2.2638.102.0_x64__cv1g1gvanyjgm\WhatsApp.Root.exe",
            @"C:\WINDOWS\UUS\amd64\MoNotificationUx.exe",
            @"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic_1.303.264.0_x64__zpdnekdrzrea0\Spotify.exe",
            @"C:\WINDOWS\System32\SecurityHealthSystray.exe",
            "",
        ];

        var held = WindowsUpdateTray.Held(paths, path => path, () => true);
        var alone = WindowsUpdateTray.Held(paths, path => path, () => false);

        Assert.Equal(paths.Where(path => !path.EndsWith("MoNotificationUx.exe", StringComparison.Ordinal)), held);
        Assert.Equal(paths, alone);
    }

    // --- Windows' "not promoted" -------------------------------------------------------------

    [Fact]
    public void WindowsNotPromotedIsTheHiddenIconMenuNeverHiddenCompletely()
    {
        var config = Karpo.Tests.Core.Configs.With(BarKind.Tray.New());
        var tray = TrayLayout.Trays(config).Single();

        Assert.Equal(TrayState.Overflow, TrayLayout.State(tray.TrayIcons, new TrayEntry("a", AppHidden: true)));
        Assert.True(TrayLayout.FollowWindows(config, "a", promoted: false));
        Assert.Equal(TrayState.Overflow, tray.TrayIcons!.States["a"]);
    }

    private static Karpo.Dock.Tray.TrayData Payload(
        uint message, HWND owner, uint flags, uint callback, HICON icon, Guid guid, string tip, uint version = 0, uint state = 0, uint mask = 0)
    {
        var payload = new Karpo.Dock.Tray.TrayData
        {
            Signature = 0x3475_3423,
            Message = message,
            Size = (uint)sizeof(Karpo.Dock.Tray.TrayData),
            Hwnd = (uint)(nint)owner,
            Flags = flags,
            Callback = callback,
            Icon = (uint)(nint)icon,
            State = state,
            StateMask = mask,
            Version = version,
            Guid = guid,
        };
        var text = tip.AsSpan(0, Math.Min(tip.Length, 127));
        text.CopyTo(new Span<char>(payload.Tip, 128));
        return payload;
    }

    /// <summary>A window of this test's own that owns the icons, so the tray keeps them.</summary>
    private static HWND Owner()
    {
        fixed (char* type = "Static")
        {
            var window = CreateWindowExW(0, type, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            return window;
        }
    }
}
