using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Storage;

/// <summary>
/// An S3-compatible store; SeaweedFS in the cluster.
/// </summary>
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    [Required]
    public Uri? ServiceUrl { get; set; }

    [Required]
    public string Bucket { get; set; } = "invoiceflow-documents";

    [Required]
    public string AccessKey { get; set; } = string.Empty;

    [Required]
    public string SecretKey { get; set; } = string.Empty;
}
