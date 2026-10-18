using System.Security.Cryptography;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Testing.Invoices;

public static class TestDocuments
{
    public static InvoiceDocument Create(
        DateTimeOffset at,
        string? sha256 = null,
        string fileName = "invoice.pdf",
        long sizeBytes = 1024)
    {
        string hash = sha256
            ?? Convert.ToHexString(SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();
        var id = Guid.CreateVersion7(at);
        return InvoiceDocument.Create(id, $"test/{id:N}", fileName, sizeBytes, hash);
    }
}
