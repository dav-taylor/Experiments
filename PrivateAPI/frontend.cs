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
using Microsoft.AspNetCore.SignalR;

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
    .WithAutomaticReconnect(new RetryPolicy())
    .Build();

var app = builder.Build();
app.MapGet("/lookup/{name}", async ([RegularExpression(@"\A[a-z0-9]{1,63}\.com\z")] string name) =>
{
    using var activity = TelemetrySource.StartActivity("Domain.Lookup");   
    app.Logger.LogInformation("Sending lookup for {domain} to Azure SignalR Service", name);
    await connection.WaitUntilConnectedAsync(timeout: TimeSpan.FromSeconds(30));
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

// This retry policy controls Azure SignalR Service reconnection attempts.
public class RetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext ctx) => ctx.PreviousRetryCount switch
    {
        0 => TimeSpan.FromSeconds(0),
        1 or 2 or 3 => TimeSpan.FromSeconds(2),
        4 or 5 or 6 => TimeSpan.FromSeconds(5),
        7 or 8 or 9 => TimeSpan.FromSeconds(10),
        _ => TimeSpan.FromSeconds(30)
    };
}

// This utility method waits for a SignalR connection to be re-established
public static class HubExtensions
{
    extension (HubConnection connection)
    {
        public async Task WaitUntilConnectedAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.State is HubConnectionState.Connected) return;
            var wait = timeout ?? TimeSpan.FromSeconds(30);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(wait);

            try
            {
                while (connection.State is not HubConnectionState.Connected)
                    await Task.Delay(100, cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"SignalR connection was not established within {wait}.");
            }
        }
    }
}
