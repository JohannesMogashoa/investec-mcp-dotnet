using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

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

        configureTools?.Invoke(server);

        return builder.Build().RunAsync();
    }
}
