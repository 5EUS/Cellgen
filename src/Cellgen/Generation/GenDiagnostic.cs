namespace Cellgen.Generation;

/// <summary>One thing the generator reported.</summary>
/// <param name="Severity">How bad it is.</param>
/// <param name="Path">Where in the recipe.</param>
/// <param name="Message">What happened.</param>
public sealed record GenDiagnostic(GenSeverity Severity, string Path, string Message)
{
    /// <inheritdoc/>
    public override string ToString()
    {
        string label = this.Severity switch
        {
            GenSeverity.Retry => "retry",
            GenSeverity.Warning => "warning",
            _ => "error",
        };
        return $"[{label}] {this.Path}: {this.Message}";
    }
}
