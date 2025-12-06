using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchDto
{
    [JsonPropertyName("batchId")]
    public required string BatchId { get; init; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    [JsonPropertyName("pendingItems")]
    public int PendingItems { get; init; }

    [JsonPropertyName("firstPendingItemId")]
    public string? FirstPendingItemId { get; init; }

    [JsonPropertyName("firstPendingStatus")]
    public string? FirstPendingStatus { get; init; }

    [JsonPropertyName("firstPendingOrdinal")]
    public int? FirstPendingOrdinal { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    public string Id => BatchId;

    public string Status
    {
        get
        {
            if (PendingItems == 0)
            {
                return "Completed";
            }

            if (string.IsNullOrWhiteSpace(FirstPendingStatus))
            {
                return "Pending";
            }

            var normalized = FirstPendingStatus.Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
        }
    }
}
