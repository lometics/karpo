// §641: Start's foot as a line (docs\widgets.md §641): the account's picture in its circle (its initials where it has
// none) and its name, a button whose menu is Start's account menu — Change account settings, Lock, Sign out, Switch user;
// then, at the line's end, the folders beside power (Settings and File Explorer unless the person chose others on its
// page, Start's Folders), each a button opening it, and the power button, whose menu is Start's power menu — Sleep and
// Hibernate where Windows' power menu offers them, Shut down, Restart, and while a restart for updates is due "Update and
// shut down" and "Update and restart" first, the button wearing Windows' orange dot meanwhile. Each command is done once
// the folder has gone, by the power buttons' own code (PowerButtonTile.Run), which a test stands in for.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.StartWidgets;

internal sealed unsafe class StartProfileLine : StartLine, IListContent
{
    /// <summary>The account's button.</summary>
    internal const int AccountSpot = 1;

    /// <summary>The power button.</summary>
    internal const int PowerSpot = 2;

    /// <summary>The first folder's button: each after it.</summary>
    internal const int FirstPlace = 10;

    public StartProfileLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx, config) => Configure(island, config);

    /// <summary>The folders it shows.</summary>
    internal IReadOnlyList<StartPlace> Places => StartPlaces.Of(Settings.Start);

    public float ListHeight(float width, in TileStyle style) => PopupPaint.Round(MathF.Max(style.Px(60), style.Text(48)));

    public override string MainPiece => "account";

    protected override List<Spot> Lay(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var button = style.Px(40);
        var mid = box.top + ((box.bottom - box.top) / 2);
        var top = PopupPaint.Round(mid - (button / 2));
        List<Spot> spots = [];
        var x = right - button;
        spots.Add(new(PowerSpot, PopupPaint.Rect(x, top, button, button), "power", PowerTip));
        var places = Places;
        for (var index = places.Count - 1; index >= 0; index--)
        {
            x -= button + style.Px(4);
            var place = places[index];
            spots.Add(new(FirstPlace + index, PopupPaint.Rect(x, top, button, button), "place " + place, OptionRows.StartPlaceName(place), Target: StartPlaces.Target(place)));
        }
        // §661: the account, the kit's item: its band from the line's leading edge to the folders' first button, top to foot;
        // its picture on the gutter, its name after it.
        spots.Add(new(AccountSpot, new D2D_RECT_F(box.left, box.top, MathF.Max(left + button, x), box.bottom), "account", Source.Account.Name));
        return spots;
    }

    /// <summary>The power button's name, and that a restart for updates is due.</summary>
    private string PowerTip => Source.UpdateDue ? $"{S.WidgetPowerName}\n{S.DockStartUpdateRestart}" : S.WidgetPowerName;

    public override IslandAction Click(int index)
    {
        _ = press.Up();
        return index switch
        {
            AccountSpot or PowerSpot => MenuOf(index),
            >= FirstPlace when index - FirstPlace < Places.Count => Leave(new StartAct(StartActKind.Place, StartPlaces.Target(Places[index - FirstPlace]))),
            _ => default,
        };
    }

    /// <summary>The account's menu or the power menu: the button's whose menu was asked for, else the one under the pointer or the keys.</summary>
    internal override IReadOnlyList<StartMenuRow> MenuRows()
    {
        var spot = menuSpot != 0 ? menuSpot : hot != 0 ? hot : KeyedPart;
        if (spot == AccountSpot)
        {
            return AccountRows();
        }
        return spot == PowerSpot ? PowerRows(Source.Power, Source.UpdateDue) : [];
    }

    /// <summary>Start's account menu.</summary>
    internal static List<StartMenuRow> AccountRows() =>
    [
        new(S.DockStartAccountSettings, Glyphs.Account, new StartAct(StartActKind.Place, "ms-settings:yourinfo").Value),
        StartMenuRow.Line,
        new(S.DockQuickLinkLock, Glyphs.Lock, Power(QuickLink.Lock)),
        new(S.DockQuickLinkSignOut, Glyphs.SignOut, Power(QuickLink.SignOut)),
        new(S.DockStartSwitchUser, Glyphs.SwitchUser, Power(QuickLink.SwitchUser)),
    ];

    /// <summary>Start's power menu on a PC whose power menu offers <paramref name="machine"/>'s, updates first while a restart is due.</summary>
    internal static List<StartMenuRow> PowerRows(QuickLinkMachine machine, bool updateDue)
    {
        List<StartMenuRow> rows = [];
        if (machine.Sleep)
        {
            rows.Add(new(S.DockQuickLinkSleep, Glyphs.Sleep, Power(QuickLink.Sleep)));
        }
        if (machine.Hibernate)
        {
            rows.Add(new(S.DockQuickLinkHibernate, Glyphs.Hibernate, Power(QuickLink.Hibernate)));
        }
        if (updateDue)
        {
            rows.Add(new(S.DockStartUpdateShutDown, Glyphs.Update, Power(QuickLink.UpdateShutDown)));
        }
        rows.Add(new(S.DockQuickLinkShutDown, Glyphs.Power, Power(QuickLink.ShutDown)));
        if (updateDue)
        {
            rows.Add(new(S.DockStartUpdateRestart, Glyphs.Update, Power(QuickLink.UpdateRestart)));
        }
        rows.Add(new(S.DockQuickLinkRestart, Glyphs.Restart, Power(QuickLink.Restart)));
        return rows;
    }

    private static string Power(QuickLink link) => new StartAct(StartActKind.Power, link.ToString()).Value;

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = style.Theme;
        foreach (var spot in Laid(box, style))
        {
            Plate(dc, spot, style, button: spot.Id != AccountSpot);
            var at = spot.Box;
            var glyph = style.Text(16);
            switch (spot.Id)
            {
                case AccountSpot:
                {
                    // §661: as large as the text it holds grows, so the initials stay inside it, on the gutter at every text size.
                    var side = style.Text(32);
                    var circle = PopupPaint.Rect(at.left + ListForm.Gutter(style), PopupPaint.Round(at.top + (((at.bottom - at.top) - side) / 2)), side, side);
                    var account = Source.Account;
                    if (account.Picture is { } picture)
                    {
                        PopupPaint.Covered(dc, picture, circle, side / 2, whole: true);
                    }
                    else
                    {
                        PopupPaint.Circle(dc, new System.Numerics.Vector2((circle.left + circle.right) / 2, (circle.top + circle.bottom) / 2), side / 2, PopupPaint.Ink(theme.Accent));
                        PopupPaint.Centred(dc, Gfx, account.Initials, style.Text(12), Weight.Semibold, theme.TextOnAccent, circle, ink: false);
                    }
                    var px = style.Text(12);
                    var tall = TilePaint.LineHeight(Gfx, px);
                    var left = circle.right + style.Px(12);
                    _ = TilePaint.Line(dc, Gfx, account.Name, px, Weight.Regular, theme.Text, left, PopupPaint.Round(at.top + (((at.bottom - at.top) - tall) / 2)), at.right - left - style.Px(8));
                    break;
                }
                case PowerSpot:
                    PopupPaint.Glyph(dc, Gfx, Glyphs.Power, glyph, theme.Text, at);
                    if (Source.UpdateDue)
                    {
                        // Windows' orange dot: a restart for updates is due.
                        var dot = style.Px(6);
                        PopupPaint.Circle(dc, new System.Numerics.Vector2(at.right - style.Px(10), at.top + style.Px(10)), dot / 2, Gfx.Ink(0.97f, 0.55f, 0.1f, 1));
                    }
                    break;
                default:
                    if (spot.Id >= FirstPlace && spot.Id - FirstPlace < Places.Count)
                    {
                        PopupPaint.Glyph(dc, Gfx, StartPlaces.Glyph(Places[spot.Id - FirstPlace]), glyph, theme.Text, at);
                    }
                    break;
            }
        }
    }
}
