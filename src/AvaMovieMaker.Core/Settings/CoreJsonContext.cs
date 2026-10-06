using System.Text.Json.Serialization;

namespace AvaMovieMaker.Settings;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
