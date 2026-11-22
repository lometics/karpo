// network: the dock hears the network come back (docs/resilience.md §4). After a wake the first fetch
// often runs before the Wi-Fi is up, and fails, and each store would then wait out its retry — a
// minute for the accounts, five for the weather. So the dock listens for an address arriving (an
// adapter connected, a DHCP answer, a VPN up): once they settle it says so to every store at once
// (OnlineUpdates.CameBack: a fetch that failed is due again) and polls every widget, as at a wake.
//
// The address change is heard on a pool thread, posted to the host and settled there: a burst of them
// (an adapter brings up IPv4 and IPv6 a moment apart) is one. Only a failed fetch is tried again, so an
// address that changes on a network that was never gone costs one poll of what is already fresh.

using System.Net.NetworkInformation;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>Posted to the host from the pool thread an address change is heard on.</summary>
    internal const uint WM_KARPO_NETWORK = WM.WM_APP + 0x2E;

    /// <summary>The host's one-shot timer that lets a burst of address changes settle.</summary>
    private const uint NETWORK = 84;

    /// <summary>How long the addresses are let settle, and the name servers answer, before the stores are told.</summary>
    private const uint NetworkSettleMs = 2_000;

    private NetworkAddressChangedEventHandler? networkHeard;

    /// <summary>The network is heard from the dock's start.</summary>
    private void HookNetwork()
    {
        if (networkHeard is not null)
        {
            return;
        }
        var tell = host;
        networkHeard = (_, _) => PostMessageW(tell, WM_KARPO_NETWORK, 0, 0);
        try
        {
            NetworkChange.NetworkAddressChanged += networkHeard;
        }
        catch (NetworkInformationException error)
        {
            networkHeard = null;
            Report(error);
        }
    }

    private void DisposeNetwork()
    {
        if (networkHeard is { } heard)
        {
            NetworkChange.NetworkAddressChanged -= heard;
            networkHeard = null;
        }
        Kill(NETWORK);
    }

    /// <summary>An address came or went: the stores are told once the burst has settled.</summary>
    private void NetworkHeard() => Arm(NETWORK, NetworkSettleMs);

    /// <summary>The addresses settled: where the network is there now, every failed fetch is due, and every widget polled.</summary>
    private void NetworkSettled()
    {
        Kill(NETWORK);
        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return;
        }
        OnlineUpdates.Shared.CameBack();
        RefreshIslands();
    }
}
