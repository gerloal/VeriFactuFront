namespace Verifactu.Portal.Models;

public sealed class RemoteUserStatusDto
{
    public string TenantId { get; init; } = string.Empty;
    public bool RemoteCertificateEnabled { get; init; }
}
