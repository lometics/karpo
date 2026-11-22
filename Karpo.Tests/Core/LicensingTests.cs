// The licence's rules (Karpo.Core\Licensing.cs, docs\widgets.md §138): who is limited and why, what
// counts as a widget, and that a limit only ever refuses an add. The person, 2026-10-11: at most 20
// widgets and 2 layouts without a licence, "the number of widgets should just be the ones that are
// actually shown in the layouts", no other limit.

namespace Karpo.Tests.Core;

public sealed class LicensingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly LicenseStatus Free = new(LicenseTier.Free, LicenseBasis.StoreFree);
    private static readonly LicenseStatus Pro = new(LicenseTier.Pro, LicenseBasis.StorePro);

    private static BarItem Entry(BarKind kind) => new() { Kind = kind, Section = BarSection.End };

    private static TaskbarLayout Layout(LayoutType type, params BarItem[] islands) => new() { Type = type, Islands = [.. islands] };

    private static TaskbarLayout WithWidgets(int count, LayoutType type = LayoutType.Taskbar) =>
        Layout(type, [.. Enumerable.Range(0, count).Select(_ => Entry(BarKind.Volume))]);

    // --- Who is limited --------------------------------------------------------------

    /// <summary>The person's own setup (artifacts\publish, no package) is never limited, whatever the Store would say.</summary>
    [Theory]
    [InlineData(StoreAnswer.NotAsked)]
    [InlineData(StoreAnswer.NotOwned)]
    [InlineData(StoreAnswer.Unavailable)]
    public void ADeveloperBuildIsUnrestricted(StoreAnswer answer)
    {
        var status = LicenseRules.Decide(packaged: false, actsFree: false, answer, null, Now);

        Assert.Equal(LicenseTier.Unrestricted, status.Tier);
        Assert.Equal(LicenseBasis.DeveloperBuild, status.Basis);
        Assert.False(status.IsLimited);
        Assert.Null(LicenseGate.Widgets(status, 200, 201));
        Assert.Null(LicenseGate.Layouts(status, 9));
    }

    /// <summary>KARPO_LICENSE=free makes a developer build the free version; a packaged copy ignores it.</summary>
    [Fact]
    public void KarpoLicenseFreeMakesADeveloperBuildActFree()
    {
        Assert.True(LicenseRules.ActsFree("free"));
        Assert.True(LicenseRules.ActsFree(" FREE "));
        Assert.False(LicenseRules.ActsFree(null));
        Assert.False(LicenseRules.ActsFree("pro"));

        var free = LicenseRules.Decide(packaged: false, actsFree: true, StoreAnswer.NotAsked, null, Now);
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.DeveloperFree), free);
        Assert.True(free.IsLimited);
        // The Store's own "owned" is what a packaged copy goes by.
        Assert.Equal(LicenseTier.Pro, LicenseRules.Decide(packaged: true, actsFree: true, StoreAnswer.Owned, null, Now).Tier);
    }

    /// <summary>
    /// §710: the Store's exe run without its package (taken out of it, or started some way that lost it) is
    /// the free version and says so; it is never the developer build a build of the source is, or anybody's
    /// copy of it would be Pro. Nothing it is told changes that, and in its package the Store decides as ever.
    /// </summary>
    [Theory]
    [InlineData(false, StoreAnswer.NotAsked)]
    [InlineData(true, StoreAnswer.NotAsked)]
    [InlineData(false, StoreAnswer.Owned)]
    public void AStoreBuildWithoutItsPackageIsTheFreeVersion(bool actsFree, StoreAnswer answer)
    {
        Assert.True(LicenseRules.IsStoreBuild("store"));
        Assert.True(LicenseRules.IsStoreBuild(" Store "));
        Assert.False(LicenseRules.IsStoreBuild(null));
        Assert.False(LicenseRules.IsStoreBuild(""));
        Assert.False(LicenseRules.IsStoreBuild("direct"));

        var status = LicenseRules.Decide(packaged: false, storeBuild: true, actsFree, answer, Now.AddDays(-1), Now);

        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreMissing), status);
        Assert.True(status.IsLimited);
        Assert.NotNull(LicenseGate.Widgets(status, LicenseRules.FreeWidgets, LicenseRules.FreeWidgets + 1));
        Assert.Equal(S.SettingsLicenseFreeTitle, LicenseView.Title(status));
        Assert.Equal(S.SettingsLicenseStoreMissing, LicenseView.Detail(status, _ => ""));
        // In its package it is what the Store says, and a build of the source stays what it was.
        Assert.Equal(Pro, LicenseRules.Decide(packaged: true, storeBuild: true, actsFree, StoreAnswer.Owned, null, Now));
        Assert.Equal(Free, LicenseRules.Decide(packaged: true, storeBuild: true, actsFree, StoreAnswer.NotOwned, null, Now));
        Assert.Equal(LicenseStatus.Developer, LicenseRules.Decide(packaged: false, storeBuild: false, actsFree: false, answer, null, Now));
    }

    [Fact]
    public void APackagedCopyIsWhatTheStoreSays()
    {
        Assert.Equal(Pro, LicenseRules.Decide(true, false, StoreAnswer.Owned, null, Now));
        Assert.Equal(Free, LicenseRules.Decide(true, false, StoreAnswer.NotOwned, null, Now));
        // "Not owned" wins over an older confirmation: a refund ends Pro.
        Assert.Equal(Free, LicenseRules.Decide(true, false, StoreAnswer.NotOwned, Now.AddDays(-1), Now));
    }

    /// <summary>Not knowing never unlocks Pro: an unanswered or unreachable Store is the free version, unless a recent confirmation stands in.</summary>
    [Fact]
    public void AnUnknownStoreIsFreeUnlessProWasConfirmedLately()
    {
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.Checking), LicenseRules.Decide(true, false, StoreAnswer.NotAsked, null, Now));
        Assert.Equal(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreUnavailable), LicenseRules.Decide(true, false, StoreAnswer.Unavailable, null, Now));

        var lately = Now.AddDays(-3);
        var grace = LicenseRules.Decide(true, false, StoreAnswer.Unavailable, lately, Now);
        Assert.Equal(LicenseTier.Pro, grace.Tier);
        Assert.Equal(LicenseBasis.OfflineGrace, grace.Basis);
        Assert.Equal(lately + LicenseRules.OfflineGrace, grace.GraceUntil);
        // At the start too, before the Store answers: a Pro user's first add is not refused.
        Assert.Equal(LicenseTier.Pro, LicenseRules.Decide(true, false, StoreAnswer.NotAsked, lately, Now).Tier);
    }

    [Fact]
    public void TheGraceEndsAfterSevenDaysAndIgnoresAFutureDate()
    {
        Assert.Equal(Now.AddDays(-7) + LicenseRules.OfflineGrace, LicenseRules.GraceUntil(Now.AddDays(-7), Now));
        Assert.Null(LicenseRules.GraceUntil(Now.AddDays(-7).AddMinutes(-1), Now));
        Assert.Null(LicenseRules.GraceUntil(null, Now));
        // A clock set back, or the file edited: no grace.
        Assert.Null(LicenseRules.GraceUntil(Now.AddHours(1), Now));
        Assert.NotNull(LicenseRules.GraceUntil(Now.AddMinutes(5), Now));
    }

    /// <summary>A placeholder in the Store id's place unlocks nothing and offers nothing for sale.</summary>
    [Fact]
    public void APlaceholderStoreIdMatchesNoLicence()
    {
        const string placeholder = "PRO-ADDON-STORE-ID";
        Assert.False(LicenseRules.IsStoreId(placeholder));
        Assert.False(LicenseRules.OwnsAddOn([(placeholder, true), (placeholder + "/0010", true)], placeholder));
    }

    /// <summary>The add-on Karpo sells is Partner Center's karpo-pro: a real Store id, owned through its SKU's licence.</summary>
    [Fact]
    public void TheProAddOnIsARealStoreId()
    {
        Assert.Equal("9NT4RFR5R3BN", StoreIds.ProAddOn);
        Assert.True(LicenseRules.IsStoreId(StoreIds.ProAddOn));
        Assert.True(LicenseRules.OwnsAddOn([(StoreIds.ProAddOn + "/0010", true)], StoreIds.ProAddOn));
        Assert.False(LicenseRules.OwnsAddOn([(StoreIds.ProAddOn + "/0010", false)], StoreIds.ProAddOn));
    }

    [Fact]
    public void AnAddOnIsOwnedByAnActiveLicenceOfItsSkus()
    {
        const string id = "9NBLGGH4R315";
        Assert.True(LicenseRules.IsStoreId(id));
        Assert.True(LicenseRules.OwnsAddOn([("9ABCDEFGHIJK/0010", true), (id + "/0010", true)], id));
        Assert.True(LicenseRules.OwnsAddOn([(id, true)], id));
        Assert.False(LicenseRules.OwnsAddOn([(id + "/0010", false)], id));
        // Another product whose id starts the same.
        Assert.False(LicenseRules.OwnsAddOn([(id + "X/0010", true)], id));
        Assert.False(LicenseRules.IsStoreId("9nblggh4r315"));
    }

    [Fact]
    public void TheKeptConfirmationReadsBackAndADamagedOneGivesNothing()
    {
        var confirmed = DateTimeOffset.FromUnixTimeSeconds(Now.ToUnixTimeSeconds());
        Assert.Equal((LicenseCacheState.Valid, confirmed), LicenseCache.Parse(LicenseCache.Format(confirmed)));
        Assert.Equal((LicenseCacheState.Absent, (DateTimeOffset?)null), LicenseCache.Parse(""));
        Assert.Equal(LicenseCacheState.Corrupt, LicenseCache.Parse("karpo-license:v2:1760000000").State);
        Assert.Equal(LicenseCacheState.Corrupt, LicenseCache.Parse("karpo-license:v1:-5").State);
        Assert.Equal(LicenseCacheState.Corrupt, LicenseCache.Parse("karpo-license:v1:99999999999999999999").State);
    }

    // --- What counts -----------------------------------------------------------------

    /// <summary>Pinned apps and Windows' own taskbar buttons are not widgets.</summary>
    [Fact]
    public void WhatWindowsTaskbarHasDoesNotCount()
    {
        var layout = Layout(LayoutType.Taskbar,
            Entry(BarKind.Start), Entry(BarKind.TaskView), Entry(BarKind.Search),
            Entry(BarKind.Pin), Entry(BarKind.Pin), Entry(BarKind.Apps), Entry(BarKind.Tray), Entry(BarKind.Indicators),
            Entry(BarKind.PenMenu), Entry(BarKind.TouchKeyboard), Entry(BarKind.VirtualTouchpad), Entry(BarKind.WidgetsBoard),
            Entry(BarKind.ShowDesktop));

        Assert.Equal(0, LicenseCount.Widgets(layout));
        Assert.Equal(1, LicenseCount.Widgets(Layout(LayoutType.Taskbar, Entry(BarKind.Volume), Entry(BarKind.Pin))));
    }

    /// <summary>A folder counts once, whatever it holds: a ready-made folder is one widget on the bar.</summary>
    [Fact]
    public void AFolderCountsOnceWhateverItHolds()
    {
        var clock = Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        Assert.True(clock.Folder!.Cells.Count() > 1);

        Assert.Equal(1, LicenseCount.Widgets(Layout(LayoutType.Taskbar, clock)));
        Assert.Equal(2, LicenseCount.Widgets(Layout(LayoutType.Taskbar, clock, Entry(BarKind.FolderList))));
    }

    /// <summary>A new install's bar leaves most of the allowance free: it is Windows' taskbar and a few ready-made folders.</summary>
    [Fact]
    public void ANewBarLeavesRoom()
    {
        var bar = Sections.DefaultLayout(["C:\\a.lnk", "C:\\b.lnk", "C:\\c.lnk", "C:\\d.lnk", "C:\\e.lnk", "C:\\f.lnk"],
            new InputHardware(Pen: true, Touch: true, Slate: true), WindowsTaskbar.OutOfTheBox);

        var count = LicenseCount.Widgets(Layout(LayoutType.Taskbar, [.. bar]));

        Assert.InRange(count, 1, LicenseRules.FreeWidgets / 2);
    }

    // --- What is refused -------------------------------------------------------------

    [Fact]
    public void TheTwentyFirstWidgetOnALayoutIsRefusedWithWhy()
    {
        Assert.Null(LicenseGate.Add(Free, WithWidgets(19), BarKind.Volume));
        var why = LicenseGate.Add(Free, WithWidgets(20), BarKind.Volume);
        Assert.Equal(S.CoreLicenseWidgetLimit(limit: 20, count: 20), why);
        // A ready-made folder is a folder on the bar.
        Assert.NotNull(LicenseGate.Add(Free, WithWidgets(20), BarKind.FolderList));
        // What doesn't count is never refused.
        Assert.Null(LicenseGate.Add(Free, WithWidgets(20), BarKind.Pin));
        // Pro: nothing.
        Assert.Null(LicenseGate.Add(Pro, WithWidgets(500), BarKind.Volume));
    }

    /// <summary>Per layout: another layout's widgets leave this one's allowance alone.</summary>
    [Fact]
    public void EachLayoutHasItsOwnAllowance()
    {
        var config = new Config { Layouts = [WithWidgets(20), WithWidgets(3)] };

        Assert.NotNull(LicenseGate.Add(Free, config.Layouts[0], BarKind.Volume));
        Assert.Null(LicenseGate.Add(Free, config.Layouts[1], BarKind.Volume));
    }

    /// <summary>A copy over the limit keeps everything, and may still move, change and remove: only an add that raises the count is refused.</summary>
    [Fact]
    public void OverTheLimitOnlyAddsAreRefused()
    {
        Assert.Null(LicenseGate.Widgets(Free, 30, 30));
        Assert.Null(LicenseGate.Widgets(Free, 30, 29));
        Assert.NotNull(LicenseGate.Widgets(Free, 30, 31));
        // A paste of several is all or nothing.
        Assert.NotNull(LicenseGate.Widgets(Free, 18, 21));
        Assert.Null(LicenseGate.Widgets(Free, 18, 20));
    }

    [Fact]
    public void TheThirdLayoutIsRefusedWithWhy()
    {
        Assert.Null(LicenseGate.Layouts(Free, 1));
        Assert.Equal(S.CoreLicenseLayoutLimit(limit: 2, count: 2), LicenseGate.Layouts(Free, 2));
        Assert.NotNull(LicenseGate.Layouts(Free, 5));
        Assert.Null(LicenseGate.Layouts(Pro, 5));
    }
}
