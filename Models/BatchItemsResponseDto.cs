using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class BatchItemsResponseDto
{
    [JsonPropertyName("items")]
    public IList<InvoiceDto> Items { get; init; } = new List<InvoiceDto>();
}
