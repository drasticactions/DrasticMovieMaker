using System.Text.Json.Serialization;
using AvaMovieMaker.Timeline.Model;

namespace AvaMovieMaker.Timeline.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(MediaTimeConverter)])]
[JsonSerializable(typeof(Project))]
public sealed partial class AmmJsonContext : JsonSerializerContext;
