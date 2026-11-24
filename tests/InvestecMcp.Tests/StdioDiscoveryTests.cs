using System.Diagnostics;
using System.Text.Json;

namespace InvestecMcp.Tests;

public sealed class StdioDiscoveryTests
{
    public static TheoryData<bool> ShutdownModes =>
        OperatingSystem.IsLinux() ? new() { false, true } : new() { false };

    [Theory]
    [MemberData(nameof(ShutdownModes))]
    public async Task StdioServer_ListsOnlyTheTestToolAndShutsDownCleanly(bool sendSigterm)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var cancellationToken = timeout.Token;
        var startInfo = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [typeof(StdioTestServer).Assembly.Location, "--Logging:LogLevel:Default=Information"])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var server = Process.Start(startInfo)!;
        var stderr = server.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await SendAsync(server, """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"stdio-test","version":"1.0"}}}
                """, cancellationToken);

            var initialization = await ReadResultAsync(server, 1, cancellationToken);
            Assert.False(string.IsNullOrWhiteSpace(initialization.GetProperty("protocolVersion").GetString()));
            Assert.True(initialization.GetProperty("capabilities").TryGetProperty("tools", out _));

            await SendAsync(server, """
                {"jsonrpc":"2.0","method":"notifications/initialized"}
                """, cancellationToken);
            await SendAsync(server, """
                {"jsonrpc":"2.0","id":2,"method":"tools/list"}
                """, cancellationToken);

            var discovery = await ReadResultAsync(server, 2, cancellationToken);
            var tool = Assert.Single(discovery.GetProperty("tools").EnumerateArray());
            Assert.Equal("test_ping", tool.GetProperty("name").GetString());
            Assert.False(discovery.TryGetProperty("nextCursor", out _));

            Assert.False(server.HasExited);
            if (sendSigterm)
            {
                using var signal = Process.Start(new ProcessStartInfo(
                    "/bin/kill", ["-TERM", server.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)]))!;
                await signal.WaitForExitAsync(cancellationToken);
                Assert.Equal(0, signal.ExitCode);
            }
            else
            {
                server.StandardInput.Close();
            }

            Assert.Null(await server.StandardOutput.ReadLineAsync(cancellationToken));
            await server.WaitForExitAsync(cancellationToken);

            var diagnostics = await stderr;
            Assert.Equal(0, server.ExitCode);
            Assert.Contains("Microsoft.Hosting.Lifetime", diagnostics);
            Assert.Contains("Application is shutting down", diagnostics);
        }
        finally
        {
            if (!server.HasExited)
            {
                server.Kill(entireProcessTree: true);
                await server.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static async Task SendAsync(Process server, string message, CancellationToken cancellationToken)
    {
        await server.StandardInput.WriteLineAsync(message.AsMemory(), cancellationToken);
        await server.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonElement> ReadResultAsync(
        Process server, int id, CancellationToken cancellationToken)
    {
        var line = await server.StandardOutput.ReadLineAsync(cancellationToken);
        Assert.NotNull(line);

        using var response = JsonDocument.Parse(line);
        var message = response.RootElement;
        Assert.Equal("2.0", message.GetProperty("jsonrpc").GetString());
        Assert.Equal(id, message.GetProperty("id").GetInt32());
        return message.GetProperty("result").Clone();
    }
}
