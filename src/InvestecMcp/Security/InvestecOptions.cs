using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Security;

public sealed class InvestecOptions
{
    private static readonly HashSet<string> ApprovedScopes =
        new(StringComparer.Ordinal) { "accounts", "balances", "transactions" };

    private InvestecOptions(
        string clientId,
        string clientSecret,
        string apiKey,
        string scopes,
        IReadOnlySet<string> allowedAccountIds,
        Uri apiBaseUri,
        Uri tokenUri,
        TimeSpan httpTimeout,
        int maxTransactionDays,
        int maxTransactionItems,
        int maxResponseBytes)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
        ApiKey = apiKey;
        Scopes = scopes;
        AllowedAccountIds = allowedAccountIds;
        ApiBaseUri = apiBaseUri;
        TokenUri = tokenUri;
        HttpTimeout = httpTimeout;
        MaxTransactionDays = maxTransactionDays;
        MaxTransactionItems = maxTransactionItems;
        MaxResponseBytes = maxResponseBytes;
    }

    public string ClientId { get; }
    public string ClientSecret { get; }
    public string ApiKey { get; }
    public string Scopes { get; }
    public IReadOnlySet<string> AllowedAccountIds { get; }
    public Uri ApiBaseUri { get; }
    public Uri TokenUri { get; }
    public TimeSpan HttpTimeout { get; }
    public int MaxTransactionDays { get; }
    public int MaxTransactionItems { get; }
    public int MaxResponseBytes { get; }

    public static InvestecOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var clientId = Required(configuration, "INVESTEC_CLIENT_ID");
        var clientSecret = Required(configuration, "INVESTEC_CLIENT_SECRET");
        var apiKey = Required(configuration, "INVESTEC_API_KEY");
        var scopes = Required(configuration, "INVESTEC_SCOPES")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (scopes.Length == 0 || scopes.Distinct(StringComparer.Ordinal).Count() != scopes.Length ||
            scopes.Any(scope => !ApprovedScopes.Contains(scope)))
        {
            throw Invalid("INVESTEC_SCOPES");
        }

        var environment = configuration["INVESTEC_API_ENVIRONMENT"] ?? "production";
        var origin = environment switch
        {
            "production" => "https://openapi.investec.com",
            "sandbox" => "https://openapisandbox.investec.com",
            _ => throw Invalid("INVESTEC_API_ENVIRONMENT")
        };

        if (!string.IsNullOrWhiteSpace(configuration["INVESTEC_BASE_URL"]) ||
            !string.IsNullOrWhiteSpace(configuration["INVESTEC_TOKEN_URL"]))
        {
            throw Invalid("INVESTEC_BASE_URL or INVESTEC_TOKEN_URL");
        }

        var allowlist = new HashSet<string>(StringComparer.Ordinal);
        var accountList = configuration["INVESTEC_ALLOWED_ACCOUNT_IDS"];
        if (accountList is not null)
        {
            foreach (var accountId in accountList.Split(',', StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrEmpty(accountId) || accountId.Any(char.IsControl))
                {
                    throw Invalid("INVESTEC_ALLOWED_ACCOUNT_IDS");
                }

                allowlist.Add(accountId);
            }
        }

        return new InvestecOptions(
            clientId,
            clientSecret,
            apiKey,
            string.Join(' ', scopes),
            allowlist,
            new Uri($"{origin}/za/pb/v1/"),
            new Uri($"{origin}/identity/v2/oauth2/token"),
            TimeSpan.FromSeconds(BoundedInteger(configuration, "INVESTEC_HTTP_TIMEOUT_SECONDS", 15, 1, 60)),
            BoundedInteger(configuration, "INVESTEC_MAX_TRANSACTION_DAYS", 90, 1, 90),
            BoundedInteger(configuration, "INVESTEC_MAX_TRANSACTION_ITEMS", 200, 1, 1000),
            BoundedInteger(configuration, "INVESTEC_MAX_RESPONSE_BYTES", 65536, 1024, 1048576));
    }

    private static string Required(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? throw Invalid(key) : value.Trim();
    }

    private static int BoundedInteger(IConfiguration configuration, string key, int fallback, int minimum, int maximum)
    {
        var raw = configuration[key];
        if (raw is null)
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > maximum)
        {
            throw Invalid(key);
        }

        return value;
    }

    private static InvalidOperationException Invalid(string key) =>
        new($"Invalid or missing configuration setting: {key}.");
}
