using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InvoiceFlow.Application.Abstractions.Extraction;
using InvoiceFlow.Domain.Invoices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceFlow.Infrastructure.Extraction;

internal sealed partial class OllamaInvoiceExtractor(
    HttpClient http,
    IOptions<DocumentExtractionOptions> options,
    ILogger<OllamaInvoiceExtractor> logger) : IInvoiceExtractor
{
    private const string Prompt = """
        Extract invoice fields. Reply with a single JSON object only, no markdown.
        Keys: supplierName, invoiceNumber, invoiceDate (yyyy-MM-dd), dueDate (yyyy-MM-dd), currency (ISO 4217),
        subtotal, taxAmount, totalAmount, purchaseOrderReference. Use null for unknown values.
        The PDF bytes were not OCR'd in this request; return nulls unless the prompt includes text.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<InvoiceFields> ExtractAsync(Stream pdf, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        // Drain the stream so callers can dispose; OCR of PDF bytes is out of scope for Stage 5.
        await pdf.CopyToAsync(Stream.Null, cancellationToken);

        var request = new OllamaGenerateRequest(options.Value.Model, Prompt, Stream: false, Think: false);
        using HttpResponseMessage response = await http.PostAsJsonAsync("/api/generate", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        OllamaGenerateResponse? body =
            await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(JsonOptions, cancellationToken);
        InvoiceFields fields = InvoiceExtractionJson.Parse(body?.Response);
        LogExtracted(logger, fields.TotalAmount);
        return fields;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ollama extraction produced totalAmount={TotalAmount}")]
    private static partial void LogExtracted(ILogger logger, decimal? totalAmount);

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("think")] bool Think);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response);
}
