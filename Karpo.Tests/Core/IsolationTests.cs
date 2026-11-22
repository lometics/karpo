namespace Karpo.Tests.Core;

public sealed class IsolationTests
{
    [Fact]
    public void ConfigLivesInATemporaryFolder()
    {
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Karpo");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Karpo");

        Assert.Equal(Isolation.Folder, Config.Folder);
        Assert.StartsWith(Path.GetTempPath(), Config.Folder, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(roaming, Config.Folder, StringComparer.OrdinalIgnoreCase);
        Assert.StartsWith(Isolation.Folder, Config.LocalFolder, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(local, Config.LocalFolder, StringComparer.OrdinalIgnoreCase);
    }
}
