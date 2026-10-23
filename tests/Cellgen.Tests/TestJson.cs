namespace Cellgen.Tests;

/// <summary>Writes JSON in tests with single quotes, which read better in C# strings.</summary>
internal static class TestJson
{
    /// <summary>Turns single quotes into double quotes.</summary>
    /// <param name="text">JSON written with single quotes.</param>
    /// <returns>Real JSON.</returns>
    internal static string Quote(string text) => text.Replace('\'', '"');
}
