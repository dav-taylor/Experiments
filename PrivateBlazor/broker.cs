#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.Azure.SignalR.Management@*
#:property PublishAOT=false
using Microsoft.Azure.SignalR.Management;

var builder = WebApplication.CreateBuilder(args);

// We will enable CORS and assume static site has a different origin
var origin = "http://localhost:5101";
builder.Services.AddCors(o => o.AddDefaultPolicy(policy => policy.WithOrigins(origin).WithMethods("POST").AllowAnyHeader()));
var app = builder.Build();
app.UseCors();

// Create instance of Azure SignalR Service Manager (used to generate client connection tokens)
using var manager = new ServiceManagerBuilder().WithOptions(options =>
{
    options.ConnectionString = builder.Configuration["Azure:SignalR:ConnectionString"];
    options.ServiceTransportType = ServiceTransportType.Transient;
}).BuildServiceManager();

// We will create tokens connecting to the blazor server componenthub.
using var hub = await manager.CreateHubContextAsync("componenthub", CancellationToken.None);

// Generate new Azure SignalR access token and provides it to connecting browser client
app.MapPost("/_blazor/negotiate", async (CancellationToken ct) =>
{
    var response = await hub.NegotiateAsync(new NegotiationOptions(), ct);
    return new { response.Url, response.AccessToken };
});

if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5102");
app.Run();
