// An app button's signals on the bar (AppSignals.cs): the progress where the running indicator is,
// and the overlay or the badge on the icon's corner. Everything here is set in place by SetMark, as
// the indicator is; only the badge's and the overlay's pictures are drawn, once per change.
//
// The progress is composition, like the indicator it stands in for: a hairline track, the part done
// over it with the indicator's round ends, and for a run without an end, a piece of the run that DWM
// keeps sliding along it — nothing in this process runs per frame for it. The overlay and the badge
// are children of the icon's sprite, so they shrink with it when the button is pressed and glide
// with it when a rebuild moves it.

using System.Numerics;
using Karpo.Core;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

public sealed partial class Scene
{
    // Per tile, in step with `tiles`: where its signals go, and what is drawn so far (made the
    // first time the tile has something to show), as the last update counted the tiles.
    private readonly List<SignalPlace> signalPlaces = [];
    private readonly List<SignalVisuals?> signalVisuals = [];

    /// <summary>§358: each tile's signals by its key, kept with it as long as the bar has the tile; and the keys this update showed.</summary>
    private readonly Dictionary<string, SignalVisuals> signals = [];
    private readonly HashSet<string> signalsSeen = [];

    /// <summary>
    /// The implicit animations the part done grows with once the scene has attached its own: its
    /// size and place together (up a vertical bar it grows from the bottom, so both change), and
    /// its rounded shape's size.
    /// </summary>
    private (ImplicitAnimationCollection Move, ImplicitAnimationCollection Fit)? signalGrow;

    /// <summary>Where one tile's signals go: its highlight's run, its icon box, its part and its key.</summary>
    private readonly record struct SignalPlace(RectF Run, RectF IconBox, int Part, string Key, bool Placed);

    private sealed class SignalVisuals
    {
        /// <summary>Everything made for them, closed when the tile goes.</summary>
        public required List<IDisposable> Bag;
        public required ContainerVisual Run;

        /// <summary>
        /// Where the run and the corner hang, kept here and never read back from the visuals: a home
        /// closed under them (its part gone, the icon's sprite made anew at another scale) answers no
        /// question about itself (RO_E_CLOSED), so the scene lets them go before it closes one (LetSignalsGo).
        /// </summary>
        public required ContainerVisual? RunHome;
        public ContainerVisual? CornerHome;
        public required SpriteVisual Track;
        public required CompositionColorBrush TrackBrush;
        public required SpriteVisual Done;
        public required CompositionColorBrush DoneBrush;
        public required SpriteVisual Runner;
        public required CompositionColorBrush RunnerBrush;
        public bool Running;

        /// <summary>The runner's lap, made the first time the run has no end and kept for the scene.</summary>
        public Vector3KeyFrameAnimation? Lap;
        public SpriteVisual? Corner;
        public CompositionSurfaceBrush? CornerBrush;
        public CompositionDrawingSurface? CornerSurface;

        /// <summary>What the corner shows now: a Pixels or a Badge, compared to know when to draw again.</summary>
        public object? CornerShows;
    }

    /// <summary>
    /// Called for each tile as an update lays it out: where its signals would go. What the tile showed
    /// before is kept, moved with it (gliding as it does) and handed to its icon as it is now.
    /// </summary>
    private void PlaceSignals(RectF highlight, RectF iconBox, int part, string key, bool placed)
    {
        var run = SignalShape.Run(highlight, layout.Edge, layout.Pill, layout.Stroke, U(Layout.HighlightRadiusDip));
        var place = new SignalPlace(run, iconBox, part, key, placed);
        signalPlaces.Add(place);
        signalsSeen.Add(key);
        if (!signals.TryGetValue(key, out var kept))
        {
            signalVisuals.Add(null);
            return;
        }
        signalVisuals.Add(kept);
        var into = Into(part, Layer.Front);
        if (!ReferenceEquals(kept.RunHome, into))
        {
            kept.RunHome?.Children.Remove(kept.Run);
            into.Children.InsertAtTop(kept.Run);
            kept.RunHome = into;
        }
        var (offset, showed) = (kept.Run.Offset, kept.Run.IsVisible);
        Put(kept.Run, run);
        if (glide && showed && offset != kept.Run.Offset)
        {
            Slide(kept.Run, offset, kept.Run.Offset, true);
        }
        // On the icon's own sprite: pressed, the corner shrinks with the icon it belongs to.
        if (kept.Corner is { } corner && tiles.Count > 0 && !ReferenceEquals(kept.CornerHome, tiles[^1]))
        {
            kept.CornerHome?.Children.Remove(corner);
            tiles[^1].Children.InsertAtTop(corner);
            kept.CornerHome = tiles[^1];
        }
    }

    /// <summary>
    /// Before the scene closes what <paramref name="closing"/> says it will (an element's bag, a part's
    /// containers), every signal hanging in it is taken out: it hangs again where the tile is next laid
    /// out (PlaceSignals), or goes with its tile (Sweep).
    /// </summary>
    private void LetSignalsGo(Func<ContainerVisual, bool> closing)
    {
        foreach (var visuals in signals.Values)
        {
            if (visuals.RunHome is { } run && closing(run))
            {
                run.Children.Remove(visuals.Run);
                visuals.RunHome = null;
            }
            if (visuals.CornerHome is { } corner && visuals.Corner is { } sprite && closing(corner))
            {
                corner.Children.Remove(sprite);
                visuals.CornerHome = null;
            }
        }
    }

    /// <summary>
    /// Shows tile <paramref name="index"/>'s signals: the progress in the indicator's place (the
    /// indicator hidden while it shows), and the badge — or, without one, the overlay — on the icon's
    /// corner. Nothing is made for a tile that never had any.
    /// </summary>
    private void ShowSignal(int index, TileSignal signal)
    {
        if ((uint)index >= (uint)signalPlaces.Count || (uint)index >= (uint)pills.Count)
        {
            return;
        }
        var place = signalPlaces[index];
        var visuals = signalVisuals[index];
        if (visuals is null && !signal.Any)
        {
            return;
        }
        if (visuals is null)
        {
            visuals = signalVisuals[index] = signals[place.Key] = MakeSignal(place);
        }
        var vertical = layout.Vertical;
        // The progress takes the indicator's place, as the taskbar's does; with nothing done it
        // shows nothing, and the indicator stays (SignalShape.Shows).
        var progress = place.Placed && SignalShape.Shows(signal, place.Run, vertical);
        pills[index].Opacity = progress ? 0f : 1f;
        visuals.Run.IsVisible = progress;
        if (progress)
        {
            var ink = SignalInk.Progress(signal.Progress, look);
            visuals.TrackBrush.Color = SignalInk.Track(look);
            visuals.DoneBrush.Color = ink;
            visuals.RunnerBrush.Color = ink;
            var local = new RectF(0f, 0f, place.Run.Width, place.Run.Height);
            if (signal.Progress == ProgressKind.Indeterminate)
            {
                visuals.Done.IsVisible = false;
                Run(visuals, local, vertical);
            }
            else
            {
                Stop(visuals);
                var done = SignalShape.Done(local, vertical, signal.Value);
                visuals.Done.IsVisible = !done.IsEmpty;
                Put(visuals.Done, done);
                if (PillShape(visuals.Done) is { } shape)
                {
                    shape.Size = done.Size;
                }
            }
        }
        else
        {
            Stop(visuals);
        }
        ShowCorner(index, visuals, place, signal);
    }

    /// <summary>The run's visuals for one tile: a clipped container holding the track, the part done and the runner.</summary>
    private SignalVisuals MakeSignal(SignalPlace place)
    {
        var c = compositor;
        var bag = new List<IDisposable>();
        using var making = Making(bag, swatches);
        var run = Own(c.CreateContainerVisual());
        Put(run, place.Run);
        run.Clip = Own(c.CreateInsetClip());
        run.IsVisible = false;
        var local = new RectF(0f, 0f, place.Run.Width, place.Run.Height);
        var trackBrush = Own(c.CreateColorBrush(SignalInk.Track(look)));
        var track = Own(c.CreateSpriteVisual());
        track.Brush = trackBrush;
        Put(track, SignalShape.Track(local, layout.Vertical, layout.Stroke));
        run.Children.InsertAtTop(track);
        var doneBrush = Own(c.CreateColorBrush());
        var done = Pill(c, layout.Pill);
        done.Brush = doneBrush;
        run.Children.InsertAtTop(done);
        var runnerBrush = Own(c.CreateColorBrush());
        var runner = Pill(c, layout.Pill);
        runner.Brush = runnerBrush;
        runner.IsVisible = false;
        run.Children.InsertAtTop(runner);
        if (signalGrow is { } grow)
        {
            done.ImplicitAnimations = grow.Move;
            if (PillShape(done) is { } shape)
            {
                shape.ImplicitAnimations = grow.Fit;
            }
        }
        var home = Into(place.Part, Layer.Front);
        home.Children.InsertAtTop(run);
        return new SignalVisuals
        {
            Bag = bag,
            Run = run,
            RunHome = home,
            Track = track,
            TrackBrush = trackBrush,
            Done = done,
            DoneBrush = doneBrush,
            Runner = runner,
            RunnerBrush = runnerBrush,
        };
    }

    /// <summary>
    /// The runner of an indeterminate run: <see cref="SignalShape.RunnerShare"/> of it, coming in at
    /// the start and leaving at the end, lap after lap, all in DWM. Without animations it stands still
    /// in the middle, which still says "busy" and never looks like a share done.
    /// </summary>
    private void Run(SignalVisuals visuals, RectF run, bool vertical)
    {
        var length = SignalShape.RunnerLength(run, vertical);
        var size = vertical ? new Vector2(run.Width, length) : new Vector2(length, run.Height);
        visuals.Runner.Size = size;
        if (PillShape(visuals.Runner) is { } shape)
        {
            shape.Size = size;
        }
        visuals.Runner.IsVisible = true;
        var span = vertical ? run.Height : run.Width;
        if (!look.Animations)
        {
            var middle = MathF.Round((span - length) / 2f, MidpointRounding.AwayFromZero);
            visuals.Runner.StopAnimation("Offset");
            visuals.Runner.Offset = vertical ? new Vector3(0f, middle, 0f) : new Vector3(middle, 0f, 0f);
            visuals.Running = false;
            return;
        }
        if (visuals.Running)
        {
            return;
        }
        if (visuals.Lap is null)
        {
            // Up a vertical bar the run goes the way its fill grows: from the bottom.
            var from = vertical ? new Vector3(0f, span, 0f) : new Vector3(-length, 0f, 0f);
            var to = vertical ? new Vector3(0f, -length, 0f) : new Vector3(span, 0f, 0f);
            var lap = compositor.CreateVector3KeyFrameAnimation();
            visuals.Bag.Add(lap);
            lap.InsertKeyFrame(0f, from);
            lap.InsertKeyFrame(1f, to, linear);
            lap.Duration = Motion.Lap(Motion.RunnerLapMs);
            lap.IterationBehavior = AnimationIterationBehavior.Forever;
            visuals.Lap = lap;
        }
        visuals.Runner.StartAnimation("Offset", visuals.Lap);
        visuals.Running = true;
    }

    /// <summary>An indeterminate run no more.</summary>
    private static void Stop(SignalVisuals visuals)
    {
        if (visuals.Running)
        {
            visuals.Runner.StopAnimation("Offset");
            visuals.Running = false;
        }
        visuals.Runner.IsVisible = false;
    }

    /// <summary>
    /// The icon's corner: the app's badge where it has one, else the overlay its window set, else
    /// nothing. Drawn again only when what it shows changed.
    /// </summary>
    private void ShowCorner(int index, SignalVisuals visuals, SignalPlace place, TileSignal signal)
    {
        object? shows = signal.Badge is { } badge ? badge : signal.Overlay;
        if (shows is null || place.IconBox.IsEmpty || !place.Placed || (uint)index >= (uint)tiles.Count)
        {
            if (visuals.Corner is { } hidden)
            {
                hidden.IsVisible = false;
            }
            visuals.CornerShows = null;
            return;
        }
        if (Equals(visuals.CornerShows, shows) && visuals.Corner is { } same)
        {
            same.IsVisible = true;
            return;
        }
        var side = SignalShape.CornerSide(layout.Icon);
        if (shows is Pixels picture && visuals.CornerShows is Pixels && visuals.CornerSurface is { } drawn && visuals.Corner is { } overlay)
        {
            // A new picture for the overlay already there: painted again where it is, as a tray icon is.
            gfx.RedrawIcon(drawn, picture);
            overlay.IsVisible = true;
            visuals.CornerShows = shows;
            return;
        }
        Vector2 size;
        CompositionDrawingSurface surface;
        if (shows is Badge shown)
        {
            var plan = SignalPaint.Plan(gfx, shown, side);
            size = plan.Size;
            surface = Badged(gfx, shown, plan);
        }
        else
        {
            size = new Vector2(side, side);
            surface = gfx.Icon((Pixels)shows, side);
        }
        // The tile's signals close what is made for them when it goes; the surface this one replaces goes now.
        if (visuals.CornerSurface is { } replaced)
        {
            visuals.Bag.Remove(replaced);
            replaced.Dispose();
        }
        this.drawn++;
        visuals.Bag.Add(surface);
        var c = compositor;
        if (visuals.Corner is null)
        {
            visuals.CornerBrush = c.CreateSurfaceBrush();
            visuals.Bag.Add(visuals.CornerBrush);
            visuals.CornerBrush.Stretch = CompositionStretch.None;
            visuals.CornerBrush.HorizontalAlignmentRatio = 0f;
            visuals.CornerBrush.VerticalAlignmentRatio = 0f;
            visuals.Corner = c.CreateSpriteVisual();
            visuals.Bag.Add(visuals.Corner);
            visuals.Corner.Brush = visuals.CornerBrush;
            // On the icon's own sprite: pressed, the corner shrinks with the icon it belongs to.
            tiles[index].Children.InsertAtTop(visuals.Corner);
            visuals.CornerHome = tiles[index];
        }
        visuals.CornerSurface = surface;
        visuals.CornerBrush!.Surface = surface;
        var corner = SignalShape.Corner(place.IconBox, size);
        Put(visuals.Corner, new RectF(corner.X - place.IconBox.X, corner.Y - place.IconBox.Y, corner.Width, corner.Height));
        visuals.Corner.IsVisible = true;
        visuals.CornerShows = shows;
    }

    /// <summary>A badge's own surface, painted once.</summary>
    private unsafe CompositionDrawingSurface Badged(Gfx gfx, Badge badge, SignalPaint.BadgePlan plan)
    {
        var dressed = look;
        return gfx.Draw(plan.Size.X, plan.Size.Y, dc => SignalPaint.Badge(dc, gfx, badge, plan, dressed));
    }

    /// <summary>The scene's animations are attached: the part done grows smoothly from here on, a run made later too.</summary>
    private void GrowSignals(ImplicitAnimationCollection move, ImplicitAnimationCollection fit)
    {
        signalGrow = (move, fit);
        foreach (var visuals in signals.Values)
        {
            visuals.Done.ImplicitAnimations = move;
            if (PillShape(visuals.Done) is { } shape)
            {
                shape.ImplicitAnimations = fit;
            }
        }
    }

    /// <summary>tests: where tile <paramref name="index"/>'s progress shows, and what is drawn of it; null when it shows none.</summary>
    internal (RectF Run, RectF Done, bool Runner, Color Ink, bool PillShown)? ProgressAt(int index)
    {
        if ((uint)index >= (uint)signalVisuals.Count || signalVisuals[index] is not { Run.IsVisible: true } visuals)
        {
            return null;
        }
        var run = visuals.Run;
        var done = visuals.Done;
        return (
            new RectF(run.Offset.X, run.Offset.Y, run.Size.X, run.Size.Y),
            done.IsVisible ? new RectF(done.Offset.X, done.Offset.Y, done.Size.X, done.Size.Y) : default,
            visuals.Runner.IsVisible,
            visuals.DoneBrush.Color,
            pills[index].Opacity > 0f);
    }

    /// <summary>tests: whether tile <paramref name="index"/>'s running indicator shows, whatever its signals.</summary>
    internal bool PillShownAt(int index) => (uint)index < (uint)pills.Count && pills[index].Opacity > 0f;

    /// <summary>tests: where tile <paramref name="index"/>'s corner shows, relative to its icon, or null.</summary>
    internal RectF? CornerAt(int index) =>
        (uint)index < (uint)signalVisuals.Count && signalVisuals[index] is { Corner: { IsVisible: true } corner }
            ? new RectF(corner.Offset.X, corner.Offset.Y, corner.Size.X, corner.Size.Y)
            : null;
}
