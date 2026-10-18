using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Documents;

public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    [Required]
    public string ServiceUrl { get; set; } = "";

    [Required]
    public string Bucket { get; set; } = "invoiceflow-documents";

    [Required]
    public string AccessKey { get; set; } = "";

    [Required]
    public string SecretKey { get; set; } = "";

    public string Region { get; set; } = "us-east-1";
}
