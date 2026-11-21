#:sdk Microsoft.NET.Sdk.Web
#:property EnableDefaultRazorComponentItems=false

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, WebRootPath = "staticwwwroot" });
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Configuration["urls"] is null) app.Urls.Add("http://localhost:5101");
app.Run();
