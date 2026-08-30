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

Here is example output from the chat app:
```console
>dotnet chat.cs

Your prompt:
Can you show the rows in the authors table?

AI Response:
Here are the rows from the **authors** table (limited to the first 100 rows):

| id | first_name | last_name | birth_year |
|----|------------|-----------|------------|
| 1  | George     | Orwell    | 1903 |
| 2  | Jane       | Austen    | 1775 |
| 3  | J.K.       | Rowling   | 1965 |
| 4  | F. Scott   | Fitzgerald| 1896 |

*(Only four rows are present in this sample data.)*

Your prompt:
Hey can you change J.K.'s name to Joanne please then run a query to make sure it worked.

AI Response:
The name was successfully updated from **J.K.** to **Joanne**. Here’s the current state of the **authors** table:

| id | first_name | last_name | birth_year |
|----|------------|-----------|------------|
| 1  | George     | Orwell    | 1903 |
| 2  | Jane       | Austen    | 1775 |
| 3  | Joanne     | Rowling   | 1965 |
| 4  | F. Scott   | Fitzgerald| 1896 |

The query confirmed that the row was correctly modified.

Your prompt:
```

Here is an example of the mcp.cs app log output:
```text
info: ModelContextProtocol.Server.McpServer[570385771]
      Server (mcp 1.0.0.0), Client (chat 1.0.0.0) method 'tools/call' request handler called.
info: McpTool[0]
      execute sql: UPDATE authors SET first_name = 'Joanne' WHERE first_name = 'J.K.'
info: McpTool[0]
      sql result: 1 rows affected
info: ModelContextProtocol.Server.McpServer[2065726448]
      "execute_sql" completed. IsError = False.
info: ModelContextProtocol.Server.McpServer[1867955179]
      Server (mcp 1.0.0.0), Client (chat 1.0.0.0) method 'tools/call' request handler completed in 13.6716ms.
```
