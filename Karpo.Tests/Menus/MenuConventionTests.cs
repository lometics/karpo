// menus: one convention for every widget's settings (2026-10-22, the person: "for all the widgets, go
// ahead and make sure that the configuration is implemented as similarly as possible, same order, same
// depth, same types of menus"; Karpo.Core\OptionRows.cs, docs\widgets.md §292). Held over every widget
// there is — each taskbar entry, each folder line, a pin to a folder, a markets widget of every type —
// and every face, so a widget added later follows it or fails here:
//
//  - **Order.** Every row says its part of the menu (OptionSlot), and the rows stand in the parts'
//    order: what it is of, what of it, its style, its look, its words, where it stands, what it does.
//  - **Type and depth by value.** On or off is a toggle on the menu's own level; named choices a level
//    of them (alignment's icons alone in the row); a number a stepper in the row; free text, a search,
//    a colour, an icon, a time and a length a level of their own; nothing a level deeper but a group's.
//  - **The same thing, the same words.** A style is "Style" with the Style icon, one to a menu; a place
//    "Place", a zone "Time zone", a pace "Update interval" (§411).

using Karpo.Dock;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class MenuConventionTests
{
    /// <summary>Every list a row chooses from, listed, so every row shows.</summary>
    private static readonly OptionDevices Sources = new(["GPU 0", "GPU 1"], ["Disk 0 (C:)", "Disk 1 (E:)"])
    {
        Drives = [new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50)],
        TodoSources = [new TodoSource("me@example.com", "tasks", "Tasks", Default: true)],
        Notebook = new NotebookFile { Lists = [new TodoList { Id = "a", Name = "Home" }], Notes = [new Note { Id = "n1", Title = "Ideas" }] },
        FirstDay = DayOfWeek.Monday,
        Targets = new TargetChoices { Folders = [TargetChoices.Row("Documents", @"C:\Users\me\Documents")] },
    };

    private static readonly OptionPlace Place = new(new Config(), Dark: true, Exists: path => path == @"C:\Work");

    /// <summary>Every widget's settings and every face's, by what they are: each kind new, a pin and a folder's contents pointed at a folder, a markets widget of each type and style.</summary>
    private static IEnumerable<(string What, IReadOnlyList<OptionRow> Rows)> Menus()
    {
        var widgets = Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()).Concat(Enum.GetValues<ListKind>().Select(kind => kind.New())).Where(item => item.Info is not null);
        foreach (var item in widgets)
        {
            if (item.Pin is { } pin)
            {
                pin.Target = @"C:\Work";
            }
            yield return (item.Info!.Id, OptionRows.Of(item, Sources, Place));
            if (item.Market is not null)
            {
                foreach (var sample in Markets.MarketRowsTests.Samples)
                {
                    item.Market = sample.Copy();
                    item.Market.Range = MarketRange.Years;
                    yield return ($"{item.Info.Id} {sample.Symbol} {sample.Style}", OptionRows.Of(item, Sources, Place));
                }
            }
        }
        foreach (var face in FolderFaces.All)
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(face.Id)];
            yield return ($"face {face.Id}", OptionRows.OfFace(folder.Folder, 0, Sources, Place));
        }
    }

    [Fact]
    public void EveryRowSaysItsPartAndTheyStandInTheOneOrder()
    {
        foreach (var (what, rows) in Menus())
        {
            Assert.All(rows, row => Assert.True(row.Slot != OptionSlot.Unset, $"{what}: {row.Key} says no part of the menu"));
            Assert.True(rows.Select(row => row.Slot).SequenceEqual(rows.Select(row => row.Slot).Order()), $"{what}: {string.Join(", ", rows.Select(row => $"{row.Key} {row.Slot}"))}");
        }
    }

    [Fact]
    public void ARowsTypeFollowsItsValue()
    {
        foreach (var (what, rows) in Menus())
        {
            foreach (var row in rows)
            {
                var said = $"{what}: {row.Key}";
                // Alignment's icons alone stand in their row: a choice of words is a level of them.
                Assert.True(!row.Segmented || (row.Slot == OptionSlot.Placement && row.ChoiceGlyphs.Count == row.Choices.Count && row.Choices.Count <= 4), said);
                // What is found by typing is what a widget is of, or what of it it shows.
                Assert.True(row.Kind != OptionRowKind.Search || row.Slot is OptionSlot.Subject or OptionSlot.Scope, said);
                // A Browse… stands over its search, never a row of its own.
                Assert.True(row.Kind != OptionRowKind.Target || rows.Any(other => other.Key == row.Within && other.Kind == OptionRowKind.Search), said);
                Assert.True(row.Glyph.Length > 0, $"{said} has no icon");
            }
            // One style, called Style, with the Style icon; a choice.
            var styles = rows.Where(row => row.Slot == OptionSlot.Style).ToList();
            Assert.True(styles.Count <= 1, what);
            Assert.All(styles, style => Assert.Equal((OptionRowKind.Choice, S.SettingsWidgetStyleHeader, OptionGlyphs.Style), (style.Kind, style.Title, style.Glyph)));
            // The same thing, the same words.
            Assert.All(rows.Where(row => row.Field == OptionField.Zone), row => Assert.Equal(S.SettingsWidgetClockZoneHeader, row.Title));
            Assert.All(rows.Where(row => row.Field == OptionField.Place), row => Assert.Equal(S.SettingsWidgetWeatherPlaceHeader, row.Title));
            Assert.All(rows.Where(row => row.Glyph == OptionGlyphs.Pace), row => Assert.Equal(S.SettingsOnlineUpdateHeader, row.Title));
        }
    }

    /// <summary>
    /// Every style of a widget or a face (§270's audit: drawings of the same data) is in the style's part,
    /// and so called Style: the calendar's and the weather's "Look", the tides' "Show", the meeting's,
    /// the air's and the quakes' widths. (The screen time's "Graph" went with the area, §603: its history is always bars.)
    /// </summary>
    [Fact]
    public void EveryStyleIsInTheStylesPart()
    {
        string[] styles = [OptionRows.Style, MarketRows.Style];
        var all = Menus().SelectMany(menu => menu.Rows).ToList();
        Assert.All(all.Where(row => styles.Contains(row.Key)), row => Assert.Equal(OptionSlot.Style, row.Slot));
        (FaceInfo Face, string Key)[] faces =
        [
            (FolderFaces.Calendar, CalendarFace.Look), (FolderFaces.Weather, WeatherFace.Look), (FolderFaces.Tides, TidesFace.Show),
            (FolderFaces.Meeting, MeetingFace.Width), (FolderFaces.AirQuality, AirQualityFace.Width), (FolderFaces.Quakes, QuakesFace.Width),
        ];
        Assert.All(faces, style => Assert.Equal((OptionSlot.Style, S.SettingsWidgetStyleHeader), (style.Face.Option(style.Key)!.Slot, style.Face.TitleOf(style.Face.Option(style.Key)!))));
    }

    /// <summary>
    /// §302 (the person: "make sure that all properties that have a unit also have it in the ui"):
    /// every number of every widget's, face's and bar's menu — the bar's and the folders' opacity, a slider's
    /// length — says its unit in its stepper ("4 px", "70%");
    /// the one number without is a count its title names (a chart's years).
    /// A time or a length is no number but a level of its parts, each saying "h" or "min".
    /// </summary>
    [Fact]
    public void EveryNumberSaysItsUnit()
    {
        var config = new Config();
        var bar = BarLooks.Rows(config, new BarSpot("\\\\.\\DISPLAY1", "One"), dark: true, LooksScope.Taskbar);
        var numbers = Menus().Append((What: "bar", Rows: bar)).SelectMany(menu => OptionRows.Flat(menu.Rows).Select(row => (menu.What, Row: row))).Where(each => each.Row.Kind == OptionRowKind.Number).ToList();
        // §717: and Windows search's counts, a number per section under the heading that names them ("Items per section").
        string[] counts = [MarketRows.Years];
        foreach (var (what, row) in numbers)
        {
            var said = $"{what}: {row.Key} says {row.Shown}";
            Assert.True(row.Unit != OptionUnit.None || counts.Contains(row.Key) || (row.Key.StartsWith(OptionRows.WinSearchHomeMost, StringComparison.Ordinal) && row.Group == WinSearchWords.HomeCounts), said);
            Assert.True(row.Said is not null || row.Shown == OptionUnits.Say(row.Unit, row.Value), said);
        }
        Assert.Equal("4 px", OptionUnits.Say(OptionUnit.Pixels, 4));
        Assert.Equal("70%", OptionUnits.Say(OptionUnit.Percent, 70));
    }

    /// <summary>
    /// The menu each widget's rows make: on or off and alignment's icons in the row; a choice, a number
    /// (its field, typed in, §521), a text, a search, a colour, an icon, a time and a length one level in, its own rows there and
    /// none of them a level again, but a group's; a Browse… on its search's level.
    /// </summary>
    [Fact]
    public void ARowsDepthFollowsItsType()
    {
        foreach (var (what, rows) in Menus())
        {
            List<OptionPick> picks = [];
            var items = OptionMenu.Rows(rows, picks);
            foreach (var item in items)
            {
                var pick = OptionMenu.PickOf(picks, item.Id);
                var row = rows.FirstOrDefault(each => each.Key == pick?.Key);
                var said = $"{what}: {item.Text}";
                if (row is null || item.Header || item.Separator)
                {
                    continue;
                }
                // §425: a pin's Target, a choice of kinds, each its own level (App, Folder, Website, …), as the person allowed.
                if (row.Group is not null || row.Owned is not null || row.Level.Any(each => each.Ticked is not null))
                {
                    // A group's level, a level of the person's own things (§324); what stands in it at most one more.
                    Assert.All(item.Children ?? [], child => Assert.All(child.Children ?? [], grandchild => Assert.Null(grandchild.Children)));
                    continue;
                }
                var level = row.Kind switch
                {
                    OptionRowKind.Toggle or OptionRowKind.Action or OptionRowKind.Link => false,
                    // §521: every number takes a number typed, its field one level in, as a line of text's.
                    OptionRowKind.Number => true,
                    OptionRowKind.Choice => !row.Segmented,
                    _ => true,
                };
                Assert.True(level == item.Children is not null, said);
                Assert.All(item.Children ?? [], child => Assert.Null(child.Children));
            }
            Assert.DoesNotContain(items, item => OptionMenu.PickOf(picks, item.Id)?.Kind == OptionPickKind.Browse);
        }
    }
}
