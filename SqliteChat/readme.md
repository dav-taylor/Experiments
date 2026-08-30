# SQLite Chat
This project provides an example implementation, ```mcp.cs```, of an HTTP MCP server that connects to a SQLite database. The MCP server starts on port 3001 by default.

There is also a local AI chat console app, ```chat.cs```, configured to connect to the MCP server over HTTP.

We have tested Ollama as the inference engine on a Raspberry Pi 5 using the lightweight ```granite4:7b-a1b-h``` model. It has 7 billion total parameters but activates only around 1 billion parameters per token. Ollama's default version uses Q4_K_M quantization and is only around 4.2 GB, so it runs comfortably on a Raspberry Pi 5 with 8 GB RAM. It is the instruction-tuned rather than thinking/reasoning variant, which also helps keep inference relatively fast.

The chat app uses Ollama at http://localhost:11434/, so make sure Ollama is running before starting the app.

The Microsoft.Extensions.AI abstractions are used throughout the chat application, so there is very little Ollama-specific code. Other inference backends can be used by adding the appropriate provider package and replacing the new OllamaApiClient(...) line with the equivalent IChatClient implementation.

## Instructions
First make sure you have installed dotnet:
```
curl -fsSL https://aka.ms/dotnet/dotnetup/preview/get-dotnetup.sh | bash
```

Make sure you have Ollama installed:
```
curl -fsSL https://ollama.com/install.sh | sh
```

Ensure Ollama is running:
```
ollama serve
```

Pull the Granite model if you don't already have it:
```
ollama pull granite4:7b-a1b-h
```

Start the MCP server using this command:
```
dotnet mcp.cs
```

Now start the chat app using this command:
```
dotnet chat.cs
```

Both C# programs use the .NET 10 file-based app format, so they can be run directly from their .cs files without creating .csproj project files.

The MCP server creates a SQLite database named sqlite.db by default.
