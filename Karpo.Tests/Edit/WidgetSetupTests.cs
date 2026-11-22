// Which widgets show nothing until they are set up (backlog item 24; Karpo.Core\WidgetSetup.cs), and
// how the settings app still reads an older link's field (StartRequest). What sets the field up is the
// widget's menu since §228 (SetupLinkTests).

using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class WidgetSetupTests
{
    /// <summary>Each of them, new, needs its one field: on the bar and as a line alike.</summary>
    [Fact]
    public void EachNewWidgetThatShowsNothingNeedsItsField()
    {
        Assert.Equal(SetupField.Target, WidgetSetup.Needs(BarKind.Pin.New()));
        Assert.Equal(SetupField.Target, WidgetSetup.Needs(ListKind.Pin.New()));
        Assert.Equal(SetupField.Symbol, WidgetSetup.Needs(ListKind.Markets.New()));
        // weather: a new one is for the device's location (2026-10-11); one with no place needs it.
        foreach (var kind in (ListKind[])[ListKind.WeatherNow, ListKind.WeatherHours, ListKind.WeatherDays])
        {
            var line = kind.New();
            Assert.Null(WidgetSetup.Needs(line));
            line.Weather = new WeatherOptions();
            Assert.Equal(SetupField.Place, WidgetSetup.Needs(line));
        }
        // folder pins: a pin that will show a folder's contents asks for its folder as every pin
        // asks for what it opens — the "Folder contents" widget's own field went with it.
        Assert.Equal(SetupField.Target, WidgetSetup.Needs(ListKind.Pin.New()));
        // A heading never asks: with no words it says "Heading" (2026-10-11).
        Assert.Null(WidgetSetup.Needs(ListKind.Heading.New()));
        // rss: a feed with no address shows nothing but that it has none.
        Assert.Equal(SetupField.Feed, WidgetSetup.Needs(ListKind.Rss.New()));
        // drives: a drive or network drive line with no drive shows nothing but that it has none
        // (2026-10-08, "Set up in Settings"), so an add opens its page at the list of drives too.
        Assert.Equal(SetupField.Volume, WidgetSetup.Needs(ListKind.NetworkDrive.New()));
        Assert.Equal(SetupField.Volume, WidgetSetup.Needs(ListKind.Drive.New()));
    }

    /// <summary>Set up, none of them asks again; and a widget with nothing to set up never asks.</summary>
    [Fact]
    public void ASetUpWidgetOrOneWithNothingToSetAsksNothing()
    {
        var pin = BarKind.Pin.New();
        pin.Pin = new PinOptions { Target = @"C:\Windows\notepad.exe" };
        var stock = ListKind.Markets.New();
        stock.Market = new MarketOptions { Symbol = "MSFT" };
        var here = ListKind.WeatherNow.New();
        here.Weather = new WeatherOptions { UseDeviceLocation = true };
        var there = ListKind.WeatherDays.New();
        there.Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.37, Longitude = 8.54 };
        var contents = ListKind.FolderContents.New();
        contents.Pin = new PinOptions { Target = @"C:\Users" };
        var heading = ListKind.Heading.New();
        heading.Heading = new HeadingOptions { Text = "Work" };

        Assert.All(new WidgetItem[] { pin, stock, here, there, contents, heading }, item => Assert.Null(WidgetSetup.Needs(item)));
        Assert.All(
            new WidgetItem[] { BarKind.NightLight.New(), BarKind.EnergySaver.New(), ListKind.Clock.New() },
            item => Assert.Null(WidgetSetup.Needs(item)));
        // Every taskbar entry: only the pin asks (the stock, the feed, the drives and a folder's contents are lines).
        Assert.Equal(
            [BarKind.Pin],
            Enum.GetValues<BarKind>().Where(kind => BarEntries.Find(kind) is not null && WidgetSetup.Needs(kind.New()) is not null).OrderBy(kind => kind));
    }

}
