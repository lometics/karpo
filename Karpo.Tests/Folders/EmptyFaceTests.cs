// A face on the bar with nothing to show (docs\widgets.md §388; the person, 2026-10-29: "an empty face on the bar
// is its glyph and "–", no words like "No calendar""). A face reads a reading or nothing: its glyph and a dash
// while it has none — a stock with no symbol, a drive or a share with none chosen, a team not followed yet, a
// feed with no address, the meeting with no calendar — never a sentence. At a 48 DIP bar, 100 % and 150 %, dark;
// on its side too. Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class EmptyFaceTests
{
    private static readonly FaceInfo[] Empty = [FolderFaces.Markets, FolderFaces.Drive, FolderFaces.NetworkDrive, FolderFaces.Sports, FolderFaces.News, FolderFaces.Meeting];

    [UiFact]
    public void AnEmptyFaceIsItsGlyphAndADash()
    {
        var wrong = new List<string>();
        OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            try
            {
                foreach (var face in Empty)
                {
                    var holder = BarKind.FolderList.New();
                    holder.Id = face.Id;
                    holder.Folder!.Face = FolderFace.Of(face.Id);
                    holder.Normalize();
                    using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                    foreach (var (scale, vertical) in new[] { (1f, false), (1.5f, false), (1.5f, true) })
                    {
                        var thickness = 48 * scale;
                        var style = new IslandStyle(scale, vertical, thickness, MathF.Round(thickness / 2), ListFormPictures.LookFor(true));
                        var (width, height) = island.CellSize(0, style);
                        var said = new List<string>();
                        Bitmap(width, height, dc =>
                        {
                            PaintWatch.Said = (text, _) => said.Add(text);
                            try
                            {
                                island.PaintCell(dc, 0, style, width, height);
                            }
                            finally
                            {
                                PaintWatch.Said = null;
                            }
                        }, _ => { });
                        var words = said.Where(text => text.Length > 0 && text != "–" && !IsGlyph(text)).ToList();
                        if (words.Count > 0)
                        {
                            wrong.Add($"{face.Id} at {scale * 100:0} %{(vertical ? ", on its side" : "")}: says {string.Join(" | ", words)}");
                        }
                    }
                }
            }
            finally
            {
                DriveWatch.Source = before;
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>A glyph of the icon font: one character of the private use area.</summary>
    private static bool IsGlyph(string text) => text.Length <= 2 && text.All(c => c is >= '' and <= '' || char.IsSurrogate(c));
}
