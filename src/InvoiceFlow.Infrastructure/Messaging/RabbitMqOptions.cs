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
    /// Unused by the DLX retry path (attempt count follows the three TTL retry tiers).
    /// Kept so existing cluster config keys remain valid.
    /// </summary>
    [Range(1, 20)]
    public int MaxDeliveryAttempts { get; set; } = 5;
}
