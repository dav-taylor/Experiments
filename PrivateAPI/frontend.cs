#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.SignalR.Client@*
#:package OpenTelemetry.Extensions.Hosting@*
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@*
#:package OpenTelemetry.Instrumentation.AspNetCore@*
#:package OpenTelemetry.Instrumentation.Http@*
#:property PublishAOT=false
using System.Buffers.Text;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.SignalR.Client;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddValidation();

// Configure telemetry
const string TelemetryName = "PrivateApi.Client";
ActivitySource TelemetrySource = new(TelemetryName);
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("System.Runtime")
        )
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Microsoft.AspNetCore.SignalR.Client")
            .AddSource(TelemetryName)
        )
        .UseOtlpExporter();
}

// Read the existing connection string.
var settings = new DbConnectionStringBuilder { ConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"] };
var accessKey = Encoding.UTF8.GetBytes((string)settings["AccessKey"]);
var endpoint = ((string)settings["Endpoint"]).TrimEnd('/');
var url = $"{endpoint}/client/?hub=apihub"; // Use /api if connecting directly to server

Task<string?> GenerateTokenAsync()
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var header = Base64Url.EncodeToString("""{"alg":"HS256","typ":"JWT"}"""u8);
    var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { aud = url, iat = now, exp = now + 3600 }));
    var content = $"{header}.{payload}";
    var signature = Base64Url.EncodeToString(HMACSHA256.HashData(accessKey, Encoding.ASCII.GetBytes(content)));
    return Task.FromResult<string?>($"{content}.{signature}");
}

// Connect directly to Azure SignalR Service.
await using var connection = new HubConnectionBuilder()
    .WithUrl(url, options => options.AccessTokenProvider = () => GenerateTokenAsync())
    .WithAutomaticReconnect()
    .Build();

var app = builder.Build();
app.MapGet("/lookup/{name}", async ([RegularExpression(@"\A[a-z0-9]{1,63}\.com\z")] string name) =>
{
    using var activity = TelemetrySource.StartActivity("Domain.Lookup");
    app.Logger.LogInformation("Sending lookup for {domain} to Azure SignalR Service", name);
    return await connection.InvokeAsync<Result>("Lookup", name);  
});

while (true)
{
    try
    {
        await connection.StartAsync();
        app.Logger.LogInformation("Connected to Azure SignalR Service");
        break;
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "SignalR connection failed. Retrying in 2 seconds...");
        await Task.Delay(2000);
    }
}

if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5201");
app.Run();

public record Result(string Domain, string FirstName, string LastName);