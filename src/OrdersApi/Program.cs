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
    .ConfigureResource(r => r.AddService("OrdersApi"))
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

app.MapGet("/orders", () =>
{
    var orders = new[]
    {
        new { Id = 101, Customer = "Alice",   Total = 34.98m,  Status = "Shipped"    },
        new { Id = 102, Customer = "Bob",     Total = 24.99m,  Status = "Processing" },
        new { Id = 103, Customer = "Charlie", Total = 9.49m,   Status = "Delivered"  },
    };
    return Results.Ok(orders);
});

app.Run();

