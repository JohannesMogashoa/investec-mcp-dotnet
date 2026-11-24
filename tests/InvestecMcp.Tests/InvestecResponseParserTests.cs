using System.Text;
using System.Text.Json;
using InvestecMcp.Banking;

namespace InvestecMcp.Tests;

public sealed class InvestecResponseParserTests
{
    [Fact]
    public void Accounts_KeepOpaqueIdsAndOptionalNames()
    {
        var response = InvestecResponseParser.ParseAccounts(ReadFixture("accounts.json"));

        Assert.Equal("9900000000000000000000001", response.Data!.Accounts![0].AccountId);
        Assert.Equal("00000000001", response.Data.Accounts[0].AccountNumber);
        Assert.Null(response.Data.Accounts[1].AccountName);
        Assert.Equal(1, response.Meta!.TotalPages);
    }

    [Fact]
    public void Balance_PreservesDecimalPrecisionAndMissingValues()
    {
        var response = InvestecResponseParser.ParseBalance(ReadFixture("balance.json"));
        Assert.Equal(100.25m, response.Data!.CurrentBalance);
        Assert.Equal(90.25m, response.Data.AvailableBalance);

        var missing = ParseBalance("""
            {"data":{"accountId":"opaque-id","currency":"ZAR"},"meta":{"totalPages":1}}
            """);
        Assert.Null(missing.Data!.CurrentBalance);
        Assert.Null(missing.Data.AvailableBalance);
    }

    [Fact]
    public void Transactions_PreserveDatesAmountAndPagination()
    {
        var response = InvestecResponseParser.ParseTransactions(ReadFixture("transactions.json"));
        var transaction = Assert.Single(response.Data!.Transactions!);

        Assert.Equal(DateOnly.Parse("2025-01-03"), transaction.PostingDate);
        Assert.Equal(DateOnly.Parse("2025-01-02"), transaction.TransactionDate);
        Assert.Equal(12.34m, transaction.Amount);
        Assert.Equal("DEBIT", transaction.Type);
        Assert.Equal(1, response.Meta!.TotalPages);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":{\"accounts\":[{}]}}")]
    [InlineData("{\"data\":{\"accounts\":[]},\"meta\":{\"totalPages\":0}}")]
    public void Accounts_RejectMissingEssentialFields(string json)
    {
        Assert.Throws<JsonException>(() => InvestecResponseParser.ParseAccounts(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void UnknownFieldsAreIgnored()
    {
        var response = ParseBalance("""
            {"data":{"accountId":"opaque-id","currentBalance":123.456789,"futureField":"value"},"newEnvelopeField":true}
            """);

        Assert.Equal(123.456789m, response.Data!.CurrentBalance);
    }

    private static InvestecResponse<InvestecBalance> ParseBalance(string json) =>
        InvestecResponseParser.ParseBalance(Encoding.UTF8.GetBytes(json));

    private static byte[] ReadFixture(string name) => File.ReadAllBytes(
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Fixtures", name)));
}
