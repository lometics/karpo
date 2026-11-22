// power: one command of Windows' power menu as a line of a list (2026-10-11: "the power menu should
// actually be separate widgets which are just single buttons"): sleep, hibernate, shut down,
// restart, sign out or lock, a widget each, where they were six rows of one line (the power
// commands, QuickPageTile). The power preset is these buttons and nothing else.
//
// Its line is the list's own row of its glyph and its name (FolderFlyout.PaintListLine), as Task
// view's is: the glyphs and the names are the quick-link menu's (QuickLinks), so a command looks
// the same everywhere in Karpo. A click does the command at once, as a row of Windows' power menu
// does — Karpo asks nothing first, and Windows does its own asking where it wants to — but only
// once the folder has gone: the click asks the folder to close (IslandView.App, which the folder
// forwards), and the dock runs it after (Dock.FolderForward, Act), off the dock's thread, so a sleep
// that holds its caller until the PC wakes never holds the bar.
//
// Sleep and hibernate are in Windows' power menu only where it offers them (QuickLinkMachine): a
// button for one it does not offer says so, as a Snipping Tool line whose app is not installed does
// (ShortcutTile) — its glyph faint with the warning badge, its name in the faint ink, the reason in
// its tooltip — and its click does nothing.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class PowerButtonTile : CellIsland
{
    private QuickLinkMachine machine;

    public PowerButtonTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        command = Info.Power ?? throw new ArgumentOutOfRangeException(nameof(island), Info.Id, "is not a power button.");
        Configure(island, config);
    }

    private readonly PowerCommand command;

    /// <summary>Nothing it shows changes on a timer: what Windows' power menu offers is read as it is configured and woken.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>The quick link its click runs: the command's own in Windows' power menu.</summary>
    internal QuickLink Link => LinkOf(command);

    /// <summary>The quick link of <paramref name="command"/>.</summary>
    internal static QuickLink LinkOf(PowerCommand command) => command switch
    {
        PowerCommand.Sleep => QuickLink.Sleep,
        PowerCommand.Hibernate => QuickLink.Hibernate,
        PowerCommand.ShutDown => QuickLink.ShutDown,
        PowerCommand.Restart => QuickLink.Restart,
        PowerCommand.SignOut => QuickLink.SignOut,
        _ => QuickLink.Lock,
    };

    /// <summary>Why it does nothing on this PC — Windows' power menu does not offer it — or null where it does its command.</summary>
    internal string? Missing => Offered(command, machine) ? null : S.DockPowerNotOffered;

    /// <summary>Whether <paramref name="machine"/>'s power menu offers <paramref name="command"/>: sleep and hibernate where it shows them, the rest always.</summary>
    internal static bool Offered(PowerCommand command, QuickLinkMachine machine) => command switch
    {
        PowerCommand.Sleep => machine.Sleep,
        PowerCommand.Hibernate => machine.Hibernate,
        _ => true,
    };

    /// <summary>
    /// Tests only, on the thread that makes the tile: what this PC's power menu offers, instead of
    /// Windows' answer.
    /// </summary>
    [ThreadStatic]
    internal static Func<QuickLinkMachine>? MachineStand;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        machine = (MachineStand ?? QuickLinkMachine.Read)();
    }

    public override string? Tooltip(int index) => Missing is { } missing ? $"{Info.Title}\n{missing}" : Info.Title;

    /// <summary>
    /// Not yet: the folder closes first, as Windows' power menu goes before the PC sleeps or the
    /// session ends, and the dock does the command once it has gone (<see cref="Act"/>). Nothing at
    /// all for a command this PC's power menu does not offer.
    /// </summary>
    public override IslandAction Click(int index) =>
        Missing is null ? new IslandAction(View: IslandView.App) : default;

    /// <summary>What the click asked to be done once the folder has gone: the command, from the dock.</summary>
    internal void Act()
    {
        if (Missing is null)
        {
            Run(Link);
        }
    }

    /// <summary>A line of a list and nothing else: the list's own row, never an entry of a bar.</summary>
    protected override float Length(int index, in IslandStyle style) =>
        throw new NotSupportedException($"{Info.Id} has no length along a bar: it is a component, a list's own row.");

    /// <summary>A resume or a device change: what the power menu offers is asked again.</summary>
    protected override bool Sample(bool everything)
    {
        if (!everything)
        {
            return false;
        }
        var was = machine;
        machine = (MachineStand ?? QuickLinkMachine.Read)();
        return was != machine;
    }

    /// <summary>Its glyph in the middle of the cell: a list's line is the list's own row, which the folder draws.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var glyph = fit.Glyph();
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ShortcutTile.Glyph(dc, Gfx, Info.Glyph, Missing is not null, ink.Text, ink.Secondary, PopupPaint.Rect((width - glyph) / 2, (height - glyph) / 2, glyph, glyph));
    }

    // --- Running a command ------------------------------------------------------------------------

    /// <summary>
    /// What runs a power command, per thread: a test's, which writes it down — no test ever sleeps,
    /// shuts down, restarts, signs out or locks the PC it runs on; null for Windows' own (<see cref="Run"/>).
    /// </summary>
    [ThreadStatic]
    private static Action<QuickLink>? runner;

    internal static Action<QuickLink>? Runner
    {
        get => runner;
        set => runner = value;
    }

    /// <summary>
    /// Runs <paramref name="link"/> on the pool, a moment after the folder was asked to close: the
    /// very code the quick links run (QuickLinks.Power). A quick-settings page in a folder asks
    /// through here too (QuickPageTile), so there is one way a command is run from a folder.
    /// </summary>
    internal static void Run(QuickLink link)
    {
        if (runner is { } test)
        {
            test(link);
            return;
        }
        _ = Task.Delay(CloseFirstMs).ContinueWith(
            _ => Troubles.Guard($"Power: {link}", () => QuickLinks.Power(link)), // not-ui: a trouble's name
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>How long a power command waits for the folder to have closed: its fade.</summary>
    private const int CloseFirstMs = 150;
}
