using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class InvoiceXmlResponseDto
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = string.Empty;

    [JsonPropertyName("facturaId")]
    public string? FacturaId { get; init; }

    [JsonPropertyName("xmlBase64")]
    public string XmlBase64 { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
}
