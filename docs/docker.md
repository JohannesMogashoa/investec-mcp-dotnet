# Docker stdio setup

Build the production image from the repository root:

```sh
docker build -t investec-mcp:local .
```

Create a private environment file outside the repository with the settings in [local configuration](configuration.md). Restrict its filesystem permissions to your user. Then launch the server with stdin attached:

```sh
docker run --rm -i --env-file /PRIVATE/PATH/investec.env --read-only --cap-drop=ALL --security-opt=no-new-privileges --tmpfs /tmp investec-mcp:local
```

The process reads MCP messages from stdin and writes protocol messages to stdout. Diagnostics go to stderr. The image runs as a non-root user, exposes no ports, and needs no published port. Do not add `-t` or `-d` to the launch command.

For Claude Desktop, use a private `claude_desktop_config.json` entry with this launch command:

```json
{
  "mcpServers": {
    "investecPersonal": {
      "command": "docker",
      "args": ["run", "--rm", "-i", "--env-file", "/PRIVATE/PATH/investec.env", "--read-only", "--cap-drop=ALL", "--security-opt=no-new-privileges", "--tmpfs", "/tmp", "investec-mcp:local"]
    }
  }
}
```

For a current VS Code/Copilot session, put the following entry in your private Copilot user configuration (`~/.copilot/mcp-config.json`, or `$COPILOT_HOME/mcp-config.json`). This is the native Copilot `mcpServers` format:

```json
{
  "mcpServers": {
    "investecPersonal": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "--rm", "-i", "--env-file", "/PRIVATE/PATH/investec.env", "--read-only", "--cap-drop=ALL", "--security-opt=no-new-privileges", "--tmpfs", "/tmp", "investec-mcp:local"],
      "tools": ["get_accounts", "get_account_balance", "get_account_transactions"]
    }
  }
}
```

Replace the placeholder path with an absolute path readable by Docker on the host. The desktop client must be able to run `docker` and reach the local Docker daemon. Keep the environment file and any credentials out of client configuration and version control.

The Copilot format and path follow the [current VS Code MCP configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration). Older editor-managed `.vscode/mcp.json` files use a different `servers` schema.

## Synthetic protocol smoke test

The `protocol-test` image contains only the test harness and synthetic banking responses. It is not the production image. Run the automated initialize, discovery, and three-tool call check with:

```sh
docker build --target protocol-test -t investec-mcp:protocol-test .
INVESTEC_DOCKER_TEST_IMAGE=investec-mcp:protocol-test INVESTEC_DOCKER_PRODUCTION_IMAGE=investec-mcp:local dotnet test -c Release --filter FullyQualifiedName~DockerProtocolTests
```

The two tests start the containers with no network, a read-only filesystem, dropped capabilities, no privilege escalation, and a temporary `/tmp`. The production image must initialize and list exactly the three read-only tools. The synthetic image must also return successful results from all three tools. Both checks reject unexpected stdout output and ensure the synthetic secret does not appear in stderr.
