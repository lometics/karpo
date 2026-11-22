// One place decides each spacing, and these fail when a second one appears.
//
// The pattern this guards is the one the person caught by eye on the now-playing card: a number
// written in two places, agreeing right up until one of them does not. Three of them had gone that
// way — a card's inset (one copy), a flyout's margin from the work area (twelve), and the gutter a
// panel keeps at its edge (four) — and in every case the copies agreed at the moment they were
// found, so nothing but somebody's eye would have noticed when they stopped.
//
// Asked of the types, so they fail when the copy is written rather than when a panel looks wrong.
// What that can and cannot see is written on each test; the card's own inset has its own guard in
// Karpo.Tests\Folders\CardRoomTests.ACardsInsetIsWorkedOutInOnePlace.

using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class SpacingHomesTests
{
    /// <summary>
    /// How far a flyout keeps from the ends of the work area is <see cref="FlyoutPlacement"/>'s
    /// answer and nobody else's, and it is one answer: the same at the start, at the end and where a
    /// panel centred on its anchor is pushed against either (§391: an Automatic folder at the screen's
    /// right kept 8 DIP, a Right one 12, and the two did not line up).
    /// <para>
    /// Twelve flyouts each wrote their margin when they placed themselves — the calendar, the clock,
    /// the drives, the removable devices, a folder, the contacts, the notifications, the quick
    /// settings, the screen time, the search, a share and the weather. They now pass the alignment
    /// and nothing else.
    /// </para>
    /// </summary>
    [Fact]
    public void AFlyoutsMarginFromTheWorkAreaIsDecidedInOnePlace()
    {
        static FlyoutPlacement At(FlyoutAlign align) =>
            new(default(POINT), default(RECT), Karpo.Core.Edge.Bottom, Align: align);

        // Windows 11's: 12 DIP in from the screen's sides, as 12 off the taskbar.
        Assert.All(Enum.GetValues<FlyoutAlign>(), align => Assert.Equal(12, At(align).MarginDip));

        // A placement that genuinely wants another margin says so, and that is a caller rather
        // than a copy: it is one argument, at one call, and it does not live in a constant.
        Assert.Equal(20, new FlyoutPlacement(default, default, Karpo.Core.Edge.Bottom, Margin: 20).MarginDip);

        // And the answer is worked out rather than stored, so there is nowhere for a second one
        // to be put: MarginDip has no setter and no backing parameter of its own.
        var margin = typeof(FlyoutPlacement).GetProperty(nameof(FlyoutPlacement.MarginDip));
        Assert.NotNull(margin);
        Assert.Null(margin.SetMethod);
    }

    /// <summary>
    /// The gutter a panel keeps at its edge is <see cref="Widget.PanelPadDip"/>'s answer, and the
    /// only other thing allowed to declare it is the one panel that deliberately keeps a different
    /// one. The weather's header, a share's header, the drives' widgets and the day graph each had
    /// their own <c>PadDip = 16</c>, each saying in its own words that it was the panel's gutter;
    /// all four are gone and their readers name the one constant.
    /// <para>
    /// <b>Membership is by the name that means the quantity, not by a name that looks like it.</b>
    /// An earlier draft of this swept every constant called <c>PadDip</c> and failed on two that
    /// were right: the edit rail's buttons keep 12 and a weather day's row keeps its own inset,
    /// and neither is a panel's gutter. A test that matches on spelling finds what it is looking
    /// for rather than what is there, which is the same mistake one level up as a picture that
    /// agrees with whatever you already believed. So a field is in this audit because it claims
    /// the quantity by name — <c>PanelPadDip</c> — and the two namesakes are out of it by meaning
    /// and not by luck. <c>EditRailPlan.PadDip</c> stayed as it was, correctly named among that
    /// control's own metrics, until the rail's one labelled button went (2026-10-05) and took it
    /// along; <c>WeatherDayRow</c>'s was renamed to <c>InsetDip</c> and written as
    /// a share of the gutter, because its 8 was a silent function of this 16 and would have
    /// drifted the day the gutter moved.
    /// </para>
    /// <para>
    /// <b>There is no exception any more.</b> There was one: <c>ScreenTimeFlyout.PanelPadDip</c>,
    /// 20 and not 16, excluded here by name and in the open. It was never a second home for this
    /// quantity — it is the <i>default of an argument</i> that screen time's components take, and
    /// they pass 0 for a list, which is how a list folder's items reach the panel's border — but
    /// it wore the name of the quantity it is not, which is the same spelling-over-meaning trap as
    /// the two namesakes above, one step along. It is <c>ScreenTimeFlyout.GutterDip</c> now, so
    /// this audit finds one home and carves nothing out. A named exception is a rule with an
    /// asterisk, and the asterisk was only ever the name.
    /// </para>
    /// <para>
    /// <b>How far to trust it.</b> A <c>const</c> is folded into whoever reads it, so this sees
    /// declarations and not uses: a painter that writes <c>context.D(16)</c> inline is invisible
    /// here, and no reflection will ever see it. What catches that is a picture or a measurement,
    /// not a type.
    /// </para>
    /// </summary>
    [Fact]
    public void APanelsGutterIsDecidedInOnePlace()
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
        const string Name = nameof(Widget.PanelPadDip);
        var homes = new List<string>();
        foreach (var type in typeof(Widget).Assembly.GetTypes())
        {
            foreach (var field in type.GetFields(Any))
            {
                if (field.IsLiteral && field.FieldType == typeof(float) && field.Name == Name)
                {
                    homes.Add($"{type.Name}.{field.Name} = {field.GetRawConstantValue()}");
                }
            }
        }

        // One home, and nothing beside it.
        Assert.Equal([$"{nameof(Widget)}.{Name} = {Widget.PanelPadDip}"], homes);
    }
}
