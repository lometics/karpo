// ListMerge: two edits of one ordered list (pins, layout ids, the islands of a
// layout by id) merged, so that neither app's edit undoes the other's.

namespace Karpo.Tests.Persistence;

public sealed class ListMergeTests
{
    static string Merge(string @base, string ours, string theirs) =>
        string.Join(' ', ListMerge.Merge(Split(@base), Split(ours), Split(theirs)));

    static string[] Split(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [InlineData("a b c", "a b c", "a b c", "a b c")]
    [InlineData("a b c", "a b c d", "a b c", "a b c d")]
    [InlineData("a b c", "a b c", "a x b c", "a x b c")]
    [InlineData("a b c", "a c", "a b c", "a c")]
    [InlineData("a b c", "a b c", "b c", "b c")]
    [InlineData("a b c", "c a b", "a b c", "c a b")]
    [InlineData("a b c", "a b c", "b c a", "b c a")]
    [InlineData("", "a", "", "a")]
    [InlineData("a", "", "a", "")]
    public void WhatOnlyOneSideChangedIsTaken(string @base, string ours, string theirs, string merged) =>
        Assert.Equal(merged, Merge(@base, ours, theirs));

    [Fact]
    public void AddsOnBothSidesAreAllKeptOursFirst()
    {
        Assert.Equal("a x y b", Merge("a b", "a x b", "a y b"));
        Assert.Equal("a b x y", Merge("a b", "a b x", "a b y"));
        Assert.Equal("x y", Merge("", "x", "y"));
    }

    [Fact]
    public void TheSameAddOnBothSidesIsKeptOnce() => Assert.Equal("a x b", Merge("a b", "a x b", "a x b"));

    /// <summary>
    /// Regression (round 1's pins data loss): the settings app reordered the pins while the dock
    /// added one, and the settings app's save wrote its list back without the dock's pin.
    /// </summary>
    [Fact]
    public void AReorderOnOneSideAndAnAddOnTheOtherBothStay()
    {
        Assert.Equal("c a b d", Merge("a b c", "c a b", "a b c d"));
        Assert.Equal("c a b d", Merge("a b c", "a b c d", "c a b"));
    }

    [Fact]
    public void ARemovalOnEitherSideWinsOverAMove()
    {
        Assert.Equal("a c", Merge("a b c", "a c", "a c b"));
        Assert.Equal("a c", Merge("a b c", "b a c", "a c"));
    }

    [Fact]
    public void RemovalsOnBothSidesAllHappen() => Assert.Equal("a d", Merge("a b c d", "a c d", "a b d"));

    [Fact]
    public void AnAddNextToARemovalLandsWhereItWasAdded() => Assert.Equal("a y c", Merge("a b c", "a c", "a b y c"));

    [Fact]
    public void MovesOfDifferentEntriesOnBothSidesBothLand()
    {
        Assert.Equal("b a d c", Merge("a b c d", "b a c d", "a b d c"));
        // Ours left c where it was; theirs moved it to the front.
        Assert.Equal("c a b d", Merge("a b c", "a b c d", "c a b"));
    }

    [Fact]
    public void WhereBothMovedTheSameEntryOursWins() => Assert.Equal("b c a", Merge("a b c", "b c a", "b a c"));

    [Fact]
    public void AnEntryBothSidesMovedApartIsPlacedOnce() =>
        // Ours moved a to the end, theirs moved it to the front; both also changed the rest.
        Assert.Equal(1, ListMerge.Merge(Split("a b c d"), Split("b c x d a"), Split("a b y c d")).Count(key => key == "a"));

    /// <summary>
    /// Whatever the edits: every entry is there once, exactly the entries that belong (kept by both,
    /// or added by one), and entries neither side moved keep their order.
    /// </summary>
    [Fact]
    public void RandomEditsKeepExactlyTheRightEntriesOnce()
    {
        var random = new Random(20260922);
        var next = 0;
        for (var round = 0; round < 3000; round++)
        {
            var @base = Enumerable.Range(0, random.Next(0, 9)).Select(_ => $"e{next++}").ToList();
            var ours = Edit(@base, random, ref next);
            var theirs = Edit(@base, random, ref next);

            var merged = ListMerge.Merge(@base, ours, theirs);

            var expected = @base.Where(key => ours.Contains(key) && theirs.Contains(key))
                .Concat(ours.Where(key => !@base.Contains(key)))
                .Concat(theirs.Where(key => !@base.Contains(key)))
                .ToHashSet();
            Assert.Equal(merged.Count, merged.Distinct().Count());
            Assert.True(expected.SetEquals(merged), $"base [{string.Join(' ', @base)}] ours [{string.Join(' ', ours)}] theirs [{string.Join(' ', theirs)}] -> [{string.Join(' ', merged)}]");
            if (ours.SequenceEqual(@base))
            {
                Assert.Equal(theirs, merged);
            }
            if (theirs.SequenceEqual(@base))
            {
                Assert.Equal(ours, merged);
            }
        }
    }

    static List<string> Edit(List<string> list, Random random, ref int next)
    {
        var edited = list.ToList();
        for (var step = random.Next(0, 4); step > 0; step--)
        {
            switch (random.Next(3))
            {
                case 0:
                    edited.Insert(random.Next(edited.Count + 1), $"e{next++}");
                    break;
                case 1 when edited.Count > 0:
                    edited.RemoveAt(random.Next(edited.Count));
                    break;
                case 2 when edited.Count > 1:
                    var from = random.Next(edited.Count);
                    var entry = edited[from];
                    edited.RemoveAt(from);
                    edited.Insert(random.Next(edited.Count + 1), entry);
                    break;
            }
        }
        return edited;
    }

    /// <summary>The alignment the merge lines lists up with: ties keep the earlier entries.</summary>
    [Fact]
    public void AlignFindsTheLongestCommonRunKeepingEarlierEntries()
    {
        Assert.Equal([1, 2, 3, -1], ListMerge.Align(["a", "|", "b", "|"], ["x", "a", "|", "b"], string.Equals));
        Assert.Equal([0, -1, 1], ListMerge.Align(["a", "b", "c"], ["a", "c"], string.Equals));
        Assert.Empty(ListMerge.Align([], ["a"], string.Equals));
    }
}
