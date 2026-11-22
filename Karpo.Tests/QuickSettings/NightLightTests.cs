// Night light and energy saver as Windows' own tiles switch them (QuickSettings\Services\NightLight.cs,
// SettingHandlers.cs, Switches.cs): through the tile's Settings handler, with night light's state read
// where Windows keeps it, as the Bond structure it is. The values here are made up or copied, and the
// handlers are made-up ones; nothing reads or writes this PC's registry or switches anything on it.

using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class NightLightTests
{
    /// <summary>A value Windows 11 25H2 wrote with night light off, copied byte for byte.</summary>
    private static readonly byte[] Off =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x9C, 0xD1, 0xE3, 0xD5, 0x06, 0x2A,
        0x2B, 0x0E, 0x10, 0x43, 0x42, 0x01, 0x00, 0xC6, 0x14, 0x81, 0xBE, 0x83, 0xB5, 0xEA, 0xCC, 0xD3,
        0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>The same with night light on: the state's field 0 written, and its length two more.</summary>
    private static readonly byte[] On =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x9C, 0xD1, 0xE3, 0xD5, 0x06, 0x2A,
        0x2B, 0x0E, 0x12, 0x43, 0x42, 0x01, 0x00, 0x10, 0x00, 0xC6, 0x14, 0x81, 0xBE, 0x83, 0xB5, 0xEA,
        0xCC, 0xD3, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// The shape the fixed-offset tools were written for, with the state's field 10 (D0 0A 02) as
    /// other PCs keep it: 41 bytes off and 43 on, whose length byte reads 0x13 and 0x15.
    /// </summary>
    private static readonly byte[] OnWithTen =
    [
        0x43, 0x42, 0x01, 0x00, 0x0A, 0x02, 0x01, 0x00, 0x2A, 0x06, 0x9C, 0xD1, 0xE3, 0xD5, 0x06, 0x2A,
        0x2B, 0x0E, 0x15, 0x43, 0x42, 0x01, 0x00, 0x10, 0x00, 0xD0, 0x0A, 0x02, 0xC6, 0x14, 0x81, 0xBE,
        0x83, 0xB5, 0xEA, 0xCC, 0xD3, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>When the copied value was written: its Unix seconds, and the same moment as a FILETIME.</summary>
    private static readonly DateTimeOffset Written = DateTimeOffset.FromUnixTimeSeconds(1790503068);

    [Fact]
    public void TheValueSaysWhetherNightLightIsOn()
    {
        Assert.False(NightLightBlob.IsOn(Off));
        Assert.True(NightLightBlob.IsOn(On));
        // Both times are the moment it was written: one in Unix seconds, one as a FILETIME.
        var state = NightLightBlob.Read(On)!.Value;
        Assert.Equal(Written, DateTimeOffset.FromUnixTimeSeconds((long)state.Written));
        var changed = DateTime.FromFileTimeUtc((long)state.Changed);
        Assert.Equal(Written.UtcDateTime, changed.AddTicks(-(changed.Ticks % TimeSpan.TicksPerSecond)));
    }

    /// <summary>
    /// The state's field 10, where a PC keeps it, is read and kept: the fixed-offset tools' value
    /// reads as on, and switched off it loses field 0 and only that — 43 bytes to 41, 0x15 to 0x13.
    /// </summary>
    [Fact]
    public void FieldTenIsReadAndKept()
    {
        var state = NightLightBlob.Read(OnWithTen)!.Value;
        Assert.True(state.On);
        Assert.Equal(2ul, state.Ten);
        Assert.Equal(OnWithTen, NightLightBlob.Write(state));

        var off = NightLightBlob.Write(state with { On = false });
        Assert.Equal(41, off.Length);
        Assert.Equal(0x13, off[18]);
        Assert.Equal(new byte[] { 0xD0, 0x0A, 0x02 }, off[23..26]);
        Assert.False(NightLightBlob.IsOn(off));
        Assert.Equal(2ul, NightLightBlob.Read(off)!.Value.Ten);
    }

    /// <summary>Writing what was read gives back the same bytes, so the shape is the one Windows writes.</summary>
    [Fact]
    public void WhatIsReadIsWrittenBackTheSame()
    {
        foreach (var value in new[] { Off, On, OnWithTen })
        {
            Assert.Equal(value, NightLightBlob.Write(NightLightBlob.Read(value)!.Value));
        }
    }

    [Fact]
    public void ASwitchWritesTheStateAndBothTimes()
    {
        var now = Written.AddMinutes(5);

        var on = NightLightBlob.Switched(Off, true, now)!;
        var state = NightLightBlob.Read(on)!.Value;
        Assert.True(state.On);
        Assert.Equal((ulong)now.ToUnixTimeSeconds(), state.Written);
        Assert.Equal((ulong)now.ToFileTime(), state.Changed);
        Assert.Equal(Off.Length + 2, on.Length);

        var off = NightLightBlob.Switched(on, false, now.AddMinutes(1))!;
        Assert.False(NightLightBlob.IsOn(off));
        Assert.Equal(Off.Length, off.Length);
    }

    /// <summary>A clock set back still writes a newer value: the service takes an older one for no change.</summary>
    [Fact]
    public void TheTimesOnlyMoveForward()
    {
        var state = NightLightBlob.Read(NightLightBlob.Switched(Off, true, Written.AddDays(-3))!)!.Value;
        var was = NightLightBlob.Read(Off)!.Value;
        Assert.Equal(was.Written + 1, state.Written);
        Assert.Equal(was.Changed + 1, state.Changed);
    }

    /// <summary>
    /// Anything that is not exactly the shape Windows writes is not read, and so never written: a
    /// guessed rewrite of a value the display service reads is not worth a button.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("43 42 01 00")]
    // Version 2 of the marshalling.
    [InlineData("43 42 02 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00")]
    // A state longer than what follows it.
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 09 43 42 01 00 C6 14 01 00 00 00 00")]
    // The structs not all closed, and one closed too many.
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00")]
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00 00")]
    // A field the state does not have here: a bool where the int32 goes.
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 0A 43 42 01 00 02 01 C6 14 01 00 00 00 00")]
    // Field 10 whose number never ends.
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 0A 43 42 01 00 D0 0A FF C6 14 01 00 00 00 00")]
    // A number that never ends.
    [InlineData("43 42 01 00 0A 02 01 00 2A 06 FF FF FF FF FF FF FF FF FF FF 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00")]
    public void WhatIsNotTheShapeIsLeftAlone(string hex)
    {
        var value = hex.Length == 0 ? [] : Convert.FromHexString(hex.Replace(" ", ""));
        Assert.Null(NightLightBlob.Read(value));
        Assert.Null(NightLightBlob.Switched(value, true, Written));
    }

    /// <summary>The smallest value of the shape reads, so each value above fails for its own one fault.</summary>
    [Fact]
    public void TheSmallestValueOfTheShapeReads() =>
        Assert.Equal(
            new NightLightBlob.State(1, false, 1),
            NightLightBlob.Read(Convert.FromHexString("43 42 01 00 0A 02 01 00 2A 06 01 2A 2B 0E 08 43 42 01 00 C6 14 01 00 00 00 00".Replace(" ", ""))));

    /// <summary>A made-up place for the value, with every write kept.</summary>
    private sealed class Store(byte[]? value, bool allow = true) : INightLightStore
    {
        public byte[]? Value { get; set; } = value;

        public List<byte[]> Written { get; } = [];

        public Action? Changed { get; private set; }

        public byte[]? Read() => Value;

        public bool Write(byte[] data)
        {
            Written.Add(data);
            if (allow)
            {
                Value = data;
            }
            return allow;
        }

        public IDisposable? Watch(Action changed)
        {
            Changed = changed;
            return null;
        }
    }

    /// <summary>A made-up tile handler: its value, whether Windows offers it, and every switch asked of it.</summary>
    internal sealed class Tile(bool? value, bool usable = true, bool allow = true) : ISettingToggle
    {
        public bool? Current { get; set; } = value;

        public List<bool> Asked { get; } = [];

        public bool Usable => usable;

        public bool? Value() => Current;

        public bool Set(bool on)
        {
            Asked.Add(on);
            if (allow)
            {
                Current = on;
            }
            return allow;
        }
    }

    private static SemaphoreSlim Started(IQuickSwitch action)
    {
        var told = new SemaphoreSlim(0);
        action.Start(() => told.Release());
        Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");
        return told;
    }

    /// <summary>The tile's own handler is what switches it, and what it says is where it stands.</summary>
    [Fact]
    public void TheHandlerSwitchesNightLight()
    {
        var store = new Store(Off);
        var tile = new Tile(false);
        using var action = new NightLightSwitch(store, () => tile, () => Written);

        Started(action);
        Assert.False(action.On);
        Assert.True(action.Available);

        action.Switch(true);
        Assert.Equal([true], tile.Asked);
        Assert.True(action.On);
        // Through the handler, the value is Windows' to write, never this.
        Assert.Empty(store.Written);
    }

    /// <summary>Where Windows leaves the tile out, the widget is not available either, and nothing is switched.</summary>
    [Fact]
    public void WhereWindowsOffersNoTileNeitherDoesThis()
    {
        using var action = new NightLightSwitch(new Store(Off), () => new Tile(false, usable: false), () => Written);

        Started(action);
        Assert.False(action.Available);
        Assert.Equal("Windows doesn't offer night light for these displays.", action.Note);
    }

    /// <summary>
    /// Regression (2026-10-16): the schedule turned night light off and the button stayed on, because
    /// what the watch read was the handler's stale answer. The value is where it stands, whatever the
    /// handler still says, and each change the schedule writes is followed.
    /// </summary>
    [Fact]
    public void TheValueWinsOverAStaleHandler()
    {
        var store = new Store(On);
        var tile = new Tile(true);
        using var action = new NightLightSwitch(store, () => tile, () => Written);

        var told = Started(action);
        Assert.True(action.On);

        // Sunrise: the schedule writes the value off; the handler's item never hears of it.
        store.Value = Off;
        store.Changed!();
        Assert.False(action.On);
        Assert.True(tile.Current);

        // Sunset: on again.
        store.Value = On;
        store.Changed!();
        Assert.True(action.On);

        // A switch through the handler shows at once, before the watch hears the value it writes.
        action.Switch(false);
        Assert.Equal([false], tile.Asked);
        Assert.False(action.On);
        told.Dispose();
    }

    /// <summary>Where the value does not read, the handler is what says where it stands.</summary>
    [Fact]
    public void TheHandlerAnswersWhereTheValueDoesNot()
    {
        using var action = new NightLightSwitch(new Store(null), () => new Tile(true), () => Written);

        Started(action);
        Assert.True(action.On);
    }

    /// <summary>The watch is on the value's parent key and everything under it, so a key written again from scratch is heard.</summary>
    [Fact]
    public void TheWatchHearsTheValuesKeyMadeAgain() =>
        Assert.Equal(
            @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.bluelightreduction.bluelightreductionstate",
            RegistryNightLight.Parent);

    /// <summary>A handler that does not answer is no state; the value answers in its place.</summary>
    [Fact]
    public void TheValueAnswersWhereTheHandlerDoesNot()
    {
        using var action = new NightLightSwitch(new Store(On), () => new Tile(null), () => Written);

        Started(action);
        Assert.True(action.On);
    }

    [Fact]
    public void WithoutAHandlerTheValueIsFollowedAndWritten()
    {
        var store = new Store(Off);
        using var action = new NightLightSwitch(store, () => null, () => Written.AddMinutes(1));
        Assert.Null(action.On);

        var told = Started(action);
        Assert.False(action.On);
        Assert.True(action.Available);

        action.Switch(true);
        Assert.True(action.On);
        Assert.Single(store.Written);
        Assert.True(NightLightBlob.IsOn(store.Written[0]));

        // Windows' own panel switching it back is heard through the watch.
        store.Value = Off;
        store.Changed!();
        Assert.False(action.On);
        told.Dispose();
    }

    /// <summary>A value it cannot read shows no state and is never written; the click is the settings page's.</summary>
    [Fact]
    public void AValueItCannotReadIsNeverWritten()
    {
        var store = new Store([0x43, 0x42, 0x01, 0x00, 0x00]);
        using var action = new NightLightSwitch(store, () => null, () => Written);

        Started(action);
        Assert.Null(action.On);
        Assert.Contains("opens its settings", action.Note);

        action.Switch(true);
        Assert.Empty(store.Written);
        Assert.Null(action.On);
    }

    /// <summary>No value at all — night light never set up — is the same: nothing is made up and written.</summary>
    [Fact]
    public void NoValueIsNeverMadeUp()
    {
        var store = new Store(null);
        using var action = new NightLightSwitch(store, () => null, () => Written);

        Started(action);
        action.Switch(true);
        Assert.Empty(store.Written);
        Assert.Null(action.On);
    }

    [Fact]
    public void ARefusalIsSaid()
    {
        var store = new Store(Off, allow: false);
        using var action = new NightLightSwitch(store, () => null, () => Written.AddMinutes(1));

        Started(action);
        action.Switch(true);
        Assert.Single(store.Written);
        Assert.False(action.On);
        Assert.Equal("Windows didn't let Karpo switch night light.", action.Note);

        var tile = new Tile(false, allow: false);
        using var through = new NightLightSwitch(new Store(Off), () => tile, () => Written);
        Started(through);
        through.Switch(true);
        Assert.Equal([true], tile.Asked);
        Assert.Equal("Windows didn't let Karpo switch night light.", through.Note);
    }

    // --- Energy saver ---------------------------------------------------------------------------

    /// <summary>
    /// Where energy saver stands is the power status's, whatever the handler says: the flag Windows
    /// raises for it, and no state at all where the status could not be read.
    /// </summary>
    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, false, null)]
    public void EnergySaverIsWhatThePowerStatusSays(bool known, bool battery, bool saving, bool? on)
    {
        using var action = new EnergySaverSwitch(() => null, () => new PowerNow(known, battery, saving), () => false);
        Assert.Equal(on, action.On);
    }

    /// <summary>
    /// Offered where Windows has it — a battery, or 24H2's energy saver on the wall — and where the
    /// tile's handler says it applies.
    /// </summary>
    [Fact]
    public void EnergySaverIsOfferedWhereWindowsHasIt()
    {
        using (var laptop = new EnergySaverSwitch(() => null, () => new PowerNow(true, true, false), () => false))
        {
            Assert.True(laptop.Available);
        }
        using (var desktop = new EnergySaverSwitch(() => null, () => new PowerNow(true, false, false), () => true))
        {
            Assert.True(desktop.Available);
        }
        using (var old = new EnergySaverSwitch(() => null, () => new PowerNow(true, false, false), () => false))
        {
            Assert.False(old.Available);
        }
        using var refused = new EnergySaverSwitch(() => new Tile(false, usable: false), () => new PowerNow(true, true, false), () => true);
        Started(refused);
        Assert.False(refused.Available);
    }

    [Fact]
    public void TheHandlerSwitchesEnergySaver()
    {
        var power = new PowerNow(true, true, false);
        var tile = new Tile(false);
        using var action = new EnergySaverSwitch(() => tile, () => power, () => false);

        Started(action);
        Assert.Contains("background", action.Note);
        action.Switch(true);
        Assert.Equal([true], tile.Asked);
        Assert.DoesNotContain("didn't", action.Note);

        var refusing = new Tile(false, allow: false);
        using var refused = new EnergySaverSwitch(() => refusing, () => power, () => false);
        Started(refused);
        refused.Switch(true);
        Assert.Equal("Windows didn't let Karpo switch energy saver.", refused.Note);
    }

    /// <summary>Without the tile's handler it says so once it has looked, and a click is the settings page's.</summary>
    [Fact]
    public void WithoutAHandlerEnergySaverSaysSo()
    {
        using var action = new EnergySaverSwitch(() => null, () => new PowerNow(true, true, true), () => false);

        Assert.Contains("background", action.Note);
        Started(action);
        Assert.Equal("Windows doesn't let Karpo switch energy saver here; a click opens its settings.", action.Note);
        Assert.True(action.On);
    }
}
