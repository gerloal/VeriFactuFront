using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchHistoryResponseDto
{
    [JsonPropertyName("batches")]
    public IList<BatchHistoryEntryDto> Batches { get; init; } = new List<BatchHistoryEntryDto>();

    [JsonPropertyName("retrievedAt")]
    public DateTimeOffset RetrievedAt { get; init; }
}

public sealed class BatchHistoryEntryDto
{
    [JsonPropertyName("batchId")]
    public string BatchId { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    [JsonPropertyName("fileType")]
    public string? FileType { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("receivedAt")]
    public DateTimeOffset ReceivedAt { get; init; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    [JsonPropertyName("completed")]
    public int Completed { get; init; }

    [JsonPropertyName("failed")]
    public int Failed { get; init; }

    [JsonPropertyName("pending")]
    public int Pending { get; init; }
}
