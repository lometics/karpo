// UI Automation for Karpo's windows: the providers Narrator and every other client talk to.
//
// Nothing answered WM_GETOBJECT before, so a screen reader found each bar and flyout as a window
// with nothing in it. Now a window that registers a source (IUiaSource: a bar, a menu, a panel of
// controls) answers with a fragment root, and every element of its tree (UiaTree.cs) is a provider
// with the patterns it has: Invoke for what is pressed, Toggle for what is switched, RangeValue for
// a slider. The keyboard's place is the focused element, and moving it raises the focus event, so
// Narrator says where the keyboard went (Win+T on the app buttons, Tab in a panel, the arrows in a
// menu).
//
// **Built by hand, as the other COM callbacks of the dock are** (QuickSettings\Services\Sinks.cs):
// a native object with a table of unmanaged function pointers for each interface, so native AOT
// needs no COM interop. One object answers IRawElementProviderSimple(2), IRawElementProviderFragment,
// IRawElementProviderFragmentRoot (the root only) and the pattern interfaces, each at its own
// pointer inside it, as a C++ object with several bases is laid out. It carries only its window and
// its element's id, so an element whose window or element is gone answers "not available".
//
// **Threads.** UI Automation calls the providers on threads of its own. They never touch the
// window's state: they read the tree, a snapshot the window's thread builds when asked (sent
// WM_KARPO_UIA_TREE, at most every tenth of a second, and not while that thread is in the middle
// of something), and what an element does is posted to the window as WM_KARPO_UIA_ACT and done on
// its thread like a click.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What UI Automation asks an element to do.</summary>
public enum UiaAct
{
    Invoke = 1,
    Toggle,
    SetValue,
    Focus,
    Menu,

    /// <summary>§523: words set as an element's value (ValuePattern); the window takes them (<see cref="Uia.TakeText"/>).</summary>
    SetText,
}

/// <summary>A window's elements for UI Automation: built and acted on on the window's own thread.</summary>
internal interface IUiaSource
{
    /// <summary>The window's tree as it stands.</summary>
    UiaTree Build();

    /// <summary>What UI Automation asked of element <paramref name="id"/>: pressed, switched, set to <paramref name="value"/>, focused, or its menu.</summary>
    void Act(UiaAct act, int id, double value);
}

internal static unsafe class Uia
{
    /// <summary>Sent to a window by UI Automation's threads: build the tree now (Rebuild).</summary>
    public const uint WM_KARPO_UIA_TREE = WM.WM_APP + 0x27;

    /// <summary>Posted to a window: wParam is the act and the element's id, lParam a slider's value in thousandths.</summary>
    public const uint WM_KARPO_UIA_ACT = WM.WM_APP + 0x28;

    private const int UiaRootObjectId = -25;
    private const int UiaAppendRuntimeId = 3;
    private const int ProviderOptionsServerSide = 0x1;
    private const int FocusChangedEvent = 20005;

    // NotificationKind_Other and NotificationProcessing_MostRecent (UIAutomationCore.h).
    private const int NotificationKindOther = 4;
    private const int NotificationProcessingMostRecent = 3;

    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const int E_POINTER = unchecked((int)0x80004003);
    private const int E_FAIL = unchecked((int)0x80004005);
    private const int UIA_E_ELEMENTNOTENABLED = unchecked((int)0x80040200);
    private const int UIA_E_ELEMENTNOTAVAILABLE = unchecked((int)0x80040201);

    /// <summary>How old a tree may be before UI Automation's threads ask the window for a new one.</summary>
    private const long FreshMs = 100;

    private static readonly ConcurrentDictionary<nint, Entry> Entries = new();

    /// <summary>§523: words UI Automation set as an element's value, by its window and its id, till the window takes them.</summary>
    private static readonly ConcurrentDictionary<(nint Window, int Id), string> Texts = new();

    /// <summary>§523: on the window's thread, the words set as element <paramref name="id"/>'s value (<see cref="UiaAct.SetText"/>), taken once; null for none.</summary>
    public static string? TakeText(HWND hwnd, int id) => Texts.TryRemove(((nint)hwnd, id), out var text) ? text : null;

    private sealed class Entry(HWND hwnd, IUiaSource source)
    {
        public HWND Hwnd { get; } = hwnd;

        public IUiaSource Source { get; } = source;

        public volatile UiaTree? Tree;

        public long BuiltAt;

        /// <summary>The root handed to UI Automation, kept while the window is (UiaReturnRawElementProvider keeps its own reference).</summary>
        public Element* Root;
    }

    /// <summary><paramref name="hwnd"/>'s elements come from <paramref name="source"/> from now on, whatever they came from before.</summary>
    public static void Register(HWND hwnd, IUiaSource source)
    {
        if (hwnd == HWND.NULL)
        {
            return;
        }
        var entry = new Entry(hwnd, source);
        Entries.AddOrUpdate((nint)hwnd, entry, (_, old) =>
        {
            entry.Root = old.Root;
            old.Root = null;
            return entry;
        });
    }

    /// <summary>The window is going: UI Automation lets go of its root, and its elements answer "not available" from now on.</summary>
    public static void Forget(HWND hwnd)
    {
        if (!Entries.TryRemove((nint)hwnd, out var entry))
        {
            return;
        }
        _ = UiaReturnRawElementProvider(hwnd, 0, 0, null);
        if (entry.Root != null)
        {
            Release(entry.Root);
            entry.Root = null;
        }
    }

    /// <summary>Whether <paramref name="hwnd"/> has elements (for a test).</summary>
    internal static bool Registered(HWND hwnd) => Entries.ContainsKey((nint)hwnd);

    /// <summary>WM_GETOBJECT: the root of <paramref name="hwnd"/>'s elements, where it has any; null for the default answer.</summary>
    public static LRESULT? Answer(HWND hwnd, WPARAM wParam, LPARAM lParam)
    {
        if ((int)(nint)lParam != UiaRootObjectId || !Entries.TryGetValue((nint)hwnd, out var entry))
        {
            return null;
        }
        if (entry.Root == null)
        {
            entry.Root = Element.Create(hwnd, 0);
        }
        return UiaReturnRawElementProvider(hwnd, (nuint)wParam, (nint)lParam, entry.Root);
    }

    /// <summary>
    /// WM_KARPO_UIA_TREE, on the window's thread: the tree built afresh — unless that thread is in the
    /// middle of something (<paramref name="now"/> false), when the last one stands.
    /// </summary>
    public static LRESULT Rebuild(HWND hwnd, bool now)
    {
        if (!now || !Entries.TryGetValue((nint)hwnd, out var entry))
        {
            return 0;
        }
        try
        {
            entry.Tree = entry.Source.Build();
            Volatile.Write(ref entry.BuiltAt, Environment.TickCount64);
            return 1;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: the accessibility tree of 0x{(nint)hwnd:X} was not built: {error.Message}");
            return 0;
        }
    }

    /// <summary>WM_KARPO_UIA_ACT, on the window's thread: what UI Automation asked, done.</summary>
    public static void Act(HWND hwnd, WPARAM wParam, LPARAM lParam)
    {
        if (!Entries.TryGetValue((nint)hwnd, out var entry))
        {
            return;
        }
        var packed = (nuint)wParam;
        entry.Source.Act((UiaAct)(int)(packed >> 24), (int)(packed & 0xFFFFFF), (int)(nint)lParam / 1000.0);
    }

    /// <summary>
    /// On the window's thread: the keyboard moved in <paramref name="hwnd"/>. The tree is built again
    /// and UI Automation is told which element has it — only while a client listens, which is when
    /// anybody could hear it.
    /// </summary>
    public static void FocusMoved(HWND hwnd)
    {
        if (!Entries.TryGetValue((nint)hwnd, out var entry) || UiaClientsAreListening() == 0)
        {
            return;
        }
        Rebuild(hwnd, now: true);
        if (entry.Tree is not { } tree)
        {
            return;
        }
        var focused = Element.Create(hwnd, FocusTarget(tree));
        _ = UiaRaiseAutomationEvent(focused, FocusChangedEvent);
        Release(focused);
    }

    /// <summary>
    /// On the window's thread: a screen reader is told <paramref name="said"/> about <paramref name="hwnd"/>
    /// — a widget's new size, set by the keys —, a notification it reads out without
    /// the focus moving (UiaRaiseNotificationEvent), the last of a quick run of them only. Only while
    /// a client listens.
    /// </summary>
    public static void Announce(HWND hwnd, string said)
    {
        if (said.Length == 0 || !Entries.ContainsKey((nint)hwnd) || UiaClientsAreListening() == 0)
        {
            return;
        }
        var root = Element.Create(hwnd, 0);
        fixed (char* text = said)
        fixed (char* activity = "Karpo.Announce")
        {
            var display = SysAllocString(text);
            var id = SysAllocString(activity);
            try
            {
                _ = UiaRaiseNotificationEvent(root, NotificationKindOther, NotificationProcessingMostRecent, (char*)display, (char*)id);
            }
            finally
            {
                SysFreeString(display);
                SysFreeString(id);
            }
        }
        Release(root);
    }

    /// <summary>
    /// The element a focus event is raised on: the one with the keyboard, or, where nothing in the
    /// window has it any more (a click on nothing in particular in a flyout), the window's own,
    /// which has it then — so a screen reader hears that the control it was on has lost it.
    /// </summary>
    internal static int FocusTarget(UiaTree tree) => tree.Focused >= 0 ? tree.Focused : tree.Root.Id;

    /// <summary>The tree of <paramref name="hwnd"/> for a provider, on any thread: asked of the window when it is older than <see cref="FreshMs"/>.</summary>
    private static UiaTree? TreeOf(HWND hwnd)
    {
        if (!Entries.TryGetValue((nint)hwnd, out var entry))
        {
            return null;
        }
        if (entry.Tree is null || Environment.TickCount64 - Volatile.Read(ref entry.BuiltAt) >= FreshMs)
        {
            nuint result;
            _ = SendMessageTimeoutW(hwnd, WM_KARPO_UIA_TREE, 0, 0, SMTO_ABORTIFHUNG, 250, &result);
        }
        return entry.Tree;
    }

    private static void Post(Element* self, UiaAct act, double value = 0) =>
        _ = PostMessageW(self->Hwnd, WM_KARPO_UIA_ACT, (WPARAM)(((nuint)(uint)act << 24) | (uint)self->Id), (LPARAM)(nint)(int)Math.Round(value * 1000));

    // --- The provider ------------------------------------------------------------------------------

    // The interfaces' ids (UIAutomationCore.idl).
    private static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid ISimple = new("d6dd68d1-86fd-4332-8666-9abedea2d24c");
    private static readonly Guid ISimple2 = new("a0a839a9-8da1-4a82-806a-8e0d44e79f56");
    private static readonly Guid IFragment = new("f7063da8-8359-439c-9297-bbc5299a7d87");
    private static readonly Guid IFragmentRoot = new("620ce2a5-ab8f-40a9-86cb-de3c75599b58");
    private static readonly Guid IInvoke = new("54fcb24b-e18e-47a2-b4d3-eccbe77599a2");
    private static readonly Guid IToggle = new("56d00bd0-c4f4-433c-a836-1a52a57e0892");
    private static readonly Guid IRange = new("36dc7aef-33e6-4691-afe1-2be7274b3d33");
    private static readonly Guid IExpand = new("d847d3a5-cab0-4a98-8c32-ecb45c59ad24");
    private static readonly Guid ISelectItem = new("2acad808-b2d4-452d-a407-91ff1ad167b2");
    private static readonly Guid IValue = new("c7935180-6fb3-4201-b174-7df73adbf64a");

    // The patterns' and the properties' ids (UIAutomationClient.h).
    private const int InvokePattern = 10000;
    private const int RangeValuePattern = 10003;
    private const int TogglePattern = 10015;
    private const int ExpandCollapsePattern = 10005;
    private const int SelectionItemPattern = 10010;
    private const int ValuePattern = 10002;

    private const int ProcessIdProperty = 30002;
    private const int ControlTypeProperty = 30003;
    private const int NameProperty = 30005;
    private const int HasKeyboardFocusProperty = 30008;
    private const int IsKeyboardFocusableProperty = 30009;
    private const int IsEnabledProperty = 30010;
    private const int AutomationIdProperty = 30011;
    private const int HelpTextProperty = 30013;
    private const int IsControlElementProperty = 30016;
    private const int IsContentElementProperty = 30017;
    private const int IsPasswordProperty = 30019;
    private const int FrameworkIdProperty = 30024;
    private const int ItemStatusProperty = 30026;
    private const int HeadingLevelProperty = 30173;

    /// <summary>HeadingLevel_None; HeadingLevel1 to 9 the ones after it.</summary>
    private const int HeadingLevelNone = 80050;

    // Which pointer inside the object each interface is.
    private const int SimpleSlot = 0;
    private const int FragmentSlot = 1;
    private const int RootSlot = 2;
    private const int InvokeSlot = 3;
    private const int ToggleSlot = 4;
    private const int RangeSlot = 5;
    private const int ExpandSlot = 6;
    private const int SelectSlot = 7;
    private const int ValueSlot = 8;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Element
    {
        public void** Simple;
        public void** Fragment;
        public void** Root;
        public void** Invoke;
        public void** Toggle;
        public void** Range;
        public void** Expand;
        public void** Select;
        public void** Value;
        public int References;
        public int Id;
        public HWND Hwnd;

        /// <summary>A new provider for element <paramref name="id"/> of <paramref name="hwnd"/> (0: the root), holding one reference.</summary>
        public static Element* Create(HWND hwnd, int id)
        {
            var element = (Element*)NativeMemory.AllocZeroed((nuint)sizeof(Element));
            element->Simple = SimpleTable;
            element->Fragment = FragmentTable;
            element->Root = RootTable;
            element->Invoke = InvokeTable;
            element->Toggle = ToggleTable;
            element->Range = RangeTable;
            element->Expand = ExpandTable;
            element->Select = SelectTable;
            element->Value = ValueTable;
            element->References = 1;
            element->Id = id;
            element->Hwnd = hwnd;
            return element;
        }
    }

    private static readonly void** SimpleTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int*, int>)&ProviderOptions,
        (delegate* unmanaged<void*, int, void**, int>)&GetPatternProvider,
        (delegate* unmanaged<void*, int, void*, int>)&GetPropertyValue,
        (delegate* unmanaged<void*, void**, int>)&HostProvider,
        (delegate* unmanaged<void*, int>)&ShowContextMenu);

    private static readonly void** FragmentTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int, void**, int>)&Navigate,
        (delegate* unmanaged<void*, SAFEARRAY**, int>)&GetRuntimeId,
        (delegate* unmanaged<void*, double*, int>)&BoundingRectangle,
        (delegate* unmanaged<void*, SAFEARRAY**, int>)&EmbeddedRoots,
        (delegate* unmanaged<void*, int>)&SetFocus,
        (delegate* unmanaged<void*, void**, int>)&FragmentRoot);

    private static readonly void** RootTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, double, double, void**, int>)&FromPoint,
        (delegate* unmanaged<void*, void**, int>)&GetFocus);

    private static readonly void** InvokeTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int>)&Invoke);

    private static readonly void** ToggleTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int>)&Toggle,
        (delegate* unmanaged<void*, int*, int>)&ToggleState);

    private static readonly void** RangeTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, double, int>)&SetValue,
        (delegate* unmanaged<void*, double*, int>)&RangeValue,
        (delegate* unmanaged<void*, int*, int>)&RangeReadOnly,
        (delegate* unmanaged<void*, double*, int>)&RangeMaximum,
        (delegate* unmanaged<void*, double*, int>)&RangeMinimum,
        (delegate* unmanaged<void*, double*, int>)&RangeLarge,
        (delegate* unmanaged<void*, double*, int>)&RangeSmall);

    // §520: a dropdown, an expander, the settings panel itself: it expands and collapses.
    private static readonly void** ExpandTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int>)&Expand,
        (delegate* unmanaged<void*, int>)&Collapse,
        (delegate* unmanaged<void*, int*, int>)&ExpandState);

    // §520: a side item of the settings panel, a segment: selected among its list's.
    private static readonly void** SelectTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, int>)&SelectItem,
        (delegate* unmanaged<void*, int>)&SelectItem,
        (delegate* unmanaged<void*, int>)&RemoveFromSelection,
        (delegate* unmanaged<void*, int*, int>)&IsSelected,
        (delegate* unmanaged<void*, void**, int>)&SelectionContainer);

    // §523: words that are its value: a line of text, a colour's hex box.
    private static readonly void** ValueTable = Table(
        (delegate* unmanaged<void*, Guid*, void**, int>)&QueryInterface,
        (delegate* unmanaged<void*, uint>)&AddRef,
        (delegate* unmanaged<void*, uint>)&ReleaseThis,
        (delegate* unmanaged<void*, char*, int>)&SetText,
        (delegate* unmanaged<void*, char**, int>)&GetText,
        (delegate* unmanaged<void*, int*, int>)&TextReadOnly);

    private static void** Table(params void*[] entries)
    {
        var table = (void**)NativeMemory.Alloc((nuint)(entries.Length * sizeof(void*)));
        for (var index = 0; index < entries.Length; index++)
        {
            table[index] = entries[index];
        }
        return table;
    }

    /// <summary>The object an interface pointer of it points into: each interface's methods know their slot.</summary>
    private static Element* Of(void* self)
    {
        // Every slot holds its table, which says which slot it is.
        var table = *(void***)self;
        var slot = table == SimpleTable ? SimpleSlot
            : table == FragmentTable ? FragmentSlot
            : table == RootTable ? RootSlot
            : table == InvokeTable ? InvokeSlot
            : table == ToggleTable ? ToggleSlot
            : table == ExpandTable ? ExpandSlot
            : table == SelectTable ? SelectSlot
            : table == ValueTable ? ValueSlot
            : RangeSlot;
        return (Element*)((void**)self - slot);
    }

    private static void* Slot(Element* element, int slot) => (void**)element + slot;

    /// <summary>The element's node in its window's tree as it stands; null when the window or the element is gone.</summary>
    private static (UiaTree Tree, UiaNode Node)? Node(Element* element) =>
        TreeOf(element->Hwnd) is { } tree && tree.Find(element->Id) is { } node ? (tree, node) : null;

    internal static void Release(Element* element)
    {
        if (Interlocked.Decrement(ref element->References) == 0)
        {
            NativeMemory.Free(element);
        }
    }

    /// <summary>A new provider for <paramref name="node"/>, as the interface at <paramref name="slot"/>.</summary>
    private static void* Made(HWND hwnd, UiaNode? node, int slot) =>
        node is null ? null : Slot(Element.Create(hwnd, node.Id), slot);

    [UnmanagedCallersOnly]
    private static int QueryInterface(void* self, Guid* iid, void** result)
    {
        try
        {
            if (result == null)
            {
                return E_POINTER;
            }
            *result = null;
            var element = Of(self);
            var id = *iid;
            int slot;
            if (id == IUnknown || id == ISimple || id == ISimple2)
            {
                slot = SimpleSlot;
            }
            else if (id == IFragment)
            {
                slot = FragmentSlot;
            }
            else if (id == IFragmentRoot && element->Id == 0)
            {
                slot = RootSlot;
            }
            else if ((id == IInvoke || id == IToggle || id == IRange || id == IExpand || id == ISelectItem || id == IValue) && Node(element) is { } found
                && ((id == IInvoke && found.Node.Pattern == UiaPattern.Invoke)
                    || (id == IToggle && found.Node.Pattern == UiaPattern.Toggle)
                    || (id == IRange && found.Node.Pattern == UiaPattern.Range)
                    || (id == IExpand && found.Node.Pattern == UiaPattern.Expand)
                    || (id == ISelectItem && found.Node.Pattern == UiaPattern.Select)
                    || (id == IValue && found.Node.Pattern == UiaPattern.Value)))
            {
                slot = id == IInvoke ? InvokeSlot : id == IToggle ? ToggleSlot : id == IRange ? RangeSlot : id == IExpand ? ExpandSlot : id == IValue ? ValueSlot : SelectSlot;
            }
            else
            {
                return E_NOINTERFACE;
            }
            Interlocked.Increment(ref element->References);
            *result = Slot(element, slot);
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(void* self) => (uint)Interlocked.Increment(ref Of(self)->References);

    [UnmanagedCallersOnly]
    private static uint ReleaseThis(void* self)
    {
        var element = Of(self);
        var left = Interlocked.Decrement(ref element->References);
        if (left == 0)
        {
            NativeMemory.Free(element);
        }
        return (uint)left;
    }

    [UnmanagedCallersOnly]
    private static int ProviderOptions(void* self, int* options)
    {
        *options = ProviderOptionsServerSide;
        return 0;
    }

    [UnmanagedCallersOnly]
    private static int GetPatternProvider(void* self, int pattern, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            if (Node(element) is not { } found)
            {
                return 0;
            }
            var slot = (pattern, found.Node.Pattern) switch
            {
                (InvokePattern, UiaPattern.Invoke) => InvokeSlot,
                (TogglePattern, UiaPattern.Toggle) => ToggleSlot,
                (RangeValuePattern, UiaPattern.Range) => RangeSlot,
                (ExpandCollapsePattern, UiaPattern.Expand) => ExpandSlot,
                (SelectionItemPattern, UiaPattern.Select) => SelectSlot,
                (ValuePattern, UiaPattern.Value) => ValueSlot,
                _ => -1,
            };
            if (slot >= 0)
            {
                Interlocked.Increment(ref element->References);
                *result = Slot(element, slot);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetPropertyValue(void* self, int property, void* variant)
    {
        try
        {
            Variant.Empty(variant);
            var element = Of(self);
            if (Node(element) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            var (tree, node) = found;
            var root = element->Id == 0;
            switch (property)
            {
                case ControlTypeProperty:
                    Variant.Int(variant, (int)node.Role);
                    break;
                case NameProperty:
                    Variant.Text(variant, node.Name);
                    break;
                case AutomationIdProperty when node.AutomationId.Length > 0:
                    Variant.Text(variant, node.AutomationId);
                    break;
                case HelpTextProperty when node.Help is { Length: > 0 } help:
                    Variant.Text(variant, help);
                    break;
                case ItemStatusProperty when node.Status is { Length: > 0 } status:
                    Variant.Text(variant, status);
                    break;
                case IsEnabledProperty:
                    Variant.Bool(variant, node.Enabled);
                    break;
                case IsKeyboardFocusableProperty when !root:
                    Variant.Bool(variant, node.Focusable);
                    break;
                case HasKeyboardFocusProperty when !root:
                    Variant.Bool(variant, tree.Focused == node.Id);
                    break;
                // §338: a password's field says it is one, and never its words.
                case IsPasswordProperty:
                    Variant.Bool(variant, node.Password);
                    break;
                // §552: a section's head says it is a heading, and of which level.
                case HeadingLevelProperty:
                    Variant.Int(variant, HeadingLevelNone + Math.Clamp(node.Heading, 0, 9));
                    break;
                case IsControlElementProperty:
                case IsContentElementProperty:
                    Variant.Bool(variant, true);
                    break;
                case FrameworkIdProperty:
                    Variant.Text(variant, "Karpo");
                    break;
                case ProcessIdProperty:
                    Variant.Int(variant, (int)GetCurrentProcessId());
                    break;
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int HostProvider(void* self, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            // The root stands for the window, which UI Automation's own provider for it describes too.
            return element->Id == 0 ? UiaHostProviderFromHwnd(element->Hwnd, result) : 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int ShowContextMenu(void* self)
    {
        try
        {
            var element = Of(self);
            if (element->Id != 0)
            {
                Post(element, UiaAct.Menu);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int Navigate(void* self, int direction, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            if (Node(element) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            var (tree, node) = found;
            var next = direction switch
            {
                // The root's parent and siblings are the window's, which UI Automation knows.
                0 => element->Id == 0 ? null : tree.Parent(node.Id),
                1 => element->Id == 0 ? null : tree.Sibling(node.Id, 1),
                2 => element->Id == 0 ? null : tree.Sibling(node.Id, -1),
                3 => node.Children.Count > 0 ? node.Children[0] : null,
                4 => node.Children.Count > 0 ? node.Children[^1] : null,
                _ => null,
            };
            *result = next is null ? null : Made(element->Hwnd, next, next.Id == 0 ? RootSlot : FragmentSlot);
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetRuntimeId(void* self, SAFEARRAY** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            // A root hosted in a window is known by the window.
            if (element->Id == 0)
            {
                return 0;
            }
            var ids = SafeArrayCreateVector((ushort)VARENUM.VT_I4, 0, 2);
            if (ids == null)
            {
                return unchecked((int)0x8007000E);
            }
            int at = 0;
            int value = UiaAppendRuntimeId;
            _ = SafeArrayPutElement(ids, &at, &value);
            at = 1;
            value = element->Id;
            _ = SafeArrayPutElement(ids, &at, &value);
            *result = ids;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int BoundingRectangle(void* self, double* rect)
    {
        try
        {
            rect[0] = rect[1] = rect[2] = rect[3] = 0;
            var element = Of(self);
            var found = Node(element);
            // The root's rectangle is its window's, which UI Automation reads there — unless the tree
            // says where it is: a popup's window spans its display (§355), and the root is its panel.
            if (element->Id == 0 && (found is not { } root || root.Node.Bounds.right <= root.Node.Bounds.left || root.Node.Bounds.bottom <= root.Node.Bounds.top))
            {
                return 0;
            }
            if (found is null)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            var bounds = found.Value.Node.Bounds;
            rect[0] = bounds.left;
            rect[1] = bounds.top;
            rect[2] = bounds.right - bounds.left;
            rect[3] = bounds.bottom - bounds.top;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int EmbeddedRoots(void* self, SAFEARRAY** result)
    {
        *result = null;
        return 0;
    }

    [UnmanagedCallersOnly]
    private static int SetFocus(void* self)
    {
        try
        {
            var element = Of(self);
            if (Node(element) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            if (found.Node.Focusable)
            {
                Post(element, UiaAct.Focus);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int FragmentRoot(void* self, void** result)
    {
        try
        {
            var element = Of(self);
            *result = Slot(Element.Create(element->Hwnd, 0), RootSlot);
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int FromPoint(void* self, double x, double y, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            if (TreeOf(element->Hwnd)?.At((int)Math.Floor(x), (int)Math.Floor(y)) is { } under)
            {
                *result = Made(element->Hwnd, under, FragmentSlot);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetFocus(void* self, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            if (TreeOf(element->Hwnd) is { Focused: >= 0 } tree)
            {
                *result = Made(element->Hwnd, tree.Find(tree.Focused), FragmentSlot);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int Invoke(void* self)
    {
        try
        {
            return Acted(Of(self), UiaAct.Invoke);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int Toggle(void* self)
    {
        try
        {
            return Acted(Of(self), UiaAct.Toggle);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>§520: expanded where it is shut (its click opens it); nothing where it stands open already.</summary>
    [UnmanagedCallersOnly]
    private static int Expand(void* self)
    {
        try
        {
            var element = Of(self);
            return Node(element) is { Node.On: true } ? 0 : Acted(element, UiaAct.Invoke);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>§520: collapsed where it stands open (its click shuts it); nothing where it is shut already.</summary>
    [UnmanagedCallersOnly]
    private static int Collapse(void* self)
    {
        try
        {
            var element = Of(self);
            return Node(element) is { Node.On: true } ? Acted(element, UiaAct.Invoke) : 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int ExpandState(void* self, int* state)
    {
        try
        {
            *state = 0;
            if (Node(Of(self)) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            // ExpandCollapseState: Collapsed 0, Expanded 1.
            *state = found.Node.On == true ? 1 : 0;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>§520: selected (its click selects it): a side item's page shown, a segment's choice made.</summary>
    [UnmanagedCallersOnly]
    private static int SelectItem(void* self)
    {
        try
        {
            var element = Of(self);
            return Node(element) is { Node.On: true } ? 0 : Acted(element, UiaAct.Invoke);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>One of a list that always has one selected: it cannot be taken out of the selection.</summary>
    [UnmanagedCallersOnly]
    private static int RemoveFromSelection(void* self) => unchecked((int)0x80131509);

    [UnmanagedCallersOnly]
    private static int IsSelected(void* self, int* selected)
    {
        try
        {
            *selected = 0;
            if (Node(Of(self)) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            *selected = found.Node.On == true ? 1 : 0;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>The list it is one of: its parent in the tree.</summary>
    [UnmanagedCallersOnly]
    private static int SelectionContainer(void* self, void** result)
    {
        try
        {
            *result = null;
            var element = Of(self);
            if (Node(element) is { } found && found.Tree.Parent(element->Id) is { } parent)
            {
                *result = Made(element->Hwnd, parent, SimpleSlot);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int ToggleState(void* self, int* state)
    {
        try
        {
            *state = 2;
            if (Node(Of(self)) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            *state = found.Node.On switch { true => 1, false => 0, null => 2 };
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int SetValue(void* self, double value)
    {
        try
        {
            var element = Of(self);
            if (Node(element) is not { Node.Range: { } range } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            if (!found.Node.Enabled)
            {
                return UIA_E_ELEMENTNOTENABLED;
            }
            if (value < range.Minimum || value > range.Maximum)
            {
                return unchecked((int)0x80070057);
            }
            Post(element, UiaAct.SetValue, value);
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>§523: words set as the element's value: handed to its window, which takes them (<see cref="TakeText"/>).</summary>
    [UnmanagedCallersOnly]
    private static int SetText(void* self, char* text)
    {
        try
        {
            var element = Of(self);
            if (Node(element) is not { Node.Pattern: UiaPattern.Value } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            if (!found.Node.Enabled)
            {
                return UIA_E_ELEMENTNOTENABLED;
            }
            Texts[((nint)element->Hwnd, element->Id)] = text == null ? "" : new string(text);
            Post(element, UiaAct.SetText);
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    /// <summary>§523: the element's words, a string of UI Automation's to free.</summary>
    [UnmanagedCallersOnly]
    private static int GetText(void* self, char** value)
    {
        try
        {
            *value = null;
            if (Node(Of(self)) is not { } found)
            {
                return UIA_E_ELEMENTNOTAVAILABLE;
            }
            fixed (char* text = found.Node.Value ?? "")
            {
                *value = (char*)SysAllocString(text);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int TextReadOnly(void* self, int* readOnly)
    {
        try
        {
            *readOnly = Node(Of(self)) is { } found && found.Node.Enabled ? 0 : 1;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeValue(void* self, double* value)
    {
        try
        {
            return Range(Of(self), value, range => range.Value);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeMaximum(void* self, double* value)
    {
        try
        {
            return Range(Of(self), value, range => range.Maximum);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeMinimum(void* self, double* value)
    {
        try
        {
            return Range(Of(self), value, range => range.Minimum);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeLarge(void* self, double* value)
    {
        try
        {
            return Range(Of(self), value, range => range.LargeChange);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeSmall(void* self, double* value)
    {
        try
        {
            return Range(Of(self), value, range => range.SmallChange);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    [UnmanagedCallersOnly]
    private static int RangeReadOnly(void* self, int* readOnly)
    {
        try
        {
            *readOnly = Node(Of(self)) is { } found && found.Node.Enabled ? 0 : 1;
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Accessibility: answering UI Automation", error);
            return E_FAIL;
        }
    }

    private static int Range(Element* element, double* value, Func<UiaRange, double> read)
    {
        *value = 0;
        if (Node(element) is not { Node.Range: { } range })
        {
            return UIA_E_ELEMENTNOTAVAILABLE;
        }
        *value = read(range);
        return 0;
    }

    private static int Acted(Element* element, UiaAct act)
    {
        if (Node(element) is not { } found)
        {
            return UIA_E_ELEMENTNOTAVAILABLE;
        }
        if (!found.Node.Enabled)
        {
            return UIA_E_ELEMENTNOTENABLED;
        }
        Post(element, act);
        return 0;
    }

    /// <summary>A VARIANT, written by its layout: the type in its first two bytes, the value from its eighth.</summary>
    private static class Variant
    {
        private const ushort Nothing = 0;
        private const ushort I4 = 3;
        private const ushort Bstr = 8;
        private const ushort Boolean = 11;

        public static void Empty(void* variant)
        {
            new Span<byte>(variant, 24).Clear();
            *(ushort*)variant = Nothing;
        }

        public static void Int(void* variant, int value)
        {
            *(ushort*)variant = I4;
            *(int*)((byte*)variant + 8) = value;
        }

        public static void Bool(void* variant, bool value)
        {
            *(ushort*)variant = Boolean;
            *(short*)((byte*)variant + 8) = value ? (short)-1 : (short)0;
        }

        public static void Text(void* variant, string value)
        {
            fixed (char* text = value)
            {
                *(ushort*)variant = Bstr;
                *(char**)((byte*)variant + 8) = (char*)SysAllocString(text);
            }
        }
    }

    // --- UIAutomationCore.dll ------------------------------------------------------------------------

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern nint UiaReturnRawElementProvider(HWND hwnd, nuint wParam, nint lParam, void* element);

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern int UiaHostProviderFromHwnd(HWND hwnd, void** provider);

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern int UiaRaiseAutomationEvent(void* provider, int id);

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern int UiaClientsAreListening();

    [DllImport("UIAutomationCore.dll", ExactSpelling = true)]
    private static extern int UiaRaiseNotificationEvent(void* provider, int kind, int processing, char* display, char* activity);

    // --- For the tests: the provider as a client sees it, through its own tables ------------------------

    /// <summary>A provider for element <paramref name="id"/> of <paramref name="hwnd"/>, as IRawElementProviderSimple; released with <see cref="Unref"/>.</summary>
    internal static void* Provider(HWND hwnd, int id) => Slot(Element.Create(hwnd, id), SimpleSlot);

    /// <summary>Gives up a reference a test holds on an interface pointer of a provider.</summary>
    internal static void Unref(void* unknown) => ((delegate* unmanaged<void*, uint>)(*(void***)unknown)[2])(unknown);

    /// <summary>The tree a registered window's providers read now, built on this thread (a test's window is its own).</summary>
    internal static UiaTree? Current(HWND hwnd)
    {
        Rebuild(hwnd, now: true);
        return Entries.TryGetValue((nint)hwnd, out var entry) ? entry.Tree : null;
    }
}
