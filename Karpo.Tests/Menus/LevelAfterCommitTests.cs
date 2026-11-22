// What is set in a level leaves the person on the level it belongs to (docs\widgets.md §365; MenuKeeps.Again,
// Flyout.LevelsAgain). The person, 2026-10-26: "when entering a new name for the layout and clicking enter i am
// not brought back to the previous level but i am brought back to the root level of the configuration. this
// should not be. fix this generally if this occurs in other places too. also fix this for other field types as
// well." A menu shown again after a change finds its open levels by the rows they opened from; a row's words
// may be what was just changed (a layout's name, a clock face's label), so it is found by its id where its
// words are new. Held here, on real menus (KARPO_UI_TESTS=1):
//
//  - **The layout named** in a layout's level › Name, Enter: back on that layout's level, under its new
//    name, its Name row lit — the dock's own loop.
//  - **Every kind of field**, committed two levels deep under rows whose words change with every change made: a
//    line of text by Enter (one back), by its level's head (one back) and by Up (its level stays); a number
//    typed (one back) and stepped; a switch; a choice, a colour, an icon, a time and a search's pick (their
//    level stays); a segment; an action that stays; a thing renamed by its pencil (one back, its row lit); a
//    thing named in its own level (one back, under its new name).

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using Karpo.Tests.Present;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class LevelAfterCommitTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    // --- The layout named ------------------------------------------------------------------------------

    /// <summary>
    /// The person's case, on Display ▾'s own rows read from a real config and read again after the name is
    /// kept, as the dock does (Dock.LooksKept, MenuAgain): Work's level, its Name's, "Office" typed, Enter —
    /// back on the layout's level, now "Office", its Name row lit, not on the menu's own rows.
    /// </summary>
    [UiFact]
    public void ALayoutNamedInDisplayMenuIsBackOnItsLevel()
    {
        WindowShots.OnHiddenDesktop("level-layout-named", (gfx, instance) =>
        {
            var config = new Config();
            config.EditScreen("\\\\.\\DISPLAY1", "One");
            _ = BarLooks.Add(config, "Work");
            var bar = new BarSpot("\\\\.\\DISPLAY1", "One");
            List<OptionPick> picks = [];
            List<FlyoutItem> Items() => TheDock.LooksItems(config, bar, dark: true, picks, scope: LooksScope.Layouts);
            using var menu = Show(gfx, instance, Items());
            var work = menu.ShownItems.Single(item => item.Text == "Work");
            Assert.Equal(default, menu.Click(work.Id));
            Assert.Equal((1, "Work"), (menu.InlineDepth, menu.InlineTitle));
            var name = work.Children!.Single(item => item.Text == S.SettingsLayoutsNameHeader);
            _ = menu.Click(name.Id);
            Assert.Equal(2, menu.InlineDepth);
            Type(menu, "Office");
            _ = Key(menu, VK.VK_RETURN);
            foreach (var said in Pump(menu).Where(said => said.Kind == FlyoutEventKind.Adjust))
            {
                // The dock's loop: what the field kept set through its row, the rows read again, shown again.
                var pick = OptionMenu.PickOf(picks, said.Id)!;
                Assert.True(FieldPicks.KeepRow(OptionRows.Find(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.Layouts), pick.Key), menu.TakeFieldCommit(said.Id)!));
                picks.Clear();
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, Items()));
            }
            Assert.Equal("Office", config.Layouts[1].Name);
            Assert.Equal((1, "Office"), (menu.InlineDepth, menu.InlineTitle));
            Assert.Equal(S.SettingsLayoutsNameHeader, menu.RowsShown.Single(row => row.Id == menu.HotId).Text);
        });
    }

    /// <summary>The real dock driven by a thread on its desktop, as a person's keys would (ArrangedFolderStandsTests' way).</summary>
    internal sealed class OnTheDock(InputReachesTheBarsTests.StageDock dock)
    {
        /// <summary>
        /// A real dock (Karpo.exe --stage) on a desktop of its own with a config of its own, named after
        /// <paramref name="name"/>, driven by <paramref name="act"/> from a thread on that desktop; <paramref name="config"/>
        /// its config where given, else a new one.
        /// </summary>
        public static void Run(string name, Action<OnTheDock> act, Config? config = null)
        {
            var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", name + "-" + Guid.NewGuid().ToString("N")[..12]);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "config.json"), (config ?? new Config()).ToJson());
            var desktopName = "karpo-" + name + "-" + Guid.NewGuid().ToString("N");
            HDESK desktop;
            fixed (char* chars = desktopName)
            {
                desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
            }
            Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
            var dock = InputReachesTheBarsTests.StageDock.Start(desktopName, folder);
            Exception? failed = null;
            try
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
                        _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                        act(new OnTheDock(dock));
                    }
                    catch (Exception error)
                    {
                        failed = error;
                    }
                });
                thread.Start();
                thread.Join();
            }
            finally
            {
                dock.Dispose();
                HiddenDesktop.Close(desktop);
            }
            if (failed is not null)
            {
                throw new Xunit.Sdk.XunitException("On the dock's desktop: " + failed);
            }
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>Edit mode, as a second start asks for it: §520 the edit bar's settings panel open on the Karpo page.</summary>
        internal void EnterEditMode()
        {
            Wait(() => File.Exists(Path.Combine(dock.Folder, "stage-map.json")), "the dock never mapped its bars");
            Pump(1500);
            Assert.True(Host() is { } host && PostMessageW(host, SecondStart.Message, 0, 0), "no dock host");
            Pump(1500);
            Wait(() => Deck("Nav:") is not null, "the settings panel did not open");
        }

        /// <summary>Whether the dock still runs.</summary>
        internal bool Running => dock.Running;

        /// <summary>Its config's folder: config.json, the stage's maps.</summary>
        internal string Folder => dock.Folder;

        /// <summary>The layouts' names as the file has them now.</summary>
        internal string[] Layouts()
        {
            try
            {
                return [.. JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!["layouts"]!.AsArray().Select(layout => (string)layout!["name"]!)];
            }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
            {
                return [];
            }
        }

        /// <summary>§520: the middle of the edit bar's or its panel's first thing whose stage key starts <paramref name="key"/> ("Fold:", "Nav:", a control's kind and row) (stage-edit.json); null before it shows.</summary>
        internal POINT? Deck(string key)
        {
            try
            {
                var path = Path.Combine(dock.Folder, "stage-edit.json");
                return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path))!.AsArray().FirstOrDefault(spot => (string)spot!["hit"]! == "Deck" && ((string)spot!["key"]!).StartsWith(key, StringComparison.Ordinal)) is { } deck
                    ? new POINT { x = (int)deck["x"]! + ((int)deck["w"]! / 2), y = (int)deck["y"]! + ((int)deck["h"]! / 2) }
                    : null;
            }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>The menu open now (stage-z.json, its stratum), none for none.</summary>
        internal HWND Menu()
        {
            try
            {
                var plan = JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "stage-z.json")))!.AsArray();
                return plan.FirstOrDefault(entry => (string)entry!["stratum"]! == nameof(Stratum.Menu)) is { } menu ? (HWND)(nint)(long)menu["hwnd"]! : HWND.NULL;
            }
            catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
            {
                return HWND.NULL;
            }
        }

        internal HWND? Host()
        {
            var at = HWND.NULL;
            fixed (char* name = DockHost.ClassName)
            {
                while ((at = FindWindowExW(HWND.NULL, at, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        /// <summary>A press and its release at <paramref name="at"/>, posted to the dock's window there.</summary>
        internal static void Click(POINT at)
        {
            var window = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
            RECT rect;
            _ = GetWindowRect(window, &rect);
            var where = (LPARAM)(nint)(((at.y - rect.top) << 16) | ((at.x - rect.left) & 0xFFFF));
            _ = PostMessageW(window, WM.WM_MOUSEMOVE, 0, where);
            _ = PostMessageW(window, WM.WM_LBUTTONDOWN, 0x0001, where);
            _ = PostMessageW(window, WM.WM_LBUTTONUP, 0, where);
            Pump(1000);
        }

        /// <summary>Keys pressed on the menu open, one by one.</summary>
        internal void Keys(params int[] keys)
        {
            foreach (var key in keys)
            {
                if (Menu() is var menu && menu != HWND.NULL)
                {
                    _ = PostMessageW(menu, WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);
                }
                Pump(300);
            }
            Pump(800);
        }

        internal void Wait(Func<bool> until, string why)
        {
            var end = Environment.TickCount64 + 30_000;
            while (!until())
            {
                Assert.True(dock.Running, "the dock ended");
                Assert.True(Environment.TickCount64 < end, why);
                Pump(100);
            }
        }

        internal static void Pump(int ms)
        {
            var until = Environment.TickCount64 + ms;
            while (Environment.TickCount64 < until)
            {
                _ = MsgWaitForMultipleObjects(0, null, BOOL.FALSE, (uint)Math.Max(1, until - Environment.TickCount64), QS.QS_ALLINPUT);
                MSG msg;
                while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                {
                    _ = TranslateMessage(&msg);
                    _ = DispatchMessageW(&msg);
                }
            }
        }
    }

    // --- Every kind of field -----------------------------------------------------------------------------

    /// <summary>
    /// A widget's settings two levels deep — "Outer" › "Inner" › a row of every kind — whose two rows' words
    /// say how many changes were made, so every change renames the levels it is made in; set as the dock sets
    /// them (Dock.MenuAdjusted: what a field kept through its row, else the pick through its row) and shown
    /// again after each (Dock.MenuAgain).
    /// </summary>
    private sealed class Model
    {
        public int Made;
        public bool On;
        public int Choice;
        public int Number = 5;
        public string Note = "Ann";
        public int Segment;
        public int Color;
        public string Glyph = "";
        public int Time = 450;
        public string Pencilled = "Groceries";
        public string Named = "Home";
        public readonly WidgetItem Clock = ListKind.Clock.New();
        public readonly List<OptionPick> Picks = [];

        public List<OptionRow> Rows() =>
        [
            new("outer", OptionRowKind.Level, $"Outer {Made}") { Level = [new("inner", OptionRowKind.Level, $"Inner {Made}") { Level = Fields() }] },
        ];

        private List<OptionRow> Fields() =>
        [
            new("toggle", OptionRowKind.Toggle, "Switch") { On = On, Set = on => Set(ref On, on == 1) },
            new("choice", OptionRowKind.Choice, "Choice") { Choices = ["A", "B", "C"], Chosen = Choice, Set = at => Set(ref Choice, at) },
            new("segments", OptionRowKind.Choice, "Align") { Choices = ["Start", "Middle", "End"], ChoiceGlyphs = [OptionGlyphs.ArrowLeft, OptionGlyphs.Monitor, OptionGlyphs.ArrowRight], Segmented = true, Chosen = Segment, Set = at => Set(ref Segment, at) },
            new("number", OptionRowKind.Number, "Number") { Value = Number, Least = 0, Most = 100, Set = value => Set(ref Number, value), SetText = typed => int.TryParse(typed, out var value) && Set(ref Number, value) },
            new("note", OptionRowKind.Text, "Note") { Text = Note, SetText = typed => Set(ref Note, typed) },
            new("color", OptionRowKind.Color, "Colour")
            {
                Tiles = [new("Automatic", Plain: true), new("Red", new Argb(255, 200, 30, 30)), new("Blue", new Argb(255, 30, 30, 200))],
                Chosen = Color,
                Set = at => Set(ref Color, at),
            },
            OptionPickers.Icons("glyph", Glyph, glyph => Set(ref Glyph, glyph)),
            new("time", OptionRowKind.Time, "Time") { Value = Time, Least = 0, Most = (24 * 60) - 1, Set = minutes => Set(ref Time, minutes) },
            OptionFields.OfClockZone(Clock, ["Tokyo Standard Time"]) with { Key = "zone" },
            new("act", OptionRowKind.Action, "Act") { Glyph = OptionGlyphs.Copy, Set = _ => Set(ref Made, Made) },
            new("things", OptionRowKind.Things, "Things")
            {
                Owned = new OptionOwned(null,
                [
                    new OptionThing(Pencilled, OptionGlyphs.Checklist) { Name = new("pencil", OptionRowKind.Text, "Rename") { Glyph = OptionGlyphs.Name, Text = Pencilled, SetText = typed => Set(ref Pencilled, typed) } },
                    new OptionThing(Named, OptionGlyphs.Pc) { Rows = [new("named", OptionRowKind.Text, "Name") { Text = Named, SetText = typed => Set(ref Named, typed) }] },
                ]),
            },
        ];

        // Every pick counts as a change, so the levels' words are new after each (an action's "change" too).
        private bool Set<T>(ref T field, T value)
        {
            field = value;
            Made++;
            return true;
        }

        /// <summary>What the menu said, done as the dock does it and the menu shown again.</summary>
        public void Answer(Flyout menu, IEnumerable<FlyoutEvent> said)
        {
            foreach (var adjust in said.Where(each => each.Kind == FlyoutEventKind.Adjust))
            {
                var pick = OptionMenu.PickOf(Picks, adjust.Id)!;
                var row = OptionRows.Find(Rows(), pick.Key);
                if (menu.TakeFieldCommit(adjust.Id) is { } kept)
                {
                    // The zone's row is the clock's own, which counts nothing.
                    Assert.True(FieldPicks.KeepRow(row, kept), $"{pick.Key} kept nothing");
                    Made += pick.Key == "zone" ? 1 : 0;
                }
                else
                {
                    Assert.True(OptionMenu.ApplyRow(row, pick), $"{pick.Key} changed nothing");
                }
                Picks.Clear();
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.Rows(Rows(), Picks)));
            }
        }
    }

    /// <summary>A kind of field committed on "Outer › Inner": how, and the level the person stands on after (its depth and the words it is under now).</summary>
    private sealed record Case(string Name, Action<Flyout, Model> Commit, int Depth, Func<Model, string> Title);

    private static FlyoutItem Row(Flyout menu, string text) => menu.RowsShown.First(row => row.Text == text);

    /// <summary>A click at <paramref name="at"/>, in window px: what the press and the release said (a stepper steps as it goes down).</summary>
    private static FlyoutEvent[] ClickAt(Flyout menu, POINT? at)
    {
        Assert.NotNull(at);
        var where = (LPARAM)(nint)((at.Value.y << 16) | (at.Value.x & 0xFFFF));
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, where);
        return [menu.Message(WM.WM_LBUTTONDOWN, 1, where), menu.Message(WM.WM_LBUTTONUP, 0, where)];
    }

    private static readonly Case[] Cases =
    [
        // A line of text's level: Enter keeps and goes back; so does its head; Up leaves the field, on its level.
        new("text, Enter", (menu, model) => { _ = menu.Click(Row(menu, "Note").Id); Type(menu, "Bob"); _ = Key(menu, VK.VK_RETURN); model.Answer(menu, Pump(menu)); }, 2, model => $"Inner {model.Made}"),
        new("text, its head", (menu, model) => { _ = menu.Click(Row(menu, "Note").Id); Type(menu, "Bob"); _ = menu.Click(Flyout.InlineBackId); model.Answer(menu, Pump(menu)); }, 2, model => $"Inner {model.Made}"),
        new("text, Up", (menu, model) => { _ = menu.Click(Row(menu, "Note").Id); Type(menu, "Bob"); _ = Key(menu, VK.VK_UP); model.Answer(menu, Pump(menu)); }, 3, _ => "Note"),
        // A number typed in its level, Enter; stepped in its row.
        new("number typed, Enter", (menu, model) => { _ = Key(menu, VK.VK_HOME); KeysTo(menu, Row(menu, "Number").Id); _ = Key(menu, VK.VK_RETURN); Assert.Equal(3, menu.InlineDepth); Type(menu, "42"); _ = Key(menu, VK.VK_RETURN); model.Answer(menu, Pump(menu)); }, 2, model => $"Inner {model.Made}"),
        new("number stepped", (menu, model) => model.Answer(menu, ClickAt(menu, menu.StepperButton(Row(menu, "Number").Id, more: true))), 2, model => $"Inner {model.Made}"),
        new("switch", (menu, model) => model.Answer(menu, [menu.Click(Row(menu, "Switch").Id)]), 2, model => $"Inner {model.Made}"),
        new("segment", (menu, model) => model.Answer(menu, ClickAt(menu, menu.ToolMiddle(Row(menu, "Align").Id, 2))), 2, model => $"Inner {model.Made}"),
        new("action that stays", (menu, model) => model.Answer(menu, [menu.Click(Row(menu, "Act").Id)]), 2, model => $"Inner {model.Made}"),
        // A pick in a level of one choice goes back to the level it opened from (§392; it stayed there, §219).
        new("choice", (menu, model) => { _ = menu.Click(Row(menu, "Choice").Id); model.Answer(menu, [menu.Click(Row(menu, "B").Id)]); }, 2, model => $"Inner {model.Made}"),
        new("colour", (menu, model) => { _ = menu.Click(Row(menu, "Colour").Id); model.Answer(menu, [menu.Click(Row(menu, "Blue").Id)]); }, 2, model => $"Inner {model.Made}"),
        new("icon", (menu, model) => { _ = menu.Click(Row(menu, S.SettingsFolderIconHeader).Id); model.Answer(menu, [menu.Click(menu.RowsShown.First(row => Flyout.IsTile(row) && row.Text != S.SettingsValueDefault("Folder")).Id)]); }, 2, model => $"Inner {model.Made}"),
        new("time", (menu, model) => { _ = menu.Click(Row(menu, "Time").Id); model.Answer(menu, ClickAt(menu, menu.StepperButton(Row(menu, S.DockEditHours).Id, more: true))); }, 3, _ => "Time"),
        new("search's pick", (menu, model) => { _ = menu.Click(Row(menu, S.SettingsWidgetClockZoneHeader).Id); Type(menu, "+9"); _ = Key(menu, VK.VK_DOWN); _ = Key(menu, VK.VK_RETURN); model.Answer(menu, Pump(menu)); }, 2, model => $"Inner {model.Made}"),
        // The person's own things: one renamed by its pencil, back on their level with its row lit; one named
        // in its own level, back on that level under its new name.
        new("thing's pencil, Enter", (menu, model) =>
        {
            _ = menu.Click(Row(menu, "Things").Id);
            var thing = Row(menu, model.Pencilled).Id;
            _ = ClickAt(menu, menu.ToolMiddle(thing, 0));
            Assert.Equal(4, menu.InlineDepth);
            Type(menu, "Food");
            _ = Key(menu, VK.VK_RETURN);
            model.Answer(menu, Pump(menu));
            Assert.True(menu.HotId == thing && menu.RowsShown.Any(row => row.Id == thing && row.Text == "Food"), "the thing's row, renamed, is not lit");
        }, 3, _ => "Things"),
        new("thing named in its level, Enter", (menu, model) => { _ = menu.Click(Row(menu, "Things").Id); _ = menu.Click(Row(menu, model.Named).Id); _ = menu.Click(Row(menu, "Name").Id); Type(menu, "Office"); _ = Key(menu, VK.VK_RETURN); model.Answer(menu, Pump(menu)); }, 4, model => model.Named),
    ];

    /// <summary>
    /// Every kind of field committed on "Outer › Inner", whose words change with it: a field's own level goes
    /// back one, anything else stays where it is — never the menu's own rows, never a level up from there.
    /// </summary>
    [UiFact]
    public void EveryKindOfFieldLeavesThePersonOnItsLevel()
    {
        WindowShots.OnHiddenDesktop("level-every-field", (gfx, instance) =>
        {
            List<string> wrong = [];
            foreach (var each in Cases)
            {
                var model = new Model();
                using var menu = Show(gfx, instance, OptionMenu.Rows(model.Rows(), model.Picks));
                _ = menu.Click(Row(menu, "Outer 0").Id);
                _ = menu.Click(Row(menu, "Inner 0").Id);
                Assert.Equal(2, menu.InlineDepth);
                var made = model.Made;
                try
                {
                    each.Commit(menu, model);
                }
                catch (Exception error) when (error is Xunit.Sdk.XunitException or InvalidOperationException)
                {
                    wrong.Add($"{each.Name}: {error.Message}");
                    continue;
                }
                if (model.Made == made)
                {
                    wrong.Add($"{each.Name}: nothing set");
                }
                else if ((menu.InlineDepth, menu.InlineTitle) != (each.Depth, each.Title(model)))
                {
                    wrong.Add($"{each.Name}: on {menu.InlineTitle ?? "the menu's own rows"} ({menu.InlineDepth} deep), not {each.Title(model)} ({each.Depth})");
                }
            }
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        });
    }

    // --- Helpers -----------------------------------------------------------------------------------------

    /// <summary>An edit-mode configuration menu, whose levels open in place (Flyout.Inline.cs).</summary>
    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
        menu.Show(gfx, AddListPictures.LookFor(dark: true) with { Animations = false }, Scale, items, FlyoutLayout.List, new POINT { x = Work.right / 3, y = Work.bottom }, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>Down until the keys are on row <paramref name="id"/>.</summary>
    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 40 && menu.HotId != id; step++)
        {
            _ = Key(menu, VK.VK_DOWN);
        }
        Assert.Equal(id, menu.HotId);
    }

    /// <summary>The menu's own posted messages handed to it for <paramref name="ms"/>, as the dock's window procedure hands them: what it said.</summary>
    private static List<FlyoutEvent> Pump(Flyout menu, int ms = 300)
    {
        List<FlyoutEvent> said = [];
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            for (var left = 64; left > 0 && PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE); left--)
            {
                if (msg.hwnd == menu.Hwnd && (msg.message is >= WM.WM_USER and <= WM.WM_USER + 0xFF || msg.message == WM.WM_TIMER))
                {
                    said.Add(menu.Message(msg.message, msg.wParam, msg.lParam));
                    continue;
                }
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        return [.. said.Where(happened => happened.Kind != FlyoutEventKind.Nothing)];
    }
}
