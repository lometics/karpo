// §642: Windows search's line over a fake index, fake apps and a hand-pumped worker: typing lists the best match and the
// groups, the arrows walk them, Enter opens through the folder's click once the folder has gone (Ctrl+Enter the location,
// Ctrl+Shift+Enter as administrator), Ctrl+C copies the path in quotes and says "Copied", an answer's Enter copies its
// value, the chips filter and Ctrl+Tab steps through them, Escape empties the box before it lets the folder close, §655 the
// home lists its sections (the searches, Top apps and Pinned as tiles, the files and folders used lately), a wide line's
// pane shows the chosen result and its actions beside a list that keeps its split, and a ghost shows the samples' home
// asking nothing. No test opens, launches or copies to Windows' clipboard (WinSearchLine.Launched, Copier), nor asks the
// real index.
//
// Pictures (KARPO_UI_TESTS=1, KARPO_PNG_DIR): winsearch-line-<dark|light>.png (the group as it opens: the home, typed with
// its best match and groups, a row under the pointer with its actions, an answer, a chip chosen, nothing found, the
// sample a ghost shows, 225 % text), §655 winsearch3-<narrow|wide>-<home|typed>-<dark|light>.png, and
// winsearch-face-<dark|light>.png (the face on 40-, 48- and 64-DIP bars and down a side).

using System.Collections.Concurrent;
using System.Numerics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.WinSearch;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using Mods = Karpo.Dock.QuickSettings.Controls.Mods;

namespace Karpo.Tests.Searching;

/// <summary>§642: Windows search's sources made up: a fake index, fake apps, recent files, and work run when pumped.</summary>
internal sealed class WinSearchFakes
{
    public sealed class Index : IWinSearchIndex
    {
        public readonly List<string> Asked = [];
        public IReadOnlyList<WinSearchRow> Rows = [];
        public string? Problem;

        public WinSearchIndexAnswer Query(string sql, CancellationToken cancel)
        {
            Asked.Add(sql);
            return new WinSearchIndexAnswer(Rows, Problem);
        }
    }

    public sealed class Apps(IReadOnlyList<WinSearchApp> apps) : IWinSearchApps
    {
        public IReadOnlyList<WinSearchApp> List() => apps;
    }

    public static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    public readonly Index Indexed = new();
    public readonly ConcurrentQueue<Action> Work = new();
    public IReadOnlyList<string> Recent = [@"C:\Users\me\Documents\plan.docx", @"C:\Users\me\Pictures\cat.png"];

    /// <summary>§655: the folders used lately, the home's.</summary>
    public IReadOnlyList<string> Folders = [@"C:\Users\me\Documents\Trips", @"C:\Users\me\Pictures\Paris 2025"];
    private readonly IReadOnlyList<WinSearchApp> listed;
    private WinSearchSources? sources;

    /// <summary>The sources, made the first time they are asked for, with <see cref="More"/> and §655 the home's (<see cref="Home"/>).</summary>
    public WinSearchSources Sources => sources ??= WinSearchSources.Windows(Indexed, new Apps(listed), Work.Enqueue, static (_, cancel) => !cancel.IsCancellationRequested, () => Now,
        web: static text => "https://www.example.com/search?q=" + Uri.EscapeDataString(text), more: More, home: Home ?? HomeOf(this));

    /// <summary>§655: the home's sections; none for <see cref="HomeOf"/>'s: the apps opened (Paint twice, Code once), the pinned apps, the files and folders used lately.</summary>
    public IReadOnlyList<IWinSearchHomeSource>? Home { get; init; }

    /// <summary>§655: the home's sections over the fakes.</summary>
    public static IReadOnlyList<IWinSearchHomeSource> HomeOf(WinSearchFakes fakes) =>
    [
        new WinSearchHomeSource(WinSearchHomeInfos.Apps, ask => WinSearchHomeHits.TopApps(fakes.listed,
            target => target.Contains("Microsoft.Paint_", StringComparison.Ordinal) ? (2, Now) : target.EndsWith("VisualStudioCode", StringComparison.Ordinal) ? (1, Now.AddDays(-1)) : (0, default),
            ask.Opened, ask.Most)),
        new WinSearchHomeSource(WinSearchHomeInfos.Pinned, ask => [.. fakes.listed.Take(ask.Most).Select(WinSearchHomeHits.OfApp)]),
        new WinSearchHomeSource(WinSearchHomeInfos.Files, ask => [.. fakes.Recent.Take(ask.Most).Select(path => WinSearchHomeHits.OfPath(path, Now, folder: false))]),
        new WinSearchHomeSource(WinSearchHomeInfos.Folders, ask => [.. fakes.Folders.Take(ask.Most).Select(path => WinSearchHomeHits.OfPath(path, Now, folder: true))]),
    ];

    /// <summary>§652: more sources, Karpo's own as a test makes them; none by default.</summary>
    public IReadOnlyList<IWinSearchSource> More { get; init; } = [];

    public WinSearchFakes(params WinSearchApp[] apps)
    {
        listed = apps.Length > 0 ? apps :
        [
            new("Paint", @"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App"),
            new("Paint 3D", @"shell:AppsFolder\Microsoft.MSPaint_8wekyb3d8bbwe!Microsoft.MSPaint"),
            new("Visual Studio Code", @"shell:AppsFolder\Microsoft.VisualStudioCode"),
            new("Windows PowerShell", @"shell:AppsFolder\{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe"),
        ];
        Indexed.Rows =
        [
            new("Packing list.docx", @"C:\Users\me\Documents\Packing list.docx", "file:C:/Users/me/Documents/Packing list.docx", ".docx", Now.AddDays(-1), 24_576, 900),
            new("Paris 2025", @"C:\Users\me\Pictures\Paris 2025", "file:C:/Users/me/Pictures/Paris 2025", "Directory", Now.AddDays(-40), null, 700),
            new("Paris.jpg", @"C:\Users\me\Pictures\Paris 2025\Paris.jpg", "file:C:/Users/me/Pictures/Paris 2025/Paris.jpg", ".jpg", Now.AddDays(-40), 3_400_000, 600),
            new("Party mix.mp3", @"C:\Users\me\Music\Party mix.mp3", "file:C:/Users/me/Music/Party mix.mp3", ".mp3", Now.AddDays(-90), 7_000_000, 500),
            new("setup.exe", @"C:\Users\me\Downloads\setup.exe", "file:C:/Users/me/Downloads/setup.exe", ".exe", Now.AddDays(-3), 90_000_000, 300),
        ];
    }

    /// <summary>
    /// §652: the icons a picture wears — Windows' own, read at once: an app's by its address, Settings' by its app's, a made-up
    /// file's or folder's its kind's (it is nowhere on a disk).
    /// </summary>
    public static Pixels? PictureIcons(string key, float px, Action came) =>
        key.StartsWith(ContactPhotos.Scheme, StringComparison.Ordinal) ? WinSearchLine.Picture(key, px, came)
        : key.StartsWith(WinSearchHit.KindScheme, StringComparison.Ordinal) ? Items.KindIcon(key[WinSearchHit.KindScheme.Length..], TargetPictures.Bucket(px))
        : key.Length > 2 && key[1] == ':' ? Items.KindIcon(Path.HasExtension(key) ? Path.GetExtension(key) : "\\", TargetPictures.Bucket(px))
        : Items.Icon(key, TargetPictures.Bucket(px));

    /// <summary>§661: the person the made-up contacts hold, with a picture (its version; the picture itself a test's to keep).</summary>
    public static readonly Person Paula = new("p", "Paula Weber", "Contoso", ["paula@example.com"], []) { Photo = "v1" };

    /// <summary>§661: a made-up picture of a person: a round face's colours, 64 px, premultiplied.</summary>
    public static Pixels Portrait()
    {
        const int side = 64;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = ((y * side) + x) * 4;
                var (dx, dy) = (x - 32f, y - 26f);
                var face = (dx * dx) + (dy * dy) < 15 * 15;
                var shoulders = y > 44 && MathF.Abs(x - 32f) < 26 - ((64 - y) * 0.4f);
                (data[at], data[at + 1], data[at + 2]) = face ? ((byte)236, (byte)190, (byte)150) : shoulders ? ((byte)170, (byte)90, (byte)40) : ((byte)(200 - y), (byte)140, (byte)90);
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>§652: Karpo's own sources over made-up data that answers "pa": a message, an event, a person, a note.</summary>
    public static IWinSearchSource[] Karpo() =>
    [
        new WinSearchMailSource(static () => [new MailItem("1", "Paula Weber", "paula@example.com", "Party on Friday", "Bring something to share", new DateTimeOffset(Now.AddHours(-3)), Unread: true, Link: "https://outlook.office.com/mail/1")], static () => Now),
        new WinSearchCalendarSource(static (_, _) => [new CalendarEvent(Now.ToLocalTime().AddDays(2), Now.ToLocalTime().AddDays(2).AddHours(1), false, "Paris trip planning", "Room 4")], static (_, _) => "https://calendar.example.com", static () => Now.ToLocalTime()),
        new WinSearchContactsSource(static () => [Paula]),
        new WinSearchNotesSource(static () => [new Note { Id = "n", Title = "Packing for Paris", Body = "- passport\n- adapter" }]),
    ];

    public void Pump()
    {
        while (Work.TryDequeue(out var work))
        {
            work();
        }
    }
}

public sealed unsafe class WinSearchLineTests
{
    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line alone over <paramref name="fakes"/>, shown as an opening folder shows it, the worker pumped.</summary>
    private static WinSearchLine Line(Gfx gfx, WinSearchFakes fakes, WinSearchHistory? history = null, bool ghost = false, Func<string, float, Action, Pixels?>? icons = null)
    {
        var cell = ListKind.WinSearch.New();
        cell.Id = "winsearch-" + Guid.NewGuid().ToString("N")[..6];
        // §635: a ghost's line is made showing the samples, as the add page's is.
        using var samples = ghost ? Samples.Show() : default;
        var line = new WinSearchLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), fakes.Sources, history ?? new WinSearchHistory(), icons ?? (static (_, _, _) => null));
        line.StandIn(ListWidgets.Of(ListKind.WinSearch));
        line.Ticks = static () => 1_000;
        line.After = static (_, _) => { };
        line.Visible(true);
        fakes.Pump();
        return line;
    }

    /// <summary>The width every line of these tests is laid out at, DIP: a folder's Medium, narrow; §655 a test sets it wide.</summary>
    [ThreadStatic]
    private static float widthDip;

    private static D2D_RECT_F Whole(WinSearchLine line, in TileStyle style)
    {
        var width = widthDip > 0 ? PopupPaint.Round(widthDip * style.Scale) : FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(WinSearchLine line, in TileStyle style) => ((ILinePieces)line).LinePieces(Whole(line, style), style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static void Type(WinSearchLine line, WinSearchFakes fakes, string text)
    {
        foreach (var c in text)
        {
            Assert.True(line.CharOn(c, WidgetSurface.List));
        }
        fakes.Pump();
    }

    /// <summary>A key as the folder hands it, with <paramref name="mods"/> held.</summary>
    private static bool Key(WinSearchLine line, int key, Mods mods = Mods.None)
    {
        WinSearchLine.Held = mods;
        try
        {
            return line.KeyOn(key, WidgetSurface.List);
        }
        finally
        {
            WinSearchLine.Held = null;
        }
    }

    /// <summary>Enter as the folder takes it: the line's own, else the folder's click on the line (FolderFlyout.Activate).</summary>
    private static IslandAction Enter(WinSearchLine line, Mods mods = Mods.None) =>
        Key(line, VK.VK_RETURN, mods) ? default : line.Click(line.OwnKeyedTarget ?? 0);

    /// <summary>The pointer onto <paramref name="piece"/>, then its click, as the folder gives it.</summary>
    private static IslandAction Click(WinSearchLine line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var at = Middle(Assert.Single(Pieces(line, style), each => each.Name == piece).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        Assert.False(line.PressOn(at, box, style, WidgetSurface.List), "a press on a row is the folder's");
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    private static string? Said(WinSearchLine line, in TileStyle style, string piece) => Pieces(line, style).Where(each => each.Name == piece).Select(each => each.Text).FirstOrDefault();

    [UiFact]
    public void TypingListsTheBestMatchThenAGroupEach()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            var style = StyleFor(1, LookFor(dark: true));
            Type(line, fakes, "pa");
            var results = line.Results;
            Assert.Equal("Paint", results.Best?.Name);
            // Settings: Touchpad's name holds "pa".
            Assert.Equal([WinSearchCategory.Apps, WinSearchCategory.Settings, WinSearchCategory.Documents, WinSearchCategory.Folders, WinSearchCategory.Photos, WinSearchCategory.Music], results.Groups.Select(group => group.Category));
            Assert.Equal("Paint", Said(line, style, "best name"));
            Assert.Equal(WinSearchWords.BestMatch, Said(line, style, "heading best"));
            Assert.Equal("Paint 3D", Said(line, style, "Apps 0 name"));
            // The best file says what it is, when it changed and how big; its folder under that.
            Type(line, fakes, "ck");
            Assert.Equal("Packing list.docx", Said(line, style, "best name"));
            Assert.Contains(WinSearchWords.Category(WinSearchCategory.Documents), Said(line, style, "best detail"), StringComparison.Ordinal);
            Assert.Equal(@"C:\Users\me\Documents", Said(line, style, "best folder"));
            // §655: no foot: the way to Searching Windows is on the widget's page (WinSearchHomeTests).
            Assert.Null(Said(line, style, "indexing"));
            Assert.Null(Said(line, style, "scope"));
            // "pa" (names: contents need three letters) and "pack" (names, and contents): the keystroke between them never asked.
            Assert.Equal(3, fakes.Indexed.Asked.Count);
        });
    }

    [UiFact]
    public void TheArrowsWalkAndEnterOpensOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            var history = new WinSearchHistory();
            using var line = Line(gfx, fakes, history);
            var style = StyleFor(1, LookFor(dark: false));
            List<(WinSearchAct, WinSearchHit)> launched = [];
            line.Launched = (act, hit) => launched.Add((act, hit));
            Type(line, fakes, "pa");
            _ = Pieces(line, style);
            // Enter with nothing walked: the best match.
            Assert.Equal(IslandView.App, Enter(line).View);
            line.OpenChosen();
            Assert.Equal((WinSearchAct.Open, "Paint"), (launched[0].Item1, launched[0].Item2.Name));
            Assert.Equal("pa", history.Searches[0]);
            Assert.Equal("Paint", history.Opened[0].Name);
            // Down three times: past the best match, Paint 3D and Touchpad settings, to the first document; Up and Down again.
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_UP));
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.Equal(IslandView.App, Enter(line).View);
            line.OpenChosen();
            Assert.Equal("Packing list.docx", launched[1].Item2.Name);
            // Opened once only.
            line.OpenChosen();
            Assert.Equal(2, launched.Count);
        });
    }

    [UiFact]
    public void CtrlEnterOpensTheLocationCtrlShiftEnterRunsAsAdministrator()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            List<(WinSearchAct Act, string Name)> launched = [];
            line.Launched = (act, hit) => launched.Add((act, hit.Name));
            Type(line, fakes, "setup");
            Assert.Equal(IslandView.App, Enter(line, Mods.Control).View);
            line.OpenChosen();
            Assert.Equal(IslandView.App, Enter(line, Mods.Control | Mods.Shift).View);
            line.OpenChosen();
            Assert.Equal([(WinSearchAct.Location, "setup.exe"), (WinSearchAct.Admin, "setup.exe")], launched);
            // An app has no location: Ctrl+Enter does nothing to it.
            line.Box.Text = "paint";
            fakes.Pump();
            Assert.True(Key(line, VK.VK_RETURN, Mods.Control));
            Assert.Null(line.Opening);
        });
    }

    /// <summary>The action buttons a row named <paramref name="row"/> shows now, by act.</summary>
    private static Dictionary<WinSearchAct, D2D_RECT_F> Buttons(WinSearchLine line, in TileStyle style, string row)
    {
        var pieces = Pieces(line, style);
        return Enum.GetValues<WinSearchAct>()
            .Select(act => (act, piece: pieces.Where(piece => piece.Name == $"{row} {act}").Select(piece => (D2D_RECT_F?)piece.Box).FirstOrDefault()))
            .Where(each => each.piece is not null)
            .ToDictionary(each => each.act, each => each.piece!.Value);
    }

    /// <summary>
    /// §659: <paramref name="row"/>'s buttons are <paramref name="hit"/>'s actions but Open, in their order, at the row's end
    /// (ending at <paramref name="end"/>), beside its words and in the middle of them — never under them; their right edge.
    /// </summary>
    private static float AssertAtTheEnd(WinSearchLine line, in TileStyle style, string row, WinSearchHit hit, float? end = null)
    {
        var buttons = Buttons(line, style, row);
        var acts = WinSearchLine.Actions(hit).Where(act => act != WinSearchAct.Open).ToList();
        Assert.Equal(acts, buttons.OrderBy(each => each.Value.left).Select(each => each.Key));
        var words = Pieces(line, style).Where(piece => piece.Name is var name && (name == $"{row} name" || name == $"{row} detail" || name == $"{row} folder")).Select(piece => piece.Box).ToList();
        var right = buttons.Count > 0 ? buttons.Values.Max(box => box.right) : 0;
        foreach (var box in buttons.Values)
        {
            Assert.True(box.left >= words.Max(word => word.right), $"{row}: a button over its words");
            var middle = (box.top + box.bottom) / 2;
            Assert.InRange(middle, words.Min(word => word.top), words.Max(word => word.bottom));
        }
        if (end is { } at && buttons.Count > 0)
        {
            Assert.Equal(at, right, 0.5f);
        }
        return right;
    }

    /// <summary>
    /// §659 (the person, 2026-11-10: "for the best match for example it should show the actions on the right like it is done
    /// for the other sections as well. this should be done for all the sections"): narrow and wide, every row of every
    /// section, the best match too, has its actions at its end — the best match's while it is where Enter lands, any row's
    /// under the pointer or the keys —, all ending at one edge; nothing stands under the best match; Open is the row itself.
    /// </summary>
    [UiTheory]
    [InlineData(360f)]
    [InlineData(700f)]
    public void EveryRowOfEverySectionHasItsActionsAtItsEndTheBestMatchToo(float dip)
    {
        Offscreen.OnStaThread(gfx => At(dip, () =>
        {
            var fakes = new WinSearchFakes { More = WinSearchFakes.Karpo() };
            using var line = Line(gfx, fakes);
            var style = StyleFor(1, LookFor(dark: true));
            var options = new WinSearchOptions();
            options.Set(WinSearchSourceInfos.Web, true);
            line.Settings.WinSearch = options;
            List<(WinSearchAct, string)> launched = [];
            line.Launched = (act, hit) => launched.Add((act, hit.Name));
            Type(line, fakes, "pa");
            var results = line.Results;
            // At rest the best match, where Enter lands, wears its actions at its end: Paint runs as administrator.
            var best = results.Best!;
            Assert.Equal([WinSearchAct.Admin], Buttons(line, style, "best").Keys);
            var edge = AssertAtTheEnd(line, style, "best", best);
            // Nothing under it: no piece of the best match below its words, and no Open button.
            var bestWords = Pieces(line, style).Where(piece => piece.Name is "best name" or "best detail").Max(piece => piece.Box.bottom);
            Assert.All(Pieces(line, style).Where(piece => piece.Name.StartsWith("best ", StringComparison.Ordinal) && piece.Name != "best plate"),
                piece => Assert.True(piece.Box.top < bestWords, piece.Name));
            Assert.Null(Said(line, style, "best Open"));
            // Every section's rows: none at rest; under the pointer its actions, ending where the best match's do.
            var box = Whole(line, style);
            var sections = new HashSet<WinSearchCategory>();
            foreach (var group in results.Groups)
            {
                for (var at = 0; at < group.Hits.Count; at++)
                {
                    var row = $"{WinSearchWords.Category(group.Category)} {at}";
                    var hit = group.Hits[at];
                    line.CardHover(null, box, style, WidgetSurface.List);
                    Assert.Empty(Buttons(line, style, row));
                    line.CardHover(Middle(PieceBox(line, style, $"{row} name")), box, style, WidgetSurface.List);
                    _ = AssertAtTheEnd(line, style, row, hit, edge);
                    sections.Add(group.Category);
                }
            }
            Assert.Superset(new HashSet<WinSearchCategory>
            {
                WinSearchCategory.Apps, WinSearchCategory.Settings, WinSearchCategory.Documents, WinSearchCategory.Folders, WinSearchCategory.Photos,
                WinSearchCategory.Music, WinSearchCategory.Email, WinSearchCategory.Events, WinSearchCategory.People, WinSearchCategory.Notes, WinSearchCategory.Web,
            }, sections);
            // A file's: its location, its path and Open with; a click does it, each names itself.
            line.CardHover(Middle(PieceBox(line, style, "Documents 0 name")), box, style, WidgetSurface.List);
            Assert.Equal([WinSearchAct.Location, WinSearchAct.Copy, WinSearchAct.OpenWith], Buttons(line, style, "Documents 0").OrderBy(each => each.Value.left).Select(each => each.Key));
            var tip = line.CardTip(Middle(PieceBox(line, style, "Documents 0 OpenWith")), Whole(line, style), style, WidgetSurface.List);
            Assert.Equal(S.DockWinSearchOpenWith, tip?.Text);
            Assert.Equal(IslandView.App, Click(line, style, "Documents 0 Location").View);
            line.OpenChosen();
            Assert.Equal((WinSearchAct.Location, "Packing list.docx"), launched[^1]);
            // The keys on a row: its actions; the best match, no longer where Enter lands, has none.
            line.CardHover(null, box, style, WidgetSurface.List);
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.Equal([WinSearchAct.Admin], Buttons(line, style, $"{WinSearchWords.Category(WinSearchCategory.Apps)} 0").Keys);
            Assert.Empty(Buttons(line, style, "best"));
            if (WinSearchLine.Wide(box.right, style))
            {
                // Wide, the pane names them again; the pointer on the pane's buttons leaves the row's alone.
                Assert.True(Key(line, VK.VK_UP));
                line.CardHover(Middle(PieceBox(line, style, "Documents 0 name")), box, style, WidgetSurface.List);
                line.CardHover(Middle(PieceBox(line, style, $"pane {WinSearchAct.Copy}")), box, style, WidgetSurface.List);
                Assert.Equal("Packing list.docx", Said(line, style, "pane name"));
                Assert.Empty(Buttons(line, style, "Documents 0"));
            }
            // An answer: its copy at its end, the best match's like any.
            line.Box.Text = "12*(3+4)";
            fakes.Pump();
            Assert.Equal([WinSearchAct.Copy], Buttons(line, style, "best").Keys);
            _ = AssertAtTheEnd(line, style, "best", line.Results.Best!, edge);
            // The home's rows alike; its tiles have none (theirs are the pane's and the keys').
            line.Box.Text = "";
            fakes.Pump();
            line.CardHover(Middle(PieceBox(line, style, "files 0 name")), box, style, WidgetSurface.List);
            _ = AssertAtTheEnd(line, style, "files 0", line.Home.For(line.Settings.WinSearch).Single(section => section.Info.Id == WinSearchHomeInfos.Files).Hits[0], edge);
            line.CardHover(Middle(PieceBox(line, style, "folders 0 name")), box, style, WidgetSurface.List);
            Assert.Equal([WinSearchAct.Location, WinSearchAct.Copy], Buttons(line, style, "folders 0").OrderBy(each => each.Value.left).Select(each => each.Key));
            line.CardHover(Middle(PieceBox(line, style, "apps 0 name")), box, style, WidgetSurface.List);
            Assert.Empty(Buttons(line, style, "apps 0"));
        }));
    }

    [UiFact]
    public void CopyingAPathPutsItInQuotesAndSaysCopiedAnAnswerItsValue()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            var style = StyleFor(1, LookFor(dark: true));
            List<string> copied = [];
            WinSearchLine.Copier = copied.Add;
            try
            {
                Type(line, fakes, "packing");
                Assert.True(Key(line, 'C', Mods.Control));
                Assert.Equal("\"C:\\Users\\me\\Documents\\Packing list.docx\"", copied.Single());
                Assert.Equal(S.DockClipboardCopied, Said(line, style, "best copied"));
                line.Box.Text = "12*(3+4)";
                fakes.Pump();
                Assert.Equal("= 84", line.Results.Best?.Name);
                // Enter on an answer copies it, in place: the folder stays.
                Assert.True(Key(line, VK.VK_RETURN));
                Assert.Equal("84", copied[^1]);
                Assert.Null(line.Opening);
            }
            finally
            {
                WinSearchLine.Copier = null;
            }
        });
    }

    [UiFact]
    public void TheChipsFilterAndCtrlTabStepsThroughThem()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            var style = StyleFor(1, LookFor(dark: true));
            Type(line, fakes, "pa");
            // All, and each group that found something.
            Assert.Equal([null, WinSearchCategory.Apps, WinSearchCategory.Settings, WinSearchCategory.Documents, WinSearchCategory.Folders, WinSearchCategory.Photos, WinSearchCategory.Music], line.Chips());
            Assert.Equal(default, Click(line, style, $"chip {WinSearchWords.Category(WinSearchCategory.Photos)}"));
            fakes.Pump();
            Assert.Equal(WinSearchCategory.Photos, line.Chip);
            Assert.Equal("Paris.jpg", line.Results.Best?.Name);
            Assert.Contains("System.Kind = 'picture'", fakes.Indexed.Asked[^1], StringComparison.Ordinal);
            Assert.True(Key(line, VK.VK_TAB, Mods.Control));
            fakes.Pump();
            Assert.Equal(WinSearchCategory.Videos, line.Chip);
            Assert.True(Key(line, VK.VK_TAB, Mods.Control | Mods.Shift));
            Assert.True(Key(line, VK.VK_LEFT, Mods.Control));
            Assert.Equal(WinSearchCategory.Folders, line.Chip);
            // Escape: the chip and the words go, the keys stay; again, the folder's.
            Assert.True(Key(line, VK.VK_ESCAPE));
            Assert.Null(line.Chip);
            Assert.Equal("", line.Box.Text);
            Assert.False(Key(line, VK.VK_ESCAPE));
        });
    }

    [UiFact]
    public void TheHomeListsItsSectionsTheSearchesTheAppsAsTilesTheFilesAndFolders()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            var history = new WinSearchHistory();
            history.Searched("tax");
            history.Searched("budget");
            using var line = Line(gfx, fakes, history);
            var style = StyleFor(1, LookFor(dark: true));
            Assert.Equal(S.DockWinSearchRecentSearches, Said(line, style, "heading searches"));
            Assert.Equal("budget", Said(line, style, "search 0"));
            // Top apps: Paint opened most, then Code; Pinned: the rest of the apps, Paint and Code shown once already.
            Assert.Equal(S.CoreWinSearchHomeApps, Said(line, style, "heading apps"));
            Assert.Equal("Paint", Said(line, style, "apps 0 name"));
            Assert.Equal("Visual Studio Code", Said(line, style, "apps 1 name"));
            Assert.Equal("Paint 3D", Said(line, style, "pinned 0 name"));
            Assert.Equal("plan.docx", Said(line, style, "files 0 name"));
            Assert.Equal("Trips", Said(line, style, "folders 0 name"));
            Assert.Empty(line.Chips());
            // A tile opens its app.
            List<(WinSearchAct, string)> launched = [];
            line.Launched = (act, hit) => launched.Add((act, hit.Name));
            Assert.Equal(IslandView.App, Click(line, style, "apps 0 name").View);
            line.OpenChosen();
            Assert.Equal((WinSearchAct.Open, "Paint"), launched.Single());
            // A search made lately typed again by a click; Delete on one forgets it.
            Assert.Equal(default, Click(line, style, "search 1"));
            fakes.Pump();
            Assert.Equal("tax", line.Box.Text);
            line.Box.Text = "";
            fakes.Pump();
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_DELETE));
            Assert.Equal(["tax"], history.Searches);
            // "Clear search history" at the heading's end: everything remembered forgotten; the section goes.
            Assert.Equal(S.DockWinSearchClearHistory, Said(line, style, "heading searches all"));
            Assert.Equal(default, Click(line, style, "heading searches all"));
            Assert.Empty(history.Searches);
            Assert.Null(Said(line, style, "heading searches"));
            // The person's own: the files alone, two of them.
            var options = new WinSearchOptions();
            options.SetHome([WinSearchHomeInfos.Files]);
            options.SetHomeMost(WinSearchHomeInfos.Files, 1);
            line.Settings.WinSearch = options;
            Assert.Null(Said(line, style, "heading apps"));
            Assert.Equal("plan.docx", Said(line, style, "files 0 name"));
            Assert.Null(Said(line, style, "files 1 name"));
            // Nothing found: says so; no foot.
            fakes.Indexed.Rows = [];
            Type(line, fakes, "zzqx");
            Assert.Equal(S.DockWinSearchNoMatch("zzqx"), Said(line, style, "note"));
        });
    }

    [UiFact]
    public void AGhostShowsTheSamplesHomeAndTheirAnswersAskingNothingOfThisPC()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            var asked = new List<string>();
            using var line = Line(gfx, fakes, ghost: true, icons: (key, _, _) =>
            {
                asked.Add(key);
                return null;
            });
            var style = StyleFor(1, LookFor(dark: true));
            Assert.True(line.Sampled);
            Assert.False(line.Box.Focused);
            // §655: its home, the samples': the searches, the apps opened most and pinned, the files and folders, the mail.
            Assert.Equal("budget", Said(line, style, "search 0"));
            Assert.Equal("Microsoft Edge", Said(line, style, "apps 0 name"));
            Assert.NotNull(Said(line, style, "pinned 0 name"));
            Assert.Equal(Karpo.Dock.StartWidgets.StartSamples.SampleFiles[0].Name, Said(line, style, "files 0 name"));
            Assert.Equal(Karpo.Dock.StartWidgets.StartSamples.SampleFolders[0].Name, Said(line, style, "folders 0 name"));
            Assert.NotNull(Said(line, style, "heading mail"));
            // Typed: the samples' answers, the best match an app, then the groups of several sources.
            Type(line, fakes, WinSearchSamples.Typed);
            Assert.Equal("Microsoft Teams", Said(line, style, "best name"));
            Assert.Equal(
                [WinSearchCategory.Apps, WinSearchCategory.Documents, WinSearchCategory.Folders, WinSearchCategory.Photos, WinSearchCategory.Email, WinSearchCategory.Events, WinSearchCategory.Notes],
                line.Results.Groups.Select(group => group.Category).Where(category => category != WinSearchCategory.Karpo));
            Assert.Empty(fakes.Indexed.Asked);
            Assert.Empty(fakes.Work);
            // Its icons: a file's and a folder's their kinds', no path of this PC's; the apps' by their Apps folder ids.
            var pieces = Pieces(line, style);
            Assert.Contains(WinSearchHit.KindIcon("x.pptx"), asked);
            Assert.Contains(WinSearchHit.KindIcon("", folder: true), asked);
            Assert.Contains(asked, key => key.StartsWith(StartApp.AppsFolder, StringComparison.OrdinalIgnoreCase));
            Assert.All(asked, key => Assert.True(key.StartsWith(WinSearchHit.KindScheme, StringComparison.Ordinal) || key.StartsWith(StartApp.AppsFolder, StringComparison.OrdinalIgnoreCase), key));
            // While none has come, each row wears its glyph: the best match's, a document's, a mail's.
            Assert.Contains(pieces, piece => piece.Name == "best glyph");
            Assert.Contains(pieces, piece => piece.Name == $"{WinSearchWords.Category(WinSearchCategory.Documents)} 0 glyph");
            Assert.Contains(pieces, piece => piece.Name == $"{WinSearchWords.Category(WinSearchCategory.Email)} 0 glyph");
        });
    }

    // --- §655: wide, the pane ---------------------------------------------------------------------------------

    /// <summary>The line laid out <paramref name="dip"/> DIP wide while <paramref name="test"/> runs.</summary>
    private static void At(float dip, Action test)
    {
        widthDip = dip;
        try
        {
            test();
        }
        finally
        {
            widthDip = 0;
        }
    }

    private static D2D_RECT_F PieceBox(WinSearchLine line, in TileStyle style, string name) => Assert.Single(Pieces(line, style), piece => piece.Name == name).Box;

    [UiFact]
    public void WideThePaneShowsTheChosenResultAndItsActionsBesideTheList()
    {
        Offscreen.OnStaThread(gfx => At(700, () =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            var style = StyleFor(1.5f, LookFor(dark: true));
            var whole = Whole(line, style);
            // With nothing typed the pane shows the home's first; the split where the line's width puts it.
            var pane = PieceBox(line, style, "pane");
            var (listRight, paneLeft) = WinSearchLine.Split(whole.right - whole.left, style, ListForm.Of(gfx, style));
            Assert.Equal(paneLeft, pane.left);
            Assert.True(PieceBox(line, style, "files 0 name").right <= listRight);
            Type(line, fakes, "pa");
            // Typed: the best match, its actions a row each in Windows' order — and §659 at its row's end too, as every row's.
            Assert.Equal((pane.left, pane.right), (PieceBox(line, style, "pane").left, PieceBox(line, style, "pane").right));
            Assert.Equal("Paint", Said(line, style, "pane name"));
            Assert.Equal(WinSearchWords.AppDetail, Said(line, style, "pane kind"));
            Assert.Equal([S.DockWidgetMenuOpen, S.DockWidgetMenuRunAsAdministrator],
                Pieces(line, style).Where(piece => piece.Name.StartsWith("pane ", StringComparison.Ordinal) && !piece.Name.EndsWith(" glyph", StringComparison.Ordinal)
                    && piece.Name is not ("pane name" or "pane kind" or "pane folder" or "pane facts" or "pane detail")).Select(piece => piece.Text));
            Assert.Null(Said(line, style, "best Open"));
            Assert.True(PieceBox(line, style, "best Admin").right <= listRight);
            // The keys walk: the pane follows the row they are on, a file now, with its folder, when it changed, its actions.
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.True(Key(line, VK.VK_DOWN));
            Assert.Equal("Packing list.docx", Said(line, style, "pane name"));
            Assert.Equal(@"C:\Users\me\Documents", Said(line, style, "pane folder"));
            Assert.Equal(S.DockWidgetMenuOpenFileLocation, Said(line, style, $"pane {WinSearchAct.Location}"));
            Assert.Equal(S.DockWidgetMenuCopyAsPath, Said(line, style, $"pane {WinSearchAct.Copy}"));
            Assert.Equal(S.DockWinSearchOpenWith, Said(line, style, $"pane {WinSearchAct.OpenWith}"));
            // The pointer on another row: the pane shows it, and keeps it as the pointer crosses to the pane.
            var box = Whole(line, style);
            line.CardHover(Middle(PieceBox(line, style, "Photos 0 name")), box, style, WidgetSurface.List);
            Assert.Equal("Paris.jpg", Said(line, style, "pane name"));
            List<(WinSearchAct, string)> launched = [];
            line.Launched = (act, hit) => launched.Add((act, hit.Name));
            Assert.Equal(IslandView.App, Click(line, style, $"pane {WinSearchAct.OpenWith}").View);
            line.OpenChosen();
            Assert.Equal((WinSearchAct.OpenWith, "Paris.jpg"), launched.Single());
            // Enter opens the row the keys chose.
            Assert.True(Key(line, VK.VK_UP));
            Assert.Equal(IslandView.App, Enter(line).View);
            line.OpenChosen();
            Assert.Equal(WinSearchAct.Open, launched[^1].Item1);
        }));
    }

    [UiTheory]
    [InlineData(280f, false)]
    [InlineData(480f, false)]
    [InlineData(559f, false)]
    [InlineData(560f, true)]
    [InlineData(640f, true)]
    [InlineData(900f, true)]
    public void ThePaneStandsFromFiveHundredSixtyDipItsSplitTheWidthsAlone(float dip, bool pane)
    {
        Offscreen.OnStaThread(gfx => At(dip, () =>
        {
            var fakes = new WinSearchFakes();
            using var line = Line(gfx, fakes);
            var style = StyleFor(1, LookFor(dark: false));
            var home = Pieces(line, style).Where(piece => piece.Name == "pane").Select(piece => (D2D_RECT_F?)piece.Box).FirstOrDefault();
            Assert.Equal(pane, home is not null);
            // Typing, the chips, a pointer: the split never moves, nothing of the list crosses it.
            Type(line, fakes, "pa");
            var typed = Pieces(line, style).Where(piece => piece.Name == "pane").Select(piece => (D2D_RECT_F?)piece.Box).FirstOrDefault();
            Assert.Equal(home?.left, typed?.left);
            Assert.Equal(home?.right, typed?.right);
            if (pane)
            {
                Assert.InRange(typed!.Value.right - typed.Value.left, style.Px(240) - 1, style.Px(360) + 1);
                Assert.All(Pieces(line, style).Where(piece => piece.Name.StartsWith("Documents ", StringComparison.Ordinal)), piece => Assert.True(piece.Box.right <= typed.Value.left));
                Assert.True(line.ScrollAside(Whole(line, style).right, style) > typed.Value.right - typed.Value.left);
            }
            else
            {
                Assert.Equal(0, line.ScrollAside(Whole(line, style).right, style));
            }
            // §659: either way the best match's actions at its end, none under it.
            Assert.NotNull(Said(line, style, "best Admin"));
            Assert.Null(Said(line, style, "best Open"));
            // At 280 DIP a home's tiles stand three across.
            if (dip <= 280)
            {
                line.Box.Text = "";
                fakes.Pump();
                var tiles = Pieces(line, style).Where(piece => piece.Name.StartsWith("pinned ", StringComparison.Ordinal) && piece.Name.EndsWith(" name", StringComparison.Ordinal)).ToList();
                Assert.Equal(tiles[0].Box.top, tiles[1].Box.top);
            }
        }));
    }

    /// <summary>§652: every row asks Windows' own icon — a file's and a folder's path, an app's address, Settings' app — and wears it once it has come.</summary>
    [UiFact]
    public void EveryRowWearsWindowsOwnIconItsGlyphWhileItComes()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fakes = new WinSearchFakes();
            var asked = new List<string>();
            var come = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var told = 0;
            var icon = new Pixels { Data = new byte[4 * 16 * 16], Width = 16, Height = 16 };
            using var line = Line(gfx, fakes, icons: (key, _, came) =>
            {
                asked.Add(key);
                if (come.Contains(key))
                {
                    return icon;
                }
                told++;
                return null;
            });
            var style = StyleFor(1, LookFor(dark: true));
            Type(line, fakes, "pa");
            var pieces = Pieces(line, style);
            Assert.Contains(@"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App", asked);
            Assert.Contains(@"C:\Users\me\Documents\Packing list.docx", asked);
            Assert.Contains(@"C:\Users\me\Pictures\Paris 2025", asked);
            Assert.Contains(pieces, piece => piece.Name == "best glyph");
            Assert.True(told > 0);
            // Come: the icon in the glyph's place.
            come.Add(@"shell:AppsFolder\Microsoft.Paint_8wekyb3d8bbwe!App");
            pieces = Pieces(line, style);
            Assert.Contains(pieces, piece => piece.Name == "best icon");
            Assert.DoesNotContain(pieces, piece => piece.Name == "best glyph");
            // Settings' pages wear Settings' own icon; an answer has none to ask for.
            asked.Clear();
            var others = new WinSearchFakes();
            using var settings = Line(gfx, others, icons: (key, _, _) =>
            {
                asked.Add(key);
                return null;
            });
            Type(settings, others, "display");
            _ = Pieces(settings, style);
            Assert.Contains(WinSearchPages.SettingsApp, asked);
        });
    }

    /// <summary>
    /// §661: a person found wears their picture — the one the contacts line keeps (ContactStore's ContactPhotos, in memory:
    /// nothing fetched for the search) — in a circle, in their row and in a wide line's pane; one without a picture, or whose
    /// picture has not come, the contacts' glyph, as the contacts line draws them. Red before: every person wore the glyph.
    /// </summary>
    [UiFact]
    public void APersonWearsTheirPictureTheContactsLineKeepsElseTheGlyph()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var paula = new Person("p", "Paula Weber", "Contoso", ["paula@example.com"], []) { Photo = "v1" };
            var pablo = new Person("q", "Pablo Diaz", null, ["pablo@example.com"], []) { Photo = "v2" };
            var parker = new Person("r", "Pat Parker", null, ["pat@example.com"], []);
            var store = new ContactStore(new AccountSet([]));
            store.Answered([paula, pablo, parker]);
            store.Pictured(paula, new Pixels { Data = new byte[4 * 64 * 64], Width = 64, Height = 64 });
            ContactsLine.Stand = store;
            try
            {
                var fakes = new WinSearchFakes { More = [new WinSearchContactsSource(() => [paula, pablo, parker])] };
                fakes.Indexed.Rows = [];
                foreach (var dip in new[] { 360f, 700f })
                {
                    widthDip = dip;
                    try
                    {
                        using var line = Line(gfx, fakes, icons: WinSearchLine.Picture);
                        var style = StyleFor(1.5f, LookFor(dark: true));
                        Type(line, fakes, "pa");
                        var pieces = Pieces(line, style);
                        string Wears(string person)
                        {
                            var name = Assert.Single(pieces, piece => piece.Text == person && piece.Name.EndsWith(" name", StringComparison.Ordinal)).Name[..^" name".Length];
                            return Assert.Single(pieces, piece => piece.Name == name + " photo" || piece.Name == name + " glyph" || piece.Name == name + " icon").Name[(name.Length + 1)..];
                        }
                        Assert.Equal("photo", Wears("Paula Weber"));
                        // Their picture is not in memory yet, or they have none: the glyph.
                        Assert.Equal("glyph", Wears("Pablo Diaz"));
                        Assert.Equal("glyph", Wears("Pat Parker"));
                        if (dip >= WinSearchLine.WideDip)
                        {
                            // The pane shows the best match, Paula: her picture, large.
                            line.CardHover(Middle(Assert.Single(pieces, piece => piece.Text == "Paula Weber" && piece.Name.EndsWith(" name", StringComparison.Ordinal)).Box), Whole(line, style), style, WidgetSurface.List);
                            Assert.Contains(Pieces(line, style), piece => piece.Name == "pane photo");
                        }
                    }
                    finally
                    {
                        widthDip = 0;
                    }
                }
            }
            finally
            {
                ContactsLine.Stand = null;
                store.Dispose();
            }
        });
    }

    /// <summary>
    /// §661: people found, Medium and Extra wide, dark: one wearing the picture the contacts line keeps (in its row, the best
    /// match, and the pane), one without a picture the contacts' glyph — winsearch5-person-dark.png.
    /// </summary>
    [UiFact]
    public void APersonsPicturePictures()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var people = new ContactStore(new AccountSet([]));
            var pat = new Person("r", "Pat Parker", "Fabrikam", ["pat@fabrikam.com"], []);
            people.Answered([WinSearchFakes.Paula, pat]);
            people.Pictured(WinSearchFakes.Paula, WinSearchFakes.Portrait());
            ContactsLine.Stand = people;
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            try
            {
                foreach (var width in new[] { FolderListWidth.Medium, FolderListWidth.ExtraWide })
                {
                    var holder = Holder();
                    holder.Folder!.Widths = [width];
                    var fakes = new WinSearchFakes { More = [new WinSearchContactsSource(() => [WinSearchFakes.Paula, pat])] };
                    fakes.Indexed.Rows = [];
                    WinSearchLine.Stand = () => fakes.Sources;
                    WinSearchLine.HistoryStand = new WinSearchHistory();
                    WinSearchLine.IconStand = WinSearchFakes.PictureIcons;
                    FolderIsland island;
                    try
                    {
                        island = new FolderIsland($"{Owner}/{holder.Id}-{panels.Count}", holder, gfx, new Config());
                    }
                    finally
                    {
                        WinSearchLine.Stand = null;
                        WinSearchLine.HistoryStand = null;
                        WinSearchLine.IconStand = null;
                    }
                    island.Opened(gfx);
                    fakes.Pump();
                    Type((WinSearchLine)island.Tiles[0].Island, fakes, width == FolderListWidth.Medium ? "pa" : "paula");
                    panels.Add(($"{FolderListWidths.Name(width)}, people{(width == FolderListWidth.Medium ? "" : ": the best match and its pane")}", look, island, Plan(island, look, 1.5f).Plan));
                }
                Sheet(gfx, "winsearch5-person-dark.png", 1.5f, panels);
            }
            finally
            {
                ContactsLine.Stand = null;
                people.Dispose();
                foreach (var (_, _, island, _) in panels)
                {
                    island.Closed();
                    island.Dispose();
                }
            }
        });
    }

    /// <summary>The preset: a folder wearing the search box face, holding the line, both Beta on the add page.</summary>
    [Fact]
    public void TheGroupIsASearchBoxFaceOverTheLineBothBeta()
    {
        var preset = Presets.Find(Presets.WinSearchId)!;
        Assert.True(preset.Beta);
        Assert.Equal(FolderFaces.WinSearchId, preset.Face?.Kind);
        Assert.Equal("winSearch", Assert.Single(preset.Items).Kind);
        Assert.True(WidgetInfos.WinSearch.Beta);
        var (lines, _, _) = AddList.Rows(WidgetType.List, turns: false, firstId: 1);
        Assert.Equal(S.DockAddBeta, AddList.Picks(lines).Single(item => item.Text == WidgetInfos.WinSearch.Title).Badge);
        // §641's Start widgets wear it too, one Beta for both (WidgetInfo.Beta); nothing else does.
        string[] beta = [.. WidgetInfos.All.Where(info => info.Beta).Select(info => info.Title)];
        Assert.All(AddList.Picks(lines).Where(item => !beta.Contains(item.Text)), item => Assert.Null(item.Badge));
        var (bar, _, _) = AddList.Rows(WidgetType.Bar, turns: false, firstId: 1);
        Assert.Equal(S.DockAddBeta, AddList.Picks(bar).Single(item => item.Text == preset.Title).Badge);
        // §652: its sources every one on but the web's, defaults not written.
        var cell = ListKind.WinSearch.New();
        cell.Normalize();
        Assert.Null(cell.WinSearch);
        Assert.All(WinSearchSourceInfos.All, info => Assert.Equal(info.Id != WinSearchSourceInfos.Web, WinSearchSourceInfos.On(cell.WinSearch, info.Id)));
    }

    // --- Pictures -----------------------------------------------------------------------------------------

    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.WinSearchId)!, new PresetMachine(HasBattery: false, FixedDrives: []));

    [UiFact]
    public void WinSearchPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                WinSearchLine.Copier = _ => { };
                // §661: Paula's picture, kept as the contacts line keeps it.
                var people = new ContactStore(new AccountSet([]));
                people.Answered([WinSearchFakes.Paula]);
                people.Pictured(WinSearchFakes.Paula, WinSearchFakes.Portrait());
                ContactsLine.Stand = people;
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Action<WinSearchLine, WinSearchFakes, TileStyle>? set = null, float text = 1f, bool ghost = false, WinSearchHistory? history = null, bool karpo = false)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder();
                        var fakes = new WinSearchFakes { More = karpo ? WinSearchFakes.Karpo() : [] };
                        WinSearchLine.Stand = () => fakes.Sources;
                        WinSearchLine.HistoryStand = history ?? new WinSearchHistory();
                        WinSearchLine.IconStand = WinSearchFakes.PictureIcons;
                        FolderIsland island;
                        // §635: a ghost's folder is made showing the samples, as the add page's is.
                        var samples = ghost ? Samples.Show() : default;
                        try
                        {
                            island = new FolderIsland($"{Owner}/{holder.Id}-{panels.Count}", holder, gfx, new Config());
                        }
                        finally
                        {
                            samples.Dispose();
                            WinSearchLine.Stand = null;
                            WinSearchLine.HistoryStand = null;
                            WinSearchLine.IconStand = null;
                        }
                        folders.Add(island);
                        island.Opened(gfx);
                        fakes.Pump();
                        var line = (WinSearchLine)island.Tiles[0].Island;
                        line.Ticks = static () => 1_000;
                        line.After = static (_, _) => { };
                        set?.Invoke(line, fakes, StyleFor(1.5f, look));
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    var recent = new WinSearchHistory();
                    recent.Searched("tax return");
                    recent.Searched("budget");
                    Add("Opened: its home", history: recent);
                    Add("Typed: best match and groups", (line, fakes, _) => Type(line, fakes, "pa"));
                    Add("Typed: Karpo's own too", (line, fakes, _) => Type(line, fakes, "paris"), karpo: true);
                    Add("A person, their picture", (line, fakes, _) =>
                    {
                        fakes.Indexed.Rows = [];
                        Type(line, fakes, "paula");
                    }, karpo: true);
                    Add("A row under the pointer", (line, fakes, style) =>
                    {
                        Type(line, fakes, "pa");
                        var box = Whole(line, style);
                        line.CardHover(Middle(Assert.Single(Pieces(line, style), piece => piece.Name == "Documents 0 name").Box), box, style, WidgetSurface.List);
                    });
                    Add("A file, its path copied", (line, fakes, _) =>
                    {
                        Type(line, fakes, "packing");
                        Key(line, 'C', Mods.Control);
                    });
                    Add("An answer", (line, fakes, _) =>
                    {
                        fakes.Indexed.Rows = [];
                        Type(line, fakes, "12*(3+4)");
                    });
                    Add("Photos chosen", (line, fakes, style) =>
                    {
                        Type(line, fakes, "pa");
                        _ = Click(line, style, $"chip {WinSearchWords.Category(WinSearchCategory.Photos)}");
                        fakes.Pump();
                    });
                    Add("Nothing found", (line, fakes, _) =>
                    {
                        fakes.Indexed.Rows = [];
                        Type(line, fakes, "zzqx");
                    });
                    Add("The add page's ghost", ghost: true);
                    Add("225 % text", (line, fakes, _) => Type(line, fakes, "pa"), text: 2.25f);
                    Sheet(gfx, $"winsearch-line-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    WinSearchLine.Copier = null;
                    ContactsLine.Stand = null;
                    people.Dispose();
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// §655: the line as the group opens it, Medium (360 DIP: one list) and Extra wide (640 DIP: the list and the pane), its
    /// home and typed "pa" (a row under the pointer, the pane following it), at 150 % — winsearch3-&lt;narrow|wide&gt;-&lt;home|typed&gt;-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void WinSearchHomeAndPanePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var width in new[] { FolderListWidth.Medium, FolderListWidth.ExtraWide })
            {
                foreach (var typed in new[] { false, true })
                {
                    Offscreen.OnStaThread(gfx =>
                    {
                        var look = LookFor(dark);
                        var holder = Holder();
                        holder.Folder!.Widths = [width];
                        var fakes = new WinSearchFakes { More = WinSearchFakes.Karpo() };
                        var history = new WinSearchHistory();
                        history.Searched("tax return");
                        history.Searched("budget");
                        WinSearchLine.Stand = () => fakes.Sources;
                        WinSearchLine.HistoryStand = history;
                        WinSearchLine.IconStand = WinSearchFakes.PictureIcons;
                        FolderIsland island;
                        try
                        {
                            island = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                        }
                        finally
                        {
                            WinSearchLine.Stand = null;
                            WinSearchLine.HistoryStand = null;
                            WinSearchLine.IconStand = null;
                        }
                        try
                        {
                            island.Opened(gfx);
                            fakes.Pump();
                            var line = (WinSearchLine)island.Tiles[0].Island;
                            var style = StyleFor(1.5f, look);
                            if (typed)
                            {
                                Type(line, fakes, "pa");
                            }
                            widthDip = FolderListWidths.Dip(width);
                            try
                            {
                                // §661: typed, a document under the pointer; at home, a top app's tile — each lit edge to edge.
                                line.CardHover(Middle(PieceBox(line, style, typed ? "Documents 0 name" : "apps 0 name")), Whole(line, style), style, WidgetSurface.List);
                            }
                            finally
                            {
                                widthDip = 0;
                            }
                            var name = $"winsearch3-{(width == FolderListWidth.Medium ? "narrow" : "wide")}-{(typed ? "typed" : "home")}-{(dark ? "dark" : "light")}.png";
                            Sheet(gfx, name, 1.5f, [($"{FolderListWidths.Name(width)}, {(typed ? "typed" : "nothing typed")}", look, island, Plan(island, look, 1.5f).Plan)]);
                        }
                        finally
                        {
                            island.Closed();
                            island.Dispose();
                        }
                    });
                }
            }
        }
    }

    /// <summary>The face on bars of 40, 48 and 64 DIP and down a side bar, at 100 and 150 % — winsearch-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void WinSearchFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                WinSearchLine.Stand = () => new WinSearchFakes().Sources;
                WinSearchLine.HistoryStand = new WinSearchHistory();
                FolderIsland island;
                try
                {
                    island = new FolderIsland($"{Owner}/winsearch-face", Holder(), gfx, new Config());
                }
                finally
                {
                    WinSearchLine.Stand = null;
                    WinSearchLine.HistoryStand = null;
                }
                using var folder = island;
                var styles = new[] { (40, 96u, false), (48, 96u, false), (64, 96u, false), (48, 144u, false), (48, 96u, true) }
                    .Select(each => new IslandStyle(each.Item2 / 96f, each.Item3, Layout.CellPx(each.Item2, new Config { Height = each.Item1 }), Layout.IconPx(each.Item2, new Config { Height = each.Item1 }), look))
                    .ToList();
                // Wider than a glyph on a top or bottom bar: the box; a glyph's square down a side.
                Assert.True(folder.CellSize(0, styles[1]).Width > 3 * folder.CellSize(0, styles[4]).Height, $"{folder.CellSize(0, styles[1])} {folder.CellSize(0, styles[4])}");
                const float gap = 12;
                var width = gap + styles.Sum(style => folder.CellSize(0, style).Width + gap);
                var height = gap + styles.Max(style => folder.CellSize(0, style).Height) + gap;
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var x = gap;
                    foreach (var style in styles)
                    {
                        var (w, h) = folder.CellSize(0, style);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, gap, () => folder.PaintCell(dc, 0, style, w, h));
                        x += w + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"winsearch-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }

    /// <summary>
    /// §661: this PC's own files as the index names them — mixed kinds, a program, a shortcut, a folder, a file under a known
    /// folder Windows names otherwise ("Public Documents") — listed with the icons the shell gives them (TargetPictures.Sized,
    /// the dock's one loader), Medium and Extra wide, dark: winsearch5-real-&lt;narrow|wide&gt;-dark.png. Read only; only with
    /// KARPO_REAL_SHELL=1 as well.
    /// </summary>
    [UiFact]
    public void ThisPCsFilesWearTheirOwnIconsPictures()
    {
        if (Environment.GetEnvironmentVariable("KARPO_REAL_SHELL") != "1")
        {
            return;
        }
        var rows = new List<WinSearchRow>();
        foreach (var word in new[] { "report", "budget", "readme", "setup", "photobooth", "notes", "invoice", "karpo" })
        {
            Karpo.Dock.Drives.DriveReader.Start(() => rows.AddRange(new WindowsIndex().Query(WinSearchQuery.Sql(WinSearchQuery.Parse(word), false, 20)!, CancellationToken.None).Rows)).Wait();
        }
        var mixed = rows.Where(WinSearchRank.Lists).DistinctBy(row => row.Type.ToLowerInvariant()).Take(16)
            .Select((row, at) => row with { Name = "pa " + row.Name, Rank = 900 - at }).ToList();
        // Two of this PC's apps, named to answer "pa" too.
        IReadOnlyList<InstalledApp> listed = [];
        Karpo.Dock.Drives.DriveReader.Start(() => listed = InstalledApps.List()).Wait();
        var apps = listed
            .Where(app => app.Name is "Settings" or "File Explorer" or "Terminal" or "Calculator").Take(2)
            .Select(app => new WinSearchApp("pa " + app.Name, app.Target)).ToArray();
        foreach (var width in new[] { FolderListWidth.Medium, FolderListWidth.ExtraWide })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark: true);
                var style = StyleFor(1.5f, look);
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var documents in new[] { false, true })
                    {
                        var holder = Holder();
                        holder.Folder!.Widths = [width];
                        var fakes = new WinSearchFakes(apps);
                        fakes.Indexed.Rows = mixed;
                        WinSearchLine.Stand = () => fakes.Sources;
                        WinSearchLine.HistoryStand = new WinSearchHistory();
                        FolderIsland island;
                        try
                        {
                            island = new FolderIsland($"{Owner}/{holder.Id}-{panels.Count}", holder, gfx, new Config());
                        }
                        finally
                        {
                            WinSearchLine.Stand = null;
                            WinSearchLine.HistoryStand = null;
                        }
                        island.Opened(gfx);
                        fakes.Pump();
                        var line = (WinSearchLine)island.Tiles[0].Island;
                        Type(line, fakes, "pa");
                        widthDip = FolderListWidths.Dip(width);
                        try
                        {
                            if (documents)
                            {
                                _ = Click(line, style, $"chip {WinSearchWords.Category(WinSearchCategory.Documents)}");
                                fakes.Pump();
                            }
                            // The icons come off the thread: drawn until they are all in, ten seconds at the most.
                            for (var wait = 0; wait < 100 && Pieces(line, style).Any(piece => piece.Name.EndsWith(" glyph", StringComparison.Ordinal) && !piece.Name.StartsWith("pane", StringComparison.Ordinal)); wait++)
                            {
                                Thread.Sleep(100);
                            }
                            if (documents)
                            {
                                // A row under the pointer: lit edge to edge, its actions at its end.
                                line.CardHover(Middle(PieceBox(line, style, "Documents 1 name")), Whole(line, style), style, WidgetSurface.List);
                            }
                        }
                        finally
                        {
                            widthDip = 0;
                        }
                        panels.Add(($"{FolderListWidths.Name(width)}, this PC's files{(documents ? ", Documents" : "")}", look, island, Plan(island, look, 1.5f).Plan));
                    }
                    Sheet(gfx, $"winsearch5-real-{(width == FolderListWidth.Medium ? "narrow" : "wide")}-dark.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var (_, _, island, _) in panels)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }
}
