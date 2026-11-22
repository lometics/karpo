// The input indicator: the language and keyboard layout of the window in use,
// "ENG US". It was a taskbar entry whose click opened the layouts in a flyout;
// since 2026-10-11 ("all the remaining flyouts should become folders with a face
// now") it is the keyboard face of the keyboard preset's folder, which holds one
// of these as its own (FolderIsland.Keyboard) and paints it (PaintFace), and the
// layouts are the keyboard's line in the folder (KeyboardPage).
//
// A layout belongs to a thread, and the change goes to that thread alone, so
// no message tells another process what it is now. The island never polls
// for it: it reads it again when the dock hears that it may have
// changed. The shell hook says when another window came to the front and,
// with HSHELL_LANGUAGE, when a layout changed; WM_INPUTLANGCHANGE reaches a
// window of Karpo's own when Karpo's thread switched while it had the keys.
// With one input method for every window (Windows' default), a window that
// comes to the front takes the current one only once it is there, so the dock
// reads once more a moment after an activation. Each read is a
// GetKeyboardLayout, and the cell is drawn again only when the layout changed.
//
// The language over the region as Windows' own indicator sets them ("ENG" over
// "CH"), at the person's asking (2026-10-08): the one taskbar entry besides now
// playing that is two lines (docs\widgets.md §12.5). Two lines where the cell has
// the height for them; where it has not — a thin bar — the language alone, as
// Windows' indicator is on a taskbar of small buttons. The codes are 11 DIP, under the clock and
// a step under Windows' own indicator's 12 (2026-10-11, twice), never shrunk to fit a square (2026-10-09): the entry is square where
// they fit one and wider where they do not. Measured for the widest code this PC's
// layouts show, never the codes showing, so the cell never changes as the layout
// does. Down a vertical bar the region stands under the language.
//
// Placed, it always shows, with one keyboard layout installed as with several:
// "keyboard layout widget does not show when i placed it on the taskbar"
// (2026-10-08). Windows' own indicator shows only where there is something to
// switch between; a new bar has the face whatever the count (2026-10-10,
// Sections.DefaultLayout).
//
// It also keeps the window the person types in (Front): the last window in front
// that is not Karpo's, which the layouts' line switches, since by the time a row of
// it is clicked the folder has the keys.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class KeyboardIsland : CellIsland
{
    // winnls.h, which TerraFX leaves out.
    private const uint LOCALE_SLOCALIZEDDISPLAYNAME = 0x00000002;
    private const uint LOCALE_SISO3166CTRYNAME = 0x0000005A;
    private const uint LOCALE_SISO639LANGNAME2 = 0x00000067;

    private const string LayoutsKey = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts\"; // not-ui: registry key

    private HKL current;
    private (string Language, string Region) label = ("ENG", "");

    /// <summary>
    /// The last window in front that was not Karpo's: the one the person types in, which a pick in
    /// the layouts' line switches (KeyboardPage). Written on the dock's thread whenever an indicator
    /// reads the layout in use, and by a folder opening over it (<see cref="FlyoutHost"/>).
    /// </summary>
    internal static HWND Front { get; set; }

    public KeyboardIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Sample(true);
    }

    /// <summary>Never ticks: the dock says when the layout may have changed (<see cref="Follow"/>, <see cref="Switched"/>).</summary>
    public override int IntervalMs => 0;

    /// <summary>One cell, however many layouts there are: placed, the indicator always shows.</summary>
    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
    }

    /// <summary>
    /// The language and region the cell shows, for the tests. Switching layout is exactly the
    /// state change the size must not follow, and there is no other way to make one happen
    /// without touching the person's own keyboard.
    /// </summary>
    internal void Show((string Language, string Region) shown)
    {
        label = shown;
        Redraw(Gfx);
    }

    /// <summary>
    /// Another window may be in front, or its layout may have changed (the shell hook): the
    /// layout in use is read again, and only a changed one is drawn, once the dock refreshes the island.
    /// </summary>
    public void Follow()
    {
        if (Read(own: null))
        {
            Post();
        }
    }

    /// <summary>
    /// Karpo's own thread switched to <paramref name="layout"/> (WM_INPUTLANGCHANGE to a window
    /// of Karpo's): the layout typed with while a window of Karpo's has the keys. With another
    /// window in front, that window's is read again instead.
    /// </summary>
    public void Switched(HKL layout)
    {
        if (Read(own: layout))
        {
            Post();
        }
    }

    protected override bool Sample(bool everything)
    {
        if (!Read(own: null) && everything)
        {
            label = Label(current);
        }
        // The one cell is always there.
        return false;
    }

    /// <summary>The layout in use, read again (see <see cref="Pick"/>); true when it changed what the cell shows.</summary>
    private bool Read(HKL? own)
    {
        var front = InFront(out var ours);
        var next = Pick(current, front, ours, own);
        if (next == HKL.NULL)
        {
            // Nothing to go by at the start: this thread's, the default input language.
            next = GetKeyboardLayout(0);
        }
        if (next == current)
        {
            return false;
        }
        current = next;
        label = Label(next);
        return true;
    }

    /// <summary>
    /// The layout to show: <paramref name="front"/>, the one of the window in front. While that
    /// window is Karpo's own (<paramref name="ours"/>: a flyout, the bar being edited), the one
    /// <paramref name="shown"/> stays, since Karpo's thread keeps a layout of its own rather than
    /// the one the person types with; unless Karpo's thread switched to <paramref name="own"/>
    /// while it had the keys, which is then the one in use. A window whose layout can't be read
    /// (null) changes nothing either. Null when there is nothing to go by.
    /// </summary>
    internal static HKL Pick(HKL shown, HKL front, bool ours, HKL? own)
    {
        if (!ours && front != HKL.NULL)
        {
            return front;
        }
        if (ours && own is { } switched && switched != HKL.NULL)
        {
            return switched;
        }
        return shown != HKL.NULL ? shown : front;
    }

    /// <summary>
    /// The layout of the thread with the keys in the window in front, and whether that window is
    /// Karpo's. No window in front counts as Karpo's: the layout shown stays. A window in front that
    /// is not Karpo's is the one the person types in from now on (<see cref="Front"/>).
    /// </summary>
    private static HKL InFront(out bool ours)
    {
        var window = GetForegroundWindow();
        uint process = 0;
        var thread = window == HWND.NULL ? 0 : GetWindowThreadProcessId(window, &process);
        if (thread == 0)
        {
            ours = true;
            return HKL.NULL;
        }
        ours = process == GetCurrentProcessId();
        if (!ours)
        {
            Front = window;
        }
        return LayoutOf(window);
    }

    /// <summary>
    /// The layout of the thread with the keys in <paramref name="window"/>. A Store app's frame
    /// belongs to ApplicationFrameHost while its content, which has the focus and the layout, is the
    /// app's own.
    /// </summary>
    internal static HKL LayoutOf(HWND window)
    {
        var thread = GetWindowThreadProcessId(window, null);
        if (thread == 0)
        {
            return HKL.NULL;
        }
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        if (GetGUIThreadInfo(thread, &info) && info.hwndFocus != HWND.NULL)
        {
            var focused = GetWindowThreadProcessId(info.hwndFocus, null);
            if (focused != 0)
            {
                thread = focused;
            }
        }
        return GetKeyboardLayout(thread);
    }

    /// <summary>The language, and the layout's keyboard under it: the face's hover, its name to a screen reader.</summary>
    public override string? Tooltip(int index)
    {
        var (language, layout) = Names(current);
        return layout.Length > 0 ? $"{language}\n{S.WidgetKeyboardLayoutKeyboard(layout)}" : language;
    }

    /// <summary>Its click is its folder's, which opens the layouts.</summary>
    public override IslandAction Click(int index) => default;

    /// <summary>What the face shows: the codes, which a layout switched paints again.</summary>
    internal string Reading => $"{label.Language}|{label.Region}";

    /// <summary>The codes it shows now, which the keyboard face across a bar is as wide as (docs\widgets.md §244).</summary>
    internal (string Language, string Region) Codes => label;

    /// <summary>Never on a bar of its own: the keyboard face paints it (<see cref="PaintFace"/>).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException("The input indicator is the keyboard face of a folder, not a widget of a bar.");

    /// <summary>
    /// The codes in <paramref name="box"/>, the keyboard face's room (FaceDrawings): the language
    /// over the region where two lines fit the bar, else the language alone, at the bar's text size,
    /// in the middle of the box both ways. Across a bar the box is the room the widest code keeps;
    /// down one it is the bar's width, whose ends are kept clear. Each line stands in the middle of the box.
    /// </summary>
    internal void PaintFace(ID2D1DeviceContext* dc, in IslandStyle style, D2D_RECT_F box)
    {
        var ink = new IslandInk(style.Look);
        var (widest, px, twoLines) = Plan(style);
        // A layout with no region has nothing to put under its language: the language alone, in
        // the middle, in the room kept for both.
        twoLines &= label.Region.Length > 0;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine top = default;
        TextLine bottom = default;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            // The room the size was chosen for: down a bar on its side, the bar's width less its
            // ends, since laying out to the whole of it lets a code reach its edge.
            var fit = new Fit(style);
            var room = MathF.Max(1, style.Vertical ? VerticalFit.Room(fit) : MathF.Max(width, widest));
            if (twoLines)
            {
                // The region under the language, as one block in the middle.
                top = TextLine.Lay(Gfx, label.Language, px, Weight.Regular, room);
                bottom = TextLine.Lay(Gfx, label.Region, px, Weight.Regular, room);
                var y = box.top + TextLine.Round((height - top.Height - bottom.Height) / 2);
                top.Draw(dc, brush, ink.Text, box.left + ((width - top.Width) / 2), y);
                bottom.Draw(dc, brush, ink.Text, box.left + ((width - bottom.Width) / 2), y + top.Height);
            }
            else
            {
                // One line: the language — for a layout with no region, or across a bar whose
                // cell has no height for two lines.
                top = TextLine.Lay(Gfx, label.Language, px, Weight.Regular, room);
                top.Draw(dc, brush, ink.Text, box.left + ((width - top.Width) / 2), box.top + ((height - top.Height) / 2));
            }
        }
        finally
        {
            bottom.Release();
            top.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The text's width, size and whether the region goes under the language (<see cref="Room"/>).</summary>
    private (float Width, float Px, bool TwoLines) Plan(in IslandStyle style) => Room(Gfx, style, label.Region.Length > 0);

    /// <summary>
    /// The room the codes keep, the size they are drawn at and whether the region goes under the
    /// language. Never measured on the codes showing now: a widget must not change size as its
    /// state changes, and switching from ENG to DEU would otherwise move the bar.
    /// <para>
    /// Across a horizontal bar the codes are Windows' indicator's size, a step under the clock's
    /// (<see cref="CodePx"/>, 2026-10-11), and never shrunk to fit: the person (2026-10-09) would rather the
    /// entry were wider than square than its text shrunk to fit a square, as it was on a thin bar.
    /// The width is the widest of <paramref name="codes"/> — this PC's installed layouts'
    /// (<see cref="Installed"/>) unless a test says otherwise — so it holds every layout one can
    /// switch to; the entry is that and its ends (<see cref="Fit.Edge"/>), never narrower than
    /// square (WidgetShapes.Keyboard). The region goes under the language where two lines of that
    /// size fit the cell's height with a sliver of air above and below (<see cref="TwoLinesFit"/>);
    /// else the language alone. Neither asks what the layout in use is.
    /// </para>
    /// <para>
    /// Across a vertical bar the bar's width is all the room a line has, and a code that does not
    /// fit it is a <b>size</b> problem rather than a length one — three letters are three letters —
    /// so the size steps down from the codes' own (docs\widgets.md §12.4, branch 2), measured on the
    /// same installed codes as across a bar rather than on three of the widest letter, which set
    /// "ENG" at 8 px on a thin side bar (2026-10-13), and the region goes under the language.
    /// </para>
    /// </summary>
    internal static (float Width, float Px, bool TwoLines) Room(Gfx gfx, in IslandStyle style, bool withRegion,
        IReadOnlyList<(string Language, string Region)>? codes = null)
    {
        var fit = new Fit(style);
        codes ??= Installed();
        if (style.Vertical)
        {
            // Largest first: the codes' own size where they fit across, else the steps under it.
            var own = CodePx(fit);
            var room = VerticalFit.Room(fit);
            float[] sizes = [own, .. VerticalFit.Ladder(fit).Where(size => size < own)];
            foreach (var size in sizes)
            {
                var (across, _) = Widths(gfx, codes, size, regions: withRegion);
                if (across <= room || size == sizes[^1])
                {
                    return (MathF.Min(across, room), size, withRegion);
                }
            }
        }
        var px = CodePx(fit);
        var (language, line) = Widths(gfx, codes, px, regions: false);
        return TwoLinesFit(fit, line) ? (Widths(gfx, codes, px, regions: true).Width, px, true) : (language, px, false);
    }

    /// <summary>
    /// The codes' size across a horizontal bar: 11 DIP of Segoe UI Variable on the 48-DIP bar, under
    /// the clock's 12.5 (<see cref="Fit.Primary"/>), and in the same proportion to it on any other
    /// bar and at any text size, never under 10 DIP; whole pixels, so the two short lines stay crisp.
    /// It was Windows' own indicator's 12 (2026-10-11: "make the keyboard layout switcher in the
    /// taskbar smaller ... just a little smaller"), then smaller again the same day: "can you also
    /// make the ENG CH text on the keyboard face smaller."
    /// </summary>
    internal static float CodePx(in Fit fit) =>
        MathF.Round(MathF.Max(10 * fit.Scale * fit.TextScale, fit.Primary * (CodeDips / 12.5f)));

    /// <summary>The codes' size on the 48-DIP bar at 100 % text, in DIP.</summary>
    internal const float CodeDips = 11;

    /// <summary>
    /// The widest of <paramref name="codes"/>' languages, and their regions where asked, at
    /// <paramref name="px"/>, and a line's height.
    /// </summary>
    private static (float Width, float Height) Widths(Gfx gfx, IReadOnlyList<(string Language, string Region)> codes, float px, bool regions)
    {
        // Nothing installed to go by: three of the widest letter, which every code fits.
        var (width, height) = codes.Count == 0 ? TextLine.Template(gfx, Widest, px, Weight.Regular) : (0f, 0f);
        foreach (var (language, region) in codes)
        {
            foreach (var code in regions ? [language, region] : (ReadOnlySpan<string>)[language])
            {
                if (code.Length > 0)
                {
                    var size = TextLine.Template(gfx, code, px, Weight.Regular);
                    (width, height) = (MathF.Max(width, size.Width), MathF.Max(height, size.Height));
                }
            }
        }
        return (width, height);
    }

    /// <summary>
    /// Whether two lines <paramref name="line"/> px tall stand in the cell with a sliver of air
    /// above and below them: the rule the taskbar's clock went by for its time over its date (which,
    /// with the now-playing face, steps down the text's sizes now: <see cref="DateTimeFaceDrawing.TwoLines"/>, §497).
    /// </summary>
    internal static bool TwoLinesFit(in Fit fit, float line) => (2 * line) + (2 * MathF.Max(1, fit.Px(2))) <= fit.Cell;

    /// <summary>The widest a language or region code ever draws: three of the widest letter.</summary>
    internal const string Widest = "WWW";

    private static readonly Lock Installing = new();
    private static HKL[] seen = [];
    private static (string Language, string Region)[] installed = [];

    /// <summary>
    /// The codes of every layout installed on this PC, which the cell keeps room for: the layouts
    /// one can switch to are what it can show. Read again only when the list changes, which is
    /// when somebody adds or removes a language in Windows' settings.
    /// </summary>
    internal static IReadOnlyList<(string Language, string Region)> Installed()
    {
        var list = new HKL[Math.Max(0, GetKeyboardLayoutList(0, null))];
        int count;
        fixed (HKL* values = list)
        {
            count = GetKeyboardLayoutList(list.Length, values);
        }
        Array.Resize(ref list, Math.Max(0, count));
        lock (Installing)
        {
            if (!list.AsSpan().SequenceEqual(seen))
            {
                installed = [.. list.Select(Label).Distinct()];
                seen = list;
            }
            return installed;
        }
    }

    /// <summary>"ENG" and "US": the language's ISO 639-2 code and the layout's country, as the indicator shows them.</summary>
    internal static (string Language, string Region) Label(HKL layout)
    {
        var language = (uint)((nuint)layout & 0xFFFF);
        var device = (uint)(((nuint)layout >> 16) & 0xFFFF);
        var code = LocaleInfo(language, LOCALE_SISO639LANGNAME2)?.ToUpperInvariant() ?? "ENG";
        // The high word is the layout's own language for a plain layout (Swiss German under
        // English: 0807); a variant's (0xFxxx) is a handle, and the language's country stands in.
        var country = LocaleInfo((device & 0xF000) == 0xF000 || device == 0 ? language : device, LOCALE_SISO3166CTRYNAME) ?? "";
        return (code, country.ToUpperInvariant());
    }

    /// <summary>The language's name in the user's language, and the layout's name ("Swiss German").</summary>
    internal static (string Language, string Layout) Names(HKL layout)
    {
        var language = (uint)((nuint)layout & 0xFFFF);
        var device = (uint)(((nuint)layout >> 16) & 0xFFFF);
        var name = LocaleInfo(language, LOCALE_SLOCALIZEDDISPLAYNAME) ?? S.WidgetKeyboardKeyboard;
        var id = (device & 0xF000) == 0xF000 || device == 0 ? language : device;
        return (name, LayoutName($"{id:X8}") ?? "");
    }

    /// <summary>A layout's display name from the registry, in the user's language where Windows has it.</summary>
    private static string? LayoutName(string klid)
    {
        HKEY key;
        fixed (char* path = LayoutsKey + klid)
        {
            if (RegOpenKeyExW(HKEY.HKEY_LOCAL_MACHINE, path, 0, (uint)KEY.KEY_READ, &key) != 0)
            {
                return null;
            }
        }
        try
        {
            var text = stackalloc char[256];
            uint size = 0;
            fixed (char* value = "Layout Display Name") // not-ui: registry value name
            {
                if (RegLoadMUIStringW(key, value, text, 255 * sizeof(char), &size, 0, null) == 0 && text[0] != '\0')
                {
                    return new string(text);
                }
            }
            uint bytes = 255 * sizeof(char);
            uint type = 0;
            fixed (char* value = "Layout Text") // not-ui: registry value name
            {
                if (RegQueryValueExW(key, value, null, &type, (byte*)text, &bytes) == 0 && bytes >= sizeof(char))
                {
                    return new string(text, 0, (int)(bytes / sizeof(char))).TrimEnd('\0');
                }
            }
            return null;
        }
        finally
        {
            _ = RegCloseKey(key);
        }
    }

    private static string? LocaleInfo(uint locale, uint type)
    {
        var buffer = stackalloc char[128];
        var length = GetLocaleInfoW(locale, type, buffer, 128);
        return length > 1 ? new string(buffer, 0, length - 1) : null;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetLocaleInfoW(uint locale, uint type, char* data, int count);
}
