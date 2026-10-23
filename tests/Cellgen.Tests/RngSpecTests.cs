using System.Globalization;

using Cellgen.Generation;

using Xunit;

namespace Cellgen.Tests;

/// <summary>
/// Pins the portable helpers of <see cref="GenRng"/> to <c>spec/rng_helpers.txt</c>, the vectors any other
/// implementation (a script host, a port to another language) must reproduce. Regenerate deliberately with
/// <c>CELLGEN_WRITE_SPEC=1 dotnet test</c>; a change to the file is a change to the spec.
/// </summary>
public sealed class RngSpecTests
{
    private static readonly ulong[] Seeds = [0UL, 1UL, 42UL, 12345UL, ulong.MaxValue, 7046029254386353131UL];

    private static readonly float[][] WeightSets =
    [
        [1f, 2f, 3f],
        [0f, 0f, 5f, 0f],
        [0.5f, 0.5f, 0.5f, 0.5f, 0.5f],
        [0.001f, 1000f],
        [0f, 0f],
    ];

    /// <summary>The helpers produce exactly the recorded results.</summary>
    [Fact]
    public void HelpersMatchTheSpecVectors()
    {
        string actual = string.Join('\n', Lines()) + '\n';
        string path = Path.Combine(AppContext.BaseDirectory, "Spec", "rng_helpers.txt");

        if (string.Equals(Environment.GetEnvironmentVariable("CELLGEN_WRITE_SPEC"), "1", StringComparison.Ordinal))
        {
            string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "spec", "rng_helpers.txt"));
            File.WriteAllText(source, actual);
            return;
        }

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n"), actual);
    }

    private static IEnumerable<string> Lines()
    {
        foreach (ulong seed in Seeds)
        {
            var rng = new GenRng(seed);
            string tag = seed.ToString(CultureInfo.InvariantCulture);
            for (int i = 0; i < 12; i++)
            {
                yield return $"{tag}|next|7|{rng.Next(7)}";
            }

            for (int i = 0; i < 6; i++)
            {
                yield return $"{tag}|range|-3,9|{rng.Range(-3, 9)}";
            }

            foreach (float[] weights in WeightSets)
            {
                string arg = string.Join(',', weights.Select(static w => w.ToString("R", CultureInfo.InvariantCulture)));
                for (int i = 0; i < 4; i++)
                {
                    yield return $"{tag}|pick_weighted|{arg}|{rng.PickWeighted(weights)}";
                }
            }

            foreach (float p in (float[])[0.25f, 0.5f, 0.75f, 1f, 0f])
            {
                yield return $"{tag}|chance|{p.ToString("R", CultureInfo.InvariantCulture)}|{(rng.Chance(p) ? 1 : 0)}";
            }

            for (int i = 0; i < 3; i++)
            {
                List<int> list = [.. Enumerable.Range(0, 10)];
                rng.Shuffle(list);
                yield return $"{tag}|shuffle|10|{string.Join(',', list)}";
            }

            for (int i = 0; i < 3; i++)
            {
                yield return $"{tag}|next_seed||{rng.NextSeed()}";
            }
        }
    }
}
