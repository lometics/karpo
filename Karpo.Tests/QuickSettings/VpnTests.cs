// VPN as Windows' own tile and page have it (QuickSettings\Services\Vpn.cs): the VPN preset's list of
// connections, what a click on the switch's line does, and connecting and disconnecting through
// the source. Everything here runs against made-up connections: nothing reads this PC's
// phonebooks, and nothing is ever dialled.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class VpnTests
{
    private static VpnConnection Work(VpnState state = VpnState.Disconnected) => new("Work", @"C:\pbk\rasphone.pbk", state);

    private static VpnConnection Home(VpnState state = VpnState.Disconnected) => new("Home lab", @"C:\pbk\rasphone.pbk", state);

    /// <summary>Starts <paramref name="action"/> and waits for its first read, which comes from the pool.</summary>
    private static SemaphoreSlim Started(VpnSwitch action)
    {
        var told = new SemaphoreSlim(0);
        action.Start(() => told.Release());
        Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");
        return told;
    }

    /// <summary>
    /// Windows' page lists the connections by name, each saying where it stands, the ones up marked;
    /// its settings are the VPN preset's footer, not a row (2026-10-11).
    /// </summary>
    [Fact]
    public void TheLineIsWindowsPage()
    {
        var rows = VpnChoices.List([Work(VpnState.Connected), Home()]);

        Assert.Equal(["Work", "Home lab"], rows.Select(row => row.Text));
        Assert.Equal("Connected", rows[0].Detail);
        Assert.Null(rows[1].Detail);
        Assert.Equal([true, false], rows.Select(row => row.Checked == true));
        Assert.All(rows, row => Assert.True(row.Enabled));
    }

    /// <summary>One connection is a row of its own, as two are; none is a row that says so and does nothing.</summary>
    [Fact]
    public void OneConnectionIsARowAndNoneIsSaid()
    {
        Assert.Equal(["Work"], VpnChoices.List([Work()]).Select(row => row.Text));

        var none = VpnChoices.List([]);
        Assert.Equal(["No VPN connections set up"], none.Select(row => row.Text));
        Assert.False(none[0].Enabled);
    }

    [Fact]
    public void ARowSaysWhereItsConnectionStands()
    {
        Assert.Equal("Connected", VpnChoices.Words(Work(VpnState.Connected)));
        Assert.Equal("Connecting…", VpnChoices.Words(Work(VpnState.Connecting)));
        Assert.Null(VpnChoices.Words(Work()));
        Assert.Equal("Can't connect: The remote connection was not made.",
            VpnChoices.Words(Work() with { Problem = "The remote connection was not made." }));
    }

    /// <summary>What the widget is on as: the connection it is on, or how many.</summary>
    [Fact]
    public void TheDetailIsTheConnectionItIsOn()
    {
        Assert.Null(VpnChoices.Detail([Work(), Home()]));
        Assert.Equal("Work", VpnChoices.Detail([Work(VpnState.Connected), Home()]));
        Assert.Equal("2 connected", VpnChoices.Detail([Work(VpnState.Connected), Home(VpnState.Connected)]));
        // Connecting is not connected: the card says so only once it is.
        Assert.Null(VpnChoices.Detail([Work(VpnState.Connecting)]));
    }

    /// <summary>
    /// A click that opens no list — a folder's, or the bar's with one connection — disconnects what
    /// is up; connects the only one, or the one connected here last; and otherwise leaves the choice
    /// to Windows' own page rather than guessing which the person meant.
    /// </summary>
    [Fact]
    public void AClickWithoutAListDoesWhatItCanKnow()
    {
        Assert.Equal((VpnChoices.Move.Disconnect, Work(VpnState.Connected)), VpnChoices.Plan([Home(), Work(VpnState.Connected)], null));
        Assert.Equal((VpnChoices.Move.Disconnect, Work(VpnState.Connecting)), VpnChoices.Plan([Work(VpnState.Connecting)], null));
        Assert.Equal((VpnChoices.Move.Connect, Work()), VpnChoices.Plan([Work()], null));
        Assert.Equal((VpnChoices.Move.Connect, Home()), VpnChoices.Plan([Work(), Home()], "home lab"));
        Assert.Equal((VpnChoices.Move.Settings, null), VpnChoices.Plan([Work(), Home()], null));
        Assert.Equal((VpnChoices.Move.Settings, null), VpnChoices.Plan([Work(), Home()], "Gone"));
        Assert.Equal((VpnChoices.Move.Settings, null), VpnChoices.Plan([], null));
    }

    [Fact]
    public void TheListIsTheConnectionsSorted() =>
        Assert.Equal(["alpha", "Beta", "gamma"], VpnChoices.Sorted(
        [
            new VpnConnection("gamma", "", VpnState.Disconnected),
            new VpnConnection("alpha", "", VpnState.Disconnected),
            new VpnConnection("Beta", "", VpnState.Disconnected),
        ]).Select(connection => connection.Name));

    /// <summary>A row of the bar's list connects its connection through the source, and the widget follows.</summary>
    [Fact]
    public void ARowConnectsItsConnection()
    {
        var source = new SampleVpn(Home(), Work());
        using var action = new VpnSwitch(() => source);
        Assert.Null(action.On);

        var told = Started(action);
        Assert.False(action.On);
        Assert.Null(action.Detail);

        var rows = action.Choices!;
        Assert.Equal("Work", rows[1].Text);
        action.Choose(1);
        Assert.True(told.Wait(Patience.Ceiling));

        Assert.Equal(["connect Work"], source.Asked);
        Assert.True(action.On);
        Assert.Equal("Work", action.Detail);
        Assert.Equal("Connected", action.Choices![1].Detail);
    }

    /// <summary>The same row disconnects it once it is up.</summary>
    [Fact]
    public void ARowDisconnectsItsConnection()
    {
        var source = new SampleVpn(Home(), Work(VpnState.Connected));
        using var action = new VpnSwitch(() => source);
        var told = Started(action);
        Assert.True(action.On);

        _ = action.Choices;
        action.Choose(1);
        Assert.True(told.Wait(Patience.Ceiling));

        Assert.Equal(["disconnect Work"], source.Asked);
        Assert.False(action.On);
    }

    /// <summary>A failure is said on the connection's row, and the widget stays off.</summary>
    [Fact]
    public void AFailureIsSaidOnItsRow()
    {
        var source = new SampleVpn(Work(), Home()) { Answer = VpnOutcome.Failed };
        using var action = new VpnSwitch(() => source);
        var told = Started(action);

        _ = action.Choices;
        action.Switch(Work(), connect: true);

        Assert.False(action.On);
        Assert.Equal("Can't connect: The remote connection was not made.", action.Choices![0].Detail);
        told.Dispose();
    }

    /// <summary>What was connected here last is what a folder's click connects next, where there are several.</summary>
    [Fact]
    public void TheLastConnectedIsRemembered()
    {
        var source = new SampleVpn(Home(), Work());
        using var action = new VpnSwitch(() => source);
        Started(action);

        action.Switch(Work(), connect: true);
        action.Switch(Work(VpnState.Connected), connect: false);

        Assert.Equal(["connect Work", "disconnect Work"], source.Asked);
        Assert.Equal((VpnChoices.Move.Connect, Work()), VpnChoices.Plan(source.Connections, "Work"));
    }

    /// <summary>
    /// A click is a switch where it connects or lets go without the person: one connection, or one
    /// up; not where several leave the choice to Windows' page, nor for a plug-in's profile, whose
    /// sign-in is its own, nor before the first read.
    /// </summary>
    [Fact]
    public void AClickIsASwitchWhereItNeedsNobody()
    {
        using (var before = new VpnSwitch(() => new SampleVpn(Work())))
        {
            Assert.False(before.Switches);
        }
        foreach (var (connections, switches) in new (VpnConnection[] Connections, bool Switches)[]
        {
            ([Work()], true),
            ([Work(VpnState.Connected), Home()], true),
            ([Work(), Home()], false),
            ([Work() with { ThirdParty = true }], false),
            ([], false),
        })
        {
            using var action = new VpnSwitch(() => new SampleVpn(connections));
            Started(action);
            Assert.Equal(switches, action.Switches);
        }
    }

    /// <summary>The line before the first read says it is looking, and a click on it switches nothing.</summary>
    [Fact]
    public void BeforeTheFirstReadTheListIsLooking()
    {
        var vpn = new SampleVpn(Work());
        using var action = new VpnSwitch(() => vpn);

        var rows = action.Choices!;
        Assert.Equal(["Looking for VPN connections…"], rows.Select(row => row.Text));
        Assert.False(rows[0].Enabled);
        action.Choose(0);
        Assert.Empty(vpn.Asked);
    }
}
