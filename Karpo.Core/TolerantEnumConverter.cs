// Reads an enum by name like JsonStringEnumConverter, but a name this build
// doesn't know doesn't make the whole file unreadable. config.json outlives
// builds in both directions: a newer Karpo writes kinds this one never heard
// of, and kinds get taken out (the performance islands). Such a name reads as
// a value outside the enum, Unknown, and Config.Repair drops what carries it.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// <see cref="JsonStringEnumConverter{TEnum}"/> (names from <see cref="JsonStringEnumMemberName"/>),
/// except that an unknown name, or null, reads as <see cref="Unknown"/> instead of throwing.
/// For enums stored as <see cref="int"/>.
/// </summary>
public sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private JsonConverter<T>? names;

    /// <summary>What an unknown name reads as: -1, which no member has.</summary>
    public static T Unknown => Unsafe.BitCast<int, T>(-1);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return Unknown;
            case JsonTokenType.String:
                try
                {
                    return Names(options).Read(ref reader, typeToConvert, options);
                }
                catch (JsonException)
                {
                    // A string is a single token: nothing to skip.
                    return Unknown;
                }
            default:
                return Names(options).Read(ref reader, typeToConvert, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        Names(options).Write(writer, value, options);

    private JsonConverter<T> Names(JsonSerializerOptions options) =>
        names ??= (JsonConverter<T>)new JsonStringEnumConverter<T>().CreateConverter(typeof(T), options)!;
}
