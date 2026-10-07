using System.Text.Json;
using System.Text.Json.Serialization;
using AvaMovieMaker.Diagnostics;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Serialization;

// Reads unknown ratios (from newer builds) as 4:3 instead of failing the whole project load.
public sealed class AspectRatioConverter : JsonConverter<AspectRatio>
{
    public override AspectRatio Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? name = reader.GetString();
            foreach (AspectRatio a in AspectRatios.All)
            {
                if (string.Equals(Name(a), name, StringComparison.OrdinalIgnoreCase))
                {
                    return a;
                }
            }

            Log.Warn("project", $"Unknown aspect ratio '{name}'; using 4:3.");
            return AspectRatio.Standard4x3;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int n) && AspectRatios.All.Contains((AspectRatio)n))
        {
            return (AspectRatio)n;
        }

        Log.Warn("project", $"Unreadable aspect ratio ({reader.TokenType}); using 4:3.");
        reader.Skip();
        return AspectRatio.Standard4x3;
    }

    public override void Write(Utf8JsonWriter writer, AspectRatio value, JsonSerializerOptions options) => writer.WriteStringValue(Name(value));

    private static string Name(AspectRatio a) => a switch
    {
        AspectRatio.Widescreen16x9 => "widescreen16x9",
        AspectRatio.Vertical9x16 => "vertical9x16",
        AspectRatio.Square1x1 => "square1x1",
        AspectRatio.Portrait4x5 => "portrait4x5",
        _ => "standard4x3",
    };
}
