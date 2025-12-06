using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchPendingResponse
{
    [JsonPropertyName("batches")]
    public IList<BatchDto> Batches { get; init; } = new List<BatchDto>();

    [JsonPropertyName("retrievedAt")]
    public DateTimeOffset RetrievedAt { get; init; }
}
