using System.Collections.Generic;

namespace Verifactu.Portal.Models;

public sealed class RemoteSubmissionResponseDto
{
    public string? Status { get; init; }
    public RemoteSubmissionSummaryDto? Summary { get; init; }
    public IList<RemoteInvoiceResultDto> Invoices { get; init; } = new List<RemoteInvoiceResultDto>();
    public IList<RemoteSkippedItemDto> Skipped { get; init; } = new List<RemoteSkippedItemDto>();
}

public sealed class RemoteSubmissionSummaryDto
{
    public string TenantId { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public int TotalInvoices { get; init; }
    public int Succeeded { get; init; }
    public int Failed { get; init; }
}

public sealed class RemoteInvoiceResultDto
{
    public string IdempotencyKey { get; init; } = string.Empty;
    public string TipoOperacion { get; init; } = string.Empty;
    public string EmisorNif { get; init; } = string.Empty;
    public string Serie { get; init; } = string.Empty;
    public string? Numero { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool AeatOk { get; init; }
    public int AeatStatusCode { get; init; }
    public string? AeatAckCode { get; init; }
    public string? AeatAckMessage { get; init; }
    public string? ResponseS3Key { get; init; }
    public string? Error { get; init; }
}

public sealed class RemoteSkippedItemDto
{
    public string? ItemId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? Status { get; init; }
    public string? Reason { get; init; }
}
