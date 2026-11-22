// Windows' taskbar pins kept in step with Karpo's (backlog 2): a pin Windows gains goes into every
// layout a display shows, after its last pin; a pin removed in Karpo comes off Windows' taskbar
// once no layout holds it; and neither echoes back as the other. All of it on a made-up pin store
// and a made-up shell: nothing here reads or touches the taskbar of the PC the tests run on.

namespace Karpo.Tests.Pinning;

public sealed class WindowsPinSyncTests
{
    private const string Brave = @"C:\Users\a\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Brave.lnk";
    private const string Code = @"C:\Users\a\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Visual Studio Code.lnk";
    private const string Outlook = @"shell:AppsFolder\Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows";
    private const string Spotify = @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
    private const string Notepad = @"C:\Windows\System32\notepad.exe";

    /// <summary>Windows' pins as a list a test holds; null reads as "can't be read now".</summary>
    private sealed class FakeStore(params string[] pins) : IWindowsPinStore
    {
        public List<string>? Pins { get; set; } = [.. pins];

        public int Reads { get; private set; }

        public IReadOnlyList<string>? Read()
        {
            Reads++;
            return Pins?.ToList();
        }
    }

    /// <summary>
    /// Windows' shell, made up: it notes what it was asked to unpin and, as Explorer would, takes
    /// it out of the store — unless it is set to refuse, as for a pin Windows offers no verb on.
    /// </summary>
    private sealed class FakeShell(FakeStore store) : IWindowsPinShell
    {
        public List<string> Asked { get; } = [];

        public bool Refuse { get; set; }

        public bool Unpin(string target)
        {
            Asked.Add(target);
            if (!Refuse)
            {
                store.Pins?.RemoveAll(pin => WindowsPinSync.SameTarget(pin, target));
            }
            return !Refuse;
        }
    }

    /// <summary>The identities the dock would resolve, by target: an app id, and the program it starts.</summary>
    private static readonly Dictionary<string, string[]> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Brave names no app id of its own here: its shortcut gets the one Windows makes up from its path.
        [Brave] = [@"aumid:{6d809377-6af0-444b-8957-a3773f02200e}\brave\brave.exe", @"exe:c:\program files\brave\brave.exe"],
        [@"C:\Program Files\Brave\brave.exe"] = [@"exe:c:\program files\brave\brave.exe"],
        [Code] = ["aumid:microsoft.visualstudiocode", @"exe:c:\code\code.exe"],
        [Outlook] = ["aumid:microsoft.outlookforwindows_8wekyb3d8bbwe!microsoft.outlookforwindows"],
        [Spotify] = ["aumid:spotifyab.spotifymusic_zpdnekdrzrea0!spotify"],
        [Notepad] = [@"exe:c:\windows\system32\notepad.exe"],
    };

    private static IReadOnlyCollection<string> KeysOf(string target) =>
        Keys.TryGetValue(target.Trim(), out var keys) ? keys : [];

    private static (WindowsPinSync Sync, FakeStore Store, FakeShell Shell) Start(string[] windows, string[]? remembered = null)
    {
        var store = new FakeStore(windows);
        var shell = new FakeShell(store);
        return (new WindowsPinSync(store, shell, KeysOf, remembered), store, shell);
    }

    private static BarItem Pin(string id, string target)
    {
        var pin = BarKind.Pin.New();
        pin.Id = id;
        pin.Pin!.Target = target;
        return pin;
    }

    private static BarItem Kind(BarKind kind, string id)
    {
        var item = kind.New();
        item.Id = id;
        return item;
    }

    /// <summary><paramref name="items"/> as a bar of Windows 11's: the clock at the end, everything else in the middle.</summary>
    private static List<BarItem> Placed(params BarItem[] items)
    {
        foreach (var item in items)
        {
            item.Section = item.Kind == BarKind.NightLight ? BarSection.End : BarSection.Middle;
        }
        return [.. items];
    }

    /// <summary>
    /// Three layouts: "home" and "work" with pins before the open apps, "spare" with only a clock; each
    /// widget's id its own, as an id is unique across every layout (<see cref="Config.Repair"/>).
    /// </summary>
    private static Config ThreeLayouts()
    {
        var config = new Config
        {
            Layouts =
            [
                new TaskbarLayout { Id = "home", Name = "Home", Islands = Placed(Kind(BarKind.Start, "s"), Pin("n", Notepad), Pin("b", Brave), Kind(BarKind.Apps, "a"), Kind(BarKind.NightLight, "c")) },
                new TaskbarLayout { Id = "work", Name = "Work", Islands = Placed(Kind(BarKind.Start, "ws"), Pin("wb", Brave), Kind(BarKind.Apps, "wa")) },
                new TaskbarLayout { Id = "spare", Name = "Spare", Islands = Placed(Kind(BarKind.NightLight, "sc")) },
            ],
            DefaultLayoutId = "home",
        };
        config.Repair();
        return config;
    }

    private static List<string?> PinsOf(Config config, string layout) =>
        config.Layouts.Single(each => each.Id == layout).Islands.Where(item => item.Kind == BarKind.Pin).Select(item => item.Pin?.Target).ToList();

    private static List<BarKind> KindsOf(Config config, string layout) =>
        config.Layouts.Single(each => each.Id == layout).Islands.Select(item => item.Kind).ToList();

    private static Config Without(Config config, string layout, string id)
    {
        var next = config.Clone();
        Karpo.Core.Folders.Remove(next.Layouts.Single(each => each.Id == layout).Islands, id);
        return next;
    }

    // --- Windows to Karpo: a pin added ---------------------------------------------------------

    [Fact]
    public void WhatWindowsAlreadyPinnedAtTheFirstStartIsNotNew()
    {
        var (sync, _, _) = Start([Brave, Outlook]);

        Assert.Empty(sync.Gained());
        Assert.Equal([Brave, Outlook], sync.Seen);
    }

    [Fact]
    public void APinWindowsGainedIsFoundOnceInWindowsOrder()
    {
        var (sync, store, _) = Start([Brave]);
        store.Pins = [Spotify, Brave, Code];

        Assert.Equal([Spotify, Code], sync.Gained());
        Assert.Empty(sync.Gained());
    }

    [Fact]
    public void APinAddedWhileKarpoWasNotRunningIsFoundAtTheStart()
    {
        var (sync, _, _) = Start([Brave, Outlook], remembered: [Brave]);

        Assert.Equal([Outlook], sync.Gained());
    }

    [Fact]
    public void PinsThatCantBeReadChangeNothing()
    {
        var (sync, store, _) = Start([Brave]);
        store.Pins = null;

        Assert.Empty(sync.Gained());
        Assert.Equal([Brave], sync.Seen);

        // And when they can be read again, what was there before is still not new.
        store.Pins = [Brave, Code];
        Assert.Equal([Code], sync.Gained());
    }

    [Fact]
    public void AGainedPinGoesIntoEveryShownLayoutAfterItsLastPin()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();

        Assert.True(sync.AddTo(config, ["home", "work"], [Outlook]));

        Assert.Equal([Notepad, Brave, Outlook], PinsOf(config, "home"));
        Assert.Equal([Brave, Outlook], PinsOf(config, "work"));
        // After the last pin: before the open apps, which came after it, in the pins' own section.
        Assert.Equal([BarKind.Start, BarKind.Pin, BarKind.Pin, BarKind.Pin, BarKind.Apps, BarKind.NightLight], KindsOf(config, "home"));
        Assert.Equal(BarSection.Middle, config.Layouts[0].Islands[3].Section);
    }

    [Fact]
    public void ALayoutNoDisplayShowsIsLeftAlone()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();

        sync.AddTo(config, ["home"], [Outlook]);

        Assert.DoesNotContain(Outlook, PinsOf(config, "work"));
        Assert.Empty(PinsOf(config, "spare"));
    }

    [Fact]
    public void TwoDisplaysShowingOneLayoutAddItOnce()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();

        sync.AddTo(config, ["work", "work"], [Outlook]);

        Assert.Equal([Brave, Outlook], PinsOf(config, "work"));
    }

    [Fact]
    public void SeveralGainedPinsKeepWindowsOrderAfterTheLastPin()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();

        sync.AddTo(config, ["work"], [Spotify, Outlook]);

        Assert.Equal([Brave, Spotify, Outlook], PinsOf(config, "work"));
    }

    [Fact]
    public void ALayoutThatHoldsTheAppAlreadyIsNotGivenItTwice()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();
        // The same app by another target: the program Brave's shortcut starts.
        config.Layouts[1].Islands[1].Pin!.Target = @"C:\Program Files\Brave\brave.exe";

        Assert.False(sync.AddTo(config, ["home", "work"], [Brave]));

        Assert.Equal([Notepad, Brave], PinsOf(config, "home"));
        Assert.Equal([@"C:\Program Files\Brave\brave.exe"], PinsOf(config, "work"));
    }

    [Fact]
    public void APinInAFolderHoldsItToo()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();
        var home = config.Layouts[0].Islands;
        // Brave's pin taken off the bar and put down a list folder as a line of its own.
        home.RemoveAll(item => item.Id == "b");
        var folder = BarKind.FolderList.New();
        folder.Id = "f";
        var line = ListKind.Pin.New();
        line.Id = "b";
        line.Pin!.Target = Brave;
        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, folder.Folder!, line));
        home.Add(folder);
        Assert.DoesNotContain(home, item => item.Kind == BarKind.Pin && item.Pin?.Target == Brave);

        Assert.False(sync.AddTo(config, ["home"], [Brave]));
    }

    [Fact]
    public void TwoAppsOfOneProgramAreTwoApps()
    {
        // Two web apps of one browser: one program, two app ids.
        Assert.False(WindowsPinSync.SameApp(["aumid:msedge._crx_mail", @"exe:c:\edge\msedge_proxy.exe"], ["aumid:msedge._crx_chat", @"exe:c:\edge\msedge_proxy.exe"]));
        // A pin of the program alone is the app its shortcut starts, where that names the id Windows
        // makes up for the program itself; not where it names an app id of its own.
        Assert.True(WindowsPinSync.SameApp([@"aumid:c:\brave.exe", @"exe:c:\brave.exe"], [@"exe:c:\brave.exe"]));
        Assert.False(WindowsPinSync.SameApp(["aumid:brave", @"exe:c:\brave.exe"], [@"exe:c:\brave.exe"]));
        Assert.True(WindowsPinSync.SameApp(["aumid:brave"], ["aumid:BRAVE"]));
        Assert.False(WindowsPinSync.SameApp([], []));
    }

    /// <summary>
    /// A bar with no pin gets it in the middle section, after the open apps standing there (the
    /// person's decision, 2026-10-05: after the last pin in whichever section holds pins, else in the
    /// middle after the apps).
    /// </summary>
    [Fact]
    public void ABarWithNoPinGetsItInTheMiddleAfterItsOpenApps()
    {
        var list = Placed(Kind(BarKind.Start, "s"), Kind(BarKind.Apps, "a"), Kind(BarKind.NightLight, "c"));
        var pin = Pin("o", Outlook);

        WindowsPinSync.AfterLastPin(new TaskbarLayout { Islands = list }, pin);

        Assert.Equal([BarKind.Start, BarKind.Apps, BarKind.Pin, BarKind.NightLight], list.Select(item => item.Kind));
        Assert.Equal(BarSection.Middle, pin.Section);
    }

    /// <summary>With no pin and no open apps in the middle, at the middle's end — an empty middle too.</summary>
    [Fact]
    public void ABarWithNoPinAndNoAppsInTheMiddleGetsItAtTheMiddlesEnd()
    {
        var weather = Kind(BarKind.WidgetsBoard, "w");
        var apps = Kind(BarKind.Apps, "a");
        var clock = Kind(BarKind.NightLight, "c");
        clock.Section = BarSection.End;
        List<BarItem> list = [weather, apps, clock];
        var pin = Pin("o", Outlook);

        WindowsPinSync.AfterLastPin(new TaskbarLayout { Islands = list }, pin);

        Assert.Equal([BarKind.WidgetsBoard, BarKind.Apps, BarKind.Pin, BarKind.NightLight], list.Select(item => item.Kind));
        Assert.Equal([BarSection.Start, BarSection.Start, BarSection.Middle, BarSection.End], list.Select(item => item.Section));
    }

    /// <summary>The last pin is the last pin in whichever section holds it: here the left, and the new one joins it there.</summary>
    [Fact]
    public void APinGoesAfterTheLastPinInItsSection()
    {
        var first = Pin("n", Notepad);
        var start = Kind(BarKind.Start, "s");
        start.Section = BarSection.Middle;
        var clock = Kind(BarKind.NightLight, "c");
        clock.Section = BarSection.End;
        List<BarItem> list = [first, start, clock];
        var pin = Pin("o", Outlook);

        WindowsPinSync.AfterLastPin(new TaskbarLayout { Islands = list }, pin);

        Assert.Equal(["n", "o", "s", "c"], list.Select(item => item.Id));
        Assert.Equal(BarSection.Start, pin.Section);
    }

    /// <summary>A shown layout with no pin at all gets one too: in its main bar's middle, after its open apps, else at the middle's end.</summary>
    [Fact]
    public void ABarWithNoPinAtAllGetsItInTheMiddle()
    {
        var config = ThreeLayouts();
        var (sync, _, _) = Start([]);

        Assert.True(sync.AddTo(config, ["spare"], [Outlook]));

        Assert.Equal([Outlook], PinsOf(config, "spare"));
        var pin = config.Layouts[2].Islands.Single(item => item.Kind == BarKind.Pin);
        Assert.Equal(BarSection.Middle, Sections.Of(pin));
    }

    /// <summary>
    /// A Dock shows its main middle only: a pin standing in a slot it hides is not where the new one
    /// goes (it would not be seen), though it still holds its app.
    /// </summary>
    [Fact]
    public void ADockGetsItInItsMiddleAfterItsLastPinThere()
    {
        var hidden = Pin("n", Notepad);
        var config = new Config
        {
            Layouts =
            [
                new TaskbarLayout { Id = "dock", Name = "Dock", Type = LayoutType.Dock, Islands = [hidden, .. Placed(Pin("b", Brave), Kind(BarKind.Apps, "a"))] },
                new TaskbarLayout { Id = "bare", Name = "Bare dock", Type = LayoutType.Dock, Islands = [Kind(BarKind.NightLight, "c")] },
            ],
            DefaultLayoutId = "dock",
        };
        config.Repair();
        hidden.Section = BarSection.Start;
        Sections.Order(config.Layouts[0].Islands);
        var (sync, _, _) = Start([]);

        Assert.True(sync.AddTo(config, ["dock", "bare"], [Outlook, Notepad]));

        // After Brave, the last pin in the middle; Notepad is held already, in the hidden start.
        var dock = config.Layouts[0].Islands;
        Assert.Equal([Notepad, Brave, Outlook], PinsOf(config, "dock"));
        Assert.Equal([BarKind.Pin, BarKind.Pin, BarKind.Pin, BarKind.Apps], dock.Select(item => item.Kind));
        Assert.Equal(BarSection.Middle, Sections.Of(dock[2]));
        // A dock with no pin: its middle, the one place it shows; Notepad too, held nowhere there.
        Assert.Equal([Outlook, Notepad], PinsOf(config, "bare"));
        Assert.All(config.Layouts[1].Islands.Where(item => item.Kind == BarKind.Pin), item => Assert.Equal(BarSection.Middle, Sections.Of(item)));
    }

    /// <summary>A pin in another section than the open apps' still holds its app: it is not added again.</summary>
    [Fact]
    public void APinInAnotherSectionHoldsItToo()
    {
        var hidden = Pin("o", Outlook);
        var config = new Config
        {
            Layouts = [new TaskbarLayout { Id = "dock", Name = "Dock", Type = LayoutType.Dock, Islands = [.. Placed(Pin("b", Brave), Kind(BarKind.Apps, "a"), hidden)] }],
            DefaultLayoutId = "dock",
        };
        config.Repair();
        hidden.Section = BarSection.End;
        var (sync, _, _) = Start([]);

        Assert.False(sync.AddTo(config, ["dock"], [Outlook]));

        Assert.Equal([Brave, Outlook], PinsOf(config, "dock"));
    }

    /// <summary>
    /// Follow reads Windows' pins and gives back one config with everything gained in every shown
    /// layout — however many writes came before the read — and null when nothing was: one save, one
    /// rebuild, and none at all for a read that gained nothing.
    /// </summary>
    [Fact]
    public void ABurstOfPinsIsOneConfigAndNothingAfter()
    {
        var (sync, store, _) = Start([Brave]);
        var config = ThreeLayouts();
        // Several writes before the one read the dock makes once they settle.
        store.Pins = [Brave, Outlook];
        store.Pins = [Brave, Outlook, Spotify];

        var next = sync.Follow(config, ["home", "work"]);

        Assert.NotNull(next);
        Assert.Equal([Notepad, Brave, Outlook, Spotify], PinsOf(next, "home"));
        Assert.Equal([Brave, Outlook, Spotify], PinsOf(next, "work"));
        // A layout no display shows is left as it was, and so is the config in force.
        Assert.Empty(PinsOf(next, "spare"));
        Assert.Equal([Notepad, Brave], PinsOf(config, "home"));
        // Read again with nothing new: nothing to save.
        Assert.Null(sync.Follow(next, ["home", "work"]));
        // A pin every shown layout holds already: Windows' pins move on, the config does not.
        store.Pins = [Brave, Outlook, Spotify, Notepad];
        Assert.Null(sync.Follow(next, ["home"]));
        Assert.Contains(Notepad, sync.Seen);
    }

    [Fact]
    public void AnAddedPinGetsAnIdOfItsOwn()
    {
        var (sync, _, _) = Start([]);
        var config = ThreeLayouts();

        sync.AddTo(config, ["home"], [Outlook, Spotify]);

        var ids = Karpo.Core.Folders.All(config.Layouts[0].Islands).Select(entry => entry.Island.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // --- Karpo to Windows: a pin removed -------------------------------------------------------

    [Fact]
    public void RemovedFromOneLayoutWhileAnotherHoldsItStaysOnWindows()
    {
        var (sync, _, shell) = Start([Brave, Outlook]);
        var config = ThreeLayouts();

        var released = sync.Release(config, Without(config, "home", "b"));

        Assert.Empty(released);
        Assert.Empty(shell.Asked);
    }

    [Fact]
    public void RemovedFromTheLastLayoutThatHeldItComesOffWindows()
    {
        var (sync, store, shell) = Start([Brave, Outlook]);
        var config = ThreeLayouts();
        var once = Without(config, "home", "b");

        var released = sync.Release(once, Without(once, "work", "wb"));

        Assert.Equal([Brave], released);
        Assert.Equal([Brave], shell.Asked);
        Assert.Equal([Outlook], store.Pins);
    }

    [Fact]
    public void DeletingTheLastLayoutThatHeldItLetsGoOfItToo()
    {
        var (sync, _, shell) = Start([Brave]);
        var config = ThreeLayouts();
        var once = Without(config, "home", "b");
        var next = once.Clone();
        Assert.True(next.DeleteLayout("work"));

        sync.Release(once, next);

        Assert.Equal([Brave], shell.Asked);
    }

    [Fact]
    public void APinWindowsDoesntHaveAsksNothingOfWindows()
    {
        var (sync, _, shell) = Start([Brave]);
        var config = ThreeLayouts();

        sync.Release(config, Without(config, "home", "n"));

        Assert.Empty(shell.Asked);
    }

    [Fact]
    public void APinWindowsNeverHadInAnyLayoutIsNotTouched()
    {
        // Outlook is on Windows' taskbar and in no layout: no change of Karpo's lets go of it.
        var (sync, _, shell) = Start([Brave, Outlook]);
        var config = ThreeLayouts();

        sync.Release(config, Without(Without(config, "home", "b"), "work", "wb"));

        Assert.Equal([Brave], shell.Asked);
    }

    [Fact]
    public void TheSameAppByAnotherTargetStillHoldsIt()
    {
        var (sync, _, shell) = Start([Brave]);
        var config = ThreeLayouts();
        config.Layouts[1].Islands[1].Pin!.Target = @"C:\Program Files\Brave\brave.exe";

        sync.Release(config, Without(config, "home", "b"));

        Assert.Empty(shell.Asked);
    }

    [Fact]
    public void AChangeThatLetsGoOfNoPinAsksNothing()
    {
        var keysAsked = 0;
        var store = new FakeStore(Brave);
        var shell = new FakeShell(store);
        var sync = new WindowsPinSync(store, shell, target => { keysAsked++; return KeysOf(target); });
        var config = ThreeLayouts();
        var next = config.Clone();
        next.Height = 60;

        Assert.Empty(sync.Release(config, next));
        Assert.Equal(0, keysAsked);
    }

    // --- No echo --------------------------------------------------------------------------------

    [Fact]
    public void KarposOwnUnpinReadBackAddsNothing()
    {
        var (sync, _, _) = Start([Brave, Outlook]);
        var config = ThreeLayouts();
        var once = Without(config, "home", "b");
        var after = Without(once, "work", "wb");
        sync.Release(once, after);

        // Explorer writes its pins, the watcher hears it, and the dock reads them again.
        Assert.Empty(sync.Gained());
        Assert.False(sync.AddTo(after, ["home", "work"], sync.Gained()));
        Assert.Equal([Outlook], sync.Seen);
    }

    [Fact]
    public void AnUnpinWindowsRefusedIsNotFoundAgainAsNew()
    {
        var (sync, _, shell) = Start([Brave]);
        shell.Refuse = true;
        var config = ThreeLayouts();
        var once = Without(config, "home", "b");

        sync.Release(once, Without(once, "work", "wb"));

        Assert.Empty(sync.Gained());
        Assert.Equal([Brave], sync.Seen);
    }

    [Fact]
    public void AddingWindowsPinsToTheLayoutsLetsGoOfNothing()
    {
        var (sync, store, shell) = Start([Brave]);
        var config = ThreeLayouts();
        store.Pins = [Brave, Outlook];
        var next = config.Clone();

        Assert.True(sync.AddTo(next, ["home", "work"], sync.Gained()));
        Assert.Empty(sync.Release(config, next));
        Assert.Empty(shell.Asked);
    }

    [Fact]
    public void TheWholeRoundTripSettles()
    {
        // Windows gains Spotify; Karpo adds it to the shown layout; the person takes it off that
        // bar again; Karpo unpins it; Windows' write is read back. Nothing more happens after that.
        var (sync, store, shell) = Start([Brave]);
        var config = ThreeLayouts();
        store.Pins = [Brave, Spotify];
        var added = config.Clone();
        sync.AddTo(added, ["work"], sync.Gained());
        var id = added.Layouts[1].Islands.Single(item => item.Pin?.Target == Spotify).Id;

        sync.Release(added, Without(added, "work", id));

        Assert.Equal([Spotify], shell.Asked);
        Assert.Empty(sync.Gained());
        Assert.Equal([Brave], store.Pins);
    }
}

/// <summary>Explorer's Favorites value as the pins are read from it, on bytes made up here.</summary>
public sealed class TaskbandFavoritesTests
{
    /// <summary>An item ID list of one root item of <paramref name="clsid"/> and one more item, ended.</summary>
    private static byte[] Pidl(Guid clsid, byte extra)
    {
        var bytes = new List<byte> { 20, 0, 0x1F, 0x80 };
        bytes.AddRange(clsid.ToByteArray());
        bytes.AddRange([5, 0, 0x31, extra, 0]);
        bytes.AddRange([0, 0]);
        return [.. bytes];
    }

    private static byte[] Blob(params byte[][] pidls)
    {
        var bytes = new List<byte>();
        foreach (var pidl in pidls)
        {
            bytes.Add(0);
            bytes.AddRange(BitConverter.GetBytes((uint)pidl.Length));
            bytes.AddRange(pidl);
        }
        bytes.Add(0xFF);
        return [.. bytes];
    }

    private static readonly Guid UserPinned = new("1F3427C8-5C10-4210-AA03-2EE45287D668");

    [Fact]
    public void EntriesReadInOrderAndTheAppsFolderIsToldApart()
    {
        var shortcut = Pidl(UserPinned, 1);
        var app = Pidl(TaskbandFavorites.AppsFolder, 2);

        var entries = TaskbandFavorites.Parse(Blob(shortcut, app, shortcut));

        Assert.NotNull(entries);
        Assert.Equal([false, true, false], entries.Select(entry => entry.InAppsFolder));
        Assert.Equal(app, entries[1].Pidl);
    }

    [Fact]
    public void NoPinsIsAnEmptyList() => Assert.Empty(TaskbandFavorites.Parse([0xFF])!);

    [Theory]
    [InlineData(new byte[0])]
    // No 0xFF at the end.
    [InlineData(new byte[] { 0, 2, 0, 0, 0, 0, 0 })]
    // A length past the end.
    [InlineData(new byte[] { 0, 200, 0, 0, 0, 0, 0, 0xFF })]
    // An entry that doesn't start with 0.
    [InlineData(new byte[] { 7, 2, 0, 0, 0, 0, 0, 0xFF })]
    // An ID list that never ends inside its entry.
    [InlineData(new byte[] { 0, 4, 0, 0, 0, 4, 0, 1, 1, 0xFF })]
    public void AValueNotInExplorersShapeReadsAsUnreadable(byte[] blob) => Assert.Null(TaskbandFavorites.Parse(blob));

    [Fact]
    public void APackagedAppIsPinnedByItsAppId()
    {
        var app = new TaskbandFavorite(Pidl(TaskbandFavorites.AppsFolder, 2), InAppsFolder: true);

        Assert.Equal(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify",
            TaskbandFavorites.Target(app, "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"));
    }

    [Fact]
    public void ADesktopAppIsPinnedByItsShortcut()
    {
        var shortcut = new TaskbandFavorite(Pidl(UserPinned, 1), InAppsFolder: false);
        const string path = @"C:\Users\a\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Brave.lnk";

        Assert.Equal(path, TaskbandFavorites.Target(shortcut, path));
        Assert.Null(TaskbandFavorites.Target(shortcut, "Brave.lnk"));
        Assert.Null(TaskbandFavorites.Target(shortcut, null));
    }
}
