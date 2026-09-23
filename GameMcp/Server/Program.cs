using GameMcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol; anything else written there corrupts it.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

int port = int.TryParse(Environment.GetEnvironmentVariable("GAME_BRIDGE_PORT"), out var p) ? p : 47831;
builder.Services.AddSingleton(new BridgeClient(port));
builder.Services.AddSingleton(new GamePaths(
    Environment.GetEnvironmentVariable("GAME_PATH") ?? @"E:\SteamLibrary\steamapps\common\Anime Shop Simulator"));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

namespace GameMcp
{
    public sealed record GamePaths(string GameDir)
    {
        public string Log(bool previous) =>
            Path.Combine(GameDir, "MelonLoader", previous ? "Latest.log.prev" : "Latest.log");
    }
}
