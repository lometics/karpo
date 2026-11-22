// The cast and nearby sharing faces (docs\widgets.md §114, §115), in place of the cast and nearby
// sharing buttons that went (2026-10-11): each glyph drawn as the status faces beside them are
// (SystemGlyph.Paint), from what the folder's own source reads (FolderIsland.Cast, .Nearby). One
// square cell whatever they show, painted again in place when it changes.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// Cast: Windows' cast glyph, and while this PC shows on a wireless display an accent dot on its
/// bottom corner, as a status that is on (the cast button had none: a click on it was the panel).
/// Not dimmed while nothing is cast to: casting is not a switch that is off, and the folder is
/// where it is done. Its tooltip names the display, or says none is cast to.
/// </summary>
internal sealed unsafe class CastFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.CastId;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        SystemGlyph.Paint(dc, context.Gfx, context.Style, FolderFaces.Cast.Glyph, null, false, null, box);
        if (context.Folder is not { } folder || !Casting(folder.Cast))
        {
            return;
        }
        // On the glyph's bottom corner, by the glyph as drawn (the taskbar's icon size), where its
        // ink — the screen's corner and the waves — leaves room.
        var fit = new Fit(context.Style);
        var side = fit.SystemIcon;
        var dot = MathF.Round(MathF.Max(fit.Px(6), side * 0.36f));
        var middle = (box.left + box.right) / 2;
        var top = (box.top + box.bottom - side) / 2;
        var x = MathF.Round(middle + (side / 2) - (dot * 0.75f));
        var y = MathF.Round(top + side - (dot * 0.75f));
        PopupPaint.Fill(dc, PopupPaint.Rect(x, y, dot, dot), dot / 2, new IslandInk(context.Style.Look).Accent);
    }

    public string Reading(in FaceContext context) => context.Folder is { } folder ? $"{Casting(folder.Cast)}|{Tip(folder.Cast)}" : "";

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => context.Folder is { } folder ? Tip(folder.Cast) : null;

    /// <summary>Whether this PC shows on a wireless display now, as far as it has been read. Pure.</summary>
    internal static bool Casting(CastState? state) => state is { Known: true } read && read.CastTo.Any();

    /// <summary>
    /// What hovering it says: "Cast" and the display cast to, or that none is; its name alone until
    /// that has been read. Null, the folder's own words, where there is no source. Pure.
    /// </summary>
    internal static string? Tip(CastState? state) => state switch
    {
        null => null,
        { Known: false } => S.DockQuickCastTitle,
        _ when state.CastTo.FirstOrDefault() is { } display => $"{S.DockQuickCastTitle}\n{S.DockQuickCastCastingTo(display)}",
        _ => $"{S.DockQuickCastTitle}\n{S.DockQuickCastNotCasting}",
    };
}

/// <summary>
/// Nearby sharing: the share glyph, dimmed while it is off and while Windows has not said, as the
/// Bluetooth face is with its radio off. Its tooltip says whether it is on and who may share.
/// </summary>
internal sealed unsafe class NearbyFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.NearbyId;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    /// <summary>Where it is only measured (no folder), the glyph undimmed, as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        SystemGlyph.Paint(dc, context.Gfx, context.Style, FolderFaces.Nearby.Glyph, null, context.Folder is { } folder && Dim(folder.Nearby), null, box);

    public string Reading(in FaceContext context) => context.Folder is { } folder ? $"{Dim(folder.Nearby)}|{Tip(folder.Nearby)}" : "";

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => context.Folder is { } folder ? Tip(folder.Nearby) : null;

    /// <summary>Dimmed while off or not yet read. Pure.</summary>
    internal static bool Dim(NearbyState? state) => state?.Mode is not (NearbyMode.MyDevices or NearbyMode.Everyone);

    /// <summary>What hovering it says (<see cref="NearbyModes.Tip"/>); null where there is no source. Pure.</summary>
    internal static string? Tip(NearbyState? state) => state is null ? null : NearbyModes.Tip(state.Mode);
}
