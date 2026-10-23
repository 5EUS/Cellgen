using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cellgen.Rules;

/// <summary>Source-generated JSON metadata for rule sets (no reflection, so it is trim and AOT safe). Keys are snake_case; an unknown key is an error.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(RuleSet))]
internal sealed partial class RuleSetJsonContext : JsonSerializerContext;
