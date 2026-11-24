using InvestecMcp.Banking;

namespace InvestecMcp.Security;

public sealed class AccountAuthorization(
    IInvestecGateway gateway,
    InvestecOptions options,
    TimeProvider timeProvider)
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private IReadOnlyList<InvestecAccount>? accounts;
    private DateTimeOffset expiresAt;

    public async Task<IReadOnlyList<InvestecAccount>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        if (accounts is not null && timeProvider.GetUtcNow() < expiresAt)
        {
            return accounts;
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (accounts is not null && timeProvider.GetUtcNow() < expiresAt)
            {
                return accounts;
            }

            var response = await gateway.GetAccountsAsync(cancellationToken);
            var allowed = (response.Data?.Accounts ?? [])
                .Where(account => options.AllowedAccountIds.Count == 0 ||
                    account.AccountId is not null && options.AllowedAccountIds.Contains(account.AccountId))
                .ToArray();
            accounts = allowed;
            expiresAt = timeProvider.GetUtcNow().AddMinutes(5);
            return allowed;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public async Task EnsureAuthorizedAsync(string accountId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        var allowed = await GetAccountsAsync(cancellationToken);
        if (!allowed.Any(account => string.Equals(account.AccountId, accountId, StringComparison.Ordinal)))
        {
            throw new UnauthorizedAccessException("UNKNOWN_ACCOUNT");
        }
    }
}
