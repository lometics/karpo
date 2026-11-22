// A separator in a folder's list (the file's "divider") is empty space (§505; the person, 2026-11-05: "the spacers
// should always just be empty space and not a line or dot or anything"; 2026-10-09: "a divider inside a list splits
// that list's panel background into separate cards with a gap ... NO line any more"): it splits its list into cards
// a gap apart, the folder separator's room (FolderFlyout.SeparatorRoom), and draws nothing (§307 to §502 it could be a
// line, a dot or nothing at all).
//
// Held here: it splits 6 DIPs apart, as before; two lists stand as far apart; a list splits into one card per run of
// lines, a divider first, last or doubled leaving no empty card; a list too tall ends its cards with the last line it
// shows; edit mode moves nothing and gives a divider a grab and its room striped (§524). Pictured in
// FolderSeparatorSheets, offscreen (folder-separators-<dark|light>-<100|150>.png), and the real folder in a window
// (folder-separators-window-<rest|edit>-dark-150.png), in KARPO_PNG_DIR.

using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;

namespace Karpo.Tests.Folders;

public sealed class FolderSeparatorTests
{
    private static Look Look => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    private static FolderOptions List(params ListKind[] kinds)
    {
        var options = new FolderOptions();
        foreach (var kind in kinds)
        {
            options.Put(kind.New());
        }
        return options;
    }

    /// <summary>A pin, a divider, a pin.</summary>
    private static FolderOptions Divided() => List(ListKind.EnergySaver, ListKind.Divider, ListKind.NightLight);

    private static FolderPlan Plan(FolderOptions options, float scale = 1f, bool editing = false, float work = 2000) =>
        FolderFlyout.Build(Look, scale, FolderView.List, options, options.Cells, work, editing: editing);

    private static float Gap(FolderPlan plan, int over, int under) => plan.Cards[under].top - plan.Cards[over].bottom;

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ASeparatorSplitsTheListSixDipsApart(float scale)
    {
        var plan = Plan(Divided(), scale);

        Assert.Equal(PopupPaint.Round(6 * scale), FolderFlyout.SeparatorRoom(scale), 3);
        // The gap is the whole room between the two lines: theirs of 2 DIPs gives way to it.
        Assert.Equal(PopupPaint.Round(6 * scale), Gap(plan, 0, 2), 3);
        Assert.Equal(PopupPaint.Round(6 * scale), plan.Cards[1].bottom - plan.Cards[1].top, 3);
        // Two cards: the line over it on one, the line under it on the other, each exactly its lines.
        var runs = plan.Lists[0].Runs;
        Assert.Equal(2, runs.Count);
        Assert.Equal(plan.Cards[0], runs[0]);
        Assert.Equal(plan.Cards[2], runs[1]);
        Assert.Equal([0, -1, 1], plan.CardRun);
        // The host is handed both, and nothing else: the folder has no title. Nothing is drawn.
        Assert.Equal([runs[0], runs[1]], plan.Pieces);
        Assert.All(plan.Bands, ink => Assert.True(ink.IsEmpty));
    }

    /// <summary>Two columns stand a divider's room apart (§505), each its own panel.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TwoColumnsStandADividersRoomApart(float scale)
    {
        var plan = Plan(new FolderOptions { Lists = [new(), new()] }, scale, editing: true);

        Assert.Equal(FolderFlyout.SeparatorRoom(scale), plan.ListGap, 3);
        Assert.Equal(plan.ListGap, plan.Lists[1].Box.left - plan.Lists[0].Box.right, 3);
    }

    /// <summary>
    /// A list of several groups is a card a group; a divider first, last or next to another parts
    /// nothing from nothing: its gap stays (it is where it was put, to be taken hold of in edit mode)
    /// but no empty card stands in it. A list of nothing but a divider is one card, the whole list.
    /// </summary>
    [Fact]
    public void ACardAGroupAndNoEmptyCard()
    {
        var three = Plan(List(ListKind.EnergySaver, ListKind.Divider, ListKind.NightLight, ListKind.AirplaneMode, ListKind.Divider, ListKind.Battery));
        Assert.Equal(3, three.Lists[0].Runs.Count);
        Assert.Equal(three.Cards[2].top, three.Lists[0].Runs[1].top, 3);
        Assert.Equal(three.Cards[3].bottom, three.Lists[0].Runs[1].bottom, 3);
        Assert.Equal([0, -1, 1, 1, -1, 2], three.CardRun);

        var edges = Plan(List(ListKind.Divider, ListKind.EnergySaver, ListKind.Divider, ListKind.Divider, ListKind.NightLight, ListKind.Divider));
        var runs = edges.Lists[0].Runs;
        Assert.Equal(2, runs.Count);
        Assert.Equal(edges.Cards[1], runs[0]);
        Assert.Equal(edges.Cards[4], runs[1]);
        // The divider first leaves its gap over the first card, the two together twice the gap.
        Assert.Equal(6, runs[0].top - edges.Lists[0].Top, 3);
        Assert.Equal(12, Gap(edges, 1, 4), 3);
        Assert.Equal(edges.Lists[0].Box.bottom, edges.Cards[5].bottom, 3);

        var alone = Plan(List(ListKind.Divider));
        Assert.Equal([alone.Lists[0].Box], alone.Lists[0].Runs);
    }

    /// <summary>
    /// A list taller than it may be scrolls nothing (2026-10-12): the lines that do not fit are not
    /// shown, whole ones from its end, and its cards end with the last line shown — a divider left
    /// last goes with what it stood over, so no bare gap stands at the foot.
    /// </summary>
    [Fact]
    public void ACardEndsWithTheLastLineShown()
    {
        var kinds = new List<ListKind>();
        for (var group = 0; group < 6; group++)
        {
            kinds.AddRange([ListKind.Divider, ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode]);
        }
        kinds.RemoveAt(0);
        var options = List([.. kinds]);
        var plan = Plan(options, work: 600);
        var list = plan.Lists[0];
        Assert.NotEmpty(list.Hidden);

        var pieces = plan.Pieces;
        Assert.All(pieces, piece => Assert.True(piece.top >= list.Box.top - 0.01f && piece.bottom <= list.Box.bottom + 0.01f));
        Assert.Equal(list.Box.top, pieces[0].top, 3);
        Assert.Equal(list.Box.bottom, pieces[^1].bottom, 3);
        var last = Enumerable.Range(0, plan.Cards.Count).Last(plan.Shows);
        Assert.Equal(list.Box.bottom, plan.Cards[last].bottom, 3);
        Assert.False(FolderFlyout.Divides(options.Cells[last]));
    }

    /// <summary>The open folder hands the host the plan's cards, and they stay there: nothing scrolls them.</summary>
    [UiFact]
    public void TheHostsPanelsAreThePlans()
    {
        WindowShots.OnHiddenDesktop("divider-scroll", (gfx, instance) =>
        {
            var island = BarKind.FolderList.New();
            island.Id = "scrolled";
            for (var group = 0; group < 6; group++)
            {
                if (group > 0)
                {
                    island.Folder!.Put(ListKind.Divider.New());
                }
                foreach (var kind in new[] { ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode })
                {
                    island.Folder!.Put(kind.New());
                }
            }
            var work = new RECT { left = 0, top = 0, right = 1600, bottom = 600 };
            using var folder = new FolderIsland("layout:default/scrolled", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Look, 1f, new POINT { x = 800, y = 600 }, work, Edge.Bottom, folder);
            var plan = flyout.Plan!;
            Assert.NotEmpty(plan.Lists[0].Hidden);
            Assert.Equal(plan.Pieces, flyout.PiecesShown);
            flyout.Hide();
        });
    }

    /// <summary>
    /// Edit mode moves nothing (the person: list spacing "must be identical in edit and view mode"),
    /// but a divider is taken hold of over a grab at least <see cref="FolderFlyout.DividerDip"/> tall,
    /// centred on its gap, laid over its neighbours' edges, and its whole room is striped (§524).
    /// Everything else is taken hold of by its card.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void EditModeGivesADividerAGrabAndMovesNothing(float scale)
    {
        var rest = Plan(Divided(), scale);
        var editing = Plan(Divided(), scale, editing: true);

        Assert.Equal(rest.Cards, editing.Cards);
        Assert.Equal(rest.Pieces, editing.Pieces);
        var grab = editing.Grab(1);
        var card = editing.Cards[1];
        Assert.True(grab.bottom - grab.top >= MathF.Max(card.bottom - card.top, PopupPaint.Round(FolderFlyout.DividerDip * scale)) - 0.01f);
        Assert.True(MathF.Abs(((grab.top + grab.bottom) / 2) - ((card.top + card.bottom) / 2)) <= 1f, "the grab is centred on the gap");
        Assert.True(grab.top <= editing.Cards[0].bottom && grab.bottom >= editing.Cards[2].top, "the grab reaches its neighbours' edges");
        Assert.True(editing.Holds(1));
        Assert.Equal(new RectF(card.left, card.top, card.right - card.left, card.bottom - card.top), editing.Bands[1]);
        Assert.True(editing.Bands[0].IsEmpty);
        Assert.Equal(editing.Cards[0], editing.Grab(0));
        Assert.Equal(editing.Cards[2], editing.Grab(2));
        // At rest the grab is the gap, and nothing is drawn.
        Assert.Equal(rest.Cards[1], rest.Grab(1));
        Assert.All(rest.Bands, ink => Assert.True(ink.IsEmpty));
    }

    /// <summary>
    /// A folder of a heading, two pins, a separator, a pin, a separator and the battery — dark and light, at 100 and
    /// 150 %, offscreen, at rest and arranged: its dividers empty space (arranged, their stripes are the compositor's).
    /// </summary>
    [UiFact]
    public void FolderSeparatorSheets()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    try
                    {
                        foreach (var (caption, editing) in new[] { ("At rest: empty space, 3 px either side", false), ("Arranged: nothing moves", true) })
                        {
                            var look = ListFormPictures.LookFor(dark);
                            var folder = ListFormPictures.Folder(gfx, new Config(), _ => { },
                                ListFormPictures.Line(ListKind.Heading, island => island.Heading!.Text = "Quick"),
                                ListFormPictures.Line(ListKind.EnergySaver),
                                ListFormPictures.Line(ListKind.NightLight),
                                ListFormPictures.Line(ListKind.Divider),
                                ListFormPictures.Line(ListKind.AirplaneMode),
                                ListFormPictures.Line(ListKind.Divider),
                                ListFormPictures.Line(ListKind.Battery));
                            foreach (var tile in folder.Tiles)
                            {
                                _ = tile.Island.Refresh(gfx);
                            }
                            var (_, plan) = ListFormPictures.Plan(folder, look, scale, editing);
                            Assert.Equal(FolderFlyout.SeparatorRoom(scale), plan.Cards[4].top - plan.Cards[2].bottom, 3);
                            Assert.Equal(3, plan.Lists[0].Runs.Count);
                            Assert.Equal(editing, !plan.Bands[3].IsEmpty);
                            panels.Add((caption, look, folder, plan));
                        }
                        ListFormPictures.Sheet(gfx, $"folder-separators-{(dark ? "dark" : "light")}-{scale * 100:0}.png", scale, panels);
                    }
                    finally
                    {
                        foreach (var (_, _, folder, _) in panels)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// The real folder in a window, dark at 150 %: its divider's visual hidden at rest — empty space, whatever style its
    /// layout wears —, and arranged its stripes where the plan has them — read back off the compositor, and pictured.
    /// </summary>
    [UiFact]
    public void TheOpenFolderDrawsNothingForItsSeparatorsAtRest()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("folder-separators", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1700, y = 1500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var editing in new[] { false, true })
            {
                var name = editing ? "edit" : "rest";
                var island = BarKind.FolderList.New();
                island.Id = "split";
                foreach (var kind in new[] { ListKind.EnergySaver, ListKind.NightLight, ListKind.Divider, ListKind.AirplaneMode, ListKind.Battery })
                {
                    island.Folder!.Put(kind.New());
                }
                var config = LookShorthand.Wearing(LookPalettes.PaperId);
                var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                try
                {
                    var look = TestLooks.LookFor(dark: true).Wearing(config);
                    flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.End, editing: editing);
                    WindowShots.Settle(300);
                    Assert.True(WindowShots.Save(flyout.Hwnd, $"folder-separators-window-{name}-dark-150.png"));
                    var plan = flyout.Plan!;
                    var visual = flyout.Stripes[2].Visual;
                    if (visual.IsVisible != editing)
                    {
                        failures.Add($"{name}: the separator's visual is {(visual.IsVisible ? "shown" : "hidden")}");
                    }
                    if (editing && (visual.Offset.Y != plan.Bands[2].Y || visual.Size != plan.Bands[2].Size))
                    {
                        failures.Add($"{name}: drawn at {visual.Offset.Y} {visual.Size}, planned at {plan.Bands[2].Y} {plan.Bands[2].Size}");
                    }
                    flyout.Hide();
                }
                finally
                {
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
