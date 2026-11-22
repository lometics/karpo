// Pinning by dropping onto the dock: files, folders, web links (dragged or as
// text), and what only the shell can name, such as an app dragged out of
// Start (a Shell IDList Array with no file behind it), This PC or a Control
// Panel item.
//
// OLE calls these on the UI thread from inside its drag loop. Each call is
// passed to the dock as a sent message, so all the state stays in one place
// and goes through the same path as everything else.
//
// The effect under the cursor is decided here, not by the dock's reply, as in
// the Rust: the target reads the data object on entry, and again on drop, and
// answers DROPEFFECT_LINK while there is something to pin (DROPEFFECT_COPY
// where the source doesn't offer a link; never MOVE, which would have the
// source delete what was dragged) and DROPEFFECT_NONE otherwise. The LRESULT
// the dock returns for WM_KARPO_DRAG is ignored. Enter and Over carry no
// targets, so the dock has nothing to decide from, and a dock that returns 0
// for every message, as the Rust one does, would turn every drop away.
//
// The COM object is built by hand so it needs no marshalling and stays
// AOT-safe: a native block whose first field points at one static vtable of
// UnmanagedCallersOnly functions. They are MemberFunction because TerraFX
// declares its vtable slots that way; a plain `unmanaged` pointer won't fit.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock;

public enum DragPhase
{
    Enter,
    Over,
    Leave,
    Drop,
}

/// <summary>
/// Sent as a GCHandle in the LPARAM of <see cref="DropTarget.WM_KARPO_DRAG"/>
/// (read it with <see cref="DropTarget.FromLParam"/>); valid for that call only.
/// </summary>
public sealed class Drag
{
    public DragPhase Phase;

    /// <summary>Screen coordinates.</summary>
    public POINT At;

    public List<string> Targets = [];
}

public static unsafe class DropTarget
{
    public const uint WM_KARPO_DRAG = WM.WM_APP + 2;

    // What OLE holds a pointer to. The vtable pointer has to come first: that
    // is all an IDropTarget* is to the caller.
    [StructLayout(LayoutKind.Sequential)]
    private struct Target
    {
        public IDropTarget.Vtbl<IDropTarget>* Vtable;
        public int Refs;
        public HWND Dock;

        // Whether the data being dragged holds anything to pin. Set on entry
        // and on drop; Over only repeats it.
        public bool Accepted;

        // The effects the source allows, as it said on entry.
        public uint Allowed;

        // app clicks: a window that takes nothing dropped, only hears the drag go by (the window list).
        public bool Refuses;
    }

    // Shared by every target and never freed, like a C++ vtable.
    private static readonly IDropTarget.Vtbl<IDropTarget>* Table = BuildTable();

    /// <summary>
    /// A new IDropTarget with one reference, which forwards every call to
    /// <paramref name="dock"/> as a sent <see cref="WM_KARPO_DRAG"/>. The caller
    /// hands it to RegisterDragDrop and then releases its own reference. One that
    /// <paramref name="refuses"/> takes nothing dropped (no effect, ever) and only tells of
    /// the drag going by: the window list, whose cards a drag rests on (Dock.AppClicks.cs).
    /// </summary>
    public static IDropTarget* Create(HWND dock, bool refuses = false)
    {
        var target = (Target*)NativeMemory.Alloc((nuint)sizeof(Target));
        *target = new Target { Vtable = Table, Refs = 1, Dock = dock, Accepted = false, Refuses = refuses };
        return (IDropTarget*)target;
    }

    /// <summary>The <see cref="Drag"/> behind the LPARAM of a WM_KARPO_DRAG.</summary>
    public static Drag FromLParam(LPARAM lParam) => (Drag)GCHandle.FromIntPtr(lParam).Target!;

    private static IDropTarget.Vtbl<IDropTarget>* BuildTable()
    {
        var table = (IDropTarget.Vtbl<IDropTarget>*)NativeMemory.Alloc((nuint)sizeof(IDropTarget.Vtbl<IDropTarget>));
        table->QueryInterface = &QueryInterface;
        table->AddRef = &AddRef;
        table->Release = &Release;
        table->DragEnter = &DragEnter;
        table->DragOver = &DragOver;
        table->DragLeave = &DragLeave;
        table->Drop = &Drop;
        return table;
    }

    // --- IUnknown ----------------------------------------------------------

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static int QueryInterface(IDropTarget* self, Guid* iid, void** result)
    {
        if (result == null)
        {
            return E.E_POINTER;
        }
        if (iid != null && (*iid == IID.IID_IUnknown || *iid == IID.IID_IDropTarget))
        {
            Interlocked.Increment(ref ((Target*)self)->Refs);
            *result = self;
            return S_OK;
        }
        *result = null;
        return iid == null ? E.E_POINTER : E.E_NOINTERFACE;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static uint AddRef(IDropTarget* self) =>
        (uint)Interlocked.Increment(ref ((Target*)self)->Refs);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static uint Release(IDropTarget* self)
    {
        var refs = Interlocked.Decrement(ref ((Target*)self)->Refs);
        if (refs == 0)
        {
            NativeMemory.Free(self);
        }
        return (uint)refs;
    }

    // --- IDropTarget -------------------------------------------------------
    //
    // None of these may let an exception escape: thrown out of an
    // UnmanagedCallersOnly method, it ends the process instead of the drag.

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static int DragEnter(IDropTarget* self, IDataObject* data, uint keys, POINTL at, uint* effect)
    {
        var target = (Target*)self;
        try
        {
            target->Allowed = effect != null ? *effect : (uint)DROPEFFECT_LINK;
            target->Accepted = data != null && Targets(data).Count > 0;
            Tell(target, DragPhase.Enter, at, []);
            Effect(target, effect);
            return S_OK;
        }
        catch (Exception error)
        {
            return Failed(target, effect, error);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static int DragOver(IDropTarget* self, uint keys, POINTL at, uint* effect)
    {
        var target = (Target*)self;
        try
        {
            Tell(target, DragPhase.Over, at, []);
            Effect(target, effect);
            return S_OK;
        }
        catch (Exception error)
        {
            return Failed(target, effect, error);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static int DragLeave(IDropTarget* self)
    {
        var target = (Target*)self;
        try
        {
            Tell(target, DragPhase.Leave, default, []);
            return S_OK;
        }
        catch (Exception error)
        {
            return Failed(target, null, error);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvMemberFunction) })]
    private static int Drop(IDropTarget* self, IDataObject* data, uint keys, POINTL at, uint* effect)
    {
        var target = (Target*)self;
        try
        {
            List<string> found = data != null ? Targets(data) : [];
            target->Accepted = found.Count > 0;
            Effect(target, effect);
            Tell(target, DragPhase.Drop, at, found);
            return S_OK;
        }
        catch (Exception error)
        {
            return Failed(target, effect, error);
        }
    }

    private static void Tell(Target* target, DragPhase phase, POINTL at, List<string> targets)
    {
        var drag = new Drag { Phase = phase, At = new POINT { x = at.x, y = at.y }, Targets = targets };
        var handle = GCHandle.Alloc(drag);
        try
        {
            // The reply is not used; see the top of the file.
            SendMessageW(target->Dock, WM_KARPO_DRAG, default, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
    }

    private static void Effect(Target* target, uint* into)
    {
        if (into == null)
        {
            return;
        }
        var allowed = target->Allowed;
        *into = !target->Accepted || target->Refuses ? (uint)DROPEFFECT_NONE
            : (allowed & (uint)DROPEFFECT_LINK) != 0 || allowed == 0 ? (uint)DROPEFFECT_LINK
            : (allowed & (uint)DROPEFFECT_COPY) != 0 ? (uint)DROPEFFECT_COPY
            : (uint)DROPEFFECT_NONE;
    }

    private static int Failed(Target* target, uint* effect, Exception error)
    {
        target->Accepted = false;
        Effect(target, effect);
        Console.Error.WriteLine($"Karpo: {error.Message}");
        return E.E_UNEXPECTED;
    }

    // --- Reading the data object -------------------------------------------

    /// <summary>
    /// What the dragged data would pin. Links first: a link dragged out of a
    /// browser also carries its text, and sometimes a temporary .url file that
    /// would be gone by the next start. Then files and folders; then what only
    /// the shell can name (an app from Start); then a web address as text.
    /// </summary>
    private static List<string> Targets(IDataObject* data)
    {
        if (DropFormats.Link(Text(data, Format("UniformResourceLocatorW"))) is { } url)
        {
            return [url];
        }
        var paths = Files(data);
        if (paths.Count > 0)
        {
            return paths;
        }
        var items = ShellItems(data);
        if (items.Count > 0)
        {
            return items;
        }
        if (DropFormats.Link(Text(data, (ushort)CF.CF_UNICODETEXT)) is { } text)
        {
            return [text];
        }
        return [];
    }

    private static ushort Format(string name)
    {
        fixed (char* text = name)
        {
            return (ushort)RegisterClipboardFormatW(text);
        }
    }

    /// <summary>
    /// The shell items of a Shell IDList Array (CFSTR_SHELLIDLIST): a parent
    /// folder's ID list and its items' lists relative to it, each as the pin
    /// that opens it again (see <see cref="DropFormats.ShellTarget"/>). An item
    /// the shell can't find again by that name is left out.
    /// </summary>
    private static List<string> ShellItems(IDataObject* data)
    {
        var found = new List<string>();
        var request = Request(Format("Shell IDList Array")); // not-ui: a clipboard format
        STGMEDIUM medium = default;
        if (data->GetData(&request, &medium).FAILED)
        {
            return found;
        }
        HGLOBAL global = medium.hGlobal;
        var start = (byte*)GlobalLock(global);
        try
        {
            if (start == null)
            {
                return found;
            }
            var size = (int)Math.Min((ulong)GlobalSize(global), int.MaxValue);
            if (DropFormats.IdListOffsets(new ReadOnlySpan<byte>(start, size)) is not { } offsets)
            {
                return found;
            }
            var parent = (ITEMIDLIST*)(start + offsets.Parent);
            var folder = ParsingName(parent);
            foreach (var offset in offsets.Items)
            {
                var full = ILCombine(parent, (ITEMIDLIST*)(start + offset));
                if (full == null)
                {
                    continue;
                }
                try
                {
                    if (DropFormats.ShellTarget(ParsingName(full), folder) is { } pin && Parses(pin))
                    {
                        found.Add(pin);
                    }
                }
                finally
                {
                    ILFree(full);
                }
            }
        }
        finally
        {
            if (start != null)
            {
                _ = GlobalUnlock(global);
            }
            ReleaseStgMedium(&medium);
        }
        return found;
    }

    /// <summary>The shell's desktop-relative parsing name for an absolute ID list; null when it has none.</summary>
    private static string? ParsingName(ITEMIDLIST* list)
    {
        IShellItem* item = null;
        if (SHCreateItemFromIDList(list, __uuidof<IShellItem>(), (void**)&item).FAILED || item == null)
        {
            return null;
        }
        try
        {
            char* name = null;
            return item->GetDisplayName(SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &name).SUCCEEDED ? ComUtil.FromTaskMem(name) : null;
        }
        finally
        {
            item->Release();
        }
    }

    /// <summary>Whether the shell finds <paramref name="target"/> again, as the pin will ask it to.</summary>
    private static bool Parses(string target)
    {
        var item = Items.ShellItem(target);
        if (item == null)
        {
            return false;
        }
        item->Release();
        return true;
    }

    private static FORMATETC Request(ushort format) => new()
    {
        cfFormat = format,
        ptd = null,
        dwAspect = (uint)DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        tymed = (uint)TYMED.TYMED_HGLOBAL,
    };

    private static string? Text(IDataObject* data, ushort format)
    {
        var request = Request(format);
        STGMEDIUM medium = default;
        if (data->GetData(&request, &medium).FAILED)
        {
            return null;
        }
        HGLOBAL global = medium.hGlobal;
        var start = (char*)GlobalLock(global);
        string? text = null;
        if (start != null)
        {
            var units = new ReadOnlySpan<char>(start, (int)Math.Min((ulong)GlobalSize(global) / 2, int.MaxValue));
            var length = units.IndexOf('\0');
            text = Lossy(length < 0 ? units : units[..length]);
        }
        GlobalUnlock(global);
        ReleaseStgMedium(&medium);
        return text;
    }

    private static List<string> Files(IDataObject* data)
    {
        var found = new List<string>();
        var request = Request((ushort)CF.CF_HDROP);
        STGMEDIUM medium = default;
        if (data->GetData(&request, &medium).FAILED)
        {
            return found;
        }
        var drop = (HDROP)(void*)medium.hGlobal;
        var count = DragQueryFileW(drop, uint.MaxValue, null, 0);
        for (uint index = 0; index < count; index++)
        {
            var length = DragQueryFileW(drop, index, null, 0);
            var path = new char[length + 1];
            fixed (char* buffer = path)
            {
                var copied = DragQueryFileW(drop, index, buffer, length + 1);
                found.Add(Lossy(path.AsSpan(0, (int)Math.Min(copied, length + 1))));
            }
        }
        ReleaseStgMedium(&medium);
        return found;
    }

    // String::from_utf16_lossy in the Rust: an unpaired surrogate becomes U+FFFD.
    private static string Lossy(ReadOnlySpan<char> units) =>
        Encoding.Unicode.GetString(MemoryMarshal.AsBytes(units));
}

/// <summary>What dropped data means as a pin; the parts that need no shell, for the drop target and its tests.</summary>
public static class DropFormats
{
    /// <summary>The Applications folder (shell:AppsFolder), as the shell names it.</summary>
    public const string AppsFolder = "::{4234D49B-0245-4DF3-B780-3893943456E1}";

    /// <summary>
    /// A dragged link, or text, as a pin: a web address alone on its first
    /// line (angle brackets or quotes around it are dropped); null for
    /// anything else, so a dragged sentence pins nothing.
    /// </summary>
    public static string? Link(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        var line = (end < 0 ? text : text[..end]).Trim().Trim('<', '>', '"').Trim();
        return line.Length > 0 && Items.IsWeb(line) && !line.Any(char.IsWhiteSpace) ? line : null;
    }

    /// <summary>
    /// The pin for a dragged shell item: its desktop-relative parsing name
    /// <paramref name="name"/>, in a folder named <paramref name="folder"/>. An
    /// app from the Applications folder is named only by its app ID there,
    /// which the shell parses back only as <c>shell:AppsFolder\&lt;ID&gt;</c>;
    /// a virtual folder ("::{CLSID}") becomes <c>shell:::{CLSID}</c>, which
    /// the shell opens as well as it parses; a path stays a path.
    /// </summary>
    public static string? ShellTarget(string? name, string? folder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        name = name.Trim();
        if (string.Equals(folder?.Trim(), AppsFolder, StringComparison.OrdinalIgnoreCase))
        {
            // Some Windows builds name it from the desktop down after all.
            if (name.StartsWith(AppsFolder + "\\", StringComparison.OrdinalIgnoreCase))
            {
                name = name[(AppsFolder.Length + 1)..];
            }
            return name.Length > 0 && !name.StartsWith("::", StringComparison.Ordinal) ? @"shell:AppsFolder\" + name : null;
        }
        if (name.StartsWith(AppsFolder + "\\", StringComparison.OrdinalIgnoreCase))
        {
            return @"shell:AppsFolder\" + name[(AppsFolder.Length + 1)..];
        }
        return name.StartsWith("::", StringComparison.Ordinal) ? "shell:" + name : name;
    }

    /// <summary>
    /// Where the ID lists of a Shell IDList Array (a CIDA) start: the parent
    /// folder's, then each item's, relative to it; null when the block is not a
    /// well-formed CIDA (every list must end inside it).
    /// </summary>
    public static (int Parent, int[] Items)? IdListOffsets(ReadOnlySpan<byte> cida)
    {
        if (cida.Length < 8)
        {
            return null;
        }
        var count = BinaryPrimitives.ReadUInt32LittleEndian(cida);
        // Nobody drags a million items; a count that large is a broken block.
        if (count == 0 || count > 65_536 || 4L + (count + 1) * 4L > cida.Length)
        {
            return null;
        }
        var header = 4 + ((int)count + 1) * 4;
        if (Offset(cida, 0, header) is not { } parent)
        {
            return null;
        }
        var items = new int[count];
        for (var index = 0; index < count; index++)
        {
            if (Offset(cida, index + 1, header) is not { } item)
            {
                return null;
            }
            items[index] = item;
        }
        return (parent, items);
    }

    /// <summary>The CIDA's offset in <paramref name="slot"/>, when it points past the header at a list that ends inside the block.</summary>
    private static int? Offset(ReadOnlySpan<byte> cida, int slot, int header)
    {
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(cida[(4 + slot * 4)..]);
        return offset >= header && offset < cida.Length && Ends(cida, (int)offset) ? (int)offset : null;
    }

    /// <summary>Whether the ID list at <paramref name="at"/> ends (a zero-length item) inside <paramref name="block"/>.</summary>
    private static bool Ends(ReadOnlySpan<byte> block, int at)
    {
        while (at + 2 <= block.Length)
        {
            var size = BinaryPrimitives.ReadUInt16LittleEndian(block[at..]);
            if (size == 0)
            {
                return true;
            }
            // An item holds its own two-byte size at least.
            if (size < 2)
            {
                return false;
            }
            at += size;
        }
        return false;
    }
}

/// <summary>Adding pins without doubling them. Pure, for the dock and its tests.</summary>
public static class PinList
{
    /// <summary>
    /// Whether two pins open the same thing: letter case aside (Windows paths
    /// and web hosts ignore it), and a trailing slash aside, which a folder or
    /// a site may be written with or without.
    /// </summary>
    public static bool Same(string? a, string? b) =>
        a is not null && b is not null && string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <paramref name="pins"/> with <paramref name="dropped"/> inserted at
    /// <paramref name="position"/> (clamped), in their order, leaving out any
    /// already pinned or dropped twice. Dividers ("|") already there stay.
    /// </summary>
    public static List<string> Insert(IReadOnlyList<string> pins, IEnumerable<string> dropped, int position)
    {
        var result = pins.ToList();
        position = Math.Clamp(position, 0, result.Count);
        foreach (var target in dropped)
        {
            if (string.IsNullOrWhiteSpace(target) || target.Trim() == "|" || result.Exists(pin => Same(pin, target)))
            {
                continue;
            }
            result.Insert(position++, target.Trim());
        }
        return result;
    }

    private static string Normal(string target)
    {
        var text = target.Trim();
        // "C:\" and "https://" keep their slash: it is what they are.
        while (text.Length > 3 && text[^1] is '\\' or '/' && text[^2] is not (':' or '/'))
        {
            text = text[..^1];
        }
        return text;
    }
}
