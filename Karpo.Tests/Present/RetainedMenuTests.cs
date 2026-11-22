// A menu's rows kept, not made again (docs\widgets.md §356, the flicker design's phase 2): a Refresh that
// shows the same rows makes nothing, a pick draws its own row again and nothing else, the lit row's plate
// goes over to it lit — never faded in again —, rows that only moved are moved, an open submenu stays. Tier 3
// (what is made, counted) and, for the lit row, tier 1 with animations on: every frame the before or the end.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class RetainedMenuTests
{
    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    // High contrast, so the lit plate is the highlight's own colour: a fade of it shows in the film.
    private static readonly Look Moving = AddListPictures.LookFor(dark: true) with
    {
        Animations = true,
        HighContrast = true,
        HcWindow = Windows.UI.Color.FromArgb(255, 0, 0, 0),
        HcText = Windows.UI.Color.FromArgb(255, 255, 255, 255),
        HcHighlight = Windows.UI.Color.FromArgb(255, 0, 255, 255),
        HcHighlightText = Windows.UI.Color.FromArgb(255, 0, 0, 0),
    };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly POINT Anchor = new() { x = 640, y = 720 };

    [UiFact]
    public void ARefreshWithTheSameRowsMakesNothing()
    {
        WindowShots.OnHiddenDesktop("retained-same", (gfx, instance) =>
        {
            using var menu = Shown(gfx, instance, Still, Toggles(6, on: 3));
            menu.PointTo(3);
            var made = menu.Made;
            var host = menu.HostMade;
            var lit = menu.LitPlate;
            menu.Refresh(Toggles(6, on: 3));
            Assert.Equal(made, menu.Made);
            Assert.Equal(host, menu.HostMade);
            Assert.Equal(lit, menu.LitPlate);
        });
    }

    [UiFact]
    public void APickDrawsItsRowAloneAndItsPlateStaysLit()
    {
        WindowShots.OnHiddenDesktop("retained-pick", (gfx, instance) =>
        {
            using var menu = Shown(gfx, instance, Still, Toggles(6, on: 3));
            menu.PointTo(2);
            var made = menu.Made;
            var lit = menu.LitPlate;
            Assert.Equal(1f, lit?.Opacity);
            menu.Refresh(Toggles(6, on: 2));
            Assert.Equal(made.Elements + 2, menu.Made.Elements);
            Assert.Equal(2u, menu.HotId);
            Assert.Same(lit!.Value.Plate, menu.LitPlate?.Plate);
            Assert.Equal(1f, menu.LitPlate?.Opacity);
        });
    }

    [UiFact]
    public void AHeldStepperStepsItsRowAlone()
    {
        WindowShots.OnHiddenDesktop("retained-stepper", (gfx, instance) =>
        {
            using var menu = Shown(gfx, instance, Still, Stepped(4));
            menu.PointTo(2);
            var made = menu.Made;
            var lit = menu.LitPlate;
            menu.Refresh(Stepped(5));
            Assert.Equal(made.Elements + 1, menu.Made.Elements);
            Assert.Same(lit!.Value.Plate, menu.LitPlate?.Plate);
        });
    }

    [UiFact]
    public void RowsThatOnlyMovedAreMovedNotMadeAgain()
    {
        WindowShots.OnHiddenDesktop("retained-moved", (gfx, instance) =>
        {
            var rows = Toggles(5, on: 1);
            using var menu = Shown(gfx, instance, Still, rows);
            var before = menu.Looks.Single(row => row.Id == 4).Plate.Y;
            var made = menu.Made;
            menu.Refresh([new FlyoutItem(99, "New", new FlyoutIcon.Glyph("")), .. rows]);
            Assert.Equal(made.Elements + 1, menu.Made.Elements);
            Assert.True(menu.Looks.Single(row => row.Id == 4).Plate.Y > before);
        });
    }

    [UiFact]
    public void ARefreshKeepsTheOpenSubmenuWhereItStands()
    {
        WindowShots.OnHiddenDesktop("retained-submenu", (gfx, instance) =>
        {
            List<FlyoutItem> Rows(string word) =>
                [new FlyoutItem(1, "First"), new FlyoutItem(2, "More", Children: [new FlyoutItem(21, word), new FlyoutItem(22, "Two")])];
            using var menu = Shown(gfx, instance, Still, Rows("One"));
            _ = menu.Click(2);
            Assert.Equal(2, menu.OpenPanels.Count);
            var stood = menu.OpenPanels[1].Panel;
            menu.Refresh(Rows("Uno"));
            Assert.Equal(2, menu.OpenPanels.Count);
            Assert.Equal(stood, menu.OpenPanels[1].Panel);
        });
    }

    [UiFact]
    public void ALevelInAndBackMakesNoMaterialNoWindowAndNoRowAgain()
    {
        WindowShots.OnHiddenDesktop("retained-level", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            menu.Show(gfx, Still, 1f, [.. Toggles(3, on: 1), new FlyoutItem(40, "More", Children: Toggles(8, on: 2))], FlyoutLayout.List, Anchor, Display, Edge.Bottom, null);
            var host = menu.HostMade;
            menu.OpenLevel(40);
            Assert.Equal(1, menu.InlineDepth);
            var inLevel = menu.Made;
            _ = menu.Click(Flyout.InlineBackId);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(host, menu.HostMade);
            // The menu's own rows were kept out of sight while the level stood, and are shown again as they were.
            Assert.Equal(inLevel.Elements, menu.Made.Elements);
            Assert.Equal(inLevel.Surfaces, menu.Made.Surfaces);
        });
    }

    /// <summary>
    /// Tier 1 with animations on: a pick in a configuration menu — the lit row's toggle flipped, the menu shown
    /// again — never fades the lit row in again: every frame is the before or the end (the person's check by
    /// hand: "pick repeatedly in a configuration menu, the lit row steady").
    /// </summary>
    [UiFact]
    public void ThePickedRowStaysLitInEveryFrame()
    {
        WindowShots.OnHiddenDesktop("film-retained-pick", (gfx, instance) =>
        {
            using var menu = Shown(gfx, instance, Moving, Toggles(6, on: 3));
            menu.PointTo(2);
            FrameFilm.Settle();
            var film = FrameFilm.Shoot("menu-pick-lit", gfx, menu.PanelOnScreen, () => [menu.Hwnd], () => menu.Refresh(Toggles(6, on: 2)));
            film.Holds();
        });
    }

    /// <summary>
    /// What a menu keeps and moves draws as it would drawn afresh: a menu refreshed from one set of rows to
    /// another — a row more on top, one gone, a switch flipped, a stepper stepped, a heading and a rule moved —
    /// looks, pixel for pixel, as a menu shown afresh with the second.
    /// </summary>
    [UiFact]
    public void ARefreshedMenuLooksAsOneShownAfresh()
    {
        WindowShots.OnHiddenDesktop("retained-afresh", (gfx, instance) =>
        {
            List<FlyoutItem> Rows(bool after) =>
            [
                .. after ? [new FlyoutItem(90, "Added", new FlyoutIcon.Glyph(""))] : Array.Empty<FlyoutItem>(),
                new FlyoutItem(0, "Looks", Header: true),
                .. Toggles(4, on: after ? 2 : 3).Where(row => !after || row.Id != 4),
                new FlyoutItem(0, "", Separator: true),
                .. Stepped(after ? 7 : 4),
                new FlyoutItem(30, "Colour", Value: new FlyoutValue("Accent"), Children: [new FlyoutItem(31, "Red")]),
            ];
            using var kept = Shown(gfx, instance, Still, Rows(after: false));
            kept.Refresh(Rows(after: true));
            FrameFilm.Settle();
            byte[]? refreshed = null;
            Assert.True(WindowShots.Save(kept.Hwnd, "retained-refreshed.png", seen: (pixels, _) => refreshed = pixels));
            kept.Hide();
            using var fresh = Shown(gfx, instance, Still, Rows(after: true));
            Assert.Equal(kept.PanelOnScreen, fresh.PanelOnScreen);
            byte[]? shown = null;
            Assert.True(WindowShots.Save(fresh.Hwnd, "retained-afresh.png", seen: (pixels, _) => shown = pixels));
            Assert.Equal(shown!.Length, refreshed!.Length);
            var off = Enumerable.Range(0, shown.Length).Count(at => Math.Abs(shown[at] - refreshed[at]) > 24);
            Assert.True(off <= shown.Length / 1000, $"{off} of {shown.Length} bytes differ");
        });
    }

    /// <summary>
    /// Tier 4, a source rule: a menu hands its host nothing per showing — what it makes is its elements' and its
    /// frame's (Flyout.Retained.cs), handed over only as the menu closes (ReleaseKept). A host.Keep anywhere else
    /// in a menu's files is something made again for every Refresh. The tooltip's own host keeps its words per
    /// tooltip shown (Flyout.Primary.cs), until tips are kept views too (the flicker design's phase 5).
    /// </summary>
    [Fact]
    public void AMenuHandsItsHostNothingPerShowing()
    {
        if (RepoSource.Root().Length == 0)
        {
            return;
        }
        var folder = Path.Combine(RepoSource.Root(), "Karpo.Dock");
        var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(folder, "Flyout.*.cs").Append(Path.Combine(folder, "Flyout.cs")))
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var keeps = tree.GetRoot().DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Count(call => call.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax
                {
                    Expression: Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax { Identifier.Text: "host" },
                    Name.Identifier.Text: "Keep",
                });
            if (keeps > 0)
            {
                found[Path.GetFileName(file)] = keeps;
            }
        }
        Assert.Equal(new Dictionary<string, int> { ["Flyout.Retained.cs"] = 7 }, found);
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static Flyout Shown(Gfx gfx, HINSTANCE instance, Look look, List<FlyoutItem> rows)
    {
        var menu = new Flyout(gfx, instance, 0);
        menu.Show(gfx, look, 1f, rows, FlyoutLayout.List, Anchor, Display, Edge.Bottom, null);
        FrameFilm.Settle();
        return menu;
    }

    /// <summary><paramref name="count"/> rows of a switch each, row <paramref name="on"/>'s on.</summary>
    private static List<FlyoutItem> Toggles(int count, int on) =>
        [.. Enumerable.Range(1, count).Select(index => new FlyoutItem((uint)index, $"Row {index}", new FlyoutIcon.Glyph(""), Control: new FlyoutControl.Toggle(index == on)))];

    /// <summary>Three rows, the second a stepper at <paramref name="value"/>.</summary>
    private static List<FlyoutItem> Stepped(int value) =>
    [
        new FlyoutItem(1, "First"),
        new FlyoutItem(2, "Height", Control: new FlyoutControl.Stepper(value.ToString(), 20, 21, true, true, "Fewer", "More")),
        new FlyoutItem(3, "Third"),
    ];
}
