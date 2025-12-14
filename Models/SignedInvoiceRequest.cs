using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class SignedInvoiceRequest
{
    [JsonPropertyName("tenantId")]
    public string? TenantId { get; init; }

    [JsonPropertyName("batchId")]
    public string BatchId { get; init; } = string.Empty;

    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = string.Empty;

    [JsonPropertyName("facturaId")]
    public string? FacturaId { get; init; }

    [JsonPropertyName("xmlSignedBase64")]
    public string XmlSignedBase64 { get; init; } = string.Empty;

    [JsonPropertyName("hashSha256Hex")]
    public string HashSha256Hex { get; init; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string?>? Metadata { get; init; }
}
