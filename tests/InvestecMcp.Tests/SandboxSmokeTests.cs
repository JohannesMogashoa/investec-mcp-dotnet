using System.Diagnostics;
using System.Text.Json;
using InvestecMcp.Tools;

namespace InvestecMcp.Tests;

public sealed class SandboxSmokeTests
{
    [Fact]
    public async Task Sandbox_ReadOnlyToolsReturnExpectedShapes_WhenPrivateEnvironmentIsProvided()
    {
        var environmentFile = Environment.GetEnvironmentVariable("INVESTEC_SANDBOX_SMOKE_ENV_FILE");
        if (string.IsNullOrWhiteSpace(environmentFile))
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var startInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(BankingTools).Assembly.Location])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var line in File.ReadAllLines(environmentFile))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                startInfo.Environment[line[..separator]] = line[(separator + 1)..];
            }
        }

        Assert.Equal("sandbox", startInfo.Environment["INVESTEC_API_ENVIRONMENT"]);
        Assert.Equal("accounts balances transactions", startInfo.Environment["INVESTEC_SCOPES"]);
        var clientSecret = startInfo.Environment["INVESTEC_CLIENT_SECRET"];
        var apiKey = startInfo.Environment["INVESTEC_API_KEY"];
        Assert.False(string.IsNullOrWhiteSpace(clientSecret));
        Assert.False(string.IsNullOrWhiteSpace(apiKey));

        using var server = Process.Start(startInfo)!;
        var diagnostics = server.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await ProductionToolDiscoveryTests.SendAsync(server, new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2025-11-25",
                    capabilities = new { },
                    clientInfo = new { name = "sandbox-smoke", version = "1.0" }
                }
            }, timeout.Token);
            using (var initialized = await ProductionToolDiscoveryTests.ReadResponseAsync(server, timeout.Token))
            {
                Assert.True(initialized.RootElement.TryGetProperty("result", out _));
            }

            await ProductionToolDiscoveryTests.SendAsync(server,
                new { jsonrpc = "2.0", method = "notifications/initialized" }, timeout.Token);
            await ProductionToolDiscoveryTests.SendAsync(server,
                new { jsonrpc = "2.0", id = 2, method = "tools/list" }, timeout.Token);
            using (var listed = await ProductionToolDiscoveryTests.ReadResponseAsync(server, timeout.Token))
            {
                var names = listed.RootElement.GetProperty("result").GetProperty("tools")
                    .EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).Order().ToArray();
                Assert.Equal(["get_account_balance", "get_account_transactions", "get_accounts"], names);
            }

            using var accounts = await CallAsync(server, 3, "get_accounts", new { }, timeout.Token);
            var accountResult = ToolResult(accounts);
            var discovered = accountResult.GetProperty("accounts");
            Assert.True(discovered.GetArrayLength() > 0);
            var accountId = discovered[0].GetProperty("accountId").GetString();
            Assert.False(string.IsNullOrWhiteSpace(accountId));

            using var balance = await CallAsync(server, 4, "get_account_balance",
                new { accountId }, timeout.Token);
            var balanceResult = ToolResult(balance);
            if (!string.Equals(accountId, balanceResult.GetProperty("accountId").GetString(),
                    StringComparison.Ordinal))
            {
                Assert.Fail("Sandbox balance account ID did not match the discovered account.");
            }
            Assert.False(string.IsNullOrWhiteSpace(balanceResult.GetProperty("currency").GetString()));

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            using var transactions = await CallAsync(server, 5, "get_account_transactions",
                new
                {
                    accountId,
                    fromDate = today.AddDays(-1).ToString("yyyy-MM-dd"),
                    toDate = today.ToString("yyyy-MM-dd")
                }, timeout.Token);
            var transactionResult = ToolResult(transactions);
            if (!string.Equals(accountId, transactionResult.GetProperty("accountId").GetString(),
                    StringComparison.Ordinal))
            {
                Assert.Fail("Sandbox transaction account ID did not match the discovered account.");
            }
            Assert.False(transactionResult.GetProperty("truncated").GetBoolean());
            Assert.True(transactionResult.GetProperty("transactions").ValueKind == JsonValueKind.Array);

            server.StandardInput.Close();
            await server.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, server.ExitCode);
            Assert.Null(await server.StandardOutput.ReadLineAsync(timeout.Token));
            var stderr = await diagnostics;
            Assert.False(stderr.Contains(clientSecret, StringComparison.Ordinal));
            Assert.False(stderr.Contains(apiKey, StringComparison.Ordinal));
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

    private static async Task<JsonDocument> CallAsync(
        Process server, int id, string name, object arguments, CancellationToken cancellationToken)
    {
        await ProductionToolDiscoveryTests.SendAsync(server, new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new { name, arguments }
        }, cancellationToken);
        return await ProductionToolDiscoveryTests.ReadResponseAsync(server, cancellationToken);
    }

    private static JsonElement ToolResult(JsonDocument document)
    {
        var result = document.RootElement.GetProperty("result");
        if (result.TryGetProperty("isError", out var isError))
        {
            Assert.False(isError.GetBoolean());
        }
        return result.GetProperty("structuredContent").GetProperty("result");
    }
}
