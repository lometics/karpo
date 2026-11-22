// Windows 11's notification center in Karpo (Karpo.Dock\NotificationsFlyout.cs, ToastActivation.cs,
// NotificationStore.cs), the parts that need no window: what a toast offers read from its XML, the
// list grouped by app, every road an activation can take — always through a made-up platform that
// records what it was asked, so no test starts an app — and a dismissal reaching Windows' own list
// through a made-up remover, so no test takes a notification out of the person's list.

using System.Text;
using Karpo.Dock;

namespace Karpo.Tests.Islands;

/// <summary>A platform that records what an activation asked of it and answers what the test says.</summary>
internal sealed class FakePlatform : IActivationPlatform
{
    public Guid? Registered { get; init; }

    public bool Package { get; init; }

    /// <summary>What the activator answers: S_OK, or the HRESULT it fails with.</summary>
    public int Reaches { get; init; }

    /// <summary>What a packaged app's own activation answers.</summary>
    public int Launches { get; init; }

    public bool HasShortcut { get; init; } = true;

    /// <summary>What ShellExecute answers for a target.</summary>
    public Func<string, int> Opens { get; init; } = _ => 0;

    public List<string> Asked { get; } = [];

    public ToastActivation? Called { get; private set; }

    public Guid? Activator(string appId)
    {
        Asked.Add($"activator {appId}");
        return Registered;
    }

    public bool Packaged(string appId) => Package;

    public int Call(Guid activator, ToastActivation activation)
    {
        Asked.Add($"call {activator}");
        Called = activation;
        return Reaches;
    }

    public int LaunchPackaged(string appId, string arguments)
    {
        Asked.Add($"package {appId} {arguments}");
        return Launches;
    }

    public int? LaunchWithArguments(string appId, string arguments)
    {
        Asked.Add($"command line {appId} {arguments}");
        return HasShortcut ? 0 : null;
    }

    public int Open(string target)
    {
        Asked.Add($"open {target}");
        return Opens(target);
    }
}

/// <summary>Windows' list as a test sees it: what it was asked to take out, and whether it was cleared.</summary>
internal sealed class FakeRemover : INotificationRemover
{
    public List<long> Removed { get; } = [];

    public int Cleared { get; private set; }

    public void Remove(IReadOnlyList<long> ids) => Removed.AddRange(ids);

    public void Clear() => Cleared++;
}

/// <summary>An activator that only writes down what it was handed.</summary>
internal sealed class FakeActivator : IToastActivator
{
    public List<ToastActivation> Activated { get; } = [];

    public void Activate(ToastActivation activation) => Activated.Add(activation);
}

public sealed class NotificationCentreTests : IDisposable
{
    private const string Chat = "Contoso.Chat_8wekyb3d8bbwe!App";
    private const string Mail = "Contoso.Mail";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "centre-" + Guid.NewGuid().ToString("N"));

    public NotificationCentreTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A chat message as Windows' samples write one: a reply box, its send button, a like, and Windows' own dismiss.</summary>
    internal const string Reply =
        "<toast launch=\"action=view&amp;id=9318\"><visual><binding template=\"ToastGeneric\"><text>Ada</text><text>Lunch at noon?</text></binding></visual>"
        + "<actions hint-systemCommands=\"SnoozeAndDismiss\">"
        + "<input id=\"reply\" type=\"text\" placeHolderContent=\"Type a reply\"/>"
        + "<input id=\"when\" type=\"selection\" title=\"Remind me\" defaultInput=\"15\"><selection id=\"5\" content=\"5 minutes\"/><selection id=\"15\" content=\"15 minutes\"/></input>"
        + "<action content=\"Send\" arguments=\"action=reply&amp;id=9318\" activationType=\"background\" hint-inputId=\"reply\"/>"
        + "<action content=\"Like\" arguments=\"action=like&amp;id=9318\" activationType=\"background\"/>"
        + "<action content=\"Open the thread\" arguments=\"contoso-chat:thread/9318\" activationType=\"protocol\"/>"
        + "<action content=\"Mute\" arguments=\"action=mute\" placement=\"contextMenu\"/>"
        + "</actions></toast>";

    [Fact]
    public void AToastsButtonsAndBoxesAreReadFromItsXml()
    {
        var toast = ToastXml.Parse(Reply);

        Assert.NotNull(toast);
        var offer = toast.Value.Offer;
        Assert.Equal("action=view&id=9318", offer.Launch);
        Assert.Equal(ToastActivationKind.Foreground, offer.Kind);
        Assert.True(offer.Actionable);
        // The context menu's button is the right-click menu's; Windows' own Dismiss joins the rest,
        // and its snooze, which only Windows can do, does not.
        Assert.Equal(["Send", "Like", "Open the thread", "Dismiss"], offer.Buttons.Select(button => button.Content));
        Assert.Equal(
            [ToastActivationKind.Background, ToastActivationKind.Background, ToastActivationKind.Protocol, ToastActivationKind.System],
            offer.Buttons.Select(button => button.Kind));
        Assert.Equal("reply", offer.Buttons[0].InputId);
        Assert.Equal("action=reply&id=9318", offer.Buttons[0].Arguments);
        Assert.True(offer.Buttons[3].Dismisses);
        var reply = offer.Inputs[0];
        Assert.Equal(("reply", ToastInputKind.Text, "Type a reply", ""), (reply.Id, reply.Kind, reply.Placeholder, reply.Default));
        var when = offer.Inputs[1];
        Assert.Equal((ToastInputKind.Selection, "Remind me", "15"), (when.Kind, when.Title, when.Default));
        Assert.Equal(["5", "15"], when.Choices.Select(choice => choice.Id));
    }

    [Fact]
    public void AToastOfWordsAloneOffersOnlyItsLaunch()
    {
        var plain = ToastXml.Parse("<toast><visual><binding template=\"ToastGeneric\"><text>Hello</text></binding></visual></toast>");
        var protocol = ToastXml.Parse("<toast launch=\"https://example.com/a\" activationType=\"protocol\"><visual><binding template=\"ToastGeneric\"><text>Hi</text></binding></visual></toast>");

        Assert.Equal("", plain!.Value.Offer.Launch);
        Assert.False(plain.Value.Offer.Actionable);
        Assert.Equal(("https://example.com/a", ToastActivationKind.Protocol), (protocol!.Value.Offer.Launch, protocol.Value.Offer.Kind));
    }

    /// <summary>Windows shows five buttons and five inputs at the most, and a button needs words or it is nothing.</summary>
    [Fact]
    public void AtMostFiveOfEachAndNoneWithoutWords()
    {
        var actions = string.Concat(Enumerable.Range(1, 7).Select(n => $"<action content=\"B{n}\" arguments=\"{n}\"/>"))
            + "<action content=\"  \" arguments=\"empty\"/>"
            + string.Concat(Enumerable.Range(1, 7).Select(n => $"<input id=\"i{n}\" type=\"text\"/>"));
        var toast = ToastXml.Parse($"<toast><visual><binding template=\"ToastGeneric\"><text>t</text></binding></visual><actions>{actions}</actions></toast>");

        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], toast!.Value.Offer.Buttons.Select(button => button.Content));
        Assert.Equal(5, toast.Value.Offer.Inputs.Count);
    }

    /// <summary>
    /// Grouped by app as Windows 11's notification center groups it: an app's notifications together,
    /// newest first, and the app something last came from at the top. Arrivals in one instant keep
    /// their ids' order.
    /// </summary>
    [Fact]
    public void TheListIsGroupedByAppNewestFirst()
    {
        NotificationEntry Entry(long id, string app, int minute) =>
            new(id, app, app == Chat ? "Chat" : "Mail", null, $"n{id}", "", new DateTime(2026, 9, 27, 12, minute, 0), null);
        var items = new[] { Entry(1, Mail, 1), Entry(2, Chat, 5), Entry(3, Mail, 9), Entry(4, Chat, 2), Entry(5, Chat, 9) };

        var groups = NotificationText.Groups(items);

        Assert.Equal([Chat, Mail], groups.Select(group => group.AppId));
        Assert.Equal(["Chat", "Mail"], groups.Select(group => group.AppName));
        Assert.Equal([5L, 2L, 4L], groups[0].Items.Select(item => item.Id));
        Assert.Equal([3L, 1L], groups[1].Items.Select(item => item.Id));
        // Two in the same instant: the newer id first, and its app first.
        Assert.Equal(5L, groups[0].Items[0].Id);
        Assert.Empty(NotificationText.Groups([]));
    }

    // --- How an activation reaches its app -------------------------------------------------------

    private static NotificationEntry Toast(string app, string xml) =>
        ToastXml.Parse(xml) is { } toast
            ? new NotificationEntry(7, app, "App", null, toast.Title, toast.Body, new DateTime(2026, 9, 27, 12, 0, 0), null, toast.Offer)
            : throw new InvalidOperationException("the toast does not parse");

    /// <summary>
    /// The app's own COM activator where it registered one, handed the arguments and every input's
    /// value — what was typed, the default of what was not — as Windows calls it.
    /// </summary>
    [Fact]
    public void AButtonReachesTheAppsActivatorWithEveryInput()
    {
        var entry = Toast(Chat, Reply);
        var send = entry.Offer!.Buttons[0];
        var activator = Guid.NewGuid();
        var platform = new FakePlatform { Registered = activator };

        var activation = ToastActivation.Of(entry, send, new Dictionary<string, string> { ["reply"] = "On my way" });
        var route = ToastActivator.Run(activation, platform).Route;

        Assert.Equal(ActivationRoute.Activator, route);
        Assert.Equal([$"activator {Chat}", $"call {activator}"], platform.Asked);
        Assert.Equal(Chat, platform.Called!.AppId);
        Assert.Equal("action=reply&id=9318", platform.Called.Arguments);
        Assert.Equal(ToastActivationKind.Background, platform.Called.Kind);
        Assert.False(platform.Called.Closes);
        Assert.Equal(
            [new KeyValuePair<string, string>("reply", "On my way"), new KeyValuePair<string, string>("when", "15")],
            platform.Called.Inputs);
    }

    /// <summary>A click on the notification itself: the toast's own launch arguments, the way its own activation type says.</summary>
    [Fact]
    public void AClickOnTheToastIsItsLaunch()
    {
        var entry = Toast(Chat, Reply);

        var launch = ToastActivation.Launch(entry);

        Assert.Equal((Chat, "action=view&id=9318", ToastActivationKind.Foreground), (launch.AppId, launch.Arguments, launch.Kind));
        Assert.Empty(launch.Inputs);
        Assert.True(launch.Closes);
        // A notification of its words alone: its app, with nothing to hand over.
        var plain = new NotificationEntry(1, Mail, "Mail", null, "t", "", DateTime.Now, null);
        Assert.Equal(("", ToastActivationKind.Foreground), (ToastActivation.Launch(plain).Arguments, ToastActivation.Launch(plain).Kind));
    }

    /// <summary>A packaged app with no activator gets its own activation with the arguments.</summary>
    [Fact]
    public void APackagedAppWithoutAnActivatorIsLaunchedWithTheArguments()
    {
        var platform = new FakePlatform { Package = true };

        var route = ToastActivator.Run(new ToastActivation(Chat, "action=view", ToastActivationKind.Foreground, []), platform).Route;

        Assert.Equal(ActivationRoute.Package, route);
        Assert.Equal([$"activator {Chat}", $"package {Chat} action=view"], platform.Asked);
    }

    /// <summary>
    /// A desktop app with no activator is started with the arguments on its command line — what
    /// Windows does for a shortcut with a CLSID and no server — and where there is no shortcut to
    /// start, or nothing to hand over, the app opens as it always did.
    /// </summary>
    [Fact]
    public void ADesktopAppWithoutAnActivatorGetsItsArgumentsOnItsCommandLine()
    {
        var shortcut = new FakePlatform();
        Assert.Equal(ActivationRoute.CommandLine, ToastActivator.Run(new ToastActivation(Mail, "--open 42", ToastActivationKind.Foreground, []), shortcut).Route);
        Assert.Equal([$"activator {Mail}", $"command line {Mail} --open 42"], shortcut.Asked);

        var none = new FakePlatform { HasShortcut = false };
        Assert.Equal(ActivationRoute.App, ToastActivator.Run(new ToastActivation(Mail, "--open 42", ToastActivationKind.Foreground, []), none).Route);
        Assert.Equal($@"open shell:AppsFolder\{Mail}", none.Asked[^1]);

        var nothing = new FakePlatform();
        Assert.Equal(ActivationRoute.App, ToastActivator.Run(new ToastActivation(Mail, "", ToastActivationKind.Foreground, []), nothing).Route);
        Assert.Equal([$"activator {Mail}", $@"open shell:AppsFolder\{Mail}"], nothing.Asked);
    }

    private const int ClassNotRegistered = unchecked((int)0x80040154);

    /// <summary>
    /// An activator that cannot be reached is not the end: the next road is taken, and what failed is
    /// one line for karpo.log — the app, how it was asked, the activator and its HRESULT, what came of
    /// it — never the arguments, which are the person's.
    /// </summary>
    [Fact]
    public void AnActivatorThatDoesNotAnswerFallsThroughAndIsLogged()
    {
        var activator = Guid.NewGuid();
        var platform = new FakePlatform { Registered = activator, Reaches = ClassNotRegistered, Package = true };
        var activation = new ToastActivation(Chat, "secret=42", ToastActivationKind.Foreground, []);

        var outcome = ToastActivator.Run(activation, platform);

        Assert.Equal(ActivationRoute.Package, outcome.Route);
        Assert.Equal([new ActivationFailure(ActivationRoute.Activator, $"{activator:B} 0x80040154")], outcome.Failed);
        Assert.Equal($"Notifications: activating {Chat} (Foreground): Activator {activator:B} 0x80040154; reached by Package", ToastActivator.Line(activation, outcome));
        Assert.DoesNotContain("secret", ToastActivator.Line(activation, outcome)!, StringComparison.Ordinal);
        // An activation that went the first way it could says nothing.
        Assert.Null(ToastActivator.Line(activation, ToastActivator.Run(activation, new FakePlatform { Registered = activator })));
    }

    /// <summary>
    /// Every road failing — the packaged app's activation, then opening it — still leaves its line,
    /// which says nothing was reached.
    /// </summary>
    [Fact]
    public void WhereEveryRoadFailsTheLineSaysSo()
    {
        var platform = new FakePlatform { Package = true, Launches = ClassNotRegistered, HasShortcut = false, Opens = _ => unchecked((int)0x80070002) };
        var activation = new ToastActivation(Chat, "x", ToastActivationKind.Foreground, []);

        var outcome = ToastActivator.Run(activation, platform);

        Assert.Equal(ActivationRoute.Nothing, outcome.Route);
        Assert.Equal(
            [new ActivationFailure(ActivationRoute.Package, "0x80040154"), new ActivationFailure(ActivationRoute.App, "0x80070002")],
            outcome.Failed);
        Assert.EndsWith("Package 0x80040154; App 0x80070002; nothing reached", ToastActivator.Line(activation, outcome), StringComparison.Ordinal);
    }

    /// <summary>
    /// A protocol activation launches its URI, and only a URI: a toast any app can post does not get
    /// to name a program on the disk for Karpo to run. Where its URI is refused, or Windows cannot
    /// launch it, the app opens instead, as Windows opens it, and the line says why. Windows' own
    /// activations are not Karpo's to make.
    /// </summary>
    [Fact]
    public void AProtocolActivationLaunchesOnlyAUriElseOpensTheApp()
    {
        var platform = new FakePlatform();
        Assert.Equal(ActivationRoute.Uri, ToastActivator.Run(new ToastActivation(Chat, "contoso-chat:thread/9318", ToastActivationKind.Protocol, []), platform).Route);
        Assert.Equal(["open contoso-chat:thread/9318"], platform.Asked);

        foreach (var refused in new[] { @"C:\Windows\System32\calc.exe", "file:///C:/Windows/System32/calc.exe", "calc.exe", "", "1http://x" })
        {
            var app = new FakePlatform();
            var outcome = ToastActivator.Run(new ToastActivation(Chat, refused, ToastActivationKind.Protocol, []), app);
            Assert.Equal(ActivationRoute.App, outcome.Route);
            Assert.Equal([$@"open shell:AppsFolder\{Chat}"], app.Asked);
            Assert.Equal(ActivationRoute.Uri, Assert.Single(outcome.Failed).Tried);
        }

        // No handler for the scheme: Windows says so, and the app opens.
        var unhandled = new FakePlatform { Opens = target => target.StartsWith("contoso", StringComparison.Ordinal) ? unchecked((int)0x80070483) : 0 };
        var launched = ToastActivator.Run(new ToastActivation(Chat, "contoso-chat:thread/9318", ToastActivationKind.Protocol, []), unhandled);
        Assert.Equal(ActivationRoute.App, launched.Route);
        Assert.Equal([new ActivationFailure(ActivationRoute.Uri, "0x80070483")], launched.Failed);

        var system = new FakePlatform();
        var dismissed = ToastActivator.Run(new ToastActivation(Chat, "dismiss", ToastActivationKind.System, []), system);
        Assert.Equal(ActivationRoute.Nothing, dismissed.Route);
        Assert.Empty(dismissed.Failed);
        Assert.Empty(system.Asked);
    }

    [Theory]
    [InlineData("Microsoft.OutlookForWindows_8wekyb3d8bbwe!Microsoft.OutlookforWindows", "Microsoft.OutlookForWindows_8wekyb3d8bbwe")]
    [InlineData("Chrome", null)]
    [InlineData(@"{6D809377-6AF0-444B-8957-A3773F02200E}\Tool\tool.exe", null)]
    [InlineData("oddly!", null)]
    public void APackagedAppIsKnownByItsFamily(string aumid, string? family) =>
        Assert.Equal(family, WindowsActivationPlatform.PackageFamily(aumid));

    /// <summary>A run that may not reach the person's world is given an activator that reaches nothing.</summary>
    [Fact]
    public void ATestProcessNeverActivatesAnApp() =>
        Assert.IsNotType<WindowsToastActivator>(ToastActivator.Shared);

    // --- Dismissing clears Windows' own list -----------------------------------------------------

    private string Database => Path.Combine(folder, "db", "wpndatabase.db");

    private void Build(params (long Id, string App, int Minutes, string Payload)[] rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Database)!);
        Assert.Equal(Sqlite.OK, Sqlite.Open(Database, Sqlite.OPEN_READWRITE | Sqlite.OPEN_CREATE, out var db));
        try
        {
            var now = DateTime.UtcNow.ToFileTimeUtc();
            var sql = new StringBuilder(
                "CREATE TABLE Notification (\"Order\" INTEGER PRIMARY KEY AUTOINCREMENT, Id INTEGER NOT NULL, HandlerId INTEGER, Type TEXT NOT NULL, "
                + "Payload BLOB, ExpiryTime INT64, ArrivalTime INT64, PayloadType TEXT NOT NULL);"
                + $"CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY, PrimaryId TEXT NOT NULL); INSERT INTO NotificationHandler VALUES (1, '{Chat}'), (2, '{Mail}');");
            foreach (var row in rows)
            {
                sql.Append($"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) VALUES ({row.Id}, {(row.App == Chat ? 1 : 2)}, 'toast', "
                    + $"X'{Convert.ToHexString(Encoding.UTF8.GetBytes(row.Payload))}', 0, {now + row.Minutes * TimeSpan.TicksPerMinute}, 'Xml');");
            }
            Assert.Equal(Sqlite.OK, Sqlite.Execute(db, sql.ToString()));
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    private static string Plain(string title) => $"<toast><visual><binding template=\"ToastGeneric\"><text>{title}</text></binding></visual></toast>";

    /// <summary>
    /// One, an app's and all of them go to Windows' list — the listener's ids are the database's —
    /// and are hidden in Karpo at once whatever Windows does next.
    /// </summary>
    [Fact]
    public void DismissingReachesWindowsOwnList()
    {
        Build((11, Chat, -1, Reply), (12, Chat, -2, Plain("b")), (13, Mail, -3, Plain("c")), (14, Mail, -4, Plain("d")));
        var windows = new FakeRemover();
        var store = new NotificationStore(Database, Path.Combine(folder, "copy"), Path.Combine(folder, "state"), app => (app, null), windows);
        store.Prepare();

        store.Dismiss(12);
        Assert.Equal([12L], windows.Removed);
        Assert.DoesNotContain(store.Items, item => item.Id == 12);

        store.DismissApp(Mail);
        Assert.Equal([12L, 13L, 14L], windows.Removed);
        Assert.Equal([11L], store.Items.Select(item => item.Id));

        store.DismissAll();
        Assert.Equal(1, windows.Cleared);
        Assert.Empty(store.Items);

        // What is not there is not asked for.
        store.Dismiss(99);
        store.DismissApp("nobody");
        Assert.Equal([12L, 13L, 14L], windows.Removed);
    }

    /// <summary>The toast's offer comes through the store with it, from the database's payload to the entry the list draws.</summary>
    [Fact]
    public void WhatAToastOffersComesThroughTheStore()
    {
        Build((21, Chat, -1, Reply), (22, Mail, -2, Plain("plain")));
        var store = new NotificationStore(Database, Path.Combine(folder, "copy"), Path.Combine(folder, "state"), app => (app, null));
        store.Prepare();

        var reply = store.Items.Single(item => item.Id == 21);
        Assert.Equal("action=view&id=9318", reply.Offer!.Launch);
        Assert.Equal(4, reply.Offer.Buttons.Count);
        Assert.False(store.Items.Single(item => item.Id == 22).Offer!.Actionable);
    }

    /// <summary>A test process's shared store clears nothing in Windows: an isolated run reads the list and leaves it be.</summary>
    [Fact]
    public void ATestProcessNeverClearsWindowsList()
    {
        Exception? failed = null;
        var thread = new Thread(() => failed = Record.Exception(() => Assert.False(NotificationStore.Shared.ClearsWindows)));
        thread.Start();
        thread.Join();
        Assert.Null(failed);
    }
}
