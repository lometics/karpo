// The list lines of the sound panel's four pieces, what's playing, a pin and every switch a list
// holds, at every height a line can be given (docs\widgets.md §9, "A line at every height").
//
// "for the list view widgets they have not been optimized ad all. they should also be optimized to
// look good from 1 to 6 cell heights (maybe even higher depending on the type of widget). they should
// keep their functionality for every size if possible." So each of these kinds is drawn here at every
// height from one row to its most, at 100, 150 and 200 % and at the largest text, and held to four
// things: nothing it draws leaves its line, nothing it lays out overlaps anything else, one row shows
// the main reading, and a taller line never says less than a shorter one (ListTiers). The sheets —
// one per kind, every height side by side at 150 % — go to %TEMP%\list-<kind>.png.
//
// The switches are every kind the list registers that a switch draws, so a switch added to the
// registry is held here with nothing of its own: its line is drawn from its data (SwitchIsland).
//
// Everything is a stand-in: sound devices and apps (SoundParts.Made), a track that is not playing
// anywhere, still at two minutes in (MediaIsland.Stand), a pin to a link drawn with a glyph, and
// switches handed over through QuickSwitches.Source. Nothing here reads or changes anything of this
// PC's.

using System.Text.RegularExpressions;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Quick = Karpo.Dock.QuickSettings.Controls;

namespace Karpo.Tests.Folders;

[Collection(Karpo.Tests.Perf.SamplerState.Collection)]
public sealed unsafe class ListLinesControlsTests
{
    /// <summary>
    /// The kinds whose lines this holds, in the order the sheets are made: the sound panel's four,
    /// what's playing, a pin, and every switch the list registers.
    /// </summary>
    private static ListKind[] Kinds() =>
    [
        ListKind.SoundOutput, ListKind.SoundInput, ListKind.SoundMixer, ListKind.SoundVolume, ListKind.Media, ListKind.Pin,
        .. ListWidgets.All.Where(widget => widget.Drawing == WidgetSource.Switch).Select(widget => widget.Kind),
    ];

    /// <summary>The display scales a line is held at.</summary>
    private static readonly float[] Scales = [1f, 1.5f, 2f];

    /// <summary>A list line's width, in DIPs: the panel's own.</summary>
    private const float WidthDip = 360;

    // --- What each kind is --------------------------------------------------------------------------

    /// <summary>
    /// Each of these kinds is a line of its own, drawn for its height rather than the list's row of a
    /// glyph and a name, and a new line of it may be one row to six at the least.
    /// </summary>
    [Fact]
    public void EachIsALineOfItsOwnFromOneRowToSix()
    {
        foreach (var kind in Kinds())
        {
            var widget = ListWidgets.Of(kind);
            Assert.True(widget.Line == ListLine.Card, $"{kind} is the list's own row, not a line of its own");
            Assert.True(widget.Range.Least == LineHeight.Fewest && widget.Range.Most >= 12, $"{kind} may be {widget.Range.Least} to {widget.Range.Most} cells");
        }
    }

    // --- Every height ------------------------------------------------------------------------------

    /// <summary>
    /// Every kind at every height from one row to its most, at 100, 150 and 200 % and at the largest
    /// text: drawn without the clip a folder gives it, nothing it inks leaves its line.
    /// </summary>
    [UiFact]
    public void EveryLineStaysInsideItself()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            foreach (var kind in Kinds())
            {
                foreach (var (scale, textScale) in Sizes())
                {
                    for (var rows = 1; rows <= MostOf(kind); rows++)
                    {
                        using var line = Line(gfx, kind);
                        var style = Style(scale, textScale);
                        var box = Box(rows, style);
                        var margin = PopupPaint.Round(24 * scale);
                        var width = TilePaint.Width(box) + (2 * margin);
                        var height = TilePaint.Height(box) + (2 * margin);
                        (float Left, float Top, float Right, float Bottom) ink = default;
                        Offscreen.Bitmap(width, height,
                            dc => Offscreen.Shifted(dc, margin, margin, () => line.PaintCard(dc, box, style, WidgetSurface.List)),
                            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
                        var at = $"{kind} at {rows} row(s), {scale * 100:0} %, text {textScale * 100:0} %";
                        if (ink.Right <= ink.Left)
                        {
                            failures.Add($"{at}: drew nothing");
                            continue;
                        }
                        if (ink.Left < margin - 1 || ink.Top < margin - 1 || ink.Right > margin + TilePaint.Width(box) + 1 || ink.Bottom > margin + TilePaint.Height(box) + 1)
                        {
                            failures.Add($"{at}: ink from ({ink.Left - margin:0}, {ink.Top - margin:0}) to ({ink.Right - margin:0}, {ink.Bottom - margin:0}) leaves a line {TilePaint.Width(box):0} by {TilePaint.Height(box):0}");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every kind at every height, at 100, 150 and 200 % and at the largest text: what it lays out —
    /// its glyph or picture, its words, its controls — stands inside the line and overlaps nothing
    /// else it lays out.
    /// </summary>
    [UiFact]
    public void NothingALineLaysOutOverlaps()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            foreach (var kind in Kinds())
            {
                foreach (var (scale, textScale) in Sizes())
                {
                    for (var rows = 1; rows <= MostOf(kind); rows++)
                    {
                        using var line = Line(gfx, kind);
                        var style = Style(scale, textScale);
                        var box = Box(rows, style);
                        var parts = PartsOf(line, box, style);
                        var at = $"{kind} at {rows} row(s), {scale * 100:0} %, text {textScale * 100:0} %";
                        foreach (var (name, part) in parts)
                        {
                            if (part.left < box.left - 0.5f || part.top < box.top - 0.5f || part.right > box.right + 0.5f || part.bottom > box.bottom + 0.5f)
                            {
                                failures.Add($"{at}: its {name} ({part.left:0}, {part.top:0})..({part.right:0}, {part.bottom:0}) leaves the line");
                            }
                        }
                        for (var one = 0; one < parts.Count; one++)
                        {
                            for (var other = one + 1; other < parts.Count; other++)
                            {
                                if (Overlap(parts[one].Box, parts[other].Box) > 0.5f)
                                {
                                    failures.Add($"{at}: its {parts[one].Name} and its {parts[other].Name} overlap");
                                }
                            }
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A device's name too long for its row is cut short of what stands at the row's end — the microphones' mute
    /// over the one in use (ListRow.Control) — at every height, at 100, 150 and 200 % and at the largest text: where
    /// it is drawn, its words end before the control, never under it.
    /// </summary>
    [UiFact]
    public void ALongNameIsCutShortOfItsRowsControls()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            SoundParts.Made = () => new Machine(Long);
            foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput })
            {
                foreach (var (scale, textScale) in Sizes())
                {
                    for (var rows = 1; rows <= MostOf(kind); rows++)
                    {
                        using var line = Line(gfx, kind);
                        var sound = Assert.IsType<SoundPartTile>(line);
                        var style = Style(scale, textScale);
                        var box = Box(rows, style);
                        var said = new List<(string Text, D2D_RECT_F Box)>();
                        try
                        {
                            PaintWatch.Said = (text, at) => said.Add((text, at));
                            Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => line.PaintCard(dc, box, style, WidgetSurface.List), _ => { });
                        }
                        finally
                        {
                            PaintWatch.Said = null;
                        }
                        var at = $"{kind} at {rows} row(s), {scale * 100:0} %, text {textScale * 100:0} %";
                        var controls = sound.Placed.Where(widget => widget is not Karpo.Dock.QuickSettings.Controls.ListRow and not Karpo.Dock.QuickSettings.Controls.Label).ToList();
                        foreach (var row in sound.Placed.OfType<Karpo.Dock.QuickSettings.Controls.ListRow>())
                        {
                            // What a row says is said in its own px: on the card, it is where the row stands.
                            foreach (var (text, local) in said.Where(each => each.Text == row.Title))
                            {
                                var words = Shifted(local, row.Bounds);
                                foreach (var control in controls.Where(control => Overlap(words, control.Bounds) > 0.5f))
                                {
                                    failures.Add($"{at}: \"{text}\" ({words.left:0}..{words.right:0}) runs under its {control.Name} ({control.Bounds.left:0}..{control.Bounds.right:0})");
                                }
                            }
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Every list's row (ListRow: Wi-Fi's networks, Bluetooth's devices, sound's, Cast's, a page's choices) with a
    /// name and a second line longer than it has room for, and each thing a row may end with — its own menu's "...",
    /// a chevron, a battery and its level, a control of its list over its band — and all of them at once, at 100,
    /// 150 and 200 % and at the largest text: its words are cut short of each, never under it, and end in the row.
    /// </summary>
    [UiFact]
    public void ARowsLongNameIsCutShortOfEverythingAtItsEnd()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            foreach (var (scale, textScale) in Sizes())
            {
                var style = Style(scale, textScale);
                var context = new Quick.QuickContext { Gfx = gfx, Theme = Quick.QuickTheme.For(style.Theme), Scale = scale, TextScale = textScale };
                var width = PopupPaint.Round(WidthDip * scale);
                var height = context.T(40);
                var side = context.D(32);
                var control = new Quick.Button("", Quick.ButtonStyle.Subtle) { Name = "control", Bounds = PopupPaint.Rect(width - context.D(16) - side, PopupPaint.Round((height - side) / 2), side, side) };
                var ends = new (string Name, Action<Quick.ListRow> Set)[]
                {
                    ("menu", row =>
                    {
                        row.Clicked = () => { };
                        row.ContextClicked = _ => { };
                        row.Hot = true;
                    }),
                    ("chevron", row => row.Trailing = ""),
                    ("battery", row =>
                    {
                        row.Tail = "72%";
                        row.TailGlyph = "";
                        row.TailRoom = "100%";
                        row.TailGlyphRoom = true;
                    }),
                    ("control", row => row.Control = control),
                };
                foreach (var (name, set) in ends.Append(("everything", row =>
                {
                    foreach (var (_, each) in ends)
                    {
                        each(row);
                    }
                })))
                {
                    var row = new Quick.ListRow { Title = Long, Detail = Long, Glyph = "", Bounds = PopupPaint.Rect(0, 0, width, height) };
                    set(row);
                    var said = new List<(string Text, D2D_RECT_F Box)>();
                    try
                    {
                        PaintWatch.Said = (text, at) => said.Add((text, at));
                        Offscreen.Bitmap(width, height, dc => row.Paint(dc, context), _ => { });
                    }
                    finally
                    {
                        PaintWatch.Said = null;
                    }
                    var at = $"A row ending in {name}, {scale * 100:0} %, text {textScale * 100:0} %";
                    var words = said.Where(each => each.Text == Long).Select(each => each.Box).ToList();
                    var others = said.Where(each => each.Text != Long && each.Text != row.Glyph).Select(each => (each.Text, each.Box)).ToList();
                    if (row.Control is { } over)
                    {
                        others.Add((over.Name, over.Bounds));
                    }
                    Assert.True(words.Count == 2, $"{at}: said its words {words.Count} time(s)");
                    Assert.True(others.Count > 0, $"{at}: drew nothing at its end");
                    foreach (var box in words)
                    {
                        if (box.right > width + 0.5f)
                        {
                            failures.Add($"{at}: its words end at {box.right:0}, past its {width:0}");
                        }
                        foreach (var (other, end) in others.Where(other => Overlap(box, other.Box) > 0.5f))
                        {
                            failures.Add($"{at}: its words ({box.left:0}..{box.right:0}) run under \"{other}\" ({end.left:0}..{end.right:0})");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // --- A meter keeps its gap (§449) --------------------------------------------------------------

    /// <summary>The scales and text sizes the meters are held at: 100 and 150 %, at the list's text and the largest.</summary>
    private static readonly (float Scale, float TextScale)[] SpacedAt = [(1f, 1f), (1.5f, 1f), (1f, 2.25f), (1.5f, 2.25f)];

    /// <summary>The rows a kind of no sizes is drawn at besides its own height.</summary>
    private static readonly int[] SpacedRows = [1, 2, 4, 6];

    /// <summary>
    /// §449 (the person, 2026-10-31: "in the focus time when the timer is running the progress bar and the pause button
    /// touch. this should not be. fix this for similar widgets where this happens as well."): every kind a list holds,
    /// each sized kind in each of its sizes and every other at its own height and at one, two, four and six rows, at 100
    /// and 150 % and at the list's text and the largest, doing what draws its meters — the timer, focus and the stopwatch
    /// running, a track playing with its timeline, the battery, the drives, the volume and the brightness: no meter,
    /// progress bar, timeline or slider (as far as its thumb goes) comes nearer a control's plate than a button's gap
    /// (<see cref="LineButton.GapDip"/>) across or down, nor into where a press on the control lands.
    /// </summary>
    [UiFact]
    public void NoMeterComesWithinAGapOfAControl()
    {
        var failures = MetersTooNear(out var seen);
        // Every kind that draws a meter in the states it is drawn in here.
        ListKind[] meters = [ListKind.Timer, ListKind.Focus, ListKind.Media, ListKind.Battery, ListKind.Drive, ListKind.NetworkDrive, ListKind.SoundVolume, ListKind.Brightness, ListKind.SoundMixer];
        Assert.True(meters.All(seen.Contains), $"meters drawn by {string.Join(", ", seen)} only");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The lines §449 found a meter too near a control in, as they draw now, dark, at 150 %, at the list's text and the
    /// largest: focus and the timer running, now playing whole with its timeline, the volume and the brightness on one
    /// row and the mixer — spacing-meters-dark.png in KARPO_PNG_DIR.
    /// </summary>
    [UiFact]
    public void MetersKeepTheirGapPicture() =>
        ListGutterTests.StoodIn<Gfx>(Karpo.Tests.Forecasts.SavedWeather.Fixed, Offscreen.OnStaThread, gfx =>
        {
            MediaIsland.Stand = new MediaStand(Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true);
            CellItem Sized(ListKind kind, LineSize size)
            {
                var line = ListGutterTests.Made(kind);
                line.Size = size;
                line.Normalize();
                return line;
            }
            var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before,
                ListGutterTests.Made(ListKind.Focus), ListGutterTests.Made(ListKind.Timer), Sized(ListKind.Media, LineSize.Full),
                Sized(ListKind.SoundVolume, LineSize.Compact), Sized(ListKind.Brightness, LineSize.Compact), Sized(ListKind.SoundVolume, LineSize.Medium),
                ListGutterTests.Made(ListKind.SoundMixer));
            try
            {
                ListGutterTests.Ready(folder);
                Running(folder);
                var looks = new[] { ("150 %", ListFormPictures.LookFor(dark: true)), ("150 %, 225 % text", ListFormPictures.LookFor(dark: true, 2.25f)) };
                ListFormPictures.Sheet(gfx, "spacing-meters-dark.png", 1.5f, [.. looks.Select(each => (each.Item1, each.Item2, folder, ListFormPictures.Plan(folder, each.Item2, 1.5f).Plan))]);
            }
            finally
            {
                MediaIsland.Stand = null;
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>What <see cref="NoMeterComesWithinAGapOfAControl"/> finds:a line per kind, size, scale and text that breaks it; the kinds that drew a meter.</summary>
    internal static List<string> MetersTooNear(out SortedSet<ListKind> seen)
    {
        var failures = new SortedSet<string>(StringComparer.Ordinal);
        var drew = new SortedSet<ListKind>();
        Spaced((gfx, folder, size) =>
        {
            foreach (var (scale, textScale) in SpacedAt)
            {
                var look = ListFormPictures.LookFor(dark: true, textScale);
                var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                var sizes = LineButton.Of(gfx, style);
                for (var index = 0; index < folder.Tiles.Count; index++)
                {
                    var kind = folder.Tiles[index].Cell.Island.List!.Value;
                    var width = plan.Cards[index].right - plan.Cards[index].left;
                    IEnumerable<float> heights = size is null
                        ? [ListFormPictures.Given(plan, index), .. SpacedRows.Select(rows => LineRows.Px(rows, style))]
                        : [ListFormPictures.Given(plan, index)];
                    foreach (var height in heights.Distinct())
                    {
                        var (meters, controls) = SpacedPieces(gfx, folder, plan, index, width, height, look, scale);
                        if (meters.Count > 0)
                        {
                            drew.Add(kind);
                        }
                        foreach (var meter in meters)
                        {
                            foreach (var control in controls)
                            {
                                var hit = sizes.Hit(control, 0, height);
                                var apart = Apart(meter, control);
                                if (apart < sizes.Gap - 0.5f || Overlap(meter, hit) > 0.5f)
                                {
                                    failures.Add($"{kind}{(size is { } named ? $" {named}" : "")} {height:0} px tall at {scale * 100:0} %, {textScale * 100:0} % text: "
                                        + $"its meter ({meter.left:0}, {meter.top:0})..({meter.right:0}, {meter.bottom:0}) {apart:0.#} px from a control ({control.left:0}, {control.top:0})..({control.right:0}, {control.bottom:0}), not {sizes.Gap:0}");
                                }
                            }
                        }
                    }
                }
            }
        });
        seen = drew;
        // One line per kind, size, scale and text: the first pair it found.
        return [.. failures.GroupBy(failure => failure[..failure.IndexOf(": its", StringComparison.Ordinal)]).Select(group => group.First())];
    }

    /// <summary>How far apart two boxes are, px: the more of the room across and down between them (0 where they overlap).</summary>
    private static float Apart(D2D_RECT_F one, D2D_RECT_F other) =>
        MathF.Max(MathF.Max(0, MathF.Max(one.left - other.right, other.left - one.right)), MathF.Max(0, MathF.Max(one.top - other.bottom, other.top - one.bottom)));

    /// <summary>
    /// <paramref name="use"/> on a folder of every kind a list holds, a folder per group of the gutter tests' (its kinds of
    /// no sizes, size null) and a folder per size of every sized kind offering it, on the gutter tests' stand-ins, a track
    /// playing with a timeline, and every timer, focus and stopwatch running.
    /// </summary>
    private static void Spaced(Action<Gfx, FolderIsland, LineSize?> use) =>
        ListGutterTests.StoodIn<Gfx>(Karpo.Tests.Forecasts.SavedWeather.Fixed, Offscreen.OnStaThread, gfx =>
        {
            MediaIsland.Stand = new MediaStand(Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true);
            var sized = Karpo.Tests.Core.LineSizeTests.Sized.ToHashSet();
            var folders = ListGutterTests.Groups.Select(group => (Lines: group.Kinds.Where(kind => !sized.Contains(kind)).Select(ListGutterTests.Made).ToArray(), Size: (LineSize?)null))
                .Concat(new[] { LineSize.Compact, LineSize.Medium, LineSize.Full }.Select(size => (Lines: sized.Where(kind => kind.Offered().Contains(size)).Select(kind =>
                {
                    var line = ListGutterTests.Made(kind);
                    line.Size = size;
                    line.Normalize();
                    return line;
                }).ToArray(), Size: (LineSize?)size)))
                .Where(each => each.Lines.Length > 0);
            try
            {
                foreach (var (lines, size) in folders)
                {
                    var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, lines);
                    try
                    {
                        ListGutterTests.Ready(folder);
                        Running(folder);
                        use(gfx, folder, size);
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        });

    /// <summary>Every timer, focus session and stopwatch of <paramref name="folder"/> started, as its start button starts it.</summary>
    private static void Running(FolderIsland folder)
    {
        foreach (var tile in folder.Tiles)
        {
            switch (tile.Island)
            {
                case CountdownTile countdown:
                    countdown.Act();
                    break;
                case StopwatchTile stopwatch:
                    stopwatch.Act();
                    break;
            }
        }
    }

    /// <summary>
    /// Line <paramref name="index"/> drawn alone into a box <paramref name="height"/> px tall, as the open folder draws it:
    /// the meters and the controls it drew (<see cref="PaintWatch.Spaced"/>), in its own px.
    /// </summary>
    private static (List<D2D_RECT_F> Meters, List<D2D_RECT_F> Controls) SpacedPieces(Gfx gfx, FolderIsland folder, FolderPlan plan, int index, float width, float height, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var tile = folder.Tiles[index];
        var box = PopupPaint.Rect(0, 0, width, height);
        var meters = new List<D2D_RECT_F>();
        var controls = new List<D2D_RECT_F>();
        try
        {
            Offscreen.Bitmap(width, height, dc =>
            {
                var device = (nint)dc;
                PaintWatch.Spaced = (what, piece) =>
                {
                    D2D_MATRIX_3X2_F at;
                    ((ID2D1DeviceContext*)device)->GetTransform(&at);
                    var placed = new D2D_RECT_F(
                        (piece.left * at._11) + (piece.top * at._21) + at._31,
                        (piece.left * at._12) + (piece.top * at._22) + at._32,
                        (piece.right * at._11) + (piece.bottom * at._21) + at._31,
                        (piece.right * at._12) + (piece.bottom * at._22) + at._32);
                    // What a line scrolls out of its box is not drawn there.
                    if (placed.right <= placed.left || placed.bottom <= 0 || placed.top >= height || placed.right <= 0 || placed.left >= width)
                    {
                        return;
                    }
                    (what == Spacing.Meter ? meters : controls).Add(placed);
                };
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                }
                else
                {
                    FolderFlyout.PaintContent(dc, tile.Island, box, style);
                }
            }, _ => { });
        }
        finally
        {
            PaintWatch.Spaced = null;
        }
        return (meters, controls);
    }

    /// <summary>One row shows the main reading, at every scale and at the largest text.</summary>
    [UiFact]
    public void OneRowShowsTheMainReading()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            foreach (var kind in Kinds())
            {
                foreach (var (scale, textScale) in Sizes())
                {
                    using var line = Line(gfx, kind);
                    var style = Style(scale, textScale);
                    var box = Box(1, style);
                    var main = MainOf(line, box, style);
                    if (main is not { } shown || shown.Box.right - shown.Box.left < 1 || shown.Text.Length == 0)
                    {
                        failures.Add($"{kind} at one row, {scale * 100:0} %, text {textScale * 100:0} %: no main reading");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A taller line never says less than a shorter one (ListTiers): every word and every glyph a
    /// line draws at one height it draws again one row taller — whole, or cut where it was cut or
    /// where the room is another — at every scale and at the largest text. A summary of what is not
    /// shown ("2 other devices") may give way to what it stood for.
    /// </summary>
    [UiFact]
    public void ATallerLineNeverSaysLess()
    {
        var failures = new List<string>();
        Drawn(gfx =>
        {
            foreach (var kind in Kinds())
            {
                foreach (var (scale, textScale) in Sizes())
                {
                    using var line = Line(gfx, kind);
                    var style = Style(scale, textScale);
                    List<string>? shorter = null;
                    for (var rows = 1; rows <= MostOf(kind); rows++)
                    {
                        var said = SaidBy(line, Box(rows, style), style);
                        if (shorter is not null)
                        {
                            foreach (var piece in shorter.Where(piece => !said.Any(now => Same(piece, now))))
                            {
                                failures.Add($"{kind} at {rows} rows, {scale * 100:0} %, text {textScale * 100:0} %: no \"{piece}\", which {rows - 1} said (it says {string.Join(", ", said.Select(now => $"\"{now}\""))})");
                            }
                        }
                        shorter = said;
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(40)));
    }

    /// <summary>
    /// A sheet per kind, every height from one row to its most side by side at 150 %, dark, and the
    /// same light: %TEMP%\list-&lt;kind&gt;.png and list-&lt;kind&gt;-light.png, for the person to see.
    /// </summary>
    [UiFact]
    public void ASheetPerKind() =>
        Drawn(gfx =>
        {
            foreach (var kind in Kinds())
            {
                foreach (var dark in new[] { true, false })
                {
                    var path = Path.Combine(Path.GetTempPath(), $"list-{ListWidgets.Of(kind).Info.Id}{(dark ? "" : "-light")}.png");
                    Sheet(gfx, kind, dark, path);
                    Assert.True(File.Exists(path), path);
                }
            }
        });

    /// <summary>
    /// What's playing says where it plays (backlog 7): the app's icon, or the favicon of the site a
    /// browser plays it on, in the leading column (MediaLineAppTests), and the whole widget's line names it. Sheets of every
    /// height at 100 and 150 %, dark and light, in KARPO_PNG_DIR: list-media-source-app-150.png,
    /// list-media-source-site-100-light.png and so on.
    /// </summary>
    [UiFact]
    public void TheMediaLineSaysWhereItPlays() =>
        Drawn(gfx =>
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named ? named : Path.GetTempPath();
            Directory.CreateDirectory(folder);
            var cover = Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.Cover();
            var stands = new[]
            {
                ("app", new MediaStand(AppName: "Spotify", Art: cover, SourceIcon: Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.AppIcon(),
                    Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true)),
                ("site", new MediaStand(Title: "lofi hip hop radio - beats to relax/study to", Artist: "www.youtube.com", AppName: "Brave", Art: cover,
                    SourceIcon: Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.Favicon(), Site: "www.youtube.com")),
                // What the media flyout had besides (§96): shuffle on, repeat this track, and two apps to pick from.
                ("modes", new MediaStand(AppName: "Spotify", Art: cover, SourceIcon: Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.AppIcon(),
                    Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true,
                    CanShuffle: true, Shuffle: true, CanRepeat: true, Repeat: Karpo.Dock.QuickSettings.Services.MediaRepeat.Track,
                    Sessions: [new("Spotify.exe", "Spotify", true), new("msedge.exe", "Microsoft Edge", false)])),
            };
            foreach (var (name, stand) in stands)
            {
                MediaIsland.Stand = stand;
                var line = Line(gfx, ListKind.Media);
                try
                {
                    // The whole widget names where it plays, by the line the flyout says over the track.
                    var style = Style(1.5f, 1f, true);
                    var said = ((MediaIsland)line).LineOf(Box(MostOf(ListKind.Media), style), style).Words.Select(words => words.Text).ToList();
                    Assert.Contains(name == "site" ? "Brave" : "Spotify", said);
                    if (name == "site")
                    {
                        // The site the artist names is said once, without its "www.".
                        Assert.Contains("youtube.com", said);
                        Assert.DoesNotContain("www.youtube.com", said);
                        // Regression: Narrator's name said the site twice, once as the artist.
                        Assert.Equal("lofi hip hop radio - beats to relax/study to · youtube.com · Brave", line.Tooltip(0));
                    }
                }
                finally
                {
                    line.Dispose();
                }
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var path = Path.Combine(folder, $"list-media-source-{name}-{scale * 100:0}{(dark ? "" : "-light")}.png");
                        Sheet(gfx, ListKind.Media, dark, path, scale);
                        Assert.True(File.Exists(path), path);
                    }
                }
            }
        });

    // --- What a kind lays out and says ---------------------------------------------------------------

    /// <summary>What <paramref name="line"/> lays out in <paramref name="box"/>, part by part, as its own layout says.</summary>
    private static List<(string Name, D2D_RECT_F Box)> PartsOf(CellIsland line, D2D_RECT_F box, in TileStyle style)
    {
        var parts = new List<(string, D2D_RECT_F)>();
        void Add(string name, D2D_RECT_F part)
        {
            if (part.right - part.left > 0 && part.bottom - part.top > 0)
            {
                parts.Add((name, part));
            }
        }
        switch (line)
        {
            case SwitchIsland toggle:
            {
                var laid = toggle.LineOf(box, style);
                Add("glyph", laid.Glyph);
                if (laid.NameText is not null)
                {
                    Add("name", laid.Name);
                }
                if (laid.StateText.Length > 0)
                {
                    Add("state", laid.State);
                }
                foreach (var (under, index) in laid.Under.Select((under, index) => (under, index)))
                {
                    Add($"line {index + 2}", under.Box);
                }
                Add("switch", laid.Switch);
                break;
            }
            case SoundPartTile sound:
            {
                // Drawn, so each row has said where it shows what it shows. A row's bounds are its band (LineItem,
                // §428): the line's width, a control of its list standing over it (the microphones' mute); what it
                // shows — its glyph and its words — is what may overlap nothing.
                var drawn = style;
                Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => sound.PaintCard(dc, box, drawn, WidgetSurface.List), _ => { });
                foreach (var control in sound.Placed)
                {
                    if (control is Karpo.Dock.QuickSettings.Controls.ListRow row)
                    {
                        Add($"{row.Name}'s glyph", Shifted(row.Shown.Glyph, row.Bounds));
                        Add($"\"{row.Title}\"", Shifted(row.Shown.Words, row.Bounds));
                    }
                    else
                    {
                        Add(control.Name.Length > 0 ? control.Name : control.GetType().Name, control.Bounds);
                    }
                }
                parts.AddRange(sound.Words.Select(word => (word.Glyph ? "glyph" : $"\"{word.Text}\"", word.Box)));
                break;
            }
            case MediaIsland media:
            {
                var laid = media.LineOf(box, style);
                Add("art", laid.Art);
                foreach (var words in laid.Words)
                {
                    Add($"\"{words.Text}\"", words.Box);
                }
                Add("previous", laid.Previous);
                Add("play", laid.Play);
                Add("next", laid.Next);
                Add("timeline", laid.Track);
                Add("time played", laid.Played);
                Add("length", laid.Length);
                break;
            }
            case PinTile pin:
            {
                var laid = pin.LineOf(box, style);
                Add("icon", laid.Icon);
                Add("running indicator", laid.Pill);
                if (laid.NameText is not null)
                {
                    Add("name", laid.Name);
                }
                foreach (var (under, index) in laid.Under.Select((under, index) => (under, index)))
                {
                    Add($"line {index + 2}", under.Box);
                }
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(line), line.Info.Id, "has no layout this test can read");
        }
        return parts;
    }

    /// <summary>The main reading a one-row line shows: where it stands and what it says.</summary>
    private static (D2D_RECT_F Box, string Text)? MainOf(CellIsland line, D2D_RECT_F box, in TileStyle style)
    {
        switch (line)
        {
            case SwitchIsland toggle:
            {
                // The switch says it, or the state in words, and the name says what of.
                var laid = toggle.LineOf(box, style);
                return laid.NameText is { } name ? (laid.Name, name) : laid.StateText.Length > 0 ? (laid.State, laid.StateText) : null;
            }
            case PinTile pin:
            {
                var laid = pin.LineOf(box, style);
                return laid.NameText is { } name ? (laid.Name, name) : null;
            }
            case MediaIsland media:
                return media.LineOf(box, style).Words is [var title, ..] ? (title.Box, title.Text) : null;
            case SoundPartTile sound:
                return SoundMain(sound, box, style);
            default:
                throw new ArgumentOutOfRangeException(nameof(line), line.Info.Id, "has no main reading this test can read");
        }
    }

    /// <summary>
    /// A sound line's main reading: the volume's level, the device in use's name, or an app's slider
    /// with its name.
    /// </summary>
    private static (D2D_RECT_F Box, string Text)? SoundMain(SoundPartTile sound, D2D_RECT_F box, in TileStyle style)
    {
        sound.LayOut(box, style);
        return sound.Region switch
        {
            SoundPart.Volume => sound.Placed.OfType<Karpo.Dock.QuickSettings.Controls.Label>().FirstOrDefault(label => int.TryParse(label.Text, out _)) is { } level ? (level.Bounds, level.Text) : null,
            SoundPart.Mixer => sound.Placed.OfType<Karpo.Dock.QuickSettings.Controls.Slider>().FirstOrDefault() is { } slider ? (slider.Bounds, slider.Name) : null,
            _ => sound.Words.FirstOrDefault(word => !word.Glyph) is { Text.Length: > 0 } name ? (name.Box, name.Text) : null,
        };
    }

    /// <summary>
    /// What <paramref name="line"/> says drawn into <paramref name="box"/>: each line of words and each
    /// glyph it draws, as heard where it is drawn (PaintWatch.Said), without an ellipsis or a leading
    /// " · ", and without a summary of what is not shown.
    /// </summary>
    private static List<string> SaidBy(CellIsland line, D2D_RECT_F box, in TileStyle style)
    {
        var said = new List<string>();
        var drawn = style;
        try
        {
            PaintWatch.Said = (text, _) => said.Add(text);
            Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => line.PaintCard(dc, box, drawn, WidgetSurface.List), _ => { });
        }
        finally
        {
            PaintWatch.Said = null;
        }
        return [.. said.Select(Plain).Where(text => text.Length > 0 && !Summary.IsMatch(text)).Distinct()];
    }

    /// <summary>What a piece says, without the ellipsis that cut it or the dot that joined it to the one before.</summary>
    private static string Plain(string text) => text.Trim().TrimEnd('…').TrimEnd().TrimStart('·').Trim();

    /// <summary>A line saying how many are not shown, which no line says any more (§461).</summary>
    private static readonly Regex Summary = new(@"^\d+ other ", RegexOptions.CultureInvariant);

    /// <summary>
    /// Whether <paramref name="now"/>, said by a taller line, says what <paramref name="before"/> did:
    /// the same words; one cut from the other where at least four letters of it still show; or the
    /// fuller form of a name a shorter line said short ("Mobile hotspot" for "Hotspot").
    /// </summary>
    private static bool Same(string before, string now) =>
        before == now
        || (Math.Min(before.Length, now.Length) >= 4
            && (before.StartsWith(now, StringComparison.Ordinal) || now.StartsWith(before, StringComparison.Ordinal) || now.Contains(before, StringComparison.OrdinalIgnoreCase)));

    // --- The lines, and what stands behind them -------------------------------------------------------

    /// <summary>The most rows a line of <paramref name="kind"/> may be given.</summary>
    private static int MostOf(ListKind kind) => ListWidgets.Of(kind).Range.Most;

    /// <summary>The scales and text sizes a line is held at: 100, 150 and 200 %, and the largest text at 100 %.</summary>
    private static IEnumerable<(float Scale, float TextScale)> Sizes() =>
        [.. Scales.Select(scale => (scale, 1f)), (1f, 2.25f)];

    private static TileStyle Style(float scale, float textScale, bool dark = true)
    {
        var look = ListFormPictures.LookFor(dark, textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    /// <summary>A line of <paramref name="rows"/> rows, as the folder gives it (ListFormPictures.Rows), at the origin.</summary>
    private static D2D_RECT_F Box(int rows, in TileStyle style) =>
        PopupPaint.Rect(0, 0, PopupPaint.Round(WidthDip * style.Scale), ListFormPictures.Rows(rows, style.Scale, style.TextScale));

    /// <summary><paramref name="local"/>, in a control's own px, on its parent's: where <paramref name="bounds"/> stands.</summary>
    private static D2D_RECT_F Shifted(D2D_RECT_F local, D2D_RECT_F bounds) =>
        new(bounds.left + local.left, bounds.top + local.top, bounds.left + local.right, bounds.top + local.bottom);

    /// <summary>How much two boxes overlap, in px on the shorter side of their overlap; 0 where they don't.</summary>
    private static float Overlap(D2D_RECT_F one, D2D_RECT_F other) =>
        MathF.Max(0, MathF.Min(MathF.Min(one.right, other.right) - MathF.Max(one.left, other.left), MathF.Min(one.bottom, other.bottom) - MathF.Max(one.top, other.top)));

    /// <summary>
    /// <paramref name="use"/> on the painting thread, with every line's stand-ins in place: sound from
    /// a made-up machine, a track that plays nowhere, and switches of each state — two whose click
    /// switches them, one off and one on, and Do not disturb on with what still gets through; one
    /// whose click opens a page, on; the rest as an isolated run has them, saying nothing of their
    /// state.
    /// </summary>
    private static void Drawn(Action<Gfx> use) =>
        Offscreen.OnStaThread(gfx =>
        {
            // Two minutes into a track of four and a half, which the app lets be sought.
            MediaIsland.Stand = new MediaStand(Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true);
            SoundParts.Made = () => new Machine();
            QuickSwitches.Source = which => which switch
            {
                QuickSwitch.AirplaneMode => new SampleSwitch("Airplane mode", on: false, note: "Turns Wi-Fi, Bluetooth and mobile broadband off and on.", switches: true),
                QuickSwitch.MobileHotspot => new SampleSwitch("Mobile hotspot", on: true, note: "Sharing this PC's connection as Karpo-7F3A.", switches: true),
                QuickSwitch.EnergySaver => new SampleSwitch("Energy saver", on: true, note: "Windows doesn't let Karpo switch energy saver here; a click opens its settings."),
                QuickSwitch.DoNotDisturb => new SampleSwitch("Do not disturb", on: true, note: "Only priority notifications and alarms show banners.", switches: true),
                // The radios' and the sound's toggles: Wi-Fi on, the microphone muted, its glyph struck through.
                QuickSwitch.WifiRadio => new SampleSwitch("Wi-Fi", on: true, note: null, switches: true),
                QuickSwitch.Microphone => new SampleSwitch("Microphone", on: false, note: null, switches: true, glyph: MuteSwitch.MicrophoneMuted),
                // Color filters on, game mode off: the toggles switched through Windows' Settings handlers.
                QuickSwitch.ColorFilters => new SampleSwitch("Color filters", on: true, note: null, switches: true),
                QuickSwitch.GameMode => new SampleSwitch("Game mode", on: false, note: null, switches: true),
                _ => null,
            };
            try
            {
                use(gfx);
            }
            finally
            {
                MediaIsland.Stand = null;
                SoundParts.Made = null;
                QuickSwitches.Source = null;
            }
        });

    /// <summary>A line of <paramref name="kind"/>, made the way a list folder makes it.</summary>
    private static CellIsland Line(Gfx gfx, ListKind kind)
    {
        var widget = ListFormPictures.Line(kind, island =>
        {
            if (kind == ListKind.Pin)
            {
                // A link drawn with a glyph of its own: nothing the shell or the network answers for.
                island.Pin = new PinOptions { Target = "https://example.com/notes", Name = "Notes", Icon = "" };
            }
        });
        var made = Karpo.Dock.Islands.CreateLine(Karpo.Core.Config.IslandKey(null, widget.Id), widget, kind, gfx, new Karpo.Core.Config());
        return Assert.IsAssignableFrom<CellIsland>(made);
    }

    /// <summary>
    /// <paramref name="kind"/>'s sheet: every height from one row to its most, side by side at 150 %,
    /// six to a row, each on the folder's panel with its box drawn faintly round it.
    /// </summary>
    private static void Sheet(Gfx gfx, ListKind kind, bool dark, string path, float scale = 1.5f)
    {
        var style = Style(scale, 1f, dark);
        var theme = style.Theme;
        var most = MostOf(kind);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(10 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var width = PopupPaint.Round(WidthDip * scale);
        const int across = 6;
        var bands = (most + across - 1) / across;
        var bandHeights = Enumerable.Range(0, bands).Select(band => ListFormPictures.Rows(Math.Min(most, (band * across) + across), scale, 1f)).ToList();
        var sheetWidth = pad + (Math.Min(most, across) * (width + pad));
        var sheetHeight = pad + bandHeights.Sum(height => labelLine + height + pad);
        var lines = Enumerable.Range(1, most).Select(_ => Line(gfx, kind)).ToList();
        try
        {
            Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                var y = pad;
                for (var band = 0; band < bands; band++)
                {
                    for (var slot = 0; slot < across && (band * across) + slot < most; slot++)
                    {
                        var rows = (band * across) + slot + 1;
                        var x = pad + (slot * (width + pad));
                        PopupPaint.Text(dc, gfx, rows == 1 ? "1 row" : $"{rows} rows", label, Weight.Regular, theme.TextSecondary, x, y);
                        var box = Box(rows, style);
                        PopupPaint.Stroke(dc, PopupPaint.Rect(x, y + labelLine, width, TilePaint.Height(box)), 0, 1, PopupPaint.Ink(theme.CardStroke));
                        var line = lines[rows - 1];
                        Offscreen.Shifted(dc, x, y + labelLine, () => line.PaintOn(dc, box, style, WidgetSurface.List));
                    }
                    y += labelLine + bandHeights[band] + pad;
                }
            }, bitmap => Offscreen.Save(bitmap, path));
        }
        finally
        {
            foreach (var line in lines)
            {
                line.Dispose();
            }
        }
    }

    /// <summary>A name longer than any row has room for: a device a driver named at length.</summary>
    private const string Long = "Microphone Array (Intel® Smart Sound Technology for Digital Microphones and Headsets)";

    /// <summary>
    /// A made-up machine: three outputs, two microphones and four apps, and nothing set anywhere; with
    /// <paramref name="named"/>, the output and the microphone in use both called that.
    /// </summary>
    private sealed class Machine(string? named = null) : ISoundSource
    {
        private static readonly string Mic = char.ConvertFromUtf32(0xE720);

        public void Start(Changes changes)
        {
        }

        public SoundState Read(int news) => new(
            true,
            0.45f,
            false,
            [
                new("speakers", named ?? "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true),
                new("headphones", "Headphones (WH-1000XM5)", WindowsSound.GlyphFor(3), false),
                new("display", "LU28R55 (NVIDIA High Definition Audio)", WindowsSound.GlyphFor(9), false),
            ],
            [
                new("system", "System sounds", 1f, false, true, null, null),
                new("music", "Music", 0.8f, false, false, null, null),
                new("call", "Call", 0.6f, true, false, null, null),
                new("game", "Game", 0.35f, false, false, null, null),
            ],
            [
                new("array", named ?? "Microphone Array (Realtek(R) Audio)", Mic, true),
                new("headset", "Headset (WH-1000XM5 Hands-Free)", Mic, false),
            ],
            false);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted)
        {
        }

        public void SetDefault(string id)
        {
        }

        public void SetDefaultInput(string id)
        {
        }

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }
}
