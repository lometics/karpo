// A folder's channels (Karpo.Core\Channels.cs): the registry, the values a folder holds while it is
// open, and that a follower always follows — without a folder or a window. FolderChannelsTests holds them in an
// open list folder.

using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class ChannelsTests
{
    /// <summary>A channel no widget uses, of another type: what the mechanism is held to besides the day.</summary>
    private static readonly Channel<int> Count = new("test.count");

    [Fact]
    public void TheRegistryHasTheDayAndFindsChannelsById()
    {
        Assert.Contains(Channels.Day, Channels.All);
        Assert.Same(Channels.Day, Channels.Find("day"));
        Assert.Null(Channels.Find("test.count"));
        Assert.Null(Channels.Find(null));
        Assert.Equal(typeof(DateOnly), Channels.Day.Type);
        Assert.Equal(typeof(int), Count.Type);
        Assert.Equal(Channels.All.Length, Channels.All.Select(channel => channel.Id).Distinct().Count());
    }

    /// <summary>Nothing published reads as nothing, for any channel; what is published is read back, and only on its channel.</summary>
    [Fact]
    public void WhatIsPublishedIsReadOnItsOwnChannel()
    {
        var values = new ChannelValues();
        var day = new DateOnly(2026, 10, 14);

        Assert.Null(values.Of(Channels.Day));
        Assert.Null(values.Of(Count));
        Assert.True(values.Set(Channels.Day, day));
        Assert.True(values.Set(Count, 3));
        Assert.Equal(day, values.Of(Channels.Day));
        Assert.Equal(3, values.Of(Count));
        Assert.True(values.Has(Count));
    }

    /// <summary>Publishing what is there already changes nothing — which is what stops a click from drawing the folder for nothing.</summary>
    [Fact]
    public void PublishingTheSameValueChangesNothing()
    {
        var values = new ChannelValues();
        values.Set(Count, 0);

        Assert.False(values.Set(Count, 0));
        Assert.True(values.Set(Count, 1));
    }

    /// <summary>A channel of the same id and another type reads as nothing, never as a wrong value.</summary>
    [Fact]
    public void AValueOfAnotherTypeReadsAsNothing()
    {
        var values = new ChannelValues();
        values.Set(Channels.Day, new DateOnly(2026, 10, 14));

        Assert.Null(values.Of(new Channel<int>("day")));
        Assert.NotNull(values.Of(Channels.Day));
    }

    /// <summary>The folder closing forgets everything published, together.</summary>
    [Fact]
    public void ClearingForgetsEveryChannel()
    {
        var values = new ChannelValues();
        values.Set(Channels.Day, new DateOnly(2026, 10, 14));
        values.Set(Count, 7);

        Assert.True(values.Clear());
        Assert.Null(values.Of(Channels.Day));
        Assert.Null(values.Of(Count));
        Assert.False(values.Clear(), "and clearing nothing is not a change");
    }

    /// <summary>The app channels: an app by its key, one app whatever the case of its path; any widget may read them.</summary>
    [Fact]
    public void TheAppChannelsCarryAnAppAndAreInTheRegistry()
    {
        Assert.Same(Channels.App, Channels.Find("app"));
        Assert.Same(Channels.AppHover, Channels.Find("appHover"));
        Assert.Equal(typeof(AppId), Channels.App.Type);
        Assert.Equal(typeof(AppId), Channels.AppHover.Type);
        Assert.True(Channels.App.Escapes);
        Assert.False(Channels.AppHover.Escapes);
        Assert.False(Channels.Day.Escapes);

        var values = new ChannelValues();
        Assert.True(values.Set(Channels.App, new AppId(@"C:\Apps\Editor.exe")));
        Assert.False(values.Set(Channels.App, new AppId(@"c:\apps\editor.EXE")), "the same app in another case is no change");
        Assert.Equal(new AppId(@"C:\APPS\EDITOR.EXE"), values.Of(Channels.App));
        Assert.Equal(new AppId("a").GetHashCode(), new AppId("A").GetHashCode());
        // The picked app and the pointed one are two things.
        Assert.Null(values.Of(Channels.AppHover));
    }

    /// <summary>A value taken back reads as unset again; taking back nothing is no change.</summary>
    [Fact]
    public void AValueTakenBackReadsAsNothing()
    {
        var values = new ChannelValues();
        values.Set(Channels.AppHover, new AppId("mail"));
        values.Set(Channels.Day, new DateOnly(2026, 10, 14));

        Assert.True(values.Unset(Channels.AppHover));
        Assert.Null(values.Of(Channels.AppHover));
        Assert.False(values.Has(Channels.AppHover));
        Assert.False(values.Unset(Channels.AppHover));
        Assert.NotNull(values.Of(Channels.Day));
    }

    /// <summary>Escape takes back what was picked — the app — and leaves the day and the pointer's app; nothing picked, it is no change.</summary>
    [Fact]
    public void EscapeTakesBackOnlyWhatWasPicked()
    {
        var values = new ChannelValues();
        var day = new DateOnly(2026, 10, 14);
        values.Set(Channels.Day, day);
        values.Set(Channels.AppHover, new AppId("mail"));
        Assert.False(values.Escape());

        values.Set(Channels.App, new AppId("mail"));
        Assert.True(values.Escape());
        Assert.Null(values.Of(Channels.App));
        Assert.Equal(day, values.Of(Channels.Day));
        Assert.Equal(new AppId("mail"), values.Of(Channels.AppHover));
    }

    /// <summary>Which widgets publish and follow is their list records': the month and screen time's total both, the agenda and screen time's apps follow, the rest neither.</summary>
    [Fact]
    public void TheMonthPublishesTheDayAndTheAgendaFollowsIt()
    {
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.Month).Publishes);
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.Month).Follows);
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.Calendar).Follows);
        Assert.Empty(ListWidgets.Of(ListKind.Calendar).Publishes);
        // Screen time: today's total steps the day and follows it, and follows the app picked and pointed at; its
        // history's bars set the day and the app; its apps follow the day (§603: the total and its bars two widgets).
        Assert.Equal([Channels.Day], ListWidgets.Of(ListKind.ScreenTime).Publishes);
        Assert.Equal([Channels.Day, Channels.App, Channels.AppHover], ListWidgets.Of(ListKind.ScreenTime).Follows);
        Assert.Equal([Channels.Day, Channels.App, Channels.AppHover], ListWidgets.Of(ListKind.ScreenTimeHistory).Follows);
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.ScreenTimeApps).Follows);
        Assert.DoesNotContain(Channels.Day, ListWidgets.Of(ListKind.ScreenTimeApps).Publishes);
        // ... and the bars and the apps publish and follow the app picked and the app pointed at (2026-10-10).
        foreach (var kind in new[] { ListKind.ScreenTimeHistory, ListKind.ScreenTimeApps })
        {
            Assert.Contains(Channels.App, ListWidgets.Of(kind).Publishes);
            Assert.Contains(Channels.App, ListWidgets.Of(kind).Follows);
            Assert.Contains(Channels.AppHover, ListWidgets.Of(kind).Publishes);
            Assert.Contains(Channels.AppHover, ListWidgets.Of(kind).Follows);
        }
        // The ring (2026-10-12): the day shown and the day pointed at in the history's bars, which the
        // history publishes; the app as the apps' line has it. It sets no day itself.
        Assert.Equal([Channels.Day, Channels.DayHover, Channels.App, Channels.AppHover], ListWidgets.Of(ListKind.ScreenTimeHistory).Publishes);
        Assert.Equal([Channels.Day, Channels.DayHover, Channels.App, Channels.AppHover], ListWidgets.Of(ListKind.ScreenTimeChart).Follows);
        Assert.Equal([Channels.App, Channels.AppHover], ListWidgets.Of(ListKind.ScreenTimeChart).Publishes);
        // The calendar's flyout as lines (2026-10-09): the views publish and follow, the day's events
        // follow, the date's header publishes today.
        foreach (var view in new[] { ListKind.DayView, ListKind.WeekView, ListKind.YearView })
        {
            Assert.Contains(Channels.Day, ListWidgets.Of(view).Publishes);
            Assert.Contains(Channels.Day, ListWidgets.Of(view).Follows);
        }
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.Events).Follows);
        Assert.Empty(ListWidgets.Of(ListKind.Events).Publishes);
        Assert.Contains(Channels.Day, ListWidgets.Of(ListKind.DateHeader).Publishes);
        Assert.Empty(ListWidgets.Of(ListKind.DateHeader).Follows);
        Assert.All(ListWidgets.All.Where(line => line.Kind is not (ListKind.Month or ListKind.Calendar or ListKind.ScreenTime or ListKind.ScreenTimeHistory or ListKind.ScreenTimeApps or ListKind.ScreenTimeChart
            or ListKind.DayView or ListKind.WeekView or ListKind.YearView or ListKind.Events or ListKind.DateHeader)), line =>
        {
            Assert.Empty(line.Follows);
            Assert.Empty(line.Publishes);
        });
        // Only channels the registry has.
        Assert.All(ListWidgets.All.SelectMany(line => line.Follows.Concat(line.Publishes)), channel => Assert.Contains(channel, Channels.All));
    }

    /// <summary>
    /// A follower always follows (§240): every style of the calendar follows the folder's day; a widget
    /// whose kind follows nothing, and a bar's, follow nothing.
    /// </summary>
    [Fact]
    public void AFollowerAlwaysFollows()
    {
        Assert.All(WidgetStyles.Calendar.Styles, style => Assert.True(Channels.Follows(style.Form.New(), Channels.Day), style.Word));
        Assert.False(Channels.Follows(ListKind.Heading.New(), Channels.Day));
        Assert.Empty(Channels.FollowedBy(BarKind.NightLight.New()));
    }

    /// <summary>A file's opt-out from before §240 ("unfollowed") is read past: the line follows, and the file says it no more.</summary>
    [Fact]
    public void AnOldOptOutIsReadPastAndNotWrittenAgain()
    {
        foreach (var style in WidgetStyles.Calendar.Styles)
        {
            var json = JsonSerializer.Serialize(style.Form.New(), ConfigJson.Default.CellItem);
            var old = string.Concat(json.AsSpan(0, json.LastIndexOf('}')), """, "unfollowed": [ "day" ] }""");
            var read = JsonSerializer.Deserialize(old, ConfigJson.Default.CellItem)!;
            read.View = FolderView.List;
            read.Normalize();

            Assert.Equal(style.Form, ListWidgets.Find(read.Kind)!.Kind);
            Assert.True(Channels.Follows(read, Channels.Day), style.Word);
            Assert.DoesNotContain("unfollowed", JsonSerializer.Serialize(read, ConfigJson.Default.CellItem), StringComparison.OrdinalIgnoreCase);
        }
    }
}
