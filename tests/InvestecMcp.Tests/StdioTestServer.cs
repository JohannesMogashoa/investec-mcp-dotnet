using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace InvestecMcp.Tests;

internal static class StdioTestServer
{
    private static Task Main(string[] args) =>
        Program.RunAsync(args, server => server.WithTools<DiscoveryTool>());
}

internal sealed class DiscoveryTool
{
    [McpServerTool(Name = "test_ping")]
    [Description("Returns a fixed response for the stdio protocol test.")]
    public static string Ping() => "pong";
}
