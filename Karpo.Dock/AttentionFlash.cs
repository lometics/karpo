// signals: an app's button asking for attention (FlashWindowEx), as Windows 11's taskbar shows it: the
// button blinks a few times — Windows' foreground flash count (SPI_GETFOREGROUNDFLASHCOUNT, 7 unless
// changed), a beat each the caret's blink time —, then stays lit until the window is activated or its
// app stops the flashing. Off where Windows' own "Show flashing on taskbar apps" is (TaskbarFlashing = 0).
//
// What the shell hook says of a flashing window, measured on a desktop of its own with a window that
// flashed (FlashProbe, 2026-10-26): each beat of the flashing is a message, HSHELL_FLASH (0x8006) as it
// lights and HSHELL_REDRAW (6) as it goes off, at the caret's blink time (530 ms); a count of them
// ends lit, on an HSHELL_FLASH, and nothing more comes; FLASHW_STOP is an HSHELL_REDRAW, and nothing
// more comes. A title changing is an HSHELL_REDRAW too, flashing or not. No call reads a window's
// flashing, so it is told from that: a redraw that changed nothing of the window (its title the same)
// may be the flashing going off; it has stopped unless the next flash comes within SettleMs — two
// beats and a little, so a flashing still going on always lights again first. A title change is never
// a stop. Not told apart: a flashing stopped right after the title changed sends nothing, and the
// button stays lit until the window is activated, as it does after a count.

namespace Karpo.Dock;

internal static class AttentionFlash
{
    /// <summary>How long after a redraw that may be the flashing going off it counts as stopped, no flash having come.</summary>
    public const long SettleMs = 1200;

    /// <summary>
    /// Whether the button is lit <paramref name="elapsed"/> ms after the attention began: lit and dark in
    /// turn, a <paramref name="beatMs"/> each, <paramref name="count"/> times lit, then lit for good.
    /// </summary>
    public static bool Lit(long elapsed, int count, uint beatMs)
    {
        if (count <= 0 || beatMs == 0 || elapsed < 0)
        {
            return true;
        }
        var beat = elapsed / beatMs;
        return beat >= count * 2L - 1 || beat % 2 == 0;
    }

    /// <summary>Whether the button still blinks <paramref name="elapsed"/> ms after the attention began, so it is drawn again on the next beat.</summary>
    public static bool Blinking(long elapsed, int count, uint beatMs) =>
        count > 0 && beatMs > 0 && elapsed >= 0 && elapsed < (count * 2L - 1) * beatMs;

    /// <summary>Whether the attention just began: within its first beat, when the button bounces once.</summary>
    public static bool Began(long elapsed, uint beatMs) => elapsed >= 0 && elapsed < Math.Max(beatMs, 1u);

    /// <summary>Whether a flashing that may have stopped at <paramref name="offAt"/> (0 for none) has, at <paramref name="now"/>.</summary>
    public static bool Stopped(long offAt, long now) => offAt != 0 && now - offAt >= SettleMs;

    /// <summary>
    /// How long until a button asking for attention since <paramref name="elapsed"/> ms, whose flashing
    /// may have stopped <paramref name="off"/> ms ago (null for not), next has to be looked at: its next
    /// beat while it blinks, the end of the settle while a stop is waited on; null for never — it is
    /// lit for good, and only a message changes that.
    /// </summary>
    public static long? NextMs(long elapsed, long? off, int count, uint beatMs)
    {
        long? next = null;
        if (Blinking(elapsed, count, beatMs))
        {
            next = beatMs - elapsed % beatMs;
        }
        if (off is { } since)
        {
            var settle = Math.Max(1, SettleMs - since);
            next = next is { } beat ? Math.Min(beat, settle) : settle;
        }
        return next;
    }
}
