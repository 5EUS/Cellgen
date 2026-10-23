using System.Globalization;

using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>Checks <see cref="GenRng"/> against sequences recorded from Godot's <c>RandomNumberGenerator</c> (<c>tools/rng_vectors.gd</c> in Trenching, stored in <c>spec/rng_vectors.txt</c>).</summary>
public sealed class GenRngTests
{
    /// <summary>Every recorded raw value, range and float matches Godot's.</summary>
    /// <param name="seedText">The seed as Godot printed it (signed).</param>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("42")]
    [InlineData("12345")]
    [InlineData("-1")]
    [InlineData("7046029254386353131")]
    [InlineData("1234567890123456789")]
    public void MatchesGodotSequences(string seedText)
    {
        ulong seed = unchecked((ulong)long.Parse(seedText, CultureInfo.InvariantCulture));
        string[] lines = [.. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Spec", "rng_vectors.txt")).Where(l => l.StartsWith(seedText + "|", StringComparison.Ordinal))];
        Assert.NotEmpty(lines);

        GenRng rng = new(seed);
        string? lastGroup = null;
        foreach (string line in lines)
        {
            string[] parts = line.Split('|');
            string op = parts[1];
            string group = GroupOf(op);
            if (!string.Equals(group, lastGroup, StringComparison.Ordinal))
            {
                rng = new GenRng(seed);
                lastGroup = group;
            }

            string actual = op switch
            {
                "randi" => rng.NextUInt().ToString(CultureInfo.InvariantCulture),
                "range_0_9" => rng.Range(0, 9).ToString(CultureInfo.InvariantCulture),
                "range_m5_5" => rng.Range(-5, 5).ToString(CultureInfo.InvariantCulture),
                "range_0_1048575" => rng.Range(0, 1048575).ToString(CultureInfo.InvariantCulture),
                "range_3_3" => rng.Range(3, 3).ToString(CultureInfo.InvariantCulture),
                "randf" or "mix_randf" => FloatHex(rng.NextFloat()),
                "mix_randi" => rng.NextUInt().ToString(CultureInfo.InvariantCulture),
                "mix_range" => rng.Range(0, 99).ToString(CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException(op),
            };

            Assert.True(string.Equals(parts[2], actual, StringComparison.Ordinal), $"seed {seedText} {op}: Godot {parts[2]}, GenRng {actual}");
        }
    }

    private static string GroupOf(string op)
    {
        if (op.StartsWith("mix_", StringComparison.Ordinal))
        {
            return "mix";
        }

        return op is "range_m5_5" or "range_0_1048575" or "range_3_3" ? "ranges" : op;
    }

    private static string FloatHex(float value) => Convert.ToHexStringLower(BitConverter.GetBytes(value));
}
