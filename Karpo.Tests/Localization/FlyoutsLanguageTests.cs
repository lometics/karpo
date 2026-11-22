// The dock's flyouts in another language: the clipboard history's line, the drives and folder lists, the
// stock card, the weather's buttons and the web search, each through the code's own functions, so
// a plural picks its form, a place lands where the sentence puts it, and a name a screen reader
// reads is the language's.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Dock.Markets;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class FlyoutsLanguageTests
{
    [Fact]
    public void TheClipboardHistoryIsNamedInGerman()
    {
        using var _ = InLanguage.Use("de");
        Assert.Equal("Zwischenablageverlauf", WidgetInfos.Clipboard.Title);
        Assert.Equal("Alle löschen", S.DockClipboardClearAll);
        Assert.Equal("3 Elemente", S.DockClipboardCount(3));
        Assert.Equal("Löschen", S.DockClipboardDelete);
    }

    [Fact]
    public void ADrivesStateIsSaidInGerman()
    {
        using var _ = InLanguage.Use("de");
        Assert.Equal("Kein Datenträger im Laufwerk", DriveListing.NotReadyLine(DriveKind.Optical));
    }

    [Fact]
    public void AFolderCountsInFrenchAndChinese()
    {
        using (InLanguage.Use("fr"))
        {
            Assert.Equal("Vide", FolderReader.CountOf(0));
            Assert.Equal("1 élément", FolderReader.CountOf(1));
            Assert.Equal("12 éléments", FolderReader.CountOf(12));
            Assert.Equal("Ce dossier est vide", FolderReader.StateLine(new FolderContents { State = FolderListState.Empty }));
        }
        using (InLanguage.Use("zh-Hans"))
        {
            // Chinese writes one and many alike.
            Assert.Equal("1 个项目", FolderReader.CountOf(1));
            Assert.Equal("12 个项目", FolderReader.CountOf(12));
        }
    }

    [Fact]
    public void WhatIsFreeOnADriveIsSaidInFrench()
    {
        using var _ = InLanguage.Use("fr");
        // The size is the units' own (another part of the dock); the word after it is the drive's.
        Assert.EndsWith(" libres", DriveListing.FreeSpace(1024UL * 1024 * 1024 * 931, 1024UL * 1024 * 1024 * 419), StringComparison.Ordinal);
    }

    [Fact]
    public void AShutMarketSaysWhichCloseInSpanish()
    {
        using var _ = InLanguage.Use("es");
        var opens = new DateTime(2026, 9, 23, 13, 30, 0, DateTimeKind.Utc);
        var quote = new MarketQuote
        {
            Symbol = "AAPL",
            SessionOpensUtc = opens,
            SessionClosesUtc = opens.AddHours(6.5),
            TradedUtc = opens.AddHours(6.5),
        };
        Assert.Equal("Al cierre de hoy", MarketReading.MarketOf(quote, opens.AddHours(7)).Line);
        Assert.Equal("Al cierre de ayer", MarketReading.MarketOf(quote, opens.AddHours(24)).Line);
        Assert.StartsWith("Al cierre del ", MarketReading.MarketOf(quote, opens.AddDays(3)).Line, StringComparison.Ordinal);
    }

    [Fact]
    public void WhyAPriceIsMissingIsSaidInTheLanguageOfTheMoment()
    {
        using var _ = InLanguage.Use("de");
        Assert.Equal("Der Kursdienst ist nicht erreichbar", MarketStore.Explain(new HttpRequestException()));
        Assert.Equal("Aktualisieren", S.DockWidgetMenuRefresh);
    }

    [Fact]
    public void TheAddListsSearchIsInFrench()
    {
        using var _ = InLanguage.Use("fr");
        Assert.Equal("Rechercher des widgets", S.DockSearchWidgets);
    }
}
