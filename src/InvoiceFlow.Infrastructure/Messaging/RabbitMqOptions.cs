using System.ComponentModel.DataAnnotations;

namespace InvoiceFlow.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "invoiceflow";

    [Required]
    public string UserName { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";

    /// <summary>
    /// After this many delivery attempts the worker acks the message and fails extraction.
    /// </summary>
    [Range(1, 20)]
    public int MaxDeliveryAttempts { get; set; } = 5;
}
