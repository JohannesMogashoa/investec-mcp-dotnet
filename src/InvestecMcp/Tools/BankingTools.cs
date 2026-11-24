using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using InvestecMcp.Banking;
using InvestecMcp.Security;
using ModelContextProtocol.Server;

namespace InvestecMcp.Tools;

public sealed class BankingTools(
    IInvestecGateway gateway,
    AccountAuthorization authorization,
    InvestecOptions options,
    TimeProvider timeProvider)
{
    [McpServerTool(Name = "get_accounts", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists the bank accounts authorized for this server.")]
    public async Task<object> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await authorization.GetAccountsAsync(cancellationToken);
        var safeAccounts = accounts.Select(account => new
        {
            accountId = account.AccountId,
            displayName = account.ReferenceName ?? account.AccountName ?? account.ProductName,
            productName = account.ProductName
        }).ToArray();
        var result = new { accounts = safeAccounts, count = safeAccounts.Length, retrievedAtUtc = timeProvider.GetUtcNow() };
        if (JsonSerializer.SerializeToUtf8Bytes(result).Length > options.MaxResponseBytes)
        {
            throw new InvalidDataException("RESPONSE_TOO_LARGE");
        }

        return result;
    }

    [McpServerTool(Name = "get_account_balance", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns the current and available balance for an authorized account.")]
    public async Task<object> GetAccountBalanceAsync(string accountId, CancellationToken cancellationToken)
    {
        await authorization.EnsureAuthorizedAsync(accountId, cancellationToken);
        var response = await gateway.GetBalanceAsync(accountId, cancellationToken);
        var balance = response.Data ?? throw new InvalidDataException("INVALID_RESPONSE");
        if (!string.Equals(accountId, balance.AccountId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("INVALID_RESPONSE");
        }
        var result = new
        {
            accountId = balance.AccountId,
            currency = balance.Currency,
            currentBalance = balance.CurrentBalance,
            availableBalance = balance.AvailableBalance,
            retrievedAtUtc = timeProvider.GetUtcNow()
        };
        if (JsonSerializer.SerializeToUtf8Bytes(result).Length > options.MaxResponseBytes)
        {
            throw new InvalidDataException("RESPONSE_TOO_LARGE");
        }

        return result;
    }

    [McpServerTool(Name = "get_account_transactions", ReadOnly = true, UseStructuredContent = true)]
    [Description("Lists transactions for an authorized account and bounded date range.")]
    public async Task<object> GetAccountTransactionsAsync(
        string accountId,
        string fromDate,
        string toDate,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(fromDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var from) ||
            !DateOnly.TryParseExact(toDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var to) ||
            from > to || to > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime) ||
            to.DayNumber - from.DayNumber >= options.MaxTransactionDays)
        {
            throw new ArgumentException("INVALID_DATE_RANGE");
        }

        await authorization.EnsureAuthorizedAsync(accountId, cancellationToken);
        var response = await gateway.GetTransactionsAsync(accountId, from, to, cancellationToken);
        var source = response.Data?.Transactions ?? throw new InvalidDataException("INVALID_RESPONSE");
        if (response.Meta?.TotalPages != 1 || response.Meta.CurrentPage is not null and not 1 ||
            source.Count > options.MaxTransactionItems ||
            source.Any(transaction => !string.Equals(transaction.AccountId, accountId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("INCOMPLETE_OR_INVALID_RESPONSE");
        }

        var balanceResponse = await gateway.GetBalanceAsync(accountId, cancellationToken);
        var balance = balanceResponse.Data;
        if (!string.Equals(balance?.AccountId, accountId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("INVALID_RESPONSE");
        }

        var transactions = source.Select(transaction => new
        {
            transactionDate = transaction.TransactionDate,
            postingDate = transaction.PostingDate,
            description = transaction.Description,
            amount = transaction.Amount,
            type = transaction.Type,
            status = transaction.Status,
            postedOrder = transaction.PostedOrder
        }).ToArray();
        var result = new
        {
            accountId,
            fromDate,
            toDate,
            transactions,
            currency = balance!.Currency,
            count = transactions.Length,
            truncated = false,
            retrievedAtUtc = timeProvider.GetUtcNow()
        };
        if (JsonSerializer.SerializeToUtf8Bytes(result).Length > options.MaxResponseBytes)
        {
            throw new InvalidDataException("RESPONSE_TOO_LARGE");
        }

        return result;
    }
}
