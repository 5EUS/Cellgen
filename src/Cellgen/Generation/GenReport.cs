namespace Cellgen.Generation;

/// <summary>
/// What the generator had to say about a level: why attempts failed, and
/// what it put up with. Every diagnostic carries a path into the recipe
/// (<c>structures[0]</c>, <c>cellsets[1].cells[3]</c>, <c>zone 4</c>), the
/// same convention as the content bake's report.
/// </summary>
public sealed class GenReport
{
    private readonly List<GenDiagnostic> diagnostics = [];

    /// <summary>Gets every diagnostic, in the order they were raised.</summary>
    public IReadOnlyList<GenDiagnostic> Diagnostics => this.diagnostics;

    /// <summary>Gets or sets how many attempts generation took, including the one that succeeded.</summary>
    public int Attempts { get; set; }

    /// <summary>Gets or sets a value indicating whether the plan came from the recipe's fallback.</summary>
    public bool UsedFallback { get; set; }

    /// <summary>Gets the number of errors.</summary>
    public int ErrorCount => this.diagnostics.FindAll(static d => d.Severity == GenSeverity.Error).Count;

    /// <summary>Records a problem with the recipe or its content that no seed can fix.</summary>
    /// <param name="path">Where in the recipe.</param>
    /// <param name="message">What is wrong.</param>
    public void Error(string path, string message) => this.diagnostics.Add(new GenDiagnostic(GenSeverity.Error, path, message));

    /// <summary>Records something suspicious that did not stop generation.</summary>
    /// <param name="path">Where in the recipe.</param>
    /// <param name="message">What is suspicious.</param>
    public void Warn(string path, string message) => this.diagnostics.Add(new GenDiagnostic(GenSeverity.Warning, path, message));

    /// <summary>Records why one attempt failed; the next seed may succeed.</summary>
    /// <param name="attempt">The attempt number.</param>
    /// <param name="message">Why it failed.</param>
    public void AttemptFailed(int attempt, string message) =>
        this.diagnostics.Add(new GenDiagnostic(GenSeverity.Retry, $"attempt {attempt}", message));

    /// <summary>Formats every diagnostic, one per line.</summary>
    /// <returns>The lines.</returns>
    public IEnumerable<string> Lines()
    {
        foreach (GenDiagnostic d in this.diagnostics)
        {
            yield return d.ToString();
        }
    }
}
