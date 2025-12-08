using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class RemoteConsultationResponseDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("aeatStatus")]
    public string? AeatStatus { get; init; }

    [JsonPropertyName("ackMessage")]
    public string? AckMessage { get; init; }

    [JsonPropertyName("registros")]
    public IList<JsonElement> Registros { get; init; } = new List<JsonElement>();

    [JsonPropertyName("rawResponseBase64")]
    public string? RawResponseBase64 { get; init; }

    [JsonPropertyName("consultaId")]
    public string? ConsultaId { get; init; }

    [JsonPropertyName("stored")]
    public RemoteConsultationStoredInfoDto? Stored { get; init; }
}

public sealed class RemoteConsultationStoredInfoDto
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("storedAt")]
    public DateTimeOffset? StoredAt { get; init; }
}
