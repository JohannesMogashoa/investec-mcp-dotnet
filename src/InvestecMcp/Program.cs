using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using InvestecMcp.Banking;
using InvestecMcp.Security;
using InvestecMcp.Tools;

[assembly: InternalsVisibleTo("InvestecMcp.Tests")]

namespace InvestecMcp;

internal static class Program
{
    private static Task Main(string[] args) => RunAsync(args);

    internal static Task RunAsync(string[] args, Action<IMcpServerBuilder>? configureTools = null)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.PostConfigure<ConsoleLoggerOptions>(options =>
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        var server = builder.Services.AddMcpServer()
            .WithStdioServerTransport();

        if (configureTools is null)
        {
            var options = InvestecOptions.FromConfiguration(builder.Configuration);
            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHttpClient<InvestecTokenProvider>();
            builder.Services.AddHttpClient<IInvestecGateway, InvestecGateway>();
            builder.Services.AddSingleton<AccountAuthorization>();
            server.WithTools<BankingTools>();
        }
        else
        {
            configureTools(server);
        }

        return builder.Build().RunAsync();
    }
}
