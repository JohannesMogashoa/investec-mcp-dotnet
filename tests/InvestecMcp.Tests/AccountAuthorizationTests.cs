using InvestecMcp.Banking;
using InvestecMcp.Security;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Tests;

public sealed class AccountAuthorizationTests
{
    [Fact]
    public async Task CachedAccounts_RefreshAtFiveMinutesAndRejectRevokedAccount()
    {
        var clock = new MutableTimeProvider();
        var gateway = new MutableGateway();
        var authorization = new AccountAuthorization(gateway, Options(), clock);

        await authorization.EnsureAuthorizedAsync("first", CancellationToken.None);
        gateway.AccountIds = ["second"];
        clock.Advance(TimeSpan.FromMinutes(5).Subtract(TimeSpan.FromTicks(1)));
        await authorization.EnsureAuthorizedAsync("first", CancellationToken.None);
        Assert.Equal(1, gateway.AccountRequests);

        clock.Advance(TimeSpan.FromTicks(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorization.EnsureAuthorizedAsync("first", CancellationToken.None));
        await authorization.EnsureAuthorizedAsync("second", CancellationToken.None);
        Assert.Equal(2, gateway.AccountRequests);
    }

    [Fact]
    public async Task Refresh_IntersectsBankAccountsWithLocalAllowlist()
    {
        var clock = new MutableTimeProvider();
        var gateway = new MutableGateway { AccountIds = ["first", "second"] };
        var authorization = new AccountAuthorization(gateway, Options("first"), clock);

        Assert.Equal(["first"], (await authorization.GetAccountsAsync(CancellationToken.None))
            .Select(account => account.AccountId));
        clock.Advance(TimeSpan.FromMinutes(5));
        gateway.AccountIds = ["second"];

        Assert.Empty(await authorization.GetAccountsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            authorization.EnsureAuthorizedAsync("second", CancellationToken.None));
        Assert.Equal(2, gateway.AccountRequests);
    }

    [Fact]
    public async Task FailedRefresh_DoesNotAuthorizeFromExpiredCache()
    {
        var clock = new MutableTimeProvider();
        var gateway = new MutableGateway();
        var authorization = new AccountAuthorization(gateway, Options(), clock);

        await authorization.EnsureAuthorizedAsync("first", CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(5));
        gateway.FailAccounts = true;

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            authorization.EnsureAuthorizedAsync("first", CancellationToken.None));
        Assert.Equal(2, gateway.AccountRequests);
    }

    private static InvestecOptions Options(string? allowedAccountIds = null) =>
        InvestecOptions.FromConfiguration(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["INVESTEC_CLIENT_ID"] = "synthetic-id",
                ["INVESTEC_CLIENT_SECRET"] = "synthetic-secret",
                ["INVESTEC_API_KEY"] = "synthetic-key",
                ["INVESTEC_SCOPES"] = "accounts",
                ["INVESTEC_ALLOWED_ACCOUNT_IDS"] = allowedAccountIds
            }).Build());

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class MutableGateway : IInvestecGateway
    {
        public string[] AccountIds { get; set; } = ["first"];
        public bool FailAccounts { get; set; }
        public int AccountRequests { get; private set; }

        public Task<InvestecResponse<AccountListData>> GetAccountsAsync(CancellationToken cancellationToken)
        {
            AccountRequests++;
            if (FailAccounts)
            {
                throw new InvalidDataException("SYNTHETIC_FAILURE");
            }

            return Task.FromResult(new InvestecResponse<AccountListData>
            {
                Data = new AccountListData
                {
                    Accounts = AccountIds.Select(id => new InvestecAccount { AccountId = id }).ToList()
                }
            });
        }

        public Task<InvestecResponse<InvestecBalance>> GetBalanceAsync(
            string accountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<InvestecResponse<TransactionListData>> GetTransactionsAsync(
            string accountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
