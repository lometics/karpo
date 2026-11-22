// How a list folder's lines look at rest and under the pointer (Karpo.Dock\FolderFlyout.cs): the fill a
// line lights with, and which lines never light: a heading and a divider.
//
// Backlog 21: "folders and pins inside a folder's subfolder don't highlight their background on hover."
// A list's line lit with the hover of a card, as the grid folder (since gone) drew one — the subtle
// fill blended into a card's seven tenths of white — which on the light theme's panel came out a few
// levels from the panel itself: the line was lit and nobody could see it. A line wears no card, so it
// lights with the subtle fill alone, as the
// rows of Windows' own lists and of Karpo's menus do. The rule is a function of plain values
// (FolderFlyout.FillOf, InkOf), held here against the panel it is drawn on.

using Karpo.Dock;
using Color = Windows.UI.Color;
using CardFill = Karpo.Dock.FolderFlyout.CardFill;

namespace Karpo.Tests.Folders;

public sealed class ListLineLookTests
{
    private static Look LookFor(bool dark) => new(Glass: false, Dark: dark, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary><paramref name="over"/> laid on <paramref name="under"/>, an opaque panel: what the eye sees.</summary>
    private static (float R, float G, float B) Over(Color over, Color under)
    {
        var a = over.A / 255f;
        return (
            (over.R * a) + (under.R * (1 - a)),
            (over.G * a) + (under.G * (1 - a)),
            (over.B * a) + (under.B * (1 - a)));
    }

    /// <summary>How far apart two colours are, in levels of the brightest channel's difference.</summary>
    private static float Apart((float R, float G, float B) one, (float R, float G, float B) other) =>
        MathF.Max(MathF.Abs(one.R - other.R), MathF.Max(MathF.Abs(one.G - other.G), MathF.Abs(one.B - other.B)));

    /// <summary>
    /// A list's line lights under the pointer as far from the panel as a menu's row does, in both
    /// themes.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AListLineLightsAsFarAsAMenusRow(bool dark)
    {
        var theme = FlyoutTheme.For(LookFor(dark));
        var panel = Over(theme.Base, Color.FromArgb(255, 0, 0, 0));
        var menuRow = Apart(Over(theme.SubtleHover, theme.Base), panel);
        var line = Apart(Over(FolderFlyout.InkOf(theme, CardFill.LineHover), theme.Base), panel);
        var pressed = Apart(Over(FolderFlyout.InkOf(theme, CardFill.LinePressed), theme.Base), panel);

        Assert.True(line >= menuRow - 0.5f, $"a line under the pointer is {line:0.0} levels from the panel; a menu's row is {menuRow:0.0}");
        Assert.True(line >= 5f, $"a line under the pointer is only {line:0.0} levels from the panel");
        Assert.True(pressed > 0, "a pressed line looks like the panel");
    }

    /// <summary>
    /// The fills, written out: a list's line wears none at rest and the subtle fill under the
    /// pointer; a heading never lights.
    /// </summary>
    [Fact]
    public void EachLineWearsItsFill()
    {
        Assert.Equal(CardFill.None, FolderFlyout.FillOf(hot: false, pressed: false, quiet: false));
        Assert.Equal(CardFill.LineHover, FolderFlyout.FillOf(hot: true, pressed: false, quiet: false));
        Assert.Equal(CardFill.LinePressed, FolderFlyout.FillOf(hot: true, pressed: true, quiet: false));
        Assert.Equal(CardFill.None, FolderFlyout.FillOf(hot: true, pressed: true, quiet: true));
        // §464: at rest a line of Windows' own background wears Windows' flyout's surface itself (no panel under it), solid
        // without transparency effects; a gap between two cards nothing: the highlight's own colour with no alpha, so a fade to
        // it changes the alpha alone (§417).
        var theme = FlyoutTheme.Folder(LookFor(true));
        var hover = theme.SubtleHover;
        Assert.Equal(theme.Base, FolderFlyout.InkOf(theme, CardFill.None));
        Assert.Equal(Color.FromArgb(0, hover.R, hover.G, hover.B), FolderFlyout.InkOf(theme, CardFill.None, carded: false));
        Assert.Equal(Color.FromArgb(0, hover.R, hover.G, hover.B), FolderFlyout.InkOf(theme, CardFill.None, new Palette(Card: new Argb(0, 0, 0, 0))));
    }

    // --- Headings --------------------------------------------------------------------------------

    private static (FolderOptions Options, List<FolderCell> Cells) List(params ListKind[] kinds)
    {
        var holder = BarKind.FolderList.New();
        foreach (var kind in kinds)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, kind.New()));
        }
        return (holder.Folder!, [.. holder.Folder!.Cells]);
    }

    /// <summary>
    /// A heading and a divider are quiet — neither lights under the pointer, neither is a thing to
    /// press — and nothing else is. A heading is set apart by its air, as Windows' Settings sets a
    /// group apart, and the list draws no line of its own anywhere (ListFolderTests).
    /// </summary>
    [Fact]
    public void AHeadingAndADividerAreQuiet()
    {
        var grouped = List(ListKind.Heading, ListKind.Pin, ListKind.Divider, ListKind.Pin);
        Assert.True(FolderFlyout.Quiet(grouped.Cells[0]));
        Assert.False(FolderFlyout.Quiet(grouped.Cells[1]));
        Assert.True(FolderFlyout.Quiet(grouped.Cells[2]));
        Assert.False(FolderFlyout.Quiet(grouped.Cells[3]));
        Assert.Equal([false, false, true, false], grouped.Cells.Select(cell => FolderFlyout.Divides(cell)));
    }

    /// <summary>
    /// A heading is its own height, a section heading's — its words' line and the air over and under
    /// it — less than a row and the same whatever it carries; nobody sets it, and a height a file
    /// still holds for one is dropped as it is read, its words kept (2026-10-11: "the heading should
    /// not have a configurable size, it does not even have to conform to the 1cell height").
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void AHeadingHasItsOwnHeight(float scale)
    {
        var heading = ListWidgets.Of(ListKind.Heading);
        Assert.Equal(ListLine.Card, heading.Line);
        Assert.Equal(CellAct.Nothing, heading.Act);
        Assert.Null(BarEntries.Find(WidgetInfos.Heading.Id));
        Assert.False(ListKind.Heading.HasHeight());

        var held = ListKind.Heading.New();
        held.Heading!.Text = "Work";
        held.Height = LineHeight.Exactly(6);
        held.Normalize();
        Assert.Null(held.Height);
        Assert.Equal("Work", held.Heading.Text);

        var (options, cells) = List(ListKind.Heading, ListKind.Pin, ListKind.Heading, ListKind.Pin);
        cells[2].Island!.Height = LineHeight.Exactly(8);
        var plan = FolderFlyout.Build(LookFor(false), scale, FolderView.List, options, cells, 2000);
        float Tall(int index) => plan.Cards[index].bottom - plan.Cards[index].top;
        var own = HeadingTile.Height(scale, 1);
        Assert.Equal(MathF.Round(32 * scale), own);
        Assert.Equal(own, Tall(0));
        Assert.Equal(own, Tall(2));
        Assert.True(own < Tall(1), $"a heading {own} px tall is no less than a row of {Tall(1)} px");
        // The lines under it stand straight under it, edge to edge (§410).
        Assert.Equal(plan.Cards[0].bottom, plan.Cards[1].top);
        // A larger text grows its words' line and nothing else.
        Assert.Equal(PopupPaint.Round(8 * scale) + PopupPaint.Round(20 * scale * 1.5f) + PopupPaint.Round(4 * scale), HeadingTile.Height(scale, 1.5f));
    }

    /// <summary>A heading's words are one line, trimmed, and no longer than a title.</summary>
    [Fact]
    public void AHeadingSaysOneLineOfATitle()
    {
        var heading = ListKind.Heading.New();
        heading.Heading!.Text = "  Work\r\nprojects  ";
        heading.Normalize();
        Assert.Equal("Work projects", heading.Heading.Text);

        heading.Heading.Text = new string('a', 100);
        heading.Normalize();
        Assert.Equal(HeadingOptions.MostLength, heading.Heading.Text.Length);

        var fresh = ListKind.Heading.New();
        Assert.Equal("", fresh.Heading!.Text);
    }
}
