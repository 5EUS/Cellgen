using System.Numerics;

namespace Cellgen.Generation;

/// <summary>
/// The generator's only source of randomness: PCG32, seeded, so a level is a
/// pure function of its seed. It reproduces Godot's <c>RandomNumberGenerator</c>
/// bit for bit (the tests check recorded Godot sequences), so seeds shared
/// before this library existed still build the same levels. Every stage gets
/// its own stream (<see cref="ForStage"/>), so a change to how loot is rolled
/// never reshuffles the layout of a seed someone reported.
/// </summary>
public sealed class GenRng
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultIncrement = 1442695040888963407UL;

    private readonly ulong increment;
    private ulong state;

    /// <summary>Initializes a new instance of the <see cref="GenRng"/> class.</summary>
    /// <param name="seed">The seed.</param>
    public GenRng(ulong seed)
    {
        this.state = 0;
        this.increment = (DefaultIncrement << 1) | 1UL;
        _ = this.NextUInt();
        this.state += seed;
        _ = this.NextUInt();
    }

    /// <summary>A stream for one stage of generation.</summary>
    /// <param name="seed">The attempt's seed.</param>
    /// <param name="stage">The stage's name.</param>
    /// <returns>A stream independent of every other stage's.</returns>
    public static GenRng ForStage(ulong seed, string stage) => new(StableHash.Derive(seed, stage));

    /// <summary>The next raw 32-bit value.</summary>
    /// <returns>The value.</returns>
    public uint NextUInt()
    {
        ulong old = this.state;
        this.state = unchecked((old * Multiplier) + this.increment);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rotation = (int)(old >> 59);
        return BitOperations.RotateRight(xorShifted, rotation);
    }

    /// <summary>An integer in <c>[0, count)</c>.</summary>
    /// <param name="count">How many values; at least 1.</param>
    /// <returns>The integer.</returns>
    public int Next(int count) => (count <= 1) ? 0 : this.Range(0, count - 1);

    /// <summary>An integer in <c>[min, max]</c>.</summary>
    /// <param name="min">The lowest value.</param>
    /// <param name="max">The highest value; clamped up to <paramref name="min"/>.</param>
    /// <returns>The integer.</returns>
    public int Range(int min, int max)
    {
        max = Math.Max(min, max);
        if (min == max)
        {
            return min;
        }

        uint bound = unchecked((uint)(max - min) + 1U);
        return unchecked((int)(this.Bounded(bound) + (uint)min));
    }

    /// <summary>A float in <c>[0, 1]</c>.</summary>
    /// <returns>The float.</returns>
    public float NextFloat()
    {
        uint exponentSource = this.NextUInt();
        if (exponentSource == 0)
        {
            return 0f;
        }

        uint significand = this.NextUInt() | 0x80000001U;
        return (float)Math.ScaleB((double)significand, -32 - BitOperations.LeadingZeroCount(exponentSource));
    }

    /// <summary>True with probability <paramref name="probability"/>.</summary>
    /// <param name="probability">The probability.</param>
    /// <returns>The outcome.</returns>
    public bool Chance(float probability) => probability >= 1f || (probability > 0f && this.NextFloat() < probability);

    /// <summary>A fresh 64-bit seed for something downstream.</summary>
    /// <returns>The seed.</returns>
    public ulong NextSeed() => ((ulong)this.NextUInt() << 32) | this.NextUInt();

    /// <summary>Picks an index with probability proportional to its weight.</summary>
    /// <param name="weights">Non-negative weights.</param>
    /// <returns>The index, or -1 when every weight is zero.</returns>
    public int PickWeighted(IReadOnlyList<float> weights)
    {
        float total = 0f;
        foreach (float w in weights)
        {
            total += Math.Max(0f, w);
        }

        if (total <= 0f)
        {
            return -1;
        }

        float roll = this.NextFloat() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= Math.Max(0f, weights[i]);
            if (roll <= 0f && weights[i] > 0f)
            {
                return i;
            }
        }

        // Float rounding can leave a sliver: give it to the last positive weight.
        for (int i = weights.Count - 1; i >= 0; i--)
        {
            if (weights[i] > 0f)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Shuffles a list in place.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="list">The list.</param>
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = this.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Orders items randomly, heavier first on average (Efraimidis–Spirakis):
    /// the order the solver tries candidates in. Zero-weight items go last.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="weight">Each item's weight.</param>
    /// <returns>A new, ordered list.</returns>
    public IReadOnlyList<T> WeightedOrder<T>(IReadOnlyList<T> items, Func<T, float> weight) => this.WeightedOrderList(items, weight);

    /// <summary>The same as <see cref="WeightedOrder{T}"/>, as the list the solver sorts in place.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="weight">Each item's weight.</param>
    /// <returns>A new, ordered list.</returns>
    internal List<T> WeightedOrderList<T>(IReadOnlyList<T> items, Func<T, float> weight)
    {
        var keyed = new List<(double Key, int Index)>(items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            float w = weight(items[i]);
            double key = (w > 0f) ? Math.Pow(Math.Max(this.NextFloat(), 1e-9f), 1.0 / w) : -1.0 - this.NextFloat();
            keyed.Add((key, i));
        }

        keyed.Sort(static (a, b) => b.Key.CompareTo(a.Key));
        var ordered = new List<T>(items.Count);
        foreach ((double _, int index) in keyed)
        {
            ordered.Add(items[index]);
        }

        return ordered;
    }

    /// <summary>An unbiased value in <c>[0, bound)</c> (PCG's rejection method).</summary>
    private uint Bounded(uint bound)
    {
        uint threshold = unchecked((uint)(-(int)bound)) % bound;
        while (true)
        {
            uint r = this.NextUInt();
            if (r >= threshold)
            {
                return r % bound;
            }
        }
    }
}
