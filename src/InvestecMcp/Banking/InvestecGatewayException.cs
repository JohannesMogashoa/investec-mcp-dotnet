using System.Net;

namespace InvestecMcp.Banking;

public sealed class InvestecGatewayException(string code, HttpStatusCode? statusCode = null)
    : Exception($"Investec request failed: {code}.")
{
    public string Code { get; } = code;
    public HttpStatusCode? StatusCode { get; } = statusCode;
}
