// §521: a popup of the settings panel — a dropdown's list, a colour's swatches, a field's box — opens as WinUI's ComboBox
// opens its list: directly under its control, flush with its left edge, at least as wide, flipped over it where there
// is no room under it; in the screen px of the control's own display. The person, 2026-11-07: "the dropdowns also appear
// at the wrong location."
//
// Asked of the real dock, Karpo.exe --stage on a desktop of its own (InputReachesTheBarsTests.StageDock), driven as the
// mouse drives it (MenuToggleTests): the panel open by a second start, a dropdown pressed, and the popup's panel on the
// screen (stage-edit.json's "Menu") set against its control's (its "Deck" Pop spot). On a page as it opens, scrolled, in
// an expander's inset rows, low on the display (flipped over it), with the panel opening up from an edit bar at the
// display's foot, and on a second display at another scale.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed unsafe class PanelPopupPlaceTests(ITestOutputHelper output)
{
    /// <summary>
    /// The edit bar at the display's top (the bar at its bottom), the panel opening down: Language's list under its box; the
    /// page scrolled, a dropdown's under its box where it stands now; Look's Taskbar widgets open, its Font's under its box
    /// inset in the card; a dropdown low on the display, over it.
    /// </summary>
    [UiFact]
    public void ADropdownOpensUnderItsControl() => Drive(Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Check("karpo-language", probe.Pop("Karpo/"), over: false);
        // Scrolled: the dropdown nearest the panel's middle where the page stands now (the formats' open, see FormatsScrolled).
        probe.FormatsScrolled();
        probe.Check("karpo-scrolled", probe.PopInSight(middle: true));
        // A card's inset rows: Appearance › Taskbar widgets › Font.
        probe.Nav(PanelPage.Appearance);
        // §592: the Taskbar widgets tab chosen, its Font under the preview and the tabs: brought into sight.
        probe.Tab(LookPart.Widgets);
        var font = "Appearance/" + PanelTabs.Mark + S.SettingsAppearanceStylesWidgets + "/";
        probe.WheelTo(PanelHitKind.Pop, font);
        probe.Check("look-inset-font", probe.InSight(PanelHitKind.Pop, font)[0]);
        // §556: closed, so the lowest dropdown in sight — the Font again, with no Weight under it — opens afresh.
        probe.Escape();
        // The lowest dropdown in sight: under it still, where the room under it holds its list.
        probe.Check("look-low", probe.PopInSight(middle: false));
    });

    /// <summary>The bar along the display's top: the edit bar at its foot, the panel opening up from it; its dropdowns' lists still hang under their boxes where there is room.</summary>
    [UiFact]
    public void ADropdownOpensUnderItsControlWithThePanelOpeningUp() => Drive(Edge.Top, null, probe =>
    {
        probe.Open();
        Assert.True(probe.PanelOpensUp(), "the panel does not open up from an edit bar at the display's foot");
        probe.Check("up-language", probe.Pop("Karpo/"));
        probe.FormatsScrolled();
        probe.Check("up-scrolled", probe.PopInSight(middle: true));
        // Low on the display, by the edit bar at its foot: no room under it, its list over it. §592 the Taskbar widgets tab,
        // its Font under the preview (the Bar tab's one dropdown, Pattern, under the fold).
        probe.Nav(PanelPage.Appearance);
        probe.Tab(LookPart.Widgets);
        probe.Check("up-low", probe.PopInSight(middle: false), over: true);
    });

    /// <summary>Two displays, 150 % and 100 %: the second display's panel, its dropdown under its box in that display's px.</summary>
    [UiFact]
    public void ADropdownOpensUnderItsControlOnASecondDisplayAtAnotherScale() =>
        Drive(Edge.Bottom, "-2560,0,2560,1440,144;0,0,1920,1080,96", probe =>
        {
            // Edit mode, then the second display's Karpo entry: its flyout opens there.
            probe.Open();
            probe.Click(probe.Spot(PanelHitKind.Nav, nameof(PanelPage.Karpo), onRight: true).Center);
            probe.Pump(1200);
            probe.Check("second-language", probe.Pop("Karpo/", onRight: true));
        });

    /// <summary>
    /// §559 (found 2026-11-07: a colour's picker opened low on the display stood over its own button): a §535 colour button's
    /// picker — its swatches and §523's mixer, taller than a list — hangs from its button as every popup of the panel does
    /// (PopupPlace): directly under it or directly over it, never over the button, on the work area; the page wheeled down a
    /// notch at a time, the highest and the lowest colour button in sight at each, the bar at the display's foot (the panel
    /// opening down) or its top (the panel opening up from an edit bar at its foot).
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void AColoursPickerHangsFromItsButton(Edge barEdge) => Drive(barEdge, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Appearance);
        probe.Colours($"colour-{barEdge}");
    });

    /// <summary>§559: on the second display at 150 %, its own panel: a colour's picker under or over its button in that display's px.</summary>
    [UiFact]
    public void AColoursPickerHangsFromItsButtonOnASecondDisplayAtAnotherScale() =>
        Drive(Edge.Bottom, "-2560,0,2560,1440,144;0,0,1920,1080,96", probe =>
        {
            probe.Open();
            probe.Nav(PanelPage.Appearance);
            probe.Colours("colour-second");
        });

    /// <summary>
    /// §531 (the person, 2026-11-07: "the add widget button still opens the dropdown at the wrong place"), §575: the edit
    /// bar's "+ Add" opens its list, a flyout of the settings' look, §584 12 DIP off the edit bar (over it from an edit bar at
    /// the display's foot), §593 hung from the whole bar, as wide (the person, 2026-11-09: "i want the add menu to be shown on
    /// the top in the same width as the edit bar all the time"), all on the work area — another flyout open or none (it takes
    /// that one's place), the bar along the display's foot or top.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom, true)]
    [InlineData(Edge.Bottom, false)]
    [InlineData(Edge.Top, true)]
    [InlineData(Edge.Top, false)]
    public void TheAddListOpensUnderTheAddButton(Edge barEdge, bool open) => Drive(barEdge, null, probe =>
    {
        probe.Open();
        if (!open)
        {
            probe.Close();
        }
        probe.CheckAdd($"add-{barEdge}-{(open ? "open" : "closed")}", probe.Spot(PanelHitKind.Add), over: barEdge == Edge.Top);
    });

    /// <summary>§531: on a second display at 100 % the add list under its own edit bar, as wide, in that display's px, another flyout open there or none.</summary>
    [UiFact]
    public void TheAddListOpensUnderTheAddButtonOnASecondDisplayAtAnotherScale() =>
        Drive(Edge.Bottom, "-2560,0,2560,1440,144;0,0,1920,1080,96", probe =>
        {
            probe.Open();
            probe.Close();
            probe.CheckAdd("second-add-closed", probe.Spot(PanelHitKind.Add, "", onRight: true), over: false);
            probe.Click(probe.Spot(PanelHitKind.Nav, nameof(PanelPage.Karpo), onRight: true).Center);
            probe.Pump(1200);
            probe.CheckAdd("second-add-open", probe.Spot(PanelHitKind.Add, "", onRight: true), over: false);
        });

    /// <summary>
    /// §599 (the person, 2026-11-09: "the dropdowns from these menus should also have a fixed width like all the others"): a
    /// row that only acts at its line's end — the taskbar menu's Add an entry (Taskbar › Behavior's expander opened) —: its list
    /// under its button, flush with its right, a value's box's one width. (§628: Add a layout opens a page, no list:
    /// AddLayoutStageTests.)
    /// </summary>
    [UiFact]
    public void AFootButtonsListIsOneWidthUnderItsButton() => Drive(Edge.Bottom, null, probe =>
    {
        // The page wheeled down till that part of the row of those words is in sight.
        Spot Named(PanelHitKind part, string words)
        {
            for (var notch = 0; notch < 20; notch++)
            {
                // Wholly on the page, however near its end (a short page's last row stands in its last 80 px).
                var spots = probe.Read("stage-edit.json");
                var page = spots.First(spot => spot.Hit == "Page").Rect;
                if (spots.FirstOrDefault(spot => spot.Hit == "Deck" && spot.Key.StartsWith(part + ":", StringComparison.Ordinal) && spot.Key.EndsWith("/" + words, StringComparison.Ordinal)
                    && spot.Rect.top >= page.top && spot.Rect.bottom <= page.bottom) is { } found)
                {
                    return found;
                }
                probe.Wheel(-1);
            }
            throw new InvalidOperationException($"no {part} of {words} came into sight");
        }
        probe.Open();
        probe.Nav(PanelPage.Taskbar);
        probe.Click(Named(PanelHitKind.Head, S.SettingsBarMenuHeader).Center);
        probe.Pump(800);
        probe.Check("foot-entry", Named(PanelHitKind.Pop, S.SettingsBarMenuAdd), right: true);
    });

    /// <summary>
    /// §521's pictures (KARPO_PNG_DIR, 150 %, dark, the stage's windows laid one over the other on a backdrop of a colour
    /// nothing draws): a dropdown open under its box (popup-under), one on a scrolled page (popup-scrolled), a long list's,
    /// its search over its finds (popup-long: Public holidays › Add a country); and the panel opening up from an edit bar at
    /// the display's foot, a dropdown open (popup-up).
    /// </summary>
    [UiFact]
    public void PanelPopupPictures()
    {
        Drive(Edge.Bottom, null, probe =>
        {
            probe.Open();
            probe.Picture("popup-under", probe.Pop("Karpo/"));
            probe.Escape();
            // §532: Public holidays under the Taskbar menu's section, the page wheeled down to it.
            probe.WheelTo(PanelHitKind.Head, "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName);
            probe.Click(probe.Spot(PanelHitKind.Head, "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName).Center);
            probe.Picture("popup-long", probe.Pop("Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName + "/"), typed: "sw");
            probe.Escape();
            probe.Nav(PanelPage.Appearance);
            // §592: the Taskbar widgets tab, its dropdowns under the preview.
            probe.Tab(LookPart.Widgets);
            probe.Wheel(-4);
            probe.Picture("popup-scrolled", probe.PopInSight(middle: true));
        });
        Drive(Edge.Top, null, probe =>
        {
            probe.Open();
            probe.Picture("popup-up", probe.Pop("Karpo/"));
        });
    }

    /// <summary>§531's pictures, §575: the add list's flyout open under the edit bar's "+ Add" (add-open); over it from an edit bar at the display's foot (add-up).</summary>
    [UiFact]
    public void TheAddListPictures()
    {
        Drive(Edge.Bottom, null, probe =>
        {
            probe.Open();
            probe.Picture("add-open", probe.Spot(PanelHitKind.Add), flyout: true);
            probe.Close();
        });
        Drive(Edge.Top, null, probe =>
        {
            probe.Open();
            probe.Picture("add-up", probe.Spot(PanelHitKind.Add), flyout: true);
            probe.Close();
        });
    }

    private void Drive(Edge barEdge, string? displays, Action<Probe> run) => Drive(output, barEdge, displays, run);

    /// <summary>
    /// The stage dock on a desktop of its own, a bar at <paramref name="barEdge"/> on <paramref name="displays"/>, driven by
    /// <paramref name="run"/> on a thread there (PanelPopupToggleTests too); its config as <paramref name="dress"/> has it, where given.
    /// </summary>
    internal static void Drive(ITestOutputHelper output, Edge barEdge, string? displays, Action<Probe> run, Action<Config>? dress = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "popup-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        var config = new Config();
        foreach (var kind in new[] { BarKind.Start, BarKind.Tray, BarKind.Volume })
        {
            var made = kind.New();
            made.Section = kind == BarKind.Start ? BarSection.Start : BarSection.End;
            config.DefaultLayout.Islands.Add(made);
        }
        Sections.Order(config.DefaultLayout.Islands);
        config.Screens.Add(new Screen { Id = "stage", Name = "Stage", Edge = barEdge });
        dress?.Invoke(config);
        File.WriteAllText(Path.Combine(folder, "config.json"), config.ToJson());
        var name = "karpo-popup-" + Guid.NewGuid().ToString("N");
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        var dock = InputReachesTheBarsTests.StageDock.Start(name, folder, displays);
        var said = new StringBuilder();
        Exception? failed = null;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
                    _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                    var probe = new Probe(dock, said);
                    _ = probe.Read("stage-map.json", wait: true);
                    probe.Pump(1500);
                    run(probe);
                    probe.Finish();
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
            output.WriteLine(said.ToString());
            if (File.Exists(Path.Combine(folder, "local", "karpo.log")))
            {
                output.WriteLine(File.ReadAllText(Path.Combine(folder, "local", "karpo.log")));
            }
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

    /// <summary>A thing to press on (stage-edit.json): what it is and where, in screen px.</summary>
    internal sealed record Spot(string Hit, int Index, string Key, RECT Rect)
    {
        public POINT Center => new() { x = (Rect.left + Rect.right) / 2, y = (Rect.top + Rect.bottom) / 2 };

        public override string ToString() => $"{Hit} {Key} {Rect.left},{Rect.top}..{Rect.right},{Rect.bottom}";
    }

    /// <summary>The drive and the checks, on a thread on the dock's desktop.</summary>
    internal sealed class Probe(InputReachesTheBarsTests.StageDock dock, StringBuilder said)
    {
        private readonly List<string> wrong = [];

        /// <summary>
        /// §699: the dock told a window was minimized or restored (FrontWatch.WM_KARPO_MINIMIZED, posted to its host): every
        /// bar's app buttons worked out again (Dock.WindowsChanged), as the person's windows coming and going do; a moment after.
        /// </summary>
        public void WindowsChanged()
        {
            var host = HWND.NULL;
            fixed (char* name = DockHost.ClassName)
            {
                while ((host = FindWindowExW(HWND.NULL, host, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(host, &pid);
                    if (pid == dock.Pid)
                    {
                        break;
                    }
                }
            }
            Assert.True(host != HWND.NULL, "the dock's host window was not found");
            _ = PostMessageW(host, FrontWatch.WM_KARPO_MINIMIZED, 0, 0);
            Pump(1500);
        }

        /// <summary>A second start: edit mode, §575 the Karpo entry's flyout open.</summary>
        public void Open()
        {
            Assert.True(Host() is { } host && PostMessageW(host, SecondStart.Message, 0, 0), "no dock host");
            Pump(1500);
            _ = Spot(PanelHitKind.Nav);
        }

        /// <summary>§575: an entry: its flyout (nothing where it is open already).</summary>
        public void Nav(PanelPage page)
        {
            if (PageShown() != page.ToString())
            {
                Click(Spot(PanelHitKind.Nav, page.ToString()).Center);
            }
        }

        /// <summary>§575: the flyout open on the screen ("Page"), and whether it hangs up from its entry (the edit bar under it).</summary>
        public bool PanelOpensUp()
        {
            var page = Read("stage-edit.json").First(spot => spot.Hit == "Page");
            var entry = Spot(PanelHitKind.Nav, page.Key).Rect;
            return page.Rect.bottom <= entry.top;
        }

        /// <summary>The first dropdown whose row's key starts with <paramref name="key"/> (on the right display, where asked).</summary>
        public Spot Pop(string key, bool onRight = false) => Spot(PanelHitKind.Pop, key, onRight);

        /// <summary>
        /// The Karpo page's formats opened (General's "Date and time formats", its rows dropdowns) and the page wheeled down a
        /// notch. Closed, the page's only dropdowns are Language and Animations at its top, and since §579 put About Karpo in a
        /// section of its own the page is tall enough to scroll them out of sight, with none to come in.
        /// </summary>
        public void FormatsScrolled()
        {
            var formats = "Karpo/" + S.SettingsGeneralHeader + "/" + S.SettingsLocalizationFormatsSection;
            Click(Spot(PanelHitKind.Head, formats).Center);
            Pump(400);
            Wheel(-1);
        }

        /// <summary>The dropdown in sight on the open page nearest its middle, or the lowest one in sight.</summary>
        public Spot PopInSight(bool middle)
        {
            var spots = Read("stage-edit.json");
            var page = spots.First(spot => spot.Hit == "Page").Rect;
            // §575: the page's viewport: its flyout, a card's height off either end.
            var (top, bottom) = (page.top + 80, page.bottom - 80);
            var pops = spots.Where(spot => spot.Hit == "Deck" && spot.Index == (int)PanelHitKind.Pop).ToList();
            // A dropdown's box, the one control width (§521); not a colour's button (§535), narrower, its picker no list.
            var wide = pops.Count == 0 ? 0 : pops.Max(spot => spot.Rect.right - spot.Rect.left);
            var shown = pops.Where(spot => spot.Rect.right - spot.Rect.left == wide && spot.Rect.top >= top && spot.Rect.bottom <= bottom).ToList();
            Assert.True(shown.Count > 0, $"no dropdown in sight between {top} and {bottom}: {string.Join("; ", pops)}");
            var mid = (top + bottom) / 2;
            return middle ? shown.MinBy(spot => Math.Abs(spot.Center.y - mid))! : shown.MaxBy(spot => spot.Rect.bottom)!;
        }

        /// <summary>
        /// §559: the page wheeled down a notch at a time, and at each the highest and the lowest colour button in sight (§535,
        /// §548: 72 DIP wide) pressed, its picker <see cref="Check"/>ed: under or over its button, never over it.
        /// </summary>
        public void Colours(string name)
        {
            var checkedOnes = 0;
            for (var notch = 0; notch < 16; notch++)
            {
                var spots = Read("stage-edit.json");
                var page = spots.First(spot => spot.Hit == "Page").Rect;
                // §575: the display of the flyout open (its "Flyout" keyed by its display).
                var display = spots.First(spot => spot.Hit == "Work" && spots.Any(flyout => flyout.Hit == "Flyout" && flyout.Key.Equals(spot.Key, StringComparison.OrdinalIgnoreCase)));
                // §613: a colour one colour button, its picker its own width.
                var wide = (int)MathF.Round(PanelMetrics.ColourWide * display.Index / 96f, MidpointRounding.AwayFromZero);
                var colours = InSight(PanelHitKind.Pop).Where(spot => Math.Abs(spot.Rect.right - spot.Rect.left - wide) <= 1).ToList();
                foreach (var colour in colours.Count > 1 ? [colours[0], colours[^1]] : colours)
                {
                    Check($"{name}-{notch}-{colour.Key}", colour, wide: PanelMetrics.PickerWide);
                    checkedOnes++;
                }
                Wheel(-2);
            }
            Assert.True(checkedOnes > 0, $"{name}: no colour button came into sight");
        }

        /// <summary>§592: the Appearance's tab of <paramref name="part"/> chosen, its rows under the tabs.</summary>
        public void Tab(LookPart part)
        {
            Click(Spot(PanelHitKind.Tab, "Appearance/" + PanelTabs.Mark + LookParts.Name(part)).Center);
            Pump(400);
        }

        /// <summary>§532: the page wheeled down, a notch at a time, till the <paramref name="hit"/> of row <paramref name="key"/> stands in its viewport.</summary>

        public void WheelTo(PanelHitKind hit, string key)
        {
            for (var notch = 0; notch < 20 && InSight(hit, key).Count == 0; notch++)
            {
                Wheel(-1);
            }
            Assert.NotEmpty(InSight(hit, key));
        }

        /// <summary>The wheel over §575 the flyout open: <paramref name="notches"/> (negative: down).</summary>
        public void Wheel(int notches)
        {
            var page = Read("stage-edit.json").First(spot => spot.Hit == "Page").Rect;
            var at = new POINT { x = (page.left + page.right) / 2, y = (page.top + page.bottom) / 2 };
            var flyout = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
            _ = PostMessageW(flyout, WM.WM_MOUSEWHEEL, (WPARAM)(nuint)(uint)((notches * 120) << 16), (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF)));
            Pump(800);
        }

        /// <summary>
        /// <paramref name="control"/> pressed: its popup opens directly under it (over it, with no room under it), flush with
        /// its left edge, at least as wide, as WinUI's ComboBox opens its list; Escape closes it. §575 an entry's (<paramref name="flyout"/>:
        /// "+ Add"'s) its flyout, §578 centred on it, pushed in only at the work area's sides, closed by its entry again.
        /// </summary>
        public void Check(string name, Spot control, bool? over = null, bool flyout = false, bool right = false, float wide = PanelMetrics.ControlWide)
        {
            var popup = flyout ? "Page" : "Menu";
            var before = flyout ? Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == popup)?.Rect : null;
            Click(control.Center);
            var until = Environment.TickCount64 + 5000;
            Spot? menu;
            while (((menu = Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == popup)) is null || (before is { } was && menu.Rect.left == was.left && menu.Rect.top == was.top)) && Environment.TickCount64 < until)
            {
                Pump(100);
            }
            if (menu is null)
            {
                wrong.Add($"{name}: {control} opened nothing");
                return;
            }
            Pump(300);
            menu = Read("stage-edit.json").First(spot => spot.Hit == popup);
            var (c, m) = (control.Rect, menu.Rect);
            var spots = Read("stage-edit.json");
            // §575: what the control stands in, the edit bar or a flyout of it, whose sides its popup keeps inside.
            var deck = spots.First(spot => spot.Hit is "EditBar" or "Flyout" && Inside(c, spot.Rect));
            var bar = deck.Rect;
            var display = spots.First(spot => spot.Hit == "Work" && spot.Key.Equals(deck.Key, StringComparison.OrdinalIgnoreCase));
            var (work, scale) = (display.Rect, display.Index / 96f);
            said.AppendLine($"{name}: control {control}; popup {menu}; edit bar {Text(bar)}; work {Text(work)} at {display.Index} dpi");
            // WinUI's 4 DIP of air (6 px at 150 %, 4 at 100 %), a px either way for rounding; 12 DIP kept from the work area's sides.
            // §584: an entry's flyout 12 DIP off the edit bar itself, its beak in the gap.
            var air = (int)MathF.Round((flyout ? Beak.GapDip : 4) * scale, MidpointRounding.AwayFromZero);
            var keep = (int)MathF.Round(12 * scale, MidpointRounding.AwayFromZero);
            var (from, to) = flyout ? (bar.top, bar.bottom) : (c.top, c.bottom);
            var under = m.top >= to && Math.Abs(m.top - to - air) <= 1;
            var onTop = m.bottom <= from && Math.Abs(from - m.bottom - air) <= 1;
            if (!under && !onTop)
            {
                wrong.Add($"{name}: the popup {Text(m)} is neither directly under nor directly over its {(flyout ? "edit bar" : "control")} {Text(flyout ? bar : c)}, {air} px of air off it");
            }
            if (over is { } expected && expected != (m.bottom <= c.top))
            {
                wrong.Add($"{name}: the popup {Text(m)} is not {(expected ? "over" : "under")} its control {Text(c)}");
            }
            // §531: flush with its control's left where that keeps it inside the edit bar (the panel) and the work area, else
            // flush with its right, as a flyout of a button at a window's end (WinUI's BottomEdgeAlignedLeft, then …Right).
            var w = m.right - m.left;
            var (least, most) = (Math.Max(bar.left, work.left + keep), Math.Min(bar.right, work.right - keep));
            // §578: an entry's flyout centred on it, a px either way, but where a work area's side pushed it in.
            if (flyout)
            {
                var pushed = Math.Abs(m.left - (work.left + keep)) <= 1 || Math.Abs(m.right - (work.right - keep)) <= 1;
                if (!pushed && Math.Abs((m.left + m.right) - (c.left + c.right)) > 2)
                {
                    wrong.Add($"{name}: the flyout {Text(m)} is not centred on its entry {Text(c)}");
                }
            }
            else if (c.left + w <= most && Math.Abs(m.left - c.left) > 1)
            {
                wrong.Add($"{name}: the popup's left {m.left} is not its control's {c.left}");
            }
            else if (c.left + w > most && c.right - w >= least && Math.Abs(m.right - c.right) > 1)
            {
                wrong.Add($"{name}: the popup ({Text(m)}) passes the edit bar's or the work area's side ({least}..{most}) and its right {m.right} is not its control's {c.right}");
            }
            if (m.left < work.left || m.top < work.top || m.right > work.right || m.bottom > work.bottom)
            {
                wrong.Add($"{name}: the popup {Text(m)} is not all on the work area {Text(work)}");
            }
            // §587: a control's popup a value's box's one width, whatever it lists; §599 a row that only acts' list flush with its
            // button's right, the button at its line's end.
            if (!flyout && Math.Abs(w - (int)MathF.Round(wide * scale, MidpointRounding.AwayFromZero)) > 1)
            {
                wrong.Add($"{name}: the popup is {w} px wide, not {wide} DIP");
            }
            if (right && Math.Abs(m.right - c.right) > 1)
            {
                wrong.Add($"{name}: the popup's right {m.right} is not its button's {c.right}");
            }
            if (w < c.right - c.left - 1)
            {
                wrong.Add($"{name}: the popup ({w} wide) is narrower than its control ({c.right - c.left})");
            }
            if (flyout)
            {
                Click(control.Center);
                return;
            }
            Escape();
        }

        /// <summary>
        /// §593: <paramref name="add"/> ("+ Add") pressed: the add page hangs from the whole edit bar, as wide, flush with its
        /// sides, 12 DIP off it — under it, over it from an edit bar at the display's foot (<paramref name="over"/>) —, all on the
        /// work area; Add again closes it.
        /// </summary>
        public void CheckAdd(string name, Spot add, bool over)
        {
            var before = Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page")?.Rect;
            Click(add.Center);
            var until = Environment.TickCount64 + 5000;
            Spot? shown;
            while (((shown = Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page")) is null || shown.Key != nameof(PanelPage.Add)
                || (before is { } was && shown.Rect.left == was.left && shown.Rect.top == was.top)) && Environment.TickCount64 < until)
            {
                Pump(100);
            }
            if (shown is null || shown.Key != nameof(PanelPage.Add))
            {
                wrong.Add($"{name}: Add opened {shown?.Key ?? "nothing"}");
                return;
            }
            Pump(300);
            var spots = Read("stage-edit.json");
            var page = spots.First(spot => spot.Hit == "Page").Rect;
            var deck = spots.First(spot => spot.Hit == "EditBar" && Inside(add.Rect, spot.Rect));
            var bar = deck.Rect;
            var display = spots.First(spot => spot.Hit == "Work" && spot.Key.Equals(deck.Key, StringComparison.OrdinalIgnoreCase));
            var (work, scale) = (display.Rect, display.Index / 96f);
            said.AppendLine($"{name}: Add {add}; page {Text(page)}; edit bar {Text(bar)}; work {Text(work)} at {display.Index} dpi");
            var air = (int)MathF.Round(Beak.GapDip * scale, MidpointRounding.AwayFromZero);
            if (Math.Abs((over ? bar.top - page.bottom : page.top - bar.bottom) - air) > 1)
            {
                wrong.Add($"{name}: the add page {Text(page)} is not {air} px {(over ? "over" : "under")} its edit bar {Text(bar)}");
            }
            if (Math.Abs(page.left - bar.left) > 1 || Math.Abs(page.right - bar.right) > 1)
            {
                wrong.Add($"{name}: the add page {Text(page)} is not as wide as its edit bar {Text(bar)}");
            }
            if (page.left < work.left || page.top < work.top || page.right > work.right || page.bottom > work.bottom)
            {
                wrong.Add($"{name}: the add page {Text(page)} is not all on the work area {Text(work)}");
            }
            Click(add.Center);
        }

        private static bool Inside(RECT inner, RECT outer) =>
            inner.left >= outer.left && inner.top >= outer.top && inner.right <= outer.right && inner.bottom <= outer.bottom;

        /// <summary>§575: the flyout open closed by its entry clicked again; nothing where none is open.</summary>
        public void Close()
        {
            if (PageShown() is not { } open)
            {
                return;
            }
            Click((open == nameof(PanelPage.Add) ? Spot(PanelHitKind.Add) : Spot(PanelHitKind.Nav, open)).Center);
            Pump(800);
            Assert.Null(PageShown());
        }

        /// <summary>
        /// <paramref name="control"/> pressed, its popup let land (§575 an entry's, <paramref name="flyout"/>: its flyout), and
        /// the display as the screen shows it saved as <paramref name="name"/>-dark.png.
        /// </summary>
        public void Picture(string name, Spot control, string typed = "", bool flyout = false)
        {
            Click(control.Center);
            var until = Environment.TickCount64 + 5000;
            while (!Read("stage-edit.json").Any(spot => spot.Hit == (flyout ? "Page" : "Menu")) && Environment.TickCount64 < until)
            {
                Pump(100);
            }
            Pump(600);
            // Words typed into its list's field: what it finds.
            foreach (var (popup, _) in Plan().Where(entry => entry.Stratum == Stratum.Menu && IsWindowVisible(entry.Window)))
            {
                foreach (var c in typed)
                {
                    _ = PostMessageW(popup, WM.WM_CHAR, c, 0);
                }
            }
            Pump(typed.Length > 0 ? 1500 : 0);
            // The open panel, or the folded edit bar the control is on.
            var spots = Read("stage-edit.json");
            var area = (spots.FirstOrDefault(spot => spot.Hit == "Page") ?? spots.First(spot => spot.Hit == "EditBar" && Inside(control.Rect, spot.Rect))).Rect;
            var menu = Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Menu")?.Rect ?? area;
            // The panel and the popup, and some of the display round them.
            area = new RECT { left = Math.Min(area.left, menu.left) - 60, top = Math.Max(0, Math.Min(area.top, menu.top) - 60), right = Math.Max(area.right, menu.right) + 60, bottom = Math.Min(1440, Math.Max(area.bottom, menu.bottom) + 60) };
            Shot(name, area);
            said.AppendLine($"{name}: control {control}; popup {Text(menu)}");
        }

        /// <summary><paramref name="area"/> of the screen (px) as the dock's windows show it now, saved as <paramref name="name"/>-dark.png.</summary>
        public void Shot(string name, RECT area) =>
            WindowShots.WritePng(Pixels(area, name), area.right - area.left, area.bottom - area.top, Path.Combine(Offscreen.Pictures, $"{name}-dark.png"));

        /// <summary>The dock's windows in sight over <paramref name="area"/> (screen px), as the screen shows them: BGRA, row by row.</summary>
        public byte[] Pixels(RECT area, string name = "")
        {
            List<HWND> mine = [];
            for (var at = GetTopWindow(HWND.NULL); at != HWND.NULL; at = GetWindow(at, GW_HWNDNEXT))
            {
                uint pid = 0;
                _ = GetWindowThreadProcessId(at, &pid);
                if (pid == dock.Pid && IsWindowVisible(at))
                {
                    mine.Add(at);
                }
            }
            return FrameFilm.Snap(area, mine, 0, name).Pixels;
        }

        public void Finish()
        {
            said.AppendLine("wrong:");
            foreach (var line in wrong)
            {
                said.AppendLine("  " + line);
            }
            Assert.True(dock.Running, "the dock ended");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        }

        /// <summary>§525: the dock's menus in sight now (a popup of the panel, the add list, the errors' list).</summary>
        public List<HWND> Menus() => [.. Plan().Where(entry => entry.Stratum == Stratum.Menu && IsWindowVisible(entry.Window)).Select(entry => entry.Window)];

        /// <summary>The edit bar's window, the panel's.</summary>
        /// <summary>§606: the stratum of the window a press at <paramref name="at"/> goes to (screen px); null for one not Karpo's.</summary>
        public Stratum? StratumAt(POINT at) => Plan().Where(entry => entry.Window == Target(at)).Select(entry => (Stratum?)entry.Stratum).FirstOrDefault();

        public HWND Deck() => Plan().First(entry => entry.Stratum == Stratum.EditBar && IsWindowVisible(entry.Window)).Window;

        /// <summary>The menu the dock has open (stage-edit.json's "Menu"), where it stands; null for none.</summary>
        public Spot? Menu() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Menu");

        /// <summary>The page the open panel shows (stage-edit.json's "Page"); null folded.</summary>
        public string? PageShown() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page")?.Key;

        /// <summary>The panel's things of <paramref name="part"/> on the open page in sight, whose key starts with <paramref name="key"/>, top first.</summary>
        public List<Spot> InSight(PanelHitKind part, string key = "")
        {
            var spots = Read("stage-edit.json");
            var page = spots.First(spot => spot.Hit == "Page").Rect;
            var (top, bottom) = (page.top + 80, page.bottom - 80);
            return [.. spots.Where(spot => spot.Hit == "Deck" && spot.Index == (int)part && spot.Key.StartsWith(part + ":" + key, StringComparison.Ordinal) && spot.Rect.top >= top && spot.Rect.bottom <= bottom).OrderBy(spot => spot.Rect.top)];
        }

        /// <summary>Until a menu is in sight (<paramref name="open"/>) or none is, five seconds at most, and a moment to land; whether it came to that.</summary>
        public bool WaitMenu(bool open)
        {
            var until = Environment.TickCount64 + 5000;
            while (Menus().Count > 0 != open && Environment.TickCount64 < until)
            {
                Pump(50);
            }
            Pump(300);
            return Menus().Count > 0 == open;
        }

        /// <summary>
        /// <paramref name="act"/> done and watched for <paramref name="ms"/>: whether a menu is in sight at each moment, as fast
        /// as it can be asked (o open, c closed).
        /// </summary>
        public string Watch(Action act, int ms = 1500)
        {
            var states = new StringBuilder();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            act();
            while (clock.ElapsedMilliseconds < ms)
            {
                Pump(5);
                states.Append(Menus().Count > 0 ? 'o' : 'c');
            }
            return states.ToString();
        }

        /// <summary>A key going down on <paramref name="window"/>, with Alt held where asked (a system key, its context code set).</summary>
        public void Key(HWND window, int key, bool alt = false)
        {
            said.AppendLine($"  key {key:X}{(alt ? " with Alt" : "")} on {(nint)window:X}");
            _ = PostMessageW(window, (uint)(alt ? WM.WM_SYSKEYDOWN : WM.WM_KEYDOWN), (WPARAM)(nuint)key, alt ? (LPARAM)(nint)(1 << 29) : 0);
        }

        /// <summary>Something wrong, said at the end.</summary>
        public void Wrong(string what) => wrong.Add(what);

        /// <summary>The dock's config file as it stands.</summary>
        public string ConfigText()
        {
            for (var tries = 0; ; tries++)
            {
                try
                {
                    return File.ReadAllText(Path.Combine(dock.Folder, "config.json"));
                }
                catch (IOException) when (tries < 100)
                {
                    Pump(50);
                }
            }
        }

        /// <summary>Escape on the open menu: it closes, the panel stays.</summary>
        public void Escape()
        {
            foreach (var (menu, _) in Plan().Where(entry => entry.Stratum == Stratum.Menu && IsWindowVisible(entry.Window)))
            {
                _ = PostMessageW(menu, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            }
            Pump(700);
        }

        /// <summary>The edit bar's or the panel's thing of <paramref name="part"/>, whose key starts with <paramref name="key"/>, on the display on the right where asked.</summary>
        public Spot Spot(PanelHitKind part, string key = "", bool onRight = false)
        {
            var until = Environment.TickCount64 + 10_000;
            while (true)
            {
                var found = Read("stage-edit.json").Where(spot => spot.Hit == "Deck" && spot.Index == (int)part && spot.Key.StartsWith(part + ":" + key, StringComparison.Ordinal)).ToList();
                if ((onRight ? found.Where(spot => spot.Rect.left >= 0) : found.Where(spot => spot.Rect.left < 0 || !found.Any(other => other.Rect.left < 0))).FirstOrDefault() is { } spot)
                {
                    return spot;
                }
                Assert.True(Environment.TickCount64 < until, $"the edit bar shows no {part} {key}");
                Pump(250);
            }
        }

        /// <summary>§593: the pointer moved to <paramref name="at"/> (screen px), no button held, and a moment after it.</summary>
        public void Move(POINT at)
        {
            said.AppendLine($"  move to {at.x},{at.y}");
            Post(Target(at), WM.WM_MOUSEMOVE, at, 0);
            Pump(900);
        }

        /// <summary>§593: the pointer gone off the window at <paramref name="at"/> (screen px), as Windows says it, and a moment after it.</summary>
        public void Leave(POINT at)
        {
            _ = PostMessageW(Target(at), WM.WM_MOUSELEAVE, 0, 0);
            Pump(900);
        }

        /// <summary>A click and a moment after it.</summary>
        public void Click(POINT at)
        {
            Press(at);
            Pump(900);
        }

        /// <summary>
        /// A press and its release at <paramref name="at"/> (screen px), as MenuToggleTests.Press hands them out. §525: and as
        /// Windows hands a click to a window that answers it with its activation — the open panel does (Dock.DeckMessage's
        /// WM_MOUSEACTIVATE, by the real pointer, which a stage desktop never has) —: the menu that had the activation loses
        /// it (WM_ACTIVATE) after the hook heard the press and before the press itself comes.
        /// </summary>
        public void Press(POINT at)
        {
            var target = Target(at);
            said.AppendLine($"  press at {at.x},{at.y}");
            if (Sink() is { } sink)
            {
                nuint result;
                _ = SendMessageTimeoutW(sink, PressHook.WM_KARPO_PRESS, (WPARAM)(nuint)(WM.WM_LBUTTONDOWN | PressHook.StagedBit), EditFollowHook.Pack(at), 0, 10_000, &result);
            }
            if (Plan().Any(entry => entry.Window == target && entry.Stratum == Stratum.EditBar) && Read("stage-edit.json").Any(spot => spot.Hit == "Page"))
            {
                foreach (var menu in Menus().Where(menu => menu != target))
                {
                    nuint result;
                    _ = SendMessageTimeoutW(menu, WM.WM_ACTIVATE, 0 /* WA_INACTIVE */, (LPARAM)(nint)target, 0, 10_000, &result);
                }
            }
            Post(target, WM.WM_MOUSEMOVE, at, 0);
            Post(target, WM.WM_LBUTTONDOWN, at, 1);
            Post(target, WM.WM_MOUSEMOVE, at, 1);
            Post(target, WM.WM_LBUTTONUP, at, 0);
        }

        /// <summary>
        /// §551: a press at <paramref name="from"/>, the pointer moved to <paramref name="to"/> in steps with the button held,
        /// let go there (screen px): a drag, as Windows hands one to the window under the press, which captures it.
        /// </summary>
        public void Drag(POINT from, POINT to, int steps = 12)
        {
            var target = Target(from);
            said.AppendLine($"  drag {from.x},{from.y} to {to.x},{to.y}");
            if (Sink() is { } sink)
            {
                nuint result;
                _ = SendMessageTimeoutW(sink, PressHook.WM_KARPO_PRESS, (WPARAM)(nuint)(WM.WM_LBUTTONDOWN | PressHook.StagedBit), EditFollowHook.Pack(from), 0, 10_000, &result);
            }
            Post(target, WM.WM_MOUSEMOVE, from, 0);
            Post(target, WM.WM_LBUTTONDOWN, from, 1);
            Pump(100);
            for (var step = 1; step <= steps; step++)
            {
                Post(target, WM.WM_MOUSEMOVE, new POINT { x = from.x + ((to.x - from.x) * step / steps), y = from.y + ((to.y - from.y) * step / steps) }, 1);
                Pump(30);
            }
            Post(target, WM.WM_LBUTTONUP, to, 0);
            Pump(900);
        }

        /// <summary>§551: Ctrl+Z, as edit mode's keyboard hook hands it to the dock (EditFollowHook: WM_KARPO_FOLLOW, Undo).</summary>
        public void Undo()
        {
            Assert.True(Host() is { } host && PostMessageW(host, EditFollowHook.WM_KARPO_FOLLOW, (WPARAM)(nuint)(uint)FollowInput.Undo, 0), "no dock host");
            Pump(900);
        }

        private HWND Target(POINT at)
        {
            var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
            var bar = Plan().FirstOrDefault(entry => entry.Stratum == Stratum.Bar).Window;
            return bar != HWND.NULL && GetGUIThreadInfo(GetWindowThreadProcessId(bar, null), &info) && info.hwndCapture != HWND.NULL
                ? info.hwndCapture
                : GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
        }

        private HWND? Sink()
        {
            var at = HWND.NULL;
            fixed (char* name = "karpo.presses")
            {
                while ((at = FindWindowExW(HWND.HWND_MESSAGE, at, name, null)) != HWND.NULL)
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

        private static void Post(HWND window, uint message, POINT screen, uint keys)
        {
            RECT rect;
            _ = GetWindowRect(window, &rect);
            var (x, y) = (screen.x - rect.left, screen.y - rect.top);
            _ = PostMessageW(window, message, keys, (LPARAM)(nint)((y << 16) | (x & 0xFFFF)));
        }

        private HWND? Host()
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

        /// <summary>The plan the dock last applied, top first, each window with its stratum.</summary>
        private List<(HWND Window, Stratum Stratum)> Plan()
        {
            for (var tries = 0; ; tries++)
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dock.Folder, "stage-z.json")));
                    return [.. json.RootElement.EnumerateArray().Select(entry => ((HWND)(nint)entry.GetProperty("hwnd").GetInt64(), Enum.Parse<Stratum>(entry.GetProperty("stratum").GetString()!)))];
                }
                catch (Exception error) when (error is IOException or JsonException && tries < 200)
                {
                    Pump(50);
                }
            }
        }

        public List<Spot> Read(string name, bool wait = false)
        {
            var until = Environment.TickCount64 + 60_000;
            while (true)
            {
                Assert.True(dock.Running, "the dock ended");
                try
                {
                    var path = Path.Combine(dock.Folder, name);
                    if (File.Exists(path))
                    {
                        using var json = JsonDocument.Parse(File.ReadAllText(path));
                        var spots = json.RootElement.EnumerateArray().Select(entry => new Spot(
                            entry.GetProperty("hit").GetString()!,
                            entry.GetProperty("index").GetInt32(),
                            entry.GetProperty("key").GetString()!,
                            new RECT
                            {
                                left = entry.GetProperty("x").GetInt32(),
                                top = entry.GetProperty("y").GetInt32(),
                                right = entry.GetProperty("x").GetInt32() + entry.GetProperty("w").GetInt32(),
                                bottom = entry.GetProperty("y").GetInt32() + entry.GetProperty("h").GetInt32(),
                            })).ToList();
                        if (spots.Count > 0 || !wait)
                        {
                            return spots;
                        }
                    }
                    else if (!wait)
                    {
                        return [];
                    }
                }
                catch (Exception error) when (error is IOException or JsonException)
                {
                    // Being written.
                }
                Assert.True(Environment.TickCount64 < until, $"the dock never wrote {name}");
                Pump(100);
            }
        }

        private static string Text(RECT r) => $"{r.left},{r.top}..{r.right},{r.bottom}";

        public void Pump(int ms)
        {
            var until = Environment.TickCount64 + ms;
            while (Environment.TickCount64 < until)
            {
                _ = MsgWaitForMultipleObjects(0, null, BOOL.FALSE, (uint)Math.Max(1, until - Environment.TickCount64), QS.QS_ALLINPUT);
                MSG msg;
                while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
            }
        }
    }
}
