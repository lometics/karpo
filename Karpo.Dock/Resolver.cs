// The application ID the taskbar groups by, for windows and shortcuts that
// don't set one themselves.
//
// When neither an app nor its shortcut names an ID, the shell derives one,
// and Microsoft documents that apps cannot retrieve it. The shell's own
// resolver is the only way to get the same answer. Its interface has kept its
// IID since Windows 8, and EarTrumpet and System Informer call it the same
// way. Without it, File Explorer, Electron apps that set their ID per process
// and every packaged app show up twice: once pinned, once open.

using System.Runtime.CompilerServices;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe class Resolver : IDisposable
{
    private static readonly Guid ClassId = new("660b90c8-73a9-4b58-8cae-355b7f55341b");

    // Null when the shell would not create one; every question then goes
    // unanswered and callers fall back to what the app declared itself.
    private IApplicationResolver* resolver;

    public Resolver()
    {
        var classId = ClassId;
        var interfaceId = IApplicationResolver.Id;
        IApplicationResolver* created = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, &interfaceId, (void**)&created).SUCCEEDED)
        {
            resolver = created;
        }
    }

    public string? Window(HWND hwnd)
    {
        if (resolver == null)
        {
            return null;
        }
        char* id = null;
        BOOL prevented = BOOL.FALSE;
        BOOL explicitId = BOOL.FALSE;
        BOOL embedded = BOOL.FALSE;
        if (resolver->GetAppIDForWindow(hwnd, &id, &prevented, &explicitId, &embedded).FAILED)
        {
            return null;
        }
        return Taken(id);
    }

    public string? Shortcut(IShellItem* item)
    {
        if (resolver == null)
        {
            return null;
        }
        char* id = null;
        if (resolver->GetAppIDForShortcut(item, &id).FAILED)
        {
            return null;
        }
        return Taken(id);
    }

    public void Dispose()
    {
        if (resolver != null)
        {
            resolver->Release();
            resolver = null;
        }
    }

    /// <summary>Copies a shell-allocated ID and frees it; an empty one counts as none.</summary>
    private static string? Taken(char* id)
    {
        var text = ComUtil.FromTaskMem(id);
        return string.IsNullOrEmpty(text) ? null : text;
    }
}

/// <summary>
/// The shell's undocumented IApplicationResolver, laid out the way TerraFX lays
/// out COM interfaces, since TerraFX has no binding for it: a vtable pointer,
/// and calls through the slot each method has after IUnknown's three. The
/// slots return <c>int</c>, not HRESULT: the native side returns a plain LONG,
/// and a struct return type would make .NET pass a hidden return buffer instead.
/// </summary>
internal unsafe struct IApplicationResolver
{
    public static readonly Guid Id = new("DE25675A-72DE-44B4-9373-05170450C140");

    // Filled in by the native object; C# only ever reads it through a pointer.
#pragma warning disable CS0649
    public void** lpVtbl;
#pragma warning restore CS0649

    public HRESULT QueryInterface(Guid* riid, void** ppvObject) =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, Guid*, void**, int>)lpVtbl[0])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this), riid, ppvObject);

    public uint AddRef() =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, uint>)lpVtbl[1])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this));

    public uint Release() =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, uint>)lpVtbl[2])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this));

    public HRESULT GetAppIDForShortcut(IShellItem* item, char** id) =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, IShellItem*, char**, int>)lpVtbl[3])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this), item, id);

    public HRESULT GetAppIDForShortcutObject(IShellLinkW* link, IShellItem* item, char** id) =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, IShellLinkW*, IShellItem*, char**, int>)lpVtbl[4])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this), link, item, id);

    public HRESULT GetAppIDForWindow(HWND hwnd, char** id, BOOL* pinningPrevented, BOOL* explicitId, BOOL* embeddedShortcutValid) =>
        ((delegate* unmanaged[MemberFunction]<IApplicationResolver*, HWND, char**, BOOL*, BOOL*, BOOL*, int>)lpVtbl[5])(
            (IApplicationResolver*)Unsafe.AsPointer(ref this), hwnd, id, pinningPrevented, explicitId, embeddedShortcutValid);
}
