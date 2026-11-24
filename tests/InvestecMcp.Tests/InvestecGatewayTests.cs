using System.Net;
using System.Net.Http.Headers;
using System.Text;
using InvestecMcp.Banking;
using InvestecMcp.Security;
using Microsoft.Extensions.Configuration;

namespace InvestecMcp.Tests;

public sealed class InvestecGatewayTests
{
    [Fact]
    public async Task Gateway_UsesFixedTokenAndReadRoutes()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Auth, string? ApiKey, string? Body)>();
        var handler = new FakeHandler(async request =>
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("x-api-key", out var values) ? values.Single() : null, body));
            return Json(request.RequestUri.AbsolutePath.EndsWith("/token", StringComparison.Ordinal)
                ? """{"access_token":"synthetic-token","token_type":"Bearer","expires_in":1799,"scope":"accounts"}"""
                : """{"data":{"accounts":[]},"meta":{"totalPages":1}}""");
        });
        using var client = new HttpClient(handler);
        var options = Options();
        var tokens = new InvestecTokenProvider(client, options, TimeProvider.System);
        var gateway = new InvestecGateway(client, tokens, options, TimeProvider.System);

        await gateway.GetAccountsAsync(CancellationToken.None);
        await gateway.GetAccountsAsync(CancellationToken.None);

        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Post, requests[0].Method);
        Assert.Equal("/identity/v2/oauth2/token", requests[0].Path);
        Assert.Equal("test-key", requests[0].ApiKey);
        Assert.StartsWith("Basic ", requests[0].Auth);
        Assert.Contains("grant_type=client_credentials", requests[0].Body);
        Assert.Contains("scope=accounts", requests[0].Body);
        Assert.All(requests.Skip(1), request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/za/pb/v1/accounts", request.Path);
            Assert.Equal("Bearer synthetic-token", request.Auth);
        });
    }

    [Fact]
    public async Task Gateway_RejectsPathInjectionBeforeHttp()
    {
        var calls = 0;
        using var client = new HttpClient(new FakeHandler(_ =>
        {
            calls++;
            return Task.FromResult(Json("{}"));
        }));
        var options = Options();
        var gateway = new InvestecGateway(client, new InvestecTokenProvider(client, options, TimeProvider.System), options, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => gateway.GetBalanceAsync("../transfer", CancellationToken.None));
        Assert.Equal(0, calls);
    }

    private static InvestecOptions Options() => InvestecOptions.FromConfiguration(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["INVESTEC_CLIENT_ID"] = "test-client",
            ["INVESTEC_CLIENT_SECRET"] = "test-secret",
            ["INVESTEC_API_KEY"] = "test-key",
            ["INVESTEC_SCOPES"] = "accounts"
        }).Build());

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
