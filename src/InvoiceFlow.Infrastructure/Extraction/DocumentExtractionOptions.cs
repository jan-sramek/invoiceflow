using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Extraction;

public sealed class DocumentExtractionOptions
{
    public const string SectionName = "DocumentExtraction";

    [Required]
    public string ServiceUrl { get; set; } = "";

    [Required]
    public string Model { get; set; } = "qwen3:0.6b";

    [Range(1, 500)]
    public int MaxPages { get; set; } = 50;

    [Range(1_000, 200_000)]
    public int MaxTextChars { get; set; } = 12_000;
}
