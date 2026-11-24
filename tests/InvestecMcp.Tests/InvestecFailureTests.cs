using System.Net;
using System.Text;
using InvestecMcp.Banking;
using InvestecMcp.Security;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Tests;

public sealed class InvestecFailureTests
{
    [Fact]
    public async Task ConcurrentTokenRequests_UseOneAuthenticationCall()
    {
        var calls = 0;
        using var client = Client((request, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(Json(Token));
        });
        var provider = new InvestecTokenProvider(client, Options(), TimeProvider.System);

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => provider.GetTokenAsync(CancellationToken.None)));

        Assert.All(results, result => Assert.Equal("synthetic-token", result));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExpiredToken_IsRenewedUsingInjectedClock()
    {
        var clock = new MutableTimeProvider();
        var calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Json(Token));
        });
        var provider = new InvestecTokenProvider(client, Options(), clock);

        await provider.GetTokenAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(31));
        await provider.GetTokenAsync(CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request")]
    [InlineData(HttpStatusCode.Forbidden, "forbidden")]
    [InlineData(HttpStatusCode.NotFound, "not_found")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limited")]
    [InlineData(HttpStatusCode.InternalServerError, "upstream_unavailable")]
    public async Task Gateway_MapsHttpFailureWithoutReturningBody(HttpStatusCode status, string code)
    {
        var calls = 0;
        using var client = Client((request, _) =>
        {
            if (request.Method == HttpMethod.Post) return Task.FromResult(Json(Token));
            calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("secret-sentinel")
            });
        });
        var options = Options();
        var gateway = new InvestecGateway(client,
            new InvestecTokenProvider(client, options, TimeProvider.System), options, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvestecGatewayException>(() =>
            gateway.GetAccountsAsync(CancellationToken.None));

        Assert.Equal(code, exception.Code);
        Assert.DoesNotContain("secret-sentinel", exception.ToString());
        Assert.Equal(status is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError ? 3 : 1, calls);
    }

    [Fact]
    public async Task UnauthorizedResponse_RefreshesTokenOnce()
    {
        var authCalls = 0;
        var bankingCalls = 0;
        using var client = Client((request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                authCalls++;
                return Task.FromResult(Json(Token.Replace("synthetic-token", $"synthetic-token-{authCalls}")));
            }
            bankingCalls++;
            return Task.FromResult(bankingCalls == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Json("""{"data":{"accounts":[]}}"""));
        });
        var options = Options();
        var gateway = new InvestecGateway(client,
            new InvestecTokenProvider(client, options, TimeProvider.System), options, TimeProvider.System);

        await gateway.GetAccountsAsync(CancellationToken.None);

        Assert.Equal(2, authCalls);
        Assert.Equal(2, bankingCalls);
    }

    [Fact]
    public async Task Cancellation_PropagatesToCaller()
    {
        using var client = Client(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException();
        });
        var provider = new InvestecTokenProvider(client, Options(), TimeProvider.System);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetTokenAsync(cancellation.Token));
    }

    [Fact]
    public async Task InvalidTokenResponse_HasStableError()
    {
        using var client = Client((_, _) => Task.FromResult(Json("secret-sentinel")));
        var provider = new InvestecTokenProvider(client, Options(), TimeProvider.System);

        var error = await Assert.ThrowsAsync<InvestecGatewayException>(() =>
            provider.GetTokenAsync(CancellationToken.None));
        Assert.Equal("invalid_token_response", error.Code);
        Assert.DoesNotContain("secret-sentinel", error.ToString());
    }

    private const string Token = """{"access_token":"synthetic-token","token_type":"Bearer","expires_in":1799}""";

    private static InvestecOptions Options() => InvestecOptions.FromConfiguration(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["INVESTEC_CLIENT_ID"] = "synthetic-id",
            ["INVESTEC_CLIENT_SECRET"] = "synthetic-secret",
            ["INVESTEC_API_KEY"] = "synthetic-key",
            ["INVESTEC_SCOPES"] = "accounts"
        }).Build());

    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) =>
        new(new Handler(respond));

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current += duration;
    }
}
