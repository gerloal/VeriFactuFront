using Microsoft.AspNetCore.Authentication;

namespace Verifactu.Portal.Services;

public sealed class HttpContextAccessTokenProvider(IHttpContextAccessor httpContextAccessor) : IAccessTokenProvider
{
    private const string AccessTokenName = "access_token";

    public async Task<string?> GetAccessTokenAsync()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        return await context.GetTokenAsync(AccessTokenName);
    }
}
