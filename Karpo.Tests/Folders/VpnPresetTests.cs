// VPN is a preset (docs\widgets.md §125): what a click on the VPN entry gave — its connections, each
// connected or disconnected from its row — is a folder holding the connections' line (QuickPageTile
// hosting ChoicesPage over the VPN switch) and the premade VPN actions under it, under the VPN face:
// the shield, lit while a connection is up. Made-up connections throughout (SampleVpn through a VPN
// switch of the test's, QuickSwitches.Source): nothing is dialled for real.
//
// Pictured: vpn-preset-<dark|light>.png at 150 % and 225 % text, and the face, vpn-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class VpnPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private const string Book = @"C:\pbk\rasphone.pbk";

    /// <summary>Two connections, Home connected and Work not; and none.</summary>
    private static SampleVpn Two() => new(new VpnConnection("Work", Book, VpnState.Disconnected), new VpnConnection("Home", Book, VpnState.Connected)); // not-ui: sample connections' names

    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.VpnId)!, Machine);

    /// <summary>The preset's folder, its line's and its face's VPN switch reading <paramref name="vpn"/>; opened where asked.</summary>
    private static FolderIsland Made(Gfx gfx, SampleVpn vpn, bool open = true, IQuickSwitch? face = null)
    {
        QuickSwitches.Source = which => which == QuickSwitch.Vpn ? face ?? new VpnSwitch(() => vpn) : null;
        var holder = Holder();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        face = null;
        if (open)
        {
            folder.Opened(gfx);
        }
        return folder;
    }

    private static void Done(FolderIsland folder)
    {
        folder.Closed();
        folder.Dispose();
        QuickSwitches.Source = null;
    }

    private static TileStyle Style(Look look, float scale = 1.5f) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line laid out at the list's width, once its switch has read the connections (on the pool).</summary>
    private static (QuickPageTile Tile, D2D_RECT_F Box, IReadOnlyList<ListRow> Rows) Laid(FolderIsland folder, TileStyle style, Func<IReadOnlyList<ListRow>, bool>? until = null)
    {
        var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single();
        var width = FolderFlyout.ListWidth(1.5f);
        var deadline = Environment.TickCount64 + 5000;
        while (true)
        {
            tile.Pump();
            var box = PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style));
            tile.LayOut(box, style);
            var rows = tile.Placed.OfType<ListRow>().ToList();
            if ((until ?? (shown => !shown.Any(row => row.Title == S.DockQuickVpnLooking)))(rows) || Environment.TickCount64 > deadline)
            {
                return (tile, box, rows);
            }
            Thread.Sleep(20);
        }
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The connections as a line, then in the same card the premade VPN actions — "VPN" for the flyout's
    /// "VPN settings", and Network &amp; internet — under the VPN face, which has no name and nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheConnectionsAndTheirFooterUnderTheVpnFace()
    {
        var preset = Presets.Find(Presets.VpnId)!;
        Assert.Equal(FolderFaces.VpnId, preset.Face!.Kind);
        Assert.Equal(["vpnConnections", "actions"], preset.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.VpnActions, preset.Items[^1].Premade);
        Assert.Equal(("vpn", "network"), PremadeWidgets.VpnActions.Actions);
        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Vpn, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(QuickSwitch.Vpn, FolderFaces.SwitchOf(FolderFaces.Vpn));
        Assert.Equal([ListKind.VpnConnections, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>The entry is gone from the bar; the switch alone stays the line it was, in Connectivity.</summary>
    [Fact]
    public void TheEntryIsGoneAndTheSwitchStaysALine()
    {
        Assert.Null(BarEntries.Find("vpn"));
        Assert.Equal(WidgetSource.Switch, ListWidgets.Find("vpn")!.Drawing);
        Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find("vpnConnections")!.Drawing);
        Assert.Contains(Presets.Find("connectivityGroup")!.Items, item => item.Form == "vpn");
    }

    /// <summary>The list as the line has it: every connection, one alone too, the ones up marked; none says so.</summary>
    [Fact]
    public void TheListIsEveryConnectionTheOnesUpMarked()
    {
        var two = VpnChoices.List(Two().Connections);
        Assert.Equal(["Work", "Home"], two.Select(choice => choice.Text));
        Assert.Equal([false, true], two.Select(choice => choice.Checked == true));
        Assert.Equal(S.DockQuickConnected, two[1].Detail);
        var one = VpnChoices.List([new VpnConnection("Work", Book, VpnState.Disconnected)]);
        Assert.Single(one);
        var none = Assert.Single(VpnChoices.List([]));
        Assert.False(none.Enabled);
        Assert.Equal(S.DockQuickVpnNone, none.Text);
        // §404: where one is added: Windows' VPN page, whose "Add VPN" it is.
        Assert.Equal("ms-settings:network-vpn", none.Add);
        Assert.Equal(VpnChoices.AddLink, none.Add);
        Assert.Null(two[0].Add);
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: "VPN" over a row per connection, Home marked as connected; a click on Work
    /// dials it, a click on Home hangs it up — through the switch, off the dock's thread — and the folder
    /// stays.
    /// </summary>
    [UiFact]
    public void TheLineListsTheConnectionsAndAClickSwitchesOne() => Offscreen.OnStaThread(gfx =>
    {
        var vpn = Two();
        var folder = Made(gfx, vpn);
        try
        {
            var style = Style(LookFor(dark: true));
            var (tile, box, rows) = Laid(folder, style);
            Assert.Equal(QuickKind.Vpn, tile.Page);
            Assert.Contains(tile.Placed, widget => widget is Label { Text: "VPN" });
            Assert.Equal(["Work", "Home"], rows.Select(row => row.Title));
            Assert.Equal([false, true], rows.Select(row => row.Selected));
            var at = rows[0].Bounds;
            Assert.True(tile.CardPress(new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2), box, style, WidgetSurface.List));
            tile.CardRelease();
            Laid(folder, style, shown => shown.All(row => row.Selected));
            Assert.Equal(["connect Work"], vpn.Asked);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// §404 (the person, 2026-10-29: "if no vpn connection is setup there should be the add button"): none set up,
    /// the line is the kit's one Status — "No VPN connections set up" — with a standard "Add" against the trailing
    /// gutter, the words before it; its click opens Windows' VPN settings, where a connection is added, and the
    /// folder goes as Settings takes the focus.
    /// </summary>
    [UiFact]
    public void WithNoConnectionTheStatusOffersToAddOne() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, new SampleVpn());
        var opened = new List<string>();
        QuickPageTile.Opener = opened.Add;
        try
        {
            var style = Style(LookFor(dark: true));
            var (tile, box, rows) = Laid(folder, style);
            var none = Assert.Single(rows);
            Assert.Equal(S.DockQuickVpnNone, none.Title);
            var add = Assert.Single(tile.Placed.OfType<Button>(), button => button.Text == S.SettingsCommonAdd);
            Assert.Equal(ButtonStyle.Standard, add.Style);
            Assert.Equal(LineButton.Of(gfx, style).Height, add.Bounds.bottom - add.Bounds.top, 0.5f);
            var gutter = FolderFlyout.RowInset(style.Text(14));
            Assert.True(MathF.Abs(add.Bounds.right - (box.right - gutter)) <= 1, $"Add ends at {add.Bounds.right:0}, the gutter at {box.right - gutter:0}");
            Assert.True(add.Bounds.top >= none.Bounds.top && add.Bounds.bottom <= none.Bounds.bottom, "Add stands on the Status's row");
            Assert.True(none.Bounds.right <= add.Bounds.left, "the words run under Add");
            Assert.Equal(LineRows.Px(1, style), box.bottom - box.top, 0.5f);

            Assert.True(tile.CardPress(Middle(add.Bounds), box, style, WidgetSurface.List));
            tile.CardRelease();
            Assert.Equal([VpnChoices.AddLink], opened);
        }
        finally
        {
            QuickPageTile.Opener = null;
            Done(folder);
        }
    });

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// The face is the switch's shield, full while a connection is up and dimmed while none is or
    /// Windows has not answered; its hover is the switch's words; another face lets the switch go.
    /// </summary>
    [Fact]
    public void TheFaceIsLitWhileAConnectionIsUp()
    {
        Assert.False(SwitchFaceDrawing.Dim(new SampleSwitch("VPN", on: true)));
        Assert.True(SwitchFaceDrawing.Dim(new SampleSwitch("VPN", on: false)));
        Assert.True(SwitchFaceDrawing.Dim(new SampleSwitch("VPN", on: null)));
        Assert.True(SwitchFaceDrawing.Dim(new SampleSwitch("VPN", on: true, available: false)));
        Assert.True(SwitchFaceDrawing.Dim(null));
    }

    [UiFact]
    public void TheFaceFollowsItsSwitch() => Offscreen.OnStaThread(gfx =>
    {
        var on = new SampleSwitch("VPN", on: true, detail: "Home");
        var folder = Made(gfx, Two(), open: false, face: on);
        try
        {
            Assert.Same(on, folder.FaceSwitch);
            var style = new IslandStyle(1f, false, Layout.CellPx(96, new Config()), Layout.IconPx(96, new Config()), LookFor(dark: true));
            var face = FaceDrawings.Of(folder.Options.Face);
            Assert.Equal(SwitchIsland.Say("VPN", true, true, on.Note, "Home"), face.Tooltip(new FaceContext(gfx, style, folder.Options, folder)));
            var lit = face.Reading(new FaceContext(gfx, style, folder.Options, folder));
            on.Act();
            Assert.NotEqual(lit, face.Reading(new FaceContext(gfx, style, folder.Options, folder)));
            var (w, h) = folder.CellSize(0, style);
            Assert.Equal(FaceWidthTests.GlyphButton(style), w, 0.5f);

            var other = Holder();
            other.Folder!.Face = FolderFace.Of(FolderFaces.IconId);
            folder.Configure(other, new Config());
            Assert.Null(folder.FaceSwitch);
        }
        finally
        {
            Done(folder);
        }
    });

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>Two connections, one up; one, none; and at 225 % text — at 150 %.</summary>
    [UiFact]
    public void VpnPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, SampleVpn vpn, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, vpn);
                        folders.Add(folder);
                        Laid(folder, Style(look));
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Two connections: Home up", Two());
                    Add("One connection", new SampleVpn(new VpnConnection("Work", Book, VpnState.Disconnected, Problem: "The remote connection was not made."))); // not-ui: a sample problem
                    Add("None set up", new SampleVpn());
                    Add("225 % text", Two(), 2.25f);
                    Sheet(gfx, $"vpn-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    QuickSwitches.Source = null;
                }
            });
        }
    }

    /// <summary>The face on a bar of 40 and 48 DIP and at 150 %: off, on, and not on this PC — vpn-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void VpnFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                IQuickSwitch[] states = [new SampleSwitch("VPN", on: false), new SampleSwitch("VPN", on: true, detail: "Home"), new SampleSwitch("VPN", on: null, available: false)];
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                var islands = states.Select(state => Made(gfx, Two(), open: false, face: state)).ToList();
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => islands.Sum(island => island.CellSize(0, style).Width + gap) + gap);
                    var height = gap + styles.Max(style => islands[0].CellSize(0, style).Height) + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var style in styles)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            x += gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"vpn-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        Done(island);
                    }
                }
            });
        }
    }
}
