// What a pin stands for on the bar and in folders: its target resolved to a
// name, an icon and the identities its app's windows carry (Item.Resolve),
// kept by target, since asking the shell costs a few milliseconds a pin and
// the answers don't change while the bar is rebuilt. A target this PC doesn't
// have (PinTargets.Check: a file, a folder or an app that isn't here, as after
// settings brought from another PC) is still a pin: a broken one, drawn dimmed
// with a warning badge, whose click explains and offers to remove or relocate it.
//
// §668: a broken answer is kept for a few seconds only (PinCache.RetryMs), then asked again. The
// shell has no answer for a pin at times it will have one moments later — the Apps folder at a
// logon, a package while Windows re-registers it, a drive still coming —, and an answer kept for
// the dock's life left that pin broken until the next restart or settings change (the person,
// 2026-11-12: "sometimes the pins are lost"). A whole answer is never asked again. And a broken
// pin still holds its app's windows by what its target names (Items.TargetKeys): never are they
// shown among the open apps as if nothing pinned them.
//
// The cache is one for the whole process and more than one thread asks it — the dock's, and a
// test's, or anything that resolves a pin off the dock's thread — so it is a concurrent one: a
// plain dictionary written from two threads at once corrupts (PinCacheTests). Two threads asking
// for the same target at once may both resolve it; either answer is a right one.

using System.Collections.Concurrent;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// Pins' answers, kept by target (see the note at the top of the file). <paramref name="check"/> says
/// whether this PC has a target, <paramref name="shell"/> resolves one (null: the shell can't),
/// <paramref name="clock"/> tells the time in ms. Tests stand in all three.
/// </summary>
internal sealed class PinCache(Func<string, TargetCheck> check, Func<string, Item?> shell, Func<long> clock)
{
    /// <summary>How long a broken answer stands before the shell is asked again (ms).</summary>
    public const long RetryMs = 5_000;

    private readonly ConcurrentDictionary<string, (Item Item, long At)> known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The item <paramref name="pin"/> stands for, with its own name and picture when it has them.</summary>
    public Item For(PinOptions? pin)
    {
        var item = Get(pin?.Target?.Trim() ?? "");
        if (pin is { Name.Length: > 0 } named)
        {
            item = item with { Name = named.Name };
        }
        if (pin is { Icon.Length: > 0 } drawn)
        {
            item = item with { Picture = drawn.Icon };
        }
        return item;
    }

    /// <summary>
    /// Forgets what isn't among <paramref name="targets"/> (pins no bar shows any more), and what was
    /// broken, so a drive plugged in or an app installed meanwhile makes its pins whole again at once.
    /// </summary>
    public void Keep(IEnumerable<string> targets)
    {
        var wanted = targets.Select(target => target.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var kept in known)
        {
            if (!wanted.Contains(kept.Key) || kept.Value.Item.Broken is not null)
            {
                // The answer seen, and only that one: another thread may have just put a new one in.
                _ = known.TryRemove(kept);
            }
        }
    }

    /// <summary>The answer kept for <paramref name="target"/>: a whole one always, a broken one for <see cref="RetryMs"/>.</summary>
    private Item Get(string target)
    {
        var now = clock();
        if (known.TryGetValue(target, out var kept) && (kept.Item.Broken is null || now - kept.At < RetryMs))
        {
            return kept.Item;
        }
        var item = Resolve(target);
        known[target] = (item, now);
        return item;
    }

    /// <summary>
    /// A pin's target as the bar takes it: the shell's item, or a broken one when this PC doesn't have
    /// it (or the shell can't find it), named by its file name or the text as written, holding the
    /// windows of what its target names.
    /// </summary>
    private Item Resolve(string target)
    {
        if (target.Length == 0)
        {
            return new Item { Target = "", Name = PinItems.EmptyName, Kind = ItemKind.Link, Keys = [], Broken = S.DockPinNothingYet };
        }
        // §643: the samples' made-up share, which only a ghost names: never asked of the network.
        if (Samples.OnShare(target))
        {
            return Samples.ShareItem(target);
        }
        var checkedNow = check(target);
        if (!checkedNow.Missing && shell(target) is { } found)
        {
            return found;
        }
        return new Item
        {
            Target = target,
            // A packaged app that isn't here by its package's name, a shortcut without ".lnk".
            Name = PinTargets.NameOf(target),
            Kind = ItemKind.Shell,
            Keys = Items.TargetKeys(target),
            Broken = checkedNow.Missing ? checkedNow.Reason : S.DockPinNotFound,
        };
    }
}

internal static class PinItems
{
    private static readonly PinCache Shared = new(target => PinTargets.Check(target, PinTargets.System), Item.Resolve, () => Environment.TickCount64);

    /// <summary>What the Name of a pin without a target says, and its tooltip.</summary>
    public static string EmptyName => S.DockPinChoose;

    /// <summary>The item <paramref name="pin"/> stands for, with its own name and picture when it has them.</summary>
    public static Item For(PinOptions? pin) => Shared.For(pin);

    /// <summary>Forgets what isn't among <paramref name="targets"/> (pins no bar shows any more), and what was broken.</summary>
    public static void Keep(IEnumerable<string> targets) => Shared.Keep(targets);

    /// <summary>
    /// §380: the name Windows gives <paramref name="target"/>, as a pin to it says it — a shortcut's without
    /// ".lnk", an app's as Start names it, a known folder's as File Explorer does —, resolved once as a pin's
    /// is; the one it is called before Windows is asked where Windows has none.
    /// </summary>
    public static string NameOf(string target) => target.Trim().Length == 0 ? "" : For(new PinOptions { Target = target }).Name;

    /// <summary>
    /// The program files dropped on a pin of <paramref name="target"/> open with: the target itself
    /// when it is a program, the program a shortcut points at on this PC; null otherwise (a folder, a
    /// document, a link, a packaged app).
    /// </summary>
    public static string? Program(string target)
    {
        if (Items.IsExe(target))
        {
            return target;
        }
        if (!Items.IsShortcut(target))
        {
            return null;
        }
        try
        {
            var bytes = File.ReadAllBytes(target);
            return PinTargets.ShortcutTarget(bytes, Path.GetDirectoryName(target)) is { } points && Items.IsExe(points) && File.Exists(Environment.ExpandEnvironmentVariables(points))
                ? Environment.ExpandEnvironmentVariables(points)
                : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
