using Xunit;

namespace Cellgen.Tests;

/// <summary>Tests for <see cref="Int3"/>.</summary>
public sealed class Int3Tests
{
    /// <summary>Plan fingerprints hash the anchor's text, so the format is a contract with Godot's <c>Vector3I.ToString</c>.</summary>
    [Fact]
    public void ToStringMatchesTheEngineFormat() =>
        Assert.Equal("(5, -1, 7)", new Int3(5, -1, 7).ToString());

    /// <summary>Arithmetic is per component.</summary>
    [Fact]
    public void ArithmeticIsPerComponent()
    {
        Assert.Equal(new Int3(2, 3, 4), new Int3(1, 1, 1) + new Int3(1, 2, 3));
        Assert.Equal(new Int3(0, -1, -2), new Int3(1, 1, 1) - new Int3(1, 2, 3));
        Assert.Equal(new Int3(2, 4, 6), new Int3(1, 2, 3) * 2);
    }
}
