// The sound's and the microphone's toggles (2026-10-11: "same for sound where you can mute / unmute,
// same for microphone"): the default output's or the default microphone's mute, through Core Audio
// as the volume slider has the output's (VolumeWatch). On is sound — unmuted — so the glyph is lit
// while it is heard, as the radios' are while they are on, and struck through while muted.
//
// Followed, not polled: the endpoint's callback says when its mute changes, whoever changed it — the
// keyboard's mute key, Windows' panel, a call app — and the enumerator's when another endpoint
// becomes the default, which the watch then follows. Read on the dock's thread when the widget is
// drawn, as the slider reads its level: two cheap calls on an object already open.

using TerraFX.Interop.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed class MuteSwitch(string name, EDataFlow flow) : IQuickSwitch
{
    /// <summary>The speaker struck through, as Windows' volume icon is while muted.</summary>
    internal const string SpeakerMuted = "";

    /// <summary>The microphone struck through, as the sound panel's microphone row is while muted.</summary>
    internal const string MicrophoneMuted = "";

    private VolumeWatch? watch;

    public string Name { get; } = name;

    /// <summary>Until it is followed, as if there were one; then whether there is a default endpoint at all.</summary>
    public bool Available => Read() is not { } now || now.Present;

    /// <summary>Unmuted is on; null until it is followed, and where there is no endpoint.</summary>
    public bool? On => Read() is { Present: true } now ? !now.Muted : null;

    public string? Note => null;

    public bool Switches => true;

    /// <summary>Struck through while muted.</summary>
    public string? Glyph => On == false ? (flow == EDataFlow.eCapture ? MicrophoneMuted : SpeakerMuted) : null;

    public void Start(Action changed) => watch ??= new VolumeWatch(changed, flow);

    /// <summary>Mutes it, or unmutes it.</summary>
    public void Act()
    {
        if (Read() is { Present: true } now)
        {
            now.SetMuted(!now.Muted);
        }
    }

    /// <summary>The watch with what changed taken; null before it is followed.</summary>
    private VolumeWatch? Read()
    {
        _ = watch?.Read();
        return watch;
    }

    public void Dispose()
    {
        watch?.Dispose();
        watch = null;
    }
}
