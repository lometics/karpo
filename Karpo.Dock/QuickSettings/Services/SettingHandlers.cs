// Windows' own quick-settings tiles are handlers of the Settings app. Each area of Settings has a
// DLL in System32 that exports GetSetting(HSTRING id, ISettingItem**), and the tile reads and sets
// the item's "Value" — which is the one road for the switches Windows lets no app switch through a
// public API: energy saver, and night light. The ids are the tiles' own, as the quick actions list
// them (HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\ActionCenter\Quick Actions\All):
//
//   energy saver    SettingsHandlers_OneCore_BatterySaver.dll         SystemSettings_BatterySaver_LandingPage_OverrideControl
//   night light     SettingsHandlers_Display.dll                      SystemSettings_Display_BlueLight_ManualToggleQuickAction
//   nearby sharing  SettingsHandlers_SharedExperiences_Rome.dll       SystemSettings_SharedExperiences_NearShareQuickAction
//   color filters   SettingsHandlers_nt.dll                           SystemSettings_Accessibility_ColorFiltering_IsEnabled
//   mono audio      SettingsHandlers_Accessibility.dll                SystemSettings_Accessibility_IsAudioMonoMixStateEnabled
//   game mode       SettingsHandlers_Gaming.dll                       SystemSettings_Gaming_GameMode_Toggle (its Settings page's switch)
//
// The last three were read and switched once each on Windows 11 (26300, 2026-10-11), and put back
// at once: SettingToggles.cs.
//
// Nearby sharing's was read on Windows 11 (26300, 2026-10-11) the same way: a switch (type 3)
// holding a boolean that matched the policy Windows keeps (Nearby.cs), off with it at 0. Its
// handler is also the one that turns Bluetooth on for nearby sharing where it is off, with
// Windows' own toast, which is why it is gone through rather than the registry alone.
//
// ISettingItem is undocumented. Its shape is the one open tools use — stegru/windows-settings, and
// blendonl/mshell, whose slots were checked against Microsoft's public symbols for 26100 — and it
// was checked here with the read-only half on Windows 11 25H2 (26200): for both ids GetSetting
// answers, the item says its own id back, its type is 3 (a switch), and GetValue("Value") is an
// IPropertyValue holding a boolean that matches the state. SetValue was not called to check it: no
// test and no probe may switch the person's energy saver or night light.
//
//   IID ISettingItem {40C037CC-D8BF-489E-8697-D66BAA3221BF}, an IInspectable; after its six:
//     6 get_Id(HSTRING*)            10 get_IsApplicable(boolean*)   13 GetValue(HSTRING, IInspectable**)
//     7 get_Type(int*)              11 get_Description(HSTRING*)    14 SetValue(HSTRING, IInspectable*)
//     8 get_IsSetByGroupPolicy      12 get_IsUpdating(boolean*)
//     9 get_IsEnabled(boolean*)
//
// Every call is guarded before anything is set: the item must say back the id it was asked for,
// be a switch, and hold a boolean, or it is not used and the widget does what it did before —
// opens the setting's page.
//
// THE HANDLERS LIVE ON ONE THREAD OF THEIR OWN: a single-threaded apartment with a message loop,
// for as long as the dock runs. A handler finishes starting up on that loop — night light's said
// it did not apply until its thread had pumped messages for half a second, and then said it did —
// so the thread waits on its work and its messages together (MsgWaitForMultipleObjectsEx), and
// every call from the switches is handed to it and waited for with a limit, from the pool. A
// handler that never answers costs its thread and nothing else: the switch shows no state and a
// click opens the page. The libraries are never unloaded, since a handler's own threads may
// outlive the item.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>A setting Windows' tile switches, as a switch asks it: through the handler, or a made-up one in the tests.</summary>
internal interface ISettingToggle
{
    /// <summary>Whether Windows offers it here: the handler says it applies and may be changed. Blocks: on the pool.</summary>
    bool Usable { get; }

    /// <summary>Where it stands, or null where the handler did not say. Blocks: on the pool.</summary>
    bool? Value();

    /// <summary>Switches it; false where the handler refused. Blocks: on the pool.</summary>
    bool Set(bool on);
}

/// <summary>One tile's setting, through its Settings handler (see the top of this file).</summary>
internal sealed unsafe class SettingHandler : ISettingToggle
{
    /// <summary>Energy saver's tile.</summary>
    internal static readonly (string Library, string Id) EnergySaver =
        ("SettingsHandlers_OneCore_BatterySaver.dll", "SystemSettings_BatterySaver_LandingPage_OverrideControl");

    /// <summary>Night light's tile.</summary>
    internal static readonly (string Library, string Id) NightLight =
        ("SettingsHandlers_Display.dll", "SystemSettings_Display_BlueLight_ManualToggleQuickAction");

    /// <summary>Nearby sharing's tile: off, or on as it was last set.</summary>
    internal static readonly (string Library, string Id) NearbySharing =
        ("SettingsHandlers_SharedExperiences_Rome.dll", "SystemSettings_SharedExperiences_NearShareQuickAction");

    /// <summary>Game mode's switch on its Settings page (SettingToggles.cs).</summary>
    internal static readonly (string Library, string Id) GameMode =
        ("SettingsHandlers_Gaming.dll", "SystemSettings_Gaming_GameMode_Toggle");

    /// <summary>Color filters' tile.</summary>
    internal static readonly (string Library, string Id) ColorFilters =
        ("SettingsHandlers_nt.dll", "SystemSettings_Accessibility_ColorFiltering_IsEnabled");

    /// <summary>Mono audio's tile.</summary>
    internal static readonly (string Library, string Id) MonoAudio =
        ("SettingsHandlers_Accessibility.dll", "SystemSettings_Accessibility_IsAudioMonoMixStateEnabled");

    private static readonly Guid ItemInterface = new("40C037CC-D8BF-489E-8697-D66BAA3221BF");
    private static readonly Guid PropertyValueInterface = new("4BD682DD-7554-40E9-9A9B-82654EDE7E62");
    private static readonly Guid PropertyValueStatics = new("629BDBC8-D932-4FF4-96B9-8D96C5C1E858");

    /// <summary>ISettingItem's type for a switch.</summary>
    private const int SwitchType = 3;

    /// <summary>PropertyType.Boolean.</summary>
    private const int BooleanType = 11;

    private const int SlotId = 6;
    private const int SlotType = 7;
    private const int SlotEnabled = 9;
    private const int SlotApplicable = 10;
    private const int SlotGetValue = 13;
    private const int SlotSetValue = 14;
    private const int SlotValueType = 6;
    private const int SlotGetBoolean = 18;
    private const int SlotCreateBoolean = 17;

    /// <summary>How long a handler has to finish starting up before it is asked whether it applies.</summary>
    private const int StartMs = 2000;

    /// <summary>How long a call is waited for before the switch goes on without it.</summary>
    private const int CallMs = 15000;

    private readonly string id;

    // The item, owned by the handlers' thread and only ever touched there.
    private void* item;

    private SettingHandler(void* item, string id)
    {
        this.item = item;
        this.id = id;
    }

    /// <summary>
    /// The handler for <paramref name="setting"/>, or null where it is not there or is not the
    /// switch this expects (see the top of this file). Blocks: on the pool.
    /// </summary>
    public static SettingHandler? Open((string Library, string Id) setting) =>
        HandlerThread.Run(() => Make(setting.Library, setting.Id), null, CallMs);

    public bool Usable => HandlerThread.Run(() => item != null && Flag(item, SlotApplicable) && Flag(item, SlotEnabled), false, CallMs);

    public bool? Value() => HandlerThread.Run(() => item == null ? null : Read(item), null, CallMs);

    public bool Set(bool on) => HandlerThread.Run(() => item != null && Write(item, on), false, CallMs);

    /// <summary>The item goes on the thread it belongs to.</summary>
    public void Release()
    {
        HandlerThread.Post(() =>
        {
            if (item != null)
            {
                _ = ((IUnknown*)item)->Release();
                item = null;
            }
        });
    }

    /// <summary>On the handlers' thread: loads the library, asks for the item, and checks it is the one asked for.</summary>
    private static SettingHandler? Make(string library, string id)
    {
        HMODULE module;
        fixed (char* name = library)
        {
            // From System32 only, and never unloaded (see the top of this file).
            module = LoadLibraryExW(name, HANDLE.NULL, LOAD.LOAD_LIBRARY_SEARCH_SYSTEM32);
        }
        if (module == HMODULE.NULL)
        {
            return null;
        }
        void* export;
        // A UTF-8 literal is stored with its NUL, which is what GetProcAddress reads to.
        fixed (byte* name = "GetSetting"u8)
        {
            export = GetProcAddress(module, (sbyte*)name);
        }
        if (export == null)
        {
            return null;
        }
        void* found = null;
        var text = Text(id);
        int asked;
        try
        {
            asked = ((delegate* unmanaged[Stdcall]<nint, void**, int>)export)(text, &found);
        }
        finally
        {
            _ = WindowsDeleteString(text);
        }
        if (asked < 0 || found == null)
        {
            return null;
        }
        void* setting = null;
        var wanted = ItemInterface;
        var cast = ((IUnknown*)found)->QueryInterface(&wanted, &setting);
        _ = ((IUnknown*)found)->Release();
        if (cast.FAILED || setting == null)
        {
            return null;
        }
        if (Id(setting) != id || Type(setting) != SwitchType || Read(setting) is null)
        {
            Troubles.Note($"Settings handler {id}", new InvalidOperationException("The handler is not the switch it was asked for."));
            _ = ((IUnknown*)setting)->Release();
            return null;
        }
        // It finishes starting up on this thread's loop; give it that before anybody asks whether it
        // applies (see the top of this file).
        var until = Environment.TickCount64 + StartMs;
        while (!(Flag(setting, SlotApplicable) && Flag(setting, SlotEnabled)) && Environment.TickCount64 < until)
        {
            HandlerThread.Pump();
            Thread.Sleep(25);
        }
        return new SettingHandler(setting, id);
    }

    private static string? Id(void* setting)
    {
        nint text = 0;
        if (((delegate* unmanaged[Stdcall]<void*, nint*, int>)Slot(setting, SlotId))(setting, &text) < 0)
        {
            return null;
        }
        try
        {
            uint length = 0;
            var raw = WindowsGetStringRawBuffer(text, &length);
            return raw == null ? "" : new string(raw, 0, (int)length);
        }
        finally
        {
            _ = WindowsDeleteString(text);
        }
    }

    private static int Type(void* setting)
    {
        var type = -1;
        return ((delegate* unmanaged[Stdcall]<void*, int*, int>)Slot(setting, SlotType))(setting, &type) < 0 ? -1 : type;
    }

    private static bool Flag(void* setting, int slot)
    {
        byte flag = 0;
        return ((delegate* unmanaged[Stdcall]<void*, byte*, int>)Slot(setting, slot))(setting, &flag) >= 0 && flag != 0;
    }

    /// <summary>The item's "Value" as a boolean, or null where it is not one.</summary>
    private static bool? Read(void* setting)
    {
        void* value = null;
        var name = Text("Value");
        int asked;
        try
        {
            asked = ((delegate* unmanaged[Stdcall]<void*, nint, void**, int>)Slot(setting, SlotGetValue))(setting, name, &value);
        }
        finally
        {
            _ = WindowsDeleteString(name);
        }
        if (asked < 0 || value == null)
        {
            return null;
        }
        void* property = null;
        try
        {
            var wanted = PropertyValueInterface;
            if (((IUnknown*)value)->QueryInterface(&wanted, &property).FAILED || property == null)
            {
                return null;
            }
            var type = -1;
            byte flag = 0;
            if (((delegate* unmanaged[Stdcall]<void*, int*, int>)Slot(property, SlotValueType))(property, &type) < 0 || type != BooleanType
                || ((delegate* unmanaged[Stdcall]<void*, byte*, int>)Slot(property, SlotGetBoolean))(property, &flag) < 0)
            {
                return null;
            }
            return flag != 0;
        }
        finally
        {
            if (property != null)
            {
                _ = ((IUnknown*)property)->Release();
            }
            _ = ((IUnknown*)value)->Release();
        }
    }

    /// <summary>Sets the item's "Value" to <paramref name="on"/>, boxed as Windows boxes a boolean.</summary>
    private static bool Write(void* setting, bool on)
    {
        void* statics = null;
        var wanted = PropertyValueStatics;
        var factory = Text("Windows.Foundation.PropertyValue");
        int made;
        try
        {
            made = RoGetActivationFactory(factory, &wanted, &statics);
        }
        finally
        {
            _ = WindowsDeleteString(factory);
        }
        if (made < 0 || statics == null)
        {
            return false;
        }
        void* boxed = null;
        var name = Text("Value");
        try
        {
            if (((delegate* unmanaged[Stdcall]<void*, byte, void**, int>)Slot(statics, SlotCreateBoolean))(statics, on ? (byte)1 : (byte)0, &boxed) < 0 || boxed == null)
            {
                return false;
            }
            return ((delegate* unmanaged[Stdcall]<void*, nint, void*, int>)Slot(setting, SlotSetValue))(setting, name, boxed) >= 0;
        }
        finally
        {
            _ = WindowsDeleteString(name);
            if (boxed != null)
            {
                _ = ((IUnknown*)boxed)->Release();
            }
            _ = ((IUnknown*)statics)->Release();
        }
    }

    private static void* Slot(void* self, int slot) => (*(void***)self)[slot];

    /// <summary>
    /// <paramref name="text"/> as an HSTRING, which the caller deletes (WindowsDeleteString takes a
    /// null one too); null where it could not be made, which a handler reads as an empty id.
    /// </summary>
    private static nint Text(string text)
    {
        nint made = 0;
        fixed (char* chars = text)
        {
            return WindowsCreateString(chars, (uint)text.Length, &made) < 0 ? 0 : made;
        }
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WindowsCreateString(char* sourceString, uint length, nint* hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WindowsDeleteString(nint hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern char* WindowsGetStringRawBuffer(nint hstring, uint* length);

    [DllImport("combase.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int RoGetActivationFactory(nint activatableClassId, Guid* iid, void** factory);
}

/// <summary>
/// The one thread the Settings handlers live on: a single-threaded apartment waiting on its work and
/// its messages together, for as long as the dock runs (see the top of SettingHandlers.cs).
/// </summary>
internal static unsafe class HandlerThread
{
    private static readonly ConcurrentQueue<Action> Work = new();
    private static readonly AutoResetEvent Wake = new(false);
    private static readonly object Gate = new();
    private static Thread? thread;

    /// <summary>
    /// Runs <paramref name="work"/> there and waits for it, <paramref name="ms"/> at the most; what it
    /// answered, or <paramref name="otherwise"/> where it threw or did not answer in time. Never from
    /// the thread itself, and never from the dock's.
    /// </summary>
    public static T Run<T>(Func<T> work, T otherwise, int ms)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception error)
            {
                Troubles.Note("Settings handler", error);
                done.SetResult(otherwise);
            }
        });
        return done.Task.Wait(ms) ? done.Task.Result : otherwise;
    }

    /// <summary>Hands <paramref name="work"/> to the thread without waiting for it.</summary>
    public static void Post(Action work)
    {
        Ensure();
        Work.Enqueue(work);
        Wake.Set();
    }

    /// <summary>Hands on whatever messages are waiting for this thread's windows.</summary>
    internal static void Pump()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }

    private static void Ensure()
    {
        lock (Gate)
        {
            if (thread is not null)
            {
                return;
            }
            thread = new Thread(() => Troubles.Guard("Settings handlers", Loop)) { IsBackground = true, Name = "Karpo settings handlers" }; // not-ui: a thread's name
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
    }

    private static void Loop()
    {
        var wake = (HANDLE)Wake.SafeWaitHandle.DangerousGetHandle();
        while (true)
        {
            _ = MsgWaitForMultipleObjectsEx(1, &wake, INFINITE, QS.QS_ALLINPUT, MWMO.MWMO_INPUTAVAILABLE);
            while (Work.TryDequeue(out var job))
            {
                // The thread is the dock's for as long as it runs: a job that throws is noted, and
                // the next one runs. Thrown out of here, it would end the process.
                try
                {
                    job();
                }
                catch (Exception error)
                {
                    Troubles.Note("Settings handler", error);
                }
            }
            Pump();
        }
    }
}
