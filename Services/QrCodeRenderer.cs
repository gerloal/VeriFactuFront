using System;
using QRCoder;

namespace Verifactu.Portal.Services;

public interface IQrCodeRenderer
{
    string GeneratePngBase64(string content);
}

public sealed class QrCodeRenderer : IQrCodeRenderer
{
    public string GeneratePngBase64(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content must not be null or whitespace.", nameof(content));
        }

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(20);
        return Convert.ToBase64String(bytes);
    }
}
