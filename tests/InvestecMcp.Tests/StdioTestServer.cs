using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;
using InvestecMcp.Banking;
using InvestecMcp.Security;
using InvestecMcp.Tools;

namespace InvestecMcp.Tests;

internal static class StdioTestServer
{
    private static Task Main(string[] args) =>
        args.Contains("--banking-test", StringComparer.Ordinal)
            ? Program.RunAsync(args, server =>
            {
                var options = InvestecOptions.FromConfiguration(
                    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["INVESTEC_CLIENT_ID"] = "synthetic-client",
                        ["INVESTEC_CLIENT_SECRET"] = "synthetic-secret",
                        ["INVESTEC_API_KEY"] = "synthetic-key",
                        ["INVESTEC_SCOPES"] = "accounts"
                    }).Build());
                server.Services.AddSingleton(options);
                server.Services.AddSingleton(TimeProvider.System);
                server.Services.AddSingleton<IInvestecGateway, SyntheticGateway>();
                server.Services.AddSingleton<AccountAuthorization>();
                server.WithTools<BankingTools>();
            })
            : Program.RunAsync(args, server => server.WithTools<DiscoveryTool>());
}

internal sealed class SyntheticGateway : IInvestecGateway
{
    public Task<InvestecResponse<AccountListData>> GetAccountsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new InvestecResponse<AccountListData>
        {
            Data = new AccountListData
            {
                Accounts = [new InvestecAccount
                {
                    AccountId = "synthetic-account",
                    ReferenceName = "Synthetic Account",
                    ProductName = "Private Bank Account",
                    AccountNumber = "00000000000"
                }]
            }
        });

    public Task<InvestecResponse<InvestecBalance>> GetBalanceAsync(
        string accountId, CancellationToken cancellationToken) =>
        Task.FromResult(new InvestecResponse<InvestecBalance>
        {
            Data = new InvestecBalance
            {
                AccountId = accountId,
                Currency = "ZAR",
                CurrentBalance = 100.25m,
                AvailableBalance = 90.25m
            }
        });

    public Task<InvestecResponse<TransactionListData>> GetTransactionsAsync(
        string accountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        Task.FromResult(new InvestecResponse<TransactionListData>
        {
            Data = new TransactionListData
            {
                Transactions = [new InvestecTransaction
                {
                    AccountId = accountId,
                    Description = "SYNTHETIC MERCHANT",
                    Type = "DEBIT",
                    Status = "POSTED",
                    PostingDate = fromDate,
                    TransactionDate = fromDate,
                    Amount = 12.34m
                }]
            },
            Meta = new InvestecMeta { TotalPages = 1 }
        });
}

internal sealed class DiscoveryTool
{
    [McpServerTool(Name = "test_ping")]
    [Description("Returns a fixed response for the stdio protocol test.")]
    public static string Ping() => "pong";
}
