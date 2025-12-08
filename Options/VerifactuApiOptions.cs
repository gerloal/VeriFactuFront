namespace Verifactu.Portal.Options;

public sealed class VerifactuApiOptions
{
    public string? BaseUrl { get; set; }

    public string? ApiKey { get; set; }

    public string? AppKey { get; set; }

    public string? TenantId { get; set; }

    public string? CloudFrontSecret { get; set; }

    public string? RemoteConsultationEndpoint { get; set; }
}
