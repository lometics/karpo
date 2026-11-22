// Glue between the two interop worlds this project lives in:
//
// - TerraFX: raw C-style bindings (Win32, Direct2D, DirectWrite, and the
//   Windows.UI.Composition interop interfaces), used through pointers exactly
//   like the C headers and the Rust `windows` crate.
// - CsWinRT: the Windows SDK projection (Windows.UI.Composition and friends),
//   used as ordinary C# objects.
//
// Composition objects cross between the two here and nowhere else.

using System.Runtime.CompilerServices;
using TerraFX.Interop.Windows;
using WinRT;
using static TerraFX.Interop.Windows.Windows;
using INativeGuid = TerraFX.Interop.INativeGuid;

namespace Karpo.Dock.Native;

public static unsafe class ComUtil
{
    /// <summary>Throws for a failed HRESULT, naming the call that failed.</summary>
    public static void Check(HRESULT hr, [CallerArgumentExpression(nameof(hr))] string? call = null)
    {
        if (hr.FAILED)
        {
            throw new NativeException($"{call} failed: 0x{hr.Value:X8}", hr.Value);
        }
    }

    /// <summary>
    /// The IInspectable behind a projected WinRT object. Borrowed: it lives as
    /// long as <paramref name="value"/> does and must not be released.
    /// </summary>
    public static IUnknown* Borrow(object value) =>
        (IUnknown*)((IWinRTObject)value).NativeObject.ThisPtr;

    /// <summary>
    /// QueryInterface on a projected WinRT object, for the classic COM interop
    /// interfaces (ICompositorDesktopInterop and friends). The caller owns the
    /// result and releases it.
    /// </summary>
    public static T* Query<T>(object value)
        where T : unmanaged, INativeGuid
    {
        T* result = null;
        Check(Borrow(value)->QueryInterface(__uuidof<T>(), (void**)&result));
        return result;
    }

    /// <summary>QueryInterface between two raw COM pointers. The caller owns the result.</summary>
    public static T* Query<T>(IUnknown* value)
        where T : unmanaged, INativeGuid
    {
        T* result = null;
        Check(value->QueryInterface(__uuidof<T>(), (void**)&result));
        return result;
    }

    /// <summary>
    /// Wraps a raw WinRT pointer the caller owns in a projected object, and
    /// gives up the caller's reference.
    /// </summary>
    public static T Adopt<T>(void* raw)
    {
        try
        {
            return MarshalInspectable<T>.FromAbi((nint)raw)!;
        }
        finally
        {
            ((IUnknown*)raw)->Release();
        }
    }

    /// <summary>Releases a raw COM pointer and clears it; null is fine.</summary>
    public static void Release<T>(ref T* pointer)
        where T : unmanaged
    {
        if (pointer != null)
        {
            ((IUnknown*)pointer)->Release();
            pointer = null;
        }
    }

    /// <summary>A string from CoTaskMemAlloc'ed UTF-16 the caller owns, which is then freed.</summary>
    public static string? FromTaskMem(char* text)
    {
        if (text == null)
        {
            return null;
        }
        try
        {
            return new string(text);
        }
        finally
        {
            CoTaskMemFree(text);
        }
    }
}

public sealed class NativeException(string message, int hresult) : Exception(message)
{
    public int Code { get; } = hresult;
}
