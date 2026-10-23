namespace Cellgen.Generation;

/// <summary>How bad a <see cref="GenDiagnostic"/> is.</summary>
public enum GenSeverity
{
    /// <summary>An attempt failed; generation retried with another seed.</summary>
    Retry,

    /// <summary>Suspicious, but the level is usable.</summary>
    Warning,

    /// <summary>The recipe or its content is wrong in a way no seed fixes.</summary>
    Error,
}
