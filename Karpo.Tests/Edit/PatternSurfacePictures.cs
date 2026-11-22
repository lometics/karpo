// A pattern, and its colour, show on the surface they are set on and nowhere else (docs\widgets.md §513; the person,
// 2026-11-05: "you should be able to set a background pattern for the folder widgets as well and also for the taskbar
// widgets. the background pattern color should also be able to be set."). Five places a pattern is set — the layout's
// Taskbar widgets, its Widgets in folders, a bar widget's own look, a folder face's own look, a line's own look — and the
// Taskbar's, each drawn with nothing set, with the pattern, and with the pattern's colour over it: the pixels inside every
// card of the bar and every line of an open folder read, with no background set (the pattern on the card's rectangle, over
// its plate at rest) and over one. A pattern changes exactly its surfaces' insides; a pattern colour changes their ink to
// it (the red of the colour shows) and nothing else; a pattern colour with no pattern changes nothing.
// To KARPO_PNG_DIR as pattern-surface-<bar>-<setting>.png and pattern-lines-<setting>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Reflection;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The folder whose face's own look the bar's table sets: the clock's (s5).</summary>
    private const int FacePart = 4;

    /// <summary>The red a pattern colour is set to: its ink shows red where the pattern is.</summary>
    private const string PatternRed = "#CCFF1744";

    /// <summary>
    /// One setting of the patterns' table: what is done to the config over <paramref name="Against"/>'s (another setting's
    /// name, its picture what this one is compared with; null for nothing set), the cards (by index into
    /// <see cref="SurfaceParts"/>, or the folder's lines) whose insides must change — <paramref name="Red"/>: to the pattern
    /// colour's red — and those that must not, and whether the bar's room changes (else it stays, and the gaps between the cards too).
    /// </summary>
    private sealed record PatternSetting(string Name, string? Against, Action<Config> Set, int[] Changes, int[] Keeps, bool Red = false, bool Ground = false);

    /// <summary><paramref name="setting"/> of <paramref name="table"/> done to <paramref name="config"/>, over what it is set against.</summary>
    private static void Setting(PatternSetting[] table, PatternSetting setting, Config config)
    {
        if (setting.Against is { } under)
        {
            Setting(table, table.Single(each => each.Name == under), config);
        }
        setting.Set(config);
    }

    private static int[] AllBut(int count, params int[] but) => [.. Enumerable.Range(0, count).Where(index => !but.Contains(index))];

    private static readonly int[] EveryCard = [.. Enumerable.Range(0, SurfaceParts.Length)];


    private static readonly PatternSetting[] BarPatterns =
    [
        // The Taskbar widgets' pattern with no background: inside every card, on its rectangle.
        new("widgets-pattern", null, config => Section(config, LookPart.Widgets, look => look.Pattern = Pattern.Grid), EveryCard, []),
        new("widgets-pattern-colour", "widgets-pattern", config => Section(config, LookPart.Widgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), EveryCard, [], Red: true),
        // A widget's own, a folder's face's own: that card alone.
        // Over a background: the cards' own.
        new("widgets-background", null, config => Section(config, LookPart.Widgets, look => look.Background = LookShorthand.Pair("#37474F")), EveryCard, []),
        new("widgets-background-pattern", "widgets-background", config => Section(config, LookPart.Widgets, look => look.Pattern = Pattern.Grid), EveryCard, []),
        new("widgets-background-pattern-colour", "widgets-background-pattern", config => Section(config, LookPart.Widgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), EveryCard, [], Red: true),
        // The person's cards: background None, outlined in white, on Contours (their config, 2026-11-05).
        new("widgets-none", null, config =>
            {
                Section(config, LookPart.Taskbar, look =>
                {
                    look.Outline = LookShorthand.Pair("none");
                    look.Pattern = Pattern.Contours;
                });
                Section(config, LookPart.Widgets, look =>
                {
                    look.Background = LookShorthand.Pair("none");
                    look.Outline = LookShorthand.Pair("#FFFFFF");
                });
            }, [], [], Ground: true),
        new("widgets-none-pattern", "widgets-none", config => Section(config, LookPart.Widgets, look => look.Pattern = Pattern.Grid), EveryCard, []),
        new("widgets-none-pattern-colour", "widgets-none-pattern", config => Section(config, LookPart.Widgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), EveryCard, [], Red: true),
        // The Taskbar's colour, the cards on a background over it: the bar's room alone.
        new("taskbar-pattern", "widgets-background", config => Section(config, LookPart.Taskbar, look => look.Pattern = Pattern.Grid), [], EveryCard, Ground: true),
        new("taskbar-pattern-colour", "taskbar-pattern", config => Section(config, LookPart.Taskbar, look => look.PatternColor = LookShorthand.Pair(PatternRed)), [], EveryCard, Red: true, Ground: true),
        // The Widgets in folders' pattern and colour: nothing on the bar. A colour with no pattern: nothing.
        new("folder-widgets-pattern-colour", null, config => Section(config, LookPart.FolderWidgets, look =>
            {
                look.Pattern = Pattern.Grid;
                look.PatternColor = LookShorthand.Pair(PatternRed);
            }), [], EveryCard),
        new("widgets-colour-alone", null, config => Section(config, LookPart.Widgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), [], EveryCard),
    ];

    /// <summary>The bars the patterns' table is drawn on: docked and floating, without transparency and with it.</summary>
    private static readonly (SurfaceKind Kind, bool Glass)[] PatternKinds = [(SurfaceKinds[0], false), (SurfaceKinds[2], false), (SurfaceKinds[4], false), (SurfaceKinds[0], true)];

    /// <summary>
    /// §513: a pattern set on the Taskbar widgets, a widget's own look or a folder's face's own look shows inside exactly those
    /// cards, with no background set and over one; its colour changes its ink there alone; the Taskbar's colour the bar's room
    /// alone; the Widgets in folders' nothing on the bar; a colour with no pattern nothing.
    /// </summary>
    [UiFact]
    public void APatternAndItsColourShowOnTheCardsTheyAreSetOn()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("pattern-surface", (gfx, instance) =>
        {
            Clock.Now = () => new DateTime(2026, 11, 5, 9, 28, 0);
            WindowsClock.SecondsStand = false;
            try
            {
                foreach (var (kind, glass) in PatternKinds)
                {
                    var name = kind.Name + (glass ? "-glass" : "");
                    var shots = new Dictionary<string, SurfaceShot> { [""] = ShootSurface(gfx, kind, _ => { }, $"pattern-surface-{name}-none.png", null, glass) };
                    foreach (var setting in BarPatterns)
                    {
                        var shot = shots[setting.Name] = ShootSurface(gfx, kind, config => Setting(BarPatterns, setting, config), $"pattern-surface-{name}-{setting.Name}.png", null, glass);
                        var before = shots[setting.Against ?? ""];
                        var what = $"{name}, {setting.Name}";
                        foreach (var index in setting.Changes)
                        {
                            var (moved, room, red) = Inside(before, shot, SurfacePlate(before.Layout, Part(before.Layout, SurfaceParts[index])));
                            if (moved < Math.Max(6, room / 40))
                            {
                                failures.Add($"{what}: {SurfaceParts[index]}'s inside did not change ({moved} of {room} px)");
                            }
                            else if (setting.Red && red < moved / 4)
                            {
                                failures.Add($"{what}: {SurfaceParts[index]}'s inside changed ({moved} px) but not to the pattern's red ({red} px)");
                            }
                        }
                        foreach (var index in setting.Keeps)
                        {
                            var (moved, room, _) = Inside(before, shot, SurfacePlate(before.Layout, Part(before.Layout, SurfaceParts[index])));
                            if (moved > 2)
                            {
                                failures.Add($"{what}: {SurfaceParts[index]}'s inside changed ({moved} of {room} px)");
                            }
                        }
                        // The bar between the cards and its room: the Taskbar's alone.
                        foreach (var (where, moved) in Compare(before, shot, Seen.Ground))
                        {
                            if (setting.Ground ? moved <= ChangedApart : moved > KeptApart)
                            {
                                failures.Add($"{what}: {where} {(setting.Ground ? "did not change" : "changed")} ({moved})");
                            }
                        }
                        foreach (var (where, moved) in setting.Ground ? [] : Compare(before, shot, Seen.Gaps))
                        {
                            if (moved > KeptApart)
                            {
                                failures.Add($"{what}: {where} changed ({moved})");
                            }
                        }
                    }
                }
                // The person's bar with patterns: Contours on it, the cards None outlined in white, dotted in orange.
                _ = ShootSurface(gfx, SurfaceKinds[0], config =>
                {
                    Setting(BarPatterns, BarPatterns.Single(each => each.Name == "widgets-none"), config);
                    Section(config, LookPart.Widgets, look =>
                    {
                        look.Pattern = Pattern.Dots;
                        look.PatternColor = LookShorthand.Pair("#B3F7630C");
                    });
                }, "pattern-surface-person-150.png", null, glass: true);
            }
            finally
            {
                WindowsClock.SecondsStand = null;
                Clock.Now = Clock.Real;
                gfx.Typeface = default;
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Inside <paramref name="card"/> (window px; past its rounded corners and its outline's band): how many pixels moved from
    /// <paramref name="before"/> to <paramref name="after"/>, of how many, and how many of those moved to red.
    /// </summary>
    private static (int Moved, int Room, int Red) Inside(SurfaceShot before, SurfaceShot after, RectF card)
    {
        var inset = (int)MathF.Ceiling(MathF.Max(before.Layout.CardRadius, before.Layout.SoftPx(4f)) * 0.4f) + (2 * (int)MathF.Ceiling(before.Layout.Stroke)) + 1;
        var (moved, room, red) = (0, 0, 0);
        for (var y = (int)MathF.Ceiling(card.Y) + inset; y < (int)card.Bottom - inset; y++)
        {
            for (var x = (int)MathF.Ceiling(card.X) + inset; x < (int)card.Right - inset; x++)
            {
                if (!before.Has(x, y) || !after.Has(x, y))
                {
                    continue;
                }
                room++;
                var (was, now) = (before.At(x, y), after.At(x, y));
                if (Apart(was, now) > KeptApart)
                {
                    moved++;
                    red += Reddened(was, now) ? 1 : 0;
                }
            }
        }
        return (moved, room, red);
    }

    /// <summary>Whether a pixel moved towards red: more red, less green and blue, against what it was.</summary>
    private static bool Reddened(Argb was, Argb now) => (now.R - was.R) - Math.Max(now.G - was.G, now.B - was.B) > 12;

    /// <summary>The open folder's lines the lines' table reads, by index into its lines: four switches and a battery.</summary>
    private static readonly ListKind[] PatternLines = [ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Battery];

    /// <summary>The line whose own look the lines' table sets.</summary>
    private const int OwnLine = 1;

    private static readonly int[] EveryLine = [.. Enumerable.Range(0, PatternLines.Length)];

    /// <summary>The folder of the lines' table, its lines <see cref="PatternLines"/>.</summary>
    private static BarItem PatternFolder(Config config) => config.DefaultLayout.Islands.Single(item => item.Id == "lines");


    private static readonly PatternSetting[] LinePatterns =
    [
        // The Widgets in folders' pattern, no background: inside every line.
        new("lines-pattern", null, config => Section(config, LookPart.FolderWidgets, look => look.Pattern = Pattern.Grid), EveryLine, []),
        new("lines-pattern-colour", "lines-pattern", config => Section(config, LookPart.FolderWidgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), EveryLine, [], Red: true),
        // A line's own: that line alone.
        // Over a background.
        new("lines-background", null, config => Section(config, LookPart.FolderWidgets, look => look.Background = LookShorthand.Pair("#37474F")), EveryLine, []),
        new("lines-background-pattern", "lines-background", config => Section(config, LookPart.FolderWidgets, look => look.Pattern = Pattern.Grid), EveryLine, []),
        new("lines-background-pattern-colour", "lines-background-pattern", config => Section(config, LookPart.FolderWidgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), EveryLine, [], Red: true),
        // The bar's patterns, the Taskbar widgets': nothing in the folder. A colour alone: nothing.
        new("bar-patterns", null, config =>
            {
                Section(config, LookPart.Taskbar, look => look.Pattern = Pattern.Grid);
                Section(config, LookPart.Widgets, look =>
                {
                    look.Pattern = Pattern.Grid;
                    look.PatternColor = LookShorthand.Pair(PatternRed);
                });
            }, [], EveryLine),
        new("lines-colour-alone", null, config => Section(config, LookPart.FolderWidgets, look => look.PatternColor = LookShorthand.Pair(PatternRed)), [], EveryLine),
    ];

    /// <summary>
    /// §513: a pattern set on the Widgets in folders or a line's own look shows inside exactly those lines of an open folder,
    /// with no background set and over one; its colour changes its ink there alone; the bar's and the face's nothing in it.
    /// </summary>
    [UiFact]
    public void APatternAndItsColourShowOnTheLinesTheyAreSetOn()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("pattern-lines", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1700, y = 1500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var glass in new[] { false, true })
            {
                var shots = new Dictionary<string, (byte[] Pixels, int Width, List<RECT> Lines)>();
                (byte[], int, List<RECT>) Shoot(string name, Action<Config> set)
                {
                    var island = BarKind.FolderList.New();
                    island.Id = "lines";
                    foreach (var kind in PatternLines)
                    {
                        island.Folder!.Put(kind.New());
                    }
                    var config = new Config();
                    config.DefaultLayout.Islands = [island];
                    set(config);
                    var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                    try
                    {
                        var look = (TestLooks.LookFor(dark: true) with { Glass = glass }).Wearing(config) with { Animations = false };
                        flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.End);
                        WindowShots.Settle(300);
                        byte[] pixels = [];
                        var width = 0;
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"pattern-lines-{(glass ? "glass-" : "")}{name}.png", default, (seen, w) => (pixels, width) = (seen, w)));
                        var plan = (FolderPlan)typeof(FolderFlyout).GetField("plan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                        var box = WindowShots.Extent(flyout.Hwnd);
                        var room = (int)(((box.right - box.left) - plan.Width) / 2);
                        var lines = plan.Cards.Take(PatternLines.Length).Select(card => new RECT
                        {
                            left = room + (int)MathF.Round(card.left),
                            top = room + (int)MathF.Round(card.top),
                            right = room + (int)MathF.Round(card.right),
                            bottom = room + (int)MathF.Round(card.bottom),
                        }).ToList();
                        flyout.Hide();
                        return (pixels, width, lines);
                    }
                    finally
                    {
                        folder.Dispose();
                    }
                }
                    shots[""] = Shoot("none", _ => { });
                foreach (var setting in LinePatterns)
                {
                    var shot = shots[setting.Name] = Shoot(setting.Name, config => Setting(LinePatterns, setting, config));
                    var before = shots[setting.Against ?? ""];
                    (int Moved, int Room, int Red) Line(int index)
                    {
                        var rect = before.Lines[index];
                        // Inside the line: a few px in from its edges and the seams, past the section's rounded corners.
                        var inset = 6;
                        var (moved, room, red) = (0, 0, 0);
                        for (var y = rect.top + inset; y < rect.bottom - inset; y++)
                        {
                            for (var x = rect.left + inset; x < rect.right - inset; x++)
                            {
                                Argb At((byte[] Pixels, int Width, List<RECT> Lines) one)
                                {
                                    var at = ((y * one.Width) + x) * 4;
                                    return new Argb(0xFF, one.Pixels[at + 2], one.Pixels[at + 1], one.Pixels[at]);
                                }
                                room++;
                                var (was, now) = (At(before), At(shot));
                                if (Apart(was, now) > KeptApart)
                                {
                                    moved++;
                                    red += Reddened(was, now) ? 1 : 0;
                                }
                            }
                        }
                        return (moved, room, red);
                    }
                    foreach (var index in setting.Changes)
                    {
                        var (moved, room, red) = Line(index);
                        if (moved < Math.Max(6, room / 40))
                        {
                            failures.Add($"{(glass ? "glass, " : "")}{setting.Name}: line {index} ({PatternLines[index]})'s inside did not change ({moved} of {room} px)");
                        }
                        else if (setting.Red && red < moved / 4)
                        {
                            failures.Add($"{(glass ? "glass, " : "")}{setting.Name}: line {index} ({PatternLines[index]})'s inside changed ({moved} px) but not to the pattern's red ({red} px)");
                        }
                    }
                    foreach (var index in setting.Keeps)
                    {
                        var (moved, room, _) = Line(index);
                        if (moved > 2)
                        {
                            failures.Add($"{(glass ? "glass, " : "")}{setting.Name}: line {index} ({PatternLines[index]})'s inside changed ({moved} of {room} px)");
                        }
                    }
                }
                // The lines with patterns: Waves in teal on every line.
                _ = Shoot("person-150", config =>
                {
                    Section(config, LookPart.FolderWidgets, look =>
                    {
                        look.Pattern = Pattern.Waves;
                        look.PatternColor = LookShorthand.Pair("#9900B7C3");
                    });
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
