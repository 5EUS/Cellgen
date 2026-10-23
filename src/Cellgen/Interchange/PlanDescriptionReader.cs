using System.Text.Json;

namespace Cellgen.Interchange;

/// <summary>Reads the JSON form of a <see cref="PlanDescription"/>. Unknown keys are errors, so a typo is reported rather than ignored.</summary>
public static class PlanDescriptionReader
{
    /// <summary>Reads a plan description. Never throws.</summary>
    /// <param name="json">The JSON document.</param>
    /// <param name="error">Why it could not be read, when null is returned.</param>
    /// <returns>The description, or null when the document is malformed.</returns>
    public static PlanDescription? Parse(string json, out string error)
    {
        try
        {
            PlanDescription? description = JsonSerializer.Deserialize(json, PlanDescriptionJsonContext.Default.PlanDescription);
            error = (description is null) ? "the plan description is empty" : string.Empty;
            return description;
        }
        catch (JsonException exception)
        {
            error = $"the plan description is not valid: {exception.Message}";
            return null;
        }
    }

    /// <summary>Writes a plan description as JSON.</summary>
    /// <param name="description">The description.</param>
    /// <returns>The document.</returns>
    public static string ToJson(PlanDescription description) =>
        JsonSerializer.Serialize(description, PlanDescriptionJsonContext.Default.PlanDescription);
}
