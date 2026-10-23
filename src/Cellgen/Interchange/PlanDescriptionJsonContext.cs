using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cellgen.Interchange;

/// <summary>Source-generated JSON metadata for plan descriptions (trim and AOT safe). Keys are snake_case; an unknown key is an error.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PlanDescription))]
internal sealed partial class PlanDescriptionJsonContext : JsonSerializerContext;
