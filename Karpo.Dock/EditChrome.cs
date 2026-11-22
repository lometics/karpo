// edit: what edit mode shows of a display besides its bars and places, as one model (docs\widgets.md
// §359): the dim (EditShade), the display's edit bar (EditDeck), the sections' edit bars (EditRail) and
// the selection rectangle a drag over the dim draws (EditBand). The flicker design's last phase.
//
// It used to be shown piece by piece, from wherever something changed: every build of a bar showed the
// rails again (twice, where one had to step aside for another), the edit bars again (reopened for a px
// of room worked out from bars half built), the dims from the arrange and again after it, each raising
// itself over whatever had come up over it (EditStack), the dims lifting the bars over them. So the
// rails jumped between two places in one build, the edit bars and rails were shown, covered by the dim,
// and lifted, and the edit dress came a frame before its dim or went a frame after it.
//
// Now the dock works out one model a display (Dock.ChromeModel: dimmed or not, the edit bar with what it
// says, each rail where it stands — its shift from the others worked out
// before anything is shown —, the band) and the chrome is reconciled to it once (Update), as the frame
// that changed it ends: what is the same moves nothing, what changed changes in place. Its windows are
// clear while out of sight (§359, WindowTable), so the dim and the bars' edit dress, the edit bar and a
// folder's crumb, come and go in the same commit; where each stands in the z-order is its stratum's
// (ZPlan): the dim under an arranged folder, under the bars, under the edit bars.

using Karpo.Core;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

/// <summary>
/// A display's edit bar as the model has it (§352): in <paramref name="Look"/> at <paramref name="Scale"/>,
/// by <paramref name="Room"/>, saying <paramref name="State"/>: on every display, a folder open on another or not (§390);
/// §575 an entry for each page of <paramref name="Content"/>, the flyout of <paramref name="Open"/> open (none for null), hung
/// from its entry; §593 the add page from the whole bar, as wide as it, no taller than leaves <paramref name="Clear"/> (screen
/// px: the open folder it adds into) clear where it can.
/// </summary>
internal sealed record EditDeckModel(Look Look, float Scale, EditDeckRoom Room, EditDeckState State, SettingsContent? Content = null, PanelPage? Open = null, RECT? Clear = null);

/// <summary>
/// A section's edit bar as the model has it: beside <paramref name="Bar"/> (the section on screen) of
/// <paramref name="Surface"/>'s <paramref name="Section"/>, on <paramref name="Edge"/>, inside
/// <paramref name="Work"/>, saying what it holds and cannot show (<paramref name="NotShown"/>),
/// <paramref name="Shift"/> px along it off its middle, clear of the others.
/// </summary>
internal sealed record EditRailModel(object Surface, BarSection Section, Look Look, float Scale, RECT Bar, Edge Edge, RECT Work, IReadOnlyList<PartSpec> NotShown, int Shift);

/// <summary>The selection rectangle over <paramref name="Area"/> (screen px), in <paramref name="Accent"/>, at <paramref name="Scale"/>.</summary>
internal sealed record EditBandModel(RECT Area, Color Accent, float Scale);

/// <summary>
/// What edit mode shows of the display at <paramref name="Display"/> (screen px): its dim, its edit bar
/// (<paramref name="Deck"/>), its sections' edit bars (<paramref name="Rails"/>), the selection rectangle
/// while a drag over its dim draws one (<paramref name="Band"/>). §612: the dim <paramref name="Dimmed"/> while edit mode's
/// dress is worn (<see cref="EditDress.Edit"/>), clear in the Appearance's preview — its window there all the same.
/// </summary>
internal sealed record EditChromeModel(RectI Display, EditDeckModel Deck, IReadOnlyList<EditRailModel> Rails, EditBandModel? Band = null, bool Dimmed = true);

/// <summary>A display's edit chrome (see the top of the file), reconciled to its model.</summary>
internal sealed class EditChrome : IDisposable
{
    private readonly Gfx gfx;
    private readonly HINSTANCE instance;
    private readonly nint dock;
    private readonly Dictionary<(object Surface, BarSection Section), EditRail> rails = [];
    private EditBand? band;

    /// <param name="dock">Goes into its windows' GWLP_USERDATA, so the dock's window procedure finds the dock.</param>
    /// <param name="dim">Tests only: the dim's colour (EditShade's ink), one a film's capture can see.</param>
    public EditChrome(Gfx gfx, HINSTANCE instance, nint dock, Color? dim = null)
    {
        (this.gfx, this.instance, this.dock) = (gfx, instance, dock);
        Shade = new EditShade(gfx, instance, dock, dim);
        Deck = new EditDeck(gfx, instance, dock);
    }

    public EditShade Shade { get; }

    public EditDeck Deck { get; }

    /// <summary>The sections' edit bars, by the surface and the section each stands beside; shown or not.</summary>
    public IReadOnlyDictionary<(object Surface, BarSection Section), EditRail> Rails => rails;

    /// <summary>The model it shows; null out of edit mode.</summary>
    public EditChromeModel? Model { get; private set; }

    /// <summary>The selection rectangle's window, while one was made.</summary>
    internal HWND BandHwnd => band?.Hwnd ?? HWND.NULL;

    /// <summary>Whether <paramref name="window"/> is one of its windows: the dim's, the edit bar's or its tip's, a rail's or its tip's.</summary>
    public bool Owns(HWND window) =>
        window != HWND.NULL && (window == Shade.Hwnd || Deck.Owns(window) || rails.Values.Any(rail => rail.Owns(window)));

    /// <summary>
    /// Shows <paramref name="model"/>, nothing out of edit mode (null): what is as it was moves nothing, what
    /// changed changes in place; a rail not in it goes out of sight, kept for when it is again.
    /// </summary>
    public void Update(EditChromeModel? model)
    {
        // §612: the dim fades by the motion of the settings' look the edit bar wears, as the edit bar comes and goes by it.
        var motion = (model ?? Model)?.Deck.Look.Motion ?? Motion.Still;
        Model = model;
        if (model is null)
        {
            Deck.Hide();
            foreach (var rail in rails.Values)
            {
                rail.Hide();
            }
            band?.Hide();
            Shade.Hide(motion);
            return;
        }
        Shade.Show(model.Display, model.Dimmed, motion);
        Deck.Show(gfx, model.Deck.Look, model.Deck.Scale, model.Deck.Room, model.Deck.State, model.Deck.Content, model.Deck.Open, model.Deck.Clear);
        var shown = new HashSet<(object, BarSection)>();
        foreach (var each in model.Rails)
        {
            var key = (each.Surface, each.Section);
            if (!rails.TryGetValue(key, out var rail))
            {
                rail = new EditRail(gfx, instance, dock);
                rails[key] = rail;
            }
            rail.Show(gfx, each.Look, each.Scale, each.Bar, each.Edge, each.Work, each.NotShown, each.Shift);
            shown.Add(key);
        }
        foreach (var (key, rail) in rails)
        {
            if (!shown.Contains(key))
            {
                rail.Hide();
            }
        }
        if (model.Band is { } area)
        {
            band ??= new EditBand(gfx, instance);
            band.Show(area.Area, area.Accent, area.Scale);
        }
        else
        {
            band?.Hide();
        }
    }

    /// <summary>The rails of surfaces <paramref name="gone"/> says are gone let go of, for good.</summary>
    public void Prune(Func<object, bool> gone)
    {
        foreach (var (key, rail) in rails.ToList())
        {
            if (gone(key.Surface))
            {
                rails.Remove(key);
                rail.Dispose();
            }
        }
    }

    public void Dispose()
    {
        band?.Dispose();
        band = null;
        foreach (var rail in rails.Values)
        {
            rail.Dispose();
        }
        rails.Clear();
        Deck.Dispose();
        Shade.Dispose();
    }
}
