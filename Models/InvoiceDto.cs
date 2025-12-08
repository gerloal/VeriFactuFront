using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceDto
{
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    [JsonPropertyName("idempotencyKey")]
    public required string IdempotencyKey { get; init; }

    [JsonPropertyName("facturaId")]
    public string? FacturaId { get; init; }

    [JsonPropertyName("numeroSerie")]
    public string? NumeroSerie { get; init; }

    [JsonPropertyName("estado")]
    public required string Status { get; init; }

    [JsonPropertyName("aeatStatusCode")]
    public int? AeatStatusCode { get; init; }

    [JsonPropertyName("aeatAckCode")]
    public string? AeatAckCode { get; init; }

    [JsonPropertyName("aeatAckMessage")]
    public string? AeatAckMessage { get; init; }
}
