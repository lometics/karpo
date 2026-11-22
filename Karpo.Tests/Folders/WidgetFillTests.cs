// How many whole items a room holds (Karpo.Dock\Islands\WidgetFill.cs, docs\widgets.md §9, §461).
//
// A line never says what it left out: where it lists items it scrolls (ListScroll.cs), and where it
// lays a form out by the room it has, it shows as many whole items as fit, never half of one, and says
// nothing about the rest. Until §461 the last slot was kept for "12 other apps"; it is gone.

using Karpo.Dock;

namespace Karpo.Tests.Folders;

public sealed class WidgetFillTests
{
    /// <summary>Room for exactly everything shows everything; room to spare is the same answer.</summary>
    [Fact]
    public void RoomForEverythingShowsEverything()
    {
        // Five rows of 20 in exactly 100.
        var exact = WidgetFill.Of(5, 20, 100);
        Assert.Equal(5, exact.Shown);
        Assert.Equal(0, exact.Left);
        Assert.Equal(100, exact.Height, 1);

        var roomy = WidgetFill.Of(5, 20, 400);
        Assert.Equal(5, roomy.Shown);
        Assert.Equal(100, roomy.Height, 1);
    }

    /// <summary>
    /// Room for five of twelve is five whole items — no slot kept for a line saying what became of the
    /// rest (the person, 2026-10-31: "when a folder has more items to show it should actually not say
    /// that but just scroll").
    /// </summary>
    [Fact]
    public void RoomForFiveShowsFiveAndKeepsNoSlotForMore()
    {
        var fill = WidgetFill.Of(12, 20, 100);

        Assert.Equal(5, fill.Shown);
        Assert.Equal(7, fill.Left);
        Assert.Equal(100, fill.Height, 1);
    }

    /// <summary>
    /// Nothing is ever drawn in room that is not there: what is shown always fits, whole, at every count
    /// and every room, and as many as fit are shown.
    /// </summary>
    [Fact]
    public void WhatIsShownAlwaysFits()
    {
        foreach (var item in new float[] { 1, 7, 20, 41.5f })
        {
            for (var count = 0; count <= 30; count++)
            {
                for (var room = 0; room <= 300; room += 7)
                {
                    var fill = WidgetFill.Of(count, item, room);
                    Assert.True(fill.Height <= room + 0.5f, $"{fill.Height} drawn in {room}");
                    Assert.True(fill.Shown >= 0 && fill.Shown <= count);
                    Assert.Equal(count, fill.Shown + fill.Left);
                    Assert.True(fill.Left == 0 || fill.Height + item > room + 0.5f, $"{count} of {item} in {room}: another fits");
                }
            }
        }
    }

    /// <summary>The two forms answer the same question, so rows of one height and rows of several get the same rule.</summary>
    [Fact]
    public void TheEvenAndUnevenFormsAgreeOnEvenRows()
    {
        foreach (var item in new float[] { 12, 20, 33 })
        {
            for (var count = 0; count <= 12; count++)
            {
                var even = new float[count];
                Array.Fill(even, item);
                for (var room = 0; room <= 300; room += 11)
                {
                    var one = WidgetFill.Of(count, item, room);
                    var many = WidgetFill.Of(even.AsSpan(), room);

                    Assert.Equal(one.Shown, many.Shown);
                    Assert.Equal(one.Left, many.Left);
                    Assert.Equal(one.Height, many.Height, 1);
                }
            }
        }
    }

    /// <summary>Rows of different heights are taken in order, as many as fit whole — a notification of four lines beside one of two.</summary>
    [Fact]
    public void UnevenRowsAreTakenInOrder()
    {
        float[] rows = [40, 80, 40, 120, 40];

        var fill = WidgetFill.Of(rows.AsSpan(), 190);
        Assert.Equal(3, fill.Shown);
        Assert.Equal(2, fill.Left);
        Assert.Equal(160, fill.Height, 1);

        var all = WidgetFill.Of(rows.AsSpan(), 320);
        Assert.Equal(5, all.Shown);
        Assert.Equal(320, all.Height, 1);
    }
}
