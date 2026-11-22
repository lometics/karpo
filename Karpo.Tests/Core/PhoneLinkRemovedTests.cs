// Phone Link, taken out (2026-10-04): "remove phone link widget completely from everywhere. it can
// just be replaced by a user by a pin."
//
// What is held here: no registry, no library page and no shared record has the widget any more.

namespace Karpo.Tests.Core;

public sealed class PhoneLinkRemovedTests
{
    /// <summary>No widget of any view, no record and no page of the library has it.</summary>
    [Fact]
    public void NoRegistryOrLibraryHasIt()
    {
        Assert.Null(BarEntries.Find("phoneLink"));
        Assert.Null(ListWidgets.Find("phoneLink"));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "phoneLink" || info.Title == "Phone Link");
        foreach (var page in Catalogue.Pages)
        {
            Assert.DoesNotContain(Catalogue.On(page), entry => entry.Id == "phoneLink");
        }
    }
}
