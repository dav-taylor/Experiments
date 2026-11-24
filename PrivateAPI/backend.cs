#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.Azure.SignalR@*
#:package OpenTelemetry.Extensions.Hosting@*
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@*
#:package OpenTelemetry.Instrumentation.AspNetCore@*
#:package OpenTelemetry.Instrumentation.Http@*
#:property PublishAOT=false
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR().AddAzureSignalR();

const string TelemetryName = "PrivateApi.Server";
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("System.Runtime", "Microsoft.AspNetCore.Http.Connections", TelemetryName)
        )
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Microsoft.AspNetCore.SignalR.Server")
            .AddSource(TelemetryName)
        )
        .UseOtlpExporter();
}

var app = builder.Build();
app.MapHub<ApiHub>("/api"); // /api is local endpoint not Azure Signalr endpoint
if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5202");
app.Run();

public class ApiHub(ILogger<ApiHub> logger) : Hub
{
    public async Task<Result> Lookup(string name)
    {
        if (!Regex.IsMatch(name, @"\A[a-z0-9]{1,63}\.com\z")) throw new Exception("Invalid domain");
    
        await Task.Delay(20); // Simulate network lookup
        logger.LogInformation("Looking up {domain} on backend", name);
        return name switch
        {
            "first.com" => new(name, "Fred", "Smith"),
            "second.com" => new(name, "John", "Taylor"),
            "third.con" => new(name, "Tony", "Lambert"),
            "exception.com" => throw new Exception("Something went wrong"),
            _ => new(name, "", "")
        };
    }  
}

public record Result(string Domain, string FirstName, string LastName);