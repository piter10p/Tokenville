using System.Text.Json;
using System.Text.Json.Serialization;
using Tokenville.Core;

namespace Tokenville.Runner;

/// <summary>
/// The one options object for config, the event log and, later, decisions and observations. PascalCase names in
/// declaration order, enums by name, ids as <c>agent-1</c>, positions as <c>{"X":..,"Y":..}</c> without the derived
/// tile properties, nulls omitted. Core carries no serialization attributes; the converters live here instead.
/// </summary>
public static class JsonFormat
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new EntityIdConverter(), new PositionConverter() },
    };

    private sealed class EntityIdConverter : JsonConverter<EntityId>
    {
        public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString();
            return EntityId.TryParse(text, out var id) ? id : throw new JsonException($"'{text}' is not an entity id.");
        }

        public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class PositionConverter : JsonConverter<Position>
    {
        public override Position Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            int? x = null, y = null;
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Position must be an object.");
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString();
                reader.Read();
                if (string.Equals(name, "X", StringComparison.OrdinalIgnoreCase)) x = reader.GetInt32();
                else if (string.Equals(name, "Y", StringComparison.OrdinalIgnoreCase)) y = reader.GetInt32();
                else throw new JsonException($"Position has no property '{name}'.");
            }
            return x is { } px && y is { } py ? new Position(px, py) : throw new JsonException("Position needs X and Y.");
        }

        public override void Write(Utf8JsonWriter writer, Position value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
