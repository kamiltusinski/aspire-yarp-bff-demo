using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Health checks
builder.Services.AddHealthChecks();

// OpenTelemetry
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("CatalogApi"))
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

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapGet("/products", () =>
{
    var products = new[]
    {
        new { Id = 1, Name = "Widget Alpha",     Price = 9.99m,  Category = "Widgets" },
        new { Id = 2, Name = "Gadget Beta",      Price = 24.99m, Category = "Gadgets" },
        new { Id = 3, Name = "Doohickey Gamma",  Price = 4.49m,  Category = "Misc" },
    };
    return Results.Ok(products);
});

app.Run();

