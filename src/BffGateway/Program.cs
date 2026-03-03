using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ── Authentication (cookie-based, fake dev login) ──────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "bff_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.LoginPath = null;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// ── Antiforgery ────────────────────────────────────────────────────────────
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

// ── Health Checks ──────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── OpenTelemetry ──────────────────────────────────────────────────────────
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("BffGateway"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation();
        if (!string.IsNullOrEmpty(otlpEndpoint))
            t.AddOtlpExporter();
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation();
        if (!string.IsNullOrEmpty(otlpEndpoint))
            m.AddOtlpExporter();
    });

builder.Logging.AddOpenTelemetry(l =>
{
    l.IncludeFormattedMessage = true;
    if (!string.IsNullOrEmpty(otlpEndpoint))
        l.AddOtlpExporter();
});

// ── YARP Reverse Proxy ─────────────────────────────────────────────────────
// Build cluster destinations dynamically so Aspire-injected URLs are used.
var catalogUrl  = builder.Configuration["Services:CatalogApi:BaseUrl"]  ?? "http://localhost:5201";
var ordersUrl   = builder.Configuration["Services:OrdersApi:BaseUrl"]   ?? "http://localhost:5202";

builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes: new[]
        {
            new RouteConfig
            {
                RouteId   = "catalog-route",
                ClusterId = "catalog-cluster",
                Match     = new RouteMatch { Path = "/api/catalog/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/api/catalog" }
                }
            },
            new RouteConfig
            {
                RouteId   = "orders-route",
                ClusterId = "orders-cluster",
                Match     = new RouteMatch { Path = "/api/orders/{**catch-all}" },
                Transforms = new[]
                {
                    new Dictionary<string, string> { ["PathRemovePrefix"] = "/api/orders" }
                }
            }
        },
        clusters: new[]
        {
            new ClusterConfig
            {
                ClusterId    = "catalog-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["catalog"] = new DestinationConfig { Address = catalogUrl }
                }
            },
            new ClusterConfig
            {
                ClusterId    = "orders-cluster",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["orders"] = new DestinationConfig { Address = ordersUrl }
                }
            }
        });

var app = builder.Build();

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

// ── Health ─────────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapGet("/alive", () => Results.Ok(new { status = "alive" }));

// ── BFF endpoints ──────────────────────────────────────────────────────────

// GET /bff/user  – returns current user info (or 401)
app.MapGet("/bff/user", (HttpContext ctx) =>
{
    if (!ctx.User.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();

    var name  = ctx.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
    var email = ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
    return Results.Ok(new { name, email, isAuthenticated = true });
});

// GET /bff/login  – fake dev login: signs in a demo user via a query-param name
app.MapGet("/bff/login", async (HttpContext ctx, string? username) =>
{
    var user = username ?? "demo-user";
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Name,  user),
        new Claim(ClaimTypes.Email, $"{user}@example.com"),
        new Claim(ClaimTypes.Role,  "user"),
    };
    var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    return Results.Redirect("/");
});

// POST /bff/logout  – signs the user out
app.MapPost("/bff/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(ctx);
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { loggedOut = true });
});

// GET /bff/csrf  – returns an antiforgery token pair
app.MapGet("/bff/csrf", (HttpContext ctx, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(ctx);
    return Results.Ok(new
    {
        headerName     = tokens.HeaderName,
        requestToken   = tokens.RequestToken
    });
});

// ── YARP ───────────────────────────────────────────────────────────────────
app.MapReverseProxy();

// ── SPA fallback (serve index.html for unmatched routes) ───────────────────
app.MapFallbackToFile("index.html");

app.Run();

