using InvestecMcp.Security;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Tests;

public sealed class InvestecOptionsTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["INVESTEC_CLIENT_ID"] = "synthetic-client",
        ["INVESTEC_CLIENT_SECRET"] = "secret-sentinel",
        ["INVESTEC_API_KEY"] = "synthetic-key",
        ["INVESTEC_SCOPES"] = "accounts"
    };

    [Fact]
    public void ValidReadOnlySettings_UseFixedProductionEndpointsAndBounds()
    {
        var options = Load();

        Assert.Equal("https://openapi.investec.com/za/pb/v1/", options.ApiBaseUri.AbsoluteUri);
        Assert.Equal("https://openapi.investec.com/identity/v2/oauth2/token", options.TokenUri.AbsoluteUri);
        Assert.Equal("accounts", options.Scopes);
        Assert.Equal(90, options.MaxTransactionDays);
        Assert.Equal(200, options.MaxTransactionItems);
        Assert.Equal(65536, options.MaxResponseBytes);
    }

    [Fact]
    public void SandboxChoice_UsesOnlyApprovedSandboxOrigin()
    {
        var options = Load("INVESTEC_API_ENVIRONMENT", "sandbox");

        Assert.Equal("openapisandbox.investec.com", options.ApiBaseUri.Host);
        Assert.Equal("openapisandbox.investec.com", options.TokenUri.Host);
    }

    [Theory]
    [InlineData("INVESTEC_CLIENT_ID", null)]
    [InlineData("INVESTEC_CLIENT_SECRET", " ")]
    [InlineData("INVESTEC_API_KEY", null)]
    [InlineData("INVESTEC_SCOPES", null)]
    [InlineData("INVESTEC_SCOPES", "accounts transfers")]
    [InlineData("INVESTEC_SCOPES", "accounts beneficiarypayments")]
    [InlineData("INVESTEC_SCOPES", "accounts accounts")]
    [InlineData("INVESTEC_API_ENVIRONMENT", "https://evil.example")]
    [InlineData("INVESTEC_BASE_URL", "https://evil.example")]
    [InlineData("INVESTEC_HTTP_TIMEOUT_SECONDS", "0")]
    [InlineData("INVESTEC_MAX_TRANSACTION_DAYS", "91")]
    [InlineData("INVESTEC_MAX_TRANSACTION_ITEMS", "1001")]
    [InlineData("INVESTEC_MAX_RESPONSE_BYTES", "1048577")]
    [InlineData("INVESTEC_ALLOWED_ACCOUNT_IDS", "valid,")]
    public void InvalidSettings_FailWithoutLeakingValues(string setting, string? value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => Load(setting, value));

        Assert.DoesNotContain("secret-sentinel", error.ToString());
        Assert.DoesNotContain("evil.example", error.ToString());
        Assert.Contains(setting, error.Message);
    }

    private static InvestecOptions Load(string? key = null, string? value = null)
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        if (key is not null)
        {
            settings[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return InvestecOptions.FromConfiguration(configuration);
    }
}
