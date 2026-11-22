// CalDAV and CardDAV as Karpo reads them (Karpo.Accounts\Dav): a multistatus,
// which collections of a home are calendars with events, discovery's
// candidates, and vCards in the shapes servers actually hold. A calendar's sync
// is DavSyncTests'.

using Karpo.Accounts.Dav;

namespace Karpo.Tests.Accounts;

public sealed class DavTests
{
    private static readonly Uri Home = new("https://dav.example.com/calendars/me/");

    private const string Listing = """
        <?xml version="1.0" encoding="utf-8"?>
        <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:i="http://apple.com/ns/ical/">
          <d:response>
            <d:href>/calendars/me/</d:href>
            <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
          </d:response>
          <d:response>
            <d:href>/calendars/me/work%20stuff/</d:href>
            <d:propstat>
              <d:prop>
                <d:displayname>Work</d:displayname>
                <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
                <c:supported-calendar-component-set><c:comp name="VEVENT"/><c:comp name="VTODO"/></c:supported-calendar-component-set>
                <i:calendar-color>#FF2968FF</i:calendar-color>
              </d:prop>
              <d:status>HTTP/1.1 200 OK</d:status>
            </d:propstat>
            <d:propstat><d:prop><d:getctag/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat>
          </d:response>
          <d:response>
            <d:href>/calendars/me/tasks/</d:href>
            <d:propstat>
              <d:prop>
                <d:displayname>Tasks</d:displayname>
                <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
                <c:supported-calendar-component-set><c:comp name="VTODO"/></c:supported-calendar-component-set>
              </d:prop>
              <d:status>HTTP/1.1 200 OK</d:status>
            </d:propstat>
          </d:response>
          <d:response>
            <d:href>https://dav.example.com/calendars/me/family/</d:href>
            <d:propstat>
              <d:prop><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop>
              <d:status>HTTP/1.1 200 OK</d:status>
            </d:propstat>
          </d:response>
        </d:multistatus>
        """;

    [Fact]
    public void AHomesCalendarsAreTheCollectionsThatHoldEvents()
    {
        var answers = DavClient.Multistatus(Listing, Home);
        var calendars = answers.Select(answer => DavReader.Calendar(answer, Home)).OfType<Karpo.Accounts.RemoteCalendar>().ToList();

        // The home itself is not one of its calendars, and a task list has no events to show.
        Assert.Equal(["Work", "family"], calendars.Select(calendar => calendar.Name));
        // An href's id is its escaped absolute form: two spellings of one resource are one id.
        Assert.Equal("https://dav.example.com/calendars/me/work%20stuff/", calendars[0].Id);
        // Apple's #RRGGBBAA, without the alpha; none where the server gives none.
        Assert.Equal("#FF2968", calendars[0].Colour);
        Assert.Null(calendars[1].Colour);
    }

    [Fact]
    public void APropstatThatFailedGivesNoProperties()
    {
        var answers = DavClient.Multistatus(Listing, Home);
        Assert.DoesNotContain(answers[1].Props, prop => prop.Name.LocalName == "getctag");
        Assert.Empty(DavClient.Multistatus("<html>proxy error</html>", Home));
    }

    [Theory]
    [InlineData("#AABBCC", "#AABBCC")]
    [InlineData(" #aabbccff ", "#aabbcc")]
    [InlineData("#abc", null)]
    [InlineData("red", null)]
    [InlineData(null, null)]
    public void AnAppleColourIsSixHexDigits(string? value, string? colour) =>
        Assert.Equal(colour, DavReader.Colour(value));

    [Fact]
    public void ATypedHostIsTriedAsItsWellKnownAddressThenItsRoot()
    {
        Assert.Equal(
            ["https://cloud.example.com/.well-known/caldav", "https://cloud.example.com/"],
            DavDiscovery.TypedUrls("cloud.example.com", "caldav").Select(url => url.AbsoluteUri));
        Assert.Equal(
            ["https://cloud.example.com/remote.php/dav"],
            DavDiscovery.TypedUrls("https://cloud.example.com/remote.php/dav", "caldav").Select(url => url.AbsoluteUri));
        Assert.Empty(DavDiscovery.TypedUrls("", "caldav"));
    }

    /// <summary>
    /// §616: an href is one key however a server escapes it, whatever its host's case and its default port, so a
    /// sync-collection's spelling and a multiget's meet; a path's own slashes and other hrefs stay apart.
    /// </summary>
    [Fact]
    public void AnHrefIsOneKeyHoweverItIsSpelled()
    {
        var key = DavClient.Key(new Uri("https://dav.example.com/cal/me%40example.com/a%20b.ics"));
        Assert.Equal(key, DavClient.Key(new Uri("https://DAV.Example.com:443/cal/me@example.com/a%20b.ics")));
        Assert.Equal(key, DavClient.Key(new Uri("https://dav.example.com/cal/me%40example.com/a b.ics")));
        Assert.NotEqual(key, DavClient.Key(new Uri("https://dav.example.com/cal/me%40example.com/a%2Fb.ics")));
        Assert.NotEqual(key, DavClient.Key(new Uri("https://dav.example.com:8443/cal/me%40example.com/a%20b.ics")));
        Assert.True(DavClient.Same(new Uri("https://dav.example.com/cal/me@x/"), new Uri("https://dav.example.com/cal/me%40x")));
    }

    /// <summary>§616: a sync-collection's multistatus ends with its token; a refusal's DAV:error names its precondition.</summary>
    [Fact]
    public void AMultistatusEndsWithItsTokenAndARefusalNamesItsPrecondition()
    {
        Assert.Equal("http://example.com/ns/sync/1234", DavClient.Token(
            """<d:multistatus xmlns:d="DAV:"><d:response><d:href>/a.ics</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response><d:sync-token> http://example.com/ns/sync/1234 </d:sync-token></d:multistatus>"""));
        Assert.Null(DavClient.Token("""<d:multistatus xmlns:d="DAV:"/>"""));
        Assert.Null(DavClient.Token("<html>proxy</html"));
        Assert.Equal("valid-sync-token", DavClient.Precondition("""<?xml version="1.0"?><d:error xmlns:d="DAV:"><d:valid-sync-token/></d:error>"""));
        Assert.Null(DavClient.Precondition("<html>Forbidden</html>"));
        Assert.Null(DavClient.Precondition(""));
    }

    [Fact]
    public void AStatusLineIsItsNumber()
    {
        Assert.Equal(404, DavClient.Status("HTTP/1.1 404 Not Found"));
        Assert.Null(DavClient.Status("nonsense"));
    }

    [Fact]
    public void AVCardIsItsNameOrganizationAddressesAndNumbers()
    {
        var person = VCard.Parse(
            "BEGIN:VCARD\r\nVERSION:3.0\r\nN:Schläpfer;Aaron;;;\r\nFN:Aaron Schläpfer\r\nORG:Bühler AG;Engineering\r\n"
            + "item1.EMAIL;type=INTERNET;type=pref:aaron@example.ch\r\nitem1.X-ABLabel:_$!<Work>!$_\r\n"
            + "TEL;TYPE=CELL:079 123 45 67\r\nTEL;VALUE=uri:tel:+41-27-329-14-15\r\nEND:VCARD\r\n",
            "https://dav.example.com/books/me/aaron.vcf")!;

        Assert.Equal("Aaron Schläpfer", person.DisplayName);
        Assert.Equal("Bühler AG", person.Organization);
        Assert.Equal(["aaron@example.ch"], person.Emails);
        Assert.Equal(["079 123 45 67", "+41-27-329-14-15"], person.Phones);
        Assert.Equal("https://dav.example.com/books/me/aaron.vcf", person.Id);
    }

    [Fact]
    public void AVCardWithoutFnIsNamedFromNAndLinesAreUnfolded()
    {
        var person = VCard.Parse(
            "BEGIN:VCARD\nVERSION:4.0\nN:Gerber;Sandra;Maria;Dr.;\nEMAIL:sandra.gerber@exam\n ple.ch\nNOTE:a\\, b\\nc\nEND:VCARD\n",
            "x")!;

        Assert.Equal("Dr. Sandra Maria Gerber", person.DisplayName);
        Assert.Equal(["sandra.gerber@example.ch"], person.Emails);
    }

    [Fact]
    public void AQuotedPrintableVCardFromAnOldPhoneReads()
    {
        var person = VCard.Parse(
            "BEGIN:VCARD\r\nVERSION:2.1\r\nFN;CHARSET=UTF-8;ENCODING=QUOTED-PRINTABLE:J=C3=B6rg M=C3=BC=\r\nller\r\nTEL;CELL:+41791234567\r\nEND:VCARD\r\n",
            "x")!;

        Assert.Equal("Jörg Müller", person.DisplayName);
        Assert.Equal(["+41791234567"], person.Phones);
    }

    [Fact]
    public void AnEmptyCardIsNobody()
    {
        Assert.Null(VCard.Parse("BEGIN:VCARD\r\nVERSION:3.0\r\nNOTE:nothing\r\nEND:VCARD\r\n", "x"));
        Assert.Equal("Air-Glacier SA", VCard.Parse("BEGIN:VCARD\r\nORG:Air-Glacier SA\r\nEND:VCARD\r\n", "x")!.DisplayName);
    }

    // --- A card's picture (docs\widgets.md §457) ----------------------------------------------------------

    /// <summary>Eight bytes standing for a JPEG, and their base64.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

    private static readonly string JpegBase64 = Convert.ToBase64String(Jpeg);

    private static string Card(string photoLine) => $"BEGIN:VCARD\r\nVERSION:3.0\r\nFN:Ruth Imboden\r\n{photoLine}END:VCARD\r\n";

    /// <summary>
    /// PHOTO the three ways servers hold it: 4.0's data: URI, 3.0's base64 under ENCODING=b folded over
    /// lines, 2.1's bare BASE64 parameter — each its bytes — and a URL, which is an address to fetch.
    /// None, an empty one, a URL of another scheme and base64 that is not, are no picture.
    /// </summary>
    [Fact]
    public void APhotoIsADataUriBase64FoldedOrAnAddress()
    {
        Assert.Equal(Jpeg, VCard.Photo(Card($"PHOTO:data:image/jpeg;base64,{JpegBase64}\r\n")).Data);
        Assert.Equal(Jpeg, VCard.Photo(Card($"PHOTO;ENCODING=b;TYPE=JPEG:{JpegBase64[..5]}\r\n {JpegBase64[5..]}\r\n")).Data);
        Assert.Equal(Jpeg, VCard.Photo(Card($"PHOTO;JPEG;ENCODING=BASE64:\r\n  {JpegBase64}\r\n\r\n")).Data);
        Assert.Equal(Jpeg, VCard.Photo(Card($"PHOTO;TYPE=JPEG;BASE64:{JpegBase64}\r\n")).Data);
        var (data, url) = VCard.Photo(Card("PHOTO;VALUE=uri:https://dav.example.com/photos/ruth.jpg\r\n"));
        Assert.Null(data);
        Assert.Equal(new Uri("https://dav.example.com/photos/ruth.jpg"), url);

        Assert.Equal((null, null), VCard.Photo(Card("")));
        Assert.Equal((null, null), VCard.Photo(Card("PHOTO:\r\n")));
        Assert.Equal((null, null), VCard.Photo(Card("PHOTO;VALUE=uri:ftp://example.com/ruth.jpg\r\n")));
        Assert.Equal((null, null), VCard.Photo(Card("PHOTO;ENCODING=b:not base64 at all!\r\n")));
    }

    /// <summary>
    /// A card's read keeps no picture, only its version — a short hash of its PHOTO, so another picture is
    /// another version and the same is the same — and none where the card has no PHOTO.
    /// </summary>
    [Fact]
    public void ACardKeepsItsPhotosVersionNotThePhoto()
    {
        var first = VCard.Parse(Card($"PHOTO;ENCODING=b:{JpegBase64}\r\n"), "x")!;
        var again = VCard.Parse(Card($"PHOTO;ENCODING=b:{JpegBase64[..4]}\r\n {JpegBase64[4..]}\r\n"), "x")!;
        var other = VCard.Parse(Card($"PHOTO;ENCODING=b:{Convert.ToBase64String([1, 2, 3])}\r\n"), "x")!;

        Assert.Equal(16, first.Photo!.Length);
        Assert.DoesNotContain(JpegBase64, first.Photo, StringComparison.Ordinal);
        Assert.Equal(first.Photo, again.Photo);
        Assert.NotEqual(first.Photo, other.Photo);
        Assert.Null(VCard.Parse(Card(""), "x")!.Photo);
    }

    /// <summary>
    /// A CardDAV server for pictures: Ruth's card holds hers, Beat's names one on the server, Carla's one on
    /// another host, Dora's card is gone (404). It writes down who asked for what, signed in or not.
    /// </summary>
    private sealed class Pictures : HttpMessageHandler
    {
        public List<(string Url, bool Signed)> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Asked.Add((url, request.Headers.Authorization is not null));
            var (status, body) = url switch
            {
                "https://dav.example.com/books/me/ruth.vcf" => (System.Net.HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(Card($"PHOTO;ENCODING=b:{JpegBase64}\r\n"))),
                "https://dav.example.com/books/me/beat.vcf" => (System.Net.HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(Card("PHOTO;VALUE=uri:https://dav.example.com/photos/beat.jpg\r\n"))),
                "https://dav.example.com/books/me/carla.vcf" => (System.Net.HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(Card("PHOTO;VALUE=uri:https://pictures.example.net/carla.jpg\r\n"))),
                "https://dav.example.com/photos/beat.jpg" or "https://pictures.example.net/carla.jpg" => (System.Net.HttpStatusCode.OK, Jpeg),
                _ => (System.Net.HttpStatusCode.NotFound, []),
            };
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });
        }
    }

    /// <summary>
    /// A person's picture is their card read again, and the bytes it holds or names: on the account's own
    /// server signed in, on another host as nobody — the password never leaves for a host a card names. A
    /// card gone is no picture, and nobody without a PHOTO is asked about.
    /// </summary>
    [Fact]
    public async Task APhotoIsReadFromTheCardAndThePasswordStaysOnItsServer()
    {
        var server = new Pictures();
        var reader = new DavReader(DavAccount(), () => new DavClient(new HttpClient(server), "me", "secret"), new HttpClient(server));
        Karpo.Accounts.Person Someone(string name, string? photo = "v") =>
            new($"https://dav.example.com/books/me/{name}.vcf", name, null, [], []) { Photo = photo };

        Assert.Equal(Jpeg, await reader.PhotoAsync(Someone("ruth"), CancellationToken.None));
        Assert.Equal(Jpeg, await reader.PhotoAsync(Someone("beat"), CancellationToken.None));
        Assert.Equal(Jpeg, await reader.PhotoAsync(Someone("carla"), CancellationToken.None));
        Assert.Null(await reader.PhotoAsync(Someone("dora"), CancellationToken.None));
        Assert.Null(await reader.PhotoAsync(Someone("ruth", photo: null), CancellationToken.None));

        Assert.Contains(("https://dav.example.com/photos/beat.jpg", true), server.Asked);
        Assert.Contains(("https://pictures.example.net/carla.jpg", false), server.Asked);
        Assert.All(server.Asked.Where(asked => asked.Url.StartsWith("https://dav.example.com/", StringComparison.Ordinal)), asked => Assert.True(asked.Signed));
        Assert.Equal(4, server.Asked.Count(asked => asked.Url.EndsWith(".vcf", StringComparison.Ordinal)));
    }

    // --- A series whatever its time (§615, §616) -----------------------------------------------------------

    /// <summary>
    /// The person, 2026-11-09: "yeah but if a caldav server does not calcualte that we miss that event". A calendar is
    /// held whole now, asked by no time-range: a birthday begun in 2016, a lunch begun in 2015 with an RDATE this month,
    /// a weekly standup and a one-off are all there, and the dock's expansion puts each on its days of November 2026.
    /// </summary>
    [Fact]
    public async Task ASeriesWhateverItsTimeIsHeldAndShown()
    {
        var server = new FakeCalDav().With("family", "Family");
        server.Put("family", "birthday.ics", "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:birthday\r\nSUMMARY:birthday\r\nDTSTART;VALUE=DATE:20161114\r\nDTEND;VALUE=DATE:20161115\r\nRRULE:FREQ=YEARLY\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
        server.Put("family", "standup.ics", FakeCalDav.Event("standup", "standup", new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), "RRULE:FREQ=WEEKLY\r\n"));
        server.Put("family", "dentist.ics", FakeCalDav.Event("dentist", "dentist", new DateTime(2026, 11, 18, 14, 0, 0, DateTimeKind.Utc)));
        server.Put("family", "lunch.ics", FakeCalDav.Event("lunch", "lunch", new DateTime(2015, 3, 10, 12, 0, 0, DateTimeKind.Utc), "RDATE:20261125T120000Z\r\n"));
        var reader = new DavReader(DavAccount(), () => new DavClient(new HttpClient(server), "me", "secret"));
        var november = new DateTime(2026, 11, 1);

        var family = Assert.Single(await reader.CalendarsAsync(november, november.AddMonths(1), CancellationToken.None));

        Assert.Equal(
            ["standup 2026-11-02", "standup 2026-11-09", "birthday 2026-11-14", "standup 2026-11-16", "dentist 2026-11-18", "standup 2026-11-23", "lunch 2026-11-25", "standup 2026-11-30"],
            family.Objects.SelectMany(text => Karpo.Dock.ICalendar.Parse(text).Between(november, november.AddMonths(1), TimeZoneInfo.Utc))
                .OrderBy(each => each.Start).Select(each => $"{each.Title} {each.Start:yyyy-MM-dd}"));
        Assert.DoesNotContain(server.Requests, request => request.EndsWith(" report", StringComparison.Ordinal));
    }

    private static Karpo.Accounts.Account DavAccount() =>
        new() { Id = "d", Kind = Karpo.Accounts.AccountKind.Dav, Address = "me@example.com", CalendarHome = FakeCalDav.Host + FakeCalDav.HomePath };
}
