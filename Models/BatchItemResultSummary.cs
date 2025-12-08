using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchItemResultSummary
{
    [JsonPropertyName("itemId")]
    public string? ItemId { get; init; }

    [JsonPropertyName("facturaId")]
    public string? FacturaId { get; init; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; init; }

    [JsonPropertyName("rawPayload")]
    public string? RawPayload { get; init; }
}
