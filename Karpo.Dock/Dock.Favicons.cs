// The dock's part of a web pin's picture: hooking the store so a fetch that comes back is drawn,
// and telling it which sites are still pinned.
//
// The store itself is Favicons\FaviconStore.cs. It is touched here first, on the dock's thread, on
// purpose: a store posts its news back through the dispatcher queue of the thread that made it, so
// making it anywhere else would leave a fetched icon sitting in memory with nothing to draw it.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    private bool faviconsHooked;

    /// <summary>
    /// The dock follows a pinned site's icon from the moment it starts. Fetching is the store's
    /// own business and happens only when a bar actually draws a web pin, so a dock with none never
    /// makes a request.
    /// </summary>
    private void HookFavicons()
    {
        if (faviconsHooked)
        {
            return;
        }
        Favicons.FaviconStore.Start().Changed += FaviconArrived;
        faviconsHooked = true;
    }

    /// <summary>
    /// A site's picture arrived. The pins that draw it hold a surface made from what they had
    /// before — the letter tile, or the picture from the last fetch — so that cache is dropped and
    /// the bars are built again.
    /// <para>
    /// Only a picture raises this, never a failure, so a site that is down or has no icon costs no
    /// rebuild at all: the pin keeps its letter and nothing on screen moves.
    /// </para>
    /// </summary>
    private void FaviconArrived()
    {
        if (disposed)
        {
            return;
        }
        icons.Forget();
        Rebuild();
        // §425: and a pin's Website typed in its menu wears it once it is in.
        TargetPictures.Shared.Again();
    }

    /// <summary>
    /// The sites the bars pin now, and the feeds' sites (§306): the store drops the rest from memory.
    /// What is on disk stays, so pinning a site again shows its picture at once instead of fetching
    /// it a second time.
    /// </summary>
    private void KeepFavicons() =>
        Favicons.FaviconStore.Current?.Keep(Folders.All(config.AllIslands)
            .Where(entry => entry.Island.Info == WidgetInfos.Pin && entry.Island.Pin is not null)
            .Select(entry => entry.Island.Pin!.Target)
            .Concat(Rss.RssStore.Shared.Sites()));
}
