// Where a face's two lines stand: the time and date's at their end, as Windows' clock has them, in every
// language. Neither a face (§277) nor a line (2026-10-29) has an alignment to pick.
//
// The sheet goes to KARPO_PNG_DIR: align-datetime.png (the time and date face at 100 and 150 %, in English
// and Arabic).
//
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed unsafe class AlignmentPictures
{
    /// <summary>The ink of the upper or the lower half of <paramref name="island"/>'s cell, in the cell's px.</summary>
    private static (float Left, float Top, float Right, float Bottom) HalfInk(FolderIsland island, in IslandStyle style, bool upper)
    {
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        var drawn = style;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () =>
            {
                var half = upper ? new D2D_RECT_F(-margin, -margin, width + margin, height / 2) : new D2D_RECT_F(-margin, height / 2, width + margin, height + margin);
                dc->PushAxisAlignedClip(&half, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                island.PaintCell(dc, 0, drawn, width, height);
                dc->PopAxisAlignedClip();
            }),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        return (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
    }

    /// <summary>
    /// The time and date face's two lines end where each other ends, at the face's end, as Windows'
    /// clock has them (§277: no alignment to pick, none turned from the right): the narrower line's
    /// start is further in than the wider's. On a bar of 48 DIP at 100 and 150 %, in English and Arabic;
    /// align-datetime.png draws them.
    /// </summary>
    [UiFact]
    public void TheTimeAndDateStandAtTheirEnd()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var made = new List<FolderIsland>();
            var items = new List<(string Name, FolderIsland Island, IslandStyle Style)>();
            try
            {
                foreach (var language in new[] { "en", "ar" })
                {
                    using var _ = InLanguage.Use(language);
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var style = FaceCentringTests.Style(48, dpi);
                        var island = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DateTimeId));
                        made.Add(island);
                        items.Add(($"time and date, {dpi} DPI, {language}", island, style));
                        var (time, date) = (HalfInk(island, style, upper: true), HalfInk(island, style, upper: false));
                        if (time.Right <= 0 || date.Right <= 0)
                        {
                            failures.Add($"{dpi} DPI, {language}: not two lines ({time}, {date})");
                        }
                        else if (MathF.Abs(time.Right - date.Right) > 1.5f || MathF.Abs(time.Left - date.Left) < 2f)
                        {
                            failures.Add($"{dpi} DPI, {language}: time {time.Left}..{time.Right}, date {date.Left}..{date.Right}");
                        }
                    }
                }
                FaceCentringTests.Sheet(gfx, items, "align-datetime.png");
            }
            finally
            {
                foreach (var island in made)
                {
                    island.Dispose();
                }
            }
        })));
        Assert.True(failures.Count == 0, $"{failures.Count} out of place:\n{string.Join("\n", failures)}");
    }
}
