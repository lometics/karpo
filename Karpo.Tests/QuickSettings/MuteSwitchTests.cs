// The sound's and the microphone's toggles (QuickSettings\Services\MuteSwitch.cs): on is heard, a click
// mutes and unmutes, the glyph is struck through while muted, and a mute made elsewhere is what the
// toggle says next. Made up (VolumeWatch.Stand): nothing reads or changes this PC's sound.

using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.QuickSettings;

public sealed class MuteSwitchTests
{
    /// <summary>A toggle of <paramref name="flow"/> started on <paramref name="endpoint"/>, made up.</summary>
    private static MuteSwitch Started(SampleVolume endpoint, EDataFlow flow, string name)
    {
        var made = new MuteSwitch(name, flow);
        Assert.Null(made.On);
        Assert.True(made.Available);
        VolumeWatch.Stand = endpoint;
        try
        {
            made.Start(() => { });
        }
        finally
        {
            VolumeWatch.Stand = null;
        }
        return made;
    }

    [Fact]
    public void AClickMutesAndUnmutes()
    {
        var output = new SampleVolume();
        using var sound = Started(output, EDataFlow.eRender, "Sound");
        Assert.True(sound.On);
        Assert.Null(sound.Glyph);
        Assert.True(sound.Switches);

        sound.Act();
        Assert.True(output.Muted);
        Assert.False(sound.On);
        Assert.Equal(MuteSwitch.SpeakerMuted, sound.Glyph);

        sound.Act();
        Assert.False(output.Muted);
        Assert.True(sound.On);
    }

    /// <summary>Muted elsewhere — the keyboard's key, Windows' panel — it says so the next time it is read.</summary>
    [Fact]
    public void AMuteMadeElsewhereIsFollowed()
    {
        var input = new SampleVolume();
        using var microphone = Started(input, EDataFlow.eCapture, "Microphone");

        input.Muted = true;

        Assert.False(microphone.On);
        Assert.Equal(MuteSwitch.MicrophoneMuted, microphone.Glyph);
    }

    /// <summary>With no output or microphone at all there is nothing to switch: not available, no state.</summary>
    [Fact]
    public void WithoutAnEndpointThereIsNothingToSwitch()
    {
        var none = new SampleVolume { Present = false };
        using var sound = Started(none, EDataFlow.eRender, "Sound");

        Assert.False(sound.Available);
        Assert.Null(sound.On);
        sound.Act();
        Assert.False(none.Muted);
    }
}
