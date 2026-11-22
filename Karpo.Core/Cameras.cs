// camera: a live webcam or IP camera as a line of a list (docs\widgets.md §331). What is the core's: the
// line's own settings (CameraOptions), how an address is read as one Karpo asks for, which way a stream
// is read (CameraProtocol) — guessed from its address, else from what the camera answers — and the
// public cameras its field offers (KnownCameras). The reading and the drawing are the dock's
// (Karpo.Dock\Cameras).
//
// The ways a camera is read, and what reads them:
//
//   mjpeg     an endless HTTP answer of JPEGs, multipart/x-mixed-replace: Karpo's own reader, WIC decodes
//   snapshot  one JPEG or PNG, asked for again at the line's pace: the same decoding
//   hls       Apple's HTTP live streaming (.m3u8)            \
//   dash      MPEG-DASH (.mpd)                                 | Windows' media stack (MediaPlayer, its
//   video     a plain video file or stream over HTTP (MP4…)    | frame server), what Windows can play
//   rtsp      RTSP (rtsp://), Media Foundation's RTSP source   /  (ASF and MP3 only: no H.264 camera)
//
// A web page (a YouTube video's, a site's camera page) is no stream: where its page names a stream of its
// own (og:video, an .m3u8 address in it) that is read; otherwise the line says it is a page.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>camera: which way a stream is read (see the top of the file). A word this build does not know reads as automatic.</summary>
[JsonConverter(typeof(TolerantEnumConverter<CameraProtocol>))]
public enum CameraProtocol
{
    [JsonStringEnumMemberName("mjpeg")] Mjpeg,
    [JsonStringEnumMemberName("snapshot")] Snapshot,
    [JsonStringEnumMemberName("hls")] Hls,
    [JsonStringEnumMemberName("dash")] Dash,
    [JsonStringEnumMemberName("video")] Video,
    [JsonStringEnumMemberName("rtsp")] Rtsp,
}

/// <summary>camera: how the picture meets the line: filling it, cut at its edges, or whole inside it.</summary>
[JsonConverter(typeof(TolerantEnumConverter<CameraFit>))]
public enum CameraFit
{
    [JsonStringEnumMemberName("fill")] Fill,
    [JsonStringEnumMemberName("whole")] Whole,
}

/// <summary>
/// camera: one camera line's own settings. Nothing is asked of anybody until an address is set, and
/// every field but the address is left out of the file while it is its default.
/// </summary>
public sealed class CameraOptions
{
    /// <summary>The stream's address (http, https or rtsp), as typed; empty = not set. A user name and a password in it are the camera's.</summary>
    public string Url { get; set; } = "";

    /// <summary>Which way it is read where somebody chose; null for automatic (<see cref="Cameras.Guess"/>, then what it answers).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CameraProtocol? Protocol { get; set; }

    /// <summary>A snapshot's pace, in seconds (<see cref="Cameras.Paces"/>); null for <see cref="Cameras.DefaultSeconds"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seconds { get; set; }

    /// <summary>How the picture meets the line; null for <see cref="CameraFit.Fill"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CameraFit? Fit { get; set; }

    /// <summary>False where somebody turned a stream's sound on; null, its default, for muted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Mute { get; set; }

    /// <summary>What the line calls it; null for a known camera's name, else the camera's host.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>The longest a name is kept.</summary>
    public const int MostName = 60;

    [JsonIgnore]
    public bool IsSet => Cameras.Address(Url) is not null;

    /// <summary>As the file keeps it: the address one Karpo asks for or none, a pace one of its steps, a default not written.</summary>
    public void Normalize()
    {
        Url = Cameras.Address(Url) ?? "";
        Protocol = Protocol is { } protocol && Enum.IsDefined(protocol) ? protocol : null;
        Seconds = Seconds is { } seconds && Cameras.NearestPace(seconds) is var pace && pace != Cameras.DefaultSeconds ? pace : null;
        Fit = Fit == CameraFit.Whole ? CameraFit.Whole : null;
        Mute = Mute == false ? false : null;
        var name = (Name ?? "").ReplaceLineEndings(" ").Trim();
        Name = name.Length == 0 ? null : name.Length <= MostName ? name : name[..MostName].TrimEnd();
    }
}

/// <summary>camera: the rules every place that reads or shows a camera shares.</summary>
public static class Cameras
{
    /// <summary>The longest an address is kept.</summary>
    public const int MostAddress = 2048;

    /// <summary>A snapshot's paces, in seconds: five seconds to ten minutes.</summary>
    public static readonly int[] Paces = [5, 10, 30, 60, 300, 600];

    /// <summary>A new line's snapshot pace: a minute — a public camera's picture rarely changes faster.</summary>
    public const int DefaultSeconds = 60;

    /// <summary>The pace nearest <paramref name="seconds"/>.</summary>
    public static int NearestPace(int seconds) => Paces.MinBy(pace => Math.Abs(pace - seconds));

    /// <summary>The pace <paramref name="options"/> asks for a snapshot again at.</summary>
    public static int PaceOf(CameraOptions? options) => NearestPace(options?.Seconds ?? DefaultSeconds);

    /// <summary>A pace as its choice says it: "Every 10 seconds", "Every minute", "Every 5 minutes".</summary>
    public static string PaceName(int seconds) =>
        seconds >= 120 ? S.SettingsOnlineUpdateEveryMinutes(seconds / 60) : PerformanceChoices.Pace(seconds);

    /// <summary>The protocols a person may choose, in the order the choice lists them after Automatic.</summary>
    public static readonly CameraProtocol[] Protocols = [CameraProtocol.Mjpeg, CameraProtocol.Snapshot, CameraProtocol.Hls, CameraProtocol.Dash, CameraProtocol.Video, CameraProtocol.Rtsp];

    /// <summary>A protocol's name in its choice; null is automatic.</summary>
    public static string ProtocolName(CameraProtocol? protocol) => protocol switch
    {
        CameraProtocol.Mjpeg => S.CoreCameraProtocolMjpeg,
        CameraProtocol.Snapshot => S.CoreCameraProtocolSnapshot,
        CameraProtocol.Hls => S.CoreCameraProtocolHls,
        CameraProtocol.Dash => S.CoreCameraProtocolDash,
        CameraProtocol.Video => S.CoreCameraProtocolVideo,
        CameraProtocol.Rtsp => S.CoreCameraProtocolRtsp,
        _ => S.CoreCameraProtocolAuto,
    };

    /// <summary>Whether <paramref name="protocol"/> is a moving picture rather than a picture asked for again.</summary>
    public static bool Moves(CameraProtocol protocol) => protocol != CameraProtocol.Snapshot;

    /// <summary>Whether <paramref name="protocol"/> is played by Windows' media stack, and so may have sound.</summary>
    public static bool Played(CameraProtocol protocol) => protocol is CameraProtocol.Hls or CameraProtocol.Dash or CameraProtocol.Video or CameraProtocol.Rtsp;

    /// <summary>
    /// What a person typed as a camera's address, as one Karpo asks for: trimmed, "https://" put in front
    /// of a bare host ("example.com/cam.jpg"); null where it is no http, https or rtsp address.
    /// </summary>
    public static string? Address(string? typed)
    {
        var text = (typed ?? "").Trim();
        if (text.Length == 0 || text.Length > MostAddress || text.Any(char.IsWhiteSpace))
        {
            return null;
        }
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }
        return Uri.TryCreate(text, UriKind.Absolute, out var url) && Reads(url) ? text : null;
    }

    /// <summary>Whether Karpo asks <paramref name="url"/> for a picture: http, https or one of RTSP's, with a host.</summary>
    private static bool Reads(Uri url) =>
        url.Host.Length > 0 && !url.IsFile && !url.IsUnc && url.Scheme.ToLowerInvariant() is "http" or "https" or "rtsp" or "rtsps" or "rtspt" or "rtspu";

    /// <summary>The camera's host, without "www." and without any user name in the address: "foto-webcam.eu"; empty for none.</summary>
    public static string HostOf(string? address) => Feeds.HostOf(address);

    /// <summary>
    /// Which way <paramref name="address"/> is read, judged by the address alone: its scheme, its file's
    /// ending, the paths IP cameras serve their streams and pictures at. Null where it says nothing; then
    /// what the camera answers decides (<see cref="FromAnswer"/>).
    /// </summary>
    public static CameraProtocol? Guess(string? address)
    {
        if (Address(address) is not { } text || !Uri.TryCreate(text, UriKind.Absolute, out var url))
        {
            return null;
        }
        if (url.Scheme.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
        {
            return CameraProtocol.Rtsp;
        }
        var path = url.AbsolutePath.ToLowerInvariant();
        var query = url.Query.ToLowerInvariant();
        return Path.GetExtension(path) switch
        {
            ".m3u8" or ".m3u" => CameraProtocol.Hls,
            ".mpd" => CameraProtocol.Dash,
            ".mp4" or ".m4v" or ".mov" or ".webm" or ".mkv" or ".ts" or ".m2ts" or ".avi" or ".wmv" => CameraProtocol.Video,
            ".mjpg" or ".mjpeg" => CameraProtocol.Mjpeg,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => CameraProtocol.Snapshot,
            // The paths cameras serve them at: Axis's mjpg/video.cgi, Foscam's videostream.cgi, Panasonic's
            // nphMotionJpeg, mjpg-streamer's ?action=stream; and their stills.
            _ when path.Contains("mjpg") || path.Contains("mjpeg") || path.Contains("motionjpeg") || path.Contains("videostream") || path.Contains("faststream")
                || query.Contains("action=stream") => CameraProtocol.Mjpeg,
            _ when path.Contains("snapshot") || path.Contains("image.cgi") || path.Contains("/jpg/") || path.Contains("still")
                || query.Contains("action=snapshot") => CameraProtocol.Snapshot,
            _ => null,
        };
    }

    /// <summary>Which way <paramref name="options"/>' camera is read before it has answered: as chosen, else as its address says.</summary>
    public static CameraProtocol? Chosen(CameraOptions? options) => options?.Protocol ?? Guess(options?.Url);

    /// <summary>
    /// What a camera's answer says it is: its content type, else its first bytes (<paramref name="head"/>) —
    /// a multipart boundary, a JPEG's or a PNG's start, a playlist's or a manifest's first words.
    /// <see cref="CameraAnswer.Page"/> for a web page, null for anything else.
    /// </summary>
    public static CameraAnswer FromAnswer(string? contentType, ReadOnlySpan<byte> head)
    {
        var type = (contentType ?? "").Trim().ToLowerInvariant();
        var media = type.Split(';')[0].Trim();
        CameraProtocol? said = media switch
        {
            _ when media.StartsWith("multipart/", StringComparison.Ordinal) => CameraProtocol.Mjpeg,
            _ when media.StartsWith("image/", StringComparison.Ordinal) => CameraProtocol.Snapshot,
            "application/vnd.apple.mpegurl" or "application/x-mpegurl" or "audio/mpegurl" or "audio/x-mpegurl" => CameraProtocol.Hls,
            "application/dash+xml" => CameraProtocol.Dash,
            "application/mp4" or "application/octet-stream" => null,
            _ when media.StartsWith("video/", StringComparison.Ordinal) => CameraProtocol.Video,
            _ => null,
        };
        if (said is { } known)
        {
            return new CameraAnswer(known);
        }
        var text = System.Text.Encoding.ASCII.GetString(head[..Math.Min(head.Length, 512)]).TrimStart('﻿', ' ', '\r', '\n', '\t');
        if (head.Length >= 2 && head[0] == 0xFF && head[1] == 0xD8 || head.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
        {
            return new CameraAnswer(CameraProtocol.Snapshot);
        }
        if (text.StartsWith("#EXTM3U", StringComparison.Ordinal))
        {
            return new CameraAnswer(CameraProtocol.Hls);
        }
        if (text.Contains("<MPD", StringComparison.Ordinal))
        {
            return new CameraAnswer(CameraProtocol.Dash);
        }
        if (text.StartsWith("--", StringComparison.Ordinal) && text.Contains("image/", StringComparison.OrdinalIgnoreCase))
        {
            return new CameraAnswer(CameraProtocol.Mjpeg);
        }
        if (head.Length >= 8 && head[4] == (byte)'f' && head[5] == (byte)'t' && head[6] == (byte)'y' && head[7] == (byte)'p')
        {
            return new CameraAnswer(CameraProtocol.Video);
        }
        var page = media is "text/html" or "application/xhtml+xml" || text.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || text.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        return new CameraAnswer(null, page);
    }

    /// <summary>
    /// The boundary a multipart answer names (<c>multipart/x-mixed-replace; boundary=--myboundary</c>),
    /// without its quotes and without the dashes some cameras put in front of it; null for none.
    /// </summary>
    public static string? Boundary(string? contentType)
    {
        foreach (var part in (contentType ?? "").Split(';'))
        {
            var pair = part.Trim();
            if (pair.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
            {
                var value = pair[9..].Trim().Trim('"').TrimStart('-');
                return value.Length > 0 ? value : null;
            }
        }
        return null;
    }

    /// <summary>
    /// A stream a web page names for itself, as its page is read when the address is a page: its og:video
    /// (or og:video:url, og:video:secure_url) where that is a stream Karpo reads, else the first .m3u8 address
    /// written in it; null where it names none. A YouTube page's og:video is another page and is none.
    /// </summary>
    public static string? StreamIn(string html, string page)
    {
        foreach (System.Text.RegularExpressions.Match meta in System.Text.RegularExpressions.Regex.Matches(html,
            """<meta[^>]+property\s*=\s*["']og:video(?::url|:secure_url)?["'][^>]*>""", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var content = System.Text.RegularExpressions.Regex.Match(meta.Value, """content\s*=\s*["']([^"']+)["']""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (content.Success && Absolute(System.Net.WebUtility.HtmlDecode(content.Groups[1].Value), page) is { } video && Guess(video) is not null)
            {
                return video;
            }
        }
        var hls = System.Text.RegularExpressions.Regex.Match(html, """https?:\\?/\\?/[^"'\s<>]+?\.m3u8[^"'\s<>]*""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Written in a script's JSON as often as in the page's markup: its escapes undone.
        return hls.Success
            ? Address(System.Net.WebUtility.HtmlDecode(hls.Value.Replace("\\/", "/", StringComparison.Ordinal).Replace("\\u0026", "&", StringComparison.OrdinalIgnoreCase)))
            : null;
    }

    private static string? Absolute(string link, string page) =>
        Uri.TryCreate(new Uri(page), link.Trim(), out var url) ? Address(url.AbsoluteUri) : null;

    /// <summary>What the line calls the camera: the name given it, a known camera's, else its host.</summary>
    public static string NameOf(CameraOptions? options) =>
        options?.Name is { Length: > 0 } name ? name
        : KnownCameras.Of(options?.Url) is { } known ? known.Name
        : HostOf(options?.Url);

    /// <summary>
    /// The page a click on the line opens: a known camera's own page, else the address itself where it is
    /// an http or https one (a stream a browser shows, or its picture); null for an RTSP camera, which no
    /// browser opens. Never with the user name and password an address may carry.
    /// </summary>
    public static string? PageOf(CameraOptions? options)
    {
        if (KnownCameras.Of(options?.Url) is { } known)
        {
            return known.Page;
        }
        if (Address(options?.Url) is not { } address || !Uri.TryCreate(address, UriKind.Absolute, out var url) || !Feeds.Opens(address))
        {
            return null;
        }
        return url.UserInfo.Length == 0 ? address : new UriBuilder(url) { UserName = "", Password = "" }.Uri.AbsoluteUri;
    }
}

/// <summary>What a camera's answer said it is (<see cref="Cameras.FromAnswer"/>): a protocol, or a web page, or nothing Karpo reads.</summary>
public readonly record struct CameraAnswer(CameraProtocol? Protocol, bool Page = false);

/// <summary>
/// camera (§331): public cameras a camera's field offers after the person's own, in several countries,
/// Switzerland's first; each asked for and seen to answer with its picture or its stream when this was
/// written (2026-10-23). Snapshots from foto-webcam.eu (pictures of the Alps every ten minutes, at 816 px),
/// a live HLS stream and DASH-IF's live test picture.
/// </summary>
public static class KnownCameras
{
    /// <summary>A camera offered: its name, where it is, its stream's address, its page, and the country it is in (empty for none).</summary>
    public sealed record Known(string Name, string Where, string Url, string Page, string Region = "")
    {
        /// <summary>Where it is as its row says it, under its name: "Bad Ragaz · Schweiz", the country in the language Karpo speaks.</summary>
        public string Detail => Region.Length == 0 ? Where : Where + " · " + HolidayPlaces.Name(Region);
    }

    /// <summary>Every one, in the order they are offered among those of the same standing.</summary>
    public static readonly IReadOnlyList<Known> All =
    [
        // Switzerland.
        new("Pizol", "Bad Ragaz", Foto("pizol"), Page("pizol"), "CH"), // not-ui: a camera's name and place
        new("Wildhaus", "Toggenburg", Foto("wildhaus"), Page("wildhaus"), "CH"), // not-ui: a camera's name and place
        new("Hoher Kasten", "Appenzell", Foto("hoherkasten"), Page("hoherkasten"), "CH"), // not-ui: a camera's name and place
        // Germany, Austria, Italy.
        new("Zugspitze", "Garmisch-Partenkirchen", Foto("zugspitze"), Page("zugspitze"), "DE"), // not-ui: a camera's name and place
        new("München", "Bayern", Foto("muenchen"), Page("muenchen"), "DE"), // not-ui: a camera's name and place
        new("Wien", "Wien", Foto("wien"), Page("wien"), "AT"), // not-ui: a camera's name and place
        new("Innsbruck", "Tirol", Foto("innsbruck"), Page("innsbruck"), "AT"), // not-ui: a camera's name and place
        new("Malcesine", "Lago di Garda", Foto("malcesine"), Page("malcesine"), "IT"), // not-ui: a camera's name and place
        new("Meran", "Südtirol", Foto("meran"), Page("meran"), "IT"), // not-ui: a camera's name and place
        // Moving pictures: Pula's forum, live (HLS, WhatsUpCams), and DASH-IF's live test picture (MPEG-DASH).
        new("Pula Forum", "Pula", "https://cdn-004.whatsupcams.com/hls/hr_pula01.m3u8", "https://www.whatsupcams.com/", "HR"), // not-ui: a camera's name and place
        new("DASH-IF Live", "DASH-IF", "https://livesim2.dashif.org/livesim2/segtimeline_1/testpic_2s/Manifest.mpd", "https://livesim2.dashif.org/"), // not-ui: a stream's name and place
    ];

    /// <summary>A foto-webcam.eu camera's picture, 816 px wide: enough for a line, a sixth of the largest's bytes.</summary>
    private static string Foto(string id) => $"https://www.foto-webcam.eu/webcam/{id}/current/816.jpg";

    /// <summary>A foto-webcam.eu camera's page, with its archive and its larger pictures.</summary>
    private static string Page(string id) => $"https://www.foto-webcam.eu/webcam/{id}/";

    /// <summary>The known camera at <paramref name="address"/>; null for any other.</summary>
    public static Known? Of(string? address) =>
        Cameras.Address(address) is { } url ? All.FirstOrDefault(known => string.Equals(known.Url, url, StringComparison.OrdinalIgnoreCase)) : null;

    /// <summary>
    /// <see cref="All"/> as a camera's field offers them to a person in <paramref name="region"/> (the country
    /// Windows' region is set to, for null): their country's first, then the rest in the list's order.
    /// </summary>
    public static IReadOnlyList<OptionFound> For(string? region = null)
    {
        var where = region ?? PublicHolidays.Home();
        return [.. All.OrderBy(known => known.Region.Length > 0 && string.Equals(known.Region, where, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select(known => Row(known.Name, known.Detail, known.Url))];
    }

    /// <summary>A camera as a row of a camera's field: its name, where it is under it, its address what it keeps.</summary>
    public static OptionFound Row(string name, string where, string address) => new(name, where, address) { Glyph = OptionGlyphs.Camera };
}
