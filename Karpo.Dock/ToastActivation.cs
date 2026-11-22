// What a notification offers beyond its words, and how a click on it reaches its app, the way
// Windows' own notification center does it.
//
// WHAT A TOAST OFFERS. Its XML says where a click on it goes — <toast launch="…"
// activationType="…"> — and carries up to five buttons and five inputs under <actions>: an
// <action> with its content, its arguments, its activation type and the input it goes with
// (hint-inputId); an <input> that is a text box (a reply) or a selection. ToastOffers.Read takes
// that apart once, when the database is read, into a plain record the list draws from.
// Buttons placed in the context menu are the right-click menu's, not the card's, and are left out;
// "hint-systemCommands" adds Windows' own Dismiss. Snooze is Windows' to do and not an app's, and
// nothing outside Windows can bring a toast back later, so a snooze button is never offered.
//
// HOW WINDOWS ACTIVATES AN APP, and so how Karpo does (ToastActivator.Run). The activation type
// decides it:
//
//   * protocol: the arguments are a URI and it is launched. Only a URI with a scheme, and never a
//     file: one — a toast is a thing any app can post, and it does not get to name a program for
//     Karpo to run.
//   * foreground and background: the app's COM activator, where it registered one. That is the
//     CLSID in System.AppUserModel.ToastActivatorCLSID on its Start menu shortcut (a desktop app),
//     under HKCU/HKLM\Software\Classes\AppUserModelId\<AUMID>\CustomActivator (Windows App SDK and
//     Windows' own system toasts), or in its package manifest's ToastNotificationActivation (a
//     packaged desktop app: Outlook, Teams). It is created out of process, handed the right to come
//     to the front for a foreground activation (CoAllowSetForegroundWindow), and called:
//     INotificationActivationCallback::Activate(AUMID, arguments, the inputs' ids and values).
//     Measured on the person's PC, 10 of 210 Start menu shortcuts carry one — Chrome, Brave,
//     OneDrive, Bitwarden (Electron), GitHub Desktop, WSL — and the new Outlook and Teams declare
//     one in their manifests. The AppsFolder item does not expose the property (0x80070490), so
//     the shortcuts themselves are read, which is also where Windows looks.
//   * with no activator, a packaged app gets its own activation with the arguments
//     (IApplicationActivationManager::ActivateApplication), and a desktop app is started from its
//     Start menu shortcut's program with the arguments on its command line — which is exactly
//     what Windows does for an app whose shortcut has a CLSID and no server behind it.
//   * where there is nothing to hand arguments to at all, the app is opened, as it always was.
//
// WHAT FAILS IS SAID, AND THE APP STILL COMES. Each road reports how it went (an HRESULT), and one
// that fails gives way to the next, down to the app opened by its AUMID — which is also where a
// protocol activation goes whose URI Karpo refuses or Windows cannot launch, as Windows opens the app
// when a toast's own activation cannot be made. Whatever failed on the way is one line in karpo.log
// (ToastActivator.Line): the app, how it was asked, each road that failed and why, and what came of
// it. Until 2026-10-23 a failure said nothing at all, and a click that reached nothing looked the
// same as one that was never made.
//
// What cannot be done from outside Windows is a packaged app's own background task: a button that
// asks for background activation from an app that has no COM activator opens the app with its
// arguments instead, which is the nearest thing there is, and the documented way such an app is
// launched with arguments.
//
// ALL OF IT OFF THE DOCK'S THREAD, on a short-lived STA of its own: an activator's Activate returns
// when the app has handled it, which can be an app starting up, and ShellExecute wants an
// apartment. The one thing done at once, on the dock's thread, is to pass on the right to take the
// foreground (AllowSetForegroundWindow) while Karpo still has it from the click.
//
// THE SEAM. The routing is pure and takes its Windows calls through IActivationPlatform, so the
// tests follow every route on a fake that records what it was asked; and a run that may not reach
// the person's world — every test, every smoke pass — gets an activator that reaches nothing
// (ToastActivator.Shared), so no test can start an app.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using Windows.Data.Xml.Dom;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>How Windows hands an activation to the app (the toast's activationType).</summary>
public enum ToastActivationKind
{
    /// <summary>The app comes to the front with the arguments.</summary>
    Foreground,

    /// <summary>The app takes the arguments without coming to the front: a "Like", a reply sent.</summary>
    Background,

    /// <summary>The arguments are a URI, and it is launched.</summary>
    Protocol,

    /// <summary>Windows' own: dismiss (or snooze, which only Windows can do).</summary>
    System,
}

public enum ToastInputKind
{
    Text,
    Selection,
}

/// <summary>One choice of a selection input.</summary>
public sealed record ToastChoice(string Id, string Content);

/// <summary>A toast's input: a text box, or a selection of choices.</summary>
/// <param name="Default">The text, or the choice's id, it starts with.</param>
public sealed record ToastInput(string Id, ToastInputKind Kind, string Placeholder, string Title, string Default, IReadOnlyList<ToastChoice> Choices);

/// <summary>A toast's button.</summary>
/// <param name="InputId">The text box it sends, drawn beside it and sent by Enter there; null for none.</param>
/// <param name="Target">protocolActivationTargetApplicationPfn: the app a protocol button's URI is meant for.</param>
public sealed record ToastButton(string Content, string Arguments, ToastActivationKind Kind, string? InputId = null, string? Target = null)
{
    /// <summary>Windows' own Dismiss: the notification goes, and nothing is activated.</summary>
    public bool Dismisses => Kind == ToastActivationKind.System;
}

/// <summary>What a toast offers beyond its words: where a click on it goes, and its buttons and inputs.</summary>
/// <param name="Launch">The toast's launch arguments; empty where it has none.</param>
public sealed record ToastOffer(string Launch, ToastActivationKind Kind, IReadOnlyList<ToastButton> Buttons, IReadOnlyList<ToastInput> Inputs, string? Target = null)
{
    /// <summary>A toast with nothing but its words: a click opens its app.</summary>
    public static ToastOffer None { get; } = new("", ToastActivationKind.Foreground, [], []);

    /// <summary>Whether it has buttons or inputs, which the card shows when it is expanded.</summary>
    public bool Actionable => Buttons.Count > 0 || Inputs.Count > 0;
}

/// <summary>Reads what a toast offers from its XML.</summary>
internal static class ToastOffers
{
    /// <summary>What Windows shows at the most, of each.</summary>
    internal const int Most = 5;

    /// <summary>What <paramref name="toast"/> (the &lt;toast&gt; element) offers. Never throws.</summary>
    public static ToastOffer Read(IXmlNode? toast)
    {
        if (toast is null)
        {
            return ToastOffer.None;
        }
        try
        {
            var launch = Attribute(toast, "launch") ?? "";
            var kind = KindOf(Attribute(toast, "activationType"));
            var target = Attribute(toast, "protocolActivationTargetApplicationPfn");
            var buttons = new List<ToastButton>();
            var inputs = new List<ToastInput>();
            foreach (var actions in Children(toast, "actions"))
            {
                foreach (var node in Children(actions, "input"))
                {
                    if (inputs.Count < Most && Input(node) is { } input)
                    {
                        inputs.Add(input);
                    }
                }
                foreach (var node in Children(actions, "action"))
                {
                    if (buttons.Count < Most && Button(node) is { } button)
                    {
                        buttons.Add(button);
                    }
                }
                // Windows' own snooze and dismiss, asked for by name: only the dismiss is Karpo's to do.
                if (string.Equals(Attribute(actions, "hint-systemCommands"), "SnoozeAndDismiss", StringComparison.OrdinalIgnoreCase)
                    && buttons.Count < Most)
                {
                    buttons.Add(new ToastButton(S.DockToastDismiss, "dismiss", ToastActivationKind.System));
                }
            }
            return new ToastOffer(launch, kind, buttons, inputs, target);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return ToastOffer.None;
        }
    }

    /// <summary>
    /// A button as the card shows it, or null for one it does not: a context menu's, one with no
    /// words, and Windows' snooze.
    /// </summary>
    private static ToastButton? Button(IXmlNode node)
    {
        if (string.Equals(Attribute(node, "placement"), "contextMenu", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var kind = KindOf(Attribute(node, "activationType"));
        var arguments = Attribute(node, "arguments") ?? "";
        var content = (Attribute(node, "content") ?? "").Trim();
        if (kind == ToastActivationKind.System)
        {
            if (!string.Equals(arguments, "dismiss", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            content = content.Length > 0 ? content : S.DockToastDismiss;
        }
        if (content.Length == 0)
        {
            return null;
        }
        return new ToastButton(content, arguments, kind, Attribute(node, "hint-inputId"), Attribute(node, "protocolActivationTargetApplicationPfn"));
    }

    private static ToastInput? Input(IXmlNode node)
    {
        var id = Attribute(node, "id") ?? "";
        if (id.Length == 0)
        {
            return null;
        }
        var selection = string.Equals(Attribute(node, "type"), "selection", StringComparison.OrdinalIgnoreCase);
        var choices = new List<ToastChoice>();
        if (selection)
        {
            foreach (var choice in Children(node, "selection"))
            {
                if (Attribute(choice, "id") is { Length: > 0 } choiceId)
                {
                    choices.Add(new ToastChoice(choiceId, Attribute(choice, "content") ?? choiceId));
                }
            }
            if (choices.Count == 0)
            {
                return null;
            }
        }
        var defaultInput = Attribute(node, "defaultInput") ?? "";
        if (selection && choices.All(choice => choice.Id != defaultInput))
        {
            defaultInput = choices[0].Id;
        }
        return new ToastInput(id, selection ? ToastInputKind.Selection : ToastInputKind.Text,
            Attribute(node, "placeHolderContent") ?? "", Attribute(node, "title") ?? "", defaultInput, choices);
    }

    /// <summary>The activationType as Windows reads it: foreground unless it says otherwise.</summary>
    internal static ToastActivationKind KindOf(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "background" => ToastActivationKind.Background,
        "protocol" => ToastActivationKind.Protocol,
        "system" => ToastActivationKind.System,
        _ => ToastActivationKind.Foreground,
    };

    private static IEnumerable<IXmlNode> Children(IXmlNode node, string name)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == NodeType.ElementNode && string.Equals(child.NodeName, name, StringComparison.OrdinalIgnoreCase))
            {
                yield return child;
            }
        }
    }

    private static string? Attribute(IXmlNode node, string name)
    {
        foreach (var attribute in node.Attributes ?? (IEnumerable<IXmlNode>)[])
        {
            if (string.Equals(attribute.NodeName, name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.NodeValue as string;
            }
        }
        return null;
    }
}

/// <summary>What an activation hands the app: its AUMID, the arguments, how, and the inputs' values.</summary>
/// <param name="Inputs">Each input's id and what it holds — the text typed, the choice's id.</param>
public sealed record ToastActivation(string AppId, string Arguments, ToastActivationKind Kind, IReadOnlyList<KeyValuePair<string, string>> Inputs, string? Target = null)
{
    /// <summary>A click on the notification itself: its launch arguments, as its own activation type says.</summary>
    public static ToastActivation Launch(NotificationEntry entry)
    {
        var offer = entry.Offer ?? ToastOffer.None;
        // A toast asking for Windows' own activation has nothing to launch: its app opens.
        var kind = offer.Kind == ToastActivationKind.System ? ToastActivationKind.Foreground : offer.Kind;
        return new ToastActivation(entry.AppId, offer.Launch, kind, [], offer.Target);
    }

    /// <summary>
    /// A button pressed: its arguments, and every input's value — what was typed, or the default
    /// where nothing was, as Windows sends them all whichever button it is.
    /// </summary>
    public static ToastActivation Of(NotificationEntry entry, ToastButton button, IReadOnlyDictionary<string, string> values)
    {
        var inputs = new List<KeyValuePair<string, string>>();
        foreach (var input in (entry.Offer ?? ToastOffer.None).Inputs)
        {
            inputs.Add(new(input.Id, values.TryGetValue(input.Id, out var value) ? value : input.Default));
        }
        return new ToastActivation(entry.AppId, button.Arguments, button.Kind, inputs, button.Target);
    }

    /// <summary>Whether the app is to come to the front, so the list closes for it as Windows' own does.</summary>
    public bool Closes => Kind is ToastActivationKind.Foreground or ToastActivationKind.Protocol;
}

/// <summary>Hands an activation to its app.</summary>
internal interface IToastActivator
{
    /// <summary>On the dock's thread: returns at once, and the app is reached off it.</summary>
    void Activate(ToastActivation activation);
}

/// <summary>The Windows calls an activation is made of: the seam the routing is tested through.</summary>
internal interface IActivationPlatform
{
    /// <summary>The COM activator the app registered for its toasts, if it has one.</summary>
    Guid? Activator(string appId);

    /// <summary>Whether the app is a packaged one (an AUMID of a package this PC has).</summary>
    bool Packaged(string appId);

    /// <summary>INotificationActivationCallback::Activate on the activator: its HRESULT, or the one it could not be reached with.</summary>
    int Call(Guid activator, ToastActivation activation);

    /// <summary>A packaged app's own activation with the arguments: its HRESULT.</summary>
    int LaunchPackaged(string appId, string arguments);

    /// <summary>The program of the app's Start menu shortcut with the arguments on its command line: the HRESULT; null where there is no such program.</summary>
    int? LaunchWithArguments(string appId, string arguments);

    /// <summary>ShellExecute: the app by its AUMID, or a URI: the HRESULT.</summary>
    int Open(string target);
}

/// <summary>Which way an activation went (<see cref="ToastActivator.Run"/>).</summary>
internal enum ActivationRoute
{
    /// <summary>Nothing activated: Windows' own, or every road failed.</summary>
    Nothing,

    Uri,
    Activator,
    Package,
    CommandLine,
    App,
}

/// <summary>A road an activation took that failed, and why: an HRESULT, or Karpo's own refusal.</summary>
internal readonly record struct ActivationFailure(ActivationRoute Tried, string Why);

/// <summary>How an activation went: the road that reached the app (Nothing where none did), and every one that failed on the way.</summary>
internal sealed record ActivationOutcome(ActivationRoute Route, IReadOnlyList<ActivationFailure> Failed);

internal static class ToastActivator
{
    [ThreadStatic]
    private static IToastActivator? made;

    /// <summary>
    /// Windows' own — and on a run that may not reach the person's world, one that reaches nothing:
    /// no test and no smoke pass can start an app, whatever it clicks (docs\widgets.md §11).
    /// </summary>
    public static IToastActivator Shared => made ?? (Config.IsIsolated || QuickSources.UseSamples ? Nobody : Theirs);

    /// <summary>What a test hands over in <see cref="Shared"/>'s place. Per thread.</summary>
    internal static IToastActivator? Made
    {
        get => made;
        set => made = value;
    }

    private static readonly IToastActivator Nobody = new NoActivator();

    private static readonly IToastActivator Theirs = new WindowsToastActivator();

    /// <summary>
    /// The way Windows would go, taken on <paramref name="platform"/>: a URI launched; the app's
    /// COM activator; a packaged app's own activation; the Start menu shortcut's program with the
    /// arguments; the app opened. Each is tried only where the one before it had nothing to try,
    /// or failed; a protocol activation that could not be launched opens the app too.
    /// </summary>
    internal static ActivationOutcome Run(ToastActivation activation, IActivationPlatform platform)
    {
        var failed = new List<ActivationFailure>();
        ActivationOutcome Went(ActivationRoute route) => new(route, failed);
        bool Reached(ActivationRoute route, int result, string? what = null)
        {
            if (result >= 0)
            {
                return true;
            }
            failed.Add(new(route, what is null ? Hex(result) : $"{what} {Hex(result)}"));
            return false;
        }
        ActivationOutcome OpenApp() =>
            activation.AppId.Length > 0 && Reached(ActivationRoute.App, platform.Open($@"shell:AppsFolder\{activation.AppId}"))
                ? Went(ActivationRoute.App)
                : Went(ActivationRoute.Nothing);

        switch (activation.Kind)
        {
            case ToastActivationKind.System:
                return Went(ActivationRoute.Nothing);
            case ToastActivationKind.Protocol:
                if (UriOf(activation.Arguments) is not { } uri)
                {
                    failed.Add(new(ActivationRoute.Uri, "refused, not a URI with a scheme other than file"));
                }
                else if (Reached(ActivationRoute.Uri, platform.Open(uri)))
                {
                    return Went(ActivationRoute.Uri);
                }
                return OpenApp();
        }
        if (activation.AppId.Length == 0)
        {
            failed.Add(new(ActivationRoute.App, "no app"));
            return Went(ActivationRoute.Nothing);
        }
        if (platform.Activator(activation.AppId) is { } activator && Reached(ActivationRoute.Activator, platform.Call(activator, activation), activator.ToString("B")))
        {
            return Went(ActivationRoute.Activator);
        }
        if (platform.Packaged(activation.AppId) && Reached(ActivationRoute.Package, platform.LaunchPackaged(activation.AppId, activation.Arguments)))
        {
            return Went(ActivationRoute.Package);
        }
        if (activation.Arguments.Length > 0 && platform.LaunchWithArguments(activation.AppId, activation.Arguments) is { } started
            && Reached(ActivationRoute.CommandLine, started))
        {
            return Went(ActivationRoute.CommandLine);
        }
        return OpenApp();
    }

    private static string Hex(int result) => $"0x{result:X8}";

    /// <summary>
    /// karpo.log's line for an activation where something failed — the app, how it was asked, each
    /// road that failed and why, and what came of it — or null where nothing did. Never the
    /// arguments or the inputs: they are the person's (a message's id, a reply typed).
    /// </summary>
    internal static string? Line(ToastActivation activation, ActivationOutcome outcome)
    {
        if (outcome.Failed.Count == 0)
        {
            return null;
        }
        var failures = string.Join("; ", outcome.Failed.Select(failure => $"{failure.Tried} {failure.Why}"));
        var came = outcome.Route == ActivationRoute.Nothing ? "nothing reached" : $"reached by {outcome.Route}";
        return $"Notifications: activating {activation.AppId} ({activation.Kind}): {failures}; {came}"; // not-ui: karpo.log's
    }

    /// <summary>
    /// A protocol activation's URI, or null where it is none Karpo will launch: a scheme is
    /// required, and file: — a program on the disk named by a notification — is refused. The rules
    /// for a scheme are RFC 3986's.
    /// </summary>
    internal static string? UriOf(string arguments)
    {
        var text = arguments.Trim();
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        // "C:\…" has its colon at 1: a drive, not a scheme.
        if (colon <= 1 || !char.IsAsciiLetter(text[0]))
        {
            return null;
        }
        foreach (var letter in text.AsSpan(0, colon))
        {
            if (!char.IsAsciiLetterOrDigit(letter) && letter is not ('+' or '-' or '.'))
            {
                return null;
            }
        }
        return string.Equals(text[..colon], "file", StringComparison.OrdinalIgnoreCase) ? null : text;
    }

    /// <summary>An isolated run's: nothing is reached.</summary>
    private sealed class NoActivator : IToastActivator
    {
        public void Activate(ToastActivation activation)
        {
        }
    }
}

/// <summary>Windows' own, on a short-lived STA thread of its own. See the top of this file.</summary>
internal sealed class WindowsToastActivator : IToastActivator
{
    public void Activate(ToastActivation activation)
    {
        if (activation.Closes)
        {
            // Now, on the dock's thread, while Karpo still has the right from the click: whoever
            // the activation starts or wakes may come to the front.
            _ = AllowSetForegroundWindow(ASFW_ANY);
        }
        var thread = new Thread(() =>
        {
            try
            {
                if (ToastActivator.Line(activation, ToastActivator.Run(activation, new WindowsActivationPlatform())) is { } line)
                {
                    Troubles.Write(line);
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Troubles.Note($"Notifications: activating {activation.AppId}", error);
            }
        })
        {
            IsBackground = true,
            Name = "Karpo toast activation", // not-ui: a thread's name
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
}

/// <summary>The Windows calls, for <see cref="WindowsToastActivator"/>.</summary>
internal sealed unsafe class WindowsActivationPlatform : IActivationPlatform
{
    /// <summary>System.AppUserModel.ToastActivatorCLSID.</summary>
    private static readonly PROPERTYKEY ToastActivatorKey = new() { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 26 };

    public Guid? Activator(string appId) =>
        Registered(appId)
        ?? (PackageFamily(appId) is { } family ? Declared(family, appId[(family.Length + 1)..]) : Shortcut(appId)?.Activator);

    public bool Packaged(string appId) => PackageFamily(appId) is { } family && PackagePath(family) is not null;

    public int Call(Guid activator, ToastActivation activation)
    {
        var classId = activator;
        INotificationActivationCallback* callback = null;
        HRESULT made = CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, __uuidof<INotificationActivationCallback>(), (void**)&callback);
        if (made.FAILED || callback == null)
        {
            return made.FAILED ? made : E.E_NOINTERFACE;
        }
        var texts = new List<nint>();
        try
        {
            if (activation.Kind == ToastActivationKind.Foreground)
            {
                // The app's process may take the foreground from here: it is about to come forward.
                _ = CoAllowSetForegroundWindow((IUnknown*)callback, null);
            }
            var count = activation.Inputs.Count;
            var data = stackalloc NOTIFICATION_USER_INPUT_DATA[Math.Max(1, count)];
            for (var index = 0; index < count; index++)
            {
                var key = Marshal.StringToCoTaskMemUni(activation.Inputs[index].Key);
                var value = Marshal.StringToCoTaskMemUni(activation.Inputs[index].Value);
                texts.Add(key);
                texts.Add(value);
                data[index].Key = (char*)key;
                data[index].Value = (char*)value;
            }
            fixed (char* app = activation.AppId)
            fixed (char* arguments = activation.Arguments)
            {
                return callback->Activate(app, arguments, count > 0 ? data : null, (uint)count);
            }
        }
        finally
        {
            foreach (var text in texts)
            {
                Marshal.FreeCoTaskMem(text);
            }
            callback->Release();
        }
    }

    // §491: the roads' shell calls are the launcher's own (Launch.Shell), made off the dock's thread once
    // Launch.Toast has closed what of Karpo's was open.
    public int LaunchPackaged(string appId, string arguments) => Launch.Shell.ActivateApplication(appId, arguments);

    public int? LaunchWithArguments(string appId, string arguments) =>
        Shortcut(appId)?.Target is not { } program || !File.Exists(program)
            ? null
            : Launch.Shell.Execute(program, null, arguments, Path.GetDirectoryName(program)) ? 0 : LastError();

    public int Open(string target) => Launch.Shell.Execute(target, "open", null) ? 0 : LastError();

    /// <summary>The shell's failure as an HRESULT: its last error, read before anything else can set another.</summary>
    private static int LastError() => Marshal.GetLastSystemError() is var error and > 0 ? HRESULT_FROM_WIN32(error) : E.E_FAIL;

    /// <summary>
    /// The activator an app registered by its AUMID: HKCU, then HKLM, AppUserModelId\&lt;AUMID&gt;
    /// CustomActivator — where the Windows App SDK and Windows' own system toasts put theirs.
    /// </summary>
    private static Guid? Registered(string appId)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey($@"Software\Classes\AppUserModelId\{appId}");
                if (key?.GetValue("CustomActivator") is string text && Guid.TryParse(text, out var activator) && activator != Guid.Empty)
                {
                    return activator;
                }
            }
            catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
            {
                // Unreadable is the same as absent: the next place is looked in.
            }
        }
        return null;
    }

    /// <summary>The package family of a packaged app's AUMID ("family!app"), or null for a desktop app's.</summary>
    internal static string? PackageFamily(string appId)
    {
        var bang = appId.IndexOf('!', StringComparison.Ordinal);
        return bang > 0 && bang < appId.Length - 1 && !appId.AsSpan(0, bang).ContainsAny('\\', '/') ? appId[..bang] : null;
    }

    /// <summary>Where the package of <paramref name="family"/> is installed, or null where this PC has none.</summary>
    private static string? PackagePath(string family)
    {
        uint count = 0;
        uint length = 0;
        fixed (char* name = family)
        {
            // Asked once for the sizes (ERROR_INSUFFICIENT_BUFFER), then for the names.
            _ = GetPackagesByPackageFamily(name, &count, null, &length, null);
            if (count == 0 || length == 0)
            {
                return null;
            }
            var names = stackalloc char*[(int)count];
            var buffer = new char[length];
            fixed (char* text = buffer)
            {
                if (GetPackagesByPackageFamily(name, &count, names, &length, text) != 0 || count == 0)
                {
                    return null;
                }
                uint size = 0;
                _ = GetPackagePathByFullName(names[0], &size, null);
                if (size == 0)
                {
                    return null;
                }
                var path = new char[size];
                fixed (char* at = path)
                {
                    return GetPackagePathByFullName(names[0], &size, at) == 0 ? new string(at) : null;
                }
            }
        }
    }

    /// <summary>
    /// The ToastActivatorCLSID a packaged app declares in its manifest for its application
    /// <paramref name="app"/> (desktop:ToastNotificationActivation, under that Application's
    /// Extensions), as the new Outlook and Teams do.
    /// </summary>
    private static Guid? Declared(string family, string app)
    {
        if (PackagePath(family) is not { } folder)
        {
            return null;
        }
        try
        {
            var manifest = new XmlDocument();
            manifest.LoadXml(File.ReadAllText(Path.Combine(folder, "AppxManifest.xml")));
            foreach (var node in Descendants(manifest.DocumentElement, "Application"))
            {
                if (!string.Equals(Attribute(node, "Id"), app, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (var declared in Descendants(node, "ToastNotificationActivation"))
                {
                    if (Guid.TryParse(Attribute(declared, "ToastActivatorCLSID"), out var activator))
                    {
                        return activator;
                    }
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A manifest that can't be read declares nothing that can be used.
        }
        return null;
    }

    /// <summary>
    /// The Start menu shortcut whose AUMID is <paramref name="appId"/>: its toast activator and its
    /// program. Read from the shortcuts themselves, the current user's and everyone's, because the
    /// AppsFolder item does not carry the activator (see the top of this file). About 210 of them
    /// on the person's PC, each a property store opened and closed, off the dock's thread.
    /// </summary>
    private static (Guid? Activator, string? Target)? Shortcut(string appId)
    {
        foreach (var folder in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
        {
            string[] links;
            try
            {
                links = Directory.GetFiles(Environment.GetFolderPath(folder), "*.lnk", SearchOption.AllDirectories);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                continue;
            }
            foreach (var link in links)
            {
                if (Read(link, appId) is { } found)
                {
                    return found;
                }
            }
        }
        return null;
    }

    /// <summary>The shortcut's activator and program where its AUMID is <paramref name="appId"/>; null where it is another's.</summary>
    private static (Guid? Activator, string? Target)? Read(string link, string appId)
    {
        IPropertyStore* store = null;
        fixed (char* path = link)
        {
            if (SHGetPropertyStoreFromParsingName(path, null, GETPROPERTYSTOREFLAGS.GPS_DEFAULT, __uuidof<IPropertyStore>(), (void**)&store).FAILED || store == null)
            {
                return null;
            }
        }
        try
        {
            if (!string.Equals(Text(store, PKEY.PKEY_AppUserModel_ID), appId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            Guid? activator = null;
            var key = ToastActivatorKey;
            PROPVARIANT value = default;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                Guid read;
                if (PropVariantToGUID(&value, &read).SUCCEEDED && read != Guid.Empty)
                {
                    activator = read;
                }
                _ = PropVariantClear(&value);
            }
            return (activator, Text(store, PKEY.PKEY_Link_TargetParsingPath));
        }
        finally
        {
            store->Release();
        }
    }

    private static string? Text(IPropertyStore* store, PROPERTYKEY key)
    {
        PROPVARIANT value = default;
        if (store->GetValue(&key, &value).FAILED)
        {
            return null;
        }
        char* text = null;
        var converted = PropVariantToStringAlloc(&value, &text);
        _ = PropVariantClear(&value);
        return converted.SUCCEEDED ? ComUtil.FromTaskMem(text) : null;
    }

    private static IEnumerable<IXmlNode> Descendants(IXmlNode? node, string localName)
    {
        if (node is null)
        {
            yield break;
        }
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType != NodeType.ElementNode)
            {
                continue;
            }
            if (string.Equals(child.LocalName as string, localName, StringComparison.Ordinal))
            {
                yield return child;
            }
            foreach (var deeper in Descendants(child, localName))
            {
                yield return deeper;
            }
        }
    }

    private static string? Attribute(IXmlNode node, string name) => node.Attributes?.GetNamedItem(name)?.NodeValue as string;
}
