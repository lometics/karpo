// What the to-do and the note lines share (docs\widgets.md §87): the notebook they read and change
// (Notebook.cs, one for both apps), woken when it changes anywhere — another line, the settings
// app, a hand edit — a caret that blinks while one of them is typed in, and the clicks counted
// into double and triple ones, which a folder's window does not count itself.

using Karpo.Core;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal abstract unsafe class NotebookTile : CellIsland
{
    /// <summary>The notebook every line reads: the person's (<see cref="Notebook.Shared"/>), or a test's.</summary>
    internal static Notebook? Stand { get; set; }

    private readonly Notebook book;
    private long lastPress;
    private System.Numerics.Vector2 lastPoint;
    private int clicks;

    protected NotebookTile(string key, WidgetItem island, Gfx gfx)
        : base(key, island, gfx)
    {
        book = Stand ?? Notebook.Shared;
        book.Watch();
        book.Changed += Heard;
    }

    protected Notebook Book => book;

    /// <summary>The caret blinks while it types; nothing ticks otherwise.</summary>
    public override int IntervalMs => CardTyping ? 500 : 0;

    protected override int Count => 1;

    public override string? Tooltip(int index) => null;

    /// <summary>A click on the line as a whole does nothing: every part of it is its own (CardPress).</summary>
    public override IslandAction Click(int index) => default;

    internal override void PaintCell(TerraFX.Interop.DirectX.ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line.");

    /// <summary>Its parts light themselves; the line does not light as a whole.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The notebook changed: the dock refreshes the line from its own thread.</summary>
    private void Heard() => Wake();

    /// <summary>A tick: the caret blinks.</summary>
    protected override bool Sample(bool everything)
    {
        if (!everything && CardTyping)
        {
            Blink();
        }
        return false;
    }

    /// <summary>The caret's blink, while it types.</summary>
    protected abstract void Blink();

    /// <summary>The folder closed: what is being typed is kept, and the line rests.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown && CardTyping)
        {
            CardStopTyping();
        }
    }

    public override void Dispose()
    {
        if (CardTyping)
        {
            CardStopTyping();
        }
        book.Changed -= Heard;
        base.Dispose();
    }

    /// <summary>Now, as the notebook writes it.</summary>
    protected static DateTimeOffset Now => new(Clock.Now());

    /// <summary>
    /// How many clicks a press at <paramref name="point"/> is: one, or two or three where it follows
    /// the last in Windows' double-click time and near it.
    /// </summary>
    protected int Clicks(System.Numerics.Vector2 point)
    {
        var now = Environment.TickCount64;
        var near = MathF.Abs(point.X - lastPoint.X) <= GetSystemMetrics(TerraFX.Interop.Windows.SM.SM_CXDOUBLECLK)
            && MathF.Abs(point.Y - lastPoint.Y) <= GetSystemMetrics(TerraFX.Interop.Windows.SM.SM_CYDOUBLECLK);
        clicks = near && now - lastPress <= GetDoubleClickTime() ? Math.Min(3, clicks + 1) : 1;
        lastPress = now;
        lastPoint = point;
        return clicks;
    }

    /// <summary>
    /// Opens <paramref name="link"/> — http, https or mailto, and nothing else (Markdown.Opens) — by
    /// the shell, off the bar's thread, as a link anywhere in Windows opens.
    /// </summary>
    internal static void Open(string link)
    {
        if (!Markdown.Opens(link))
        {
            return;
        }
        if (Opening is { } stand)
        {
            stand(link);
            return;
        }
        var thread = new Thread(() =>
        {
            try
            {
                _ = Launch.Open(link);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a note's link did not open: {error.Message}");
            }
        })
        { IsBackground = true, Name = "Karpo note link" }; // not-ui: a thread's name
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>For a test: what opens a link instead of the shell.</summary>
    internal static Action<string>? Opening { get; set; }
}
