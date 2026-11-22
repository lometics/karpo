// The calendar app "Open Calendar" opens: the app Windows has for outlookcal:, the calendar's own
// link, or for ms-calendar: where nothing has that. A PC can have neither: the old Mail and Calendar
// app gone and nothing else taking its link, or the link's key left behind with no app under it
// (what the new Outlook leaves). The button then opened nothing, so the person asked for it to be
// gone wherever there is no calendar app to open (CalendarLayout's footer).
//
// Windows is asked as Explorer asks before it opens a link (AssocQueryString with
// ASSOCF_IS_PROTOCOL, for its "open" verb): a desktop app's command, a packaged app's id, or a
// handler that runs the command itself (DelegateExecute). A key with none of the three opens nothing.

using System.Runtime.InteropServices;

namespace Karpo.Dock;

internal static unsafe class CalendarApp
{
    /// <summary>The calendar's links, the one Windows' own calendar flyout opens first.</summary>
    internal static readonly string[] Links = ["outlookcal:", "ms-calendar:"];

    [ThreadStatic]
    private static Func<string?>? stand;

    /// <summary>A test's calendar app, per thread: the link it says (null for none); unset, Windows' answer (<see cref="Find"/>).</summary>
    internal static Func<string?>? Stand
    {
        get => stand;
        set => stand = value;
    }

    // Windows' last answer and when it was asked: a line lays its events out on every move of the pointer, and an
    // app taking the links or giving them up is rare, so the answer stands for a few seconds.
    private sealed record Answer(string? Link, long At);

    private static Answer? known;

    /// <summary>The link the calendar app opens by — "Open Calendar"'s, and an event's with nowhere else to open (§434) — or null where this PC has none.</summary>
    public static string? Link()
    {
        if (Stand is { } test)
        {
            return test();
        }
        var now = Environment.TickCount64;
        if (known is not { } was || now - was.At > 5000)
        {
            known = was = new Answer(Find(), now);
        }
        return was.Link;
    }

    /// <summary>
    /// The link to open for the calendar app: the first of <see cref="Links"/> whose scheme
    /// <paramref name="handles"/> says an app takes, or null where none does and there is nothing to
    /// open. <paramref name="handles"/> is Windows (<see cref="Registered"/>) unless a test hands in
    /// its own.
    /// </summary>
    public static string? Find(Func<string, bool>? handles = null)
    {
        handles ??= Registered;
        foreach (var link in Links)
        {
            if (handles(link.TrimEnd(':')))
            {
                return link;
            }
        }
        return null;
    }

    /// <summary>Whether an app takes links of <paramref name="scheme"/> ("outlookcal"), as Windows says.</summary>
    internal static bool Registered(string scheme)
    {
        foreach (var asked in (ReadOnlySpan<uint>)[AssocCommand, AssocAppId, AssocDelegateExecute])
        {
            uint length = 0;
            // The length alone: S_FALSE (1) with it where there is an answer, an error where not.
            // The verb named: without it Windows answers a desktop app's link with "no association".
            if (AssocQueryStringW(AssocIsProtocol, asked, scheme, Verb, null, &length) >= 0 && length > 1)
            {
                return true;
            }
        }
        return false;
    }

    private const string Verb = "open";
    private const uint AssocIsProtocol = 0x1000;
    private const uint AssocCommand = 1;
    private const uint AssocDelegateExecute = 18;
    private const uint AssocAppId = 21;

    [DllImport("shlwapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int AssocQueryStringW(uint flags, uint str, string assoc, string? extra, char* output, uint* length);
}
