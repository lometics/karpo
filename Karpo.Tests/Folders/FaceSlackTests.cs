// folders: a face on the bar takes what it shows and the button's padding, and nothing more (the
// person, 2026-10-22: "some of the faces in the taskbar have space around them although they dont need
// it. make it so that every face on the taskbar only takes as much space as it needs plus the current
// padding", docs\widgets.md §275).
//
// Held for every face there is, alone in its button and between two others, across a bar and down
// one, at 100 and 150 %: the button is its faces, the room between them and its two ends, in whole px
// — never a cell for a lone glyph — and its ink fills that but for a glyph's own margin and a line's
// leading. And each face draws among others what it draws alone: the keyboard face second in the person's
// "Tides" folder drew nothing, its indicator made only where it was the first face (§277).
//
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Folders;

public sealed unsafe class FaceSlackTests
{
    /// <summary>A folder wearing <paramref name="faces"/>, each with a place where it reads one, made over the test's stands.</summary>
    internal static FolderIsland Wearing(Gfx gfx, params string[] faces)
    {
        var item = FolderFaceTests.Folder(faces[0]);
        item.Folder!.Faces = [.. faces.Select(id => FolderFace.Of(id))];
        foreach (var face in item.Folder.Faces)
        {
            if (FolderFaces.HasPlace(face.Kind))
            {
                face.Weather = face.Kind == FolderFaces.TidesId ? SavedTides.Options() : SavedWeather.Options();
            }
        }
        item.Normalize();
        FolderIsland.AirStand = SavedAir.Cache(32, 72);
        FolderIsland.TidesStand = SavedTides.Cache();
        FolderIsland.WeatherStand = SavedWeather.Cache();
        try
        {
            var island = FolderFaceTests.Make(gfx, item);
            _ = ((IIsland)island).Refresh(gfx);
            return island;
        }
        finally
        {
            FolderIsland.AirStand = null;
            FolderIsland.TidesStand = null;
            FolderIsland.WeatherStand = null;
        }
    }

    /// <summary>The first and last px along the bar with ink on them in <paramref name="island"/>'s button.</summary>
    private static (float First, float Last) InkAlong(FolderIsland island, in IslandStyle style)
    {
        var ((left, top, right, bottom), _, _) = FaceCentringTests.InkOf(island, style);
        return style.Vertical ? (top, bottom) : (left, right);
    }

    /// <summary>The ink of face <paramref name="index"/> of <paramref name="island"/>'s button: what is drawn in that face's box alone, from the box's corner.</summary>
    private static (float Left, float Top, float Right, float Bottom) InkOfFace(Gfx gfx, FolderIsland island, in IslandStyle style, int index)
    {
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        var box = FolderIsland.FaceBoxes(gfx, island.Options, style, width, height, folder: island)[index];
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        var drawn = style;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () =>
            {
                var clip = box;
                dc->PushAxisAlignedClip(&clip, TerraFX.Interop.DirectX.D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                island.PaintCell(dc, 0, drawn, width, height);
                dc->PopAxisAlignedClip();
            }),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        return ink.Right > ink.Left ? (ink.Left - margin - box.left, ink.Top - margin - box.top, ink.Right - margin - box.left, ink.Bottom - margin - box.top) : default;
    }

    private static IEnumerable<IslandStyle> Styles()
    {
        foreach (var vertical in new[] { false, true })
        {
            foreach (var dpi in new uint[] { 96, 144 })
            {
                yield return FaceCentringTests.Style(48, dpi, vertical);
            }
        }
    }

    /// <summary>
    /// Every face, alone in its button and between an icon and the power glyph, across a bar and down
    /// one at 100 and 150 %: the button is exactly its faces, each with the count it shows past it, the
    /// room between them and its two ends, in whole px — a lone glyph's never held to a cell — and the
    /// ink of a lone face fills its room but for a glyph's own margin, its ends at the button's ends.
    /// </summary>
    [UiFact]
    public void EveryFaceIsItsContentAndThePaddingAndNothingMore()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            foreach (var id in FaceDrawings.Ids)
            {
                using var alone = Wearing(gfx, id);
                using var among = Wearing(gfx, FolderFaces.IconId, id, FolderFaces.PowerId);
                foreach (var style in Styles())
                {
                    var fit = new Fit(style);
                    var where = $"{id}, {(style.Vertical ? "down" : "across")} at {style.Scale * 100:0} %";
                    float Length(FolderIsland folder) => folder.CellSize(0, style) is var (w, h) && style.Vertical ? h : w;
                    float Faces(FolderIsland folder) =>
                        FolderIsland.FaceSizes(gfx, folder.Options, style, folder: folder).Sum(size => size.Along + size.Trail)
                        + (FolderIsland.Between(fit) * (folder.Options.Faces.Count - 1));

                    var length = Length(alone);
                    if (MathF.Abs(length - FaceWidthTests.Button(style, Faces(alone))) > 0.5f)
                    {
                        failures.Add($"{where}: alone {length} px for {Faces(alone):0.#} px of face and its ends");
                    }
                    var face = FolderIsland.FaceSizes(gfx, alone.Options, style, folder: alone)[0];
                    if (face.Square && face.Trail == 0 && MathF.Abs(face.Along - fit.Glyph()) < 0.5f && MathF.Abs(length - FaceWidthTests.GlyphButton(style)) > 0.5f)
                    {
                        failures.Add($"{where}: a glyph's button is {length} px, its glyph and ends {FaceWidthTests.GlyphButton(style)} px");
                    }
                    var between = Length(among);
                    if (MathF.Abs(between - FaceWidthTests.Button(style, Faces(among))) > 0.5f)
                    {
                        failures.Add($"{where}: among two others {between} px for {Faces(among):0.#} px of faces, the room between them and its ends");
                    }

                    // The ink: no further in than the ends and a glyph's own margin, never past them.
                    var (first, last) = InkAlong(alone, style);
                    var ends = FolderIsland.Ends(fit);
                    var margin = (style.Vertical ? 0.75f : 0.5f) * fit.Glyph();
                    if (first < ends - margin - (2 * style.Scale) || last > length - ends + margin + (2 * style.Scale))
                    {
                        failures.Add($"{where}: its ink {first:0}-{last:0} px runs into the ends of a {length} px button");
                    }
                    // Past the room a count is kept (§276), shown or not.
                    if (length - face.Trail - (last - first) - (2 * ends) > margin + (2 * style.Scale))
                    {
                        failures.Add($"{where}: {length - face.Trail - (last - first) - (2 * ends):0} px of room past its ink, its count's and its ends");
                    }
                }
            }
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} faces not their content and padding:\n{string.Join("\n", failures)}");
    }

    /// <summary>
    /// Every face between an icon and the power glyph draws in its own box what it draws alone, across a bar
    /// and down one, at 100 and 150 %: a face whose reading the folder holds for it (the keyboard's layout in
    /// use, the VPN's switch) is read whether it is the first face or another (§277).
    /// </summary>
    [UiFact]
    public void EveryFaceDrawsAmongOthersWhatItDrawsAlone()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            foreach (var id in FaceDrawings.Ids)
            {
                using var alone = Wearing(gfx, id);
                using var among = Wearing(gfx, FolderFaces.IconId, id, FolderFaces.PowerId);
                foreach (var style in Styles())
                {
                    var where = $"{id}, {(style.Vertical ? "down" : "across")} at {style.Scale * 100:0} %";
                    var own = InkOfFace(gfx, alone, style, 0);
                    var shared = InkOfFace(gfx, among, style, 1);
                    if (MathF.Abs((own.Right - own.Left) - (shared.Right - shared.Left)) > 1 || MathF.Abs((own.Bottom - own.Top) - (shared.Bottom - shared.Top)) > 1)
                    {
                        failures.Add($"{where}: {own.Right - own.Left:0} by {own.Bottom - own.Top:0} px of ink alone, {shared.Right - shared.Left:0} by {shared.Bottom - shared.Top:0} among others");
                    }
                }
            }
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} faces drawn otherwise among others:\n{string.Join("\n", failures)}");
    }
}
