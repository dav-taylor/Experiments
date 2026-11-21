#!/usr/bin/env dotnet
#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.Azure.SignalR@*
#:package OpenTelemetry.Extensions.Hosting@*
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@*
#:property RootNamespace=
#:property PublishAOT=false
#:include Components/Counter.razor
using Microsoft.AspNetCore.Components.Web;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddMeter("Microsoft.AspNetCore.Components")
            .AddMeter("Microsoft.AspNetCore.Components.Lifecycle")
            .AddMeter("System.Net.Http")
            .AddMeter("System.Runtime")
        )
        .WithTracing(tracing => tracing
            .AddSource("Microsoft.AspNetCore.Components")
            .AddSource("Microsoft.AspNetCore.Components.Server.Circuits")
            .AddSource("Microsoft.AspNetCore.SignalR.Server")
            .AddSource("Components")
        )
        .UseOtlpExporter();

builder.Services.AddServerSideBlazor(options =>
{
    options.RootComponents.RegisterForJavaScript<Components.Counter>("counter");
});
builder.Services.AddSignalR().AddAzureSignalR();

var app = builder.Build();
app.MapBlazorHub();

if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5103");
app.Run();
