#:package Aspire.Hosting.AppHost@17.0.0
#:package Aspire.Hosting.Dotnet@17.0.0

var builder = DistributedApplication.CreateBuilder(args);

// Required secret: Aspire prompts if it isn't configured.
var signalr = builder.AddParameter("signalrConnectionString", secret: true)
    .WithDescription("Connection string for an existing Azure SignalR Service.");

var components = builder.AddDotnetProject("components", "./components.cs")
    .WithHttpEndpoint(port: 5103)
    .WithEnvironment("Azure__SignalR__ConnectionString", signalr);

var broker = builder.AddDotnetProject("broker", "./broker.cs")
    .WithHttpEndpoint(port: 5102)
    .WithEnvironment("Azure__SignalR__ConnectionString", signalr);

builder.AddDotnetProject("static", "./static.cs")
    .WithHttpEndpoint(port: 5101)
    .WaitFor(components)
    .WaitFor(broker);

builder.Build().Run();
