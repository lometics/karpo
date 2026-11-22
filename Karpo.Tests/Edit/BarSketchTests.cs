// edit, §679 (the person, 2026-11-12: "the previews look shit. you cant really see how it would change anything. and i didnt
// mean that you literaly have to render a preview of the whole app. it should be more like you did it for the appearance
// settings for example. also settings like combine buttons didnt get the preview"): a choice of how a display's bar stands or
// behaves pictured by a piece of the bar close up (LookShown.Sketch) — which rows, what app buttons, what each picture says.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class BarSketchTests
{
    private static readonly BarSpot Spot = new("DISPLAY1", "One");

    /// <summary>Taskbar's Show and Behavior choices the page's display's; a display's edge and layout the display they are of; nothing else.</summary>
    [Fact]
    public void EachRowOfHowABarBehavesHasItsSketch()
    {
        Assert.Equal((BarSketch.Type, "DISPLAY1"), TheDock.SketchOf(BarLooks.TypeRow, Spot));
        Assert.Equal((BarSketch.Show, "DISPLAY1"), TheDock.SketchOf(BarLooks.ShowRow, Spot));
        Assert.Equal((BarSketch.Buttons, "DISPLAY1"), TheDock.SketchOf(BarLooks.CombineRow, Spot));
        Assert.Equal((BarSketch.Desktops, "DISPLAY1"), TheDock.SketchOf(BarLooks.DesktopsRow, Spot));
        Assert.Equal((BarSketch.Opens, "DISPLAY1"), TheDock.SketchOf(BarLooks.HoverRow, Spot));
        Assert.Equal((BarSketch.Edge, "DISPLAY2"), TheDock.SketchOf(BarLooks.KeyOf(BarLooks.PositionRow, "DISPLAY2"), Spot));
        Assert.Equal((BarSketch.Motion, "DISPLAY1"), TheDock.SketchOf(KarpoMenu.AnimationsRow, Spot));
        // §700: a display's layout each its own bar, a line each (§682 had them names alone).
        Assert.Equal((BarSketch.Layout, "DISPLAY2"), TheDock.SketchOf(BarLooks.KeyOf(BarLooks.ShownRow, "DISPLAY2"), Spot));
        Assert.True(LookPreview.FullWide(TheDock.Sketched(Menus.MenuCorpus.Fresh(), BarSketch.Layout, Menus.MenuCorpus.Display)));
        // Size and the Appearance's none.
        Assert.Null(TheDock.SketchOf(BarLooks.SizeRow, Spot));
        Assert.Null(TheDock.SketchOf(LookRows.PaletteRow, Spot));
    }

    private static LookShown Sketch(BarSketch sketch, ButtonCombine combine, bool all)
    {
        var config = Menus.MenuCorpus.Fresh();
        var layout = config.ActiveLayout(Menus.MenuCorpus.Display);
        layout.Combine = combine;
        layout.AllDesktops = all;
        return TheDock.Sketched(config, sketch, Menus.MenuCorpus.Display);
    }

    /// <summary>Combined always an app each, its windows counted, no words; else a window each, its title beside it.</summary>
    [Fact]
    public void CombinedAnAppEachElseAWindowEachSayingItsTitle()
    {
        var always = LookPreview.AppsOf(Sketch(BarSketch.Buttons, ButtonCombine.Always, all: false));
        Assert.Equal(2, always.Count);
        Assert.Equal(2, always[0].Count);
        Assert.All(always, app => Assert.Null(app.Label));
        var never = LookPreview.AppsOf(Sketch(BarSketch.Buttons, ButtonCombine.Never, all: false));
        Assert.All(never, window => Assert.Equal(1, window.Count));
        Assert.Equal(["Documents", "notes.txt", "Downloads"], never.Select(window => window.Label));
    }

    /// <summary>When the taskbar is full: titles while there is room, then every window an app each.</summary>
    [Fact]
    public void WhenFullIsTitlesThenAnAppEach()
    {
        var (room, full) = LookPreview.FullOf();
        Assert.All(room, window => Assert.NotNull(window.Label));
        Assert.All(full, app => Assert.Null(app.Label));
        Assert.Equal(LookPreview.WindowStandIns.Length, full.Sum(app => app.Count));
        Assert.True(full.Count < LookPreview.WindowStandIns.Length);
    }

    /// <summary>Of this desktop, fewer buttons than of every desktop.</summary>
    [Fact]
    public void ThisDesktopsWindowsAreFewerThanEveryDesktops()
    {
        var here = LookPreview.AppsOf(Sketch(BarSketch.Desktops, ButtonCombine.Always, all: false));
        var all = LookPreview.AppsOf(Sketch(BarSketch.Desktops, ButtonCombine.Always, all: true));
        Assert.Equal(2, here.Count);
        Assert.Equal(3, all.Count);
        // Combine buttons' the same whichever desktops are shown.
        Assert.Equal(LookPreview.AppsOf(Sketch(BarSketch.Buttons, ButtonCombine.Always, all: true)), LookPreview.AppsOf(Sketch(BarSketch.Buttons, ButtonCombine.Always, all: false)));
    }

    /// <summary>A sketch's bar: the Apps widget alone for app buttons.</summary>
    [Fact]
    public void ASketchDrawsWhatItsChoiceIsAbout() =>
        Assert.Equal([BarKind.Apps], Sketch(BarSketch.Buttons, ButtonCombine.Always, false).Layout.Islands.Select(item => item.Kind));

    /// <summary>§682: Animations' sketch its flyout coming up as fast as a flyout opens at that speed, slower slower; at once Off.</summary>
    [Fact]
    public void AnimationsSketchIsAsFastAsItsSpeed()
    {
        Assert.Equal((0d, 0d), LookPreview.MotionMs(Animation.Off));
        var (slower, _) = LookPreview.MotionMs(Animation.Slower);
        var (normal, _) = LookPreview.MotionMs(Animation.Normal);
        var (faster, _) = LookPreview.MotionMs(Animation.Faster);
        Assert.True(slower > normal && normal > faster && faster > 0);
        Assert.True(slower + LookPreview.MotionMs(Animation.Slower).Close < LookPreview.MotionLapMs);
    }

    /// <summary>Hidden till pointed at: a short mark in the middle of its edge, inside the room.</summary>
    [Fact]
    public void AHiddenBarLeavesAMarkAtItsEdge()
    {
        var desktop = new RectF(0, 0, 200, 100);
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            var mark = LookPreview.HandleOf(desktop, edge, 2);
            Assert.True(mark.X >= 0 && mark.Y >= 0 && mark.Right <= 200 && mark.Bottom <= 100, $"{edge}: {mark}");
            var vertical = edge is Edge.Left or Edge.Right;
            Assert.Equal(vertical ? 2 : 36, mark.Width);
            Assert.Equal(vertical ? 18 : 2, mark.Height);
        }
    }

    /// <summary>Another edge, way of showing, combining, desktops or opening: other words, another picture.</summary>
    [Fact]
    public void ASketchsWordsFollowWhatItShows()
    {
        var config = Menus.MenuCorpus.Fresh();
        var display = Menus.MenuCorpus.Display;
        var layout = config.ActiveLayout(display);
        string Said(BarSketch sketch) => TheDock.Sketched(config, sketch, display).Values();
        var before = Enum.GetValues<BarSketch>().Where(each => each != BarSketch.None).ToDictionary(each => each, Said);
        Assert.True(BarLooks.WriteEdge(config, new BarSpot(display, "One"), Edge.Top));
        Assert.NotEqual(before[BarSketch.Edge], Said(BarSketch.Edge));
        Assert.True(BarLooks.WriteShow(layout, EdgeMode.Hover));
        Assert.NotEqual(before[BarSketch.Show], Said(BarSketch.Show));
        layout.Combine = layout.Combine == ButtonCombine.Never ? ButtonCombine.Always : ButtonCombine.Never;
        Assert.NotEqual(before[BarSketch.Buttons], Said(BarSketch.Buttons));
        layout.AllDesktops = !layout.AllDesktops;
        Assert.NotEqual(before[BarSketch.Desktops], Said(BarSketch.Desktops));
        layout.OpenOnHover = !layout.OpenOnHover;
        Assert.NotEqual(before[BarSketch.Opens], Said(BarSketch.Opens));
    }
}
