// The people's pictures (Karpo.Dock\Pim\ContactPhotos.cs, docs\widgets.md §457): fetched once per
// version, kept small and encrypted in a folder of the test's own, taken from there at the next run,
// fetched again for a new version, forgotten for nobody listed — but only once every account has been
// read. Every account here is a FakeReader: no test reaches a server, and none touches the person's
// own folder.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Rss;

namespace Karpo.Tests.Pim;

public sealed class ContactPhotosTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-contact-photos-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Person Someone(string id, string name, string? photo) => new(id, name, null, [], []) { Photo = photo };

    private string[] Files() => Directory.Exists(folder) ? Directory.GetFiles(folder) : [];

    private string FileOf(Person person) => Path.Combine(folder, ContactPhotos.Key(person) + ".bin");

    private async Task<ContactStore> Read(params FakeReader[] readers)
    {
        var store = new ContactStore(new AccountSet(readers), photos: folder);
        store.Refresh();
        await store.Idle();
        return store;
    }

    /// <summary>
    /// The key a picture is kept under is a hash of the person's id and their picture's version: another
    /// version or another person is another key, nobody without a picture has one, and nothing of the
    /// person — not their id, not their name — is in it.
    /// </summary>
    [Fact]
    public void TheKeyIsTheIdAndTheVersionHashed()
    {
        var ruth = Someone("https://dav.example.com/books/me/ruth.vcf", "Ruth Imboden", "v1");

        var key = ContactPhotos.Key(ruth)!;

        Assert.Equal(32, key.Length);
        Assert.True(key.All(char.IsAsciiHexDigitLower));
        Assert.Equal(key, ContactPhotos.Key(ruth with { DisplayName = "Ruth I." }));
        Assert.NotEqual(key, ContactPhotos.Key(ruth with { Photo = "v2" }));
        Assert.NotEqual(key, ContactPhotos.Key(ruth with { Id = "other" }));
        Assert.Null(ContactPhotos.Key(ruth with { Photo = null }));
    }

    /// <summary>
    /// A read hands everyone over: a picture is fetched, cut down and kept encrypted; one who has none is
    /// kept as an empty file, so they are not asked again; one without a version is never asked. The next
    /// run takes them from the disk, asking the account nothing.
    /// </summary>
    [Fact]
    public async Task APictureIsFetchedOnceKeptEncryptedAndTakenFromTheDiskNextRun()
    {
        var ada = Someone("a", "Ada Lovelace", "v1");
        var bea = Someone("b", "Bea Keller", "v1");
        var carl = Someone("c", "Carl Mendes", null);
        var account = new FakeReader(AccountKind.Dav);
        account.People.AddRange([ada, bea, carl]);
        account.Photos["a"] = RssSamples.Bitmap(200, 120, 90);

        using var first = await Read(account);

        Assert.Equal(2, account.PhotoReads);
        var picture = first.Photo(ada);
        Assert.NotNull(picture);
        Assert.Equal(ContactPhotos.Side, Math.Max(picture.Width, picture.Height));
        Assert.Null(first.Photo(bea));
        Assert.Null(first.Photo(carl));
        Assert.Equal(2, Files().Length);
        Assert.Empty(File.ReadAllBytes(FileOf(bea)));
        var kept = File.ReadAllBytes(FileOf(ada));
        // Encrypted for this Windows user: not the pixels, and the pixels once decrypted.
        Assert.NotEqual(ContactPhotos.Packed(picture), kept);
        Assert.Equal(picture.Data, ContactPhotos.Unpacked(PimCache.Unprotect(kept))!.Data);

        var again = new FakeReader(AccountKind.Dav);
        again.People.AddRange([ada, bea, carl]);
        using var second = await Read(again);

        Assert.Equal(0, again.PhotoReads);
        Assert.Equal(picture.Data, second.Photo(ada)!.Data);
        Assert.Null(second.Photo(bea));
    }

    /// <summary>A new picture is a new version: fetched again, and the old one's file goes.</summary>
    [Fact]
    public async Task ANewVersionIsFetchedAgainAndTheOldOneGoes()
    {
        var ada = Someone("a", "Ada Lovelace", "v1");
        var account = new FakeReader(AccountKind.Dav);
        account.People.Add(ada);
        account.Photos["a"] = RssSamples.Bitmap(200);
        using var store = await Read(account);
        var old = FileOf(ada);
        Assert.True(File.Exists(old));

        var changed = ada with { Photo = "v2" };
        account.People[0] = changed;
        account.Photos["a"] = RssSamples.Bitmap(20);
        store.Reread();
        await store.Idle();

        Assert.Equal(2, account.PhotoReads);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(FileOf(changed)));
        Assert.NotNull(store.Photo(changed));
        Assert.Null(store.Photo(ada));
    }

    /// <summary>
    /// An account that fails a picture (offline, signed out) is asked no more in that pass, and nothing of
    /// it is kept — not even "none" — so the next read asks again, and then it comes.
    /// </summary>
    [Fact]
    public async Task AnAccountThatFailsItsPicturesKeepsNothingAndIsAskedAgain()
    {
        var account = new FakeReader(AccountKind.Microsoft);
        account.People.AddRange([Someone("a", "Ada Lovelace", "k1"), Someone("b", "Bea Keller", "k1")]);
        account.Photos["a"] = RssSamples.Bitmap(200);
        account.PhotoFailure = new HttpRequestException("A test's network is down (ContactPhotosTests)");
        using var store = await Read(account);

        Assert.Equal(1, account.PhotoReads);
        Assert.Empty(Files());
        Assert.Null(store.Photo(account.People[0]));

        account.PhotoFailure = null;
        store.Reread();
        await store.Idle();

        Assert.Equal(3, account.PhotoReads);
        Assert.NotNull(store.Photo(account.People[0]));
        Assert.Equal(2, Files().Length);
    }

    /// <summary>
    /// The picture of somebody nobody lists any more goes from the disk — but only once every account was
    /// read: a start where one account cannot be read keeps its people's pictures for when it can, and an
    /// account removed takes its people's pictures with it.
    /// </summary>
    [Fact]
    public async Task PicturesOfNobodyListedGoOnlyOnceEveryAccountWasRead()
    {
        var ada = Someone("a", "Ada Lovelace", "v1");
        var bea = Someone("b", "Bea Keller", "v1");
        FakeReader Home()
        {
            var home = new FakeReader(AccountKind.Dav, "home@example.com");
            home.People.Add(ada);
            home.Photos["a"] = RssSamples.Bitmap(200);
            return home;
        }
        var work = new FakeReader(AccountKind.Microsoft, "work@example.com");
        work.People.Add(bea);
        work.Photos["b"] = RssSamples.Bitmap(40);
        using (await Read(Home(), work))
        {
            Assert.Equal(2, Files().Length);
        }

        work.Failure = new HttpRequestException("A test's network is down (ContactPhotosTests)");
        using (await Read(Home(), work))
        {
            Assert.True(File.Exists(FileOf(bea)), "an account not read keeps its people's pictures");
        }

        using (await Read(Home()))
        {
            Assert.False(File.Exists(FileOf(bea)), "an account removed takes its people's pictures");
            Assert.True(File.Exists(FileOf(ada)));
        }
    }

    /// <summary>A kept file that is not a picture of the right shape reads as nothing kept.</summary>
    [Fact]
    public void AFileThatIsNoPictureIsNothing()
    {
        var pixels = new Pixels { Data = new byte[4 * 3 * 4], Width = 4, Height = 3 };
        Assert.Equal(pixels.Data, ContactPhotos.Unpacked(ContactPhotos.Packed(pixels))!.Data);
        Assert.Null(ContactPhotos.Unpacked([1, 2, 3]));
        Assert.Null(ContactPhotos.Unpacked([.. ContactPhotos.Packed(pixels), 0]));
        var huge = new Pixels { Data = new byte[(ContactPhotos.Side + 1) * 4], Width = ContactPhotos.Side + 1, Height = 1 };
        Assert.Null(ContactPhotos.Unpacked(ContactPhotos.Packed(huge)));
    }
}
