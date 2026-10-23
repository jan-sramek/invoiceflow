using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Documents;

/// <summary>
/// Local filesystem document store. Bound under <see cref="DocumentStorageOptions.SectionName"/> when
/// <c>RootPath</c> is set (takes precedence over S3 for local Windows / multi-process runs).
/// </summary>
public sealed class FileSystemDocumentStorageOptions
{
    /// <summary>Absolute or relative directory where objects are stored as files.</summary>
    [Required]
    public string RootPath { get; set; } = "";
}
