// A bar's scene made and laid out at once, as a picture or a tree test wants one: the dock keeps one
// scene a window and lays it out again at every change (Scene.Update, docs\widgets.md §358).

using System.Numerics;
using Karpo.Dock;
using Windows.UI.Composition.Desktop;

namespace Karpo.Tests;

internal static class Scenes
{
    /// <summary>A scene on <paramref name="target"/>, laid out once as <paramref name="layout"/> with <paramref name="content"/>.</summary>
    public static Scene Build(Gfx gfx, DesktopWindowTarget target, Layout layout, Content content, Look look, Icons icons, bool shown, EditView? edit = null, Vector2 origin = default)
    {
        var scene = new Scene(gfx, target);
        scene.Update(layout, content, look, icons, shown, edit, origin);
        return scene;
    }
}
