#:sdk Microsoft.NET.Sdk.Web
#:package Dapper@2.1.*
#:package Microsoft.Data.Sqlite@10.0.*
#:package ModelContextProtocol.AspNetCore@2.2.*
#:property JsonSerializerIsReflectionEnabledByDefault=true

using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<McpTool>();

var app = builder.Build();
app.MapMcp();
app.Run("http://0.0.0.0:3001");

class McpTool
{
    string path = "sqlite.db";
    ILogger<McpTool> logger;

    public McpTool(ILogger<McpTool> logger)
    {
        this.logger = logger;
        if (!File.Exists(path))
        {
            using var db = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate");
            db.Open();
        }
    }

    [McpServerTool(ReadOnly = true), Description("Runs SQLite SQL that returns rows. Use for SELECT, PRAGMA, EXPLAIN, or statements containing RETURNING.")]
    object QuerySql(string sql)
    {
        logger.LogInformation("query sql: {sql}", sql);
        using var db = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        return Invoke(() => db.Query(sql));
    }

    [McpServerTool, Description("Runs SQLite SQL that does not return rows. Use for CREATE, DROP, INSERT, UPDATE, DELETE, and other commands without RETURNING.")]
    object ExecuteSql(string sql)
    {
        logger.LogInformation("execute sql: {sql}", sql);
        using var db = new SqliteConnection($"Data Source={path}");
        var result = Invoke(() => db.Execute(sql));
        return result is int rowsAffected ? new { rowsAffected } : result;
    }

    // Utility function to wrap SqliteException and return inner message to LLM
    object Invoke(Func<object> action)
    {
        try
        {
            var result = action();
            // Limit to 100 rows returned over MCP
            if (result is IEnumerable<dynamic> rowResult) result = rowResult.Take(100);
            logger.LogInformation("sql result: {result}", result switch
            {
                int rowsAffected => $"{rowsAffected} rows affected",
                IEnumerable<dynamic> rows => $"returned {rows.Count()} rows",
                _ => "completed"
            });
            return result;
        }
        catch (SqliteException ex)
        {
            logger.LogWarning("sql error {message}", ex.Message);
            return ex.Message;
        }
    }
}
