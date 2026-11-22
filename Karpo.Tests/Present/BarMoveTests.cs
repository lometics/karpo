// §534: a bar dragged by its empty room to another edge of its display, on the real dock (Karpo.exe --stage on a
// desktop of its own, LevelAfterCommitTests.OnTheDock), driven as the mouse drives it: the pointer on the room between
// the bar's start and its end, pressed, carried up the display, let go. Unlocked, the bar stands at the top from then
// on, the config saying so where the Display page's Position writes it; locked (as a new config is), it stays at the
// bottom; Escape while it is carried leaves it at the bottom, the strip it showed where it would land gone.
//
// §663 (the person, 2026-11-10: "locking the taskbar should be shown in the default right click context menu again.
// when the taskbar is locked dragging a widget should not go into edit mode anymore"): on a locked bar (a new config's)
// a folder's face pressed and carried along the bar is no drag — no edit mode, the face where it stood; the bar's right
// click, the default menu, has "Lock the taskbar", which chosen writes the bar unlocked; and then the same drag is edit
// mode's, as it always was.

using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed unsafe class BarMoveTests
{
    /// <summary>The bars' window class (Dock.ClassName).</summary>
    private const string BarClass = "karpo.dock";

    /// <summary>The stage's made-up display (InputReachesTheBarsTests.Display): 2560 by 1440, left of the main one.</summary>
    private static readonly RECT Display = new() { left = -2560, top = 0, right = 0, bottom = 1440 };

    [UiFact]
    public void UnlockedTheBarDraggedUpStandsAtTheTop() => Drive(locked: false, escape: false, Edge.Top);

    [UiFact]
    public void LockedTheBarStaysWhereItIs() => Drive(locked: true, escape: false, Edge.Bottom);

    [UiFact]
    public void EscapeLeavesTheBarWhereItWas() => Drive(locked: false, escape: true, Edge.Bottom);

    /// <summary>
    /// A bar of Start alone at its start and Show desktop at its end, the room between them its own; pressed there, carried
    /// to the display's top and let go (Escape before the release where <paramref name="escape"/>): the edge the file has
    /// then is <paramref name="expected"/>, and the bar stands on it.
    /// </summary>
    private static void Drive(bool locked, bool escape, Edge expected)
    {
        var config = new Config { LockTaskbar = locked };
        config.DefaultLayout.Islands.Clear();
        var start = BarKind.Start.New();
        start.Section = BarSection.Start;
        config.DefaultLayout.Islands.Add(start);
        var desktop = BarKind.ShowDesktop.New();
        desktop.Section = BarSection.End;
        config.DefaultLayout.Islands.Add(desktop);
        Sections.Order(config.DefaultLayout.Islands);
        LevelAfterCommitTests.OnTheDock.Run($"bar-move-{(locked ? "locked" : escape ? "escape" : "unlocked")}", dock =>
        {
            dock.Wait(() => Bar(dock) != HWND.NULL, "no bar came");
            LevelAfterCommitTests.OnTheDock.Pump(1500);
            var bar = Bar(dock);
            RECT rect;
            _ = GetWindowRect(bar, &rect);
            Assert.True(rect.bottom == Display.bottom, $"the bar stands at {rect.left},{rect.top}..{rect.right},{rect.bottom}, not at the bottom");
            // The middle of the display along the bar, a few px in from the screen's edge: the bar's own room.
            var room = new POINT { x = (Display.left + Display.right) / 2, y = Display.bottom - 12 };
            var top = new POINT { x = room.x, y = Display.top + 60 };
            Post(bar, WM.WM_MOUSEMOVE, room);
            Post(bar, WM.WM_LBUTTONDOWN, room);
            Post(bar, WM.WM_MOUSEMOVE, room with { y = room.y - 30 }, held: true);
            Post(bar, WM.WM_MOUSEMOVE, top, held: true);
            LevelAfterCommitTests.OnTheDock.Pump(600);
            // Carried, unlocked: where it would land shows, a strip along the top; locked, nothing does.
            var landing = Landing(dock);
            if (locked)
            {
                Assert.True(landing is null, "a locked bar showed where it would land");
            }
            else
            {
                Assert.True(landing is { } strip && strip.top == Display.top && strip.left == Display.left && strip.right == Display.right && strip.bottom < Display.bottom / 4,
                    $"where it would land: {(landing is { } shown ? $"{shown.left},{shown.top}..{shown.right},{shown.bottom}" : "nothing")}");
            }
            if (escape)
            {
                _ = PostMessageW(bar, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                LevelAfterCommitTests.OnTheDock.Pump(300);
                Assert.True(Landing(dock) is null, "Escape left where it would land in sight");
            }
            Post(bar, WM.WM_MOUSEMOVE, top, held: true);
            Post(bar, WM.WM_LBUTTONUP, top);
            if (expected == Edge.Top)
            {
                dock.Wait(() => EdgeInFile(dock) == "top", "the file never said the bar is at the top");
                dock.Wait(() => Bar(dock) is var moved && moved != HWND.NULL && Rect(moved).top == Display.top, "the bar never stood at the top");
                Assert.True(Landing(dock) is null, "where it landed stays in sight");
            }
            else
            {
                LevelAfterCommitTests.OnTheDock.Pump(2500);
                Assert.Equal("bottom", EdgeInFile(dock));
                Assert.Equal(Display.bottom, Rect(Bar(dock)).bottom);
                Assert.True(Landing(dock) is null, "where it would have landed stays in sight");
            }
        }, config);
    }

    /// <summary>
    /// §663: a bar of one folder in its middle, locked: the folder's face pressed and carried 300 px along the bar and let
    /// go turns no edit mode on and leaves the face where it stood; the right click on the bar's room, End, Up and Enter
    /// (the default menu's last rows: "Lock the taskbar" over "Taskbar settings") writes it unlocked; the same drag then
    /// turns edit mode on.
    /// </summary>
    [UiFact]
    public void LockedAWidgetDraggedIsNoDragUntilTheMenuUnlocksTheBar()
    {
        var config = new Config();
        Assert.True(config.LockTaskbar);
        config.DefaultLayout.Islands.Clear();
        var folder = BarKind.FolderList.New();
        folder.Id = "box";
        folder.Section = BarSection.Middle;
        var battery = ListKind.Battery.New();
        battery.Id = "battery";
        folder.Folder!.Put(battery);
        config.DefaultLayout.Islands.Add(folder);
        Sections.Order(config.DefaultLayout.Islands);
        LevelAfterCommitTests.OnTheDock.Run("bar-lock-drag", dock =>
        {
            dock.Wait(() => Bar(dock) != HWND.NULL && FaceAt(dock) is not null, "no bar with the folder came");
            LevelAfterCommitTests.OnTheDock.Pump(1500);
            var bar = Bar(dock);
            var face = FaceAt(dock)!.Value;

            // Locked: carried along the bar and let go there, nothing.
            DragAlong(bar, face);
            LevelAfterCommitTests.OnTheDock.Pump(1500);
            Assert.True(Find(dock, null, EditDim) is null, "a locked bar's widget dragged turned edit mode on");
            Assert.Equal(face.x, FaceAt(dock)?.x);
            Assert.Equal("true", LockInFile(dock));

            // The bar's right click on its room: the default menu, its last rows "Lock the taskbar" over "Taskbar settings".
            var room = new POINT { x = Display.left + 200, y = Display.bottom - 12 };
            Post(bar, WM.WM_MOUSEMOVE, room);
            Post(bar, WM.WM_RBUTTONDOWN, room);
            Post(bar, WM.WM_RBUTTONUP, room);
            dock.Wait(() => dock.Menu() != HWND.NULL, "the bar's menu did not open");
            dock.Keys(VK.VK_END, VK.VK_UP, VK.VK_RETURN);
            dock.Wait(() => LockInFile(dock) == "false", "Lock the taskbar chosen did not write the bar unlocked");

            // Unlocked: the same drag is edit mode's.
            dock.Wait(() => Bar(dock) != HWND.NULL && FaceAt(dock) is not null, "the bar did not come back");
            LevelAfterCommitTests.OnTheDock.Pump(1000);
            DragAlong(Bar(dock), FaceAt(dock)!.Value);
            dock.Wait(() => Find(dock, null, EditDim) is not null, "an unlocked bar's widget dragged did not turn edit mode on");
        }, config);
    }

    /// <summary>The edit dim's window title (Dock.EditDeck.cs, EditShade): in sight while edit mode is on.</summary>
    private const string EditDim = "Karpo edit dim";

    /// <summary>A press at <paramref name="start"/>, carried 300 px toward the bar's start in steps, let go there.</summary>
    private static void DragAlong(HWND bar, POINT start)
    {
        Post(bar, WM.WM_MOUSEMOVE, start);
        Post(bar, WM.WM_LBUTTONDOWN, start);
        for (var step = 1; step <= 6; step++)
        {
            Post(bar, WM.WM_MOUSEMOVE, start with { x = start.x - (50 * step) }, held: true);
            LevelAfterCommitTests.OnTheDock.Pump(60);
        }
        Post(bar, WM.WM_LBUTTONUP, start with { x = start.x - 300 });
    }

    /// <summary>The middle of the folder's face on the bar (stage-map.json), screen px; null before it is mapped.</summary>
    private static POINT? FaceAt(LevelAfterCommitTests.OnTheDock dock)
    {
        try
        {
            var path = Path.Combine(dock.Folder, "stage-map.json");
            return File.Exists(path) && JsonNode.Parse(File.ReadAllText(path))!.AsArray()
                    .FirstOrDefault(entry => (string)entry!["kind"]! == nameof(BarKind.FolderList)) is { } face
                ? new POINT { x = (int)face["x"]! + ((int)face["w"]! / 2), y = (int)face["y"]! + ((int)face["h"]! / 2) }
                : null;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>"lockTaskbar" as the file has it, "true" where it says nothing (a new config's).</summary>
    private static string LockInFile(LevelAfterCommitTests.OnTheDock dock)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!;
            return root["lockTaskbar"] is { } locked ? ((bool)locked ? "true" : "false") : "true";
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
        {
            return "";
        }
    }

    private static RECT Rect(HWND window)
    {
        RECT rect;
        _ = GetWindowRect(window, &rect);
        return rect;
    }

    /// <summary>The dock's bar in sight, the first found.</summary>
    private static HWND Bar(LevelAfterCommitTests.OnTheDock dock) => Find(dock, BarClass, null) ?? HWND.NULL;

    /// <summary>Where a bar carried would land, its window in sight: its rectangle; null where none shows.</summary>
    private static RECT? Landing(LevelAfterCommitTests.OnTheDock dock) =>
        Find(dock, null, Karpo.Dock.Dock.LandingTitle) is { } shown && Region(shown) is { } region ? region : null;

    /// <summary>What a pointer window shows: its region's box on the screen (PointerWindow: it spans every display, its region where it shows); null for none.</summary>
    private static RECT? Region(HWND window)
    {
        RECT rect;
        _ = GetWindowRect(window, &rect);
        var region = CreateRectRgn(0, 0, 0, 0);
        try
        {
            if (GetWindowRgn(window, region) == 0)
            {
                return null;
            }
            RECT box;
            _ = GetRgnBox(region, &box);
            return box.right > box.left && box.bottom > box.top
                ? new RECT { left = rect.left + box.left, top = rect.top + box.top, right = rect.left + box.right, bottom = rect.top + box.bottom }
                : null;
        }
        finally
        {
            _ = DeleteObject(region);
        }
    }

    /// <summary>The dock's first window in sight of <paramref name="className"/> or titled <paramref name="title"/>.</summary>
    private static HWND? Find(LevelAfterCommitTests.OnTheDock dock, string? className, string? title)
    {
        var host = dock.Host();
        uint pid = 0;
        if (host is { } own)
        {
            _ = GetWindowThreadProcessId(own, &pid);
        }
        var at = HWND.NULL;
        fixed (char* named = className)
        fixed (char* titled = title)
        {
            while ((at = FindWindowExW(HWND.NULL, at, named, titled)) != HWND.NULL)
            {
                uint of = 0;
                _ = GetWindowThreadProcessId(at, &of);
                if (of == pid && IsWindowVisible(at))
                {
                    return at;
                }
            }
        }
        return null;
    }

    /// <summary>The edge the file says the stage's display's bar stands on: "bottom" where it says none.</summary>
    private static string EdgeInFile(LevelAfterCommitTests.OnTheDock dock)
    {
        try
        {
            var screens = JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!["screens"]?.AsArray();
            return screens is { Count: > 0 } && (string?)screens[0]!["edge"] is { } edge ? edge : "bottom";
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException)
        {
            return "";
        }
    }

    /// <summary>A mouse message at <paramref name="screen"/>, posted to <paramref name="window"/> in its own px; the left button down where <paramref name="held"/>.</summary>
    private static void Post(HWND window, uint message, POINT screen, bool held = false)
    {
        var rect = Rect(window);
        var (x, y) = (screen.x - rect.left, screen.y - rect.top);
        _ = PostMessageW(window, message, held || message == WM.WM_LBUTTONDOWN ? 0x0001u : 0u, (LPARAM)(nint)((y << 16) | (x & 0xFFFF)));
    }
}
