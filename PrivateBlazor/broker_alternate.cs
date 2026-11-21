#:sdk Microsoft.NET.Sdk.Web
#:property PublishAOT=false
using System.Buffers.Text;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// We will enable CORS and assume static site has a different origin
var origin = "http://localhost:5101";
builder.Services.AddCors(o => o.AddDefaultPolicy(policy => policy.WithOrigins(origin).WithMethods("POST").AllowAnyHeader()));
var app = builder.Build();
app.UseCors();

// Read the Azure SignalR connection string.
var settings = new DbConnectionStringBuilder { ConnectionString = app.Configuration["Azure:SignalR:ConnectionString"] };
var endpoint = ((string)settings["Endpoint"]).TrimEnd('/');
var accessKey = Encoding.UTF8.GetBytes((string)settings["AccessKey"]);
var url = $"{endpoint}/client/?hub=componenthub";

// JWT header is constant, so encode it just once.
var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));

// Issue a signed token allowing the browser to connect to Azure SignalR.
app.MapPost("/_blazor/negotiate", () =>
{
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { aud = url, iat = now, exp = now + 3600 }));
    var content = $"{header}.{payload}";
    var signature = Base64Url.EncodeToString(HMACSHA256.HashData(accessKey, Encoding.ASCII.GetBytes(content)));
    return new { url, accessToken = $"{content}.{signature}" };
});

if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5102");
app.Run();
