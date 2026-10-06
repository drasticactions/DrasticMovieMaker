using System.Text.Json;
using System.Text.Json.Serialization;
using AvaMovieMaker.Time;

namespace AvaMovieMaker.Timeline.Serialization;

public sealed class MediaTimeConverter : JsonConverter<MediaTime>
{
    public override MediaTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetInt64());

    public override void Write(Utf8JsonWriter writer, MediaTime value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Ticks);
}
