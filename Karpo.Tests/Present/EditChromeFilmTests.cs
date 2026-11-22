// Edit mode's chrome as one model (docs\widgets.md §359; EditChrome): the dim, the edit bar and the
// folder arranged under it come and go in the same commit as the bars' edit dress, so every frame is the
// before or the end — the frames between the commit and its fence too, which §354's fence alone left to
// tier 2 (a bar in edit dress a frame before its dim). Filmed as the dock's handler runs them
// (FrameFilm), animations off (tier 1, every frame): edit mode on and off without a folder and with one
// kept open through it; a folder opened and closed in edit mode, the edit bar's folder crumb with it.
// And where each stands: the dim under the arranged folder, under the bars, under the edit bar (ZPlan).

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;
using Plain = Karpo.Tests.Present.FlickerFilmTests.Plain;

namespace Karpo.Tests.Present;

public sealed class EditChromeFilmTests
{
    private const string Owner = "layout:default";

    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly RectI Bounds = new(0, 0, 1280, 720);

    /// <summary>The bar's strip along the bottom, as the films' bar stands.</summary>
    private static readonly RECT Strip = new() { left = 0, top = 648, right = 1280, bottom = 720 };

    /// <summary>The work area over the bar.</summary>
    private static readonly RECT Work = new() { right = 1280, bottom = 648 };

    private static readonly POINT Anchor = new() { x = 640, y = 648 };

    private static readonly Color Rest = Color.FromArgb(255, 0x20, 0x40, 0x80);

    private static readonly Color Dressed = Color.FromArgb(255, 0x30, 0x90, 0x50);

    /// <summary>
    /// The dim, in a colour the films can see: a capture (PrintWindow) gives what is translucent flattened on
    /// black, the dim's own 50 % black as black, which the film takes for nothing.
    /// </summary>
    private static readonly Color Dim = Color.FromArgb(0x80, 0xFF, 0x80, 0x00);

    /// <summary>
    /// Edit mode on (Dock.BeginEdit: the bar laid out edit-dressed, the display's chrome to its model) and
    /// off (Dock.EndEdit: the chrome to none, the bar at rest), each in one handler: every frame the before
    /// or the end.
    /// </summary>
    [UiFact]
    public void EditModeComesAndGoesInOneChangeEveryFrame()
    {
        OnHiddenDesktop("chrome-edit-mode", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            var on = FrameFilm.Shoot("chrome-edit-on", gfx, Display, () => Windows(bar, chrome), () =>
            {
                bar.Paint(Dressed);
                chrome.Update(Model(gfx));
            });
            Assert.True(Z.Above(chrome.Deck.Hwnd, bar.Hwnd) && Z.Above(bar.Hwnd, chrome.Shade.Hwnd), "the edit bar over the bar over the dim");
            var off = FrameFilm.Shoot("chrome-edit-off", gfx, Display, () => Windows(bar, chrome), () =>
            {
                chrome.Update(null);
                bar.Paint(Rest);
            });
            Seen(on);
            on.Holds(everyFrame: true);
            off.Holds(everyFrame: true);
        });
    }

    /// <summary>
    /// The limit of §354's fence alone, which the clear windows lift (§359): the dim's window shown at the
    /// fence, as it was, its content and the bar's edit dress at the commit before it — between the two the
    /// screen shows the bar dressed on an undimmed desktop. Looked at between them, the time held open (it is
    /// a frame on a quiet machine, more on a loaded one), and not as a film, whose captures a loaded run may
    /// take longer than that time to make.
    /// </summary>
    [UiFact]
    public void TheFenceAloneShowedTheBarDressedBeforeItsDim()
    {
        OnHiddenDesktop("chrome-fence-alone", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            // As before §359: the dim shown once its commit has landed, with the plan.
            Surfaces.Enlist(chrome.Shade.Hwnd, Stratum.Dim, clear: false);
            Surfaces.FenceHoldMs = 4000;
            HWND[] windows = Windows(bar, chrome);
            FrameFilm.Settle();
            var before = FrameFilm.Snap(Display, windows, 0, "before");
            var committed = Surfaces.Fences.Committed;
            using (Surfaces.Begin(gfx.Compositor))
            {
                bar.Paint(Dressed);
                chrome.Update(Model(gfx));
            }
            // The commit has answered; its fence is held.
            var until = Environment.TickCount64 + 3000;
            while (Surfaces.Fences.Committed == committed && Environment.TickCount64 < until)
            {
                FrameFilm.Pump();
                Thread.Sleep(5);
            }
            WindowShots.Settle(60);
            Assert.False(Surfaces.Settled, "the fence landed before it was looked at");
            var between = FrameFilm.Snap(Display, windows, 0, "between");
            Surfaces.FenceHoldMs = 120;
            until = Environment.TickCount64 + 6000;
            while (!Surfaces.Settled && Environment.TickCount64 < until)
            {
                FrameFilm.Pump();
                Thread.Sleep(10);
            }
            FrameFilm.Settle();
            var end = FrameFilm.Snap(Display, windows, 0, "end");
            var film = new Film("chrome-edit-on-fence-alone", Display) { Before = before, End = end };
            film.Frames.Add(between);
            film.Save();
            Assert.True(Surfaces.Settled);
            // Between the commit and the fence: the bar in its dress, no dim — neither the before nor the end.
            Assert.NotEmpty(film.Between(everyFrame: true));
            Assert.True(film.Differs(between, end) > 0.5, $"the dim was there before its fence: {film.Differs(between, end):P0} off the end");
            chrome.Update(null);
        });
    }

    /// <summary>
    /// Edit mode on with a folder open (the folder spread for it, FolderFlyout.Arrange, its crumb on the edit
    /// bar) and off with it kept (§348: FolderFlyout.Rest): every frame the before or the end. The folder
    /// sinks under the bar as the frame ends, before its spread content comes; it rises over the bar back at
    /// rest only once its content is at rest (ZPlan): never its edit dress over the bar for a frame.
    /// </summary>
    [UiFact]
    public void EditModeWithAFolderOpenComesAndGoesInOneChangeEveryFrame()
    {
        OnHiddenDesktop("chrome-edit-folder", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            var island = Readings("kept");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Still, 1f, Anchor, Work, Edge.Bottom, folder, display: Display);
            FrameFilm.Settle();
            Assert.True(Z.Above(flyout.Hwnd, bar.Hwnd), "at rest, the folder over the bar");
            var on = FrameFilm.Shoot("chrome-edit-on-folder", gfx, Display, () => [.. Windows(bar, chrome), flyout.Hwnd], () =>
            {
                bar.Paint(Dressed);
                flyout.Arrange();
                chrome.Update(Model(gfx, folder: "kept"));
            });
            Assert.True(Z.Above(chrome.Deck.Hwnd, flyout.Hwnd) && Z.Above(bar.Hwnd, flyout.Hwnd) && Z.Above(flyout.Hwnd, chrome.Shade.Hwnd),
                "arranged: the edit bar over the folder, the bar over it, the folder over its dim");
            var off = FrameFilm.Shoot("chrome-edit-off-folder", gfx, Display, () => [.. Windows(bar, chrome), flyout.Hwnd], () =>
            {
                chrome.Update(null);
                flyout.Rest();
                bar.Paint(Rest);
            });
            Assert.True(Z.Above(flyout.Hwnd, bar.Hwnd), "back at rest, the folder over the bar");
            on.Holds(0.005, everyFrame: true);
            off.Holds(0.005, everyFrame: true);
        });
    }

    /// <summary>
    /// A folder opened and closed in edit mode (Dock.ShowFolder, CloseFolder): the folder arranged and the
    /// edit bar's folder crumb in one change, every frame the before or the end; and gone again so.
    /// </summary>
    [UiFact]
    public void AFolderOpenedAndClosedInEditModeComesWithItsCrumbEveryFrame()
    {
        OnHiddenDesktop("chrome-folder", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance, Dressed);
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            var island = Readings("opened");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            // §357: the folder's view kept, as the dock keeps it: opened before, closed.
            flyout.Show(gfx, Still, 1f, Anchor, Work, Edge.Bottom, folder, editing: true, display: Display);
            flyout.Hide();
            chrome.Update(Model(gfx));
            FrameFilm.Settle();
            var open = FrameFilm.Shoot("chrome-folder-open", gfx, Display, () => [.. Windows(bar, chrome), flyout.Hwnd], () =>
            {
                flyout.Show(gfx, Still, 1f, Anchor, Work, Edge.Bottom, folder, editing: true, display: Display);
                chrome.Update(Model(gfx, folder: "opened"));
            });
            Assert.True(Z.Above(chrome.Deck.Hwnd, flyout.Hwnd) && Z.Above(bar.Hwnd, flyout.Hwnd) && Z.Above(flyout.Hwnd, chrome.Shade.Hwnd));
            var close = FrameFilm.Shoot("chrome-folder-close", gfx, Display, () => [.. Windows(bar, chrome), flyout.Hwnd], () =>
            {
                flyout.Hide();
                chrome.Update(Model(gfx));
            });
            open.Holds(0.005, everyFrame: true);
            close.Holds(0.005, everyFrame: true);
            chrome.Update(null);
        });
    }

    /// <summary>
    /// The model reconciled again with nothing changed moves nothing and draws nothing: the dim, the edit bar
    /// and a section's edit bar stay as they are, no window placed, no material made, no rail shown again.
    /// </summary>
    [UiFact]
    public void TheSameModelAgainChangesNothing()
    {
        OnHiddenDesktop("chrome-same", (gfx, instance) =>
        {
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            var surface = new object();
            var rail = new EditRailModel(surface, BarSection.Start, Still, 1f, new RECT { left = 100, top = 648, right = 400, bottom = 720 }, Edge.Bottom, Work, [new PartSpec("layout:default/volume", BarKind.Volume)], 0);
            var model = Model(gfx) with { Rails = [rail] };
            chrome.Update(model);
            FrameFilm.Settle();
            var deck = chrome.Deck.HostMade;
            var shown = chrome.Deck.Entrances;
            var placed = chrome.Rails[(surface, BarSection.Start)].Panel;
            using (Surfaces.Begin(gfx.Compositor))
            {
                chrome.Update(model with { Deck = model.Deck with { State = EditDeckState.Plain }, Rails = [rail with { NotShown = [.. rail.NotShown] }] });
                Assert.True(Surfaces.Settled, "the same model asked something of a window");
            }
            Assert.Equal(deck, chrome.Deck.HostMade);
            Assert.Equal(shown, chrome.Deck.Entrances);
            Assert.Equal(placed, chrome.Rails[(surface, BarSection.Start)].Panel);
            // The rail's panel is where the model said, worked out before it was shown (EditRail.PanelFor).
            Assert.Equal(EditRail.PanelFor(1f, rail.Bar, rail.Edge, rail.Work, anyHidden: true, 0), placed);
            // The edit bar's, too (EditDeck.PanelFor).
            Assert.Equal((RECT?)EditDeck.PanelFor(gfx, Still, 1f, model.Deck.Room, EditDeckState.Plain), chrome.Deck.Panel);
            chrome.Update(null);
        });
    }

    /// <summary>
    /// §612: the dim by the motion. Edit mode on, animations on: the dim fades in (the film has frames between the before and
    /// the end); the Appearance's preview (the model not dimmed): it fades clear, its window still in sight, a click on it the
    /// dim's; dressed again it fades back; edit mode off: it fades out, its window out of sight once it has. Animations off —
    /// Karpo's, or Windows' Animation effects, which Look.Motion carries —: each at once, every frame the before or the end.
    /// </summary>
    [UiFact]
    public void TheDimFadesByTheMotionAndIsClearInTheAppearancesPreview()
    {
        OnHiddenDesktop("chrome-dim-motion", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var chrome = new EditChrome(gfx, instance, 0, Dim);
            var moving = Still with { Animations = true };
            var on = FrameFilm.Shoot("dress-dim-on", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(Model(gfx, look: moving)), filmMs: 300);
            Assert.Equal((true, 1f), chrome.Shade.Faded);
            Seen(on);
            Assert.NotEmpty(on.Between(everyFrame: true));
            on.Save();
            var preview = FrameFilm.Shoot("dress-dim-preview", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(Model(gfx, look: moving) with { Dimmed = false }), filmMs: 300);
            Assert.Equal((true, 0f), chrome.Shade.Faded);
            Assert.True(IsWindowVisible(chrome.Shade.Hwnd), "the dim's window went in the preview");
            Assert.True(IsWindowVisible(chrome.Deck.Hwnd), "the edit bar went in the preview");
            Assert.True(preview.Differs(preview.Before, preview.End) > 0.5, "the preview did not clear the dim");
            Assert.NotEmpty(preview.Between(everyFrame: true));
            preview.Save();
            _ = FrameFilm.Shoot("dress-dim-back", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(Model(gfx, look: moving)), filmMs: 300);
            Assert.Equal((true, 1f), chrome.Shade.Faded);
            var off = FrameFilm.Shoot("dress-dim-off", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(null), filmMs: 300);
            Assert.NotEmpty(off.Between(everyFrame: true));
            off.Save();
            Assert.False(IsWindowVisible(chrome.Shade.Hwnd), "the dim's window stayed once it had faded out");

            // Animations off: at once, every frame the before or the end; the preview clear at once, its window there.
            var still = FrameFilm.Shoot("dress-dim-still-on", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(Model(gfx)));
            Assert.Equal((false, 1f), chrome.Shade.Faded);
            still.Holds(everyFrame: true);
            var plain = FrameFilm.Shoot("dress-dim-still-preview", gfx, Display, () => Windows(bar, chrome), () => chrome.Update(Model(gfx) with { Dimmed = false }));
            Assert.Equal((false, 0f), chrome.Shade.Faded);
            Assert.True(IsWindowVisible(chrome.Shade.Hwnd));
            plain.Holds(everyFrame: true);
            chrome.Update(null);
            FrameFilm.Settle();
            Assert.False(IsWindowVisible(chrome.Shade.Hwnd));
        });
    }

    /// <summary>
    /// §612: a folder open arranged in the Appearance's preview (FolderFlyout.Dress): still arranged — edit mode is on —, but
    /// plain, as at rest: its lines where they stand at rest, no "+", its window the panel's (not spread to the display); its
    /// dress back on, the "+" and the spread back. A folder at rest is never dressed by it.
    /// </summary>
    [UiFact]
    public void AnArrangedFolderIsPlainInTheAppearancesPreview()
    {
        OnHiddenDesktop("chrome-folder-dress", (gfx, instance) =>
        {
            var island = Readings("dressed");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Still, 1f, Anchor, Work, Edge.Bottom, folder, display: Display);
            FrameFilm.Settle();
            List<RECT?> Lines() => [.. flyout.LineKeys.Select(flyout.LineRect)];
            flyout.Dress(false);
            Assert.False(flyout.Arranged);
            flyout.Arrange();
            FrameFilm.Settle();
            Assert.True(flyout.DressWorn);
            Assert.NotEmpty(flyout.AddsOnScreen);
            var spread = WindowShots.Extent(flyout.Hwnd);
            flyout.Dress(false);
            FrameFilm.Settle();
            Assert.True(flyout.Arranged, "the preview ended the folder's edit mode");
            Assert.False(flyout.DressWorn);
            Assert.Empty(flyout.AddsOnScreen);
            var plain = (Lines(), WindowShots.Extent(flyout.Hwnd));
            Assert.NotEqual(spread, plain.Item2);
            flyout.Dress(true);
            FrameFilm.Settle();
            Assert.True(flyout.DressWorn);
            Assert.NotEmpty(flyout.AddsOnScreen);
            Assert.Equal(spread, WindowShots.Extent(flyout.Hwnd));
            // Plain is at rest: the lines and the window where edit mode's end leaves them (Rest).
            flyout.Rest();
            FrameFilm.Settle();
            static string Said((List<RECT?> Lines, RECT Window) at) => string.Join(" ", at.Lines.Select(line => line is { } r ? $"{r.left},{r.top},{r.right},{r.bottom}" : "-")) + $" window {at.Window.left},{at.Window.top},{at.Window.right},{at.Window.bottom}";
            Assert.Equal(Said(plain), Said((Lines(), WindowShots.Extent(flyout.Hwnd))));
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    /// <summary>
    /// The display's chrome as the dock's model has it: dimmed, its edit bar saying <paramref name="folder"/>'s crumb where one
    /// is open; in <paramref name="look"/> (still where none is given), whose motion the dim fades by (§612).
    /// </summary>
    private static EditChromeModel Model(Gfx gfx, string? folder = null, Look? look = null) => new(
        Bounds,
        new EditDeckModel(look ?? Still, 1f, new EditDeckRoom(Bounds, 0, 72),
            folder is null ? EditDeckState.Plain : EditDeckState.Plain with { Folder = new EditDeckSubject(OptionGlyphs.FolderOpen, folder) }),
        []);

    /// <summary>The windows the films look at: the bar, the dim, the edit bar.</summary>
    private static HWND[] Windows(Plain bar, EditChrome chrome) => [bar.Hwnd, chrome.Shade.Hwnd, chrome.Deck.Hwnd];

    /// <summary>A bar's stand-in along the bottom, in the bars' stratum.</summary>
    private static Plain Bar(Gfx gfx, HINSTANCE instance, Color? color = null)
    {
        var bar = new Plain(gfx, instance, Strip, color ?? Rest);
        Surfaces.Enlist(bar.Hwnd, Stratum.Bar);
        FrameFilm.Settle();
        return bar;
    }

    /// <summary>The dim is in the film: the end darker than the before all over the display, where nothing else stands.</summary>
    private static void Seen(Film film) =>
        Assert.True(film.Differs(film.Before, film.End) > 0.5, $"{film.Name}: the dim is not in the film ({film.Differs(film.Before, film.End):P0} of it changed)");

    /// <summary>A list folder of widgets that only read — a clock, the battery, a heading — fetching nothing, launching nothing.</summary>
    private static BarItem Readings(string id)
    {
        var folder = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = folder;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            folder.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = "Notes" };
        folder.Put(title);
        return island;
    }

    /// <summary>
    /// On a hidden desktop of its own, the time between a commit and its fence held open, as a loaded machine
    /// may take it: the films see what the screen shows meanwhile, whatever the timing of the run.
    /// </summary>
    private static void OnHiddenDesktop(string what, Action<Gfx, HINSTANCE> body) =>
        FlickerFilmTests.OnHiddenDesktop(what, (gfx, instance) =>
        {
            Surfaces.FenceHoldMs = 120;
            try
            {
                body(gfx, instance);
            }
            finally
            {
                Surfaces.FenceHoldMs = 0;
            }
        });
}
