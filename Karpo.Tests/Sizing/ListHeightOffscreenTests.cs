// A list folder's lines at Automatic, Automatic up to a most, and Fixed — drawn, and measured.
//
// The rule is the bar's, measured down a list instead of along a bar: a line is what its content
// works out to, up to its most, or exactly its rows where it is fixed (docs\widgets.md §76). These sheets put the
// three things a person can say side by side with the real widgets drawing into the real plan,
// and every line's height is asserted in pixels as well as drawn, so the picture and the numbers
// under it cannot disagree.
//
// The contents are fixed so two runs can be compared: notifications are made up
// (NotificationPartTile.Shown) and the weather is the saved forecast at a fixed time. Each panel
// is captioned with what it was asked for and the heights its lines came to.
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the sheets go to KARPO_PNG_DIR. Nothing
// is shown on the person's desktop.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sizing;

public sealed unsafe class ListHeightOffscreenTests(ITestOutputHelper output)
{
    private const string Owner = "layout:default";

    private const float Scale = 1.5f;

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>One row is a line of the list; n rows are n folder cells and the gaps between them, in px.</summary>
    private static float Rows(int rows, float scale, float textScale = 1) => rows * LineHeight.Row * FolderFlyout.CellPx(scale, textScale);

    /// <summary>
    /// The notifications list, the component whose content is a count, in the three states: on
    /// Automatic with three notifications, which is its content and nothing more; Automatic up to six
    /// rows with eight, which is its most and a summary of the rest; and Fixed at four rows with
    /// three, which is four rows whatever the content is.
    /// </summary>
    [UiFact]
    public void ANotificationsListAtAutomaticInARangeAndFixed()
    {
        OnStaThread(gfx =>
        {
            var was = Clock.Now;
            var at = new DateTime(2026, 9, 22, 18, 0, 0);
            Clock.Now = () => at;
            try
            {
                var look = LookFor(dark: true);
                var cases = new (string Name, LineHeight? Height, int Count)[]
                {
                    ("automatic", null, 3),
                    ("automatic up to 6 rows", LineHeight.Automatic(12), 8),
                    ("fixed 4 rows", LineHeight.Exactly(8), 3),
                };
                var panels = new List<(string Caption, FolderIsland Folder, FolderPlan Plan)>();
                try
                {
                    foreach (var (name, height, count) in cases)
                    {
                        var island = Folders.PanelFolders.Notifications();
                        var list = island.Folder!.Cells.Single(cell => cell.Island.List == ListKind.NotificationsList).Island;
                        list.Height = height;
                        list.Normalize();
                        var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
                        var entries = Made(at, count);
                        foreach (var tile in folder.Tiles)
                        {
                            if (tile.Island is NotificationPartTile card)
                            {
                                card.Shown = entries;
                            }
                        }
                        folder.Opened(gfx);
                        var natural = FolderFlyout.Natural(folder.Tiles, folder.Options, Scale, look);
                        var plan = FolderFlyout.Build(look, Scale, folder.View, folder.Options, [.. folder.Tiles.Select(tile => tile.Cell)], 1600, natural: natural);

                        var index = Index(folder, ListKind.NotificationsList);
                        var content = natural[index];
                        var given = plan.Cards[index].bottom - plan.Cards[index].top;
                        var cell = FolderFlyout.CellPx(Scale, 1);
                        var expected = height is { } held ? held.Given(content, cell) : cell * LineHeight.CellsFor(content, cell);
                        Assert.Equal(expected, given, 1);
                        output.WriteLine($"{name}: {count} notifications measure {content:0} px, the line is given {given:0} px");
                        panels.Add(($"{name} — {count} notifications: {content:0} px of content, line {given:0} px", folder, plan));
                    }

                    float Line((string Caption, FolderIsland Folder, FolderPlan Plan) panel)
                    {
                        var at = Index(panel.Folder, ListKind.NotificationsList);
                        return panel.Plan.Cards[at].bottom - panel.Plan.Cards[at].top;
                    }
                    // The three are different on purpose, or the sheet shows one thing three times.
                    Assert.Equal(3, panels.Select(Line).Distinct().Count());
                    // The most is where eight notifications stop: six rows, and a summary.
                    Assert.Equal(Rows(6, Scale), Line(panels[1]), 1);
                    // Fixed is fixed, three notifications or not.
                    Assert.Equal(Rows(4, Scale), Line(panels[2]), 1);

                    Sheet(gfx, look, "list-height-notifications.png", panels);
                }
                finally
                {
                    foreach (var (_, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }

    /// <summary>
    /// The weather folder on Automatic, before and after: its conditions at the rows their kind was
    /// declared at, and at their own content. (The Today curve, four rows around a 125-DIP graph —
    /// the dead space under it that the person called "way too much spacing on the bottom" — and
    /// the week left lists as widgets on 2026-09-26.)
    /// </summary>
    [UiFact]
    public void TheWeatherFolderIsItsContent()
    {
        OnStaThread(gfx => Forecasts.SavedWeather.AtAFixedTime(() =>
        {
            var look = LookFor(dark: true);
            var island = Folders.PanelFolders.Weather();
            island.Id = "wf";
            // Each row carries its own place.
            foreach (var cell in island.Folder!.Cells)
            {
                cell.Island.Weather = Forecasts.SavedWeather.Options();
            }
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, Core.Configs.With(island));
            var source = Forecasts.SavedWeather.Source(Forecasts.SavedWeather.Cache(Clock.Now().ToUniversalTime().AddMinutes(-15)));
            foreach (var tile in folder.Tiles)
            {
                if (tile.Island is Karpo.Dock.Weather.WeatherPartTile card)
                {
                    card.Source = source;
                }
            }
            folder.Opened(gfx);
            try
            {
                var cells = folder.Tiles.Select(tile => tile.Cell).ToList();
                var before = FolderFlyout.Build(look, Scale, folder.View, folder.Options, cells, 1600);
                var natural = FolderFlyout.Natural(folder.Tiles, folder.Options, Scale, look);
                var after = FolderFlyout.Build(look, Scale, folder.View, folder.Options, cells, 1600, natural: natural);

                var lines = new List<string>();
                for (var index = 0; index < folder.Tiles.Count; index++)
                {
                    var kind = folder.Tiles[index].Cell.Island.List!.Value;
                    var was = before.Cards[index].bottom - before.Cards[index].top;
                    var now = after.Cards[index].bottom - after.Cards[index].top;
                    // Every line is its content — measured, not guessed — and never more than its
                    // kind permits; these kinds' content cannot outgrow their range at 100 % text,
                    // which is the claim the registry makes for them.
                    Assert.True(natural[index] > 0, $"{kind} measured nothing");
                    Assert.True(natural[index] <= Rows(kind.ListRange().Most, Scale) + 0.5f, $"{kind}'s content is {natural[index]} px, over its {kind.ListRange().Most} rows");
                    // The weather's pieces do not vary, so they are fixed at their rows (§138): the
                    // line is its height for that content, its rows where it is fixed.
                    Assert.Equal(LineHeight.For(folder.Tiles[index].Cell.Island).Given(natural[index], FolderFlyout.CellPx(Scale, 1)), now, 1);
                    output.WriteLine($"{kind}: declared {was:0} px, content {natural[index]:0} px, given {now:0} px");
                    lines.Add($"{kind.ToString().Replace("Weather", "")} {was:0}→{now:0}");
                }
                Sheet(gfx, look, "list-height-weather.png",
                [
                    ("before — the rows each kind declared", folder, before),
                    ($"after — each card its content: {string.Join(", ", lines)} px", folder, after),
                ]);
            }
            finally
            {
                folder.Closed();
            }
        }));
    }

    private static int Index(FolderIsland folder, ListKind kind)
    {
        for (var index = 0; index < folder.Tiles.Count; index++)
        {
            if (folder.Tiles[index].Cell.Island.List == kind)
            {
                return index;
            }
        }
        throw new InvalidOperationException($"no {kind} in the folder");
    }

    private static List<NotificationEntry> Made(DateTime at, int count)
    {
        var said = new[]
        {
            ("Mail", "Vera Holm", "Re: the roof — I've asked them for a second quote, and they say Thursday at the earliest."),
            ("Calendar", "Standup in 10 minutes", "Daily · Team room"),
            ("Messages", "Dad", "Landed! Getting a coffee then I'll find the taxi rank."),
            ("Photos", "3 years ago today", "A morning at the lake."),
            ("Mail", "Nils Ahlberg", "Invoice 2214 attached."),
            ("Updates", "Restart to finish updating", "Karpo will reopen where you left it."),
            ("Messages", "Ida", "are you coming tonight?"),
            ("Mail", "Building management", "Water off 09:00–12:00 on Tuesday."),
        };
        return [.. said.Take(count).Select((line, index) => new NotificationEntry(
            index + 1, line.Item1.ToLowerInvariant(), line.Item1, null, line.Item2, line.Item3, at.AddMinutes(-7 * index * index), null))];
    }

    /// <summary>
    /// The panels side by side, each under a caption, on the same backdrop and in the same rounded
    /// clip the real panel has — drawn by the product's own painters (FolderFlyout.PaintEmpty for
    /// the panel, PaintContent and PaintListLine for the lines), the same calls the open
    /// folder makes, so the sheet cannot be a folder nobody will see.
    /// </summary>
    private static void Sheet(Gfx gfx, Look look, string file, IReadOnlyList<(string Caption, FolderIsland Folder, FolderPlan Plan)> panels)
        => Sheet(gfx, look, file, [.. panels.Select(panel => (Split(panel.Caption), panel.Folder, panel.Plan))]);

    /// <summary>A caption's two lines: what was asked, and what it came to.</summary>
    private static (string First, string Second) Split(string caption)
    {
        var colon = caption.IndexOf(": ", StringComparison.Ordinal);
        return colon < 0 ? (caption, "") : (caption[..colon], caption[(colon + 2)..]);
    }

    private static void Sheet(Gfx gfx, Look look, string file, IReadOnlyList<((string First, string Second) Caption, FolderIsland Folder, FolderPlan Plan)> panels)
    {
        var theme = FlyoutTheme.For(look);
        const float margin = 24;
        var caption = PopupPaint.Round(14 * Scale);
        var captionBand = caption * 3;
        var width = margin + panels.Sum(panel => panel.Plan.Width + margin);
        var height = margin + captionBand + panels.Max(panel => panel.Plan.Height) + margin;
        Bitmap(width, height, dc =>
        {
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var ink = theme.Dark ? new DXGI_RGBA(1, 1, 1, 0.9f) : new DXGI_RGBA(0, 0, 0, 0.9f);
            var left = margin;
            foreach (var (words, folder, plan) in panels)
            {
                PopupPaint.Text(dc, gfx, words.First, caption, Weight.Semibold, ink, left, margin, plan.Width);
                PopupPaint.Text(dc, gfx, words.Second, caption, Weight.Regular, ink, left, margin + (caption * 1.4f), plan.Width);
                var box = new D2D_RECT_F(left, margin + captionBand, left + plan.Width, margin + captionBand + plan.Height);
                PopupPaint.Fill(dc, box, 8 * Scale, PopupPaint.Ink(theme.Base));
                PopupPaint.Stroke(dc, box, 8 * Scale, MathF.Max(1, MathF.Round(Scale)), PopupPaint.Ink(theme.Stroke));
                var context = dc;
                var at = (box.left, box.top);
                PopupPaint.Clipped(context, box, 8 * Scale, () => Shifted(context, at.left, at.top, () => Panel(context, gfx, folder, plan, look, theme)));
                left += plan.Width + margin;
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }

    private static void Panel(ID2D1DeviceContext* dc, Gfx gfx, FolderIsland folder, FolderPlan plan, Look look, FlyoutTheme theme)
    {
        FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
        for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
        {
            if (!plan.Shows(index))
            {
                continue;
            }
            var card = plan.Cards[index];
            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
            var style = FolderFlyout.StyleFor(Scale, look, theme);
            var tile = folder.Tiles[index];
            var context = dc;
            Shifted(context, card.left, card.top, () =>
            {
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(context, gfx, tile.Island, tile.Cell.Island, box, plan, theme, Scale);
                }
                else
                {
                    FolderFlyout.PaintContent(context, tile.Island, box, style);
                }
            });
        }
    }
}
