// The flicker harness (docs\widgets.md §354; the flicker design's §6): a transition filmed as the
// screen shows it. Karpo's windows are captured as DWM holds them (PrintWindow, PW_RENDERFULLCONTENT)
// and laid one over the other in their z-order on a backdrop of a colour nothing draws: before, then
// the transition run in a frame (Surfaces.Begin) as the dock's handler runs it, then f0 — captured
// before anything is pumped, what DWM shows between the handler and its commit — then a frame every few
// ms while the queue is pumped, and the end once everything has settled.
//
// Tier 1 (Holds): with animations off f0 is the before or the end, and every frame once all that was
// asked has landed is the end — never a window up before its content (a frame of nothing), one gone
// before its replacement shows, a dim gone while the bars still wear edit mode's dress. Between the
// commit and its fence the windows in sight already show their new content, the window operations still
// to come (content never after its window, rule R3); tier 2 watches those frames — and, where the
// windows that come and go are clear while out of sight (§359: edit mode's chrome, a folder, a tooltip),
// tier 1 holds for those too (Holds(everyFrame: true)): every frame the before or the end.
// Tier 2 (StaysInSight): something in sight before and after is in sight in every frame — at least one
// of the places given shows what is drawn there, not the backdrop and not a window's empty black.
//
// Each film is saved as a strip (film-<name>.png: before, every frame that differs from the one
// before it, the end) with its numbers (film-<name>.txt), in KARPO_PNG_DIR.

using System.Diagnostics;
using System.Text;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

internal static unsafe class FrameFilm
{
    private const uint PW_RENDERFULLCONTENT = 2;

    /// <summary>What nothing draws: where no window shows anything.</summary>
    private static readonly (byte B, byte G, byte R) Backdrop = (160, 64, 128);

    /// <summary>How far apart two pixels may be and still be the same, in any channel.</summary>
    private const int Near = 24;

    /// <summary>What the screen showed of the filmed windows at one moment, in <see cref="Film.Area"/>.</summary>
    internal sealed record Shot(byte[] Pixels, double At, string Note);

    internal sealed class Film(string name, RECT area)
    {
        public string Name => name;

        public RECT Area => area;

        public int Width => area.right - area.left;

        public int Height => area.bottom - area.top;

        public Shot Before { get; set; } = null!;

        /// <summary>f0 first: before anything was pumped.</summary>
        public List<Shot> Frames { get; } = [];

        public Shot End { get; set; } = null!;

        /// <summary>The places (screen px) a test looks at in every frame: what must stay in sight (<see cref="StaysInSight"/>).</summary>
        public RECT[] Places { get; set; } = [];

        /// <summary>How long the transition itself ran, in ms.</summary>
        public double ActMs { get; set; }

        /// <summary>When everything asked had landed, in ms from the start of the transition; null: never in the film.</summary>
        public double? SettledAt { get; set; }

        /// <summary>The share of pixels that differ between two shots.</summary>
        public double Differs(Shot a, Shot b)
        {
            var differ = 0;
            for (var at = 0; at < a.Pixels.Length; at += 4)
            {
                if (Math.Abs(a.Pixels[at] - b.Pixels[at]) > Near || Math.Abs(a.Pixels[at + 1] - b.Pixels[at + 1]) > Near || Math.Abs(a.Pixels[at + 2] - b.Pixels[at + 2]) > Near)
                {
                    differ++;
                }
            }
            return differ / (double)(a.Pixels.Length / 4);
        }

        /// <summary>The share of <paramref name="rect"/> (screen px) that shows something drawn: neither the backdrop nor a window's empty black.</summary>
        public double Shows(Shot shot, RECT rect)
        {
            var (shown, all) = (0, 0);
            for (var y = Math.Max(rect.top, area.top); y < Math.Min(rect.bottom, area.bottom); y++)
            {
                for (var x = Math.Max(rect.left, area.left); x < Math.Min(rect.right, area.right); x++)
                {
                    var at = ((y - area.top) * Width + x - area.left) * 4;
                    var (b, g, r) = (shot.Pixels[at], shot.Pixels[at + 1], shot.Pixels[at + 2]);
                    all++;
                    var backdrop = Math.Abs(b - Backdrop.B) <= Near && Math.Abs(g - Backdrop.G) <= Near && Math.Abs(r - Backdrop.R) <= Near;
                    var black = b <= 4 && g <= 4 && r <= 4;
                    shown += backdrop || black ? 0 : 1;
                }
            }
            return all == 0 ? 0 : shown / (double)all;
        }

        /// <summary>The share of the area that is a window's empty black: shown with nothing drawn in it.</summary>
        public double Empty(Shot shot)
        {
            var black = 0;
            for (var at = 0; at < shot.Pixels.Length; at += 4)
            {
                black += shot.Pixels[at] <= 4 && shot.Pixels[at + 1] <= 4 && shot.Pixels[at + 2] <= 4 ? 1 : 0;
            }
            return black / (double)(shot.Pixels.Length / 4);
        }

        /// <summary>
        /// Tier 1: every frame is the before or the end (within <paramref name="tolerance"/> of the
        /// pixels), f0 first; the frames that are neither, as a sentence each. Animations off.
        /// <paramref name="everyFrame"/> (§359): the frames between the commit and its fence too.
        /// </summary>
        public List<string> Between(double tolerance = 0.01, bool everyFrame = false)
        {
            var wrong = new List<string>();
            foreach (var (shot, index) in Frames.Select((shot, index) => (shot, index)))
            {
                var (before, end) = (Differs(shot, Before), Differs(shot, End));
                // Between the commit and its fence (waiting, after f0) the windows already in sight show
                // their new content and the window operations are still to come: content never after its
                // window, which is all the fence promises (R3). Those frames are left to tier 2.
                if (index > 0 && shot.Note == "waiting" && !everyFrame)
                {
                    continue;
                }
                if (shot.Note == "landed" ? end > tolerance : before > tolerance && end > tolerance)
                {
                    wrong.Add($"f{index} at {shot.At:F1} ms ({shot.Note}) is neither: {before:P1} off the before, {end:P1} off the end, {Empty(shot):P0} empty");
                }
            }
            return wrong;
        }

        /// <summary>Tier 1 as an assertion, the before and the end told apart first so it says something.</summary>
        public void Holds(double tolerance = 0.01, bool everyFrame = false)
        {
            Assert.True(Differs(Before, End) > tolerance * 2, $"{name}: the before and the end are the same picture; the film shows nothing");
            var wrong = Between(tolerance, everyFrame);
            Assert.True(wrong.Count == 0, $"{name}: a frame between the before and the end:{Environment.NewLine}{string.Join(Environment.NewLine, wrong)}");
        }

        /// <summary>Tier 2: in every frame, at least <paramref name="least"/> of one of <paramref name="places"/> (screen px) shows something drawn; the frames where none does.</summary>
        public List<string> Gaps(double least, params RECT[] places)
        {
            var wrong = new List<string>();
            foreach (var (shot, index) in new[] { Before }.Concat(Frames).Append(End).Select((shot, index) => (shot, index)))
            {
                if (!places.Any(place => Shows(shot, place) >= least))
                {
                    wrong.Add($"frame {index} at {shot.At:F1} ms ({shot.Note}): {string.Join(", ", places.Select(place => Shows(shot, place).ToString("P0")))} shown");
                }
            }
            return wrong;
        }

        public void StaysInSight(double least, params RECT[] places)
        {
            var wrong = Gaps(least, places);
            Assert.True(wrong.Count == 0, $"{name}: out of sight for a moment:{Environment.NewLine}{string.Join(Environment.NewLine, wrong)}");
        }

        /// <summary>
        /// A part changing one way (a hover's plate fading in or out): over <paramref name="rect"/> (screen px) every frame on
        /// the straight way from the before to the end — a fade, or the one or the other —, never off it (another colour, a
        /// plate of another size or place, something shown and taken away), and never back towards the before once it has
        /// left it. The frames that are not, as a sentence each; <paramref name="steps"/> each frame's share of the way.
        /// </summary>
        public List<string> OffTheWay(RECT rect, out string steps)
        {
            var whole = Distance(Before, End, rect);
            var (wrong, shares, furthest) = (new List<string>(), new StringBuilder(), 0.0);
            foreach (var (shot, index) in Frames.Select((shot, index) => (shot, index)))
            {
                var (from, to) = (Distance(Before, shot, rect), Distance(shot, End, rect));
                var along = whole <= 0 ? 0 : from / whole;
                // On the way, from the before plus to the end is the whole way; off it, more.
                var aside = (from + to - whole) / Math.Max(whole, 1.0);
                shares.Append($" {along:F2}{(aside > 0.25 ? "!" : "")}");
                if (aside > 0.25 && from + to - whole > 1.0)
                {
                    wrong.Add($"f{index} at {shot.At:F0} ms is off the way: {from:F1} from the before, {to:F1} from the end, the whole way {whole:F1}");
                }
                else if (along < furthest - 0.2)
                {
                    wrong.Add($"f{index} at {shot.At:F0} ms goes back: {along:P0} of the way, after {furthest:P0}");
                }
                furthest = Math.Max(furthest, along);
            }
            steps = $"the whole way {whole:F1};{shares}";
            return wrong;
        }

        /// <summary>How far apart <paramref name="a"/> and <paramref name="b"/> are over <paramref name="rect"/> (screen px): every channel's difference, summed, a pixel's mean.</summary>
        public double Distance(Shot a, Shot b, RECT rect)
        {
            var (sum, all) = (0L, 0);
            for (var y = Math.Max(rect.top, area.top); y < Math.Min(rect.bottom, area.bottom); y++)
            {
                for (var x = Math.Max(rect.left, area.left); x < Math.Min(rect.right, area.right); x++)
                {
                    var at = (((y - area.top) * Width) + x - area.left) * 4;
                    all++;
                    sum += Math.Abs(a.Pixels[at] - b.Pixels[at]) + Math.Abs(a.Pixels[at + 1] - b.Pixels[at + 1]) + Math.Abs(a.Pixels[at + 2] - b.Pixels[at + 2]);
                }
            }
            return all == 0 ? 0 : sum / (double)all;
        }

        /// <summary>
        /// §705: <see cref="Distance"/> as the frames would read over a black desktop: the backdrop read as black. The capture
        /// gives a see-through pixel already laid on black (its alpha lost), so a fading ground at a tenth of its strength reads
        /// as a dark pixel where the backdrop was — on the backdrop's colour that is nearly the whole way at once; on black it is a
        /// tenth of it, as the screen shows it. What a morph's way is measured by.
        /// </summary>
        public double DistanceOverBlack(Shot a, Shot b, RECT rect)
        {
            var (sum, all) = (0L, 0);
            for (var y = Math.Max(rect.top, area.top); y < Math.Min(rect.bottom, area.bottom); y++)
            {
                for (var x = Math.Max(rect.left, area.left); x < Math.Min(rect.right, area.right); x++)
                {
                    var at = (((y - area.top) * Width) + x - area.left) * 4;
                    all++;
                    var (fromA, fromB) = (OverBlack(a.Pixels, at), OverBlack(b.Pixels, at));
                    sum += Math.Abs(fromA.B - fromB.B) + Math.Abs(fromA.G - fromB.G) + Math.Abs(fromA.R - fromB.R);
                }
            }
            return all == 0 ? 0 : sum / (double)all;
        }

        private static (int B, int G, int R) OverBlack(byte[] pixels, int at) =>
            (pixels[at], pixels[at + 1], pixels[at + 2]) == Backdrop ? (0, 0, 0) : (pixels[at], pixels[at + 1], pixels[at + 2]);

        /// <summary>The numbers, one line a frame, as the probe wrote them.</summary>
        public string Report()
        {
            var text = new StringBuilder();
            text.AppendLine($"== {name}: the transition ran {ActMs:F1} ms; everything landed {(SettledAt is { } at ? $"at {at:F1} ms" : "never in the film")}; before vs end {Differs(Before, End):P1}");
            foreach (var (shot, index) in Frames.Select((shot, index) => (shot, index)))
            {
                text.AppendLine($"   f{index,2} t={shot.At,7:F1}ms vsEnd={Differs(shot, End),6:P1} vsBefore={Differs(shot, Before),6:P1} empty={Empty(shot),5:P0} {shot.Note}");
            }
            return text.ToString();
        }

        /// <summary>The strip and the numbers, to KARPO_PNG_DIR.</summary>
        public void Save()
        {
            var shown = new List<Shot> { Before };
            foreach (var shot in Frames)
            {
                if (Differs(shot, shown[^1]) > 0.001)
                {
                    shown.Add(shot);
                }
            }
            shown.Add(End);
            const int gap = 6;
            var width = (Width * shown.Count) + (gap * (shown.Count - 1));
            var canvas = new byte[width * Height * 4];
            for (var at = 0; at < canvas.Length; at += 4)
            {
                (canvas[at], canvas[at + 1], canvas[at + 2], canvas[at + 3]) = ((byte)255, (byte)255, (byte)255, (byte)255);
            }
            for (var index = 0; index < shown.Count; index++)
            {
                var x0 = index * (Width + gap);
                for (var y = 0; y < Height; y++)
                {
                    Buffer.BlockCopy(shown[index].Pixels, y * Width * 4, canvas, ((y * width) + x0) * 4, Width * 4);
                }
            }
            WindowShots.WritePng(canvas, width, Height, Path.Combine(Offscreen.Pictures, $"film-{name}.png"));
            File.WriteAllText(Path.Combine(Offscreen.Pictures, $"film-{name}.txt"), Report());
        }
    }

    /// <summary>
    /// <paramref name="act"/> filmed over <paramref name="area"/> (screen px): the windows
    /// <paramref name="windows"/> names at each moment, in a frame of the dock's as a handler runs it
    /// — or, <paramref name="unfenced"/>, as Karpo did before the fence. <paramref name="filmMs"/> of
    /// frames after it, then the end once everything has landed. <paramref name="pumped"/>: run after
    /// every pump, as the dock's timers that a test's window does not get would run (a folder's quick tick).
    /// </summary>
    internal static Film Shoot(string name, Gfx gfx, RECT area, Func<IEnumerable<HWND>> windows, Action act, bool unfenced = false, int filmMs = 300, Action? pumped = null)
    {
        var film = new Film(name, area);
        Settle();
        film.Before = Snap(area, windows(), 0, "before");
        var clock = Stopwatch.StartNew();
        var was = Surfaces.Unfenced;
        Surfaces.Unfenced = unfenced;
        try
        {
            using (Surfaces.Begin(gfx.Compositor))
            {
                act();
            }
            film.ActMs = clock.Elapsed.TotalMilliseconds;
            film.Frames.Add(Snap(area, windows(), film.ActMs, "f0, nothing pumped"));
            while (clock.ElapsedMilliseconds < film.ActMs + filmMs)
            {
                Pump();
                pumped?.Invoke();
                if (film.SettledAt is null && Surfaces.Settled)
                {
                    film.SettledAt = clock.Elapsed.TotalMilliseconds;
                }
                film.Frames.Add(Snap(area, windows(), clock.Elapsed.TotalMilliseconds, Surfaces.Settled ? "landed" : "waiting"));
                Thread.Sleep(2);
            }
            var until = Environment.TickCount64 + (long)Patience.Ceiling.TotalMilliseconds;
            while (!Surfaces.Settled && Environment.TickCount64 < until)
            {
                Pump();
                Thread.Sleep(5);
            }
            Settle();
            film.End = Snap(area, windows(), clock.Elapsed.TotalMilliseconds, "end");
        }
        finally
        {
            Surfaces.Unfenced = was;
        }
        film.Save();
        return film;
    }

    /// <summary>Every message waiting on this thread handled, once: the commit and what waits for it.</summary>
    internal static void Pump()
    {
        MSG msg;
        while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&msg);
            _ = DispatchMessageW(&msg);
        }
    }

    /// <summary>
    /// Pumped until everything asked has landed and the screen has caught up. On the thing itself, with a generous
    /// ceiling (Patience): a fence lands within a frame or its 250 ms limit, but a test thread starved by the whole
    /// suite starting at once (software rendering on every core) once took more than the 2 s this waited, and the
    /// plan was asserted before it had landed (§363, ThePlanGoesInOneBatchByStrata).
    /// </summary>
    internal static void Settle()
    {
        var until = Environment.TickCount64 + (long)Patience.Ceiling.TotalMilliseconds;
        do
        {
            WindowShots.Settle(60);
        }
        while (!Surfaces.Settled && Environment.TickCount64 < until);
    }

    /// <summary>The screen over <paramref name="area"/>: the backdrop, and every one of <paramref name="windows"/> in sight over it in its z-order.</summary>
    internal static Shot Snap(RECT area, IEnumerable<HWND> windows, double at, string note)
    {
        var width = area.right - area.left;
        var height = area.bottom - area.top;
        var canvas = new byte[width * height * 4];
        for (var spot = 0; spot < canvas.Length; spot += 4)
        {
            (canvas[spot], canvas[spot + 1], canvas[spot + 2], canvas[spot + 3]) = (Backdrop.B, Backdrop.G, Backdrop.R, (byte)255);
        }
        var wanted = windows.Where(window => window != HWND.NULL).ToHashSet();
        // Bottom first: the z-order top first, turned round.
        var order = new List<HWND>();
        for (var window = GetTopWindow(HWND.NULL); window != HWND.NULL; window = GetWindow(window, GW_HWNDNEXT))
        {
            if (wanted.Contains(window))
            {
                order.Add(window);
            }
        }
        order.Reverse();
        foreach (var window in order)
        {
            if (!IsWindowVisible(window) || Capture(window, area) is not var (pixels, shows))
            {
                continue;
            }
            var w = shows.right - shows.left;
            for (var y = Math.Max(shows.top, area.top); y < Math.Min(shows.bottom, area.bottom); y++)
            {
                for (var x = Math.Max(shows.left, area.left); x < Math.Min(shows.right, area.right); x++)
                {
                    // The capture gives what the window leaves clear as black, which on the screen shows
                    // what is under it (§355: a popup's window spans its display, nearly all of it clear):
                    // laid over the windows under it, black is nothing.
                    var from = (((y - shows.top) * w) + x - shows.left) * 4;
                    if (pixels[from] <= 4 && pixels[from + 1] <= 4 && pixels[from + 2] <= 4)
                    {
                        continue;
                    }
                    var to = (((y - area.top) * width) + x - area.left) * 4;
                    canvas[to] = pixels[from];
                    canvas[to + 1] = pixels[from + 1];
                    canvas[to + 2] = pixels[from + 2];
                }
            }
        }
        return new Shot(canvas, at, note);
    }

    /// <summary>
    /// What <paramref name="window"/> shows of <paramref name="area"/> (screen px) as DWM holds it, BGRA,
    /// and where that is: cut to its region, where it has one (DWM cuts it there; §355: a popup's window
    /// spans its display, its region where it stands) — the window captured whole, that part of it kept in the
    /// picture, the rest of it dropped.
    /// </summary>
    private static (byte[] Pixels, RECT Shows)? Capture(HWND window, RECT area)
    {
        RECT box;
        if (!GetWindowRect(window, &box) || box.right <= box.left || box.bottom <= box.top)
        {
            return null;
        }
        var shows = box;
        RECT cut;
        if (GetWindowRgnBox(window, &cut) is SIMPLEREGION or COMPLEXREGION)
        {
            shows = new RECT { left = box.left + cut.left, top = box.top + cut.top, right = box.left + cut.right, bottom = box.top + cut.bottom };
        }
        shows = new RECT
        {
            left = Math.Max(Math.Max(shows.left, box.left), area.left),
            top = Math.Max(Math.Max(shows.top, box.top), area.top),
            right = Math.Min(Math.Min(shows.right, box.right), area.right),
            bottom = Math.Min(Math.Min(shows.bottom, box.bottom), area.bottom),
        };
        if (shows.right <= shows.left || shows.bottom <= shows.top)
        {
            return null;
        }
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var screen = GetDC(HWND.NULL);
        var memory = CreateCompatibleDC(screen);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = BI.BI_RGB;
        void* bits = null;
        var dib = CreateDIBSection(memory, &info, DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        var old = SelectObject(memory, dib);
        try
        {
            _ = PrintWindow(window, memory, PW_RENDERFULLCONTENT);
            _ = GdiFlush();
            var (w, h) = (shows.right - shows.left, shows.bottom - shows.top);
            var pixels = new byte[w * h * 4];
            var all = new ReadOnlySpan<byte>(bits, width * height * 4);
            for (var y = 0; y < h; y++)
            {
                all.Slice((((shows.top - box.top + y) * width) + shows.left - box.left) * 4, w * 4).CopyTo(pixels.AsSpan(y * w * 4));
            }
            return (pixels, shows);
        }
        finally
        {
            _ = SelectObject(memory, old);
            _ = DeleteObject(dib);
            _ = DeleteDC(memory);
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }
}
