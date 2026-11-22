using Karpo.Dock;
using TerraFX.Interop.Windows;
using Xunit;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Pinning;

/// <summary>
/// A packaged app's jump list (Windows.UI.StartScreen.JumpList), as Windows writes it to the app's
/// CustomDestinations file: each line a link to a made-up path (the Desktop folder and the AUMID),
/// named by a BSTR, carrying the AUMID, the arguments it is activated with and its picture's address.
/// The properties are WhatsApp's, recorded from the file on the person's PC (2026-10-11), under an app
/// this PC does not have, so nothing is looked up in a real package.
/// </summary>
public sealed unsafe class JumpListPackagedTests : IDisposable
{
    private const string App = "Contoso.Chat_8wekyb3d8bbwe!App";

    private static readonly Guid AppUserModel = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");

    private readonly string folder = Directory.CreateTempSubdirectory("karpo-jumplist-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Fact]
    public void APackagedAppsLineIsNamedAndActivatesTheApp()
    {
        // Regression: every WhatsApp line was named "5319275A.WhatsAppDesktop_…!App", the made-up
        // path's name, and pointed at that path, which opens nothing.
        var file = Write(Packaged("New chat", "newchat", "ms-appx:///Contoso.Design/Assets/JumpList/NewChat.png"), Packaged("Ana", "lid=1@lid", "ms-appdata:///local/ContactPictures/contact_1.png"));

        var tasks = Assert.Single(JumpLists.Custom(file, 12, 16, default, App)!);

        Assert.Equal(2u, tasks.Kind);
        Assert.Collection(
            tasks.Entries,
            chat =>
            {
                Assert.Equal("New chat", chat.Name);
                Assert.Equal(App, chat.App);
                Assert.Equal("newchat", chat.Arguments);
                Assert.True(chat.IsTask);
                // No package to find its picture in: the menu draws the app's own.
                Assert.Null(chat.Icon);
            },
            contact =>
            {
                Assert.Equal("Ana", contact.Name);
                Assert.Equal("lid=1@lid", contact.Arguments);
                Assert.Equal(App, contact.App);
            });
    }

    [Fact]
    public void ADesktopAppsTaskStaysItsCommandLine()
    {
        // An app's own link with a program and arguments, and its AUMID set so its window groups (as
        // Chrome's are): no activation context, so it is started as written.
        var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
        var file = Write(Desktop("New window", notepad, "--new-window"));

        var task = Assert.Single(Assert.Single(JumpLists.Custom(file, 12, 16, default, App)!).Entries);

        Assert.Equal("New window", task.Name);
        Assert.Null(task.App);
        Assert.Equal(notepad, task.Target, ignoreCase: true);
        Assert.Equal("--new-window", task.Arguments);
    }

    [Theory]
    [InlineData("ms-resource:StandardModeText", "ms-resource://Contoso.Chat/Resources/StandardModeText")]
    [InlineData("ms-resource:///Resources/StandardModeText", "ms-resource://Contoso.Chat/Resources/StandardModeText")]
    [InlineData("ms-resource://Other.App/Resources/Name", "ms-resource://Other.App/Resources/Name")]
    [InlineData("New chat", null)]
    public void ResourceNamesAreLookedUpInTheAppsPackage(string text, string? expected) =>
        Assert.Equal(expected, JumpLists.PackageResource(text, "Contoso.Chat_8wekyb3d8bbwe"));

    [Theory]
    [InlineData("local/ContactPictures/contact_1.png", @"C:\Data\Packages\Contoso.Chat_8wekyb3d8bbwe\LocalState\ContactPictures\contact_1.png")]
    [InlineData("roaming/a%20b.png", @"C:\Data\Packages\Contoso.Chat_8wekyb3d8bbwe\RoamingState\a b.png")]
    [InlineData("temp/x.png", @"C:\Data\Packages\Contoso.Chat_8wekyb3d8bbwe\TempState\x.png")]
    [InlineData("local/../../Other/secret.png", null)]
    [InlineData("elsewhere/x.png", null)]
    [InlineData("local/", null)]
    public void AppDataAddressesAreInThePackagesData(string address, string? expected) =>
        Assert.Equal(expected, JumpLists.AppDataFile(address, "Contoso.Chat_8wekyb3d8bbwe", @"C:\Data"));

    /// <summary>One line to write: a packaged app's when it has an activation context, else a desktop app's.</summary>
    private sealed record Line(string Title, string Program, string Arguments, string? Context, string? Logo);

    /// <summary>A packaged app's line as Windows writes it (the properties recorded from WhatsApp's).</summary>
    private static Line Packaged(string title, string arguments, string logo) =>
        new(title, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), App), "", arguments, logo);

    private static Line Desktop(string title, string program, string arguments) => new(title, program, arguments, null, null);

    private static void Fill(IShellLinkW* link, IPropertyStore* store, Line line)
    {
        fixed (char* path = line.Program)
        fixed (char* text = line.Arguments)
        {
            _ = link->SetPath(path);
            _ = link->SetArguments(text);
        }
        Set(store, AppUserModel, 5, App);
        if (line.Context is not { } context)
        {
            Set(store, PKEY.PKEY_Title.fmtid, PKEY.PKEY_Title.pid, line.Title);
            return;
        }
        Set(store, AppUserModel, 28, "");
        Set(store, AppUserModel, 27, line.Title);
        Set(store, AppUserModel, 30, "");
        Set(store, AppUserModel, 29, line.Logo ?? "");
        Set(store, AppUserModel, 20, context);
        SetBstr(store, PKEY.PKEY_Title, line.Title);
        Set(store, new Guid("436F2667-14E2-4FEB-B30A-146C53B5B674"), 100, context);
    }

    /// <summary>A CustomDestinations file of one Tasks category holding <paramref name="lines"/>.</summary>
    private string Write(params Line[] lines)
    {
        var stream = SHCreateMemStream(null, 0);
        try
        {
            Put(stream, 2);
            Put(stream, 1);
            Put(stream, 0);
            Put(stream, 2);
            Put(stream, (uint)lines.Length);
            foreach (var line in lines)
            {
                var classId = CLSID.CLSID_ShellLink;
                IShellLinkW* link = null;
                Assert.True(CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IShellLinkW>(), (void**)&link).SUCCEEDED);
                IPropertyStore* store = null;
                IPersistStream* persist = null;
                try
                {
                    Assert.True(((IUnknown*)link)->QueryInterface(__uuidof<IPropertyStore>(), (void**)&store).SUCCEEDED);
                    Fill(link, store, line);
                    Assert.True(store->Commit().SUCCEEDED);
                    Assert.True(((IUnknown*)link)->QueryInterface(__uuidof<IPersistStream>(), (void**)&persist).SUCCEEDED);
                    Assert.True(WriteClassStm(stream, &classId).SUCCEEDED);
                    Assert.True(persist->Save(stream, BOOL.TRUE).SUCCEEDED);
                }
                finally
                {
                    if (persist != null)
                    {
                        _ = persist->Release();
                    }
                    if (store != null)
                    {
                        _ = store->Release();
                    }
                    _ = link->Release();
                }
            }
            Put(stream, 0xBABFFBAB);
            STATSTG stat;
            Assert.True(stream->Stat(&stat, (uint)STATFLAG.STATFLAG_NONAME).SUCCEEDED);
            var bytes = new byte[(int)stat.cbSize.QuadPart];
            Assert.True(stream->Seek(default, (uint)STREAM_SEEK.STREAM_SEEK_SET, null).SUCCEEDED);
            uint read;
            fixed (byte* data = bytes)
            {
                Assert.True(stream->Read(data, (uint)bytes.Length, &read).SUCCEEDED);
            }
            var file = Path.Combine(folder, "list.customDestinations-ms");
            File.WriteAllBytes(file, bytes);
            return file;
        }
        finally
        {
            _ = stream->Release();
        }
    }

    private static void Put(IStream* stream, uint value)
    {
        uint written;
        Assert.True(stream->Write(&value, 4, &written).SUCCEEDED);
    }

    private static void Set(IPropertyStore* store, Guid fmtid, uint pid, string text)
    {
        var key = new PROPERTYKEY { fmtid = fmtid, pid = pid };
        PROPVARIANT value = default;
        fixed (char* chars = text)
        {
            Assert.True(InitPropVariantFromString(chars, &value).SUCCEEDED);
        }
        try
        {
            Assert.True(store->SetValue(&key, &value).SUCCEEDED);
        }
        finally
        {
            _ = PropVariantClear(&value);
        }
    }

    /// <summary>The name as Windows writes a packaged app's: a BSTR, not a string the shell allocated.</summary>
    private static void SetBstr(IPropertyStore* store, PROPERTYKEY key, string text)
    {
        PROPVARIANT value = default;
        value.vt = (ushort)VARENUM.VT_BSTR;
        fixed (char* chars = text)
        {
            value.bstrVal = SysAllocString(chars);
        }
        try
        {
            Assert.True(store->SetValue(&key, &value).SUCCEEDED);
        }
        finally
        {
            _ = PropVariantClear(&value);
        }
    }
}
