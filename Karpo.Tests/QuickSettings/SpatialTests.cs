// Spatial sound for the output in use (QuickSettings\Services\Spatial.cs): the formats an output
// offers, read from the blobs its property store keeps as Windows' page reads them, what a click
// turns on, and the switch that asks Windows for a format. Everything is made up: nothing
// reads or changes this PC's sound.

using System.Text;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class SpatialTests
{
    private const string Sonic = SpatialFormats.WindowsSonic;
    private const string Atmos = "{1459AC38-3875-49BF-BB59-0FE80F4D395D}";
    private const string HeadphoneX = "{4444ACB0-8DC0-4C2C-A0D8-2C76DB470F86}";

    /// <summary>
    /// A format's blob as an output's property store keeps it: 834 bytes, the name at 0, the Store
    /// page of its app at 512, its GUID at 768, and at 784 whether it is there to be chosen.
    /// </summary>
    private static byte[] Blob(string name, string id, uint there, string store = "", int length = 834)
    {
        var blob = new byte[length];
        Encoding.Unicode.GetBytes(name).CopyTo(blob, 0);
        if (store.Length > 0 && length >= 768)
        {
            Encoding.Unicode.GetBytes(store).CopyTo(blob, 512);
        }
        if (length >= 784)
        {
            Guid.Parse(id).ToByteArray().CopyTo(blob, 768);
        }
        if (length >= 788)
        {
            BitConverter.GetBytes(there).CopyTo(blob, 784);
        }
        return blob;
    }

    /// <summary>A format there to be chosen reads as its name and its GUID, as Windows writes it.</summary>
    [Fact]
    public void AFormatReadsFromItsBlob()
    {
        Assert.Equal(new SpatialFormat(Sonic, "Windows Sonic for Headphones"), SpatialFormats.From(Blob("Windows Sonic for Headphones", Sonic, 1)));
        Assert.Equal(new SpatialFormat(Atmos, "Dolby Atmos for Headphones"),
            SpatialFormats.From(Blob("Dolby Atmos for Headphones", Atmos.ToLowerInvariant(), 1, "ms-windows-store://pdp/?productid=9n0866fs04w8")));
    }

    /// <summary>
    /// A format whose app is not installed is not offered, as Windows' quick list leaves it out; and a
    /// blob that is not the shape — short, nameless, or no format at all — is skipped.
    /// </summary>
    [Fact]
    public void WhatIsNotThereToChooseIsLeftOut()
    {
        Assert.Null(SpatialFormats.From(Blob("Dolby Atmos for Headphones", Atmos, 0, "ms-windows-store://pdp/?productid=9n0866fs04w8")));
        Assert.Null(SpatialFormats.From(Blob("Windows Sonic for Headphones", Sonic, 1, length: 786)));
        Assert.Null(SpatialFormats.From(Blob("", Sonic, 1)));
        Assert.Null(SpatialFormats.From(Blob("Nothing", SpatialFormats.Off, 1)));
        Assert.Null(SpatialFormats.From([]));
    }

    /// <summary>A card says a format in its short form, which is what it is without what it is for.</summary>
    [Theory]
    [InlineData("Windows Sonic for Headphones", "Windows Sonic")]
    [InlineData("Dolby Atmos for Home Theater", "Dolby Atmos")]
    [InlineData("DTS Headphone:X", "DTS Headphone:X")]
    public void AFormatsShortFormIsWhatItIs(string name, string shortName) => Assert.Equal(shortName, SpatialFormats.Short(name));

    [Fact]
    public void OffIsNoneOrTheNullGuid()
    {
        Assert.True(SpatialFormats.IsOff(SpatialFormats.Off));
        Assert.True(SpatialFormats.IsOff(null));
        Assert.True(SpatialFormats.IsOff(""));
        Assert.True(SpatialFormats.IsOff("not a guid"));
        Assert.False(SpatialFormats.IsOff(Sonic));
        Assert.True(SpatialFormats.Same(Sonic, Sonic.ToLowerInvariant().Trim('{', '}')));
    }

    /// <summary>What a format is called: the output's own words for it, Windows Sonic's where the output has none, nothing for off.</summary>
    [Fact]
    public void AFormatIsCalledAsTheOutputCallsIt()
    {
        var dolby = new SpatialState(true, Atmos, [new(Atmos, "Dolby Atmos for Headphones")]);
        Assert.Equal("Dolby Atmos for Headphones", SpatialFormats.NameOf(dolby, Atmos));
        Assert.Equal("Windows Sonic for Headphones", SpatialFormats.NameOf(dolby, Sonic));
        Assert.Null(SpatialFormats.NameOf(dolby, SpatialFormats.Off));
    }

    /// <summary>A click turns it off; on again, the format last used here, else the first offered.</summary>
    [Fact]
    public void AClickSwitchesOffAndBack()
    {
        var formats = new List<SpatialFormat> { new(Sonic, "Windows Sonic for Headphones"), new(Atmos, "Dolby Atmos for Headphones") };
        Assert.Equal(SpatialFormats.Off, SpatialFormats.Next(new SpatialState(true, Atmos, formats), null));
        Assert.Equal(Atmos, SpatialFormats.Next(new SpatialState(true, SpatialFormats.Off, formats), Atmos));
        Assert.Equal(Sonic, SpatialFormats.Next(new SpatialState(true, SpatialFormats.Off, formats), null));
        Assert.Equal(Sonic, SpatialFormats.Next(new SpatialState(true, SpatialFormats.Off, formats), "{00000000-0000-0000-0000-000000000001}"));
        Assert.Null(SpatialFormats.Next(new SpatialState(true, SpatialFormats.Off, []), null));
    }

    /// <summary>
    /// The format the widget's card names is what a click turns on, before the one last used — where
    /// the output offers it; where not, the click turns on what it would have. Off is off whatever is chosen.
    /// </summary>
    [Fact]
    public void AClickTurnsOnTheFormatTheCardNames()
    {
        var formats = new List<SpatialFormat> { new(Sonic, "Windows Sonic for Headphones"), new(Atmos, "Dolby Atmos for Headphones") };
        var off = new SpatialState(true, SpatialFormats.Off, formats);
        Assert.Equal(Atmos, SpatialFormats.Next(off, last: null, chosen: Atmos));
        Assert.Equal(Atmos, SpatialFormats.Next(off, last: Sonic, chosen: Atmos.ToLowerInvariant()));
        Assert.Equal(Sonic, SpatialFormats.Next(off, last: Sonic, chosen: HeadphoneX));
        Assert.Equal(Sonic, SpatialFormats.Next(off, last: null, chosen: HeadphoneX));
        Assert.Equal(SpatialFormats.Off, SpatialFormats.Next(new SpatialState(true, Atmos, formats), last: null, chosen: Atmos));
    }

    /// <summary>The formats Settings offers by name are the ones Windows writes (Karpo.Core's list, the dock's GUIDs).</summary>
    [Fact]
    public void SettingsNamesTheFormatsAsWindowsWritesThem()
    {
        Assert.Equal(Sonic, SpatialSoundFormats.Known[0].Id);
        Assert.Contains(SpatialSoundFormats.Known, format => SpatialFormats.Same(format.Id, Atmos));
        Assert.Contains(SpatialSoundFormats.Known, format => SpatialFormats.Same(format.Id, HeadphoneX));
        Assert.All(SpatialSoundFormats.Known, format =>
        {
            Assert.False(SpatialFormats.IsOff(format.Id));
            Assert.Equal(SpatialFormats.Id(Guid.Parse(format.Id)), format.Id);
        });
        Assert.Equal(SpatialSoundFormats.Known.Length, SpatialSoundFormats.Known.Select(format => format.Id).Distinct().Count());
    }

    private static SemaphoreSlim Started(SpatialSwitch action)
    {
        var told = new SemaphoreSlim(0);
        action.Start(() => told.Release());
        Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");
        return told;
    }

    /// <summary>
    /// A click on the widget asks Windows for the format its card names, and the widget follows, named
    /// by it; a second click turns it off. It opens no list: spatial sound is a toggle (2026-10-11).
    /// </summary>
    [Fact]
    public void AClickAsksForTheCardsFormat()
    {
        var source = new SampleSpatial(new SpatialState(true, SpatialFormats.Off, [new(Sonic, "Windows Sonic for Headphones"), new(Atmos, "Dolby Atmos for Headphones")]));
        using var action = new SpatialSwitch(() => source) { Refused = () => { } };
        Assert.Null(action.On);
        Assert.Null(((IQuickSwitch)action).Choices);

        var told = Started(action);
        Assert.False(action.On);
        Assert.True(action.Available);

        var options = new SwitchOptions { SpatialFormat = Atmos };
        action.Act(options);
        Assert.True(told.Wait(Patience.Ceiling));
        Assert.Equal([Atmos], source.Asked);
        Assert.True(action.On);
        Assert.Equal("Dolby Atmos", action.Detail);

        action.Act(options);
        Assert.True(told.Wait(Patience.Ceiling));
        Assert.Equal([Atmos, SpatialFormats.Off], source.Asked);
        Assert.False(action.On);
    }

    /// <summary>Turned off and on again without a list, it comes back as it was.</summary>
    [Fact]
    public void OffAndOnAgainIsTheSameFormat()
    {
        var source = new SampleSpatial(new SpatialState(true, Atmos, [new(Sonic, "Windows Sonic for Headphones"), new(Atmos, "Dolby Atmos for Headphones")]));
        using var action = new SpatialSwitch(() => source) { Refused = () => { } };
        Started(action);

        action.Switch(SpatialFormats.Next(source.State, null)!);
        Assert.False(action.On);
        action.Switch(SpatialFormats.Next(source.State, Atmos)!);

        Assert.Equal([SpatialFormats.Off, Atmos], source.Asked);
        Assert.Equal("Dolby Atmos", action.Detail);
    }

    /// <summary>Where Windows refuses — it keeps the choice for Settings — the widget says so and hands the person to the settings.</summary>
    [Fact]
    public void ARefusalOpensTheSettings()
    {
        var refused = 0;
        var source = new SampleSpatial(new SpatialState(true, SpatialFormats.Off, [new(Sonic, "Windows Sonic for Headphones")]), allow: false);
        using var action = new SpatialSwitch(() => source) { Refused = () => refused++ };
        Started(action);

        action.Switch(Sonic);

        Assert.Equal(1, refused);
        Assert.False(action.On);
        Assert.Equal("Windows didn't let Karpo change spatial sound; its sound settings open instead.", action.Note);
    }

    /// <summary>An output that does no spatial sound is not available, and says why.</summary>
    [Fact]
    public void AnOutputWithoutItIsNotAvailable()
    {
        using var action = new SpatialSwitch(() => new SampleSpatial(new SpatialState(false, SpatialFormats.Off, []))) { Refused = () => { } };
        Assert.True(action.Available);

        Started(action);

        Assert.False(action.Available);
        Assert.Equal("The output in use doesn't do spatial sound.", action.Note);
    }

    /// <summary>
    /// A click is a switch where the output has a format to switch to and Windows has not refused
    /// one: after a refusal a click opens the sound settings, which is not a switch.
    /// </summary>
    [Fact]
    public void AClickIsASwitchUntilWindowsRefusesOne()
    {
        var state = new SpatialState(true, SpatialFormats.Off, [new(Sonic, "Windows Sonic for Headphones")]);
        using (var before = new SpatialSwitch(() => new SampleSpatial(state)))
        {
            Assert.False(before.Switches);
        }
        using var action = new SpatialSwitch(() => new SampleSpatial(state, allow: false)) { Refused = () => { } };
        Started(action);
        Assert.True(action.Switches);
        action.Switch(Sonic);
        Assert.False(action.Switches);

        using var none = new SpatialSwitch(() => new SampleSpatial(new SpatialState(true, SpatialFormats.Off, []))) { Refused = () => { } };
        Started(none);
        Assert.False(none.Switches);
    }
}
