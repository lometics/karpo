// Nothing that keeps the bars, their strips, the full screen apps and Explorer's taskbar (§272) ever
// activates a window: a game in exclusive full screen gives the display up the moment it loses the
// activation, and a bar coming back after it must not take the keys from whatever is in front. Read
// off the code itself, so a later change that brings an activation in fails here.

using Karpo.Tests.Localization;

namespace Karpo.Tests.Docking;

public sealed class NoActivationTests
{
    /// <summary>The files the reconcile lives in, whole.</summary>
    private static readonly string[] Files =
    [
        "Karpo.Dock/BarKeeper.cs",
        "Karpo.Dock/AppbarHolder.cs",
        "Karpo.Dock/AppbarShell.cs",
        "Karpo.Dock/AppbarSpace.cs",
        "Karpo.Dock/FrontWatch.cs",
        "Karpo.Dock/FullscreenApps.cs",
        "Karpo.Dock/Taskbar.cs",
    ];

    /// <summary>What activates a window, or shows one with the activation.</summary>
    private static readonly string[] Activating =
    [
        "SetForegroundWindow",
        "SetActiveWindow",
        "BringWindowToTop",
        "SetFocus(",
        "SwitchToThisWindow",
        "SW.SW_SHOW)",
        "SW.SW_SHOWNORMAL",
        "SW.SW_RESTORE",
        "SW.SW_SHOWDEFAULT",
        "SW.SW_SHOWMAXIMIZED",
        "Apps.Activate",
    ];

    [Fact]
    public void TheReconcileNeverActivatesAWindow()
    {
        foreach (var file in Files)
        {
            var text = File.ReadAllText(Path.Combine(StringFiles.Repository, file));
            foreach (var call in Activating)
            {
                Assert.False(text.Contains(call, StringComparison.Ordinal), $"{file} calls {call}");
            }
        }
    }

    /// <summary>The bar making way and coming back (DockWindow.MakeWay) hides and shows without the activation.</summary>
    [Fact]
    public void ABarComingBackFromAFullScreenAppTakesNoActivation()
    {
        var dock = File.ReadAllText(Path.Combine(StringFiles.Repository, "Karpo.Dock/Dock.cs"));
        var start = dock.IndexOf("public void MakeWay(bool on)", StringComparison.Ordinal);
        Assert.True(start > 0);
        var end = dock.IndexOf("// --- What the pointer is over", start, StringComparison.Ordinal);
        var body = dock[start..end];
        // §354: through the window table, whose shows never activate (Surfaces: SWP_NOACTIVATE, the focus asked apart).
        Assert.Contains("Surfaces.Hide(Hwnd)", body);
        Assert.Contains("Surfaces.Show(Hwnd)", body);
        var surfaces = File.ReadAllText(Path.Combine(StringFiles.Repository, "Karpo.Dock/Present/Surfaces.cs"));
        Assert.Contains("SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE | (entry.Show ? SWP.SWP_SHOWWINDOW", surfaces);
        foreach (var call in Activating)
        {
            Assert.False(body.Contains(call, StringComparison.Ordinal), $"MakeWay calls {call}");
        }
    }
}
