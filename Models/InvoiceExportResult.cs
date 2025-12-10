using System;

namespace Verifactu.Portal.Models;

public sealed class InvoiceExportResult
{
    public InvoiceExportResult(byte[] content, string contentType, string fileName)
    {
        Content = content ?? Array.Empty<byte>();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/zip" : contentType;
        FileName = string.IsNullOrWhiteSpace(fileName) ? "facturas-export.zip" : fileName;
    }

    public byte[] Content { get; }

    public string ContentType { get; }

    public string FileName { get; }
}
