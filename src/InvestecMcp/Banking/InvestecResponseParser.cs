using System.Text.Json;

namespace InvestecMcp.Banking;

public static class InvestecResponseParser
{
    public static InvestecResponse<AccountListData> ParseAccounts(ReadOnlySpan<byte> json)
    {
        var response = Parse<AccountListData>(json);
        if (response.Data?.Accounts is null ||
            response.Data.Accounts.Any(account => account is null || string.IsNullOrWhiteSpace(account.AccountId)) ||
            response.Meta?.TotalPages != 1)
        {
            throw new JsonException("Invalid account response.");
        }

        return response;
    }

    public static InvestecResponse<InvestecBalance> ParseBalance(ReadOnlySpan<byte> json)
    {
        var response = Parse<InvestecBalance>(json);
        if (string.IsNullOrWhiteSpace(response.Data?.AccountId))
        {
            throw new JsonException("Invalid balance response.");
        }

        return response;
    }

    public static InvestecResponse<TransactionListData> ParseTransactions(ReadOnlySpan<byte> json)
    {
        var response = Parse<TransactionListData>(json);
        if (response.Data?.Transactions is null ||
            response.Data.Transactions.Any(transaction =>
                transaction is null ||
                string.IsNullOrWhiteSpace(transaction.AccountId) ||
                transaction.Amount is null ||
                transaction.PostingDate is null))
        {
            throw new JsonException("Invalid transaction response.");
        }

        return response;
    }

    private static InvestecResponse<T> Parse<T>(ReadOnlySpan<byte> json)
    {
        var response = JsonSerializer.Deserialize<InvestecResponse<T>>(json)
            ?? throw new JsonException("Invalid Investec response.");

        if (response.Meta?.TotalPages is < 1 || response.Meta?.CurrentPage is < 1)
        {
            throw new JsonException("Invalid pagination metadata.");
        }

        return response;
    }
}
