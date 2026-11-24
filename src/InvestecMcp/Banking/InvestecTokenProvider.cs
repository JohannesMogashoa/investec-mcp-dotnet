using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InvestecMcp.Security;

namespace InvestecMcp.Banking;

public sealed class InvestecTokenProvider(HttpClient httpClient, InvestecOptions options, TimeProvider timeProvider)
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private string? accessToken;
    private DateTimeOffset expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.HttpTimeout);
        try
        {
            return await GetTokenCoreAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvestecGatewayException("timeout");
        }
        catch (HttpRequestException)
        {
            throw new InvestecGatewayException("upstream_unavailable");
        }
    }

    private async Task<string> GetTokenCoreAsync(CancellationToken cancellationToken)
    {
        if (accessToken is { } token && timeProvider.GetUtcNow() < expiresAt)
        {
            return token;
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (accessToken is { } current && timeProvider.GetUtcNow() < expiresAt)
            {
                return current;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
            request.Headers.Add("x-api-key", options.ApiKey);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = options.Scopes
            });
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvestecGatewayException("authentication_failed", response.StatusCode);
            }

            try
            {
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
                var root = document.RootElement;
                token = root.GetProperty("access_token").GetString();
                var lifetime = root.GetProperty("expires_in").GetInt32();
                var tokenType = root.GetProperty("token_type").GetString();
                if (string.IsNullOrWhiteSpace(token) || lifetime <= 0 || tokenType != "Bearer")
                {
                    throw new JsonException();
                }

                accessToken = token;
                expiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(1, lifetime - Math.Min(60, lifetime / 10)));
                return token;
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new InvestecGatewayException("invalid_token_response", response.StatusCode);
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void Invalidate(string rejectedToken)
    {
        if (accessToken == rejectedToken)
        {
            expiresAt = DateTimeOffset.MinValue;
        }
    }
}
