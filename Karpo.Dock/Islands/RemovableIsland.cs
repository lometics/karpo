// The removable devices: a line of a list (RemovableIsland.Cards.cs), and what is plugged in now.
//
// The user: "there should also be the variant where it shows a list of
// removable devices as flyouts". It was a button on the bar whose flyout listed
// them, until the button became the removable-devices preset (2026-10-10,
// docs\widgets.md §91): a folder wearing the stick as the button did
// (RemovableFaceDrawing) and holding this line, which shows what the flyout
// showed — the devices themselves, each with what it holds and its Eject.
//
// What counts as removable and the face's hover are here, so the line and the
// face read the same devices and say the same of them.

using Karpo.Core;
using Karpo.Dock.Drives;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe partial class RemovableIsland : CellIsland
{
    /// <summary>Segoe Fluent Icons: USB, a stick — which is what most removable devices are.</summary>
    internal const string UsbGlyph = "";

    /// <summary>The most devices the hover names; past them it says there are more.</summary>
    private const int MostNamed = 5;

    private readonly List<Drive> devices = [];

    public RemovableIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>
    /// A minute. A device arriving sends a message the dock already listens for
    /// (WM_DEVICECHANGE), so the tick is only a safety net for what Windows doesn't announce —
    /// a card slipped into a reader.
    /// </summary>
    public override int IntervalMs => 60_000;

    /// <summary>One: the line.</summary>
    protected override int Count => 1;

    /// <summary>The devices plugged in now, which the line lists.</summary>
    public IReadOnlyList<Drive> Devices => devices;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Read();
        Forget();
    }

    public override string? Tooltip(int index) => Tip(devices);

    /// <summary>
    /// What hovering the removable devices says — the face's, as the button's was: how many and
    /// their names, or that nothing is plugged in.
    /// </summary>
    internal static string Tip(IReadOnlyList<Drive> devices)
    {
        if (devices.Count == 0)
        {
            return $"{S.WidgetRemovableTitle}\n{S.WidgetRemovableNothing}";
        }
        var names = string.Join("\n", devices.Take(MostNamed).Select(device => device.Title));
        var head = S.WidgetRemovableCount(devices.Count);
        return devices.Count > MostNamed ? $"{head}\n{names}\n…" : $"{head}\n{names}";
    }

    /// <summary>
    /// A press on a device's row or its eject button is a control of the line's own and acts there
    /// (<see cref="Act"/>); anywhere else on the line does nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        return index >= OpenTarget ? Act(index) : default;
    }

    /// <summary>
    /// Lists what is plugged in. A refresh reads again only when a device came or went since the
    /// last reading (the dock forgets the list when Windows says so, DriveWatch.Changes): a line in
    /// a folder is refreshed whenever any widget of the folder has news, its own pointer included,
    /// and reading every drive's label for that would be waste. The minute's tick reads anyway, as
    /// the safety net.
    /// </summary>
    protected override bool Sample(bool everything)
    {
        if (!everything || DriveWatch.Changes != readAt)
        {
            Read();
        }
        return false;
    }

    // The DriveWatch.Changes the devices were last read at.
    private int readAt = -1;

    /// <summary>
    /// Never drawn on a bar: it is a line of a list and nothing else (<see cref="ListWidgets"/>);
    /// on the bar the removable devices are the preset's face (RemovableFaceDrawing).
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    private void Read()
    {
        readAt = DriveWatch.Changes;
        devices.Clear();
        devices.AddRange(Plugged());
    }

    /// <summary>
    /// What is plugged in now and counts as removable: media a person takes out — a stick, a card,
    /// an external disk Windows reports as removable, and an optical drive, which Explorer ejects
    /// the same way. A fixed disk in an external enclosure often reports itself fixed and is not
    /// listed; that is Windows' own answer, and a Drive widget shows it properly.
    /// </summary>
    internal static List<Drive> Plugged() =>
        [.. DriveWatch.Read(labels: true, remote: false).Where(drive => drive.Kind.IsRemovable())];

    /// <summary>A device's second line: what it holds, or why it has no figures.</summary>
    internal static string Detail(Drive device) =>
        device.Total == 0
            ? DriveListing.NotReadyLine(device.Kind)
            : S.WidgetRemovableFreeOf(free: DriveListing.FreeSpace(device.Total, device.Free), total: Units.Size(device.Total));

    /// <summary>The shell answered on its own thread: the line is drawn again with the picture.</summary>
    private void Announce() => Wake();
}
