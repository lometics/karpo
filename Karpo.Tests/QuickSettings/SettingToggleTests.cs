// Game mode, color filters and mono audio (QuickSettings\Services\SettingToggles.cs): where each stands
// is read from the value Windows keeps, absent being game mode on and the others off; a click goes
// through the Settings handler, color filters writing the widget's filter first; a change made
// elsewhere is what the toggle says next. Made up (a store and a handler of the test's): nothing
// reads or changes this PC's registry.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;

namespace Karpo.Tests.QuickSettings;

public sealed class SettingToggleTests
{
    [Fact]
    public void AValueSaysWhereItStands()
    {
        // Game mode is on until somebody turns it off: Windows 11 writes nothing until then.
        Assert.True(SettingPlace.GameMode.State(null));
        Assert.False(SettingPlace.ColorFilters.State(null));
        Assert.False(SettingPlace.MonoAudio.State(null));
        Assert.False(SettingPlace.GameMode.State(0));
        Assert.True(SettingPlace.MonoAudio.State(1));
        Assert.True(SettingPlace.ColorFilters.State(2));
        // Not a DWORD: not known, rather than a guess.
        Assert.Null(SettingPlace.MonoAudio.State("1"));
        Assert.Null(SettingPlace.MonoAudio.State(new byte[] { 1 }));
    }

    /// <summary>The card offers each of Windows' filters once; each is the number Windows writes for it.</summary>
    [Fact]
    public void TheFiltersAreWindowsOwn()
    {
        Assert.Equal(Enum.GetValues<ColorFilter>().Order(), ColorFilterChoices.All.Order());
        Assert.Equal([0, 1, 2, 3, 4, 5], new[] { ColorFilter.Grayscale, ColorFilter.Inverted, ColorFilter.GrayscaleInverted, ColorFilter.Deuteranopia, ColorFilter.Protanopia, ColorFilter.Tritanopia }.Select(filter => (int)filter));
        Assert.Equal(ColorFilter.Grayscale, ColorFilterChoices.Known((ColorFilter)(-1)));
        Assert.Equal(ColorFilter.Grayscale, ColorFilterChoices.Known((ColorFilter)9));
        Assert.Equal(ColorFilter.Tritanopia, ColorFilterChoices.Known(ColorFilter.Tritanopia));
    }

    /// <summary>
    /// A file names color filters' filter by its word; grayscale is not written, an unknown word reads
    /// as grayscale, and any other switch's is dropped. Everything else in the file loads as it was.
    /// </summary>
    [Fact]
    public void TheFilterLoadsFromAFile()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "c", "kind": "colorFilters", "switch": { "colorFilter": "protanopia", "showLabel": true } },
              { "id": "u", "kind": "colorFilters", "switch": { "colorFilter": "sepia" } },
              { "id": "g", "kind": "gameMode", "switch": { "colorFilter": "inverted" } },
              { "id": "m", "kind": "monoAudio" } ] } ] }
            """);

        var bar = Configs.Islands(config);
        Assert.Equal(["c", "u", "g", "m"], bar.Select(island => island.Id));
        Assert.Equal(ColorFilter.Protanopia, bar[0].Switch!.ColorFilter);
        Assert.True(bar[0].Switch!.ShowLabel);
        Assert.Equal(ColorFilter.Grayscale, bar[1].Switch!.ColorFilter);
        Assert.Equal(ColorFilter.Grayscale, bar[2].Switch!.ColorFilter);
        var json = config.ToJson();
        Assert.Contains("\"protanopia\"", json);
        Assert.DoesNotContain("grayscale", json);
    }

    [Fact]
    public void AClickTurnsColorFiltersOnInTheWidgetsFilter()
    {
        var store = new Store();
        var handler = new Handler(store, "Active");
        using var toggle = Started(SettingPlace.ColorFilters, store, handler);
        Assert.False(toggle.On);
        Assert.True(toggle.Switches);

        toggle.Switch(on: true, ColorFilter.Protanopia);
        // The filter first, then the switch: the filter reads it as it starts.
        Assert.Equal(["FilterType=4", "Active=1"], store.Written);
        Assert.True(toggle.On);

        toggle.Switch(on: false, ColorFilter.Protanopia);
        Assert.Equal(["FilterType=4", "Active=1", "Active=0"], store.Written);
        Assert.False(toggle.On);
    }

    /// <summary>Game mode and mono audio write nothing but the switch, through the handler.</summary>
    [Fact]
    public void TheOthersWriteOnlyTheSwitch()
    {
        var store = new Store();
        var handler = new Handler(store, "AutoGameModeEnabled");
        using var toggle = Started(SettingPlace.GameMode, store, handler);
        Assert.True(toggle.On);
        toggle.Switch(on: false, ColorFilter.Inverted);
        Assert.Equal(["AutoGameModeEnabled=0"], store.Written);
        Assert.False(toggle.On);
    }

    /// <summary>Switched in Windows' own Settings, the toggle says so: the value is followed.</summary>
    [Fact]
    public void AChangeMadeElsewhereIsFollowed()
    {
        var store = new Store();
        using var toggle = Started(SettingPlace.MonoAudio, store, new Handler(store, "AccessibilityMonoMixState"));
        Assert.False(toggle.On);
        store.Values["AccessibilityMonoMixState"] = 1;
        store.Changed!();
        Assert.True(toggle.On);
    }

    /// <summary>
    /// Without the handler nothing here applies it, so nothing is switched: the state still shows, and
    /// the tooltip says a click opens its settings.
    /// </summary>
    [Fact]
    public void WithoutTheHandlerItOnlyShows()
    {
        var store = new Store();
        store.Values["AccessibilityMonoMixState"] = 1;
        using var toggle = Started(SettingPlace.MonoAudio, store, null);
        Assert.True(toggle.On);
        Assert.False(toggle.Switches);
        Assert.Equal(S.DockQuickSettingNotHere, toggle.Note);
        toggle.Switch(on: false, ColorFilter.Grayscale);
        Assert.Empty(store.Written);
    }

    /// <summary>
    /// A click on the widget hands the switch the widget's options: color filters' filter, spatial
    /// sound's format. Regression: the click lost them when the bar's lists went (96e4ad1), and spatial
    /// sound turned on in the format last used whatever its card named.
    /// </summary>
    [UiFact]
    public void AClickHandsOverTheWidgetsOptions()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var heard = new Heard();
            QuickSwitches.Source = _ => heard;
            try
            {
                var entry = BarKind.ColorFilters.New();
                entry.Id = "c";
                entry.Switch!.ColorFilter = ColorFilter.Tritanopia;
                using var island = new SwitchIsland(Config.IslandKey(null, "c"), entry, gfx, new Config());
                _ = island.Click(0);
                Assert.Equal(ColorFilter.Tritanopia, heard.Options?.ColorFilter);
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>A toggle of <paramref name="place"/> over the test's store and handler, once it has looked.</summary>
    private static SettingToggle Started(SettingPlace place, Store store, Handler? handler)
    {
        var toggle = new SettingToggle("Setting", "What it does.", place, store, () => handler);
        var looked = new TaskCompletionSource();
        toggle.Start(() => looked.TrySetResult());
        Assert.True(looked.Task.Wait(TimeSpan.FromSeconds(10)));
        return toggle;
    }

    /// <summary>The values of one key, made up, with what was written in order.</summary>
    internal sealed class Store : ISettingStore
    {
        public Dictionary<string, object> Values { get; } = [];

        public List<string> Written { get; } = [];

        public Action? Changed { get; private set; }

        public object? Read(string name) => Values.GetValueOrDefault(name);

        public bool Write(string name, int data)
        {
            Values[name] = data;
            Written.Add($"{name}={data}");
            return true;
        }

        public IDisposable? Watch(Action changed)
        {
            Changed = changed;
            return null;
        }
    }

    /// <summary>A Settings handler that writes the switch's value, as Windows' do.</summary>
    internal sealed class Handler(Store store, string value) : ISettingToggle
    {
        public bool Usable => true;

        public bool? Value() => store.Read(value) is int data ? data != 0 : null;

        public bool Set(bool on) => store.Write(value, on ? 1 : 0);
    }

    /// <summary>A switch that remembers the options its click came with.</summary>
    private sealed class Heard : IQuickSwitch
    {
        public SwitchOptions? Options { get; private set; }

        public string Name => "Color filters";

        public bool Available => true;

        public bool? On => false;

        public string? Note => null;

        public void Start(Action changed)
        {
        }

        public void Act() => Act(null);

        public void Act(SwitchOptions? options) => Options = options;

        public void Dispose()
        {
        }
    }
}
