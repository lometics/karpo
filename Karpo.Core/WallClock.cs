namespace Karpo.Core;

/// <summary>
/// How long ago something happened by the wall clock, for what is due again after a while (a fetch,
/// a retry). The wall clock can go back: after a hibernation on a PC whose clock ran fast, Windows
/// sets it right; the person sets it; a time server corrects it. A moment that is then in the future
/// is taken as long ago, so nothing waits for the clock to catch up with it (docs/resilience.md §4).
/// </summary>
public static class WallClock
{
    public static TimeSpan Since(DateTime then, DateTime now) => now >= then ? now - then : TimeSpan.MaxValue;
}
