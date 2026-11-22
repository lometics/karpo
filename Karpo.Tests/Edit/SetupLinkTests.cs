// "Set up in Settings" (the person, 2026-10-08: "rss feed flyout should show shortcut to settings if
// it is not fully configured to show a source, this should also be done for all the other widgets.
// in the same fashion"): every widget that shows nothing until it is set up carries the link, on the
// bar and on its line as in its flyout (Karpo.Dock\SetupPrompt.cs), and the link opens the widget's own
// page at the box that sets it up â€” or the Accounts page for the mail, the calendar and the
// contacts. Which widgets need it is Core's one rule (WidgetSetup.Needs); these hold that every kind
// it names answers with the link, that the link names the right page and field, and that a widget
// set up has none.
//
// The drawings need Direct2D, so those run only with KARPO_UI_TESTS=1; the pictures go to
// KARPO_PNG_DIR as setup-*.png. The flyouts' own are with theirs (RssPictureTests,
// CalendarWeatherFolderTests, MarketOffscreenTests).

using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Dock.Pim;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe class SetupLinkTests
{
    private const string Owner = "display:default";

    /// <summary>A widget in a folder, as the dock keys one: the owner, the folder, the widget.</summary>
    private const string InFolder = Owner + "/f1/w1";

    // --- Where the link goes --------------------------------------------------------------

    /// <summary>
    /// Every field is a setting of the widget's own menu (§228: no page in the settings app any
    /// more), which its link opens: a widget of each kind that needs one has the row that sets it,
    /// and the link is no account's. Words for its line and its flyout, in the language Karpo speaks.
    /// </summary>
    [Fact]
    public void EveryFieldTheLinkAsksForIsARowOfTheWidgetsMenu()
    {
        // §425: the row each field is, which a menu opened to set it up opens on (WidgetSetup.RowOf).
        var RowOf = WidgetSetup.RowOf;
        var devices = new OptionDevices { Drives = [new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50)] };
        var asked = new HashSet<SetupField>();
        // A new weather follows this PC's location; one told not to needs its place.
        var placeless = ListKind.WeatherNow.New();
        placeless.Weather = new WeatherOptions { UseDeviceLocation = false };
        foreach (var item in Enum.GetValues<ListKind>().Select(kind => (WidgetItem)kind.New()).Concat(Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New())).Append(placeless))
        {
            if (WidgetSetup.Needs(item) is not { } field)
            {
                continue;
            }
            asked.Add(field);
            Assert.Contains(OptionRows.Of(item, devices, new OptionPlace(new Config(), Dark: true)), row => row.Key == RowOf(field));
            var need = SetupNeed.Of(InFolder, field);
            Assert.Equal(field, need.Link.Field);
            Assert.False(need.Link.ToAccounts);
            Assert.False(string.IsNullOrWhiteSpace(need.Short));
            Assert.False(string.IsNullOrWhiteSpace(need.Long));
        }
        Assert.Equal(Enum.GetValues<SetupField>().Order(), asked.Order());
    }

    /// <summary>
    /// The mail, the calendar, the contacts and the to-do lists need an account, not a field (§340: no
    /// settings app any more): theirs opens ⚙ › Accounts (§351: the accounts are Karpo's, not the widget's),
    /// which every such kind's menu links to too (OptionAccountRows).
    /// </summary>
    [Fact]
    public void AnAccountLinkOpensTheAccounts()
    {
        foreach (var need in new[] { SetupNeed.Mail, SetupNeed.Calendar, SetupNeed.Contacts })
        {
            Assert.True(need.Link.ToAccounts);
            Assert.Null(need.Key);
            Assert.False(string.IsNullOrWhiteSpace(need.Long));
        }
        foreach (var kind in (ListKind[])[ListKind.Mail, ListKind.Contacts, ListKind.Todo, ListKind.Calendar])
        {
            var rows = OptionRows.Of(kind.New(), null, new OptionPlace(new Config(), Dark: true));
            Assert.Contains(rows, row => row is { Kind: OptionRowKind.Link, Link: OptionLink.Accounts });
        }
        // The dock opens it there: the folder's "Set up" of a widget that needs an account is ⚙ › Accounts, the
        // rest the widget's configuration menu.
        var folders = RepoSource.Read("Karpo.Dock", "Dock.Folders.cs");
        Assert.Contains("OpenEditMenuLater(opened.Window, EditEntry.ManageAccounts);", folders);
        Assert.Contains("WidgetMenu(opened.Window, widget.Key, happened.At, opened.Hit, folder: opened, widget: widget, configure: true);", folders);
    }

    /// <summary>
    /// The drives are the newest to need a field: a drive or network drive line with no drive says
    /// so, and its link opens its menu, where its drive is chosen.
    /// </summary>
    [Fact]
    public void ADriveWidgetWithNoDriveNeedsItsVolume()
    {
        Assert.Equal(SetupField.Volume, WidgetSetup.Needs(ListKind.Drive.New()));
        Assert.Equal(SetupField.Volume, WidgetSetup.Needs(ListKind.NetworkDrive.New()));
        var set = ListKind.Drive.New();
        set.Drive = new DriveTargetOptions { Letter = "C:" };
        Assert.Null(WidgetSetup.Needs(set));
        Assert.Null(WidgetSetup.Needs(ListKind.RemovableDevices.New()));
    }

    /// <summary>
    /// A pin with nothing to open yet, or whose target has gone, ends its menu with "Choose…" or
    /// "Locate…" and a pin's own "Unpin from taskbar": no "Set up in Settings" or "Settings" under them
    /// any more, what it opens being its menu's own row (§228).
    /// </summary>
    [Fact]
    public void APinWithNothingToOpenOffersToChooseIt()
    {
        var unset = TheDock.BrokenRows("Pin", S.DockPinNothingYet, "", inFolder: false);
        Assert.Equal([S.DockPinChooseMenu, S.DockMenuUnpin], unset.TakeLast(2).Select(row => row.Text));
        var gone = TheDock.BrokenRows("gone", "Can't be found", @"C:\gone.exe", inFolder: false);
        Assert.Equal([S.DockPinLocate, S.DockMenuUnpin], gone.TakeLast(2).Select(row => row.Text));
        Assert.DoesNotContain(unset.Concat(gone), row => row.Text is var text && (text == S.DockSetupButton || text == S.DockMenuSettings));
    }

    // --- Every kind that needs it --------------------------------------------------------

    /// <summary>
    /// Every taskbar entry and list line whose new widget needs a field (WidgetSetup.Needs) is made
    /// as the dock makes it and answers with the link to that field; a line three rows tall says what
    /// is missing and draws the button, with its whole words; the whole of a line is one target,
    /// which takes no pointer, so a click on it is the folder's to turn into the link.
    /// </summary>
    [UiFact]
    public void EveryKindThatNeedsAFieldShowsTheLinkUntilItIsSet()
    {
        var wrong = new List<string>();
        var seen = new HashSet<SetupField>();
        Offscreen.OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            try
            {
                var config = new Config();
                foreach (var kind in Enum.GetValues<BarKind>().Where(kind => WidgetSetup.Needs(kind.New()) is not null))
                {
                    var item = kind.New();
                    item.Id = "w1";
                    // A pin on the bar is the scene's own drawing, and no island: its menu carries the link.
                    using var island = Karpo.Dock.Islands.Create(Config.IslandKey(Owner, "w1"), item, gfx, config) as CellIsland;
                    if (island is not null)
                    {
                        Check($"bar {kind}", island, WidgetSetup.Needs(item)!.Value, $"{Owner}/w1", null);
                    }
                }

                foreach (var kind in Enum.GetValues<ListKind>().Where(kind => WidgetSetup.Needs(kind.New()) is not null))
                {
                    var item = kind.New();
                    item.Id = "w1";
                    using var island = (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, item, kind, gfx, config)!;
                    Check($"line {kind}", island, WidgetSetup.Needs(item)!.Value, InFolder, WidgetSurface.List);
                }

                // weather: a new line is for the device's location and needs nothing (2026-10-11); one
                // whose place was taken away links to it, and so does one for the device's location
                // while Windows gives none and nothing was fetched — until a forecast comes.
                Expect(WidgetSetup.Needs(ListKind.WeatherNow.New()) is null, "a new weather line asks for a place");
                foreach (var kind in (ListKind[])[ListKind.WeatherNow, ListKind.WeatherHours, ListKind.WeatherDays])
                {
                    var placeless = kind.New();
                    placeless.Id = "w1";
                    placeless.Weather = new WeatherOptions();
                    using (var island = (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, placeless, kind, gfx, config)!)
                    {
                        Check($"line {kind} without a place", island, SetupField.Place, InFolder, WidgetSurface.List);
                    }
                    var located = kind.New();
                    located.Id = "w1";
                    using var line = (WeatherLineTile)Karpo.Dock.Islands.CreateLine(InFolder, located, kind, gfx, config)!;
                    WeatherCache? shown = null;
                    line.Source = new WeatherSource { Read = () => (shown, S.WidgetWeatherLocationOff, false), Ask = () => false };
                    line.Refresh(gfx);
                    Check($"line {kind} where Windows gives no location", line, SetupField.Place, InFolder, WidgetSurface.List);
                    shown = SavedWeather.Cache();
                    line.Refresh(gfx);
                    Expect(line.Unset is null, $"line {kind} with a forecast for the device's location still asks for a place");
                    line.Source = new WeatherSource { Read = () => (null, S.WidgetWeatherUnreachable, false), Ask = () => false };
                    line.Refresh(gfx);
                    Expect(line.Unset is null, $"line {kind} that could not reach the service asks for a place");
                }

                // Set up, the offline ones ask nothing more (the online ones would start a fetch).
                var pin = ListKind.Pin.New();
                pin.Id = "w1";
                using var pinned = (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, pin, ListKind.Pin, gfx, config)!;
                pin.Pin = new PinOptions { Target = @"C:\Windows\notepad.exe" };
                pinned.Configure(pin, config);
                Expect(pinned.Unset is null, "a pin with a target still asks to be set up");
                var heading = ListKind.Heading.New();
                heading.Id = "w1";
                using var named = (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, heading, ListKind.Heading, gfx, config)!;
                heading.Heading = new HeadingOptions { Text = "Work" };
                named.Configure(heading, config);
                Expect(named.Unset is null, "a heading with words still asks to be set up");
                var drive = ListKind.Drive.New();
                drive.Id = "w1";
                using var chosen = (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, drive, ListKind.Drive, gfx, config)!;
                drive.Drive = new DriveTargetOptions { Letter = "C:", Label = "Local Disk" };
                chosen.Configure(drive, config);
                Expect(chosen.Unset is null, "a drive widget with a drive still asks to be set up");
            }
            finally
            {
                DriveWatch.Source = before;
            }

            void Expect(bool held, string why)
            {
                if (!held)
                {
                    wrong.Add(why);
                }
            }

            void Check(string what, CellIsland island, SetupField field, string key, WidgetSurface? surface)
            {
                seen.Add(field);
                if (island.Unset is not { } need)
                {
                    wrong.Add($"{what}: no link");
                    return;
                }
                if (need.Link.Field != field || need.Key != key)
                {
                    wrong.Add($"{what}: links to {need.Link.Field} of {need.Key}, not {field} of {key}");
                }
                if (surface is not { } on)
                {
                    return;
                }
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), ListFormPictures.Rows(3, 1.5f, 1f));
                var style = FolderFlyout.StyleFor(1.5f, TestLooks.LookFor(dark: true), FlyoutTheme.For(TestLooks.LookFor(dark: true)));
                var said = new List<string>();
                PaintWatch.Said = (text, _) => said.Add(text);
                try
                {
                    Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => island.PaintOn(dc, box, style, on), _ => { });
                }
                finally
                {
                    PaintWatch.Said = null;
                }
                // A line has the room for the button's whole words: "Set up", its menu (§228).
                var button = S.DockSetupShort;
                if (!said.Contains(button))
                {
                    wrong.Add($"{what}: no \"{button}\" drawn, only {string.Join(" | ", said)}");
                }
                if (!said.Contains(need.Short))
                {
                    wrong.Add($"{what}: \"{need.Short}\" not said, only {string.Join(" | ", said)}");
                }
                var middle = new System.Numerics.Vector2((box.left + box.right) / 3, (box.top + box.bottom) / 3);
                if (island.TargetOn(middle, box, style, on) != 0 || island.PressOn(middle, box, style, on))
                {
                    wrong.Add($"{what}: not one target, or it takes the pointer");
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        // Every field has a kind that needs it, so none of them is a word nobody sends.
        Assert.Equal(Enum.GetValues<SetupField>().ToHashSet(), seen);
    }

    /// <summary>
    /// The mail and the contacts with no account that gives them anything: their lines are the link
    /// to the Accounts page; once an account answers, they are the mail
    /// and the contacts again.
    /// </summary>
    [UiFact]
    public void TheMailAndTheContactsWithNoAccountLinkToTheAccounts()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            using var mail = new MailStore(new AccountSet([]));
            var mailItem = ListKind.Mail.New();
            mailItem.Id = "m1";
            using var inbox = new MailIsland(InFolder, mailItem, gfx, config, mail);
            mail.NoAccountFound();
            Assert.Equal(SetupNeed.Mail, inbox.Unset);
            mail.Answered([], 0);
            Assert.Null(inbox.Unset);

            var contacts = new ContactStore(new AccountSet([]));
            var people = ListKind.Contacts.New();
            people.Id = "c1";
            using var book = new ContactsLine(InFolder, people, gfx, config, contacts);
            // Before anything has read the accounts, nothing is known to be missing.
            Assert.Null(book.Unset);
            contacts.NoAccountFound();
            Assert.Equal(SetupNeed.Contacts, book.Unset);
            contacts.Answered([]);
            Assert.Null(book.Unset);
        });
    }

    // --- Centred -------------------------------------------------------------------------

    /// <summary>The list kinds that need a field, each made as a folder's line, unset.</summary>
    private static List<CellIsland> UnsetLines(Gfx gfx, Config config) =>
        [.. Enum.GetValues<ListKind>().Where(kind => WidgetSetup.Needs(kind.New()) is not null).Select(kind =>
        {
            var item = kind.New();
            item.Id = "w1";
            return (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, item, kind, gfx, config)!;
        })];

    /// <summary>
    /// The prompt is the kit's one Status (§388; it was centred in its box, 2026-10-10): one row at the
    /// line's top, never centred and never large — the kind's glyph in the list's leading column, what is
    /// missing where every name begins, and a standard "Set up" button (never the accent's, never "Set up in
    /// Settings") against the trailing gutter. Every kind that needs a field, at 100 % and 150 %, a line one
    /// to six rows tall, at a list's width and a narrow one: inside its box, nothing on anything.
    /// </summary>
    [UiFact]
    public void ThePromptIsOneStatusRowAtTheTopOfItsLine()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            var made = UnsetLines(gfx, new Config());
            try
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = FolderFlyout.StyleFor(scale, TestLooks.LookFor(dark: true), FlyoutTheme.For(TestLooks.LookFor(dark: true)));
                    var form = ListForm.Of(gfx, style);
                    var list = FolderFlyout.ListWidth(scale);
                    foreach (var width in new[] { list, MathF.Round(list * 0.6f) })
                    {
                        for (var rows = 1; rows <= 6; rows++)
                        {
                            // Off the origin, so a plan that forgets the box's corner is caught.
                            var box = PopupPaint.Rect(13, 29, width, ListFormPictures.Rows(rows, scale, 1f));
                            var middle = box.top + (MathF.Min(box.bottom - box.top, LineRows.Px(1, style)) / 2);
                            foreach (var island in made)
                            {
                                var what = $"{island.Info.Id} at {scale:0.#}x, {width} wide, {rows} rows";
                                if (island.SetupPlan(box, style) is not { } plan)
                                {
                                    wrong.Add($"{what}: no prompt");
                                    continue;
                                }
                                var all = plan.Bounds;
                                if (all.left < box.left || all.top < box.top || all.right > box.right || all.bottom > box.bottom)
                                {
                                    wrong.Add($"{what}: outside its box");
                                }
                                if (plan.Stacked || plan.Form == SetupFace.Form.Words)
                                {
                                    wrong.Add($"{what}: stacked {plan.Stacked}, {plan.Form}");
                                }
                                if (MathF.Abs(((plan.Button.top + plan.Button.bottom) / 2) - middle) > 1 || MathF.Abs(plan.Button.right - (box.right - form.Inset)) > 1)
                                {
                                    wrong.Add($"{what}: its button at {plan.Button.left},{plan.Button.top} to {plan.Button.right},{plan.Button.bottom}, not on the top row's trailing gutter");
                                }
                                if (plan.Words is { } said && MathF.Abs(said.left - (box.left + form.TextLeft)) > 1)
                                {
                                    wrong.Add($"{what}: its words start at {said.left - box.left}, not {form.TextLeft}");
                                }
                                if (plan.Glyph is { } glyph && (glyph.left < box.left + form.Inset - 1 || glyph.right > box.left + form.TextLeft))
                                {
                                    wrong.Add($"{what}: its glyph at {glyph.left - box.left}..{glyph.right - box.left}, out of the leading column");
                                }
                                D2D_RECT_F?[] pieces = [plan.Glyph, plan.Words, plan.Button];
                                var placed = pieces.OfType<D2D_RECT_F>().ToList();
                                for (var a = 0; a < placed.Count; a++)
                                {
                                    for (var b = a + 1; b < placed.Count; b++)
                                    {
                                        if (placed[a].left < placed[b].right && placed[b].left < placed[a].right && placed[a].top < placed[b].bottom && placed[b].top < placed[a].bottom)
                                        {
                                            wrong.Add($"{what}: pieces {a} and {b} overlap");
                                        }
                                    }
                                }
                                if (width == list && plan.Words is null)
                                {
                                    wrong.Add($"{what}: no words at a list's width");
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                made.ForEach(island => island.Dispose());
                DriveWatch.Source = before;
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }
    /// <summary>
    /// A taskbar entry that needs a field says so in the middle of its cell, at every bar height
    /// from the thinnest to a tall one, at 100 % and 150 %: its ink centred across and down.
    /// setup-bar-*.png.
    /// </summary>
    [UiFact]
    public void ABarEntryThatNeedsSettingUpIsCentredInItsCell()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            var config = new Config();
            var made = new List<CellIsland>();
            try
            {
                // A pin on the bar is the scene's own drawing, and no island: its menu carries the link.
                // No other entry needs a field since the drives went into presets (docs\widgets.md
                // §99): their faces say "Set up" for a line with no drive, as the entries did.
                Assert.Equal([BarKind.Pin], Enum.GetValues<BarKind>().Where(kind => BarEntries.Find(kind) is not null && WidgetSetup.Needs(kind.New()) is not null));
                foreach (var kind in new[] { ListKind.Drive, ListKind.NetworkDrive })
                {
                    var holder = Karpo.Tests.Storage.DriveWidgetOffscreenTests.Holder($"w{made.Count}", kind, null);
                    made.Add(new FolderIsland(Config.IslandKey(Owner, holder.Id), holder, gfx, config));
                }
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = TestLooks.LookFor(dark: true);
                    var theme = FlyoutTheme.For(look);
                    var sheet = new List<(CellIsland Island, IslandStyle Style, float Width, float Height)>();
                    foreach (var height in new[] { 28, 36, 48, 64 })
                    {
                        var bar = new Config { Height = height };
                        var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, bar), Layout.IconPx(dpi, bar), look);
                        foreach (var island in made)
                        {
                            var (width, cell) = island.CellSize(0, style);
                            sheet.Add((island, style, width, cell));
                            Offscreen.Bitmap(width, cell, dc => island.PaintBar(dc, 0, style, width, cell), bitmap =>
                            {
                                var ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(cell));
                                if (ink.Right <= ink.Left)
                                {
                                    wrong.Add($"{island.Info.Id} at {height} px, {dpi} dpi: nothing drawn");
                                    return;
                                }
                                var across = ((ink.Left + ink.Right) / 2) - (width / 2);
                                var down = ((ink.Top + ink.Bottom) / 2) - (cell / 2);
                                // By its ink, whose descenders ("yet") sit below the line's middle: a few px down is centred.
                                if (MathF.Abs(across) > 2 || MathF.Abs(down) > MathF.Max(2, cell * 0.05f))
                                {
                                    wrong.Add($"{island.Info.Id} at {height} px, {dpi} dpi: ink off centre by ({across}, {down}) in {width}×{cell}");
                                }
                            });
                        }
                    }
                    // The sheet: each entry on a card the size of its cell, the bar heights down.
                    var gap = MathF.Round(8 * dpi / 96f);
                    var rows = sheet.Chunk(made.Count).ToList();
                    var sheetWidth = rows.Max(row => row.Sum(entry => entry.Width + gap)) + gap;
                    var sheetHeight = rows.Sum(row => row.Max(entry => entry.Height) + gap) + gap;
                    Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var row in rows)
                        {
                            var x = gap;
                            foreach (var (island, style, width, cell) in row)
                            {
                                Offscreen.Shifted(dc, x, y, () =>
                                {
                                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, cell), MathF.Round(4 * dpi / 96f), PopupPaint.Ink(theme.CardBackground));
                                    island.PaintBar(dc, 0, style, width, cell);
                                });
                                x += width + gap;
                            }
                            y += row.Max(entry => entry.Height) + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"setup-bar-{dpi}-dark.png")));
                }
            }
            finally
            {
                made.ForEach(island => island.Dispose());
                DriveWatch.Source = before;
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // --- Pictures ------------------------------------------------------------------------

    /// <summary>
    /// The link as the lines of every kind that has one draw it, at 150 %, dark and light:
    /// setup-lines-*.png.
    /// </summary>
    [UiFact]
    public void TheLinkOnLinesIsPictured()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            try
            {
                var config = new Config();
                foreach (var dark in new[] { true, false })
                {
                    Lines(gfx, config, dark, Path.Combine(Offscreen.Pictures, $"setup-lines-150-{(dark ? "dark" : "light")}.png"));
                }
            }
            finally
            {
                DriveWatch.Source = before;
            }
        });
    }

    /// <summary>One line of each kind that needs a field, one, two and four rows tall, one under another, at a list's width.</summary>
    private static void Lines(Gfx gfx, Config config, bool dark, string file)
    {
        const float scale = 1.5f;
        var look = TestLooks.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        float[] heights = [ListFormPictures.Rows(1, scale, 1f), ListFormPictures.Rows(2, scale, 1f), ListFormPictures.Rows(4, scale, 1f)];
        var gap = MathF.Round(8 * scale);
        var made = Enum.GetValues<ListKind>().Where(kind => WidgetSetup.Needs(kind.New()) is not null).Select(kind =>
        {
            var item = kind.New();
            item.Id = "w1";
            return (CellIsland)Karpo.Dock.Islands.CreateLine(InFolder, item, kind, gfx, config)!;
        }).ToList();
        var total = (made.Count * (heights.Sum() + (heights.Length * gap))) + gap;
        try
        {
            Offscreen.Bitmap(width + (2 * gap), total, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width + (2 * gap), total), 0, PopupPaint.Ink(theme.Base));
                var y = gap;
                foreach (var island in made)
                {
                    foreach (var height in heights)
                    {
                        var box = PopupPaint.Rect(0, 0, width, height);
                        Offscreen.Shifted(dc, gap, y, () =>
                        {
                            PopupPaint.Fill(dc, box, MathF.Round(4 * scale), PopupPaint.Ink(theme.CardBackground));
                            island.PaintOn(dc, box, style, WidgetSurface.List);
                        });
                        y += height + gap;
                    }
                }
            }, bitmap => Offscreen.Save(bitmap, file));
        }
        finally
        {
            made.ForEach(island => island.Dispose());
        }
    }
}
