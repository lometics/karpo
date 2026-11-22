// The config folder's lock (ConfigFile.cs, FileLock) held by another process past a save's patience: a
// save in the middle of its read, merge and write, held up or hung. A write now would be lost under its
// own, or lose it, so nothing is written until it lets go.

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class LockTests : IDisposable
{
    private readonly int patience = Config.LockPatience;

    public LockTests()
    {
        Files.Reset();
        Config.LockPatience = 200;
    }

    public void Dispose()
    {
        Config.LockPatience = patience;
        Files.Reset();
    }

    /// <summary>Regression: a save that waited its patience for the lock went ahead without it, and another process's save under way could come down on top of it.</summary>
    [Fact]
    public void ASaveWhileAnotherHoldsTheLockWritesNothingAndSavesOnceItLetsGo()
    {
        var config = Files.Start();
        var before = Files.Text();
        config.TaskbarOpacity = 55;

        using (var held = new HeldLock())
        {
            Assert.Throws<IOException>(() => config.Save());
            Assert.Equal(before, Files.Text());
        }

        config.Save();
        Assert.Equal(55, Config.Load().TaskbarOpacity);
    }

    /// <summary>A load while another holds the lock reads the file, and writes nothing back: an id it had to make up is written by the next load or save.</summary>
    [Fact]
    public void ALoadWhileAnotherHoldsTheLockReadsAndWritesNothing()
    {
        var text = """{ "layouts": [ { "id": "home", "name": "Home", "islands": [ { "kind": "start" } ] } ], "defaultLayoutId": "home" }""";
        File.WriteAllText(Config.FilePath, text);

        Config config;
        using (var held = new HeldLock())
        {
            config = Config.Load();
        }

        Assert.Equal(text, Files.Text());
        Assert.Single(Karpo.Tests.Core.Configs.Islands(config));
    }

    /// <summary>The lock as another process holds it: owned by a thread of its own (a mutex is the thread's that took it), until disposed.</summary>
    private sealed class HeldLock : IDisposable
    {
        private readonly ManualResetEventSlim taken = new();
        private readonly ManualResetEventSlim done = new();
        private readonly Thread thread;

        public HeldLock()
        {
            var name = Config.LockName;
            thread = new Thread(() =>
            {
                using var mutex = new Mutex(false, name);
                mutex.WaitOne();
                taken.Set();
                done.Wait();
                mutex.ReleaseMutex();
            });
            thread.Start();
            taken.Wait();
        }

        public void Dispose()
        {
            done.Set();
            thread.Join();
            taken.Dispose();
            done.Dispose();
        }
    }
}
