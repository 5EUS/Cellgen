using System.Text;

namespace Cellgen.Generation;

/// <summary>
/// A hash that is the same on every machine and every run - unlike
/// <c>string.GetHashCode</c>, which .NET randomizes per process. Seeds are
/// derived through this, so a seed shared in a bug report reproduces.
/// </summary>
public static class StableHash
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>FNV-1a over a string's UTF-8 bytes, finished with a SplitMix64 mix.</summary>
    /// <param name="text">The text.</param>
    /// <param name="seed">Mixed in first, so the same text hashes differently per seed.</param>
    /// <returns>The hash.</returns>
    public static ulong Of(string text, ulong seed = 0)
    {
        ulong hash = FnvOffset ^ Mix(seed);
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return Mix(hash);
    }

    /// <summary>Derives a child seed from a parent seed and a label: a stage, an attempt, a zone.</summary>
    /// <param name="seed">The parent seed.</param>
    /// <param name="label">What the child seed is for.</param>
    /// <returns>The child seed.</returns>
    public static ulong Derive(ulong seed, string label) => Of(label, seed);

    /// <summary>The SplitMix64 finalizer: spreads every input bit across the output.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The mixed value.</returns>
    public static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
