using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Extraction;

public sealed class DocumentExtractionOptions
{
    public const string SectionName = "DocumentExtraction";

    [Required]
    public string ServiceUrl { get; set; } = "";

    [Required]
    public string Model { get; set; } = "qwen3:0.6b";
}
