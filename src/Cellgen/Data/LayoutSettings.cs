using System.Globalization;

namespace Cellgen;

/// <summary>
/// Free-form numbers and text a recipe hands its layout planner: whatever that
/// planner documents, such as band widths or bend chances. Cellgen never reads
/// them; a missing key gives the planner's own fallback.
/// </summary>
public sealed class LayoutSettings
{
    private readonly IReadOnlyDictionary<string, object> values;

    /// <summary>Initializes a new instance of the <see cref="LayoutSettings"/> class.</summary>
    /// <param name="values">Key to value; numbers, booleans and strings.</param>
    public LayoutSettings(IReadOnlyDictionary<string, object> values) => this.values = values;

    /// <summary>Gets settings with no keys.</summary>
    public static LayoutSettings Empty { get; } = new(new Dictionary<string, object>(StringComparer.Ordinal));

    /// <summary>Gets a number.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">Returned when the key is missing or not a number.</param>
    /// <returns>The value.</returns>
    public float GetFloat(string key, float fallback) =>
        this.values.TryGetValue(key, out object? value) && value is IConvertible number and not string and not bool
            ? Convert.ToSingle(number, CultureInfo.InvariantCulture)
            : fallback;

    /// <summary>Gets a whole number.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">Returned when the key is missing or not a number.</param>
    /// <returns>The value, rounded to the nearest integer.</returns>
    public int GetInt(string key, int fallback) =>
        this.values.TryGetValue(key, out object? value) && value is IConvertible number and not string and not bool
            ? (int)Math.Round(Convert.ToDouble(number, CultureInfo.InvariantCulture))
            : fallback;

    /// <summary>Gets text.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">Returned when the key is missing or not text.</param>
    /// <returns>The value.</returns>
    public string GetString(string key, string fallback) =>
        this.values.TryGetValue(key, out object? value) && value is string text ? text : fallback;

    /// <summary>Gets a flag.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">Returned when the key is missing or not a boolean.</param>
    /// <returns>The value.</returns>
    public bool GetBool(string key, bool fallback) =>
        this.values.TryGetValue(key, out object? value) && value is bool flag ? flag : fallback;
}
