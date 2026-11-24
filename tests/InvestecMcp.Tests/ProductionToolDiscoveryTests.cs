using System.Diagnostics;
using System.Text.Json;
using InvestecMcp.Tools;

namespace InvestecMcp.Tests;

public sealed class ProductionToolDiscoveryTests
{
    [Fact]
    public async Task ProductionServer_AdvertisesExactlyThreeReadOnlyTools()
    {
        var startInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(BankingTools).Assembly.Location])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["INVESTEC_CLIENT_ID"] = "synthetic-client";
        startInfo.Environment["INVESTEC_CLIENT_SECRET"] = "synthetic-secret";
        startInfo.Environment["INVESTEC_API_KEY"] = "synthetic-key";
        startInfo.Environment["INVESTEC_SCOPES"] = "accounts";
        await RunDiscoveryAsync(startInfo);
    }

    internal static async Task RunDiscoveryAsync(ProcessStartInfo startInfo)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var server = Process.Start(startInfo)!;
        var stderr = server.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await SendAsync(server, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new { name = "production-discovery-test", version = "1.0" }
                }
            }, timeout.Token);
            Assert.NotNull(await ReadResponseAsync(server, timeout.Token));
            await SendAsync(server, new { jsonrpc = "2.0", method = "notifications/initialized" }, timeout.Token);
            await SendAsync(server, new { jsonrpc = "2.0", id = 2, method = "tools/list" }, timeout.Token);

            using var response = await ReadResponseAsync(server, timeout.Token);
            var tools = response.RootElement.GetProperty("result").GetProperty("tools");
            var names = tools.EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).ToArray();
            Assert.Equal(["get_account_balance", "get_account_transactions", "get_accounts"], names.Order());
            foreach (var tool in tools.EnumerateArray())
            {
                Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
            }

            server.StandardInput.Close();
            await server.WaitForExitAsync(timeout.Token);
            Assert.Null(await server.StandardOutput.ReadLineAsync(timeout.Token));
            Assert.Equal(0, server.ExitCode);
            Assert.DoesNotContain("synthetic-secret", await stderr);
        }
        finally
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    internal static async Task SendAsync(Process server, object message, CancellationToken cancellationToken)
    {
        await server.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        await server.StandardInput.FlushAsync(cancellationToken);
    }

    internal static async Task<JsonDocument> ReadResponseAsync(Process server, CancellationToken cancellationToken)
    {
        var line = await server.StandardOutput.ReadLineAsync(cancellationToken);
        Assert.NotNull(line);
        return JsonDocument.Parse(line);
    }
}
