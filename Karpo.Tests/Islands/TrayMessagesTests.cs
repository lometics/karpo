// The callback messages a notification icon's app gets for a click or while the
// pointer is on its icon (Karpo.Dock\Tray.cs, TrayMessages): what Explorer
// sends for each version the app asked for, and how version 4 packs them.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class TrayMessagesTests
{
    private const uint Id = 7;
    private static readonly POINT At = new() { x = 1200, y = 1050 };

    private static uint[] Events(TrayGesture gesture, uint version) =>
        TrayMessages.For(gesture, version, Id, At).Select(pair => TrayMessages.EventOf(version, pair.LParam)).ToArray();

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(4u)]
    public void ButtonsGoDownAsThemselves(uint version)
    {
        Assert.Equal([WM.WM_LBUTTONDOWN], Events(TrayGesture.LeftDown, version));
        Assert.Equal([WM.WM_LBUTTONDBLCLK], Events(TrayGesture.LeftDoubleDown, version));
        Assert.Equal([WM.WM_RBUTTONDOWN], Events(TrayGesture.RightDown, version));
    }

    [Fact]
    public void OldIconsGetOnlyTheButtonMessages()
    {
        Assert.Equal([WM.WM_LBUTTONUP], Events(TrayGesture.LeftUp, 0));
        Assert.Equal([WM.WM_RBUTTONUP], Events(TrayGesture.RightUp, 0));
        Assert.Equal([WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP], Events(TrayGesture.KeySelect, 0));
        Assert.Equal([WM.WM_RBUTTONDOWN, WM.WM_RBUTTONUP], Events(TrayGesture.ContextKey, 0));
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(4u)]
    public void NewerIconsGetTheButtonUpAndThenSelectOrTheMenu(uint version)
    {
        // The button's own up message comes first: apps that act on it (the likely
        // cause of tray clicks that did nothing) never got it before.
        Assert.Equal([WM.WM_LBUTTONUP, TrayMessages.NinSelect], Events(TrayGesture.LeftUp, version));
        Assert.Equal([WM.WM_RBUTTONUP, WM.WM_CONTEXTMENU], Events(TrayGesture.RightUp, version));
        Assert.Equal([TrayMessages.NinKeySelect], Events(TrayGesture.KeySelect, version));
        Assert.Equal([WM.WM_CONTEXTMENU], Events(TrayGesture.ContextKey, version));
    }

    [Fact]
    public void BeforeVersionFourWParamIsTheIdAndLParamTheEvent()
    {
        var (wParam, lParam) = TrayMessages.Encode(WM.WM_LBUTTONUP, 3, Id, At);

        Assert.Equal((nuint)Id, wParam);
        Assert.Equal((nint)WM.WM_LBUTTONUP, lParam);
    }

    [Fact]
    public void VersionFourPacksThePointAndTheId()
    {
        var (wParam, lParam) = TrayMessages.Encode(TrayMessages.NinSelect, 4, Id, At);

        Assert.Equal(1200u, (uint)(wParam & 0xFFFF));
        Assert.Equal(1050u, (uint)((wParam >> 16) & 0xFFFF));
        Assert.Equal(TrayMessages.NinSelect, (uint)(lParam & 0xFFFF));
        Assert.Equal(Id, (uint)((lParam >> 16) & 0xFFFF));
    }

    [Fact]
    public void AnIconLeftOfThePrimaryDisplayKeepsItsNegativeX()
    {
        var (wParam, _) = TrayMessages.Encode(WM.WM_LBUTTONDOWN, 4, Id, new POINT { x = -200, y = 900 });

        // GET_X_LPARAM reads the low word as a signed short.
        Assert.Equal(-200, (short)(wParam & 0xFFFF));
        Assert.Equal(900, (short)((wParam >> 16) & 0xFFFF));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(4u)]
    public void EveryVersionHearsThePointerMove(uint version)
    {
        Assert.Equal([WM.WM_MOUSEMOVE], Events(TrayGesture.Move, version));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    public void OnlyVersionFourHearsOfItsPopup(uint version)
    {
        Assert.Empty(TrayMessages.For(TrayGesture.PopupOpen, version, Id, At));
        Assert.Empty(TrayMessages.For(TrayGesture.PopupClose, version, Id, At));
    }

    [Fact]
    public void VersionFourOpensAndClosesItsPopup()
    {
        Assert.Equal([TrayMessages.NinPopupOpen], Events(TrayGesture.PopupOpen, 4));
        Assert.Equal([TrayMessages.NinPopupClose], Events(TrayGesture.PopupClose, 4));
        // WM_USER + 6 and + 7, as shellapi.h has them.
        Assert.Equal((uint)(WM.WM_USER + 6), TrayMessages.NinPopupOpen);
        Assert.Equal((uint)(WM.WM_USER + 7), TrayMessages.NinPopupClose);
    }

    [Fact]
    public void AnOldIconsMoveIsItsIdAndTheMessage()
    {
        var (wParam, lParam) = TrayMessages.For(TrayGesture.Move, 0, Id, At).Single();

        Assert.Equal((nuint)Id, wParam);
        Assert.Equal((nint)WM.WM_MOUSEMOVE, lParam);
    }

    [Fact]
    public void AVersionFourPopupCarriesThePointer()
    {
        var (wParam, lParam) = TrayMessages.For(TrayGesture.PopupOpen, 4, Id, At).Single();

        Assert.Equal(1200, (short)(wParam & 0xFFFF));
        Assert.Equal(1050, (short)((wParam >> 16) & 0xFFFF));
        Assert.Equal(TrayMessages.NinPopupOpen, (uint)(lParam & 0xFFFF));
        Assert.Equal(Id, (uint)((lParam >> 16) & 0xFFFF));
    }
}
