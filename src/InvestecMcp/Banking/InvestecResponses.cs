using System.Text.Json.Serialization;

namespace InvestecMcp.Banking;

public sealed class InvestecResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("links")]
    public InvestecLinks? Links { get; init; }

    [JsonPropertyName("meta")]
    public InvestecMeta? Meta { get; init; }
}

public sealed class InvestecLinks
{
    [JsonPropertyName("self")]
    public string? Self { get; init; }
}

public sealed class InvestecMeta
{
    [JsonPropertyName("totalPages")]
    public int? TotalPages { get; init; }

    [JsonPropertyName("currentPage")]
    public int? CurrentPage { get; init; }

    [JsonPropertyName("resultCount")]
    public int? ResultCount { get; init; }

    [JsonPropertyName("totalCount")]
    public int? TotalCount { get; init; }
}

public sealed class AccountListData
{
    [JsonPropertyName("accounts")]
    public List<InvestecAccount>? Accounts { get; init; }
}

public sealed class InvestecAccount
{
    [JsonPropertyName("accountId")]
    public string? AccountId { get; init; }

    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; init; }

    [JsonPropertyName("referenceName")]
    public string? ReferenceName { get; init; }

    [JsonPropertyName("productName")]
    public string? ProductName { get; init; }

    [JsonPropertyName("kycCompliant")]
    public bool? KycCompliant { get; init; }

    [JsonPropertyName("profileId")]
    public string? ProfileId { get; init; }
}

public sealed class InvestecBalance
{
    [JsonPropertyName("accountId")]
    public string? AccountId { get; init; }

    [JsonPropertyName("currentBalance")]
    public decimal? CurrentBalance { get; init; }

    [JsonPropertyName("availableBalance")]
    public decimal? AvailableBalance { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

public sealed class TransactionListData
{
    [JsonPropertyName("transactions")]
    public List<InvestecTransaction>? Transactions { get; init; }
}

public sealed class InvestecTransaction
{
    [JsonPropertyName("accountId")]
    public string? AccountId { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("transactionType")]
    public string? TransactionType { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("cardNumber")]
    public string? CardNumber { get; init; }

    [JsonPropertyName("postedOrder")]
    public long? PostedOrder { get; init; }

    [JsonPropertyName("postingDate")]
    public DateOnly? PostingDate { get; init; }

    [JsonPropertyName("valueDate")]
    public DateOnly? ValueDate { get; init; }

    [JsonPropertyName("actionDate")]
    public DateOnly? ActionDate { get; init; }

    [JsonPropertyName("transactionDate")]
    public DateOnly? TransactionDate { get; init; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("runningBalance")]
    public decimal? RunningBalance { get; init; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}
