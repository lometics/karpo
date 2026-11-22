// One way to say nothing (docs\widgets.md §388 rule 1, §401): a line that has nothing to show — not set up, nothing
// copied, no account, one layout only, no VPN, no apps counted yet — is the kit's one Status row, and a new line of
// it, its height Automatic, is one row tall: no blank room under the sentence. The proposal's states mockup is a
// column of such rows, each 40 DIP. Held on the gutter tests' stand-ins (the isolated run has no accounts, no
// cameras, no VPN, one taskbar layout and nothing counted), at 100 % and 150 %. Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed class StatusRowTests
{
    /// <summary>The kinds the states mockup shows with nothing to show, on the stand-ins (none of them are other forks').</summary>
    private static readonly ListKind[] Nothing =
        [ListKind.Contacts, ListKind.Camera, ListKind.Clipboard, ListKind.Mail, ListKind.Layouts, ListKind.VpnConnections, ListKind.ScreenTimeApps];

    [UiFact]
    public void ALineWithNothingToShowIsOneRow()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var scale in (float[])[1f, 1.5f])
            {
                var look = ListFormPictures.LookFor(dark: true);
                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                var row = LineRows.Px(1, style);
                var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. Nothing.Select(ListGutterTests.Made)]);
                try
                {
                    ListGutterTests.Ready(folder);
                    var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                    for (var index = 0; index < Nothing.Length; index++)
                    {
                        var kind = Nothing[index];
                        var given = ListFormPictures.Given(plan, index);
                        if (MathF.Abs(given - row) > 0.5f)
                        {
                            failures.Add($"{kind} at {scale * 100:0} %: given {given:0} px, one row is {row:0}");
                        }
                        var ink = ListFormPictures.Ink(folder, plan, index, look, scale);
                        if (ink.Bottom > row + 0.5f)
                        {
                            failures.Add($"{kind} at {scale * 100:0} %: inks down to {ink.Bottom:0} px, past its one row of {row:0}");
                        }
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
