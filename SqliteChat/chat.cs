#:package ModelContextProtocol@2.2.*
#:package OllamaSharp@5.4.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OllamaSharp;

// Create ollama chat client wrapped with MCP tool calling capability
IChatClient client = new OllamaApiClient("http://localhost:11434/", "granite4:7b-a1b-h");
client = new FunctionInvokingChatClient(client);

// Create MCP connection and get list of tools
var clientTransport = new HttpClientTransport(new() { Endpoint = new Uri("http://localhost:3001") });
var mcpClient = await McpClient.CreateAsync(clientTransport);
var tools = await mcpClient.ListToolsAsync();

// Start the conversation with a friendly system prompt
List<ChatMessage> chatHistory = [new(ChatRole.System, """
    You are a friendly chatbot using tools to query and modify a SQLite database.
    You have been given access to a SQLite database so ensure any SQL queries use valid SQLite syntax.  
    Use the query_sql tool to run SELECT, PRAGMA or EXPLAIN queries and the 
    execute_sql tool for CREATE, DROP, INSERT, UPDATE and DELETE commands.
    The tool is limited to returning 100 rows, but you should use a smaller LIMIT in your query unless 
    requested by the user. Important: Actually *call* the tools and do not just show the user suggested SQL.
    You are already connected to the SQLite database file.
    """)
];

// Now run the chat loop
while (true)
{
    Console.WriteLine("\nYour prompt:");
    string? userPrompt = Console.ReadLine();
    chatHistory.Add(new(ChatRole.User, userPrompt));

    Console.WriteLine("\nAI Response:");
    string response = "";
    await foreach (var item in client.GetStreamingResponseAsync(chatHistory, new() { Tools = [.. tools] }))
    {
        Console.Write(item.Text);
        response += item.Text;
    }
    chatHistory.Add(new(ChatRole.Assistant, response));
    Console.WriteLine();
}
