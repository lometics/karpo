// The switch behind each toggle of a toggles row (Karpo.Core\Toggles.cs): its one-button widget's own
// switch (QuickSwitches.For, the same object and the same rules) — for Wi-Fi and Bluetooth their
// radio, switched through Windows.Devices.Radios as their lines switch it (RadioToggle, the Wi-Fi and
// Bluetooth toggles' switch since they became widgets, 2026-10-11). Nothing here asks Windows anything
// on the dock's thread: a radio is looked for on the pool when the switch starts, and says it does
// not know until then.

using Karpo.Core;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

internal static class ToggleSwitches
{
    /// <summary>
    /// Where the switches come from in a test: made-up ones per toggle, so a picture can show a radio
    /// on without touching this PC's. Per thread, as <see cref="QuickSwitches.Source"/> is.
    /// </summary>
    [ThreadStatic]
    private static Func<Toggle, IQuickSwitch?>? source;

    internal static Func<Toggle, IQuickSwitch?>? Source
    {
        get => source;
        set => source = value;
    }

    /// <summary>
    /// The switch behind <paramref name="toggle"/>: a test's, else its one-button widget's switch,
    /// whose own guard keeps an isolated run off this PC's radios and sound (QuickSwitches.For).
    /// </summary>
    public static IQuickSwitch For(Toggle toggle)
    {
        if (Source?.Invoke(toggle) is { } given)
        {
            return given;
        }
        var info = Toggles.Find(toggle) ?? throw new ArgumentOutOfRangeException(nameof(toggle), toggle, "is not a toggle.");
        return QuickSwitches.For(info.Switch ?? throw new ArgumentOutOfRangeException(nameof(toggle), toggle, "has no switch."));
    }
}

/// <summary>
/// Wi-Fi's or Bluetooth's radio as a toggle: on, off, or not known until it is found. A click flips
/// it; where Windows refuses, the tooltip says so, and the next click asks again.
/// </summary>
internal sealed class RadioToggle(string name, Radios.RadioKind kind) : IQuickSwitch
{
    private RadioSwitch? radio;
    private Action? changed;
    private volatile string? note;

    public string Name { get; } = name;

    /// <summary>Until the radio is looked for, as if it were there; after, whether it was found.</summary>
    public bool Available => radio is not { Looked: true } found || found.Found;

    public bool? On => radio?.On;

    public string? Note => note;

    public bool Switches => true;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        radio ??= new RadioSwitch(kind, () => changed?.Invoke());
        radio.Find();
    }

    public void Act()
    {
        if (radio is { Found: true } found && found.On is { } on)
        {
            _ = Switch(found, !on);
        }
    }

    private async Task Switch(RadioSwitch found, bool on)
    {
        var done = await found.Set(on);
        note = done ? null : S.DockQuickRadioRefused;
        if (!done)
        {
            Interlocked.Increment(ref refusals);
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    public void Dispose() => radio?.Dispose();
}
