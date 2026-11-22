// The date in a zone of its own (docs\widgets.md §230; the person, 2026-10-19: "for the date the
// timezone is actually important too"): the calendar face, the time-and-date face, and the clock
// widget's date and time styles (DateHeaderTile, TimeHeaderTile) show today where the zone they are
// set to is — this PC's unless set, so a file that sets none reads as it did. Around midnight that
// is another day. The clock is frozen on the test's thread (Clock.Now) and the zones are Windows'.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Folders;

public sealed class DateZoneTests
{
    /// <summary>A zone this PC is not in, whose date at 23:30 here is tomorrow: ahead of this PC's by more than half an hour.</summary>
    private static string Ahead()
    {
        var local = TimeZoneInfo.Local.BaseUtcOffset;
        return new[] { "Line Islands Standard Time", "Tonga Standard Time", "Tokyo Standard Time" }
            .First(id => TimeZoneInfo.FindSystemTimeZoneById(id).BaseUtcOffset - local >= TimeSpan.FromHours(1));
    }

    private static readonly DateTime LateEvening = new(2026, 10, 19, 23, 30, 0, DateTimeKind.Local);

    /// <summary>The date in <paramref name="zone"/> at <see cref="LateEvening"/> here.</summary>
    private static DateOnly DateIn(string zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(LateEvening.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById(zone)));

    private static void At(DateTime now, Action body)
    {
        Clock.Now = () => now;
        try
        {
            body();
        }
        finally
        {
            Clock.Now = Clock.Real;
        }
    }

    /// <summary>
    /// The calendar face and the time-and-date face have a zone, as the clock face has: unset on a
    /// new face — nothing is written — and offered in its menu as the clock's zone is, searched for.
    /// The zones they are set to are among the zones in use, which a zone's search offers first.
    /// </summary>
    [Fact]
    public void TheDateFacesHaveAZoneAsTheClockHas()
    {
        foreach (var info in new[] { FolderFaces.Calendar, FolderFaces.DateTime })
        {
            var option = info.Option(ClockFace.Zone);
            Assert.NotNull(option);
            Assert.Equal(FaceOptionKind.Zone, option.Kind);
            Assert.Equal("", option.Default);
            Assert.Equal(Strings.Text("core.face.clock.zone.title"), info.TitleOf(option));
            var holder = BarKind.FolderList.New();
            holder.Folder!.Faces = [FolderFace.Of(info.Id)];
            Assert.Empty(holder.Folder.Faces[0].Options);
            var row = OptionRows.OfFace(holder.Folder, 0).Single(row => row.Key == ClockFace.Zone);
            Assert.Equal(OptionRowKind.Search, row.Kind);
            Assert.Equal(OptionField.Zone, row.Field);
            holder.Folder.Faces[0].Set(option, "Tokyo Standard Time");
            Assert.Equal("Tokyo Standard Time", info == FolderFaces.Calendar ? CalendarFace.ZoneOf(holder.Folder.Faces[0]) : DateTimeFace.ZoneOf(holder.Folder.Faces[0]));
            var config = new Config { Layouts = [new TaskbarLayout { Islands = [holder] }] };
            Assert.Equal(["Tokyo Standard Time"], OptionFields.ZonesInUse(config));
        }
    }

    /// <summary>A date's zone says its offset at its menu row's end, "UTC+9" (TimeZoneText, §230), as a clock's does.</summary>
    [Fact]
    public void ADatesZoneIsSaidByItsOffset()
    {
        const string tokyo = "Tokyo Standard Time";
        foreach (var info in new[] { FolderFaces.Calendar, FolderFaces.DateTime })
        {
            var holder = BarKind.FolderList.New();
            holder.Folder!.Faces = [FolderFace.Of(info.Id)];
            holder.Folder.Faces[0].Set(info.Option(ClockFace.Zone)!, tokyo);
            var row = OptionRows.OfFace(holder.Folder, 0).Single(row => row.Key == ClockFace.Zone);
            Assert.Equal(new FlyoutValue("UTC+9"), OptionMenu.ValueOf(row));
        }
        foreach (var kind in new[] { ListKind.DateHeader, ListKind.TimeHeader })
        {
            var line = kind.New();
            line.Clock!.TimeZone = tokyo;
            Assert.Equal(new FlyoutValue("UTC+9"), OptionMenu.ValueOf(OptionRows.Of(line).Single(row => row.Key == OptionRows.ClockZone)));
        }
    }

    /// <summary>
    /// The clock widget's date and time styles carry the clock's zone, and its row — not its seconds
    /// nor its label, which are the clock's alone — so a clock switched to its date shows that zone's.
    /// </summary>
    [Fact]
    public void TheClocksDateAndTimeStylesHaveItsZone()
    {
        foreach (var kind in new[] { ListKind.DateHeader, ListKind.TimeHeader })
        {
            var line = kind.New();
            Assert.Equal("", line.Clock!.TimeZone);
            var keys = OptionRows.Of(line).Select(row => row.Key).ToList();
            Assert.Contains(OptionRows.ClockZone, keys);
            Assert.DoesNotContain(OptionRows.ClockSeconds, keys);
            Assert.DoesNotContain(OptionRows.ClockName, keys);
        }
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        Assert.True(WidgetStyles.Restyle(clock, WidgetStyles.Clock.Styles.Single(style => style.Form == ListKind.TimeHeader)));
        clock.Normalize();
        Assert.Equal(ListKind.TimeHeader, clock.List);
        Assert.Equal("Tokyo Standard Time", clock.Clock!.TimeZone);
    }

    /// <summary>
    /// Nothing is written for a zone nobody set, and a file without one reads as this PC's: a date
    /// line, a time line and the two date faces as a file from before has them.
    /// </summary>
    [Fact]
    public void AZoneNobodySetIsNotWritten()
    {
        var holder = Karpo.Tests.Core.Configs.Holding(ListKind.DateHeader.New(), ListKind.TimeHeader.New());
        holder.Folder!.Faces = [CalendarFace.New(), FolderFace.Of(FolderFaces.DateTimeId)];
        var json = Karpo.Tests.Core.Configs.With(holder).ToJson();
        Assert.DoesNotContain("\"zone\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Standard Time", json, StringComparison.Ordinal);
        var read = Karpo.Tests.Core.Configs.Repaired(json);
        var folder = read.Layouts[0].Islands.Single(island => island.Id == holder.Id).Folder!;
        Assert.All(folder.Cells.Where(cell => cell is not null), cell => Assert.Equal("", cell!.Island.Clock!.TimeZone));
        Assert.Equal("", CalendarFace.ZoneOf(folder.Faces[0]));
        Assert.Equal("", DateTimeFace.ZoneOf(folder.Faces[1]));
    }

    /// <summary>
    /// At 23:30 here, a date set to a zone ahead shows tomorrow — on the calendar face, the time-and-
    /// date face, and the clock's date and time styles — and this PC's today where none is set.
    /// </summary>
    [UiFact]
    public void AroundMidnightTheZonesDateIsShown()
    {
        var zone = Ahead();
        var tomorrow = DateIn(zone);
        var today = DateOnly.FromDateTime(LateEvening);
        Assert.NotEqual(today, tomorrow);
        Offscreen.OnStaThread(gfx => At(LateEvening, () =>
        {
            var calendar = CalendarFace.New();
            var dateTime = FolderFace.Of(FolderFaces.DateTimeId);
            Assert.Equal(today, CalendarFaceDrawing.Today(calendar));
            Assert.Equal(today, DateTimeFaceDrawing.Today(dateTime));
            calendar.Set(FolderFaces.Calendar.Option(CalendarFace.Zone)!, zone);
            dateTime.Set(FolderFaces.DateTime.Option(DateTimeFace.Zone)!, zone);
            Assert.Equal(tomorrow, CalendarFaceDrawing.Today(calendar));
            Assert.Equal(tomorrow, DateTimeFaceDrawing.Today(dateTime));
            // The time with it: the zone's, not this PC's 23:30.
            Assert.Equal(Clock.Read(zone, seconds: false).Time.Replace(':', FaceDrawings.Ratio), ClockFaceDrawing.Time(DateTimeFaceDrawing.Settings(dateTime), DateTime.MinValue));

            var style = MediaPresetTestsStyle();
            foreach (var (set, day) in new[] { ("", today), (zone, tomorrow) })
            {
                var header = ListKind.DateHeader.New();
                header.Clock!.TimeZone = set;
                using var date = new DateHeaderTile("d", header, gfx, new Config());
                var said = date.LinePieces(PopupPaint.Rect(0, 0, 2000, 200), style).Single().Text;
                Assert.Equal(Karpo.Dock.Panels.Nls.LongDateWithoutYear(day), said);

                var top = ListKind.TimeHeader.New();
                top.Clock!.TimeZone = set;
                using var time = new TimeHeaderTile("t", top, gfx, new Config());
                var pieces = time.LinePieces(PopupPaint.Rect(0, 0, 2000, 200), style);
                Assert.Equal(Clock.Read(set, seconds: true).Time, pieces[0].Text);
                Assert.Equal(Clock.Read(set, seconds: true).Long, pieces[1].Text);
            }
        }));
    }

    private static TileStyle MediaPresetTestsStyle()
    {
        var look = ListFormPictures.LookFor(true);
        return FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
    }
}
