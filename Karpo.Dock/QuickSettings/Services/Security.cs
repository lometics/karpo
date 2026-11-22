// Whether this PC is protected, for the Security widget: the antivirus and the
// firewall, as Windows' own Security Center reports them, and the name of
// whichever product is actually doing the work.
//
// WHY IT IS CALLED SECURITY AND NOT DEFENDER. It was asked for as a Defender
// widget. Windows offers no "how is Defender doing" answer that a bar like this
// can read — that one is WMI (MSFT_MpComputerStatus in root\Microsoft\Windows\
// Defender), and WMI is out (see below). What Windows does offer is Security
// Center, whose whole design is "whichever antivirus and firewall are
// registered". On a PC with Norton or Bitdefender that is not Defender, and a
// widget carrying Defender's name would then have to either show a green shield
// for work Defender is not doing, or show "unprotected" on a machine that is
// protected. Both are worse than no widget. So the widget names whoever is in
// charge — "Microsoft Defender" where that is the answer — and never claims it
// is somebody it is not.
//
// WHAT IS WATCHED: the antivirus and the firewall, and nothing else. Security
// Center also reports Windows Update, Internet settings, User Account Control
// and its own service. Those are often turned down deliberately, and a shield
// that goes red because somebody chose a UAC level is a shield people learn to
// ignore — which is the one failure that makes a security widget worse than no
// security widget.
//
// WMI IS NOT AN OPTION HERE, and it is worth knowing why before anybody tries.
// System.Management announces itself at publish — IL2104 "produced trim
// warnings" and IL3053 "produced AOT analysis warnings" — and then, in the
// published native-AOT bar, dies on the first query:
//
//   MissingMethodException: No parameterless constructor defined for type
//   'System.Management.WbemDefPath'. (HResult 0x80131513)
//
// A type it creates by reflection, trimmed away. It read every field perfectly
// under the JIT, in 151 ms. That is the second AOT-only failure in this codebase
// and the first that said so in advance: read the publish warnings. Everything
// below was published as native AOT and run before a line of the widget was
// written, and all of it works there: wscapi's health, the product list, and the
// change registration.
//
// TWO THINGS MEASURED THAT WILL SAVE THE NEXT PERSON:
//
//   * The product-list interfaces derive from IDispatch, not IUnknown, so the
//     first real method is vtable slot 7 (3 IUnknown + 4 IDispatch). Getting
//     that wrong is an access violation, not an error code.
//   * IWscProduct::get_ProductIsDefault answered 0 for the only antivirus on the
//     machine. It is NOT a usable "who is in charge" signal. The name and the
//     product GUID are.
//
// NOTHING IS POLLED. WscRegisterForChanges calls back when Windows' security
// state changes, and that is the only thing that makes this read again. While
// nobody is looking the cost is one registration handle and no CPU at all — and
// the registration itself waits for the widget's first draw (docs\widgets.md
// section 11), so a PC with no Security widget registers nothing.
//
// A reading costs 0.75 ms for the two healths and 0.3 ms for the product list,
// measured warm; the first call into wscapi.dll is about 7 ms while it loads.
// It happens on the pool all the same, and what crosses to the dock is a plain
// record.
//
// KARPO NEVER ACTS ON ANY OF THIS. A click opens Windows Security and that is
// the whole of it. Turning protection on or off, starting a scan and dismissing
// a warning are Windows' to offer and not a taskbar's.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>How one provider stands, as Security Center reports it.</summary>
internal enum ProviderHealth
{
    /// <summary>Nobody has asked yet, or the asking failed. Not a state to draw as anything.</summary>
    Unknown = -1,

    Good = 0,

    /// <summary>Windows is not watching this one — a policy, or a product that reports nothing.</summary>
    NotMonitored = 1,

    /// <summary>Off, out of date, or otherwise wanting attention.</summary>
    Poor = 2,

    /// <summary>The person told Windows to stop asking about it for a while.</summary>
    Snoozed = 3,
}

/// <summary>What the widget draws: the two providers boiled down to one thing to show.</summary>
internal enum SecurityStanding
{
    /// <summary>Nothing is known yet. The widget shows no state at all — blank, never wrong.</summary>
    Unknown,

    /// <summary>Both watched providers are good.</summary>
    Good,

    /// <summary>One of them is poor or snoozed: something is asking for the person.</summary>
    Attention,

    /// <summary>Windows is not watching one of them, and neither is poor. Not good, not bad.</summary>
    NotMonitored,
}

/// <summary>
/// What Security Center says right now.
/// </summary>
/// <param name="Guard">
/// The antivirus product's own name, or null where nothing is registered or nobody has looked.
/// This is what the widget says is in charge, because it is the only honest answer.
/// </param>
/// <param name="IsDefender">Whether <paramref name="Guard"/> is Microsoft Defender, by its product GUID.</param>
/// <param name="Open">Where a click goes, or null before anything is known.</param>
internal sealed record SecurityState(
    ProviderHealth Antivirus,
    ProviderHealth Firewall,
    string? Guard = null,
    bool IsDefender = false,
    string? Open = null)
{
    /// <summary>Nothing asked yet: what the widget starts as, and what a failed reading leaves.</summary>
    public static SecurityState Nothing { get; } = new(ProviderHealth.Unknown, ProviderHealth.Unknown);

    public SecurityStanding Standing => Security.StandingOf(Antivirus, Firewall);
}

internal interface ISecuritySource : IDisposable
{
    /// <summary>
    /// Starts following it; <paramref name="changed"/> comes from any thread. Called the first
    /// time the widget is looked at, not when it is made, and it does not block
    /// (docs\widgets.md section 11).
    /// </summary>
    void Start(Action changed);

    /// <summary>The last thing Windows said. A plain record: nothing here is a COM object.</summary>
    SecurityState Read();
}

internal static class Security
{
    /// <summary>Microsoft Defender's product GUID in Security Center, measured on a PC running it.</summary>
    internal const string DefenderGuid = "{D68DDC3A-831F-4FAE-9E44-DA132C1ACF46}";

    /// <summary>Windows Security, which is the whole of what a click may do.</summary>
    internal const string WindowsSecurity = "windowsdefender://";

    /// <summary>
    /// The source the widget follows: Windows' own, and nothing that reaches Windows at all on a
    /// run that may not reach the person's world.
    /// <para>
    /// The guarantee is here rather than in the widget, for the reason the switches learned it
    /// (docs\widgets.md section 11): a source starts on the widget's first draw, and a drawn
    /// widget is what every offscreen picture and <c>Karpo.exe --smoke</c> produces. Reading a
    /// person's antivirus state in a test is not as harmful as switching their radio, but it is
    /// still the person's machine answering, and a picture that changed with whose PC took it
    /// would be a bad picture.
    /// </para>
    /// </summary>
    public static ISecuritySource Source() =>
        Made?.Invoke() ?? (Config.IsIsolated || QuickSources.UseSamples ? new SampleSecurity() : new WindowsSecuritySource());

    /// <summary>Where the source comes from, for a test that wants one of its own. Per thread.</summary>
    [ThreadStatic]
    private static Func<ISecuritySource>? made;

    internal static Func<ISecuritySource>? Made
    {
        get => made;
        set => made = value;
    }

    /// <summary>
    /// The two providers boiled down to the one thing the widget shows. Pure, and the whole of
    /// the widget's judgement.
    /// <para>
    /// Unknown wins over everything: a shield that says "protected" before Windows has answered
    /// is the worst thing this widget could do, so not knowing about either provider means not
    /// saying anything about both. After that the worst answer wins, and "not monitored" is not a
    /// worst answer — it is Windows declining to watch, which is neither good nor bad.
    /// </para>
    /// </summary>
    internal static SecurityStanding StandingOf(ProviderHealth antivirus, ProviderHealth firewall)
    {
        if (antivirus == ProviderHealth.Unknown || firewall == ProviderHealth.Unknown)
        {
            return SecurityStanding.Unknown;
        }
        if (Wants(antivirus) || Wants(firewall))
        {
            return SecurityStanding.Attention;
        }
        return antivirus == ProviderHealth.NotMonitored || firewall == ProviderHealth.NotMonitored
            ? SecurityStanding.NotMonitored
            : SecurityStanding.Good;
    }

    private static bool Wants(ProviderHealth health) =>
        health is ProviderHealth.Poor or ProviderHealth.Snoozed;

    /// <summary>
    /// Where a click goes for a product whose remediation path is <paramref name="remediation"/>.
    /// <para>
    /// Security Center hands over whatever the product registered, and for a third-party product
    /// that can be a path to an executable. Karpo will not run one because a registry field said
    /// so: only a URI scheme is taken, and anything else falls back to Windows Security, which is
    /// always the right place to look even when it is not the right product.
    /// </para>
    /// </summary>
    internal static string OpenFor(string? remediation)
    {
        if (remediation is not { Length: > 0 } path)
        {
            return WindowsSecurity;
        }
        var colon = path.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || path.AsSpan(0, colon).ContainsAny('\\', '/', ' ') || colon == 1)
        {
            // "C:\..." has a colon at 1, a UNC path has slashes before one, and a bare name has
            // none: none of those is a scheme.
            return WindowsSecurity;
        }
        foreach (var letter in path.AsSpan(0, colon))
        {
            if (!char.IsAsciiLetterOrDigit(letter) && letter is not ('+' or '-' or '.'))
            {
                return WindowsSecurity;
            }
        }
        return path;
    }

    /// <summary>
    /// What the tooltip says: where the machine stands, who is in charge, and what a click does.
    /// Pure, so the words are tested rather than looked at.
    /// </summary>
    internal static string Say(SecurityState state)
    {
        var standing = state.Standing switch
        {
            SecurityStanding.Good => S.DockQuickSecurityGood,
            SecurityStanding.Attention => S.DockQuickSecurityAttention,
            SecurityStanding.NotMonitored => S.DockQuickSecurityNotMonitored,
            _ => S.DockQuickSecurityName,
        };
        return $"{standing}\n{Guard(state)}\n{S.DockQuickSecurityClick}";
    }

    /// <summary>Who is in charge, in a sentence: the tooltip's second line, and the one under a list line's standing.</summary>
    internal static string Guard(SecurityState state) =>
        state.Standing == SecurityStanding.Unknown
            ? S.DockQuickSecurityWaiting
            : state.Guard is { Length: > 0 } name
                ? S.DockQuickSecurityGuard(name)
                : S.DockQuickSecurityNoAntivirus;

    /// <summary>The line under a folder card's name: where it stands, in as few words as it takes.</summary>
    internal static string Line(SecurityState state) => state.Standing switch
    {
        SecurityStanding.Good => S.DockQuickSecurityProtected,
        SecurityStanding.Attention => S.DockQuickSecurityNeedsAttention,
        SecurityStanding.NotMonitored => S.DockQuickSecurityNotWatched,
        _ => S.DockQuickAskingWindows,
    };
}

/// <summary>Security Center, through wscapi.dll and its product list. See the top of this file.</summary>
internal sealed unsafe class WindowsSecuritySource : ISecuritySource
{
    private const uint Firewall = 0x1;
    private const uint Antivirus = 0x4;

    private volatile SecurityState state = SecurityState.Nothing;
    private Action? changed;
    private nint registration;
    private GCHandle self;
    private volatile bool disposed;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // Both on the pool: the first call into wscapi.dll is about 7 ms while the library loads,
        // and the dock's thread is drawing.
        _ = Task.Run(() => Troubles.Guard("Security: reading who protects this PC", () =>
        {
            Look();
            Follow();
        }));
    }

    public SecurityState Read() => state;

    public void Dispose()
    {
        disposed = true;
        if (registration != 0)
        {
            try
            {
                _ = WscUnRegisterChanges(registration);
            }
            catch (Exception)
            {
            }
            registration = 0;
        }
        if (self.IsAllocated)
        {
            self.Free();
        }
    }

    /// <summary>
    /// Asks Windows to say when its security state changes, so nothing here is polled. The handle
    /// is the whole cost while nobody is looking.
    /// </summary>
    private void Follow()
    {
        if (disposed)
        {
            return;
        }
        try
        {
            self = GCHandle.Alloc(this);
            if (WscRegisterForChanges(0, out var handle, &Woken, GCHandle.ToIntPtr(self)) == 0)
            {
                registration = handle;
            }
            else
            {
                self.Free();
            }
        }
        catch (Exception error)
        {
            Troubles.Note("Security: following Windows' security state", error);
        }
    }

    /// <summary>
    /// Windows calling back on one of its own threads. It must not throw across that boundary, so
    /// everything inside is caught.
    /// </summary>
    [UnmanagedCallersOnly]
    private static uint Woken(nint context)
    {
        try
        {
            if (GCHandle.FromIntPtr(context).Target is WindowsSecuritySource source && !source.disposed)
            {
                source.Look();
            }
        }
        catch (Exception)
        {
            // There is nowhere for an exception to go from here, and Windows is not to be thrown at.
        }
        return 0;
    }

    /// <summary>Reads both healths and who is in charge, and tells the dock if any of it moved.</summary>
    private void Look()
    {
        SecurityState found;
        try
        {
            var guard = Product(Antivirus);
            found = new SecurityState(
                Health(Antivirus),
                Health(Firewall),
                guard.Name,
                guard.IsDefender,
                Security.OpenFor(guard.Remediation));
        }
        catch (Exception error)
        {
            Troubles.Note("Security: reading Windows' security state", error);
            return;
        }
        if (state == found)
        {
            return;
        }
        state = found;
        changed?.Invoke();
    }

    /// <summary>One provider's health, or Unknown where Windows would not say.</summary>
    private static ProviderHealth Health(uint provider)
    {
        try
        {
            return WscGetSecurityProviderHealth(provider, out var health) == 0 && health <= 3
                ? (ProviderHealth)health
                : ProviderHealth.Unknown;
        }
        catch (Exception error)
        {
            Troubles.Note("Security: a provider's health", error);
            return ProviderHealth.Unknown;
        }
    }

    /// <summary>
    /// The first product registered for <paramref name="provider"/>: its name, whether it is
    /// Defender, and where it says its own settings are.
    /// <para>
    /// The first, not the default one: get_ProductIsDefault answered 0 for the only antivirus on
    /// the machine this was measured on, so it says nothing (see the top of this file). Windows'
    /// own Security page shows the same list, and a PC with two registered antivirus products is
    /// a PC with a bigger problem than a widget's wording.
    /// </para>
    /// </summary>
    private static (string? Name, bool IsDefender, string? Remediation) Product(uint provider)
    {
        var classId = new Guid("17072F7B-9ABE-4A74-A261-1EB76B55107A");
        var listId = new Guid("722A338C-6E8E-4E72-AC27-1417FB0C81C2");
        void* made = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &listId, &made).FAILED || made is null)
        {
            return (null, false, null);
        }
        var list = (nint)made;
        try
        {
            // IDispatch, so the first method is slot 7: Initialize, get_Count, get_Item.
            var vtable = *(void***)list;
            if (((delegate* unmanaged<nint, uint, int>)vtable[7])(list, provider) != 0)
            {
                return (null, false, null);
            }
            var count = 0;
            if (((delegate* unmanaged<nint, int*, int>)vtable[8])(list, &count) != 0 || count <= 0)
            {
                return (null, false, null);
            }
            nint product;
            if (((delegate* unmanaged<nint, int, nint*, int>)vtable[9])(list, 0, &product) != 0 || product == 0)
            {
                return (null, false, null);
            }
            try
            {
                var members = *(void***)product;
                nint name = 0, guid = 0, remediation = 0;
                _ = ((delegate* unmanaged<nint, nint*, int>)members[7])(product, &name);
                _ = ((delegate* unmanaged<nint, nint*, int>)members[10])(product, &remediation);
                _ = ((delegate* unmanaged<nint, nint*, int>)members[12])(product, &guid);
                try
                {
                    var said = Marshal.PtrToStringBSTR(name);
                    var which = guid == 0 ? null : Marshal.PtrToStringBSTR(guid);
                    return (
                        string.IsNullOrWhiteSpace(said) ? null : said,
                        which is { Length: > 0 } && string.Equals(which, Security.DefenderGuid, StringComparison.OrdinalIgnoreCase),
                        remediation == 0 ? null : Marshal.PtrToStringBSTR(remediation));
                }
                finally
                {
                    Free(name);
                    Free(guid);
                    Free(remediation);
                }
            }
            finally
            {
                _ = Marshal.Release(product);
            }
        }
        finally
        {
            _ = Marshal.Release(list);
        }
    }

    private static void Free(nint text)
    {
        if (text != 0)
        {
            Marshal.FreeBSTR(text);
        }
    }

    [DllImport("wscapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WscGetSecurityProviderHealth(uint providers, out uint health);

    [DllImport("wscapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WscRegisterForChanges(nint reserved, out nint registration, delegate* unmanaged<nint, uint> callback, nint context);

    [DllImport("wscapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WscUnRegisterChanges(nint registration);
}

/// <summary>
/// A made-up PC for pictures and tests, and what any isolated run gets. It reaches nothing: no
/// test and no smoke pass reads the person's antivirus.
/// </summary>
internal sealed class SampleSecurity : ISecuritySource
{
    private readonly SecurityState state;

    /// <summary>
    /// A made-up PC. The default is the ordinary one: Defender in charge and everything good.
    /// </summary>
    public SampleSecurity(SecurityState? state = null) =>
        this.state = state ?? new SecurityState(
            ProviderHealth.Good,
            ProviderHealth.Good,
            "Microsoft Defender Antivirus", // not-ui: a sample antivirus's name
            IsDefender: true,
            Security.WindowsSecurity);

    public void Start(Action changed) => changed();

    public SecurityState Read() => state;

    public void Dispose()
    {
    }
}
