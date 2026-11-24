using System.Diagnostics;

namespace InvestecMcp.Tests;

public sealed class DockerProtocolTests
{
    [Fact]
    public async Task ProductionContainer_AdvertisesReadOnlyToolsWithRestrictedRuntime()
    {
        var image = Environment.GetEnvironmentVariable("INVESTEC_DOCKER_PRODUCTION_IMAGE");
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        var startInfo = CreateDockerStartInfo(image);
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "-e");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "INVESTEC_CLIENT_ID=synthetic-client");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "-e");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "INVESTEC_CLIENT_SECRET=synthetic-secret");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "-e");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "INVESTEC_API_KEY=synthetic-key");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "-e");
        startInfo.ArgumentList.Insert(startInfo.ArgumentList.Count - 1, "INVESTEC_SCOPES=accounts");
        await ProductionToolDiscoveryTests.RunDiscoveryAsync(startInfo);
    }

    [Fact]
    public async Task Container_UsesStdioForAllSyntheticBankingTools()
    {
        var image = Environment.GetEnvironmentVariable("INVESTEC_DOCKER_TEST_IMAGE");
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        await StdioBankingTests.RunProtocolAsync(CreateDockerStartInfo(image));
    }

    private static ProcessStartInfo CreateDockerStartInfo(string image)
    {
        var startInfo = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "run", "--rm", "-i", "--network=none", "--read-only", "--cap-drop=ALL",
            "--security-opt=no-new-privileges", "--tmpfs", "/tmp", image
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
