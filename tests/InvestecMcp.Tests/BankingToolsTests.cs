using InvestecMcp.Banking;
using InvestecMcp.Security;
using InvestecMcp.Tools;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Tests;

public sealed class BankingToolsTests
{
    [Fact]
    public async Task Balance_RejectsUnknownAccountBeforeBalanceRequest()
    {
        var gateway = new FakeGateway();
        var options = Options();
        var tools = new BankingTools(gateway, new AccountAuthorization(gateway, options, TimeProvider.System),
            options, TimeProvider.System);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tools.GetAccountBalanceAsync("other-account", CancellationToken.None));
        Assert.Equal(0, gateway.BalanceRequests);
    }

    [Fact]
    public async Task Transactions_RejectsInvalidDatesBeforeBankingRequest()
    {
        var gateway = new FakeGateway();
        var options = Options();
        var tools = new BankingTools(gateway, new AccountAuthorization(gateway, options, TimeProvider.System),
            options, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tools.GetAccountTransactionsAsync("synthetic-account", "2025-99-01", "2025-01-02", CancellationToken.None));
        Assert.Equal(0, gateway.TransactionRequests);
    }

    private static InvestecOptions Options() => InvestecOptions.FromConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["INVESTEC_CLIENT_ID"] = "synthetic-id",
            ["INVESTEC_CLIENT_SECRET"] = "synthetic-secret",
            ["INVESTEC_API_KEY"] = "synthetic-key",
            ["INVESTEC_SCOPES"] = "accounts"
        }).Build());

    private sealed class FakeGateway : IInvestecGateway
    {
        public int BalanceRequests { get; private set; }
        public int TransactionRequests { get; private set; }

        public Task<InvestecResponse<AccountListData>> GetAccountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new InvestecResponse<AccountListData>
            {
                Data = new AccountListData
                {
                    Accounts = [new InvestecAccount { AccountId = "synthetic-account" }]
                }
            });

        public Task<InvestecResponse<InvestecBalance>> GetBalanceAsync(string accountId, CancellationToken cancellationToken)
        {
            BalanceRequests++;
            return Task.FromResult(new InvestecResponse<InvestecBalance>());
        }

        public Task<InvestecResponse<TransactionListData>> GetTransactionsAsync(
            string accountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        {
            TransactionRequests++;
            return Task.FromResult(new InvestecResponse<TransactionListData>());
        }
    }
}
