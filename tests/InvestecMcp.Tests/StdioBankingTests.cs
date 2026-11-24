using System.Diagnostics;
using System.Text.Json;

namespace InvestecMcp.Tests;

public sealed class StdioBankingTests
{
    [Fact]
    public async Task BankingTools_CallAllReadsAndRejectWriteTool()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var startInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(StdioTestServer).Assembly.Location, "--banking-test"])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
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
                    clientInfo = new { name = "banking-test", version = "1.0" }
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
            Assert.Equal(1, accounts.RootElement.GetProperty("result").GetProperty("structuredContent")
                .GetProperty("result").GetProperty("count").GetInt32());
            Assert.DoesNotContain("00000000000", accounts.RootElement.GetRawText());

            using var balance = await CallAsync(server, 4, "get_account_balance",
                new { accountId = "synthetic-account" }, timeout.Token);
            Assert.Equal(100.25m, balance.RootElement.GetProperty("result")
                .GetProperty("structuredContent").GetProperty("result").GetProperty("currentBalance").GetDecimal());

            var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            using var transactions = await CallAsync(server, 5, "get_account_transactions",
                new { accountId = "synthetic-account", fromDate = today, toDate = today }, timeout.Token);
            Assert.Equal(1, transactions.RootElement.GetProperty("result")
                .GetProperty("structuredContent").GetProperty("result").GetProperty("count").GetInt32());

            var requestId = 6;
            foreach (var name in new[] { "transfer_money", "transfer_multiple", "pay_beneficiary", "pay_multiple" })
            {
                using var forbidden = await CallAsync(server, requestId++, name, new { }, timeout.Token);
                Assert.True(forbidden.RootElement.TryGetProperty("error", out _) ||
                    forbidden.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            }

            using var unknownAccount = await CallAsync(server, requestId,
                "get_account_balance", new { accountId = "unknown-account" }, timeout.Token);
            Assert.True(unknownAccount.RootElement.TryGetProperty("error", out _) ||
                unknownAccount.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());

            server.StandardInput.Close();
            await server.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, server.ExitCode);
            Assert.Null(await server.StandardOutput.ReadLineAsync(timeout.Token));
            Assert.DoesNotContain("synthetic-secret", await diagnostics);
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
}
