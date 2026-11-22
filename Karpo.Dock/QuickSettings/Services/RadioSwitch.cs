// A radio (Wi-Fi or Bluetooth) as Windows.Devices.Radios has it: found once,
// followed while its flyout is open, and switched on or off when the person asks.
// Windows may refuse the switch to an unpackaged app; that is reported, and the
// flyout then offers its Settings page instead of a switch that does nothing.
//
// Where it stands is read on the thread the radio came from and kept in a field,
// never read from the flyout's own thread. The radio arrives on the pool (an
// MTA) and the dock's thread is an STA, and reading one apartment's object from
// the other throws — which is the bug that made the Bluetooth panel show no
// devices at all (see the top of Bluetooth.cs). The state is one bool; keeping
// it costs nothing and removes the question.

using Foundation = Windows.Foundation;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed class RadioSwitch : IDisposable
{
    private readonly Radios.RadioKind kind;
    private readonly Action changed;
    private readonly Foundation.TypedEventHandler<Radios.Radio, object> stateChanged;
    private volatile Radios.Radio? radio;
    private volatile bool looked;
    private volatile bool disposed;

    // Where the radio stands, as last read on its own thread: 1 on, 0 off, -1 nothing known.
    private volatile int state = -1;

    /// <param name="changed">From any thread: the radio was found, or it switched.</param>
    public RadioSwitch(Radios.RadioKind kind, Action changed)
    {
        this.kind = kind;
        this.changed = changed;
        // Read here, on the radio's own thread, and only then told about.
        stateChanged = (sender, _) =>
        {
            Note(sender);
            changed();
        };
    }

    /// <summary>Whether the lookup has finished (found or not).</summary>
    public bool Looked => looked;

    /// <summary>
    /// On, off, or null when there is no such radio (or it hasn't been found yet). Answered from
    /// what was last read on the radio's own thread, so any thread may ask.
    /// </summary>
    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    /// <summary>Where <paramref name="found"/> stands, read here because here is its own thread.</summary>
    private void Note(Radios.Radio? found)
    {
        try
        {
            state = found is null ? -1 : found.State == Radios.RadioState.On ? 1 : 0;
        }
        catch (Exception)
        {
            // It went while it was being read, which is what a radio being switched off looks like.
            state = -1;
        }
    }

    public bool Found => radio is not null;

    /// <summary>Looks for the radio on the pool; <see cref="changed"/> hears when it is done.</summary>
    public void Find() => _ = Look();

    private async Task Look()
    {
        try
        {
            foreach (var candidate in await Radios.Radio.GetRadiosAsync())
            {
                if (candidate.Kind == kind)
                {
                    radio = candidate;
                    Note(candidate);
                    if (!disposed)
                    {
                        candidate.StateChanged += stateChanged;
                    }
                    break;
                }
            }
        }
        catch (Exception)
        {
            // No radio stack at all.
        }
        looked = true;
        changed();
    }

    /// <summary>Switches the radio; false when Windows refused (or there is none).</summary>
    public async Task<bool> Set(bool on)
    {
        try
        {
            if (radio is not { } found || await Radios.Radio.RequestAccessAsync() != Radios.RadioAccessStatus.Allowed)
            {
                return false;
            }
            var allowed = await found.SetStateAsync(on ? Radios.RadioState.On : Radios.RadioState.Off) == Radios.RadioAccessStatus.Allowed;
            if (allowed)
            {
                // Here rather than waiting for the event: this is the radio's own thread too.
                Note(found);
            }
            return allowed;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (radio is { } found)
        {
            try
            {
                found.StateChanged -= stateChanged;
            }
            catch (Exception)
            {
            }
        }
    }
}
