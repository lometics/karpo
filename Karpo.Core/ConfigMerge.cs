// Three-way merges, so the dock and the settings app can both save config.json at
// the same time without either losing what the other changed.
//
// A writer knows the file as it last read or wrote it (the base), has its own
// version (ours), and finds the file as someone else left it since (theirs).
// What only one side changed is taken from that side; what both changed alike is
// taken once; where both changed the same value differently, the writer that is
// saving now wins, as it would have with a whole-file save. Lists merge entry by
// entry, so a pin the dock added while the settings app reordered the pins, or a
// widget one app edited while the other added another, all survive.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Karpo.Core;

/// <summary>Three-way merge of ordered lists whose entries are told apart by a key.</summary>
public static class ListMerge
{
    /// <summary>
    /// For each entry of <paramref name="a"/>, the index of the same entry in <paramref name="b"/>, or -1:
    /// their longest common subsequence, entries compared with <paramref name="same"/>. Ties keep the
    /// earlier entries of <paramref name="a"/> matched, so of two equal entries the first is found.
    /// </summary>
    public static int[] Align<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, Func<T, T, bool> same)
    {
        var rows = a.Count;
        var columns = b.Count;
        // lengths[i, j]: the longest common subsequence of a[i..] and b[j..].
        var lengths = new int[rows + 1, columns + 1];
        for (var i = rows - 1; i >= 0; i--)
        {
            for (var j = columns - 1; j >= 0; j--)
            {
                lengths[i, j] = same(a[i], b[j])
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }
        var map = new int[rows];
        Array.Fill(map, -1);
        for (int i = 0, j = 0; i < rows && j < columns;)
        {
            if (same(a[i], b[j]) && lengths[i, j] == lengths[i + 1, j + 1] + 1)
            {
                map[i++] = j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }
        return map;
    }

    /// <summary>
    /// The keys of <paramref name="ours"/> and <paramref name="theirs"/>, two edits of <paramref name="base"/>,
    /// merged, in order. Keys are unique within each list.
    /// <list type="bullet">
    /// <item>An entry either side added is kept; an entry either side removed goes, even if the other side moved it.</item>
    /// <item>Stretches between entries all three lists keep in the same order come from the side that changed
    /// them (diff3); where both changed the same stretch, ours comes first, then what theirs added there.</item>
    /// </list>
    /// </summary>
    public static List<string> Merge(IReadOnlyList<string> @base, IReadOnlyList<string> ours, IReadOnlyList<string> theirs)
    {
        var inBase = new HashSet<string>(@base, StringComparer.Ordinal);
        var inOurs = new HashSet<string>(ours, StringComparer.Ordinal);
        var inTheirs = new HashSet<string>(theirs, StringComparer.Ordinal);
        bool Keep(string key) => inBase.Contains(key)
            ? inOurs.Contains(key) && inTheirs.Contains(key)
            : inOurs.Contains(key) || inTheirs.Contains(key);

        var toOurs = Align(@base, ours, string.Equals);
        var toTheirs = Align(@base, theirs, string.Equals);
        // Entries of the base a side still has but not in its old order: that side moved them.
        var movedByOurs = new HashSet<string>(ours.Where(inBase.Contains), StringComparer.Ordinal);
        var movedByTheirs = new HashSet<string>(theirs.Where(inBase.Contains), StringComparer.Ordinal);
        for (var index = 0; index < @base.Count; index++)
        {
            if (toOurs[index] >= 0)
            {
                movedByOurs.Remove(@base[index]);
            }
            if (toTheirs[index] >= 0)
            {
                movedByTheirs.Remove(@base[index]);
            }
        }
        var emitted = new List<(string Key, bool Ours)>();
        int fromBase = 0, fromOurs = 0, fromTheirs = 0;
        for (var index = 0; index <= @base.Count; index++)
        {
            var last = index == @base.Count;
            // An anchor: an entry of the base both sides still have, in the same order.
            if (!last && (toOurs[index] < 0 || toTheirs[index] < 0))
            {
                continue;
            }
            var oursEnd = last ? ours.Count : toOurs[index];
            var theirsEnd = last ? theirs.Count : toTheirs[index];
            Stretch(
                Slice(@base, fromBase, index),
                Slice(ours, fromOurs, oursEnd),
                Slice(theirs, fromTheirs, theirsEnd),
                inBase,
                movedByOurs,
                movedByTheirs,
                emitted);
            if (!last)
            {
                emitted.Add((@base[index], true));
                fromBase = index + 1;
                fromOurs = oursEnd + 1;
                fromTheirs = theirsEnd + 1;
            }
        }

        // Each key once: where ours put it when ours put it anywhere.
        var placedByOurs = emitted.Where(entry => entry.Ours).Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var result = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, byOurs) in emitted)
        {
            if (Keep(key) && (byOurs || !placedByOurs.Contains(key)) && placed.Add(key))
            {
                result.Add(key);
            }
        }
        // Entries both sides moved into stretches the other side changed too are placed by
        // no stretch: they go after the entry they follow in ours, else in theirs.
        PlaceMissing(result, placed, ours, Keep);
        PlaceMissing(result, placed, theirs, Keep);
        return result;
    }

    private static List<string> Slice(IReadOnlyList<string> list, int from, int to)
    {
        var slice = new List<string>(Math.Max(0, to - from));
        for (var index = from; index < to; index++)
        {
            slice.Add(list[index]);
        }
        return slice;
    }

    private static void Stretch(
        List<string> @base,
        List<string> ours,
        List<string> theirs,
        HashSet<string> inBase,
        HashSet<string> movedByOurs,
        HashSet<string> movedByTheirs,
        List<(string Key, bool Ours)> emitted)
    {
        if (ours.SequenceEqual(@base))
        {
            emitted.AddRange(theirs.Select(key => (key, false)));
            return;
        }
        if (theirs.SequenceEqual(@base) || theirs.SequenceEqual(ours))
        {
            emitted.AddRange(ours.Select(key => (key, true)));
            return;
        }
        // Both changed this stretch. Ours stands, except that an entry theirs moved away
        // from here, and ours only left where it was, goes where theirs put it; then come
        // what theirs added here and what theirs moved here that ours left in place.
        emitted.AddRange(ours
            .Where(key => !(@base.Contains(key) && !theirs.Contains(key) && movedByTheirs.Contains(key)))
            .Select(key => (key, true)));
        emitted.AddRange(theirs
            .Where(key => !ours.Contains(key) && !@base.Contains(key) && (!inBase.Contains(key) || !movedByOurs.Contains(key)))
            .Select(key => (key, false)));
    }

    private static void PlaceMissing(List<string> result, HashSet<string> placed, IReadOnlyList<string> source, Func<string, bool> keep)
    {
        var after = -1;
        foreach (var key in source)
        {
            if (placed.Contains(key))
            {
                after = result.IndexOf(key);
                continue;
            }
            if (!keep(key))
            {
                continue;
            }
            result.Insert(++after, key);
            placed.Add(key);
        }
    }
}

/// <summary>
/// The three-way merge of config.json texts (<see cref="Config.Save"/> uses it when the file changed
/// since the config being saved was read). Plain JSON, no model: a property the model gains later
/// merges without a change here.
/// <list type="bullet">
/// <item>Objects merge property by property, and so do dictionaries (they are objects in the file).</item>
/// <item>Arrays of objects that have an identity (a string <c>id</c>, a folder cell's <c>island.id</c>, else — a folder's
/// faces — a <c>kind</c>) merge by it: entries added on either side are kept, an entry removed on either side goes (a
/// removal wins over an edit on the other side), entries both kept merge in turn, and the order merges as
/// <see cref="ListMerge"/> does.</item>
/// <item>Arrays of such arrays (a folder's <c>lists</c>) merge as one folder: each list by its place, an entry moved
/// between lists where the side that moved it put it (<see cref="MergeLists"/>).</item>
/// <item>Arrays of plain values (pins, targets, layout ids) merge as ordered lists; a value that appears twice
/// (the pin divider) counts as two entries.</item>
/// <item>Anything else both sides changed differently: the saver's (ours).</item>
/// </list>
/// </summary>
public static class ConfigMerge
{
    private static readonly JsonDocumentOptions Reading = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary><paramref name="ours"/> and <paramref name="theirs"/>, two edits of <paramref name="base"/>, as one JSON text.</summary>
    public static string Merge(string @base, string ours, string theirs)
    {
        var merged = Merge(
            Side.Of(JsonNode.Parse(@base, documentOptions: Reading)),
            Side.Of(JsonNode.Parse(ours, documentOptions: Reading)),
            Side.Of(JsonNode.Parse(theirs, documentOptions: Reading)));
        return merged.Node?.ToJsonString() ?? "null";
    }

    /// <summary>A value on one side: absent (a property that isn't there), or a node (null is JSON null).</summary>
    private readonly record struct Side(bool Has, JsonNode? Node)
    {
        public static readonly Side None = new(false, null);

        public static Side Of(JsonNode? node) => new(true, node);
    }

    private static bool Same(Side a, Side b) => a.Has == b.Has && (!a.Has || JsonNode.DeepEquals(a.Node, b.Node));

    private static Side Merge(Side @base, Side ours, Side theirs)
    {
        if (Same(ours, theirs) || Same(theirs, @base))
        {
            return ours;
        }
        if (Same(ours, @base))
        {
            return theirs;
        }
        // Both changed it, differently: look inside when both are containers of the same sort.
        if (ours.Node is JsonObject oursObject && theirs.Node is JsonObject theirsObject && @base.Node is null or JsonObject)
        {
            return Side.Of(MergeObjects(@base.Node as JsonObject, oursObject, theirsObject));
        }
        if (ours.Node is JsonArray oursArray && theirs.Node is JsonArray theirsArray && @base.Node is null or JsonArray)
        {
            return Side.Of(MergeArrays(@base.Node as JsonArray, oursArray, theirsArray));
        }
        return ours;
    }

    private static JsonObject MergeObjects(JsonObject? @base, JsonObject ours, JsonObject theirs)
    {
        var merged = new JsonObject();
        // Ours' order, then what only theirs has; what only the base had, both removed.
        var keys = ours.Select(property => property.Key).ToList();
        keys.AddRange(theirs.Select(property => property.Key).Where(key => !ours.ContainsKey(key)));
        foreach (var key in keys)
        {
            var result = Merge(Get(@base, key), Get(ours, key), Get(theirs, key));
            if (result.Has)
            {
                merged[key] = Detached(result.Node);
            }
        }
        return merged;
    }

    private static Side Get(JsonObject? node, string key) =>
        node is not null && node.TryGetPropertyValue(key, out var value) ? Side.Of(value) : Side.None;

    private static JsonArray MergeArrays(JsonArray? @base, JsonArray ours, JsonArray theirs)
    {
        var baseItems = @base?.ToList() ?? [];
        var oursItems = ours.ToList();
        var theirsItems = theirs.ToList();
        var all = baseItems.Concat(oursItems).Concat(theirsItems).ToList();
        if (all.Count > 0 && all.All(item => item is JsonArray list && list.All(entry => Identity(entry) is not null)))
        {
            // Lists of entries with an identity: a folder's lists, its columns of lines.
            return MergeLists(baseItems, oursItems, theirsItems);
        }
        Func<JsonNode?, string> identity;
        if (all.All(item => Identity(item) is not null))
        {
            identity = item => Identity(item)!;
        }
        else if (all.All(item => item is null or JsonValue))
        {
            identity = item => "=" + (item?.ToJsonString() ?? "null");
        }
        else
        {
            // Nothing to tell the entries apart by: the saver's list.
            return (JsonArray)Detached(ours)!;
        }

        var baseKeys = Keys(baseItems, identity);
        var oursKeys = Keys(oursItems, identity);
        var theirsKeys = Keys(theirsItems, identity);
        var baseByKey = ByKey(baseKeys, baseItems);
        var oursByKey = ByKey(oursKeys, oursItems);
        var theirsByKey = ByKey(theirsKeys, theirsItems);
        var merged = new JsonArray();
        foreach (var key in ListMerge.Merge(baseKeys, oursKeys, theirsKeys))
        {
            var result = Merge(Find(baseByKey, key), Find(oursByKey, key), Find(theirsByKey, key));
            if (result.Has)
            {
                merged.Add(Detached(result.Node));
            }
        }
        return merged;
    }

    /// <summary>
    /// A list of lists whose entries have an identity (a folder's <c>lists</c>, each a column of its lines, each
    /// line known by the island it holds), merged as one folder rather than taken whole from the saver: what one
    /// side added to any list, moved between lists or took out, and what the other did to other lines, all stay.
    /// <list type="bullet">
    /// <item>The lists are told apart by their place: the first, the second… How many there are is the side's that
    /// changed it, ours where both did.</item>
    /// <item>An entry is kept as <see cref="ListMerge"/> keeps one: an entry either side added stays, one either side took
    /// out goes. It stands in the list the side that moved it put it in, ours where both moved it, and in a
    /// list past the last one there is, in the last.</item>
    /// <item>Each list's order merges as <see cref="ListMerge"/> does; an entry moved into it goes after the one it follows
    /// on the side that moved it. Entries both kept merge in turn, property by property.</item>
    /// </list>
    /// </summary>
    private static JsonArray MergeLists(List<JsonNode?> @base, List<JsonNode?> ours, List<JsonNode?> theirs)
    {
        var baseLists = Columns(@base);
        var oursLists = Columns(ours);
        var theirsLists = Columns(theirs);
        var count = oursLists.Count != baseLists.Count ? oursLists.Count : theirsLists.Count;
        count = Math.Max(count, 1);
        var baseWhere = Where(baseLists);
        var oursWhere = Where(oursLists);
        var theirsWhere = Where(theirsLists);

        // Which list each entry stands in, of those that stay.
        var column = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in oursWhere.Keys.Concat(theirsWhere.Keys).Distinct(StringComparer.Ordinal))
        {
            var inBase = baseWhere.TryGetValue(key, out var was);
            var inOurs = oursWhere.TryGetValue(key, out var byOurs);
            var inTheirs = theirsWhere.TryGetValue(key, out var byTheirs);
            if (inBase ? !(inOurs && inTheirs) : !(inOurs || inTheirs))
            {
                continue;
            }
            var place = inOurs && (!inBase || byOurs.List != was.List || !inTheirs) ? byOurs.List : byTheirs.List;
            column[key] = Math.Min(place, count - 1);
        }

        var merged = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var baseKeys = KeysOf(baseLists, index);
            var oursKeys = KeysOf(oursLists, index);
            var theirsKeys = KeysOf(theirsLists, index);
            var order = ListMerge.Merge(baseKeys, oursKeys, theirsKeys)
                .Where(key => column.GetValueOrDefault(key, -1) == index)
                .ToList();
            // An entry moved here from another list that the list's own merge did not place: after the
            // one it follows in the list of the side that put it here, ours first.
            var placed = new HashSet<string>(order, StringComparer.Ordinal);
            foreach (var side in new[] { oursKeys, theirsKeys })
            {
                var after = -1;
                foreach (var key in side)
                {
                    if (placed.Contains(key))
                    {
                        after = order.IndexOf(key);
                        continue;
                    }
                    if (column.GetValueOrDefault(key, -1) == index)
                    {
                        order.Insert(++after, key);
                        placed.Add(key);
                    }
                }
            }
            // One that ended up here from a list past the last: at the end.
            order.AddRange(column.Where(entry => entry.Value == index && !placed.Contains(entry.Key)).Select(entry => entry.Key).OrderBy(key => key, StringComparer.Ordinal));

            var list = new JsonArray();
            foreach (var key in order)
            {
                var result = Merge(Entry(baseWhere, key), Entry(oursWhere, key), Entry(theirsWhere, key));
                if (result.Has)
                {
                    list.Add(Detached(result.Node));
                }
            }
            merged.Add((JsonNode)list);
        }
        return merged;
    }

    /// <summary>Each list's entries' keys, in order (as <see cref="Keys"/> makes them, the second of two alike another key).</summary>
    private static List<List<(string Key, JsonNode? Node)>> Columns(List<JsonNode?> lists) =>
        [.. lists.Select(list =>
        {
            var items = ((JsonArray)list!).ToList();
            var keys = Keys(items, item => Identity(item)!);
            return keys.Select((key, index) => (key, items[index])).ToList();
        })];

    /// <summary>Where each key stands: its list, and the entry.</summary>
    private static Dictionary<string, (int List, JsonNode? Node)> Where(List<List<(string Key, JsonNode? Node)>> lists)
    {
        var where = new Dictionary<string, (int List, JsonNode? Node)>(StringComparer.Ordinal);
        for (var index = 0; index < lists.Count; index++)
        {
            foreach (var (key, node) in lists[index])
            {
                where.TryAdd(key, (index, node));
            }
        }
        return where;
    }

    private static List<string> KeysOf(List<List<(string Key, JsonNode? Node)>> lists, int index) =>
        index < lists.Count ? [.. lists[index].Select(entry => entry.Key)] : [];

    private static Side Entry(Dictionary<string, (int List, JsonNode? Node)> where, string key) =>
        where.TryGetValue(key, out var found) ? Side.Of(found.Node) : Side.None;

    /// <summary>
    /// What tells an entry of a list of objects apart: its string id, the id of the island a folder cell
    /// holds, or — for what has neither, a folder's faces — its kind (the second of a kind is another entry).
    /// </summary>
    private static string? Identity(JsonNode? item)
    {
        if (item is not JsonObject entry)
        {
            return null;
        }
        if (Text(entry, "id") is { } id)
        {
            return "id:" + id;
        }
        if (entry.TryGetPropertyValue("island", out var island))
        {
            return island is JsonObject inner && Text(inner, "id") is { } islandId ? "island:" + islandId : null;
        }
        return Text(entry, "kind") is { } kind ? "kind:" + kind : null;
    }

    private static string? Text(JsonObject entry, string key) =>
        entry.TryGetPropertyValue(key, out var value) && value is JsonValue text && text.GetValueKind() == JsonValueKind.String
            ? text.GetValue<string>()
            : null;

    /// <summary>A key per entry; the second of two equal entries is another entry than the first.</summary>
    private static List<string> Keys(List<JsonNode?> items, Func<JsonNode?, string> identity)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new List<string>(items.Count);
        foreach (var item in items)
        {
            var key = identity(item);
            var count = counts.GetValueOrDefault(key);
            counts[key] = count + 1;
            keys.Add(count == 0 ? key : $"{key}\u0000{count}");
        }
        return keys;
    }

    private static Dictionary<string, JsonNode?> ByKey(List<string> keys, List<JsonNode?> items)
    {
        var map = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        for (var index = 0; index < keys.Count; index++)
        {
            map[keys[index]] = items[index];
        }
        return map;
    }

    private static Side Find(Dictionary<string, JsonNode?> map, string key) =>
        map.TryGetValue(key, out var node) ? Side.Of(node) : Side.None;

    /// <summary>
    /// A node that may go into the merged tree: a node can have one parent only, so one that
    /// belongs to an input already is copied; one this merge built is taken as it is.
    /// </summary>
    private static JsonNode? Detached(JsonNode? node) => node?.Parent is null ? node : node.DeepClone();
}
