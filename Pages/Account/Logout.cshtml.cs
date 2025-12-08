using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace Verifactu.Portal.Pages.Account;

[AllowAnonymous]
public sealed class LogoutModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LogoutModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost(string? returnUrl = null)
    {
        var redirectUri = ResolveRedirectUri(returnUrl);
        var authProperties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return SignOut(authProperties, CookieAuthenticationDefaults.AuthenticationScheme, "Cognito");
    }

    private string ResolveRedirectUri(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return BuildAbsoluteUri(returnUrl);
        }

        var configured = _configuration["Cognito:SignedOutRedirectUri"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Uri.TryCreate(configured, UriKind.Absolute, out var absolute))
            {
                return absolute.ToString();
            }

            var absoluteFromPage = Url.Page(configured, null, null, Request.Scheme);
            if (!string.IsNullOrWhiteSpace(absoluteFromPage))
            {
                return absoluteFromPage;
            }
        }

        return Url.Page("/Account/SignedOut", null, null, Request.Scheme)
               ?? Url.Page("/Index", null, null, Request.Scheme)
               ?? BuildAbsoluteUri("/");
    }

    private string BuildAbsoluteUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            relativePath = "/";
        }

        if (!relativePath.StartsWith("/", StringComparison.Ordinal))
        {
            relativePath = "/" + relativePath.TrimStart('~');
        }

        return string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), relativePath);
    }
}
