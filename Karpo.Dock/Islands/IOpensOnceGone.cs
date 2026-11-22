namespace Karpo.Dock;

/// <summary>
/// A line whose click chose an address to open in the browser — a ticker's quote, a clock's time
/// (§263, §377) — which the dock opens once the folder has gone (Dock.FolderForward), as a headline's
/// story opens.
/// </summary>
internal interface IOpensOnceGone
{
    /// <summary>Where the address opens: the browser, through the shell. A test puts its own here.</summary>
    Action<string> Opened { get; set; }

    /// <summary>The dock, once the folder has gone: opens what the click chose, once.</summary>
    void Open();
}
