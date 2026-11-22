// Edit mode's own bars keep their buttons (docs\widgets.md §363; KeptButtons): a section's edit bar drew every button
// afresh whenever it moved with its section. Now only what changed is made again: what is made, counted. (The display's
// edit bar kept its buttons the same way until §520; since, it is one surface drawn whole — the settings panel's bar,
// SettingsPanel.Paint.cs — and shown in the commit that brings it.)

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class KeptEditBarTests
{
    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Work = new() { right = 1280, bottom = 648 };

    /// <summary>A section's edit bar moved with its section: nothing made, its buttons moved with it.</summary>
    [UiFact]
    public void ARailMovedWithItsSectionMakesNothing()
    {
        FlickerFilmTests.OnHiddenDesktop("kept-rail", (gfx, instance) =>
        {
            using var rail = new EditRail(gfx, instance, 0);
            var section = new RECT { left = 100, top = 648, right = 400, bottom = 720 };
            PartSpec[] hidden = [new PartSpec("layout:default/volume", BarKind.Volume)];
            rail.Show(gfx, Still, 1f, section, Edge.Bottom, Work, hidden);
            FrameFilm.Settle();
            var made = rail.Made;
            var stood = rail.Panel;
            rail.Show(gfx, Still, 1f, section with { left = 160, right = 460 }, Edge.Bottom, Work, hidden);
            FrameFilm.Settle();
            Assert.Equal(made, rail.Made);
            Assert.NotEqual(stood?.left, rail.Panel?.left);
            // What it says changes: its mark's brush swapped, nothing made.
            rail.Show(gfx, Still, 1f, section with { left = 160, right = 460 }, Edge.Bottom, Work, [.. hidden, new PartSpec("layout:default/nightlight", BarKind.NightLight)]);
            Assert.Equal(made, rail.Made);
        });
    }
}
