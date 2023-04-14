using MagiCommon.Models.AssistantChat;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MagiCommon.Serialization
{
    public class RoleJsonConverter : JsonConverter<Role>
    {
        public override Role Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (Enum.TryParse<Role>(value, true, out var role))
            {
                return role;
            }

            throw new JsonException($"Invalid role value: {value}");
        }

        public override void Write(Utf8JsonWriter writer, Role value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }

}
