// An icon Karpo's tray never saw added (Karpo.Dock\Tray.cs, Tray.Apply).
//
// Karpo announces TaskbarCreated when it starts, and most apps add their icons again. Windows
// Security's tray app (SecurityHealthSystray) does not: it only changes its icon — NIM_MODIFY with a
// picture, a tooltip and its GUID, but no callback message — and adds it again only where that change
// fails. Recorded on a desktop of its own, with a tray that answered as Explorer does (the change
// worked) and then as Karpo's tray does now (it failed). Answered as Explorer answered, the app never
// added it again, Karpo never learnt its callback message, and an icon without one is never shown:
// the shield, and the "action recommended" badge on it, were missing from Karpo's bar.

using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.TrayIcons;

[Collection(OneTray.Name)]
public sealed unsafe class TrayReAddTests
{
    private const uint Add = 0;
    private const uint Modify = 1;
    private const uint Delete = 2;
    private const uint SetVersion = 4;

    // NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_STATE | NIF_GUID | NIF_SHOWTIP, and the same without NIF_MESSAGE.
    private const uint Full = 0xAF;
    private const uint NoMessage = 0xAE;

    /// <summary>
    /// What Windows Security's tray app sent after TaskbarCreated, in order: a change Karpo's tray
    /// can't take is refused, the add it brings is taken and answered as done, and the icon is shown
    /// with its callback message and version.
    /// </summary>
    [Fact]
    public void AnAppThatOnlyChangesItsIconIsAskedToAddItAgain()
    {
        var owner = Owner();
        var guid = Guid.NewGuid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        try
        {
            // The change after TaskbarCreated: refused, so the app adds it again.
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "Windows Security - Actions recommended.")));
            // Its add, with the callback message: taken, and answered as done whatever Explorer says.
            Assert.True(Karpo.Dock.Tray.Apply(Payload(Add, owner, Full, 0x460, icon, guid, "")));
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(SetVersion, owner, Full, 0x460, icon, guid, "", version: 4)));
            var entry = Karpo.Dock.Tray.Entries.Single(entry => entry.IconGuid == guid);
            Assert.Equal(0x460u, entry.Callback);
            Assert.Equal(4u, entry.Version);
            Assert.NotNull(entry.Pixels);

            // Shown: the tray lists it (in the overflow or not, as Windows' own choice says).
            var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
            Assert.Contains(tray.Icons, shown => shown.Guid == guid && shown.CallbackMessage == 0x460);

            // From now on its changes are Explorer's to answer, and the badge's picture is taken.
            var before = entry.Pixels;
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "Windows Security - Actions recommended.")));
            Assert.NotSame(before, entry.Pixels);
            Assert.Equal("Windows Security - Actions recommended.", entry.Tip);
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(owner);
        }
    }

    /// <summary>
    /// Before it is added, the icon is never shown, whatever its changes say: without a callback
    /// message a click on it would reach nobody. A change without even a picture is refused too.
    /// </summary>
    [Fact]
    public void AnIconWithoutACallbackIsNotShownAndItsChangesAreRefused()
    {
        var owner = Owner();
        var guid = Guid.NewGuid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        try
        {
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, 0x24 /* NIF_TIP | NIF_GUID */, 0, HICON.NULL, guid, "tip only")));
            Assert.DoesNotContain(Karpo.Dock.Tray.Entries, entry => entry.IconGuid == guid);
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "picture")));
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "picture again")));
            var tray = new Karpo.Dock.Tray(HWND.NULL, HWND.NULL);
            Assert.DoesNotContain(tray.Icons, shown => shown.Guid == guid);
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(owner);
        }
    }

    /// <summary>
    /// An app that tries again whenever a call fails, and adds its icon without a callback message:
    /// its first change is refused, the add it brings is taken, and from then on its changes are
    /// Explorer's to answer. They were all refused while the icon had no callback, so such an app
    /// would have added it again on every change, for ever.
    /// </summary>
    [Fact]
    public void ARetryingAppsChangesAreTakenOnceItHasAddedItsIcon()
    {
        var owner = Owner();
        var guid = Guid.NewGuid();
        var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION);
        try
        {
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "first")));
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Add, owner, NoMessage, 0, icon, guid, "added")));
            var entry = Karpo.Dock.Tray.Entries.Single(entry => entry.IconGuid == guid);
            Assert.True(entry.Added);
            for (var change = 0; change < 3; change++)
            {
                Assert.Null(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, $"change {change}")));
                Assert.Equal($"change {change}", entry.Tip);
            }
            // Gone and back: refused again until it is added again.
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Delete, owner, NoMessage, 0, HICON.NULL, guid, "")));
            Assert.False(Karpo.Dock.Tray.Apply(Payload(Modify, owner, NoMessage, 0, icon, guid, "after")));
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(owner);
        }
    }

    /// <summary>The answer on its own: refused only before the add, and only while there is no callback to click.</summary>
    [Theory]
    [InlineData(Add, false, true, true)]
    [InlineData(Add, false, false, null)]
    [InlineData(Modify, false, false, false)]
    [InlineData(Modify, false, true, null)]
    [InlineData(Modify, true, false, null)]
    [InlineData(Modify, true, true, null)]
    public void AChangeIsRefusedOnlyBeforeTheAdd(uint message, bool added, bool hasCallback, bool? answer) =>
        Assert.Equal(answer, Karpo.Dock.Tray.Answer(message, added, hasCallback));

    /// <summary>An add without a callback message is Explorer's to answer: Karpo has nothing of its own to say.</summary>
    [Fact]
    public void AnAddWithoutACallbackIsExplorersToAnswer()
    {
        var owner = Owner();
        var guid = Guid.NewGuid();
        try
        {
            Assert.Null(Karpo.Dock.Tray.Apply(Payload(Add, owner, NoMessage, 0, LoadIconW(HINSTANCE.NULL, IDI.IDI_APPLICATION), guid, "")));
        }
        finally
        {
            Karpo.Dock.Tray.Entries.RemoveAll(entry => entry.IconGuid == guid);
            DestroyWindow(owner);
        }
    }

    private static Karpo.Dock.Tray.TrayData Payload(uint message, HWND owner, uint flags, uint callback, HICON icon, Guid guid, string tip, uint version = 0)
    {
        var payload = new Karpo.Dock.Tray.TrayData
        {
            Signature = 0x3475_3423,
            Message = message,
            Size = (uint)sizeof(Karpo.Dock.Tray.TrayData),
            Hwnd = (uint)(nint)owner,
            Uid = 100,
            Flags = flags,
            Callback = callback,
            Icon = (uint)(nint)icon,
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
