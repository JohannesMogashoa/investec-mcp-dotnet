using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using InvestecMcp.Security;

namespace InvestecMcp.Banking;

public sealed class InvestecGateway(HttpClient httpClient, InvestecTokenProvider tokens, InvestecOptions options, TimeProvider timeProvider) : IInvestecGateway
{
    public Task<InvestecResponse<AccountListData>> GetAccountsAsync(CancellationToken cancellationToken) =>
        GetAsync("accounts", InvestecResponseParser.ParseAccounts, cancellationToken);

    public Task<InvestecResponse<InvestecBalance>> GetBalanceAsync(string accountId, CancellationToken cancellationToken) =>
        GetAsync($"accounts/{SafeAccountId(accountId)}/balance", InvestecResponseParser.ParseBalance, cancellationToken);

    public Task<InvestecResponse<TransactionListData>> GetTransactionsAsync(string accountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        if (fromDate > toDate)
        {
            throw new ArgumentException("The transaction date range is invalid.");
        }

        var route = $"accounts/{SafeAccountId(accountId)}/transactions?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}";
        return GetAsync(route, InvestecResponseParser.ParseTransactions, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string route, Func<ReadOnlySpan<byte>, T> parse, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.HttpTimeout);
        try
        {
            return await GetCoreAsync(route, parse, timeout.Token);
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

    private async Task<T> GetCoreAsync<T>(string route, Func<ReadOnlySpan<byte>, T> parse, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.ApiBaseUri, route));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                tokens.Invalidate(token);
                token = await tokens.GetTokenAsync(cancellationToken);
                continue;
            }

            if ((response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500) && attempt < 2)
            {
                var delay = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date - timeProvider.GetUtcNow()) ??
                    TimeSpan.FromMilliseconds(200 * (attempt + 1));
                await Task.Delay(delay < TimeSpan.Zero ? TimeSpan.Zero :
                    delay > TimeSpan.FromSeconds(2) ? TimeSpan.FromSeconds(2) : delay,
                    timeProvider, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvestecGatewayException(response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => "invalid_request",
                    HttpStatusCode.Unauthorized => "unauthorized",
                    HttpStatusCode.Forbidden => "forbidden",
                    HttpStatusCode.NotFound => "not_found",
                    HttpStatusCode.TooManyRequests => "rate_limited",
                    _ when (int)response.StatusCode >= 500 => "upstream_unavailable",
                    _ => "upstream_error"
                }, response.StatusCode);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken)) != 0)
                {
                    if (buffer.Length + read > options.MaxResponseBytes)
                    {
                        throw new InvestecGatewayException("response_too_large", response.StatusCode);
                    }

                    buffer.Write(chunk, 0, read);
                }

                return parse(buffer.ToArray());
            }
            catch (JsonException)
            {
                throw new InvestecGatewayException("invalid_response", response.StatusCode);
            }
        }

        throw new InvestecGatewayException("upstream_unavailable");
    }

    private static string SafeAccountId(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || !accountId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            throw new ArgumentException("The account ID is invalid.", nameof(accountId));
        }

        return Uri.EscapeDataString(accountId);
    }
}
