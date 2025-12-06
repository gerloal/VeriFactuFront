using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddScoped<IAccessTokenProvider, HttpContextAccessTokenProvider>();

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
