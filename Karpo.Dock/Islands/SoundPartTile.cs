// The sound panel's regions as ordinary widgets (docs\widgets.md §9, the fourth conversion): the
// output devices, the microphones, the volume and the per-app mixer, each a line of a folder's list.
//
// **Four, and the volume is one of them.** It was going to be the volume slider (BarKind.Volume),
// which already stands alone on the bar. It cannot stand in a list: a list draws a bar
// kind as its own row of an icon and a name (Folders.CardInList), so the slider would have been a
// line reading "Volume slider" that nothing could drag. The page's own volume row is the region the
// panel has, drawn by the page's own controls, so that is what this is.
//
// **A retained-control page, re-housed.** The sound panel has no painters to split and no plan of
// rectangles — it is a quick-settings page whose every control paints itself — so what this tile
// shares with it is the controls and the placement, not a painter. The rows are SoundRows' rows:
// the same ListRow, Button, Slider and Label, told the same things by the same code, placed by the
// same arithmetic. So there is one drawing of a device row and one of a mix, and a change to either
// shows in the flyout and in the folder alike.
//
// **On the list's one gutter** (docs\widgets.md §9, "One gutter"), not the page's (its rows at 8,
// its words at 20): a heading's words start on it, and every row stands between the two, a chosen
// device's plate and its mark on the gutter rather than against the panel's border. What a row
// shows sits as far inside it as it does on the page — a device's icon, an app's icon and a
// summary 12 in, a level 12 from the row's end — so the devices and the apps under a heading line
// up with each other as they do in the flyout. The volume is one row of controls with no heading
// over it, so its button's box and its level's field stand on the gutters themselves.
//
// **The host is small because the control set is fixed.** A card has no QuickPanel: nothing here
// takes the keyboard, draws a focus ring or tests hits across a page. The folder routes the pointer
// to the card (CardPress, CardDrag, CardHover, CardWheel, CardTarget, all in the card's own px), and
// this hands each control the part of it that landed on the control, doing for the few controls it
// owns what the panel does for a page: it marks a control Pressed before handing it the press and
// clears it after the release, because the slider's anti-clobber guard reads exactly that
// (SoundRows.Show). The one thing a control takes from its host is a scale (Widget.Host), because a
// slider measures its thumb against one; that is the whole of what this reaches into the toolkit for.
//
// **The pickers became the lists.** The page's pickers open a dropdown drawn over the panel, which a
// card cannot have — there is no panel to draw over, and a card may not grow. So the devices are the
// component: every device a row, the one in use selected, a click on another making it Windows'
// default. The open-or-shut state is gone rather than moved.
//
// **Filling the room.** As many rows as the line holds, and the rest scroll under the heading
// (ListScroll.cs). The order is this widget's one contribution. Devices come with **the one in use
// first**, so the row that answers "what am I listening on" is the first one seen; apps come in the
// service's order — by name — with Windows' own sounds last, so a line too short for all of them
// shows the person's own apps first.
//
// **Separation without the folder's vocabulary.** A full-width hairline inside a card is how a list
// folder says "two widgets" (§9), so the rows here are kept apart the way the page keeps them: a
// heading above and two pixels of air between. Nothing draws edge to edge.
//
// **Every height a line is given**, in the lists' one design (ListTiers; docs\widgets.md §9, "A line
// at every height"). One row is one line: the device in use — its glyph in the list's icon column and
// its name, whole or short ("Speakers" for "Speakers (Realtek(R) Audio)") or cut while it still
// reads, and the microphones' mute at the end — one app's level, the first that is not Windows' own
// sounds, or the volume's own row. Where two of a list's rows fit at their most compact it is its
// rows, drawn closer, down to a compact row, before anything scrolls; its heading stands over them
// where it costs no row — where the rows under it, at the flyout's own height, are all the line would
// show without it. A list shows more of itself as it grows, never bigger type, and stands at the top
// of a line taller than it. The
// volume has nothing more to list: where the height holds them it says which output it sets, over
// the slider across the line, and with more the level stands large between them, as a reading's
// figure does (ListTiers.HeroPx).
//
// **The volume's line is one control.** It is the volume slider's list form (Partners), so the wheel
// anywhere on it steps the level, as it does anywhere over the slider on the bar; a mixer has a
// slider per app, and there the wheel is the slider's under it and nothing else's.
//
// **Beside its controls, the volume's line is its page's** (§605; the person, 2026-11-09: "the volume slider widget
// should similar as the bluetooth widget when i click anywhere it should go into the sound settings, except for the
// areas that are with other buttons"). Where it has its ">" (PageShown), a click anywhere but on the speaker's button
// and the slider is the ">"'s: the sound page in the folder's column, as a click beside the Bluetooth line's switch is
// its page (SwitchIsland.PageTarget). Under the pointer there, and pressed, the whole line lights as a switch's page
// part does; on the button or the slider, that control lights itself and the line does not.
//
// **The shared store runs from the dock's start** for every sound card and line the config names,
// wherever it stands (Sources\StoreData.cs, SoundData), so the devices and the apps are there when a
// folder opens. Making the tile starts nothing of its own; the first look — the folder opening, a
// measurement, a draw, a hover or a press — watches the store, to be woken by its news, and starts a
// test's own store (SoundParts.Made). The source a store reads is the samples' on any run that may
// not reach the person's world (SoundParts.Source) — a test or a smoke pass never reads, let alone
// sets, this PC's devices, levels or mutes.
//
// **The folder opening is the look that counts.** A list line is as tall as its content
// (IListContent), and the folder measures its lines the moment it opens, before anything is drawn.
// A source started by the first draw was started too late for that: the lines were measured with
// no devices, the panel opened short, and grew a frame later when the source read. So the region
// starts following when the folder shows it (Visible), and the first measurement is the devices
// and the apps there are. The store reads the source as it starts it (SoundStore.Watch), so there
// is nothing to wait for.
//
// **On the sound page the devices are dropdowns** (2026-10-16, the person: "the sound submenu of the
// quick settings should show the input and output device selectable from a dropdown"). An output or
// an input line on a page (Karpo.Core\FolderPages.cs) is WinUI's ComboBox with its header over it,
// "Output" and "Input": the device in use in the field and the chevron at its end. A click, Enter or
// Space opens the devices as a menu under the field, the one in use ticked, over the folder, which
// stays open behind it (IslandView.Choices, Dock.Folders.cs). A pick makes it Windows' default for
// every role, as a row's click does. A PC with none says so in the field, greyed.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal enum SoundPart
{
    /// <summary>The output devices, the one in use selected.</summary>
    Output,

    /// <summary>The microphones, with their mute beside the heading.</summary>
    Input,

    /// <summary>Each app's own level and mute.</summary>
    Mixer,

    /// <summary>The output's own level and mute: the panel's volume row.</summary>
    Volume,
}

/// <summary>
/// Where a sound component's service comes from — Windows' own, or the samples on a run that may not
/// reach the person's world. The guarantee lives here, in the thing being reached, rather than in the
/// widget or in any of the paths that draw it (docs\widgets.md §11): every offscreen picture and
/// every smoke pass draws these cards, and a component that set this PC's default device in a test
/// would be setting it for real.
/// </summary>
internal static class SoundParts
{
    public static ISoundSource Source() =>
        Made?.Invoke() ?? (Config.IsIsolated || QuickSources.UseSamples ? new SampleSound() : new WindowsSound());

    /// <summary>A test's own source, per thread, so a test can hand a component any machine it likes.</summary>
    [ThreadStatic]
    private static Func<ISoundSource>? made;

    internal static Func<ISoundSource>? Made
    {
        get => made;
        set => made = value;
    }
}

internal sealed unsafe class SoundPartTile : CellIsland, IListContent, IListScroll
{
    private readonly SoundPart part;
    /// <summary>
    /// The source this region reads, shared with every other sound region in the dock
    /// (SoundStore) — or, where a test handed this one a machine of its own, a store of its own.
    /// </summary>
    private readonly SoundStore store;
    private readonly Action waken;
    private bool following;

    /// <summary>What the store last read: every region drawn from it says the same.</summary>
    private SoundState state => store.State;

    // The controls, one per device or app, kept across paints so a slider keeps its grab and its
    // Pressed through a drag, and a row keeps its Hot while the pointer rests on it.
    private readonly Label heading;
    private readonly Label empty = new("", LabelStyle.Caption);
    private readonly Button micMute = new("", ButtonStyle.Subtle) { Name = S.WidgetSoundMuteMicrophone };
    private readonly Button mute = new("", ButtonStyle.Subtle) { Name = S.WidgetSoundMute };
    private readonly Slider volume = new() { Name = S.WidgetSoundVolume };
    // The level against the trailing gutter, as every row's reading ends (§401).
    private readonly Label value = new("", LabelStyle.Body) { Trailing = true };

    /// <summary>
    /// The volume's ">" in an open folder, Windows' own at the slider's end: the output devices and the
    /// mixer as a page in the folder's column (Karpo.Core\FolderPages.cs). Its click is the line's
    /// <see cref="IslandView.Page"/>.
    /// </summary>
    private readonly Button pageButton = new("", ButtonStyle.Subtle, SwitchIsland.ChevronGlyph) { Name = S.DockPageOutputs };
    private bool pageAsked;

    /// <summary>
    /// The page's dropdown, where this output or input line stands on a page: its header and its field
    /// (see the note at the top). Placed only there.
    /// </summary>
    private readonly bool picker;
    private readonly Label fieldHead = new("", LabelStyle.Body);
    private readonly ComboField combo;
    private bool choicesAsked;
    private readonly Keyed<ListRow> rows = new();
    private readonly Keyed<Button> icons = new();
    private readonly Keyed<Slider> sliders = new();
    private readonly Keyed<Label> numbers = new();

    /// <summary>What the last layout placed, in the card's own px; the pointer reads this.</summary>
    private readonly List<Widget> placed = [];

    /// <summary>tests: what the last layout placed, a control's target its place one up (<see cref="CardTarget"/>).</summary>
    internal IReadOnlyList<Widget> LinePlaced => placed;

    private Widget? hot;
    private Widget? held;
    private Vector2 heldAt;

    // §605: the pointer on the volume's line beside its controls (its page's), and a press down there (RowPress's 1).
    private bool pageHot;
    private RowPress press;

    /// <summary>tests: whether the volume's line lights whole, the pointer beside its controls (§605).</summary>
    internal bool PageLit => pageHot;

    // The box the held slider was pressed in: the drag reads the pointer against it.
    private D2D_RECT_F heldBox;

    /// <summary>
    /// What the last layout draws itself rather than placing as a control: the glyph and the name of
    /// the one-row device line, the name of the output the volume sets, and the volume's level at its
    /// largest. In the card's own px.
    /// </summary>
    private readonly List<Drawn> drawn = [];

    /// <summary>
    /// A glyph or a line of words the layout draws itself (<see cref="Words"/>); <paramref name="Hidden"/>
    /// in the greyed ink of a device hidden at rest (§159).
    /// </summary>
    internal readonly record struct Drawn(D2D_RECT_F Box, string Text, float Px, Weight Weight, bool Glyph, bool Quiet, bool Hidden = false);

    /// <summary>
    /// edit: each device the last layout showed and where, in the card's own px: what a click in edit
    /// mode hides or shows again (<see cref="CardEditTarget"/>), one up.
    /// </summary>
    private readonly List<(D2D_RECT_F Box, string Id)> shownDevices = [];

    public SoundPartTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        part = ListWidgets.Find(island.Info?.Id)?.Kind switch
        {
            ListKind.SoundInput => SoundPart.Input,
            ListKind.SoundMixer => SoundPart.Mixer,
            ListKind.SoundVolume => SoundPart.Volume,
            _ => SoundPart.Output,
        };
        heading = new Label(HeadingOf(part), LabelStyle.Strong) { Name = HeadingOf(part) };
        picker = part is SoundPart.Output or SoundPart.Input && key.Contains("/page/", StringComparison.OrdinalIgnoreCase);
        combo = new ComboField { Name = HeadingOf(part) };
        combo.Clicked = () => choicesAsked = true;
        // Not started here: the shared store is followed from the dock's start (SoundData), a test's
        // own from the first look (Follow).
        store = SoundParts.Made is { } made ? new SoundStore(made) : SoundStore.Shared;
        waken = () => _ = Wake();
        micMute.Clicked = () =>
        {
            store.Source.SetInputMuted(!state.InputMuted);
            store.Changes.Add(SoundNews.Devices);
        };
        // The volume row's controls do here what they did in the flyout (SoundPage's constructor,
        // until it went), tick included: it is the same control, settling the same way.
        mute.Clicked = () =>
        {
            store.Source.SetMuted(!state.Muted);
            store.Changes.Add(SoundNews.Volume);
        };
        volume.Changed = level =>
        {
            store.Source.SetVolume(level / 100f);
            value.Text = SoundRows.Number(level);
        };
        volume.Settled = level => Feedback.Volume(level, state.Muted);
        volume.Stepped = level => Feedback.Notch(level, state.Muted);
        Configure(island, config);
    }

    /// <summary>Nothing ticks: the service reports its own changes.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>
    /// The volume with no output: its row as it would be, the slider at half, greyed, and why
    /// (docs\widgets.md §247). The device lists still say there is none: a device made up to list
    /// would be one somebody could look for.
    /// </summary>
    internal override string? UnavailableOn(nint screen) =>
        (part == SoundPart.Volume && !state.Present) ? NotOnThisPc(Info.Title, S.WidgetSoundNoOutputDevice) : null;

    /// <summary>
    /// Which region of the panel this line is. Not <c>Part</c>: CellIsland already has one, and it
    /// is a slice of a cell being painted. Two meanings under one name on one object is how a call
    /// site gets the wrong one — and the compiler's hiding warning only shows in the AOT publish.
    /// </summary>
    internal SoundPart Region => part;

    /// <summary>What the source last said, for a test.</summary>
    internal SoundState State => state;

    // folders: its menu (Dock.WidgetMenus.cs) presses the region's own mute buttons — the volume
    // row's and the microphones' — which every region has made, whether or not it shows them.

    /// <summary>
    /// Whether the output and the microphone are muted now, which its menu ticks, and whether there
    /// is an output and a microphone to mute at all — the page says so in words where there is not.
    /// </summary>
    internal (bool Muted, bool MicrophoneMuted, bool Output, bool Microphone) Mutes
    {
        get
        {
            Follow();
            Take();
            return (state.Muted, state.InputMuted, state.Present, state.Microphones.Count > 0);
        }
    }

    /// <summary>The output muted or not, from its menu: the volume row's own button.</summary>
    internal void ToggleMute()
    {
        Follow();
        mute.Clicked?.Invoke();
        _ = Wake();
    }

    /// <summary>The microphone muted or not, from its menu: the microphones' own button.</summary>
    internal void ToggleMicrophone()
    {
        Follow();
        micMute.Clicked?.Invoke();
        _ = Wake();
    }

    /// <summary>
    /// The devices its menu names, for a region that lists them — the outputs or the microphones,
    /// the one in use first and ticked — so a line too short for them still chooses one. None for
    /// the mixer and the volume, and none where there is no device.
    /// </summary>
    internal IReadOnlyList<(string Id, string Name, bool InUse)> MenuDevices
    {
        get
        {
            Follow();
            Take();
            if (part is not (SoundPart.Output or SoundPart.Input) || !state.Present)
            {
                return [];
            }
            return [.. InUseFirst(Listed(part == SoundPart.Input ? state.Microphones : state.Outputs)).Select(device => (device.Id, device.Name, device.Default))];
        }
    }

    // --- Hidden devices (docs\widgets.md §159) --------------------------------------------------
    //
    // A person hides the devices they never use from an output or a microphone line, in edit mode,
    // where the line lists every device and a click on one hides it or shows it again: a hidden
    // one stands greyed, where it would stand, so it can be found to be shown again. At rest the
    // line lists only the rest, and measures, scrolls and fills by them; the menu names them too.
    // Hiding is the line's, not Windows': a hidden device may still be the one in use, and the
    // volume's line still says it is what it sets.

    /// <summary>Whether <paramref name="device"/> is hidden on this line.</summary>
    internal bool IsHidden(AudioOutput device) => Settings.HiddenDevices is { } hidden && Array.IndexOf(hidden, device.Id) >= 0;

    /// <summary>The devices the line lists: every one while arranged, else the ones not hidden.</summary>
    internal IReadOnlyList<AudioOutput> Listed(IReadOnlyList<AudioOutput> devices) =>
        Arranged || Settings.HiddenDevices is not { Length: > 0 } ? devices : [.. devices.Where(device => !IsHidden(device))];

    /// <summary>The devices of this line's part, as listed (<see cref="Listed"/>); none for the mixer and the volume.</summary>
    private IReadOnlyList<AudioOutput> Devices =>
        part switch
        {
            SoundPart.Input => Listed(state.Microphones),
            SoundPart.Output => Listed(state.Outputs),
            _ => [],
        };

    /// <summary>edit: the device under <paramref name="point"/> while arranged, one up; 0 elsewhere (§159).</summary>
    protected internal override int CardEditTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!Arranged || part is not (SoundPart.Output or SoundPart.Input))
        {
            return 0;
        }
        Follow();
        _ = Lay(box, style);
        var own = Own(point, box);
        for (var index = 0; index < shownDevices.Count; index++)
        {
            var at = shownDevices[index].Box;
            if (own.X >= at.left && own.X < at.right && own.Y >= at.top && own.Y < at.bottom)
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary>edit: the device clicked hidden, or shown again where it was hidden, in <paramref name="item"/>.</summary>
    protected internal override bool EditClick(int target, WidgetItem item)
    {
        if (target < 1 || target > shownDevices.Count)
        {
            return false;
        }
        var id = shownDevices[target - 1].Id;
        item.SetDeviceHidden(id, hidden: item.HiddenDevices is not { } hidden || Array.IndexOf(hidden, id) < 0);
        return true;
    }

    /// <summary>Whether this line is the page's dropdown (see the note at the top).</summary>
    internal bool Picker => picker;

    /// <summary>
    /// The dropdown's devices in Windows' order, each with whether it is the one in use: what its list
    /// shows, which keeps its order whichever is chosen. None where there is no device, or no dropdown.
    /// </summary>
    internal IReadOnlyList<(string Id, string Name, bool InUse)> Choices
    {
        get
        {
            Follow();
            Take();
            return picker && state.Present ? [.. Devices.Select(device => (device.Id, device.Name, device.Default))] : [];
        }
    }

    /// <summary>The dropdown's field, in the card's own px as the last layout placed it: what its list opens under. Null where there is none.</summary>
    internal D2D_RECT_F? ChoicesField => picker && placed.Contains(combo) ? combo.Bounds : null;

    /// <summary>The dropdown's header: "Output", "Input".</summary>
    internal static string PickerHeadOf(SoundPart part) => part == SoundPart.Input ? S.DockQuickSoundInput : S.DockQuickSoundOutput;

    /// <summary>What the field says where there is no device to choose.</summary>
    internal static string NoneOf(SoundPart part) => part == SoundPart.Input ? S.WidgetSoundNoInputDevices : S.WidgetSoundNoOutputDevice;

    /// <summary>A device chosen from its menu: Windows' default, as a click on its row makes it.</summary>
    internal void ChooseDevice(string id)
    {
        Follow();
        Choose(id);
        _ = Wake();
    }

    /// <summary>
    /// The apps its menu names, for the mixer — the person's own first, as the line lists them — each
    /// with whether it is muted, so a line too short for them still mutes one.
    /// </summary>
    internal IReadOnlyList<(string Key, string Name, bool Muted)> MenuApps
    {
        get
        {
            Follow();
            Take();
            return part == SoundPart.Mixer ? [.. OwnFirst(state.Apps).Select(app => (app.Key, app.Name, app.Muted))] : [];
        }
    }

    /// <summary>An app muted or not from its menu, as its icon's click in the mixer does it.</summary>
    internal void ToggleAppMute(string key)
    {
        Follow();
        if (state.Apps.FirstOrDefault(app => app.Key == key) is { } app)
        {
            store.Source.SetAppMuted(key, !app.Muted);
            store.Changes.Add(SoundNews.SessionChanged);
            _ = Wake();
        }
    }

    /// <summary>What the last layout placed, in the card's own px, in the order it is drawn.</summary>
    internal IReadOnlyList<Widget> Placed => placed;

    /// <summary>What the last layout draws itself, in the card's own px (<see cref="Drawn"/>).</summary>
    internal IReadOnlyList<Drawn> Words => drawn;

    /// <summary>
    /// Lays the card out in <paramref name="box"/> exactly as a paint would, without painting: the
    /// pointer's question and the drawing's, asked by a test.
    /// </summary>
    internal void LayOut(D2D_RECT_F box, in TileStyle style)
    {
        Follow();
        Take();
        _ = Lay(box, style);
    }

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>What each part's heading says: the panel's own names for them.</summary>
    internal static string HeadingOf(SoundPart part) => part switch
    {
        SoundPart.Input => S.WidgetSoundInputDevice,
        SoundPart.Mixer => S.WidgetSoundVolumeMixer,
        // A name, not a heading: the flyout's volume row has none above it, and neither does this.
        SoundPart.Volume => S.WidgetSoundVolume,
        _ => S.WidgetSoundOutputDevice,
    };

    /// <summary>
    /// The folder opening: the source starts now, before the folder measures its lines, so a line
    /// is measured with the devices and the apps there are rather than with none (see the note at
    /// the top). Going out of sight stops nothing: the last region to go stops the source.
    /// </summary>
    public override void Visible(bool shown)
    {
        if (shown)
        {
            Follow();
            Take();
        }
        else
        {
            pageHot = false;
            press.Up();
        }
    }

    /// <summary>
    /// list: the line this region needs to show everything it has (<see cref="IListContent"/>),
    /// from the same pitches the layout places it by. A list of devices or apps is its heading, a
    /// row each, and the air the page keeps under its body; with nothing to list, the heading and
    /// the one caption saying so. The volume is its size's form, as the brightness's line is (§432, SliderLine).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        Follow();
        Take();
        var context = ContextFor(style);
        if (picker)
        {
            return PickerHeight(context);
        }
        if (part == SoundPart.Volume)
        {
            // One height for both sliders, the brightness's line beside it (SliderLine).
            return SliderLine.Height(Gfx, context, style, LineSizes.Of(Settings));
        }
        var body = part switch
        {
            SoundPart.Mixer => state.Apps.Count == 0 ? context.T(28) : state.Apps.Count * MixPitch(context),
            _ => !state.Present || Devices.Count == 0 ? context.T(28) : Devices.Count * DevicePitch(context),
        };
        return HeadingBottom(context) + body + (ListsItems ? Tail(context) : context.D(8));
    }

    /// <summary>Whether the line lists devices, items (LineItem): not the mixer's apps, which are controls.</summary>
    private bool ListsItems => part is not SoundPart.Mixer and not SoundPart.Volume && state.Present && Devices.Count > 0;

    /// <summary>The room between two devices' rows, in their pitch: each row's band has half of it on either side.</summary>
    private static float DeviceGap(QuickContext context) => context.D(2);

    /// <summary>
    /// Past the devices' pitches, the room under the list: the last row's band's half of the gap and no more, so the
    /// line's foot is its band's (LineItem.Foot) — less than the gap the last pitch counted.
    /// </summary>
    private static float Tail(QuickContext context) => LineItem.Foot(DeviceGap(context)) - DeviceGap(context);

    /// <summary>
    /// Every row, where the line is a list of them and they do not all fit: under the heading where a
    /// row fits under it, else bare — and they scroll (ListScroll.cs). The volume does not scroll.
    /// </summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var context = ContextFor(style);
        var count = picker ? 0 : part switch
        {
            SoundPart.Volume => 0,
            SoundPart.Mixer => state.Apps.Count,
            _ => state.Present ? Devices.Count : 0,
        };
        if (count == 0 || !IsList(context, height))
        {
            return 0;
        }
        var pitch = part == SoundPart.Mixer ? MixPitch(context) : DevicePitch(context);
        return height + 0.5f >= HeadingBottom(context) + context.D(8) + pitch
            ? ListHeight(width, style)
            : part == SoundPart.Mixer ? (2 * context.D(4)) + (count * pitch) : count * pitch;
    }

    /// <summary>The heading stays while the rows scroll under it, where there is one.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) =>
        MathF.Abs(content - ListHeight(width, style)) < 0.5f ? HeadingBottom(ContextFor(style)) : 0;

    /// <summary>
    /// Watches the store the first time anybody looks at the card — the folder opening, a
    /// measurement, a draw, a hover or a press — to be woken by its news. The shared store has run
    /// since the dock's start (SoundData); a test's own starts here (§11).
    /// </summary>
    private void Follow()
    {
        if (following)
        {
            return;
        }
        following = true;
        // The first sound region to look starts the one source (SoundStore).
        store.Watch(waken);
    }

    /// <summary>The service posted: take what it says, and the folder redraws the card.</summary>
    protected override bool Sample(bool everything)
    {
        Take();
        return false;
    }

    /// <summary>What changed, read once for every region drawn from the store (SoundStore.Take).</summary>
    private void Take() => store.Take();

    public override string? Tooltip(int index) => null;

    public override void Dispose()
    {
        // The last region to go stops the source: nothing listens to Windows for nobody.
        if (following)
        {
            store.Unwatch(waken);
        }
        base.Dispose();
    }

    /// <summary>
    /// Never drawn on a bar: they are lines of a list and nothing else (<see cref="ListWidgets"/>), so
    /// <see cref="CellIsland.PaintOn"/> refuses a bar drawing before this could be reached.
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The layout ---------------------------------------------------------------------------

    /// <summary>The context the controls paint with: the card's own scales, and the folder's theme.</summary>
    private QuickContext ContextFor(in TileStyle style) =>
        new() { Gfx = Gfx, Theme = QuickTheme.For(style.Theme), Scale = style.Scale, TextScale = style.TextScale };

    /// <summary>The pitch of a device row: a list row's height and the page's two pixels of air.</summary>
    internal static float DevicePitch(QuickContext context) => context.T(40) + context.D(2);

    /// <summary>The pitch of a mix row, the same way.</summary>
    internal static float MixPitch(QuickContext context) => SoundRows.MixHeight(context) + context.D(2);

    /// <summary>WinUI's ComboBox: 32 high, taller with the text.</summary>
    internal static float FieldHeight(QuickContext context) => MathF.Max(context.D(32), context.T(32));

    /// <summary>
    /// The dropdown's line: 4 of air, its header a line of body text, WinUI's 8 under a header, the
    /// field, and 8 under it before the next line.
    /// </summary>
    internal static float PickerHeight(QuickContext context) => context.D(4) + context.T(20) + context.D(8) + FieldHeight(context) + context.D(8);

    /// <summary>The heading line: the page's own heading (PageLayout.Heading), 8 of air above it.</summary>
    internal static float HeadingBottom(QuickContext context) => context.D(8) + MathF.Max(context.T(32), context.D(36));

    /// <summary>
    /// The devices in the order the card lists them: the one in use first, then the rest as Windows
    /// gave them. Only the order is this card's — it is what the fill summarises the tail of, and the
    /// row that answers "which device am I on" must never be the tail.
    /// </summary>
    internal static List<AudioOutput> InUseFirst(IReadOnlyList<AudioOutput> devices) =>
        [.. devices.Where(device => device.Default), .. devices.Where(device => !device.Default)];

    /// <summary>
    /// The apps in the order the line lists them: the service's, with Windows' own sounds last — the
    /// person's own apps are what a line too short for all of them shows, one row included.
    /// </summary>
    internal static List<AudioApp> OwnFirst(IReadOnlyList<AudioApp> apps) =>
        [.. apps.Where(app => !app.System), .. apps.Where(app => app.System)];

    /// <summary>
    /// Lays the card out in <paramref name="box"/> (its own px, 0, 0 at its top-left): places every
    /// control it shows and remembers them for the pointer, and says what it draws itself. A layout
    /// rather than a painting, so the pointer asks the same question the drawing does. Returns the
    /// context its controls were placed for, which is what they are painted with.
    /// </summary>
    private QuickContext Lay(D2D_RECT_F box, in TileStyle style)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        // The list's gutter, on both sides: rows and words alike stand between the two (see the note
        // at the top).
        var gutter = ListForm.Gutter(style);
        placed.Clear();
        drawn.Clear();
        shownDevices.Clear();
        var context = ContextFor(style);
        if (picker)
        {
            LayPicker(context, width, gutter);
        }
        else if (part == SoundPart.Volume)
        {
            LayVolume(context, style, width, height);
        }
        else if (IsList(context, height))
        {
            // A list shows more of itself as it grows, never bigger type.
            LayList(context, width, height, gutter);
        }
        else
        {
            LayOne(context, style, width, height, gutter);
        }
        Settle();
        return context;
    }

    /// <summary>
    /// Whether a line <paramref name="height"/> px tall is a list of rows rather than one line: where
    /// two of its rows fit at their most compact (ListTiers' compact line). What fits decides, not
    /// how many rows the line is: at the largest text a line of two rows holds one line, not a list.
    /// </summary>
    private static bool IsList(QuickContext context, float height) =>
        height - (2 * context.D(4)) >= (2 * (context.T(32) + context.D(2))) - 0.5f;

    /// <summary>What a device is called on one row, fullest first: its name, and the name before what it is in brackets ("Speakers").</summary>
    internal static string[] NamesOf(string name)
    {
        var bracket = name.IndexOf(" (", StringComparison.Ordinal);
        return bracket > 0 ? [name, name[..bracket].TrimEnd()] : [name];
    }

    /// <summary>
    /// The layout is over: the keyed controls it did not ask for are let go, and so is a pointer
    /// held by a control that is no longer there.
    /// </summary>
    private void Settle()
    {
        rows.Done();
        icons.Done();
        sliders.Done();
        numbers.Done();
        // A control that went away mid-press — a device unplugged, an app that closed while its
        // slider was held — lets go of the pointer rather than holding it for nobody.
        if (held is not null && !placed.Contains(held))
        {
            held.Pressed = false;
            held = null;
        }
        if (hot is not null && !placed.Contains(hot))
        {
            hot = null;
        }
    }

    /// <summary>A caption where the line has nothing to list or to set, at <paramref name="top"/>.</summary>
    private void Say(QuickContext context, string words, float left, float right, float top)
    {
        empty.Text = words;
        empty.Bounds = PopupPaint.Rect(left, top, MathF.Max(1, right - left), context.T(28));
        placed.Add(empty);
    }

    /// <summary>
    /// One row of a list: the device in use — its glyph in the list's icon column and its name where
    /// a list's names begin, and the microphones' mute at the line's end — or one app's level, the
    /// first that is not Windows' own sounds where there is one. What a row this short leaves out, the
    /// other devices and apps, is the widget's right-click menu's.
    /// </summary>
    private void LayOne(QuickContext context, in TileStyle style, float width, float height, float gutter)
    {
        var form = ListForm.Of(Gfx, style);
        var middle = height / 2;
        var caption = PopupPaint.Round(middle - (context.T(28) / 2));
        if (part == SoundPart.Mixer)
        {
            if (state.Apps.Count == 0)
            {
                Say(context, S.WidgetSoundNoApps, form.TextLeft, width - gutter, caption);
                return;
            }
            var app = OwnFirst(state.Apps)[0];
            // Its level against the trailing gutter (§401); the page keeps it 12 in from the row's end.
            PlaceMix(context, app, SoundRows.MixIn(context, gutter + context.D(12), width - gutter + context.D(12), 0, height));
            return;
        }
        var devices = Devices;
        if (!state.Present || devices.Count == 0)
        {
            Say(context, part == SoundPart.Input ? S.WidgetSoundNoMicrophone : S.WidgetSoundNoOutputDevice, form.TextLeft, width - gutter, caption);
            return;
        }
        var device = InUseFirst(devices)[0];
        var hidden = IsHidden(device);
        shownDevices.Add((PopupPaint.Rect(gutter, 0, MathF.Max(1, width - (2 * gutter)), height), device.Id));
        var end = width - gutter;
        if (part == SoundPart.Input)
        {
            var buttons = Buttons(context);
            var size = MathF.Min(buttons.Height, height);
            PlaceMicMute(PopupPaint.Rect(end - size, PopupPaint.Round(middle - (size / 2)), size, size), buttons);
            end = micMute.Bounds.left - buttons.Gap;
        }
        drawn.Add(new Drawn(PopupPaint.Rect(gutter, PopupPaint.Round(middle - (form.Icon / 2)), form.Icon, form.Icon), device.Glyph, form.Glyph, Weight.Regular, Glyph: true, Quiet: false, hidden));
        // The reading wins its row (ListTiers): the name whole, else short, else cut while it reads.
        var room = MathF.Max(1, end - form.TextLeft);
        if (ListTiers.Name(Gfx, NamesOf(device.Name), form.TextPx, Weight.Regular, room) is { } name)
        {
            drawn.Add(new Drawn(PopupPaint.Rect(form.TextLeft, PopupPaint.Round(middle - (form.Line / 2)), room, form.Line), name, form.TextPx, Weight.Regular, Glyph: false, Quiet: false, hidden));
        }
    }

    /// <summary>
    /// The page's dropdown: its header, and under it the field from gutter to gutter with the device in
    /// use, or where there is none what says so, greyed (see the note at the top).
    /// </summary>
    private void LayPicker(QuickContext context, float width, float gutter)
    {
        var left = gutter;
        var right = MathF.Max(left + 1, width - gutter);
        var top = context.D(4);
        fieldHead.Text = PickerHeadOf(part);
        fieldHead.Bounds = PopupPaint.Rect(left, top, right - left, context.T(20));
        placed.Add(fieldHead);
        var devices = state.Present ? Devices : [];
        var shown = devices.Count > 0 ? InUseFirst(devices)[0] : null;
        combo.Text = shown?.Name ?? NoneOf(part);
        combo.Glyph = shown?.Glyph;
        combo.Enabled = shown is not null;
        combo.Bounds = PopupPaint.Rect(left, top + context.T(20) + context.D(8), right - left, FieldHeight(context));
        placed.Add(combo);
    }

    /// <summary>The lines' one button (LineButton) at the line's scale and text size.</summary>
    private LineButton Buttons(QuickContext context) => LineButton.Of(Gfx, context.Scale, context.TextScale);

    /// <summary>The microphones' mute, in <paramref name="at"/>: muted, it shows the struck-out microphone.</summary>
    private void PlaceMicMute(D2D_RECT_F at, in LineButton buttons)
    {
        buttons.Dress(micMute);
        micMute.Glyph = state.InputMuted ? SoundRows.MicMuted : SoundRows.Mic;
        micMute.Dimmed = state.InputMuted;
        micMute.Bounds = at;
        placed.Add(micMute);
    }

    /// <summary>
    /// Two rows and more of a list — devices, microphones or apps. As many rows as the line holds,
    /// drawn closer — down to a compact row — before any is left out (where it scrolls, the line is as
    /// tall as all of them). The heading, with the microphones' mute at its end, where it costs no
    /// row: where the rows under it, at the flyout's own height, are every row the line would show
    /// without it. Without a heading the microphones' mute stands at the end of the microphone in use.
    /// What is shown stands at the top of a line taller than it.
    /// </summary>
    private void LayList(QuickContext context, float width, float height, float gutter)
    {
        var rowLeft = gutter;
        var rowRight = MathF.Max(rowLeft + 1, width - gutter);
        var devices = Devices;
        var count = part == SoundPart.Mixer ? state.Apps.Count : state.Present ? devices.Count : 0;
        var standard = part == SoundPart.Mixer ? MixPitch(context) : DevicePitch(context);
        var compact = context.T(32) + context.D(2);
        var mic = part == SoundPart.Input && count > 0;
        // Devices are items: the first's band from the line's top (or the heading's foot), the last's to the line's
        // foot at the height they ask for, each as tall as the others (LineItem) — half the gap between two rows past
        // either end; the mixer's apps 4 DIP in from the line's edges and 8 under the heading.
        var items = part != SoundPart.Mixer && count > 0;
        var air = items ? Tail(context) : context.D(8);
        var edge = items ? LineItem.Foot(DeviceGap(context)) : context.D(4);

        // Without a heading: the rows as close as it takes to show what the room holds at a compact
        // row, and no closer.
        var bareRoom = MathF.Max(0, height - (2 * edge) + (items ? DeviceGap(context) : 0));
        var compactFill = WidgetFill.Of(count, compact, bareRoom);
        var slots = Math.Max(1, compactFill.Shown);
        var barePitch = Math.Clamp(MathF.Floor(bareRoom / slots), compact, standard);
        var bare = WidgetFill.Of(count, barePitch, bareRoom);
        // Under a heading: the flyout's own.
        var headedRoom = MathF.Max(0, height - HeadingBottom(context) - air);
        var underHeading = WidgetFill.Of(count, standard, headedRoom);
        var headed = count == 0
            ? headedRoom >= context.T(28) - 0.5f
            : underHeading.Shown >= 1 && underHeading.Shown >= bare.Shown;
        var fill = headed ? underHeading : bare;
        var pitch = headed ? standard : barePitch;
        var body = count == 0 ? context.T(28) : fill.Height;
        // From the top: a list shorter than its line stands at its top, never in its middle.
        var shift = 0f;
        var top = shift + (headed ? HeadingBottom(context) : edge);

        if (headed)
        {
            // The heading, where the page puts a heading; the microphone's mute at its end, because the
            // mute is a control of the microphones rather than a subject of its own.
            var headingTop = shift + context.D(8);
            var line = MathF.Max(context.T(32), context.D(36));
            var buttons = Buttons(context);
            var muteRoom = mic ? buttons.Height + buttons.Gap : 0;
            heading.Text = HeadingOf(part);
            heading.Bounds = PopupPaint.Rect(rowLeft, headingTop, MathF.Max(1, rowRight - rowLeft - muteRoom), line);
            placed.Add(heading);
            if (mic)
            {
                var size = buttons.Height;
                PlaceMicMute(PopupPaint.Rect(rowRight - size, headingTop + PopupPaint.Round((line - size) / 2), size, size), buttons);
            }
        }
        if (count == 0)
        {
            Say(context, part switch
            {
                SoundPart.Mixer => S.WidgetSoundNoApps,
                SoundPart.Input => S.WidgetSoundNoMicrophone,
                _ => S.WidgetSoundNoOutputDevice,
            }, rowLeft, rowRight, top);
            return;
        }

        var rowHeight = pitch - context.D(2);
        if (part == SoundPart.Mixer)
        {
            LayMixer(context, top, fill, pitch, rowHeight, rowLeft, rowRight);
        }
        else
        {
            // A device's row on the list's own columns: its glyph on the gutter (ListRow, §388).
            LayDevices(context, devices, top, fill, pitch, rowHeight, width, rowLeft, rowRight, headed, mic && !headed);
        }
    }

    /// <summary>
    /// The volume, in the fullest of its three designs that fits the line (ListTiers). Where the height
    /// holds them, the button and the output it sets at the top, the level large in the middle and the
    /// slider at the bottom, spread over the line; else the button, that output's name and the level on
    /// one row with the slider under them from the text edge to the gutter; else the flyout's own row, its button's
    /// box and its level's field on the gutters. A PC with no output shows the row at half, greyed
    /// (<see cref="UnavailableOn"/>).
    /// </summary>
    private void LayVolume(QuickContext context, in TileStyle style, float width, float height)
    {
        volume.Host = context;
        // No output: the row at half, which the folder greys (UnavailableOn).
        if (state.Present)
        {
            SoundRows.Master(mute, volume, value, state.Volume, state.Muted);
        }
        else
        {
            SoundRows.Master(mute, volume, value, VolumeIsland.UnavailableLevel / 100f, muted: false);
        }
        // The brightness's line asks the same, so the two sliders are one design (SliderLine).
        // The ">", where the line has a page: the last control of the speaker's row, the row's level and words before it (SliderLine).
        var line = SliderLine.Lay(Gfx, context, style, width, height, PageShown);
        if (PageShown)
        {
            pageButton.Clicked ??= () => pageAsked = true;
            // The lines' one button, its ">" the switches' own size so the chevrons of a folder line up.
            Buttons(context).Dress(pageButton);
            pageButton.GlyphSize = style.Px(SwitchIsland.ChevronGlyphDip);
            pageButton.Bounds = line.Page;
            placed.Add(pageButton);
        }
        mute.Bounds = line.Button;
        mute.GlyphSize = line.GlyphPx;
        volume.Bounds = line.Slider;
        placed.Add(mute);
        placed.Add(volume);
        if (line.Form == SliderLineForm.Tall)
        {
            drawn.Add(new Drawn(line.Big, value.Text, line.BigPx, Weight.Semibold, Glyph: false, Quiet: false));
        }
        else
        {
            value.Bounds = line.Number;
            placed.Add(value);
        }
        if (SoundState.Chosen(state.Outputs)?.Name is { Length: > 0 } output && TilePaint.Width(line.Name) > 1)
        {
            drawn.Add(new Drawn(line.Name, output, context.TextPx, Weight.Regular, Glyph: false, Quiet: true));
        }
    }

    /// <summary>
    /// The devices' rows from <paramref name="top"/>, <paramref name="pitch"/> apart: each an item (LineItem, §428),
    /// its band the line's whole <paramref name="width"/> and half the room to the rows over and under it, its glyph on
    /// the gutter (<paramref name="rowLeft"/>); the microphones' mute, where it stands at the end of the one in use,
    /// over its row.
    /// </summary>
    private void LayDevices(QuickContext context, IReadOnlyList<AudioOutput> devices, float top, WidgetFill fill, float pitch, float rowHeight, float width, float rowLeft, float rowRight, bool headed, bool muteBeside)
    {
        var ordered = InUseFirst(devices);
        var y = top;
        for (var index = 0; index < fill.Shown; index++)
        {
            var device = ordered[index];
            var row = rows.Get(device.Id, () => new ListRow { Name = device.Name });
            SoundRows.Device(row, device, Choose);
            // edit: a hidden device greyed where it stands (§159); it is listed only while arranged.
            row.Enabled = !IsHidden(device);
            // Half the gap over it and under it; the first from the heading's foot, or the line's top (LineItem).
            var above = index > 0 ? PopupPaint.Round(y - ((pitch - rowHeight) / 2)) : headed ? y : 0;
            var below = PopupPaint.Round(y + rowHeight + ((pitch - rowHeight) / 2));
            row.Lead = rowLeft;
            row.Over = y - above;
            row.Under = below - (y + rowHeight);
            row.Control = null;
            row.Bounds = new D2D_RECT_F(0, above, width, below);
            placed.Add(row);
            shownDevices.Add((row.Bounds, device.Id));
            if (index == 0 && muteBeside)
            {
                // No heading to stand on: the microphones' mute at the end of the one in use, over its row, its words short of it.
                var buttons = Buttons(context);
                var size = MathF.Min(buttons.Height, rowHeight);
                PlaceMicMute(PopupPaint.Rect(rowRight - size, y + PopupPaint.Round((rowHeight - size) / 2), size, size), buttons);
                row.Control = micMute;
            }
            y += pitch;
        }
    }

    private void LayMixer(QuickContext context, float top, WidgetFill fill, float pitch, float rowHeight, float textLeft, float rowRight)
    {
        var y = top;
        var apps = OwnFirst(state.Apps);
        for (var index = 0; index < fill.Shown; index++)
        {
            // A mix's icon stands where a device's does, 12 inside the row, and its level against the trailing
            // gutter, as every row's reading ends (§401): the page keeps it 12 in from the row's end.
            PlaceMix(context, apps[index], SoundRows.MixIn(context, textLeft + context.D(12), rowRight + context.D(12), y, rowHeight));
            y += pitch;
        }
    }

    /// <summary>One app's mix in <paramref name="boxes"/>: its icon that mutes it, its slider and its level.</summary>
    private void PlaceMix(QuickContext context, AudioApp app, SoundRows.MixBoxes boxes)
    {
        var icon = icons.Get(app.Key, () => new Button("", ButtonStyle.Subtle) { Name = S.WidgetSoundMuteApp(app.Name) });
        var slider = sliders.Get(app.Key, () => new Slider { Name = app.Name });
        var number = numbers.Get(app.Key, () => new Label("", LabelStyle.Caption) { Trailing = true });
        // The slider measures its thumb against this; a card has no panel to lend it one.
        slider.Host = context;
        SoundRows.App(app, icon, slider, number, store.Source, store.Changes, context);
        // The icon that mutes it: the lines' one glyph button (LineButton), where the page's stands.
        var buttons = Buttons(context);
        buttons.Dress(icon);
        var side = MathF.Min(buttons.Height, TilePaint.Height(boxes.Icon));
        icon.Bounds = PopupPaint.Rect(boxes.Icon.left + PopupPaint.Round((TilePaint.Width(boxes.Icon) - side) / 2),
            boxes.Icon.top + PopupPaint.Round((TilePaint.Height(boxes.Icon) - side) / 2), side, side);

        slider.Bounds = boxes.Slider;
        SoundRows.Show(slider, number, app.Volume);
        number.Bounds = boxes.Number;
        placed.Add(icon);
        placed.Add(slider);
        placed.Add(number);
    }

    /// <summary>A device chosen: Windows' default for every role, as its own picker sets it.</summary>
    private void Choose(string id)
    {
        if (part == SoundPart.Input)
        {
            store.Source.SetDefaultInput(id);
        }
        else
        {
            store.Source.SetDefault(id);
        }
        store.Changes.Add(SoundNews.Devices);
    }

    // --- The drawing --------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Follow();
        Take();
        var context = Lay(box, style);
        if (press.Held == 1 || (press.Held == 0 && pageHot))
        {
            // The page's part is the whole line but its controls (§605): lit as a switch's page part is, edge to edge.
            PopupPaint.Fill(dc, box, 0, press.Held == 1 ? context.Theme.SubtlePressed : context.Theme.SubtleHover);
        }
        foreach (var control in placed)
        {
            var at = control.Bounds;
            TilePaint.Placed(dc, box.left + at.left, box.top + at.top, 1, () => control.Paint(dc, context));
        }
        foreach (var each in drawn)
        {
            var ink = each.Hidden ? context.Theme.TextDisabled : each.Quiet ? context.Theme.TextSecondary : context.Theme.Text;
            var at = PopupPaint.Rect(box.left + each.Box.left, box.top + each.Box.top, TilePaint.Width(each.Box), TilePaint.Height(each.Box));
            if (each.Glyph)
            {
                PopupPaint.Glyph(dc, Gfx, each.Text, each.Px, ink, at);
            }
            else
            {
                PopupPaint.Text(dc, Gfx, each.Text, each.Px, each.Weight, ink, at.left, at.top, TilePaint.Width(at));
            }
        }
    }

    // --- The pointer --------------------------------------------------------------------------

    /// <summary>The control under <paramref name="point"/> that answers a pointer, or null.</summary>
    private Widget? Under(Vector2 point)
    {
        for (var index = placed.Count - 1; index >= 0; index--)
        {
            var control = placed[index];
            var box = control.Bounds;
            if (control.Enabled && control.HitTestVisible
                && point.X >= box.left && point.X < box.right && point.Y >= box.top && point.Y < box.bottom)
            {
                return control;
            }
        }
        return null;
    }

    private static Vector2 Local(Widget control, Vector2 point) => new(point.X - control.Bounds.left, point.Y - control.Bounds.top);

    /// <summary><paramref name="point"/> in the card's own px, from <paramref name="box"/>'s top-left: where its layout places its controls.</summary>
    private static Vector2 Own(Vector2 point, D2D_RECT_F box) => new(point.X - box.left, point.Y - box.top);

    /// <summary>
    /// A click lands on a control of the card's own: its index in the layout, one up. A slider is one too, though
    /// the pointer drags it (CardPress) rather than clicks it: so it is a stop the folder's keys step (CardStep).
    /// The volume's line with its ">" answers its page's target anywhere else (§605, <see cref="PageBeside"/>).
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Follow();
        _ = Lay(box, style);
        var under = Under(Own(point, box));
        return PageBeside(under) ? placed.IndexOf(pageButton) + 1
            : under is { } control && Acts(control) ? placed.IndexOf(control) + 1 : 0;
    }

    /// <summary>keys: the page's stop is its ">", though the line's room answers it too (§605).</summary>
    protected internal override D2D_RECT_F? StopBox(int target, D2D_RECT_F box, in TileStyle style)
    {
        Follow();
        _ = Lay(box, style);
        if (!PageShown || target != placed.IndexOf(pageButton) + 1 || target <= 0)
        {
            return null;
        }
        var at = pageButton.Bounds;
        return new D2D_RECT_F(box.left + at.left, box.top + at.top, box.left + at.right, box.top + at.bottom);
    }

    /// <summary>Whether <paramref name="control"/> answers the pointer itself: a click of its own, or a slider's drag.</summary>
    private static bool Acts(Widget control) => control.Clicked is not null || control is Slider;

    /// <summary>
    /// §605: whether a point over <paramref name="under"/> (null: over none) is the page's — on the volume's line with its
    /// ">", anywhere but on a control that acts itself, the ">" with it. As laid out last.
    /// </summary>
    private bool PageBeside(Widget? under) =>
        PageShown && placed.Contains(pageButton) && (under is null || under == pageButton || !Acts(under));

    /// <summary>
    /// keys: a slider the folder's keys are on (<see cref="CardTarget"/>) stepped — the volume's or an app's, a level
    /// a step, as the page's own slider steps by its keys (Slider.Step). Nothing for any other control.
    /// </summary>
    protected internal override bool CardStep(int target, int steps, nint screen)
    {
        // The controls as last laid out, which the target numbers: the slider steps from the level it shows.
        if (target <= 0 || target > placed.Count || placed[target - 1] is not Slider { Enabled: true } slider)
        {
            return false;
        }
        _ = slider.Step(steps);
        _ = Wake();
        return true;
    }

    /// <summary>
    /// A click on a device, on an app's icon or on the microphone's mute: what that control's own
    /// click does, exactly as it does in the flyout. A click on nothing does nothing; a line of the
    /// panel is not a button.
    /// </summary>
    public override IslandAction Click(int index)
    {
        Follow();
        // The dropdown: its field clicked, or Enter or Space on its line (index 0), opens its list
        // where there is a device to choose.
        if (picker)
        {
            choicesAsked = index == 0;
            if (index > 0 && index <= placed.Count)
            {
                placed[index - 1].Clicked?.Invoke();
            }
            var asked = choicesAsked;
            choicesAsked = false;
            return asked && Choices.Count > 0 ? new IslandAction(View: IslandView.Choices) : default;
        }
        if (press.Up())
        {
            _ = Wake();
        }
        if (index > 0 && index <= placed.Count)
        {
            pageAsked = false;
            placed[index - 1].Clicked?.Invoke();
            _ = Wake();
            if (pageAsked)
            {
                pageAsked = false;
                return new IslandAction(View: IslandView.Page);
            }
        }
        return default;
    }

    /// <summary>Whether the volume's line has its page's ">": in an open folder, not on a page itself (SwitchIsland.PageShown).</summary>
    internal bool PageShown =>
        part == SoundPart.Volume && Container is FolderIsland && !Key.Contains("/page/", StringComparison.OrdinalIgnoreCase)
        && FolderPages.Has(ListKind.SoundVolume);

    /// <summary>
    /// A press is this card's only on a slider — the one control that is dragged. A press anywhere
    /// else is left to the folder, which makes it a click (CardTarget, Click), so a mixer's mute and
    /// a slider in one card each answer the way they do in the flyout: taking the pointer is
    /// all-or-nothing per card, and this is where the card says which of its parts are its own.
    /// </summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Follow();
        _ = Lay(box, style);
        var own = Own(point, box);
        var under = Under(own);
        if (under is not Slider slider)
        {
            // Not taken, so the folder's click: the line lit pressed until it comes, where the press is its page's (§605).
            if (press.Down(PageBeside(under) ? 1 : 0))
            {
                _ = Wake();
            }
            return false;
        }
        // Pressed first, as the panel does: the slider's Dragging is its Pressed, and it is what
        // keeps a refresh from writing the level Windows read back into the thumb under the pointer.
        slider.Pressed = true;
        if (!slider.PointerPressed(Local(slider, own), Mods.None, 1))
        {
            slider.Pressed = false;
            return false;
        }
        held = slider;
        heldBox = box;
        heldAt = own;
        _ = Wake();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (held is null)
        {
            return;
        }
        heldAt = Own(point, heldBox);
        held.PointerMoved(Local(held, heldAt));
        _ = Wake();
    }

    /// <summary>
    /// The pointer let go. The slider hears it where it was last dragged to — the folder does not
    /// say where a release happened — and settles once, which is when the flyout plays its tick.
    /// </summary>
    protected internal override void CardRelease()
    {
        if (held is not { } was)
        {
            return;
        }
        held = null;
        was.Pressed = false;
        was.PointerReleased(Local(was, heldAt), inside: true);
        _ = Wake();
    }

    /// <summary>
    /// The wheel over a mix's slider steps that app's level, as it does in the flyout; the wheel
    /// anywhere on the volume's line steps the level (see the note at the top).
    /// </summary>
    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        Follow();
        // What the last notch set, read first, as a paint reads it: laid out from the level before
        // it, the slider would start this notch where the last one started, and a quick turn would
        // go nowhere.
        Take();
        _ = Lay(box, style);
        var slider = part == SoundPart.Volume && placed.Contains(volume) ? volume : Under(Own(point, box)) as Slider;
        if (slider is null)
        {
            return false;
        }
        _ = slider.Wheel(notches * 120);
        _ = Wake();
        return true;
    }

    /// <summary>
    /// Only a device's row and the controls light under the pointer, never the line around them (CellIsland.LightsItsRows), as on the sound page
    /// — but for the volume's line beside its controls, which is its page's and lights itself (§605).
    /// </summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The control under the pointer lights, as it does in the flyout, or the volume's whole line beside its controls (§605); the card asks for its redraw.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Follow();
        _ = Lay(box, style);
        var under = point is { } at ? Under(Own(at, box)) : null;
        // §605: beside the volume's controls the line is its page's and lights whole; no control lights with it.
        var beside = point is not null && PageBeside(under);
        if (beside)
        {
            under = null;
        }
        var changed = press.Over(beside ? 1 : 0);
        if (beside != pageHot || changed)
        {
            pageHot = beside;
            _ = Wake();
        }
        if (under == hot)
        {
            return;
        }
        if (hot is not null)
        {
            hot.Hot = false;
        }
        hot = under;
        if (hot is not null)
        {
            hot.Hot = true;
        }
        _ = Wake();
    }
}
