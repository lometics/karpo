// Pictures Karpo as it is, for the website: the real dock on a made-up display (Karpo.exe --stage),
// a rectangle of the left display with a wallpaper behind it (an illustration of store/wallpapers.json, as
// Themia's pictures have them; a scene names its own with `wallpaper NAME`), the pointer moved by a
// script, and the rectangle pictured once a scene is set.
//
//   dotnet run scripts/stage/Stage.cs -- peek            the stage up, a picture of it, the map; out
//   dotnet run scripts/stage/Stage.cs -- shots [scene]   every scene of shots.txt (or one), to artifacts/stage/shots
//   dotnet run scripts/stage/Stage.cs -- try "step" ...  steps of shots.txt's language, pictures to artifacts/stage/shots
//   dotnet run scripts/stage/Stage.cs -- catalog         the folder of nearly every widget (catalog.json), pictured
//   dotnet run scripts/stage/Stage.cs -- store MODE ...  the same for the Microsoft Store's pictures: a 16:9 stage
//                                                        at 200 %, scenes from store/shots.txt, to artifacts/store/shots
//                                                        (STAGE_LANGUAGE=de: Karpo in German, to artifacts/store/shots/de);
//                                                        `store concepts [scene]` the scenes of store/concepts.txt;
//                                                        `store site [scene]` the website's own (store/site.txt on
//                                                        store/site.json), to artifacts/store/shots/site
//
// It runs the dock published native AOT, as Karpo ships, into artifacts/stage/dock-aot (dotnet publish
// Karpo.Dock -c Release -r win-x64, its publish folder copied there; a Debug build draws each flyout
// seconds late the first time, compiling as it goes),
// on a config of its own in artifacts/stage/config, made fresh from scripts/stage/config.json each
// run. The person's own Karpo keeps running; the stage takes no mutex, no appbar and no taskbar.
// The pointer is a drawn one of its own (Pointer): the person's mouse is never moved.

#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property AllowUnsafeBlocks=true
#:property PublishAot=false

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;

var repo = Repo();
var stageDir = Path.Combine(repo, "artifacts", "stage");
// The Store's pictures (store/README.md) are a set of their own: the same modes, another stage.
var store = args.Length > 0 && args[0] == "store";
if (store)
{
    args = args[1..];
}
var mode = args.Length > 0 ? args[0] : "peek";
// The language Karpo speaks on the stage; the Store's pictures of another than English go to a folder of its own.
var language = Environment.GetEnvironmentVariable("STAGE_LANGUAGE") is { Length: > 0 } asked ? asked : "en";
// The time of day the stage's Karpo tells (its KARPO_NOW), whatever hour the pictures are taken at: the Store's early in
// the afternoon, a day's meetings behind and ahead of it; STAGE_TIME=09:30 another, STAGE_TIME=real the PC's own.
var time = Environment.GetEnvironmentVariable("STAGE_TIME") is { Length: > 0 } told ? told : store ? "13:10" : "real";
// `store site`: the website's pictures, of a bar of its own (store/site.json, store/site.txt) on the Store's stage.
var site = store && mode == "site";
var shots = store ? Path.Combine(repo, "artifacts", "store", "shots", site ? "site" : language == "en" ? "" : language) : Path.Combine(stageDir, "shots");
// `store concepts` runs store/concepts.txt's scenes as `store shots` runs shots.txt's: pictures tried for the Store
// before any is chosen, into the same folder.
var scenes = store ? Path.Combine(repo, "store", mode == "concepts" ? "concepts.txt" : site ? "site.txt" : "shots.txt") : Path.Combine(repo, "scripts", "stage", "shots.txt");

// The stage: 2048 x 1280 px of the left display at 150 %, so the dock lays out as on a 1366 x 853
// display, the site's pictures' size, pictured at 1.5 times its pixels.
// The catalogue, one folder of nearly every widget, is a picture of a 16:10 desktop, the site's monitor's shape, nearly the whole left display, room for all of it (the whole display, Windows takes the wallpaper for a full screen app).
// The Store's: 3072 x 1728 px of the left display, 16:9 as the Store shows a screenshot, at 200 %, so
// the dock lays out as on a 1536 x 864 display and reads well when the Store shows it small; its
// catalogue at 160 %, so the five columns fit.
var catalog = mode == "catalog";
var stage = store ? new Rectangle(-3840 + 384, 216, 3072, 1728)
    : catalog ? new Rectangle(-2560 + 160, 20, 2240, 1400) : new Rectangle(-2560 + 256, 72, 2048, 1280);
// At 150 %, and the catalogue at 117 %, so its five columns fit.
var stageDpi = store ? (catalog ? 154 : 192) : catalog ? 112 : 144;

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var config = Path.Combine(stageDir, "config");
if (Directory.Exists(config))
{
    Directory.Delete(config, recursive: true);
}
Directory.CreateDirectory(config);
// The Store's scenes have configs of their own (store/config.json, store/catalog.json): the same bars, filled for the Store.
var template = Path.Combine(repo, store ? "store" : Path.Combine("scripts", "stage"), catalog ? "catalog.json" : site ? "site.json" : "config.json");
if (File.Exists(template))
{
    File.WriteAllText(Path.Combine(config, "config.json"), File.ReadAllText(template).Replace("{files}", DemoFiles(stageDir).Replace("\\", "\\\\")));
}
// The Store's notes and to-do list, made up (store/notebook.json), where an isolated dock keeps its notebook.
var notebook = Path.Combine(repo, "store", "notebook.json");
if (store && File.Exists(notebook))
{
    Directory.CreateDirectory(Path.Combine(config, "local"));
    File.Copy(notebook, Path.Combine(config, "local", "notebook.json"));
}

using var wall = Wallpaper.Show(stage, Path.Combine(repo, "store", "wallpapers"));
var dock = StartDock(repo, config, stage, stageDpi, language, time);
Pointer.Wall = wall.Handle;
try
{
    var map = Map.Wait(Path.Combine(config, "stage-map.json"), TimeSpan.FromSeconds(30));
    // The bar comes in a moment after its first build.
    Thread.Sleep(5000);
    map = Map.Read(Path.Combine(config, "stage-map.json"));
    switch (mode)
    {
        case "peek":
            Snap.Shot(stage, Path.Combine(stageDir, "peek.png"));
            Snap.Shot(new Rectangle(-2560, 0, 2560, 1440), Path.Combine(stageDir, "peek-display.png"));
            foreach (var e in map.Entries)
            {
                Console.WriteLine($"{e.Hit,-8} {e.Kind,-14} {e.Key,-28} part {e.Part,2} index {e.Index,2}  {e.X},{e.Y} {e.W}x{e.H}");
            }
            WindowList.Dump(dock.Id);
            Console.WriteLine($"peek: {Path.Combine(stageDir, "peek.png")}");
            break;
        case "shots":
        case "concepts" when store:
        case "site" when store:
            // Every scene of shots.txt, each a picture and its captions in artifacts/stage/shots.
            new Script(stage, stageDpi, Path.Combine(config, "stage-map.json"), config, shots, dock.Id, wall)
                .Run(File.ReadAllLines(scenes), args.Length > 1 ? args[1] : null);
            break;
        case "catalog":
            // The folder of every widget open, its picture in artifacts/stage/shots/catalog.png; the
            // arrow then off the stage, past its right end, whatever the stage's width.
            new Script(stage, stageDpi, Path.Combine(config, "stage-map.json"), config, shots, dock.Id, wall)
                .Run(["wait 3500", "open everything", "wait 3500", $"move {stage.Width + 60},150 400", "wait 600", "shot catalog"], null);
            if (Environment.GetEnvironmentVariable("STAGE_DEBUG") is not null)
            {
                WindowList.Dump(dock.Id);
            }
            break;
        case "try":
            // Steps from the command line, pictures in artifacts/stage/shots.
            new Script(stage, stageDpi, Path.Combine(config, "stage-map.json"), config, shots, dock.Id, wall)
                .Run(args.Skip(1).ToArray(), null);
            break;
        default:
            Console.Error.WriteLine("usage: Stage.cs peek|shots [scene]|try step...|catalog");
            break;
    }
}
finally
{
    if (dock.HasExited)
    {
        Console.Error.WriteLine($"stage: the dock ended on its own, code {dock.ExitCode} (0x{dock.ExitCode:X8}), {dock.ExitTime:HH:mm:ss}");
    }
    if (!dock.HasExited)
    {
        dock.Kill();
        dock.WaitForExit(5000);
    }
}

static string Repo([CallerFilePath] string here = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", ".."));

static Process StartDock(string repo, string config, Rectangle stage, int dpi, string language, string time)
{
    var exe = Path.Combine(repo, "artifacts", "stage", "dock-aot", "Karpo.exe");
    var start = new ProcessStartInfo(exe, ["--stage", $"{stage.X},{stage.Y},{stage.Width},{stage.Height},{dpi}"])
    {
        UseShellExecute = false,
    };
    start.Environment["KARPO_CONFIG_DIR"] = config;
    start.Environment["KARPO_QUICK_SAMPLES"] = "1";
    start.Environment["KARPO_LANGUAGE"] = language;
    if (time != "real")
    {
        start.Environment["KARPO_NOW"] = time;
    }
    return Process.Start(start) ?? throw new InvalidOperationException("Karpo.exe did not start");
}

static class Snap
{
    public static void Shot(Rectangle area, string path)
    {
    using var bitmap = new Bitmap(area.Width, area.Height);
    using (var g = Graphics.FromImage(bitmap))
    {
        g.CopyFromScreen(area.X, area.Y, 0, 0, area.Size);
    }
    bitmap.Save(path, ImageFormat.Png);
    }
}

/// <summary>
/// The desktop behind the stage: a borderless topmost window, made before the dock's, which stand over it. Its picture
/// is one of the folder's (store/wallpapers, fetched by `node store/wallpapers.mjs`), by name; a scene starts on
/// <see cref="Usual"/> and may name another.
/// </summary>
sealed class Wallpaper : IDisposable
{
    /// <summary>The wallpaper of a scene that names none.</summary>
    public const string Usual = "tropical-bay";

    private readonly Thread thread;
    private readonly Rectangle area;
    private readonly string folder;
    private Form? form;
    private string shown = Usual;

    private Wallpaper(Rectangle area, string folder)
    {
        this.area = area;
        this.folder = folder;
        var first = Fit(Usual);
        using var ready = new ManualResetEventSlim();
        thread = new Thread(() =>
        {
            form = new StillForm
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                BackColor = Color.Black,
                BackgroundImage = first,
                BackgroundImageLayout = ImageLayout.Stretch,
            };
            form.Load += (_, _) => form.Bounds = area;
            form.Shown += (_, _) => ready.Set();
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
        Thread.Sleep(300);
    }

    public static Wallpaper Show(Rectangle area, string folder) => new(area, folder);

    /// <summary>Another picture behind the stage, by its name in the folder; the one already there stays.</summary>
    public void Set(string name)
    {
        if (name == shown || form is null)
        {
            return;
        }
        var next = Fit(name);
        form.Invoke(() =>
        {
            var old = form.BackgroundImage;
            form.BackgroundImage = next;
            old?.Dispose();
            form.Refresh();
        });
        shown = name;
        Thread.Sleep(300);
    }

    /// <summary>The name of the picture behind the stage now.</summary>
    public string Shown => shown;

    /// <summary>
    /// The picture behind the stage now, as it is drawn there, px for px, to wallpaper-NAME.png in a folder: what a
    /// capture shows wherever nothing of Karpo's stands, for whoever cuts the captures.
    /// </summary>
    public void Save(string folder)
    {
        using var picture = Fit(shown);
        picture.Save(Path.Combine(folder, $"wallpaper-{shown}.png"), ImageFormat.Png);
    }

    /// <summary>The picture covering the stage: scaled till it fills it, what is over cut off evenly at both sides.</summary>
    private Bitmap Fit(string name)
    {
        var file = Path.Combine(folder, name + ".png");
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"stage: no wallpaper {name}; fetch them with `node store/wallpapers.mjs`", file);
        }
        using var source = Image.FromFile(file);
        var scale = Math.Max((float)area.Width / source.Width, (float)area.Height / source.Height);
        var (w, h) = (area.Width / scale, area.Height / scale);
        var bitmap = new Bitmap(area.Width, area.Height);
        using var g = Graphics.FromImage(bitmap);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(source, new Rectangle(0, 0, area.Width, area.Height), new RectangleF((source.Width - w) / 2, (source.Height - h) / 2, w, h), GraphicsUnit.Pixel);
        return bitmap;
    }

    public IntPtr Handle => form?.Invoke(() => form.Handle) ?? IntPtr.Zero;

    public void Dispose()
    {
        form?.Invoke(() => form.Close());
        thread.Join(2000);
    }
}

sealed record Entry(string Hit, int Index, int Part, string Key, string Kind, int X, int Y, int W, int H)
{
    public Point Middle => new(X + (W / 2), Y + (H / 2));
}

sealed class Map(List<Entry> entries)
{
    public List<Entry> Entries { get; } = entries;

    public static Map Read(string path)
    {
        for (var tries = 0; ; tries++)
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                return new Map([.. json.RootElement.EnumerateArray().Select(e => new Entry(
                    e.GetProperty("hit").GetString()!, e.GetProperty("index").GetInt32(), e.GetProperty("part").GetInt32(),
                    e.GetProperty("key").GetString()!, e.GetProperty("kind").GetString()!,
                    e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32()))]);
            }
            catch (Exception) when (tries < 20)
            {
                Thread.Sleep(50);
            }
        }
    }

    public static Map Wait(string path, TimeSpan most)
    {
        var until = DateTime.UtcNow + most;
        while (!File.Exists(path))
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException("the dock wrote no stage-map.json: did it start?");
            }
            Thread.Sleep(100);
        }
        return Read(path);
    }

    /// <summary>The first entry of a part of this kind (BarKind), or with this key.</summary>
    public Entry Of(string kindOrKey, int nth = 0) =>
        Entries.Where(e => e.Kind.Equals(kindOrKey, StringComparison.OrdinalIgnoreCase) || e.Key.Equals(kindOrKey, StringComparison.OrdinalIgnoreCase)).Skip(nth).FirstOrDefault()
        ?? throw new KeyNotFoundException($"nothing on the bar is {kindOrKey}; there are: {string.Join(", ", Entries.Select(e => e.Kind + "/" + e.Key).Distinct())}");

    public Entry? Find(string kindOrKey) =>
        Entries.FirstOrDefault(e => e.Kind.Equals(kindOrKey, StringComparison.OrdinalIgnoreCase) || e.Key.Equals(kindOrKey, StringComparison.OrdinalIgnoreCase));
}

static unsafe partial class WindowList
{
    [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hwnd, char* name, int count);
    [DllImport("user32.dll")] private static extern int GetWindowLongW(IntPtr hwnd, int index);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>Every top-level window of the dock's process and this one, front to back.</summary>
    public static void Dump(int dockPid)
    {
        var self = Environment.ProcessId;
        var name = stackalloc char[64];
        var z = 0;
        for (var w = GetTopWindow(IntPtr.Zero); w != IntPtr.Zero; w = GetWindow(w, 2), z++)
        {
            GetWindowThreadProcessId(w, out var pid);
            if (pid != dockPid && pid != self) continue;
            GetWindowRect(w, out var r);
            var n = GetClassNameW(w, name, 64);
            DwmGetWindowAttribute(w, 14, out var cloaked, 4);
            var ex = GetWindowLongW(w, -20);
            Console.WriteLine($"z{z,4} {(pid == self ? "wall" : "dock")} {new string(name, 0, n),-20} vis {IsWindowVisible(w),-5} cloak {cloaked} top {(ex & 8) != 0,-5} {r.Left},{r.Top} {r.Right - r.Left}x{r.Bottom - r.Top}");
        }
    }
}

/// <summary>
/// The script's pointer, which is not the person's: an arrow of its own drawn in a window nothing can
/// click, moved as a hand moves it, and its moves, presses and wheel
/// posted to whichever of the dock's windows is under it. The person's mouse stays where it is.
/// </summary>
static unsafe class Pointer
{
    private const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MOUSEWHEEL = 0x020A;
    private const int MK_LBUTTON = 0x0001, MK_RBUTTON = 0x0002;

    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(IntPtr hwnd, ref Point at);

    private static CursorWindow? arrow;
    private static int dockPid;

    public static Point At { get; private set; }

    /// <summary>The stage's wallpaper window, kept under the dock's.</summary>
    public static IntPtr Wall { get; set; }

    private static long kept;

    /// <summary>The arrow up, over everything, at <paramref name="scale"/> times its 100 % size, for the dock of <paramref name="pid"/>.</summary>
    public static void Start(int pid, Point at, float scale)
    {
        dockPid = pid;
        arrow ??= new CursorWindow(scale);
        Jump(at);
    }

    public static void Stop()
    {
        arrow?.Dispose();
        arrow = null;
    }

    /// <summary>The arrow off the stage for a picture, the dock told nothing: to it the pointer is still where it was.</summary>
    public static void Away() => arrow?.MoveTo(new Point(-32000, -32000));

    /// <summary>The arrow back where the pointer is.</summary>
    public static void Back() => arrow?.MoveTo(At);

    /// <summary>Straight to <paramref name="to"/>, in screen px.</summary>
    public static void Jump(Point to)
    {
        // The wallpaper stays just under the dock's windows, whatever else came to the front.
        if (Wall != IntPtr.Zero && Environment.TickCount64 - kept > 250)
        {
            kept = Environment.TickCount64;
            WindowList.KeepUnder(Wall, dockPid);
        }
        At = to;
        arrow?.MoveTo(to);
        // The dock hears the pointer at most twenty times a second, and always where it stops: each
        // move has it light what is under it and, told at once that the pointer left (Windows' own
        // is elsewhere), put that out again, and at the arrow's pace that kept it too busy to draw.
        if (settled || Environment.TickCount64 - heard >= 50)
        {
            heard = Environment.TickCount64;
            Post(WM_MOUSEMOVE, 0, to);
        }
    }

    private static long heard;

    /// <summary>Whether the move being made is where a glide ends, which the dock always hears.</summary>
    private static bool settled = true;

    /// <summary>To <paramref name="to"/> over <paramref name="ms"/>, slow out, slow in, along a slight curve.</summary>
    public static void Glide(Point to, int ms = 700)
    {
        var from = At;
        var watch = Stopwatch.StartNew();
        var bend = new PointF((to.Y - from.Y) * 0.08f, (from.X - to.X) * 0.08f);
        while (watch.ElapsedMilliseconds < ms)
        {
            var t = watch.ElapsedMilliseconds / (float)ms;
            var e = t < 0.5f ? 4 * t * t * t : 1 - (MathF.Pow(-2 * t + 2, 3) / 2);
            var arc = 4 * e * (1 - e);
            settled = false;
            Jump(new Point(
                (int)MathF.Round(from.X + ((to.X - from.X) * e) + (bend.X * arc)),
                (int)MathF.Round(from.Y + ((to.Y - from.Y) * e) + (bend.Y * arc))));
            settled = true;
            Thread.Sleep(8);
        }
        Jump(to);
    }

    public static void Click(int holdMs = 70)
    {
        // A click on the desktop closes what is open, as the dock's own hook hears a real one do;
        // posted, it reaches no window of the dock's, so the flyouts hear Escape instead.
        if (WindowList.DockWindowAt(dockPid, At) is null)
        {
            WindowList.Escape(dockPid);
            return;
        }
        // Move, press and let go in one go: each move has the dock ask Windows to say when the
        // pointer leaves, and Windows, whose pointer is elsewhere, says so at once; queued behind the
        // press, that word can no longer take the press's target away.
        // All three to the one window under the arrow now: a flyout the press opens at once must not
        // take the release, or the bar is left pressed and reads a press and hold.
        var window = WindowList.DockWindowAt(dockPid, At);
        Post(window, WM_MOUSEMOVE, 0, At);
        Post(window, WM_LBUTTONDOWN, MK_LBUTTON, At);
        Post(window, WM_LBUTTONUP, 0, At);
        Thread.Sleep(holdMs);
    }

    public static void RightClick()
    {
        var window = WindowList.DockWindowAt(dockPid, At);
        Post(window, WM_MOUSEMOVE, 0, At);
        Post(window, WM_RBUTTONDOWN, MK_RBUTTON, At);
        Post(window, WM_RBUTTONUP, 0, At);
        Thread.Sleep(70);
    }

    /// <summary>The wheel, <paramref name="notches"/> notches (negative: towards the person), one every <paramref name="everyMs"/>.</summary>
    public static void Wheel(int notches, int everyMs = 90)
    {
        for (var i = 0; i < Math.Abs(notches); i++)
        {
            if (WindowList.DockWindowAt(dockPid, At) is { } window)
            {
                // The wheel's place is the screen's, not the window's.
                PostMessageW(window, WM_MOUSEWHEEL, (IntPtr)((120 * Math.Sign(notches)) << 16), Lparam(At));
            }
            Thread.Sleep(everyMs);
        }
    }

    private static void Post(uint message, int keys, Point at) => Post(WindowList.DockWindowAt(dockPid, at), message, keys, at);

    private static void Post(IntPtr? to, uint message, int keys, Point at)
    {
        if (to is not { } window)
        {
            return;
        }
        var local = at;
        ScreenToClient(window, ref local);
        PostMessageW(window, message, keys, Lparam(local));
    }

    private static IntPtr Lparam(Point p) => (IntPtr)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF));
}

/// <summary>Windows 11's arrow, white with a dark edge, in a layered window that takes no clicks and is always in front.</summary>
sealed class CursorWindow : IDisposable
{
    private const int Side = 64;
    private readonly Thread thread;
    private ArrowForm? form;

    public CursorWindow(float scale)
    {
        using var ready = new ManualResetEventSlim();
        thread = new Thread(() =>
        {
            form = new ArrowForm(scale);
            form.Shown += (_, _) => ready.Set();
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
    }

    public void MoveTo(Point at) => form?.MoveTo(at);

    public void Dispose()
    {
        form?.Invoke(() => form.Close());
        thread.Join(2000);
    }

    private sealed class ArrowForm : Form
    {
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, IntPtr pos, ref Size size, IntPtr src, ref Point srcPos, int key, ref Blend blend, int flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Blend { public byte Op, Flags, Alpha, Format; }

        private readonly float scale;

        public ArrowForm(float scale)
        {
            this.scale = scale;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-32000, -32000, Side, Side);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams;
                // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                p.ExStyle |= 0x00080000 | 0x00000020 | 0x00000008 | 0x00000080 | 0x08000000;
                return p;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Draw();
        }

        /// <summary>The arrow's tip at <paramref name="at"/>, in front of the dock's newest flyout.</summary>
        public void MoveTo(Point at) =>
            SetWindowPos(Handle, (IntPtr)(-1), at.X - 1, at.Y - 1, 0, 0, 0x0001 | 0x0010 | 0x0040); // NOSIZE | NOACTIVATE | SHOWWINDOW

        private void Draw()
        {
            using var bitmap = new Bitmap(Side, Side, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.TranslateTransform(1.5f, 1.5f);
                g.ScaleTransform(scale, scale);
                // The arrow at 100 %: 12 x 19 px, as Windows 11 draws it.
                PointF[] outline =
                [
                    new(0, 0), new(0, 16.5f), new(4, 12.8f), new(6.6f, 18.8f), new(9.2f, 17.7f), new(6.7f, 11.8f), new(12, 11.8f),
                ];
                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddPolygon(outline);
                using var shadow = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
                g.TranslateTransform(0.6f, 0.9f);
                g.FillPath(shadow, path);
                g.TranslateTransform(-0.6f, -0.9f);
                g.FillPath(Brushes.White, path);
                using var edge = new Pen(Color.Black, 1.1f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
                g.DrawPath(edge, path);
            }
            var screen = GetDC(IntPtr.Zero);
            var memory = CreateCompatibleDC(screen);
            var hbitmap = bitmap.GetHbitmap(Color.FromArgb(0));
            var old = SelectObject(memory, hbitmap);
            var size = new Size(Side, Side);
            var origin = Point.Empty;
            var blend = new Blend { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
            UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, memory, ref origin, 0, ref blend, 2);
            SelectObject(memory, old);
            DeleteObject(hbitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
}

static unsafe partial class WindowList
{
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point at);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    /// <summary>
    /// The dock's window at <paramref name="at"/> as Windows hit-tests it — a bar's window is taller
    /// than the bar, for its tooltips, and lets the flyouts under it have their own pointer — or null
    /// where the window there is not the dock's. The script's arrow takes no part: it lets the pointer through.
    /// </summary>
    public static IntPtr? DockWindowAt(int dockPid, Point at)
    {
        var w = GetAncestor(WindowFromPoint(at), 2); // GA_ROOT
        GetWindowThreadProcessId(w, out var pid);
        return w != IntPtr.Zero && pid == dockPid ? w : null;
    }
}

static partial class Program
{
    /// <summary>
    /// The folders the Files folder shows, filled with made-up files (empty, so their icons are the
    /// types' own) and Windows' own wallpapers as pictures, so nobody's real files are in a picture.
    /// </summary>
    static string DemoFiles(string stageDir)
    {
        var root = Path.Combine(stageDir, "files");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        var made = new Dictionary<string, string[]>
        {
            ["Downloads"] = ["Boarding pass LX 1610.pdf", "Invoice 2026-10.pdf", "Q4 numbers.xlsx", "Team offsite.zip", "Karpo press kit.zip"],
            ["Documents"] = ["Design review.pptx", "Budget 2027.xlsx", "Meeting notes.docx", "Roadmap.docx", "Travel checklist.txt"],
        };
        var at = DateTime.Now;
        foreach (var (folder, names) in made)
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
            foreach (var name in names)
            {
                var path = Path.Combine(root, folder, name);
                File.WriteAllBytes(path, []);
                File.SetLastWriteTime(path, at = at.AddMinutes(-37));
            }
        }
        var pictures = Directory.CreateDirectory(Path.Combine(root, "Pictures")).FullName;
        var wallpapers = Directory.EnumerateFiles(@"C:\Windows\Web\Wallpaper", "*.jpg", SearchOption.AllDirectories).Take(6).ToList();
        string[] titles = ["Lake Lugano.jpg", "Morning hike.jpg", "Old town.jpg", "Sunset.jpg", "Mountains.jpg", "Weekend.jpg"];
        for (var i = 0; i < wallpapers.Count; i++)
        {
            File.Copy(wallpapers[i], Path.Combine(pictures, titles[i]));
        }
        return root;
    }
}

/// <summary>
/// The pictures, in a little language, a step a line (shots.txt):
///   scene NAME           a new scene: what follows sets it up, and its shot is NAME.png; it starts on the usual wallpaper
///   wallpaper NAME       another wallpaper behind this scene (store/wallpapers.json)
///   move TARGET [MS]     the pointer glides there: an island's key ("clock") or kind, "x,y" in the stage's
///                        own px, or "~dx,dy" from where it is; a key with ":dx,dy" is that far from its middle
///   click | rclick       the left or right button, where the pointer is
///   open TARGET          the pointer there and a click, again until a flyout is open, and a moment
///                        for it to draw
///   close                every open flyout dismissed, and nothing until they are gone
///   wait MS              nothing for a while
///   wheel N              the wheel, N notches (negative: down)
///   layout ID            the stage's layout switched in its config, as the settings app would
///   edge SIDE            the stage's bar moved to an edge (bottom, top, left, right) in its config
///   palette ID [MODE]    the Appearance's palette worn ("paper", "harbor"; "karpo" for Karpo's own) in its config,
///                        in Windows' mode or in MODE (dark, light)
///   say KEY SIDE TARGET  a caption of the scene's: the site's words.say[KEY] in a bubble on the SIDE
///                        (above, below, left, right) of TARGET (as move's), a line from it to there;
///                        SIDE:N has the line N times as long, to stand clear of another bubble
///   shot [NAME]          the stage pictured without the arrow: the scene's NAME.png and its captions in
///                        NAME.json (where each points, in fractions of the picture), or NAME.png alone
///   dump                 the dock's windows, front to back, to the console
///   frame NAME           a picture of the open flyouts and the bar, and a little room round them
///   # ...                a note
/// </summary>
sealed class Script(Rectangle stage, int dpi, string mapPath, string config, string outDir, int dockPid, Wallpaper wall)
{
    /// <summary>The scene's captions so far: what, on which side of where, how far from it.</summary>
    private readonly List<(string Say, string Side, double Reach, PointF At)> says = [];

    private string? scene;

    /// <summary>
    /// What the scene has open for its picture: the step that opened it (an `open`, or the `click` after a `move`),
    /// where, and the waits since. A window coming forward elsewhere on the PC closes a flyout at any moment, and a
    /// folder sometimes lets a click pass, so a picture first makes sure something stands open.
    /// </summary>
    private (string Verb, string Target, int Settle)? opening;

    private string? moved;

    public void Run(string[] steps, string? only)
    {
        Directory.CreateDirectory(outDir);
        // From the middle of the desktop, where a hand would start.
        Pointer.Start(dockPid, new Point(stage.X + (stage.Width * 2 / 3), stage.Y + (stage.Height / 2)), dpi / 96f);
        try
        {
            foreach (var raw in steps)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }
                var words = line.Split(' ', 2, StringSplitOptions.TrimEntries);
                var rest = words.Length > 1 ? words[1] : "";
                if (words[0] == "scene")
                {
                    scene = rest;
                    says.Clear();
                    opening = null;
                    // A scene run alone shows what it shows in a whole run: each starts on the usual wallpaper.
                    if (only is null || only == scene)
                    {
                        wall.Set(Wallpaper.Usual);
                    }
                    continue;
                }
                // Another scene's steps still run when they change the stage (its layout, its edge).
                if (only is not null && scene is not null && only != scene && words[0] is not ("layout" or "edge"))
                {
                    continue;
                }
                Step(words[0], rest);
            }
        }
        finally
        {
            Pointer.Stop();
        }
    }

    private void Step(string verb, string rest)
    {
        switch (verb)
        {
            case "move":
                var parts = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Pointer.Glide(Target(parts[0]), parts.Length > 1 ? int.Parse(parts[1]) : 650);
                Console.WriteLine($"  move {parts[0]}");
                moved = parts[0];
                break;
            case "click":
                Pointer.Click();
                // A click after a move may be what opens the scene's flyout: its own picture holds it to that.
                if (opening is null && moved is not null)
                {
                    opening = ("click", moved, 0);
                }
                break;
            case "open":
                Open(rest);
                opening = ("open", rest, 0);
                break;
            case "close":
                opening = null;
                WindowList.Escape(dockPid);
                Until(() => WindowList.FlyoutBox(dockPid, stage) is null, 3000);
                // Long enough that the next click isn't taken for the closing folder's.
                Thread.Sleep(1600);
                break;
            case "rclick":
                Pointer.RightClick();
                break;
            case "wait":
                Thread.Sleep(int.Parse(rest));
                if (opening is { } o)
                {
                    opening = o with { Settle = o.Settle + int.Parse(rest) };
                }
                break;
            case "wheel":
                Pointer.Wheel(int.Parse(rest));
                break;
            case "layout":
                opening = null;
                Screen("activeLayoutId", rest);
                break;
            case "edge":
                opening = null;
                Screen("edge", rest);
                break;
            case "palette":
                Palette(rest.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                break;
            case "wallpaper":
                wall.Set(rest);
                break;
            case "say":
                var say = rest.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                var at = Target(say[2]);
                var side = say[1].Split(':');
                var reach = side.Length > 1 ? double.Parse(side[1], System.Globalization.CultureInfo.InvariantCulture) : 1;
                says.Add((say[0], side[0], reach, new PointF((at.X - stage.X) / (float)stage.Width, (at.Y - stage.Y) / (float)stage.Height)));
                Console.WriteLine($"  say {say[0]} {say[1]} {say[2]}");
                break;
            case "shot":
                StillOpen(own: rest.Length == 0);
                Shot(rest.Length > 0 ? rest : scene ?? throw new FormatException("shots: a shot with no name and no scene"));
                break;
            case "dump":
                WindowList.Dump(dockPid);
                break;
            case "frame":
                Frame(rest);
                break;
            default:
                throw new FormatException($"shots: no step called {verb}");
        }
    }

    /// <summary>
    /// A folder opened from the bar: a click, and another if no flyout shows (the dock, just built or
    /// just done closing one, can let a click go by), then a moment for its widgets to draw.
    /// </summary>
    private void Open(string target)
    {
        Pointer.Glide(Target(target), 300);
        for (var tries = 0; tries < 5; tries++)
        {
            Pointer.Click();
            if (Until(() => WindowList.FlyoutBox(dockPid, stage) is not null, 4000))
            {
                Thread.Sleep(2500);
                // Still there: a window coming forward elsewhere on the PC closes a flyout, and the
                // picture would be of the bare desktop.
                if (WindowList.FlyoutBox(dockPid, stage) is null)
                {
                    Console.WriteLine($"  open {target}: closed again, once more");
                    Thread.Sleep(800);
                    continue;
                }
                Console.WriteLine($"  open {target}");
                return;
            }
        }
        throw new InvalidOperationException($"shots: {target} opened nothing");
    }

    /// <summary>
    /// What the scene opened still stands, or is opened again as the scene opened it, its waits waited again. A click
    /// counts for the scene's own picture alone (<paramref name="own"/>, a `shot` with no name): there it was to open
    /// what the scene shows; before a named picture a click may have been for anything.
    /// </summary>
    private void StillOpen(bool own)
    {
        if (opening is not { } o || (o.Verb == "click" && !own))
        {
            return;
        }
        for (var tries = 0; WindowList.FlyoutBox(dockPid, stage) is null; tries++)
        {
            if (tries == 5)
            {
                throw new InvalidOperationException($"shots: {o.Target} would not stay open for its picture");
            }
            Console.WriteLine($"  {o.Target}: closed before its picture, once more");
            if (o.Verb == "open")
            {
                Open(o.Target);
            }
            else
            {
                Pointer.Glide(Target(o.Target), 300);
                Pointer.Click();
            }
            Thread.Sleep(o.Settle);
        }
    }

    /// <summary>Whether <paramref name="done"/> came true within <paramref name="ms"/>.</summary>
    private static bool Until(Func<bool> done, int ms)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.ElapsedMilliseconds > ms)
            {
                return false;
            }
            Thread.Sleep(50);
        }
        return true;
    }

    /// <summary>The stage pictured with the arrow away, and the scene's captions beside it.</summary>
    private void Shot(string name)
    {
        Pointer.Away();
        Thread.Sleep(150);
        Snap.Shot(stage, Path.Combine(outDir, name + ".png"));
        Pointer.Back();
        if (name == scene)
        {
            // Which wallpaper stands behind this scene, and that wallpaper itself as drawn: beside the captures.
            var behind = Path.Combine(outDir, "wallpapers.json");
            var walls = File.Exists(behind) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(behind)) ?? [] : [];
            walls[name] = wall.Shown;
            File.WriteAllText(behind, JsonSerializer.Serialize(new SortedDictionary<string, string>(walls), new JsonSerializerOptions { WriteIndented = true }));
            wall.Save(outDir);
            var json = says.Select(c => new Dictionary<string, object>
            {
                ["say"] = c.Say,
                ["side"] = c.Side,
                ["reach"] = c.Reach,
                ["at"] = new[] { Math.Round(c.At.X, 4), Math.Round(c.At.Y, 4) },
            });
            File.WriteAllText(Path.Combine(outDir, name + ".json"), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
        }
        Console.WriteLine($"  shot {name}");
    }

    /// <summary>The open flyouts and the bar, 32 px of the desktop round them, pictured.</summary>
    private void Frame(string name)
    {
        var flyouts = WindowList.FlyoutBox(dockPid, stage) ?? RectangleF.Empty;
        var both = flyouts.IsEmpty ? Bar() : RectangleF.Union(flyouts, Bar());
        var px = Rectangle.Intersect(
            Rectangle.Round(new RectangleF(stage.X + (both.X * stage.Width) - 32, stage.Y + (both.Y * stage.Height) - 32, (both.Width * stage.Width) + 64, (both.Height * stage.Height) + 64)),
            stage);
        Snap.Shot(px, Path.Combine(outDir, name + ".png"));
        Console.WriteLine($"  frame {name}: {px.Width} x {px.Height}");
    }

    /// <summary>The bar, everything on it, in fractions of the stage.</summary>
    private RectangleF Bar()
    {
        var all = Map.Read(mapPath).Entries.Select(e => new Rectangle(e.X, e.Y, e.W, e.H)).Aggregate(Rectangle.Union);
        return new RectangleF((all.X - stage.X) / (float)stage.Width, (all.Y - stage.Y) / (float)stage.Height, all.Width / (float)stage.Width, all.Height / (float)stage.Height);
    }

    private Point Target(string what)
    {
        var at = Pointer.At;
        if (what.StartsWith('~'))
        {
            var d = what[1..].Split(',').Select(int.Parse).ToArray();
            return new Point(at.X + d[0], at.Y + d[1]);
        }
        var xy = what.Split(',');
        if (xy.Length == 2 && int.TryParse(xy[0], out var x) && int.TryParse(xy[1], out var y))
        {
            return new Point(stage.X + x, stage.Y + y);
        }
        var name = what;
        var offset = Point.Empty;
        if (what.IndexOf(':') is var colon and > 0)
        {
            name = what[..colon];
            var d = what[(colon + 1)..].Split(',').Select(int.Parse).ToArray();
            offset = new Point(d[0], d[1]);
        }
        var map = Map.Read(mapPath);
        var entry = map.Entries.FirstOrDefault(e => e.Key.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase)) ?? map.Of(name);
        return new Point(entry.Middle.X + offset.X, entry.Middle.Y + offset.Y);
    }

    /// <summary>The stage's display's layout or edge, as the edit bar switches one: its config rewritten in one step.</summary>
    private void Screen(string key, string value) => Rewrite(json => json["screens"]![0]![key] = value);

    /// <summary>The Appearance's palette and mode, as its Settings page switches them: Karpo's own and Windows' mode say nothing.</summary>
    private void Palette(string[] worn) => Rewrite(json =>
    {
        var appearance = new System.Text.Json.Nodes.JsonObject();
        if (worn.Length > 1)
        {
            appearance["mode"] = worn[1];
        }
        if (worn.Length > 0 && worn[0] != "karpo")
        {
            appearance["palette"] = worn[0];
        }
        json["appearance"] = appearance.Count > 0 ? appearance : null;
    });

    /// <summary>The stage's config changed and written in one step, for the dock to read.</summary>
    private void Rewrite(Action<System.Text.Json.Nodes.JsonNode> change)
    {
        var path = Path.Combine(config, "config.json");
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        change(json);
        File.WriteAllText(path + ".tmp", json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        // The dock may be reading or writing it this very moment: the move waits its turn.
        for (var tries = 0; ; tries++)
        {
            try
            {
                File.Move(path + ".tmp", path, overwrite: true);
                break;
            }
            catch (UnauthorizedAccessException) when (tries < 20)
            {
                Thread.Sleep(100);
            }
            catch (IOException) when (tries < 20)
            {
                Thread.Sleep(100);
            }
        }
        // The dock reads it and builds the bar again; the map follows.
        Thread.Sleep(600);
    }
}

/// <summary>A window nothing activates, so it never comes up over the dock's: a click on it only dismisses.</summary>
sealed class StillForm : Form
{
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var p = base.CreateParams;
            p.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return p;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // WM_MOUSEACTIVATE: MA_NOACTIVATE
        if (m.Msg == 0x0021)
        {
            m.Result = 3;
            return;
        }
        base.WndProc(ref m);
    }
}

static unsafe partial class WindowList
{
    private delegate bool EnumProc(IntPtr hwnd, IntPtr state);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc proc, IntPtr state);

    /// <summary>
    /// Where the dock's open flyouts stand, as one box in fractions of the stage: every visible
    /// karpo.flyout window of the dock's on the stage. Null when none is open.
    /// </summary>
    public static RectangleF? FlyoutBox(int dockPid, Rectangle stage)
    {
        Rectangle? all = null;
        var name = stackalloc char[64];
        var at = (IntPtr)name;
        EnumWindows((w, _) =>
        {
            GetWindowThreadProcessId(w, out var pid);
            if (pid != dockPid || !IsWindowVisible(w))
            {
                return true;
            }
            var n = GetClassNameW(w, (char*)at, 64);
            if (new string((char*)at, 0, n) != "karpo.flyout" || !GetWindowRect(w, out var r))
            {
                return true;
            }
            var box = Rectangle.Intersect(Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom), stage);
            if (!box.IsEmpty)
            {
                all = all is { } so_far ? Rectangle.Union(so_far, box) : box;
            }
            return true;
        }, IntPtr.Zero);
        return all is { } found
            ? new RectangleF((found.X - stage.X) / (float)stage.Width, (found.Y - stage.Y) / (float)stage.Height, found.Width / (float)stage.Width, found.Height / (float)stage.Height)
            : null;
    }
}

static unsafe partial class WindowList
{
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

    /// <summary>Every open flyout of the dock's hears Escape, which closes it.</summary>
    public static void Escape(int dockPid)
    {
        var name = stackalloc char[64];
        for (var w = GetTopWindow(IntPtr.Zero); w != IntPtr.Zero; w = GetWindow(w, 2))
        {
            GetWindowThreadProcessId(w, out var pid);
            if (pid == dockPid && IsWindowVisible(w) && new string(name, 0, GetClassNameW(w, name, 64)) == "karpo.flyout")
            {
                PostMessageW(w, 0x0100, 0x1B, IntPtr.Zero); // WM_KEYDOWN, VK_ESCAPE
                PostMessageW(w, 0x0101, 0x1B, unchecked((IntPtr)0xC0000001)); // WM_KEYUP
            }
        }
    }

    /// <summary><paramref name="wall"/> just under the last of the dock's visible windows, in the topmost band with them.</summary>
    public static void KeepUnder(IntPtr wall, int dockPid)
    {
        IntPtr last = IntPtr.Zero;
        for (var w = GetTopWindow(IntPtr.Zero); w != IntPtr.Zero; w = GetWindow(w, 2))
        {
            GetWindowThreadProcessId(w, out var pid);
            if (pid == dockPid && IsWindowVisible(w))
            {
                last = w;
            }
        }
        if (last != IntPtr.Zero)
        {
            SetWindowPos(wall, last, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE | NOMOVE | NOACTIVATE
        }
    }
}

