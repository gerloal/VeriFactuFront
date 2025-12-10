using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Verifactu.Portal.Options;
using Verifactu.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://+:8080");
}

builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<VerifactuApiOptions>(builder.Configuration.GetSection("VerifactuApi"));
builder.Services.PostConfigure<VerifactuApiOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.AppKey))
    {
        options.AppKey = builder.Configuration["Security:AppKey"]
                          ?? builder.Configuration["Security:ExpectedAppKey"]
                          ?? (builder.Environment.IsDevelopment() ? "bpfs7fovu2" : null);
    }

    if (string.IsNullOrWhiteSpace(options.CloudFrontSecret))
    {
        options.CloudFrontSecret = builder.Configuration["Security:ExpectedCloudFrontSecret"]
                                   ?? (builder.Environment.IsDevelopment()
                                       ? "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkdlbmVyYXRlZCBVc2VyIiwiaWF0IjoxNzY0OTU5NjM5LCJleHAiOjE3NjQ5NjMyMzl9.WoXGXFdbBd+1zsipXPzo5QzHQo8eELnNSuNCkHQ7lYU="
                                       : null);
    }

    if (string.IsNullOrWhiteSpace(options.AeatConsultaEndpoint))
    {
        options.AeatConsultaEndpoint = builder.Configuration["Security:AeatConsultaEndpoint"]
                                   ?? "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";
    }
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Cognito";
})
.AddCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddOpenIdConnect("Cognito", options =>
{
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.SaveTokens = true;
    options.SignedOutCallbackPath = "/signout-callback-oidc";

    options.CorrelationCookie.SameSite = SameSiteMode.None;
    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    options.NonceCookie.SameSite = SameSiteMode.None;
    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;

    var cognitoSection = builder.Configuration.GetSection("Cognito");

    options.Authority = cognitoSection["Authority"];
    options.ClientId = cognitoSection["ClientId"];
    options.ClientSecret = cognitoSection["ClientSecret"];
    options.ResponseType = cognitoSection.GetValue<string>("ResponseType") ?? "code";

    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");

    options.TokenValidationParameters.NameClaimType = ClaimTypes.Name;
    options.ClaimActions.MapJsonKey("tenantId", "custom:tenantId");
    options.ClaimActions.MapJsonKey("apiKey", "custom:ApiKey");

    var signedOutRedirect = cognitoSection["SignedOutRedirectUri"];
    if (!string.IsNullOrWhiteSpace(signedOutRedirect))
    {
        options.SignedOutRedirectUri = signedOutRedirect;
    }

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProviderForSignOut = context =>
        {
            var cognitoDomain = cognitoSection["HostedDomain"];
            if (string.IsNullOrWhiteSpace(cognitoDomain))
            {
                cognitoDomain = context.Options.Authority;
            }

            cognitoDomain ??= string.Empty;

            var request = context.HttpContext.Request;
            var callbackPath = context.Options.SignedOutCallbackPath.HasValue
                ? context.Options.SignedOutCallbackPath.Value
                : "/";

            var callback = callbackPath.StartsWith("/", StringComparison.Ordinal)
                ? string.Concat(request.Scheme, "://", request.Host.ToUriComponent(), callbackPath)
                : callbackPath;

            if (context.Properties is null)
            {
                throw new InvalidOperationException("Logout context did not include authentication properties.");
            }

            var redirectDestination = context.Properties.RedirectUri;
            if (string.IsNullOrWhiteSpace(redirectDestination))
            {
                redirectDestination = signedOutRedirect;
            }

            if (string.IsNullOrWhiteSpace(redirectDestination))
            {
                redirectDestination = context.Options.SignedOutRedirectUri;
            }

            if (string.IsNullOrWhiteSpace(redirectDestination))
            {
                redirectDestination = "/";
            }

            if (redirectDestination.StartsWith("/", StringComparison.Ordinal))
            {
                redirectDestination = string.Concat(request.Scheme, "://", request.Host.ToUriComponent(), redirectDestination);
            }

            context.Properties.RedirectUri = redirectDestination;

            var logoutUri = string.Concat(cognitoDomain.TrimEnd('/'), "/logout");
            var clientId = context.Options.ClientId ?? cognitoSection["ClientId"] ?? string.Empty;
            var redirectUrl = $"{logoutUri}?client_id={Uri.EscapeDataString(clientId)}&logout_uri={Uri.EscapeDataString(callback)}";

            context.Response.Redirect(redirectUrl);
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnSignedOutCallbackRedirect = context =>
        {
            string? target = context.Properties?.RedirectUri;

            if (string.IsNullOrWhiteSpace(target))
            {
                target = !string.IsNullOrWhiteSpace(signedOutRedirect)
                    ? signedOutRedirect
                    : context.Options.SignedOutRedirectUri;
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                target = "/";
            }

            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                var request = context.HttpContext.Request;
                target = string.Concat(request.Scheme, "://", request.Host.ToUriComponent(), target);
            }

            context.Response.Redirect(target);
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();
builder.Services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddHttpClient<VerifactuApiClient>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<VerifactuApiOptions>>().Value;

    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
