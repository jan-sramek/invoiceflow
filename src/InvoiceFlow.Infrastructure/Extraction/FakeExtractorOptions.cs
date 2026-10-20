using System.ComponentModel.DataAnnotations;
using InvoiceFlow.Application.Errors;

namespace InvoiceFlow.Infrastructure.Extraction;

/// <summary>
/// Deterministic extraction stand-in when Ollama is not configured (NFR-AI-06 / Stage 5).
/// </summary>
public sealed class FakeExtractorOptions
{
    public const string SectionName = "FakeExtractor";

    /// <summary>Artificial latency before returning a result (exercises timeouts / retries).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Probability of failure in <c>[0, 1]</c>. Combined with <see cref="FailureMode"/>.
    /// </summary>
    [Range(0d, 1d)]
    public double FailureRate { get; set; }

    /// <summary>When a failure is drawn, whether to throw (technical) or return <c>Failed</c> (business).</summary>
    public FakeExtractorFailureMode FailureMode { get; set; } = FakeExtractorFailureMode.Technical;

    /// <summary>Catalogue code used for <see cref="FakeExtractorFailureMode.Business"/> failures.</summary>
    [MaxLength(64)]
    public string BusinessFailureCode { get; set; } = ApplicationErrors.Extraction.FakeFailed;
}

public enum FakeExtractorFailureMode
{
    Technical = 0,
    Business = 1,
}
