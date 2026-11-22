// edit, §536 (the person, 2026-11-07: "for all the elements where you can edit the style you should show a preview of one of
// the elements as well so that a user can see the style / look change directly"): a look's preview in the settings panel.
//
// The rows that edit the Appearance (Karpo.Core's LookRows: §592 the Appearance page's four tabs, Bar, Taskbar widgets,
// Flyouts, Widgets in folders; §567 no widget, folder or line has a look of its own) say which (LookShown), and the panel
// shows one card of it over them (PanelRows.PreviewOf, PanelTabs): a sample of what the look styles, drawn by Karpo's own
// painters in the look's values as they are now — every change of a value is rows made again, so the sample with them; a
// colour being mixed (§523) is tried on a config of its own, whose rows say so too. One rule picks the sample
// (LookPreview.SampleOf), no row of its own:
//
//   Whole   §592 (the person, 2026-11-09: "for the appearance settings page i want only one preview now") the whole look at
//           once: the bar, the very bar the layout's bars are (Scene, Layout, the widgets' own islands) — §613 Start, search,
//           Task view, Settings and the Snipping Tool in its middle, a folder wearing the clock at its end, each on its card —,
//           that folder open, centred in the room, its panel whole, its lines drawn as the open folder draws them
//           (FolderFlyout's painters): its outline round a clock, a world clock and a calendar. Every one of the four parts in it.
//           §634 the folder open, its backdrop (§623) over the desktop, under the bar and the folder, as it stands live.
//           §649 while the Flyouts say an attached folder's ground (or one is tried on, §622), that folder stands attached to
//           the bar (§647) on that ground, its lines flat on it: the one place the colour shows on the page. §651 the ground
//           the bar grown out: across the whole room, flush on the bar, its far side's outline, a seam between columns.
//
// A sample's room holds what it draws, whole (§571): a bar's thickness, a menu's plan, a folder's lines as tall as the open
// folder lays them out (LookPreviews.LinesDip: measured once a look, Windows' look and scale), never a guessed height.
//
// What a sample shows stands in for anything of this PC's or the network's (LookStand): the clock stands still at 9:41
// today, a calendar shows two made-up events; nothing else it draws reads anything. §613 (the person, 2026-11-09: "it should
// also show the background image of the desktop and it should show a light and dark mode icon"): it stands on the desktop's
// own picture — the one of the display the panel is on, as §547 reads it for the bars (only read), filling the room as
// Windows' Fill does —, sharp around the bar and blurred under it as the bar blurs it (Scene.Wallpaper, the Taskbar's Blur);
// a picture that cannot be read a quiet gradient of the mode. It is drawn in the one mode the taskbar is in (the Appearance's
// Mode, Looks.Dark: §582's two halves cut by a slanted line and §606's marks over them are gone with the colour pairs on the
// page), that mode's sun or moon in a badge at the room's top right. In high contrast Windows' one look, no badge. A sample
// is drawn once a look — its key is the look's values, the scale, the width, Windows' own look and the wallpaper — and only
// moved as the page scrolls (LookPreviews.Place); the panel's surface draws its Caption and its room alone
// (PanelPainter.Preview).

using System.Numerics;
using System.Runtime.CompilerServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

/// <summary>§536: what a look's preview draws (see the top of the file).</summary>
internal enum LookSample
{
    /// <summary>§592: the whole Appearance at once: the bar, its widgets, a folder open over its face with its lines.</summary>
    Whole,

    /// <summary>§551: the taskbar's right-click menu as the bar draws it now (the Taskbar menu page's preview).</summary>
    Menu,

    /// <summary>
    /// §628: a layout's own bar, every widget of it where it stands (LookShown.Itself: Add a layout's tile of a preset), laid out
    /// across a desktop <see cref="LookPreview.ItselfWideDip"/> wide and drawn as small as the tile's picture is.
    /// </summary>
    Itself,
}

/// <summary>§536: which sample a look gets, how tall its card is, what it shows and stands in for (see the top of the file). Pure: a folder's lines measured by its caller (§571, IPanelText.LinesDip).</summary>
internal static class LookPreview
{
    /// <summary>The preview's line: 12 over its Caption's 16 and 4 under it, its sample's room, 12 under that; 16 in at the sides.</summary>
    public const float TopDip = 32;

    public const float CaptionMidDip = 20;
    public const float FootDip = 12;
    public const float CornerDip = 4;

    /// <summary>The thickest bar a sample is drawn at: a thicker layout's bar is drawn this thick, its look the same.</summary>
    public const float BarMostDip = 64;

    /// <summary>The room over and under a menu; over an open folder.</summary>
    public const float AroundDip = 12;

    /// <summary>
    /// The room between the folder open in the whole look's sample and its face's bar, as a folder stands off its bar. §671 (the
    /// person, 2026-11-12: "in the preview there is a little gap on the bottom between the taskbar and preview end. this should
    /// not be"): none under the bar — it stands at the room's foot, as a taskbar at a display's.
    /// </summary>
    public const float OpenGapDip = 8;

    /// <summary>§613: the mode's badge at the room's top right: its side (round), 8 in from the room's edges, its glyph's size.</summary>
    public const float BadgeDip = 28;

    public const float BadgeInDip = 8;
    public const float BadgeGlyphDip = 14;

    /// <summary>
    /// §628: the desktop a layout's own bar is pictured across (<see cref="LookSample.Itself"/>), DIP: a 1080p display's at 150 %
    /// wide, the bar at its foot under a strip of the desktop; drawn scaled to its picture's width, its height with it.
    /// </summary>
    public const float ItselfWideDip = 1280;

    public const float ItselfTallDip = 144;

    /// <summary>
    /// §679: how big a choice's picture is (DIP; the settings panel's tile): a sketch's as what it shows needs — the bar's foot
    /// under a window, a small display, a layout's whole bar, a few app buttons, a flyout over its button —; a piece of a bar
    /// (§675) else.
    /// </summary>
    public static Vector2 PictureDip(LookShown shows) => shows.Sketch switch
    {
        BarSketch.Type => new(152, 56),
        BarSketch.Show => new(152, 80),
        // §682 (the person, 2026-11-12: "positions setting should be all four positions in one row"): four to a line.
        BarSketch.Edge => new(112, 70),
        BarSketch.Buttons or BarSketch.Desktops => new(248, 40),
        // §700 (the person, 2026-11-13: "layouts should show a little preview for all the layouts vertically"): its whole bar,
        // a line each (FullWide).
        BarSketch.Layout => new(288, 30),
        BarSketch.Opens => new(152, 96),
        // §682 (the person, 2026-11-12: "they should be in one row only as well"): Animations' four to a line.
        BarSketch.Motion => new(112, 80),
        _ => new(144, 48),
    };

    /// <summary>
    /// §681 (the person, 2026-11-12: "for the combine buttons previews they should be shown full width", "same for show windows
    /// from"): whether a sketch's tile takes the picker's whole width — a bar's app buttons, as wide as a bar has room for
    /// them —, its height <see cref="PictureDip"/>'s.
    /// </summary>
    public static bool FullWide(LookShown shows) => shows.Sketch is BarSketch.Buttons or BarSketch.Desktops or BarSketch.Layout;

    /// <summary>§685: one lap of Show's "when the pointer comes near" sketch, ms.</summary>
    public const double ShowLapMs = 3200;

    /// <summary>§682: one lap of Open flyouts' sketch, and of Animations', ms.</summary>
    public const double OpensLapMs = 2800;

    public const double MotionLapMs = 2600;

    /// <summary>
    /// §682: how long Animations' sketch takes its flyout to come up and to go at <paramref name="speed"/>, ms: a flyout's own
    /// opening and closing at that speed (Motion), twice as long so the speeds read apart in a picture; none Off.
    /// </summary>
    public static (double Open, double Close) MotionMs(Animation speed) => speed == Animation.Off ? (0, 0)
        : (2 * Motion.NormalMs(MotionKind.Open) * Motion.Factor(speed), 2 * Motion.NormalMs(MotionKind.Close) * Motion.Factor(speed));

    /// <summary>§679: how much of a sketch's height its bar takes: on a strip of desktop (Type), under a window (Show), under a flyout (Open flyouts).</summary>
    public const float TypeBarShare = 0.62f;

    public const float ShowBarShare = 0.4f;

    public const float OpensBarShare = 0.28f;

    /// <summary>§679: how thick a small display's bar is (Position), of the display's shorter side.</summary>
    public const float EdgeBarShare = 0.22f;

    /// <summary>
    /// §679: the open windows a sketch's app buttons stand for (Combine buttons, Show windows from), in the order they were
    /// first seen: Explorer's two, Notepad's, Task Manager's; the second two on another desktop.
    /// </summary>
    public static readonly (string App, string Title, bool Elsewhere)[] WindowStandIns =
    [
        ("explorer.exe", "Documents", false), // not-ui: a window's title, a sample
        ("System32\\notepad.exe", "notes.txt", false), // not-ui: a window's title, a sample
        ("explorer.exe", "Downloads", true), // not-ui: a window's title, a sample
        ("System32\\Taskmgr.exe", "Task Manager", true), // not-ui: a window's title, a sample
    ];

    /// <summary>
    /// §679: the app buttons a sketch of <paramref name="shows"/> draws on one bar: Show windows from, the windows of this
    /// desktop alone or of every desktop; Combine buttons, the first three (Explorer's two among them). Combined always, an app
    /// each; else a window each saying its title — a bar this short never full (<see cref="FullOf"/> draws one that is).
    /// </summary>
    public static List<(string App, int Count, string? Label)> AppsOf(LookShown shows) =>
        Buttons(shows.Sketch == BarSketch.Desktops
            ? WindowStandIns.Where(window => shows.Layout.AllDesktops || !window.Elsewhere)
            : WindowStandIns.Take(3), shows.Layout.Combine == ButtonCombine.Always);

    /// <summary>
    /// §679: Combine buttons' "When taskbar is full" in two halves: a few windows each saying its title while there is room,
    /// then a full bar's every window, an app each.
    /// </summary>
    public static (List<(string App, int Count, string? Label)> Room, List<(string App, int Count, string? Label)> Full) FullOf() =>
        (Buttons(WindowStandIns.Take(2), combined: false), Buttons(WindowStandIns, combined: true));

    /// <summary>
    /// §679: the buttons of <paramref name="windows"/> — what each opens (under Windows' folder), how many windows it stands
    /// for, what it says beside its icon — as the bar makes them (AppButtons): <paramref name="combined"/>, an app each with
    /// its icon alone, its windows counted; else a window each saying its title.
    /// </summary>
    public static List<(string App, int Count, string? Label)> Buttons(IEnumerable<(string App, string Title, bool Elsewhere)> windows, bool combined) =>
        combined
            ? [.. windows.GroupBy(window => window.App).Select(app => (app.Key, app.Count(), (string?)null))]
            : [.. windows.Select(window => (window.App, 1, (string?)window.Title))];

    /// <summary>§678: the mark a bar hidden till pointed at leaves on a screen's picture: a short line along the middle of <paramref name="edge"/>.</summary>
    public static RectF HandleOf(RectF desktop, Edge edge, float thick)
    {
        var vertical = edge is Edge.Left or Edge.Right;
        var length = MathF.Round((vertical ? desktop.Height : desktop.Width) * 0.18f);
        return edge switch
        {
            Edge.Top => new RectF(desktop.X + MathF.Round((desktop.Width - length) / 2), desktop.Y + thick, length, thick),
            Edge.Left => new RectF(desktop.X + thick, desktop.Y + MathF.Round((desktop.Height - length) / 2), thick, length),
            Edge.Right => new RectF(desktop.Right - (2 * thick), desktop.Y + MathF.Round((desktop.Height - length) / 2), thick, length),
            _ => new RectF(desktop.X + MathF.Round((desktop.Width - length) / 2), desktop.Bottom - (2 * thick), length, thick),
        };
    }

    /// <summary>
    /// The sample of <paramref name="shows"/>: §592 the Appearance's the whole look; §551 the taskbar menu's the menu itself; §628
    /// a layout's own bar itself.
    /// </summary>
    public static LookSample SampleOf(LookShown shows) =>
        shows.Itself ? LookSample.Itself : shows.Menu is not null ? LookSample.Menu : LookSample.Whole;

    /// <summary>How thick a sample draws <paramref name="layout"/>'s bar (DIP): its own size, at most <see cref="BarMostDip"/>.</summary>
    public static int BarDip(TaskbarLayout layout) => Math.Clamp(layout.Size, Config.MinHeight, (int)BarMostDip);

    /// <summary>
    /// How tall <paramref name="shows"/>' sample's room is (DIP): what it draws, whole, and the room round it. §571 a folder's
    /// lines as tall as the open folder lays them out at <paramref name="scale"/> (<paramref name="measure"/>: its plan, which
    /// the widgets and a device measure), not a guess: 12 over them, the gap down to its face's bar, the bar, the gap under it;
    /// a menu and 12 over and under it. §671 nothing under the bar.
    /// </summary>
    public static float BoxDip(LookShown shows, IPanelText measure, float scale) => SampleOf(shows) switch
    {
        LookSample.Menu => MenuSample.Plan(MenuOf(shows)).Height + (2 * AroundDip),
        _ => AroundDip + measure.LinesDip(shows, scale) + OpenGapDip + BarDip(shows.Layout),
    };

    /// <summary>
    /// §551: the rows of <paramref name="shows"/>' menu as the bar's menu is made of them now (Dock.BarRows, ShowBarMenu): its
    /// entries, the layouts under no heading (the one its layout is ticked), Lock the taskbar ticked where the bars are
    /// locked. A shortcut by its own name and glyph: the shell is not asked (nothing of this PC's, as every sample).
    /// </summary>
    public static List<FlyoutItem> MenuOf(LookShown shows) =>
        Dock.BarRows(shows.Menu ?? [], Dock.LayoutRows(shows.Config.Layouts, shows.Layout.Id), File.Exists(QuickLinks.WindowsTerminal), ShortcutLooks.None, shows.Config.LockTaskbar).Items;

    /// <summary>How tall <paramref name="shows"/>' preview's line is (DIP): its Caption, its sample's room, the room under it.</summary>
    public static float TallDip(LookShown shows, IPanelText measure, float scale) => TopDip + BoxDip(shows, measure, scale) + FootDip;

    /// <summary>What a screen reader calls the preview: §592 "Preview of Appearance"; §551 "Preview of Taskbar menu".</summary>
    public static string NameOf(LookShown shows) =>
        S.DockEditPreviewOf(shows.Menu is not null ? S.SettingsBarMenuHeader : S.SettingsAppearanceHeader);

    /// <summary>
    /// §613: the room's ground under a sample, and where no desktop picture can be read the gradient it stands on instead, top
    /// to foot: a quiet blue-grey of the mode <paramref name="dark"/>, as Windows' own default desktop's tones.
    /// </summary>
    public static Color Ground(bool dark) => dark ? Color.FromArgb(255, 0x1F, 0x2A, 0x38) : Color.FromArgb(255, 0xC9, 0xD6, 0xE4);

    public static Color GroundFoot(bool dark) => dark ? Color.FromArgb(255, 0x10, 0x16, 0x1E) : Color.FromArgb(255, 0x9F, 0xB4, 0xCB);

    /// <summary>§613: the badge's ground behind its sun or moon, a flyout's of its mode, a little see-through.</summary>
    public static Color BadgeGround(bool dark) => dark ? Color.FromArgb(0xE0, 0x2C, 0x2C, 0x2C) : Color.FromArgb(0xE6, 0xF9, 0xF9, 0xF9);

    /// <summary>§613: where the mode's badge stands in a room of <paramref name="size"/> px at <paramref name="scale"/>: its top right, 8 in.</summary>
    public static RectF BadgeOf(Vector2 size, float scale)
    {
        var side = MathF.Round(BadgeDip * scale);
        var inset = MathF.Round(BadgeInDip * scale);
        return new RectF(size.X - inset - side, inset, side, side);
    }

    /// <summary>
    /// §634: the backdrop the whole look's sample lays over its desktop, under its bar and its open folder: the one the dock lays
    /// over every display while a folder stands open at rest (§623, <see cref="FolderBackdrop.Of"/>) in <paramref name="look"/>
    /// (the Appearance as the bars wear it — a choice tried on too, §622); null for none (unset, None, high contrast).
    /// </summary>
    public static Argb? BackdropOf(Look look) => FolderBackdrop.Of(folderOpen: true, editing: false, look.HighContrast, look.Worn.Flyouts);

    /// <summary>
    /// §649: the ground the whole look's sample's folder stands attached on: the Flyouts' attached folders' colour in
    /// <paramref name="look"/> where one is said (a choice tried on too, §622) — the one place the page shows it —; null for
    /// none (unset: the folder stands as cards, as every folder does by default; high contrast; §656 the sample bar
    /// <paramref name="layout"/>'s a dock, beside which no folder stands attached).
    /// </summary>
    public static Argb? GroundOf(Look look, TaskbarLayout layout) =>
        look.HighContrast || !FolderStyles.Offered(layout) ? null : look.Worn.Flyouts.Ground.Color;

    // --- What stands in --------------------------------------------------------------------------------

    /// <summary>A folder on the bar wearing the clock.</summary>
    public static BarItem ClockFolder()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId)];
        folder.Folder.Face = folder.Folder.Faces[0];
        return folder;
    }

    /// <summary>
    /// The sample's bar (§613: "it should also show more widgets on the taskbar"): Start, search, Task view, Settings and the
    /// Snipping Tool in its middle, as Windows 11's centred taskbar; at its end a folder wearing the clock (the one opened), where
    /// Windows' clock stands. Nothing of this PC's: the buttons Karpo draws itself, the clock standing still (LookStand).
    /// </summary>
    public static List<BarItem> BarStandIns()
    {
        List<BarItem> items = [BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New(), BarKind.WindowsSettings.New(), BarKind.SnippingTool.New()];
        foreach (var item in items)
        {
            item.Section = BarSection.Middle;
        }
        var folder = ClockFolder();
        folder.Section = BarSection.End;
        return [.. items, folder];
    }

    /// <summary>
    /// §679: a short bar's stand-ins (a small display's, Position; the one a flyout opens over, Open flyouts): Start, search,
    /// a folder wearing the clock in their middle — the flyout's —, Task view and Settings.
    /// </summary>
    public static List<BarItem> EdgeStandIns()
    {
        List<BarItem> items = [BarKind.Start.New(), BarKind.Search.New(), ClockFolder(), BarKind.TaskView.New(), BarKind.WindowsSettings.New()];
        foreach (var item in items)
        {
            item.Section = BarSection.Middle;
        }
        return items;
    }

    /// <summary>
    /// The open folder's lines (§613: "it should show more widgets in the folder"): a compact clock (its time, in the accent's
    /// company), a compact world clock (Tokyo's time of the same still moment) and a calendar (its events' accent). No month:
    /// its grid would make the preview twice as tall, the tabs under it out of sight.
    /// </summary>
    public static List<CellItem> LineStandIns()
    {
        var clock = ListKind.Clock.New();
        clock.Size = LineSize.Compact;
        var world = ListKind.Clock.New();
        world.Size = LineSize.Compact;
        world.Clock = new ClockOptions { TimeZone = "Tokyo Standard Time", Label = "Tokyo" }; // not-ui: a city's name
        return [clock, world, ListKind.Calendar.New()];
    }

    /// <summary>The folder the whole look's sample opens: a clock folder holding the lines (<see cref="LineStandIns"/>).</summary>
    public static BarItem PanelOf()
    {
        var holder = ClockFolder();
        holder.Folder!.Lists = [[]];
        foreach (var line in LineStandIns())
        {
            _ = Folders.Add(BarKind.FolderList, holder.Folder, line);
        }
        return holder;
    }

    /// <summary>
    /// What <paramref name="shows"/>' sample is drawn from, as words: its sample, the Appearance, its bar's kind and size,
    /// what Windows looks like (<paramref name="windows"/>), the scale and the room's size, the day the clock stands still on,
    /// §613 the desktop's picture under it (<paramref name="wallpaper"/>: its file and when it was written). The same words,
    /// the same picture; another value anywhere, another picture.
    /// </summary>
    public static string KeyOf(LookShown shows, Look windows, float scale, Vector2 size, WallpaperSource? wallpaper = null) =>
        string.Join('|', SampleOf(shows), shows.Values(), windows, scale, size, LookStand.Day, wallpaper);
}

/// <summary>
/// §536: what a look's sample reads in place of this PC's and the network's while it is made and drawn, on this thread: the
/// clock standing still at 9:41 today, a calendar of two made-up events (no account is asked), and §635 every other widget's
/// data the samples' (Samples: a layout's tile, §628, draws the layout's own widgets — its weather Zürich's, its news the
/// samples' headlines).
/// </summary>
internal sealed class LookStand : IDisposable
{
    private readonly Clock.StillClock clock;
    private readonly CalendarStore? calendar;
    private readonly Samples.Scope samples;

    public LookStand()
    {
        clock = Clock.Still(At);
        calendar = CalendarIsland.Made;
        CalendarIsland.Made = Samples.PreviewCalendar();
        samples = Samples.Show();
    }

    /// <summary>The day the samples' clock stands on: today.</summary>
    public static DateOnly Day => DateOnly.FromDateTime(Clock.Real().Date);

    /// <summary>The moment the samples' clock stands at: 9:41 today.</summary>
    public static DateTime At => Clock.Real().Date.AddHours(9).AddMinutes(41);

    public void Dispose()
    {
        samples.Dispose();
        CalendarIsland.Made = calendar;
        clock.Dispose();
    }
}

/// <summary>
/// §536: the settings panel's look previews, each a sample drawn once a look in visuals of its own over the panel's surface
/// where its room stands (see the top of the file): one layer over the panel's surface, cut to its page, a sample in it a
/// preview shown on the page.
/// </summary>
internal sealed unsafe class LookPreviews(Gfx gfx) : IDisposable
{
    /// <summary>One sample shown: what it was drawn from, its visual, and what it is made of (closed with it).</summary>
    private sealed class Shown : IDisposable
    {
        public required string Key;
        public required ContainerVisual Visual;

        /// <summary>§571: where the folder's lines stand in the room (px), §592 the whole look's sample's; null for none (a menu).</summary>
        public RectF? Lines;

        /// <summary>§613: the mode it is drawn in, the taskbar's: dark or not.</summary>
        public required bool Dark;

        /// <summary>§634: the folder's backdrop laid over its desktop (§623), null for none.</summary>
        public Argb? Backdrop;

        /// <summary>§649: where the whole look's folder's ground stands (room px), attached; empty for none.</summary>
        public RectF Ground;

        /// <summary>§613: the desktop's picture it stands on (sharp), and under its bar (blurred); null for the gradient.</summary>
        public CompositionDrawingSurface? Wallpaper;

        public CompositionDrawingSurface? Blurred;

        public readonly List<IDisposable> Owned = [];
        public readonly List<IIsland> Islands = [];
        public readonly List<Scene> Scenes = [];

        /// <summary>§685: its rounded room's shape, the clip it is cut to (shortened to a scrolling picker's band).</summary>
        public CompositionRoundedRectangleGeometry? Shape;

        /// <summary>§628: its pins' icons, let go with it.</summary>
        public readonly Icons Icons = new();

        public void Dispose()
        {
            Quietly(Icons.Forget);
            foreach (var scene in Scenes)
            {
                Quietly(scene.Dispose);
            }
            foreach (var island in Islands)
            {
                if (island is FolderIsland folder)
                {
                    Close(folder);
                }
                else
                {
                    Quietly(island.Dispose);
                }
            }
            Quietly(() => Visual.Parent?.Children.Remove(Visual));
            for (var at = Owned.Count - 1; at >= 0; at--)
            {
                Quietly(Owned[at].Dispose);
            }
            Quietly(() => Wallpaper?.Dispose());
            Quietly(() => Blurred?.Dispose());
        }
    }

    /// <summary>
    /// §613: the desktop's pictures last composed for a sample, by what they are made of (the room's whole picture, and the
    /// strip under its bar with the margin the blur reads): decoded once, so a value changed draws the sample again without
    /// reading the file again. One room at a time.
    /// </summary>
    private readonly Dictionary<WallpaperKey, Pixels?> composed = [];

    private readonly Dictionary<string, Shown> shown = new(StringComparer.Ordinal);

    /// <summary>Each look's key as it was worked out once (<see cref="LookPreview.KeyOf"/>), by the very rows' look.</summary>
    private readonly ConditionalWeakTable<LookShown, string> keys = [];

    private ContainerVisual? layer;
    private InsetClip? clip;
    private Visual? over;

    /// <summary>Tests: how many samples were drawn, all told: a scroll or a hover draws none.</summary>
    internal int Drawn { get; private set; }

    /// <summary>Tests: the samples shown, by their preview's key on the page, and what each was drawn from.</summary>
    internal IReadOnlyDictionary<string, string> Keys => shown.ToDictionary(each => each.Key, each => each.Value.Key, StringComparer.Ordinal);

    /// <summary>Tests (§571): each sample's room (px) and where a folder's lines stand in it, by its preview's key on the page.</summary>
    internal IReadOnlyDictionary<string, (Vector2 Room, RectF? Lines)> Rooms =>
        shown.ToDictionary(each => each.Key, each => (each.Value.Visual.Size, each.Value.Lines), StringComparer.Ordinal);

    /// <summary>Tests (§613): the mode each sample is drawn in (dark or not), by its preview's key on the page.</summary>
    internal IReadOnlyDictionary<string, bool> Modes => shown.ToDictionary(each => each.Key, each => each.Value.Dark, StringComparer.Ordinal);

    /// <summary>Tests (§634): the folder's backdrop each sample lays over its desktop (null for none), by its preview's key on the page.</summary>
    internal IReadOnlyDictionary<string, Argb?> Backdrops => shown.ToDictionary(each => each.Key, each => each.Value.Backdrop, StringComparer.Ordinal);

    /// <summary>Tests (§649): where each sample's folder's ground stands (room px; empty for cards), by its preview's key on the page.</summary>
    internal IReadOnlyDictionary<string, RectF> Grounds => shown.ToDictionary(each => each.Key, each => each.Value.Ground, StringComparer.Ordinal);

    /// <summary>Tests (§613): whether each sample stands on the desktop's picture (not the gradient), by its preview's key on the page.</summary>
    internal IReadOnlyDictionary<string, bool> OnWallpaper => shown.ToDictionary(each => each.Key, each => each.Value.Wallpaper is not null, StringComparer.Ordinal);

    /// <summary>§571: each look's lines as measured (<see cref="LinesDip"/>): what they were measured from, and how tall they stand (DIP).</summary>
    private static readonly ConditionalWeakTable<LookShown, Tuple<string, float>> measured = [];

    /// <summary>
    /// §571: how tall <paramref name="shows"/>' folder's lines stand (DIP) as the open folder lays them out (<see cref="Laid"/>,
    /// the very plan the whole look's sample draws) over Windows' look <paramref name="windows"/> at <paramref name="scale"/>:
    /// what the sample's room holds (LookPreview.BoxDip). Measured once a look (the rows are made again at every change, so a
    /// new LookShown is a new look) and again only for another Windows' look or scale: a scroll measures nothing. 0 where
    /// nothing came of it, as the sample is then the checker alone.
    /// </summary>
    internal static float LinesDip(Gfx gfx, LookShown shows, Look windows, float scale)
    {
        var key = LookPreview.KeyOf(shows, windows, scale, Vector2.Zero);
        if (measured.TryGetValue(shows, out var was) && was.Item1 == key)
        {
            return was.Item2;
        }
        var dip = 0f;
        try
        {
            using var stand = new LookStand();
            var (folder, _, plan) = Laid(gfx, shows, Worn(shows, windows), scale);
            Close(folder);
            dip = MathF.Ceiling(plan.Height / scale);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a look's preview not measured: {error.Message}");
        }
        measured.AddOrUpdate(shows, Tuple.Create(key, dip));
        return dip;
    }

    /// <summary>The look a sample of <paramref name="shows"/> is drawn in (§567): the Appearance as the bars wear it, Karpo's own under it; still, as a picture is.</summary>
    private static Look Worn(LookShown shows, Look windows) => windows.Wearing(shows.Config) with { Animations = false };

    /// <summary>
    /// The samples of <paramref name="layout"/>'s previews in <paramref name="into"/> over <paramref name="above"/> (the panel's
    /// surface), each where its room is as the page is scrolled, cut to the page, drawn again only where its look changed; one
    /// gone from the page let go. Windows' look <paramref name="windows"/> under the Appearance; §613 over the desktop's
    /// picture <paramref name="wallpaper"/> (null: none known, the gradient).
    /// </summary>
    public void Place(ContainerVisual into, Visual above, PanelLayout layout, Look windows, WallpaperSource? wallpaper = null)
    {
        var c = gfx.Compositor;
        if (layer is null)
        {
            layer = c.CreateContainerVisual();
            clip = c.CreateInsetClip();
            layer.Clip = clip;
        }
        if (!ReferenceEquals(layer.Parent, into) || !ReferenceEquals(over, above))
        {
            Quietly(() => layer.Parent?.Children.Remove(layer));
            into.Children.InsertAbove(layer, above);
            over = above;
        }
        layer.IsVisible = layout.IsOpen;
        HashSet<string> seen = new(StringComparer.Ordinal);
        if (layout.IsOpen)
        {
            var (cut, insets) = Cut(layout);
            layer.Size = cut;
            (clip!.LeftInset, clip.TopInset, clip.RightInset, clip.BottomInset) = insets;
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
            {
                // §628: a tile's picture of what it makes is a sample as a preview's is.
                if ((line.Row.Shows ?? line.Row.Picture) is not { } shows)
                {
                    continue;
                }
                Show(line.Row.Key, shows, layout.OnPanel(layout.PreviewBox(line)));
            }
            // §675: an inline picker's choices pictured, each the sample of what it draws over its tile.
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Kind == PanelRowKind.PickerBody))
            {
                // §682: a picker that scrolls its samples cut to its band.
                var band = layout.PickerOverflow(line) > 0 ? layout.OnPanel(layout.PickerBand(line)) : (RectF?)null;
                foreach (var cell in layout.PickerCells(line).Where(cell => cell is { Kind: PickerCellKind.Tile, Item.Shows: not null }))
                {
                    Show(line.Row.Key + "#" + cell.Item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), cell.Item.Shows!, layout.OnPanel(layout.PictureIn(cell)), band);
                }
            }
        }
        // One sample in its room (panel px), drawn again only for another look or size; §682 cut to a scrolling picker's band.
        void Show(string at, LookShown shows, RectF box, RectF? band = null)
        {
            var size = new Vector2(MathF.Round(box.Width), MathF.Round(box.Height));
            if (size.X < 1 || size.Y < 1)
            {
                return;
            }
            var key = keys.GetValue(shows, each => LookPreview.KeyOf(each, windows, layout.Scale, size, wallpaper));
            if (!shown.TryGetValue(at, out var sample) || sample.Key != key)
            {
                sample?.Dispose();
                sample = Make(shows, windows, layout.Scale, size, key, wallpaper);
                shown[at] = sample;
                layer!.Children.InsertAtTop(sample.Visual);
            }
            sample.Visual.Offset = new Vector3(MathF.Round(box.X), MathF.Round(box.Y), 0);
            sample.Visual.IsVisible = box.Bottom > layout.Viewport.Y && box.Y < layout.Viewport.Bottom
                && (band is not { } inBand || (box.Bottom > inBand.Y && box.Y < inBand.Bottom));
            // §682: its rounded room cut to a scrolling picker's band (its clip's shape moved and shortened); §685 never taken
            // off — every sample stays inside its room (a hidden bar's slide, a window's foot).
            if (sample.Shape is { } room)
            {
                var top = band is { } cut ? MathF.Max(0, MathF.Round(cut.Y - box.Y)) : 0;
                var bottom = band is { } under ? MathF.Max(0, MathF.Round(box.Y + size.Y - under.Bottom)) : 0;
                room.Offset = new Vector2(0, top);
                room.Size = new Vector2(size.X, MathF.Max(0, size.Y - top - bottom));
            }
            seen.Add(at);
        }
        foreach (var gone in shown.Keys.Where(key => !seen.Contains(key)).ToList())
        {
            shown[gone].Dispose();
            shown.Remove(gone);
        }
    }

    /// <summary>
    /// The layer's size and its clip's insets (left, top, right, bottom; px) for <paramref name="layout"/>: an inset clip is inset
    /// from its visual's size, so the layer is the flyout's (§576: §575 left it the bar's, 48 DIP tall, and every sample below
    /// the flyout's first 48 DIP was cut away — the Taskbar page's menu a blank box), cut to where the page shows. Pure.
    /// </summary>
    internal static (Vector2 Size, (float Left, float Top, float Right, float Bottom) Insets) Cut(PanelLayout layout)
    {
        var (size, view) = (layout.FlyoutSize, layout.Viewport);
        return (size, (view.X, view.Y, MathF.Max(0, size.X - view.Right), MathF.Max(0, size.Y - view.Bottom)));
    }

    public void Dispose()
    {
        foreach (var sample in shown.Values)
        {
            sample.Dispose();
        }
        shown.Clear();
        if (layer is not null)
        {
            Quietly(() => layer.Parent?.Children.Remove(layer));
            Quietly(layer.Dispose);
            layer = null;
        }
        if (clip is not null)
        {
            Quietly(clip.Dispose);
            clip = null;
        }
    }

    // --- A sample ----------------------------------------------------------------------------------------

    /// <summary>
    /// §613: <paramref name="shows"/>' sample, whole, in the mode the taskbar is in (the Appearance's Mode over Windows' look
    /// <paramref name="windows"/>), over the desktop's picture <paramref name="wallpaper"/>, that mode's badge at its top right.
    /// </summary>
    private Shown Make(LookShown shows, Look windows, float scale, Vector2 size, string key, WallpaperSource? wallpaper)
    {
        Drawn++;
        var c = gfx.Compositor;
        var visual = c.CreateContainerVisual();
        visual.Size = size;
        var look = Worn(shows, windows);
        var sample = new Shown { Key = key, Visual = visual, Dark = look.Dark };
        // Its room's corners, as the panel's surface draws the room under it.
        var shape = c.CreateRoundedRectangleGeometry();
        shape.Size = size;
        shape.CornerRadius = new Vector2(Px(LookPreview.CornerDip, scale));
        sample.Shape = shape;
        var cut = c.CreateGeometricClip(shape);
        visual.Clip = cut;
        sample.Owned.Add(shape);
        sample.Owned.Add(cut);
        using var stand = new LookStand();
        Draw(sample, visual, shows, look, scale, size, wallpaper);
        return sample;
    }

    /// <summary>
    /// <paramref name="shows"/>' sample into <paramref name="into"/>, a room of <paramref name="size"/> px, in <paramref name="look"/>
    /// (the Appearance as the bars wear it, §613 in the taskbar's mode): the desktop it stands on, then what it shows, then the
    /// mode's badge.
    /// </summary>
    private void Draw(Shown sample, ContainerVisual into, LookShown shows, Look look, float scale, Vector2 size, WallpaperSource? wallpaper)
    {
        float P(float dip) => Px(dip, scale);
        var room = new RectI(0, 0, (int)size.X, (int)size.Y);
        // §613: the desktop's picture filling the room, sharp; where none can be read, the mode's quiet gradient.
        if (wallpaper is { } source && Picture(source, room, room, 0) is { } whole)
        {
            sample.Wallpaper = WallpaperStrips.Blurred(gfx, whole, 0, 0, null);
            Sprite(sample, into, Vector2.Zero, size, sample.Wallpaper);
        }
        else
        {
            Sprite(sample, into, Vector2.Zero, size, dc => Gradient(dc, size, look.Dark));
        }
        try
        {
            switch (LookPreview.SampleOf(shows))
            {
                // §551: the taskbar's menu as the bar draws it, in the middle of the room.
                case LookSample.Menu:
                    Menu(sample, into, shows, look, scale, size);
                    break;
                // §628: the layout's own bar at the room's foot, laid out across a desktop as wide as a 1080p display at 150 % and
                // drawn as small as the room is wide (its widgets' own drawing at that scale, crisp, not a picture shrunk).
                // §675: zoomed (a choice's picture) as tall as the room, its widgets in its middle.
                // §679: a piece of the bar close up, where a choice of how it stands or behaves shows.
                case LookSample.Itself when shows.Sketch != BarSketch.None:
                    Sketch(sample, into, shows, look, size, wallpaper, room);
                    break;
                case LookSample.Itself:
                    Bar(sample, into, shows, shows.Layout.Islands, look, shows.Zoomed ? size.Y / LookPreview.BarDip(shows.Layout) : size.X / LookPreview.ItselfWideDip, size, wallpaper, room);
                    break;
                default:
                {
                    // §634: the folder open, its backdrop (§623, the Flyouts' "Background while a folder is open") laid over the
                    // desktop, under the bar and the folder — as it stands over every display while a folder is open. §623 laid it
                    // live alone; the preview drew none, so the colour picked showed nowhere on the page.
                    if (LookPreview.BackdropOf(look) is { } backdrop)
                    {
                        sample.Backdrop = backdrop;
                        var ink = Color.FromArgb(backdrop.A, backdrop.R, backdrop.G, backdrop.B);
                        Sprite(sample, into, Vector2.Zero, size, dc => PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, size.X, size.Y), 0, PopupPaint.Ink(ink)));
                    }
                    // §592: the bar and its widgets at the room's foot; §613 the folder at its end open in the room's middle,
                    // whole (§571), standing off the bar as a folder does.
                    var gap = P(LookPreview.OpenGapDip);
                    var bar = Bar(sample, into, shows, LookPreview.BarStandIns(), look, scale, size, wallpaper, room)?.Y;
                    var (surface, plan) = Lines(sample, shows, look, scale);
                    if (bar is { } top && plan is { } laid && surface is not null)
                    {
                        var x = MathF.Max(P(8), MathF.Round((size.X - laid.Width) / 2));
                        var at = new Vector2(x, top - gap - laid.Height);
                        // §649, §651: attached, on its ground: the bar grown out, across the whole room, from the bar's top to its
                        // lines' — flush on the bar —, its far side's line the outline, its columns parted by seams.
                        if (LookPreview.GroundOf(look, shows.Layout) is { } ground)
                        {
                            at = at with { Y = top - laid.Height };
                            var ink = Color.FromArgb(ground.A, ground.R, ground.G, ground.B);
                            var area = new Vector2(size.X, laid.Height);
                            var theme = FlyoutTheme.Folder(look.Flyout(ground));
                            var stroke = MathF.Max(1, MathF.Round(theme.StrokeDip * scale));
                            sample.Ground = new RectF(0, at.Y, area.X, area.Y);
                            Sprite(sample, into, new Vector2(0, at.Y), area, dc =>
                            {
                                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, area.X, area.Y), 0, PopupPaint.Ink(ink));
                                foreach (var (left, right) in laid.Seams)
                                {
                                    PopupPaint.Fill(dc, PopupPaint.Rect(at.X + left, 0, right - left, area.Y), 0, PopupPaint.Ink(FolderFlyout.RuleInk(theme)));
                                }
                                if (theme.Stroke.A > 0)
                                {
                                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, area.X, stroke), 0, PopupPaint.Ink(theme.Stroke));
                                }
                            });
                        }
                        Lined(sample, into, at, laid, surface);
                    }
                    break;
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A sample that can't be drawn leaves its room the desktop; the settings stand.
            Console.Error.WriteLine($"Karpo: a look's preview not drawn: {error.Message}");
        }
        // §613 (the person, 2026-11-09: "it should show a light and dark mode icon"): the mode it is drawn in, its sun or moon in
        // a round badge of its mode's flyout at the top right; none in high contrast, which is neither. §628 none on a tile's
        // picture of a layout, too small for it and one of several alike.
        if (!look.HighContrast && !shows.Itself)
        {
            var badge = LookPreview.BadgeOf(size, scale);
            var dark = look.Dark;
            Sprite(sample, into, new Vector2(badge.X, badge.Y), new Vector2(badge.Width, badge.Height), dc =>
            {
                var r = PopupPaint.Rect(0, 0, badge.Width, badge.Height);
                PopupPaint.Fill(dc, r, badge.Width / 2, PopupPaint.Ink(LookPreview.BadgeGround(dark)));
                PopupPaint.Glyph(dc, gfx, dark ? OptionGlyphs.Moon : OptionGlyphs.Sun, P(LookPreview.BadgeGlyphDip),
                    PopupPaint.Ink(dark ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(0xE4, 0, 0, 0)), r);
            });
        }
    }

    /// <summary>
    /// §613: the desktop's picture <paramref name="source"/> filling <paramref name="room"/> (px) as Windows' Fill does, the part
    /// under <paramref name="strip"/> and <paramref name="margin"/> round it (WallpaperStrips.Compose: decoded at the size it is
    /// drawn, only that part read); composed once a room (<see cref="composed"/>). Null where it cannot be read.
    /// </summary>
    private Pixels? Picture(WallpaperSource source, RectI room, RectI strip, int margin)
    {
        var key = new WallpaperKey(source with { Fit = WallpaperFit.Fill }, room, room, strip, margin);
        if (!composed.TryGetValue(key, out var pixels))
        {
            if (composed.Count >= 4)
            {
                composed.Clear();
            }
            try
            {
                pixels = source.Path is null ? null : WallpaperStrips.Compose(key);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a look's preview's desktop not read: {error.Message}");
                pixels = null;
            }
            composed[key] = pixels;
        }
        return pixels;
    }

    /// <summary>§613: the mode's quiet gradient over <paramref name="size"/> px, top to foot, where no desktop's picture can be read.</summary>
    private static void Gradient(ID2D1DeviceContext* dc, Vector2 size, bool dark)
    {
        var (top, foot) = (LookPreview.Ground(dark), LookPreview.GroundFoot(dark));
        const int bands = 48;
        var tall = size.Y / bands;
        for (var at = 0; at < bands; at++)
        {
            var t = at / (float)(bands - 1);
            byte Mix(byte a, byte b) => (byte)MathF.Round(a + ((b - a) * t));
            var ink = Color.FromArgb(255, Mix(top.R, foot.R), Mix(top.G, foot.G), Mix(top.B, foot.B));
            PopupPaint.Fill(dc, PopupPaint.Rect(0, MathF.Floor(at * tall), size.X, MathF.Ceiling(tall) + 1), 0, PopupPaint.Ink(ink));
        }
    }

    private static float Px(float dip, float scale) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The bar of <paramref name="items"/>, <paramref name="shows"/>' layout's kind of bar at its size (at most
    /// <see cref="LookPreview.BarMostDip"/>) along the foot of <paramref name="room"/> (px), laid out and drawn as the layout's
    /// bars are (Layout, Scene, each widget's island), §613 the desktop's picture <paramref name="wallpaper"/> (filling
    /// <paramref name="desktop"/>) blurred under it as the Taskbar's Blur says (Scene.Wallpaper, as a bar of the dock): the
    /// bar's top; none where it has nothing.
    /// </summary>
    private RectF? Bar(Shown sample, ContainerVisual into, LookShown shows, List<BarItem> items, Look look, float scale, Vector2 room, WallpaperSource? wallpaper, RectI desktop,
        Edge at = Edge.Bottom, List<(string App, int Count, string? Label)>? apps = null)
    {
        var dpi = (uint)MathF.Round(scale * 96);
        var edge = new EdgeBar
        {
            Size = LookPreview.BarDip(shows.Layout),
            Layout = shows.Layout.Type == LayoutType.Dock ? EdgeLayout.Dock : EdgeLayout.Taskbar,
            Of = shows.Layout,
        };
        var config = shows.Config.WithEdge(edge);
        var islands = new IslandCollection();
        List<PartSpec> specs = [];
        // §628: a pin its app's own button, its icon the shell's (a layout's own bar: its pins); no open app's.
        List<(Item Item, Mark Mark)> tiles = [];
        List<string?> owners = [];
        foreach (var item in items)
        {
            var key = Config.IslandKey("preview", item.Id);
            specs.Add(PartSpec.For(key, item));
            if (item.Kind == BarKind.Pin && item.Pin?.Target is { Length: > 0 } target && Resolved(target) is { } app)
            {
                tiles.Add((app, default));
                owners.Add(key);
            }
            if (Islands.Create(key, item, gfx, config) is { } island)
            {
                sample.Islands.Add(island);
                _ = island.Refresh(gfx);
                islands[key] = island;
            }
        }
        // §679: a sketch's app buttons, the Apps widget's: their windows the sample's, the first the one in front.
        List<string?> labels = [.. tiles.Select(_ => (string?)null)];
        var windir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        foreach (var (app, count, label) in apps ?? [])
        {
            if (Resolved(Path.Combine(windir, app)) is { } opened)
            {
                tiles.Add((opened, new Mark(count, Active: labels.Count == 0, Attention: false)));
                owners.Add(null);
                labels.Add(label);
            }
        }
        var content = new Content { Islands = islands, Tiles = tiles, TileParts = owners, Labels = labels };
        var area = new RECT { left = 0, top = 0, right = (int)room.X, bottom = (int)room.Y };
        var laid = new Layout(gfx, dpi, area, area, at, config, look, specs, content);
        var holder = gfx.Compositor.CreateContainerVisual();
        holder.Offset = new Vector3(laid.X, laid.Y, 0);
        into.Children.InsertAtTop(holder);
        sample.Owned.Add(holder);
        var scene = new Scene(gfx, holder);
        sample.Scenes.Add(scene);
        scene.Update(laid, content, look, sample.Icons, shown: true);
        var panel = laid.PanelRect;
        if (panel.Width < 1 || panel.Height < 1)
        {
            return null;
        }
        // §613: under its see-through ground the desktop blurred, the strip under the bar as the dock blurs its own (§547).
        var under = new RectI((int)MathF.Round(laid.X + panel.X), (int)MathF.Round(laid.Y + panel.Y), (int)MathF.Round(laid.X + panel.Right), (int)MathF.Round(laid.Y + panel.Bottom));
        var margin = Wallpapers.Margin(scale);
        if (wallpaper is { } source && Picture(source, desktop, under, margin) is { } strip)
        {
            sample.Blurred = WallpaperStrips.Blurred(gfx, strip, margin, KarpoLook.Sigma(look.Dressing.Ground.Blur ?? KarpoLook.Blur, scale), null);
            scene.Wallpaper(sample.Blurred);
            // §704: and how light it is, which Liquid glass's tint follows, as on the dock's bars.
            scene.Backdrop(LiquidGlass.Behind(strip.Data, strip.Width, strip.Height, margin));
        }
        return new RectF(laid.X + panel.X, laid.Y + panel.Y, panel.Width, panel.Height);
    }

    /// <summary>
    /// §679: <paramref name="shows"/>' sketch in <paramref name="size"/> px (<see cref="BarSketch"/>), its bar's widgets Karpo's
    /// own drawing of them: a layout's whole bar at the foot as a layout's tile has it (§628), nothing where its display shows
    /// none; a small display, its bar along its edge; the app buttons as tall as the room; the bar's foot under a window; a
    /// flyout open over a button.
    /// </summary>
    private void Sketch(Shown sample, ContainerVisual into, LookShown shows, Look look, Vector2 size, WallpaperSource? wallpaper, RectI room)
    {
        var layout = shows.Layout;
        float bar = LookPreview.BarDip(layout);
        var hidden = shows.Display is { } display && !shows.Config.ScreenFor(display).Enabled;
        switch (shows.Sketch)
        {
            // A short bar on a strip of desktop: a taskbar's strip edge to edge, a dock's islands off the edge.
            case BarSketch.Type:
                Bar(sample, into, shows, layout.Islands, look, MathF.Round(size.Y * LookPreview.TypeBarShare) / bar, size, wallpaper, room);
                break;
            // When the taskbar is full: its titles while there is room, a line, then a full bar's an app each.
            case BarSketch.Buttons when layout.Combine == ButtonCombine.WhenFull:
                var (roomy, full) = LookPreview.FullOf();
                var half = MathF.Round(size.X * 0.5f);
                Bar(sample, into, shows, layout.Islands, look, size.Y / bar, new Vector2(half, size.Y), wallpaper, room, apps: roomy);
                var right = Layer(sample, into);
                right.Offset = new Vector3(half, 0, 0);
                Bar(sample, right, shows, layout.Islands, look, size.Y / bar, new Vector2(size.X - half, size.Y), wallpaper, room, apps: full);
                var rule = look.Dark ? Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x60, 0, 0, 0);
                var inset = MathF.Round(size.Y * 0.2f);
                Sprite(sample, into, new Vector2(half - 1, inset), new Vector2(2, size.Y - (2 * inset)),
                    dc => PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, 2, size.Y - (2 * inset)), 1, PopupPaint.Ink(rule)));
                break;
            // §700: a layout's whole bar as tall as the tile, its widgets Karpo's own drawing of them.
            case BarSketch.Layout when !hidden:
                Bar(sample, into, shows, layout.Islands, look, size.Y / bar, size, wallpaper, room);
                break;
            case BarSketch.Edge when !hidden:
                var edge = shows.Display is { } at ? shows.Config.EdgeOfScreen(at) : Edge.Bottom;
                var thick = MathF.Round(MathF.Min(size.X, size.Y) * LookPreview.EdgeBarShare);
                Bar(sample, into, shows, layout.Islands, look, thick / bar, size, wallpaper, room, edge);
                break;
            case BarSketch.Buttons or BarSketch.Desktops:
                Bar(sample, into, shows, layout.Islands, look, size.Y / bar, size, wallpaper, room, apps: LookPreview.AppsOf(shows));
                break;
            case BarSketch.Show:
                Show(sample, into, shows, look, size, wallpaper, room);
                break;
            case BarSketch.Opens or BarSketch.Motion:
                Opens(sample, into, shows, look, size, wallpaper, room);
                break;
        }
    }

    /// <summary>
    /// §679: the bar's foot of a display and a window over it, as <paramref name="shows"/>' Show says: keeping its room, the
    /// window's foot stopping at the bar; over the windows, the window running on under it; hidden till pointed at, no bar,
    /// only its mark at the edge, the window down to it. §685 (the person, 2026-11-12: "Bar > show has a space underneath the
    /// taskbar to the edge of the preview. it should also be animated (the pointer coming close to open the taskbar)"): the
    /// window ends at the picture's foot (it ran 8 DIP past it, its rim showing under the bar); hidden till pointed at, the
    /// pointer comes down to the edge, the bar slides up out of it, the pointer goes, the bar slides away — over and over.
    /// </summary>
    private void Show(Shown sample, ContainerVisual into, LookShown shows, Look look, Vector2 size, WallpaperSource? wallpaper, RectI room)
    {
        var layout = shows.Layout;
        var scale = MathF.Round(size.Y * LookPreview.ShowBarShare) / LookPreview.BarDip(layout);
        float P(float dip) => Px(dip, scale);
        // The window's layer first: under the bar.
        var windows = Layer(sample, into);
        var hides = layout.Show == EdgeMode.Hover;
        var slide = hides ? Layer(sample, into) : into;
        var panel = Bar(sample, slide, shows, layout.Islands, look, scale, size, wallpaper, room);
        var (side, top) = (MathF.Round(size.X * 0.1f), MathF.Round(size.Y * 0.1f));
        var foot = layout.Show == EdgeMode.Reserve && panel is { } stands
            ? stands.Y - (layout.Type == LayoutType.Dock ? P(8) : 0)
            : size.Y;
        Window(sample, windows, new RectF(side, top, size.X - (2 * side), foot - top), look, scale);
        if (!hides || panel is not { } bar)
        {
            return;
        }
        var mark = Mark(sample, into, LookPreview.HandleOf(new RectF(0, 0, size.X, size.Y), Edge.Bottom, MathF.Max(2, P(4))), look);
        var tall = P(22);
        var down = new Vector2(MathF.Round(size.X * 0.55f), size.Y - MathF.Round(tall * 0.35f));
        var up = new Vector2(down.X + P(24), MathF.Round(size.Y * 0.32f));
        var pointer = Sprite(sample, into, down, new Vector2(tall, tall), dc => Pointer(dc, tall));
        // Hidden: below the picture's foot, its mark at the edge.
        var away = size.Y - bar.Y;
        Loop(sample, slide, "Offset.Y", LookPreview.ShowLapMs, (0, away), (0.3f, away), (0.38f, 0), (0.74f, 0), (0.84f, away), (1, away));
        Loop(sample, mark, "Opacity", LookPreview.ShowLapMs, (0, 1), (0.3f, 1), (0.32f, 0), (0.82f, 0), (0.86f, 1), (1, 1));
        Loop(sample, pointer, "Offset.X", LookPreview.ShowLapMs, (0, up.X), (0.06f, up.X), (0.28f, down.X), (0.66f, down.X), (0.78f, up.X), (1, up.X));
        Loop(sample, pointer, "Offset.Y", LookPreview.ShowLapMs, (0, up.Y), (0.06f, up.Y), (0.28f, down.Y), (0.66f, down.Y), (0.78f, up.Y), (1, up.Y));
    }

    /// <summary>
    /// §679: a flyout open over the folder in the middle of a short bar of stand-ins, the pointer on the folder: clicked (a
    /// ripple round its tip) where flyouts open on a click, resting where they open on hover. §682 (the person, 2026-11-12:
    /// "animations should get previews as well. you can also animate them slightly if it makes sense, same for the open on
    /// click"): played over and over — Open flyouts the pointer coming
    /// onto the folder and clicking it, or resting on it a moment, before the flyout comes up; Animations the folder clicked
    /// and its flyout coming up and going at that speed (<see cref="LookPreview.MotionMs"/>), at once where it is Off.
    /// </summary>
    private void Opens(Shown sample, ContainerVisual into, LookShown shows, Look look, Vector2 size, WallpaperSource? wallpaper, RectI room)
    {
        var layout = shows.Layout;
        var scale = MathF.Round(size.Y * LookPreview.OpensBarShare) / LookPreview.BarDip(layout);
        float P(float dip) => Px(dip, scale);
        if (Bar(sample, into, shows, layout.Islands, look, scale, size, wallpaper, room) is not { } panel)
        {
            return;
        }
        var dark = look.Dark;
        var middle = MathF.Round(panel.X + (panel.Width / 2));
        var wide = MathF.Round(size.X * 0.56f);
        var card = new RectF(MathF.Round(middle - (wide / 2)), P(8), wide, MathF.Max(0, panel.Y - P(8) - P(8)));
        var body = dark ? Color.FromArgb(0xF4, 0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF4, 0xF9, 0xF9, 0xF9);
        var rim = dark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x24, 0, 0, 0);
        var lines = dark ? Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x38, 0, 0, 0);
        var accent = look.Accent;
        var flyout = Sprite(sample, into, new Vector2(card.X, card.Y), new Vector2(card.Width, card.Height), dc =>
        {
            var (w, h) = (card.Width, card.Height);
            var radius = P(8);
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, w, h), radius, PopupPaint.Ink(body));
            PopupPaint.Stroke(dc, PopupPaint.Rect(0, 0, w, h), radius, 1, PopupPaint.Ink(rim));
            // What it shows: a heading in the accent, a few lines.
            var (inset, line) = (P(12), MathF.Max(2, P(6)));
            PopupPaint.Fill(dc, PopupPaint.Rect(inset, inset, w * 0.4f, line * 1.4f), line * 0.7f, PopupPaint.Ink(accent));
            for (var at = 0; at < 3; at++)
            {
                var y = inset + (line * 1.4f) + P(12) + (at * P(16));
                if (y + line < h - inset)
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(inset, y, (w - (2 * inset)) * (at % 2 == 0 ? 0.85f : 0.6f), line), line / 2, PopupPaint.Ink(lines));
                }
            }
        });
        var tip = new Vector2(middle + P(4), MathF.Round(panel.Y + (panel.Height * 0.5f)));
        var motion = shows.Sketch == BarSketch.Motion;
        var clicked = motion || !layout.OpenOnHover;
        SpriteVisual? ripple = null;
        if (clicked)
        {
            var ring = P(10);
            ripple = Sprite(sample, into, tip - new Vector2(ring * 2, ring * 2), new Vector2(ring * 4, ring * 4), dc =>
            {
                var centre = new Vector2(ring * 2, ring * 2);
                PopupPaint.Circle(dc, centre, ring, PopupPaint.Ink(Color.FromArgb(0x60, accent.R, accent.G, accent.B)));
                PopupPaint.Ring(dc, centre, ring * 1.6f, MathF.Max(1, P(2)), PopupPaint.Ink(Color.FromArgb(0xA0, accent.R, accent.G, accent.B)));
            });
        }
        var tall = P(22);
        var pointer = Sprite(sample, into, tip, new Vector2(tall, tall), dc => Pointer(dc, tall));
        // §682 (the person, 2026-11-12: "the inline animation speed selector is not yet animated", "animate the open flyout
        // previews too"): both always play — what they show is a motion. Every sample is drawn with its motion off (Worn),
        // so asking that left them still.
        // Played over and over: each a share of the lap.
        if (motion)
        {
            var lap = LookPreview.MotionLapMs;
            var (open, shut) = LookPreview.MotionMs(shows.Config.Animation);
            float At(double ms) => (float)Math.Clamp(ms / lap, 0, 1);
            const double click = 400, held = 1500;
            Loop(sample, ripple!, "Opacity", lap, (0, 0), (At(click - 60), 0), (At(click), 1), (At(click + 260), 0), (1, 0));
            Loop(sample, flyout, "Opacity", lap, (0, 0), (At(click), 0), (At(click + Math.Max(open, 1)), 1), (At(click + held), 1), (At(click + held + Math.Max(shut, 1)), 0), (1, 0));
            Loop(sample, flyout, "Offset.Y", lap, (0, card.Y + P(16)), (At(click), card.Y + P(16)), (At(click + Math.Max(open, 1)), card.Y), (At(click + held), card.Y),
                (At(click + held + Math.Max(shut, 1)), card.Y + P(16)), (1, card.Y + P(16)));
            return;
        }
        // Open flyouts: the pointer comes onto the folder, clicks it (or rests on it), the flyout comes up; then all goes back.
        var from = tip + new Vector2(P(56), -P(28));
        var opens = clicked ? 0.42f : 0.62f;
        Loop(sample, pointer, "Offset.X", LookPreview.OpensLapMs, (0, from.X), (0.08f, from.X), (0.32f, tip.X), (0.86f, tip.X), (1, from.X));
        Loop(sample, pointer, "Offset.Y", LookPreview.OpensLapMs, (0, from.Y), (0.08f, from.Y), (0.32f, tip.Y), (0.86f, tip.Y), (1, from.Y));
        Loop(sample, flyout, "Opacity", LookPreview.OpensLapMs, (0, 0), (opens, 0), (opens + 0.07f, 1), (0.82f, 1), (0.88f, 0), (1, 0));
        if (ripple is not null)
        {
            Loop(sample, ripple, "Opacity", LookPreview.OpensLapMs, (0, 0), (0.36f, 0), (0.39f, 1), (0.5f, 0), (1, 0));
        }
    }

    /// <summary>§682: <paramref name="visual"/>'s <paramref name="property"/> through <paramref name="frames"/> (a share of the lap, its value) every <paramref name="ms"/>, for ever.</summary>
    private void Loop(Shown sample, Visual visual, string property, double ms, params (float At, float Value)[] frames)
    {
        var c = gfx.Compositor;
        // §489: a loop's lap is the engine's straight run and its time a lap (Motion.cs), as the marching ants' are.
        var ease = MotionEase.Straight(c);
        var run = c.CreateScalarKeyFrameAnimation();
        var last = -1f;
        foreach (var (at, value) in frames)
        {
            // Strictly on: two frames at one moment are a jump.
            var when = MathF.Min(1, MathF.Max(at, last + 0.0005f));
            run.InsertKeyFrame(when, value, ease);
            last = when;
        }
        run.Duration = Motion.Lap(ms);
        run.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation(property, run);
        sample.Owned.Add(run);
        sample.Owned.Add(ease);
    }

    /// <summary>§679: a layer of its own in <paramref name="into"/> now, under what is added after it.</summary>
    private ContainerVisual Layer(Shown sample, ContainerVisual into)
    {
        var layer = gfx.Compositor.CreateContainerVisual();
        into.Children.InsertAtTop(layer);
        sample.Owned.Add(layer);
        return layer;
    }

    /// <summary>
    /// §679: a window standing in <paramref name="frame"/> (px, at <paramref name="scale"/>) in the mode's colours: its title
    /// bar, a side pane, lines of what it shows and a picture in the accent across its foot, its rim round — so where its foot
    /// stops, or runs on under the bar, reads.
    /// </summary>
    private void Window(Shown sample, ContainerVisual into, RectF frame, Look look, float scale)
    {
        if (frame.Width < 1 || frame.Height < 1)
        {
            return;
        }
        float P(float dip) => Px(dip, scale);
        var dark = look.Dark;
        var body = dark ? Color.FromArgb(255, 0x2B, 0x2B, 0x2B) : Color.FromArgb(255, 0xF9, 0xF9, 0xF9);
        var pane = dark ? Color.FromArgb(255, 0x24, 0x24, 0x24) : Color.FromArgb(255, 0xEE, 0xEE, 0xEE);
        var title = dark ? Color.FromArgb(255, 0x38, 0x38, 0x38) : Color.FromArgb(255, 0xE3, 0xE3, 0xE3);
        var rim = dark ? Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x40, 0, 0, 0);
        var lines = dark ? Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x30, 0, 0, 0);
        var accent = look.Accent;
        var picture = Color.FromArgb(0xC0, accent.R, accent.G, accent.B);
        var (w, h) = (frame.Width, frame.Height);
        Sprite(sample, into, new Vector2(frame.X, frame.Y), new Vector2(w, h), dc =>
        {
            var radius = P(8);
            PopupPaint.Clipped(dc, PopupPaint.Rect(0, 0, w, h), radius, () =>
            {
                var head = MathF.Max(2, P(16));
                var side = MathF.Round(w * 0.26f);
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, w, h), 0, PopupPaint.Ink(body));
                PopupPaint.Fill(dc, PopupPaint.Rect(0, head, side, h - head), 0, PopupPaint.Ink(pane));
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, w, head), 0, PopupPaint.Ink(title));
                var line = MathF.Max(2, P(5));
                for (var at = 0; at < 2; at++)
                {
                    var y = head + P(10) + (at * P(12));
                    PopupPaint.Fill(dc, PopupPaint.Rect(side + P(10), y, (w - side - P(20)) * (at == 0 ? 0.7f : 0.45f), line), line / 2, PopupPaint.Ink(lines));
                    PopupPaint.Fill(dc, PopupPaint.Rect(P(8), y, side - P(16), line), line / 2, PopupPaint.Ink(lines));
                }
                // A picture across its foot: what a bar over the windows covers, what one keeping its room leaves whole.
                var top = head + P(36);
                PopupPaint.Fill(dc, PopupPaint.Rect(side + P(10), top, w - side - P(20), MathF.Max(0, h - top - P(10))), P(4), PopupPaint.Ink(picture));
            });
            PopupPaint.Stroke(dc, PopupPaint.Rect(0, 0, w, h), radius, 1, PopupPaint.Ink(rim));
        });
    }

    /// <summary>§679: a bar's mark at its edge (hidden till pointed at), round, in the mode's ink.</summary>
    private SpriteVisual Mark(Shown sample, ContainerVisual into, RectF mark, Look look)
    {
        var ink = look.Dark ? Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xA0, 0, 0, 0);
        return Sprite(sample, into, new Vector2(mark.X, mark.Y), new Vector2(mark.Width, mark.Height),
            dc => PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, mark.Width, mark.Height), MathF.Min(mark.Width, mark.Height) / 2, PopupPaint.Ink(ink)));
    }

    /// <summary>§679: the pointer's arrow, its tip at the top left, <paramref name="tall"/> px: white, outlined in black, as Windows' own.</summary>
    private static void Pointer(ID2D1DeviceContext* dc, float tall)
    {
        var u = tall / 20f;
        ReadOnlySpan<Vector2> shape = [new(1, 1), new(1, 17), new(5.2f, 13), new(8.2f, 19.2f), new(11, 18), new(8.1f, 11.9f), new(13.6f, 11.9f)];
        var points = stackalloc D2D_POINT_2F[shape.Length];
        for (var at = 0; at < shape.Length; at++)
        {
            points[at] = new D2D_POINT_2F(shape[at].X * u, shape[at].Y * u);
        }
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        try
        {
            dc->GetFactory(&factory);
            ComUtil.Check(factory->CreatePathGeometry(&path));
            ComUtil.Check(path->Open(&sink));
            sink->BeginFigure(points[0], D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_FILLED);
            sink->AddLines(points + 1, (uint)(shape.Length - 1));
            sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_CLOSED);
            ComUtil.Check(sink->Close());
            var white = Gfx.Brush(dc, PopupPaint.Ink(Color.FromArgb(255, 255, 255, 255)));
            var black = Gfx.Brush(dc, PopupPaint.Ink(Color.FromArgb(255, 0, 0, 0)));
            try
            {
                dc->FillGeometry((ID2D1Geometry*)path, (ID2D1Brush*)white, null);
                dc->DrawGeometry((ID2D1Geometry*)path, (ID2D1Brush*)black, MathF.Max(1, u * 1.2f), null);
            }
            finally
            {
                white->Release();
                black->Release();
            }
        }
        finally
        {
            ComUtil.Release(ref sink);
            ComUtil.Release(ref path);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>§628: what a pin of a sample opens, its name and icon as the shell has them; null where it cannot say (a pin the bar shows dimmed).</summary>
    private static Item? Resolved(string target)
    {
        try
        {
            return Item.Resolve(target);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a preview's pin not resolved: {error.Message}");
            return null;
        }
    }

    /// <summary>
    /// §551: <paramref name="shows"/>' menu (LookPreview.MenuOf) drawn as the bar's menu is (MenuSample), a menu's width,
    /// in the middle of <paramref name="room"/> (px): in the tokens a menu of the bar wears (FlyoutTheme.For, the bar's look,
    /// its font), Explorer's own pictures where its rows' glyphs have one, as the menu draws them.
    /// </summary>
    private void Menu(Shown sample, ContainerVisual into, LookShown shows, Look look, float scale, Vector2 room)
    {
        var items = LookPreview.MenuOf(shows);
        var theme = FlyoutTheme.For(look);
        var tone = theme.Dark ? ExplorerIcons.Tone.Dark : ExplorerIcons.Tone.Light;
        bool Pictured(string glyph) => !theme.HighContrast && ExplorerIcons.For(glyph, tone) is not null;
        float P(float dip) => Px(dip, scale);
        var size = new Vector2(P(Flyout.MenuWidthDip), P(MenuSample.Plan(items).Height));
        var at = new Vector2(MathF.Round((room.X - size.X) / 2), MathF.Round((room.Y - size.Y) / 2));
        using (gfx.Wearing(look.MenuTypeface))
        {
            Sprite(sample, into, at, size, dc => MenuSample.Paint(dc, gfx, theme, scale, size.X, items, Pictured));
        }
        foreach (var (glyph, box) in MenuSample.Pictured(items, scale, Pictured))
        {
            if (ExplorerIcons.For(glyph, tone) is { } svg && gfx.Svg(svg, box.Width) is { } picture)
            {
                sample.Owned.Add(picture);
                Sprite(sample, into, at + new Vector2(box.X, box.Y), new Vector2(box.Width, box.Height), picture);
            }
        }
    }

    /// <summary>
    /// The section's lines in their folder (<see cref="LookPreview.PanelOf"/>), opened and laid out as the open folder lays them
    /// out (FolderFlyout's plan, each line in the Appearance's Widgets in folders; §514 open, a flyout, its outline the
    /// Flyouts'): the folder (its caller closes it, <see cref="Close"/>), the flyout's look and the plan. One plan, measured
    /// (<see cref="LinesDip"/>) and drawn (<see cref="Lines"/>) alike.
    /// </summary>
    private static (FolderIsland Folder, Look Worn, FolderPlan Plan) Laid(Gfx gfx, LookShown shows, Look look, float scale)
    {
        var holder = LookPreview.PanelOf();
        var folder = new FolderIsland(Config.IslandKey("preview", holder.Id), holder, gfx, shows.Config);
        try
        {
            folder.Opened(gfx);
            // §649: attached on its ground where the Appearance says one (LookPreview.GroundOf), as the open folder wears it.
            var ground = LookPreview.GroundOf(look, shows.Layout);
            var worn = look.Flyout(ground);
            var natural = FolderFlyout.Natural(folder.Tiles, folder.Laid, scale, worn);
            var plan = FolderFlyout.Build(worn, scale, folder.View, folder.Laid, [.. folder.Tiles.Select(tile => tile.Cell)], 100_000,
                natural: natural, room: FlyoutPlacement.Room(1440 * scale, scale), attached: ground is not null);
            return (folder, worn, plan);
        }
        catch
        {
            Close(folder);
            throw;
        }
    }

    /// <summary>A folder of a sample's let go: closed, then disposed.</summary>
    private static void Close(FolderIsland folder)
    {
        Quietly(folder.Closed);
        Quietly(folder.Dispose);
    }

    /// <summary>
    /// The section's lines (<see cref="Laid"/>) drawn as the open folder draws them (FolderFlyout's painters, the panel's rim
    /// the flyout's outline): the surface and the plan; none where nothing came of it.
    /// </summary>
    private (CompositionDrawingSurface? Surface, FolderPlan? Plan) Lines(Shown sample, LookShown shows, Look look, float scale)
    {
        var (folder, worn, plan) = Laid(gfx, shows, look, scale);
        sample.Islands.Add(folder);
        if (plan.Width < 1 || plan.Height < 1)
        {
            return (null, null);
        }
        var flat = LookPreview.GroundOf(look, shows.Layout) is not null;
        var surface = gfx.Draw(plan.Width, plan.Height, dc => PaintLines(dc, folder, plan, worn, scale, flat));
        sample.Owned.Add(surface);
        return (surface, plan);
    }

    /// <summary>A sample's lines (<paramref name="surface"/>, <paramref name="plan"/>'s size) at <paramref name="at"/> in its room, where they stand kept.</summary>
    private void Lined(Shown sample, ContainerVisual into, Vector2 at, FolderPlan plan, CompositionDrawingSurface surface)
    {
        var size = new Vector2(plan.Width, plan.Height);
        Sprite(sample, into, at, size, surface);
        sample.Lines = new RectF(at.X, at.Y, size.X, size.Y);
    }

    /// <summary>
    /// The open folder's lines as it draws them: each on its card in its colours (FolderFlyout.InkOf, its pattern a sheet of
    /// its section's), its widget's drawing on it, its own outline; each section's rim the flyout's outline (the host's rim).
    /// §649 <paramref name="flat"/>, attached: no card, no rim — the lines on the ground under them —, a divider a line of the
    /// outline across its room (FolderFlyout.RuleOf).
    /// </summary>
    private void PaintLines(ID2D1DeviceContext* dc, FolderIsland folder, FolderPlan plan, Look worn, float scale, bool flat)
    {
        var theme = FlyoutTheme.Folder(worn);
        var hair = MathF.Max(1, MathF.Round(scale));
        FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
        // §704: Liquid glass's light over the lines' tint and under what stands on them: the cards first, then each piece's
        // glow and lens, then the lines' drawing, the hairlines between lines that meet; its rim in place of the outline.
        var glass = flat ? null : worn.Liquid;
        if (glass is not null)
        {
            Each(cards: true, drawn: false);
            foreach (var piece in plan.Pieces)
            {
                GlassPaint.Under(dc, piece, plan.Radius, glass, scale);
            }
            Each(cards: false, drawn: true);
        }
        else
        {
            Each(cards: true, drawn: true);
        }
        void Each(bool cards, bool drawn)
        {
            for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
            {
                if (!plan.Shows(index))
                {
                    continue;
                }
                var tile = folder.Tiles[index];
                var line = tile.Cell.Island;
                var own = worn.HighContrast || !FolderFlyout.Carded(tile.Cell) ? default : worn.Colors;
                var card = plan.Cards[index];
                var list = plan.Lists[plan.CardList[index]];
                var run = index < plan.CardRun.Count ? plan.CardRun[index] : 0;
                var on = run >= 0 && run < list.Runs.Count ? list.Runs[run] : list.Box;
                var style = FolderFlyout.StyleFor(scale, worn, theme);
                PopupPaint.Clipped(dc, on, plan.Radius, () =>
                {
                    if (cards)
                    {
                        PopupPaint.Fill(dc, card, 0, PopupPaint.Ink(FolderFlyout.InkOf(theme, FolderFlyout.CardFill.None, own, carded: !flat)));
                        if (own.CardPattern is { } pattern)
                        {
                            PatternPaint.Fill(dc, pattern, scale, card, new Vector2(on.left, on.top));
                        }
                    }
                    if (!drawn)
                    {
                        return;
                    }
                    Moved(dc, card.left, card.top, () =>
                    {
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        if (ListWidgets.Find(line.Kind) is not { Line: ListLine.Card })
                        {
                            using var typeface = gfx.Wearing(style.Look.Typeface);
                            FolderFlyout.PaintListLine(dc, gfx, tile.Island, line, box, plan, theme, scale);
                        }
                        else
                        {
                            FolderFlyout.PaintContent(dc, tile.Island, box, style);
                        }
                    });
                    if (own.CardOutline is { A: > 0 } rim)
                    {
                        var inset = hair / 2;
                        PopupPaint.Stroke(dc, PopupPaint.Rect(card.left + inset, card.top + inset, card.right - card.left - hair, card.bottom - card.top - hair), 0, hair,
                            PopupPaint.Ink(Color.FromArgb(rim.A, rim.R, rim.G, rim.B)));
                    }
                });
            }
        }
        var stroke = MathF.Max(1, MathF.Round(theme.StrokeDip * scale));
        // §649: attached, each divider a line of the folder's outline across its room, as the open folder draws it.
        if (flat)
        {
            for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
            {
                if (plan.Shows(index) && FolderFlyout.Divides(folder.Tiles[index].Cell))
                {
                    var rule = FolderFlyout.RuleOf(plan.Cards[index], stroke);
                    PopupPaint.Fill(dc, rule, 0, PopupPaint.Ink(FolderFlyout.RuleInk(theme)));
                }
            }
            return;
        }
        // §704: Liquid glass's hairlines between the lines that meet in a piece, as the open folder draws them
        // (FolderFlyout.Seams), and its rim round each piece, as the host draws it.
        if (glass is not null)
        {
            var seams = FolderFlyout.SeamsOf(plan, glass, scale, index => index < folder.Tiles.Count && FolderFlyout.Carded(folder.Tiles[index].Cell));
            foreach (var seam in seams)
            {
                if (seam is { } at)
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect((float)at.Left, (float)at.Top, (float)at.Width, (float)at.Height), 0, PopupPaint.Ink(GlassPaint.DividerInk(glass)));
                }
            }
            foreach (var piece in plan.Pieces)
            {
                GlassPaint.Rim(dc, piece, plan.Radius, glass, scale);
            }
            return;
        }
        // Each section's rim, as the folder's host draws it round each of its pieces: the flyout's outline.
        if (theme.Stroke.A > 0)
        {
            foreach (var piece in plan.Pieces)
            {
                var inset = stroke / 2;
                PopupPaint.Stroke(dc, PopupPaint.Rect(piece.left + inset, piece.top + inset, piece.right - piece.left - stroke, piece.bottom - piece.top - stroke),
                    MathF.Max(0, plan.Radius - inset), stroke, PopupPaint.Ink(theme.Stroke));
            }
        }
    }

    /// <summary><paramref name="paint"/> with the origin moved to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static void Moved(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        var moved = D2D_MATRIX_3X2_F.Translation(x, y) * was;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&was);
        }
    }


    /// <summary>A sprite of the sample at <paramref name="at"/>, <paramref name="size"/> px, drawn by <paramref name="paint"/> once.</summary>
    private SpriteVisual Sprite(Shown sample, ContainerVisual into, Vector2 at, Vector2 size, Paint paint)
    {
        var surface = gfx.Draw(MathF.Max(1, size.X), MathF.Max(1, size.Y), paint);
        sample.Owned.Add(surface);
        return Sprite(sample, into, at, size, surface);
    }

    /// <summary>A sprite of the sample at <paramref name="at"/>, <paramref name="size"/> px, showing <paramref name="surface"/>.</summary>
    private SpriteVisual Sprite(Shown sample, ContainerVisual into, Vector2 at, Vector2 size, CompositionDrawingSurface surface)
    {
        var c = gfx.Compositor;
        var brush = c.CreateSurfaceBrush(surface);
        var sprite = c.CreateSpriteVisual();
        sprite.Brush = brush;
        sprite.Size = size;
        sprite.Offset = new Vector3(at, 0);
        into.Children.InsertAtTop(sprite);
        sample.Owned.Add(brush);
        sample.Owned.Add(sprite);
        return sprite;
    }

    private static void Quietly(Action act)
    {
        try
        {
            act();
        }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Its window's tree went first (Alt+F4): it went with it.
        }
    }
}
