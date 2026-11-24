#:package Aspire.Hosting.AppHost@17.0.0
#:package Aspire.Hosting.Dotnet@17.0.0

var builder = DistributedApplication.CreateBuilder(args);

// Required secret: Aspire prompts if it isn't configured.
var signalr = builder.AddParameter("signalrConnectionString", secret: true)
    .WithDescription("Connection string for an existing Azure SignalR Service.");

var backend = builder.AddDotnetProject("backend", "./backend.cs")
    .WithHttpEndpoint(port: 5202)
    .WithEnvironment("Azure__SignalR__ConnectionString", signalr);

builder.AddDotnetProject("frontend", "./frontend.cs")
    .WithHttpEndpoint(port: 5201)
    .WithEnvironment("Azure__SignalR__ConnectionString", signalr)
    .WaitFor(backend);

builder.Build().Run();
