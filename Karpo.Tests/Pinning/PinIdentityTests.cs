// §668: a pin is what its target names, whatever the shell answers at one moment. A pin's answer
// (PinCache) that came while the shell couldn't give one — at a logon before the Apps folder is
// up, while a package is being re-registered, a drive not there yet — is asked again a few seconds
// later, never kept for the dock's life; a pin of an app holds that app's windows by the app id its
// target names even then, and whichever folder an update moved the program into; and the bar's
// picture of a pin is kept apart for a broken and a whole one, so a pin that heals is drawn afresh.

using Karpo.Dock;

namespace Karpo.Tests.Pinning;

public sealed class PinIdentityTests
{
    private const string Claude = @"shell:AppsFolder\Claude_pzs8sxrjxfjjc!Claude";

    private const string Squirrel = @"shell:AppsFolder\com.squirrel.AnthropicClaude.claude";

    /// <summary>A stand-in for the shell: answers null (it can't find the target) until <see cref="Ready"/>, and counts the asks.</summary>
    private sealed class Shell
    {
        public bool Ready;
        public int Asked;

        public Item? Resolve(string target)
        {
            Asked++;
            return Ready
                ? new Item { Target = target, Name = "Claude", Kind = ItemKind.Shell, Keys = [.. Items.TargetKeys(target)], Aumid = "Claude_pzs8sxrjxfjjc!Claude" }
                : null;
        }
    }

    private static TargetCheck Unchecked(string target) => new(TargetState.Unchecked, "");

    /// <summary>A window of an app: its key (the app id where it has one) and its program.</summary>
    private static Win Window(string? aumid, string exe) =>
        new() { Key = aumid is null ? Items.ExeKey(exe) : Items.AumidKey(aumid), Aumid = aumid, Exe = exe };

    private static bool Holds(Item pin, Win window) => window.Keys().Any(pin.Matches);

    /// <summary>
    /// The person (2026-11-12): "sometimes the pins are lost, for example the claude one is lost right now". The
    /// pin stayed in the file throughout; the bar had kept the answer the shell gave once — none — for as long as
    /// it ran. Asked again after <see cref="PinCache.RetryMs"/>, it heals; a whole answer is never asked again.
    /// </summary>
    [Fact]
    public void APinTheShellCouldNotAnswerYetIsAskedAgainAndHeals()
    {
        var shell = new Shell();
        var now = 0L;
        var cache = new PinCache(Unchecked, shell.Resolve, () => now);

        Assert.NotNull(cache.For(new PinOptions { Target = Claude }).Broken);
        now = PinCache.RetryMs - 1;
        shell.Ready = true;
        Assert.NotNull(cache.For(new PinOptions { Target = Claude }).Broken);
        Assert.Equal(1, shell.Asked);

        now = PinCache.RetryMs;
        var healed = cache.For(new PinOptions { Target = Claude });
        Assert.Null(healed.Broken);
        Assert.Equal("Claude", healed.Name);

        now = 100 * PinCache.RetryMs;
        Assert.Same(healed, cache.For(new PinOptions { Target = Claude }));
        Assert.Equal(2, shell.Asked);
    }

    /// <summary>
    /// While the shell has no answer the pin is still the app's: it is drawn where it stands (dimmed), and its
    /// windows stand at it, never among the open apps as if it were not pinned.
    /// </summary>
    [Fact]
    public void AnAppPinTheShellCannotFindStillHoldsItsWindowsByItsAppId()
    {
        var cache = new PinCache(Unchecked, new Shell().Resolve, () => 0);

        var pin = cache.For(new PinOptions { Target = Claude });

        Assert.NotNull(pin.Broken);
        Assert.Equal(Claude, pin.Target);
        Assert.True(Holds(pin, Window("Claude_pzs8sxrjxfjjc!Claude", @"C:\Program Files\WindowsApps\Claude_5.1231.2.0_x64__pzs8sxrjxfjjc\app\claude.exe")));
        Assert.False(Holds(pin, Window("OpenAI.Codex_2p2nqsd0c76g0!App", @"C:\Program Files\WindowsApps\OpenAI.Codex_1.0.0.0_x64__2p2nqsd0c76g0\app\ChatGPT.exe")));
    }

    /// <summary>
    /// An Electron app updated by Squirrel moves its program into a new versioned folder at every update; its
    /// pin, by the app id the app sets, holds its windows from the old folder and the new alike.
    /// </summary>
    [Fact]
    public void AnElectronAppsPinHoldsItsWindowsAcrossAnUpdate()
    {
        var shell = new Shell { Ready = true };
        var cache = new PinCache(Unchecked, shell.Resolve, () => 0);

        var pin = cache.For(new PinOptions { Target = Squirrel });

        Assert.True(Holds(pin, Window("com.squirrel.AnthropicClaude.claude", @"C:\Users\A\AppData\Local\AnthropicClaude\app-0.14.10\claude.exe")));
        Assert.True(Holds(pin, Window("com.squirrel.AnthropicClaude.claude", @"C:\Users\A\AppData\Local\AnthropicClaude\app-0.15.0\claude.exe")));
    }

    /// <summary>What a target names by itself: an app of the Apps folder its id, a program its path; nothing for the rest.</summary>
    [Fact]
    public void ATargetNamesItsOwnIdentity()
    {
        Assert.Equal([Items.AumidKey("Claude_pzs8sxrjxfjjc!Claude")], Items.TargetKeys(Claude));
        Assert.Equal([Items.AumidKey("Claude_pzs8sxrjxfjjc!Claude")], Items.TargetKeys("  SHELL:appsfolder\\Claude_pzs8sxrjxfjjc!Claude "));
        Assert.Equal([Items.ExeKey(@"C:\Tools\tool.exe")], Items.TargetKeys(@"C:\Tools\tool.exe"));
        Assert.Empty(Items.TargetKeys(@"shell:AppsFolder\"));
        Assert.Empty(Items.TargetKeys(@"C:\Users\A\Desktop\Notes.txt"));
        Assert.Empty(Items.TargetKeys("https://example.com"));
    }

    /// <summary>
    /// The bar keeps one picture per pin it draws; a pin drawn while broken (dimmed, its first letter) and the same
    /// pin whole are two pictures, so the one that heals is read afresh — as is a pin given a picture of its own.
    /// </summary>
    [Fact]
    public void ABrokenPinsPictureIsKeptApartFromTheWholeOnes()
    {
        var whole = new Item { Target = Claude, Name = "Claude", Kind = ItemKind.Shell, Keys = [] };
        var broken = whole with { Broken = "not found" };
        var own = whole with { Picture = @"C:\Pictures\claude.ico" };

        Assert.NotEqual(Icons.KeyOf(whole, 32), Icons.KeyOf(broken, 32));
        Assert.NotEqual(Icons.KeyOf(whole, 32), Icons.KeyOf(own, 32));
        Assert.Equal(Icons.KeyOf(whole, 32), Icons.KeyOf(whole with { Name = "Other" }, 32));
        Assert.NotEqual(Icons.KeyOf(whole, 32), Icons.KeyOf(whole, 48));
    }
}
