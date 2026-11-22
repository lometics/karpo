// §524 (the person, 2026-11-07: "the spacers between groups should be a moving stripe pattern and not the line"): an
// arranged folder's spacer (the file's "divider") is its whole room striped — thin diagonal stripes in the folder's text
// at a low alpha, moving slowly (SpacerStripes) — in place of §307's rule across its gap. At rest it is empty room.
//
// Held here, pure: the band is the spacer's room, a Fixed one's fixed room and a Grow one's all it grows to, and nothing
// at rest; the stripes lie across the whole band at every size and scale, and keep their place along it as it grows; their
// colour, stronger under the pointer and selected. On the real folder in a window (KARPO_UI_TESTS=1): each spacer's
// stripes where the plan has its band, stronger hovered and selected; moving while arranged and resting, standing still
// while lines are carried, give way or glide, with Karpo's Animations off and once the folder closes; none at rest.
// Pictures to KARPO_PNG_DIR as spacer-stripes-<dark|light>-150-<edit|hover|selected|rest>.png.

using System.Numerics;
using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static Karpo.Tests.Folders.FolderEditClickTests;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class SpacerStripesTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    private static CellItem Spacer(bool grows)
    {
        var spacer = ListKind.Divider.New();
        if (grows)
        {
            spacer.Divider = new DividerOptions { Style = DividerStyle.Grow };
        }
        return spacer;
    }

    /// <summary>A list: two pins, a Fixed spacer, a pin, a Grow spacer, the battery.</summary>
    private static FolderOptions Options()
    {
        var options = new FolderOptions();
        foreach (var line in new[] { ListKind.EnergySaver.New(), ListKind.NightLight.New(), Spacer(grows: false), ListKind.AirplaneMode.New(), Spacer(grows: true), ListKind.Battery.New() })
        {
            options.Put(line);
        }
        return options;
    }

    private const int Fixed = 2, Grows = 4;

    private static RectF Room(D2D_RECT_F card) => new(card.left, card.top, card.right - card.left, card.bottom - card.top);

    /// <summary>The band is the spacer's whole room: a Fixed one's fixed room, a Grow one's all it grows to; nothing for any other line, nor at rest.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void TheBandIsTheSpacersWholeRoom(float scale)
    {
        var options = Options();
        var rest = FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, 1040, editing: false);
        var editing = FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, 1040, editing: true);

        Assert.All(rest.Bands, band => Assert.True(band.IsEmpty));
        Assert.Equal(Room(editing.Cards[Fixed]), editing.Bands[Fixed]);
        Assert.Equal(FolderFlyout.SeparatorRoom(scale), editing.Bands[Fixed].Height, 3);
        Assert.Equal(Room(editing.Cards[Grows]), editing.Bands[Grows]);
        Assert.True(editing.Bands[Grows].Height > 4 * FolderFlyout.SeparatorRoom(scale), $"the Grow spacer's band is {editing.Bands[Grows].Height} px, not all it grows to");
        foreach (var other in new[] { 0, 1, 3, 5 })
        {
            Assert.True(editing.Bands[other].IsEmpty);
        }
        // Arranged nothing moves: the band is the room it has at rest.
        Assert.Equal(rest.Cards, editing.Cards);
    }

    /// <summary>
    /// The stroke reaches past the band at both ends, top and foot, whatever the lean, so the band is striped edge to
    /// edge; its thickness is the band's height, its stripes StripeDip wide and leaning, the corner a card's or half the band.
    /// </summary>
    [Theory]
    [InlineData(300f, 6f, 1f)]
    [InlineData(450f, 9f, 1.5f)]
    [InlineData(450f, 600f, 1.5f)]
    [InlineData(40f, 900f, 2f)]
    [InlineData(1f, 1f, 1f)]
    public void TheStripesLieAcrossTheWholeBand(float width, float height, float scale)
    {
        var laid = SpacerStripes.LayOf(new Vector2(width, height), scale);
        Vector2 At(float u, float v) => Vector2.Transform(new Vector2(u, v), laid.Transform);

        foreach (var v in new[] { 0f, 1f })
        {
            Assert.True(At(laid.From.X, v).X <= -SpacerStripes.PeriodDip * scale + 0.01f, $"the stripes start at {At(laid.From.X, v).X} at v {v}");
            Assert.True(At(laid.To.X, v).X >= width + (SpacerStripes.PeriodDip * scale) - 0.01f, $"the stripes end at {At(laid.To.X, v).X} at v {v}");
        }
        Assert.Equal(0.5f, laid.From.Y);
        Assert.Equal(0.5f, laid.To.Y);
        Assert.Equal(0f, At(0, 0).Y, 3);
        Assert.Equal(height, At(0, 1).Y, 3);
        // A stripe StripeDip wide along the band, its top a lean further along than its foot.
        Assert.Equal(SpacerStripes.StripeDip * scale, At(SpacerStripes.StripeDip, 1).X - At(0, 1).X, 3);
        Assert.Equal(height * SpacerStripes.Lean, At(0, 0).X - At(0, 1).X, 3);
        Assert.Equal(MathF.Min(MathF.Round(SpacerStripes.CornerDip * scale), MathF.Min(width, height) / 2), laid.Corner, 3);
    }

    /// <summary>A band that grows keeps its stripes where they were along it: its stroke starts a whole number of stripes before its foot.</summary>
    [Fact]
    public void AGrowingBandKeepsItsStripesInPlace()
    {
        foreach (var height in new[] { 9f, 90f, 900f })
        {
            var laid = SpacerStripes.LayOf(new Vector2(300, height), 1.5f);
            var stripes = laid.From.X / SpacerStripes.PeriodDip;
            Assert.Equal(MathF.Round(stripes), stripes, 3);
            Assert.Equal(Vector2.Zero, Vector2.Transform(new Vector2(0, 1), laid.Transform) - new Vector2(0, height));
        }
    }

    /// <summary>The folder's text at a low alpha, stronger under the pointer and selected; in high contrast the text itself.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheStripesAreTheTextFaint(bool dark)
    {
        var theme = FlyoutTheme.Folder(Dark with { Dark = dark });
        var rest = SpacerStripes.InkOf(theme, strong: false);
        var strong = SpacerStripes.InkOf(theme, strong: true);

        Assert.Equal((byte)MathF.Round(theme.Text.r * 255), rest.R);
        Assert.Equal((byte)MathF.Round(theme.Text.a * SpacerStripes.RestAlpha * 255), rest.A);
        Assert.Equal((byte)MathF.Round(theme.Text.a * SpacerStripes.StrongAlpha * 255), strong.A);
        Assert.True(rest.A < 80 && strong.A > rest.A);
        Assert.Equal(rest with { A = strong.A }, strong);

        var contrast = theme with { HighContrast = true };
        Assert.Equal((byte)MathF.Round(theme.Text.a * 255), SpacerStripes.InkOf(contrast, strong: false).A);
    }

    /// <summary>
    /// The real folder, arranged at 150 % in both themes: each spacer's stripes where its band is, moving; stronger hovered
    /// and selected; standing still while a line is carried and while the lines glide home, moving again once they rest;
    /// standing still with Karpo's Animations off, and once the folder closes; nothing at rest. Pictured.
    /// </summary>
    [UiFact]
    public void AnArrangedFoldersSpacersAreStripedAndMove()
    {
        var failures = new List<string>();
        void Expect(bool held, string what)
        {
            if (!held)
            {
                failures.Add(what);
            }
        }
        WindowShots.OnHiddenDesktop("spacer-stripes", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 1600, bottom = 900 };
            var anchor = new POINT { x = 1100, y = 900 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var dark in new[] { true, false })
            {
                var name = $"spacer-stripes-{(dark ? "dark" : "light")}-150";
                var island = BarKind.FolderList.New();
                island.Id = "spaced";
                island.Folder = Options();
                var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
                var look = TestLooks.LookFor(dark);
                // The folder has no panel (§464): its spacers' room shows the desktop, which a capture of its window leaves black
                // (and the light look's stripes, the text's dark, unseen). The pictures lay Windows' own desktop of the mode
                // under its lists, in its window, as it stands on the screen; nothing the test reads is changed by it.
                var host = (FlyoutHost)typeof(FolderFlyout).GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                using var desktop = host.Compositor.CreateSpriteVisual();
                using var desktopInk = host.Compositor.CreateColorBrush(dark ? Color.FromArgb(255, 0x20, 0x20, 0x20) : Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
                desktop.Brush = desktopInk;
                desktop.Size = new Vector2(8000, 8000);
                desktop.Offset = new Vector3(-4000, -4000, 0);
                bool Picture(string file)
                {
                    if (desktop.Parent != host.Content)
                    {
                        host.Content.Children.InsertAtBottom(desktop);
                        WindowShots.Settle(100);
                    }
                    return WindowShots.Save(flyout.Hwnd, file);
                }
                try
                {
                    flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.End, editing: true);
                    WindowShots.Settle(300);
                    Assert.True(Picture($"{name}-edit.png"));
                    var plan = flyout.Plan!;
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var band = flyout.Stripes[index].Visual;
                        var room = plan.Bands[index];
                        Expect(band.IsVisible == !room.IsEmpty, $"{name}: line {index}'s stripes are {(band.IsVisible ? "shown" : "hidden")}");
                        Expect(band.IsVisible == (index is Fixed or Grows), $"{name}: line {index} is no spacer, its stripes {(band.IsVisible ? "shown" : "hidden")}");
                        if (band.IsVisible)
                        {
                            Expect(band.Offset == new Vector3(room.X, room.Y, 0) && band.Size == room.Size, $"{name}: line {index}'s stripes stand at {band.Offset} {band.Size}, its band at {room.X},{room.Y} {room.Size}");
                        }
                    }
                    Expect(flyout.BandsMove == true, $"{name}: arranged, the stripes do not move ({flyout.BandsMove})");
                    var rest = flyout.Stripes[Fixed].Ink;
                    Expect(ReferenceEquals(rest, flyout.Stripes[Grows].Ink), $"{name}: the two spacers' stripes are not in one ink");

                    // Under the pointer, stronger.
                    var on = flyout.PanelOnScreen;
                    var grab = plan.Grab(Fixed);
                    var over = new POINT { x = on.left + (int)MathF.Round((grab.left + grab.right) / 2), y = on.top + (int)MathF.Round((grab.top + grab.bottom) / 2) };
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, over));
                    WindowShots.Settle(150);
                    Expect(flyout.Lit.Hot == Fixed, $"{name}: the pointer over the Fixed spacer lights line {flyout.Lit.Hot}");
                    Expect(!ReferenceEquals(flyout.Stripes[Fixed].Ink, rest) && ReferenceEquals(flyout.Stripes[Grows].Ink, rest), $"{name}: hovered, the Fixed spacer's stripes are not the stronger");
                    Expect(((CompositionColorBrush)flyout.Stripes[Fixed].Ink!).Color.A > ((CompositionColorBrush)rest!).Color.A, $"{name}: the stronger stripes are not stronger");
                    Assert.True(Picture($"{name}-hover.png"));

                    // Selected, stronger, in its ring; the pointer away.
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, flyout.ScreenOfCard(0)));
                    flyout.Picked = folder.Tiles[Grows].Island.Key;
                    WindowShots.Settle(200);
                    Expect(flyout.PickedShown, $"{name}: the Grow spacer selected shows no ring");
                    Expect(ReferenceEquals(flyout.Stripes[Fixed].Ink, rest) && !ReferenceEquals(flyout.Stripes[Grows].Ink, rest), $"{name}: selected, the Grow spacer's stripes are not the stronger");
                    Assert.True(Picture($"{name}-selected.png"));
                    flyout.Picked = null;

                    // A line carried, the others giving way: the stripes stand still; gliding home too; resting, they move.
                    var press = flyout.ScreenOfCard(0);
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - 12 }));
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, flyout.ScreenOfCard(5)));
                    Expect(flyout.LiftedLine.Lifted, $"{name}: the line pressed and moved is not carried");
                    Expect(flyout.Stripes.All(band => !band.Marching), $"{name}: a line carried, the stripes move");
                    _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                    if (flyout.Settling)
                    {
                        Expect(flyout.Stripes.All(band => !band.Marching), $"{name}: the lines gliding home, the stripes move");
                    }
                    WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
                    _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
                    Expect(!flyout.Settling && flyout.BandsMove == true, $"{name}: the lines at rest again, the stripes do not move ({flyout.BandsMove})");

                    // Closed, nothing moves.
                    flyout.Hide();
                    Expect(flyout.Stripes.All(band => !band.Marching), $"{name}: closed, the stripes move");

                    // Karpo's Animations off: the stripes stand still.
                    flyout.Show(gfx, look with { Animations = false }, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.End, editing: true);
                    WindowShots.Settle(200);
                    Expect(flyout.Stripes[Fixed].Visual.IsVisible && flyout.BandsMove == false, $"{name}: Animations off, the stripes are {(flyout.Stripes[Fixed].Visual.IsVisible ? "shown" : "hidden")}, moving {flyout.BandsMove}");
                    flyout.Hide();

                    // At rest: empty room, nothing drawn, nothing moving.
                    flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.End, editing: false);
                    WindowShots.Settle(200);
                    Assert.True(Picture($"{name}-rest.png"));
                    Expect(flyout.Stripes.All(band => !band.Visual.IsVisible && !band.Marching), $"{name}: at rest, a spacer's stripes show or move");
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
