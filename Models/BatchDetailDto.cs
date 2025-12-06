using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchDetailDto
{
    [JsonPropertyName("batchId")]
    public required string BatchId { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    [JsonPropertyName("completed")]
    public int Completed { get; init; }

    [JsonPropertyName("failed")]
    public int Failed { get; init; }

    [JsonPropertyName("pending")]
    public int Pending { get; init; }

    [JsonPropertyName("consolidatedCsvBase64")]
    public string? ConsolidatedCsvBase64 { get; init; }

    [JsonPropertyName("items")]
    public IList<InvoiceDto> Items { get; init; } = new List<InvoiceDto>();

    public bool HasConsolidatedCsv => !string.IsNullOrWhiteSpace(ConsolidatedCsvBase64);
}
