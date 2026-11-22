// A tray icon's tooltip on the bar, and a double click on a tray icon (Karpo.Dock\Tray.cs,
// Karpo.Dock\Dock.cs). A standard tray tooltip shows every line its app gave it, as the taskbar's
// does, where Karpo showed only the first; an app of version 4 that draws a popup of its own when
// the pointer rests on its icon gets no tooltip of Karpo's over it unless it asked for the standard
// one as well (NIF_SHOWTIP), which a change of picture alone does not take back; and a second left
// press on the same icon within the double-click time is a double click, on the bar and in the
// overflow alike.

using Karpo.Dock;
using TerraFX.Interop.Windows;

// Karpo.Dock is the namespace as well as the class, and the namespace wins from in here.
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Islands;

public sealed class TrayTipTests
{
    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;
    private const uint NIF_SHOWTIP = 0x80;

    private static TrayIcon Icon(uint version, string tip, bool showTip = false) =>
        new("{a}", HWND.NULL, 1, Guid.Empty, 0x8001, version, null, tip, false, ShowTip: showTip);

    // --- What the bar shows ----------------------------------------------------------------------

    /// <summary>Every line of a standard tooltip, as the taskbar shows it, whatever the app ends its lines with.</summary>
    [Theory]
    [InlineData("Speakers (Realtek(R) Audio)\r\n50%", "Speakers (Realtek(R) Audio)\n50%")]
    [InlineData("OneDrive\nUp to date\nPersonal", "OneDrive\nUp to date\nPersonal")]
    [InlineData("Wi-Fi\rConnected", "Wi-Fi\nConnected")]
    [InlineData("\n  Updates  \nRestart required\n\n", "  Updates\nRestart required")]
    [InlineData("One line", "One line")]
    public void AStandardTooltipShowsEveryLine(string tip, string shown)
    {
        Assert.Equal(shown, TheDock.TrayTip(Icon(3, tip)));
        Assert.Equal(shown, TheDock.TrayTip(Icon(0, tip)));
        Assert.Equal(shown, TheDock.TrayTip(Icon(4, tip, showTip: true)));
    }

    /// <summary>An icon whose app gave no words still has a tooltip that says what it is.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("\r\n \n")]
    public void AnIconWithoutWordsIsATrayIcon(string tip) => Assert.Equal("Tray icon", TheDock.TrayTip(Icon(3, tip)));

    /// <summary>
    /// A version 4 app that did not ask for the standard tooltip draws a popup of its own when the
    /// pointer rests on its icon: Karpo shows none, so nothing of Karpo's stands over it.
    /// </summary>
    [Fact]
    public void AVersionFourIconWithoutTheStandardTipGetsNone()
    {
        Assert.Null(TheDock.TrayTip(Icon(4, "Teams\nAvailable")));
        Assert.False(Icon(4, "Teams").StandardTip);
        Assert.True(Icon(4, "Teams", showTip: true).StandardTip);
        Assert.True(Icon(3, "Teams").StandardTip);
        Assert.True(Icon(0, "Teams").StandardTip);
    }

    // --- NIF_SHOWTIP, as the app says it ----------------------------------------------------------

    /// <summary>An add says it either way: with the flag, the standard tooltip; without, none.</summary>
    [Fact]
    public void AnAddSaysItEitherWay()
    {
        Assert.True(Tray.ShowsTip(NIM_ADD, NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP));
        Assert.False(Tray.ShowsTip(NIM_ADD, NIF_MESSAGE | NIF_ICON | NIF_TIP));
        Assert.False(Tray.ShowsTip(NIM_ADD, NIF_MESSAGE | NIF_ICON));
    }

    /// <summary>
    /// A change about the tooltip says it; a change of anything else says nothing, and the icon keeps
    /// what it had — an app that asked for the tooltip once does not lose it by changing its picture.
    /// </summary>
    [Fact]
    public void AChangeSaysItOnlyWhereItIsAboutTheTooltip()
    {
        Assert.True(Tray.ShowsTip(NIM_MODIFY, NIF_TIP | NIF_SHOWTIP));
        Assert.True(Tray.ShowsTip(NIM_MODIFY, NIF_SHOWTIP));
        Assert.False(Tray.ShowsTip(NIM_MODIFY, NIF_TIP));
        Assert.Null(Tray.ShowsTip(NIM_MODIFY, NIF_ICON));
        Assert.Null(Tray.ShowsTip(NIM_MODIFY, NIF_MESSAGE));
        Assert.Null(Tray.ShowsTip(NIM_DELETE, NIF_SHOWTIP));
        Assert.Null(Tray.ShowsTip(NIM_SETVERSION, 0));
    }

    // --- A double click ----------------------------------------------------------------------------

    /// <summary>
    /// A second left press on the same icon within the double-click time is a double click; a third
    /// starts a pair of its own; another icon, or a press after the time, is a first press again.
    /// </summary>
    [Fact]
    public void ASecondPressInTimeIsADoubleClick()
    {
        (string Key, uint At)? last = null;
        const uint within = 500;

        Assert.False(TheDock.Doubled(ref last, "{a}", 1_000, within));
        Assert.True(TheDock.Doubled(ref last, "{a}", 1_400, within));
        Assert.False(TheDock.Doubled(ref last, "{a}", 1_600, within));
        Assert.True(TheDock.Doubled(ref last, "{a}", 2_100, within));

        // Another icon between them: each is a first press.
        Assert.False(TheDock.Doubled(ref last, "{a}", 3_000, within));
        Assert.False(TheDock.Doubled(ref last, "{b}", 3_100, within));
        Assert.False(TheDock.Doubled(ref last, "{a}", 3_200, within));

        // Too slow: a first press again.
        Assert.False(TheDock.Doubled(ref last, "{a}", 3_701, within));
        Assert.True(TheDock.Doubled(ref last, "{a}", 3_900, within));

        // Across the tick count's wrap, 49.7 days in.
        last = null;
        Assert.False(TheDock.Doubled(ref last, "{a}", uint.MaxValue - 100, within));
        Assert.True(TheDock.Doubled(ref last, "{a}", 200, within));
    }
}
