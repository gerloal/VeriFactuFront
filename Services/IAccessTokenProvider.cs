namespace Verifactu.Portal.Services;

public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync();
}
