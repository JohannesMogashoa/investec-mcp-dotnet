# Native desktop setup

Publish the server from the repository root:

```sh
dotnet restore --locked-mode
dotnet test -c Release
dotnet publish src/InvestecMcp/InvestecMcp.csproj -c Release -o ./publish
```

The client launches the published `InvestecMcp.dll` with `dotnet`. Replace the paths below with absolute paths on the machine where the client runs. Install the matching .NET 11 runtime there. Supply the settings in [local configuration](configuration.md) to the desktop client's process through a private OS environment or credential-backed launcher. A terminal's environment is not necessarily inherited by an already running desktop app. Keep credential values out of these JSON files.

The server targets any host with a compatible .NET 11 runtime; this checkout has been exercised under Ubuntu in WSL. The Windows and macOS desktop configurations below are setup targets, pending manual acceptance on those hosts.

## Claude Desktop

Add this entry to the private `claude_desktop_config.json` file. On Windows it is normally under `%APPDATA%\Claude`; on macOS it is normally under `~/Library/Application Support/Claude`.

```json
{
  "mcpServers": {
    "investecPersonal": {
      "command": "dotnet",
      "args": ["/ABSOLUTE/PATH/publish/InvestecMcp.dll"]
    }
  }
}
```

Fully quit and reopen Claude Desktop after changing the file or the process environment. In the chat composer, open **+ → Connectors** to find the local server and its tools. Depending on the desktop version, connection status and logs are under **Settings → Desktop app → Extensions** or **Settings → Developer**. These steps follow [Anthropic's local MCP guidance](https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop) and the [MCP host setup reference](https://py.sdk.modelcontextprotocol.io/get-started/real-host/).

## VS Code / Copilot

For a current Copilot session, choose **Chat: Open Customizations → MCP Servers → Add Server → Global → Copilot Global**, or edit the private `$COPILOT_HOME/mcp-config.json` file (`~/.copilot/mcp-config.json` when `COPILOT_HOME` is unset). The native Copilot format uses `mcpServers`:

```json
{
  "mcpServers": {
    "investecPersonal": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["/ABSOLUTE/PATH/publish/InvestecMcp.dll"],
      "tools": ["get_accounts", "get_account_balance", "get_account_transactions"]
    }
  }
}
```

Open the MCP Servers panel in the intended Copilot session and confirm that the server is enabled. Review each tool call before approving access to financial data. An existing `.vscode/mcp.json` or profile `mcp.json` uses the older `servers` format and may be forwarded by the editor, but it is not the recommended destination for a new Copilot configuration. The [VS Code configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration) describes both formats and the remote-host rules.

## Acceptance and recovery

Start with synthetic protocol tests, then use the Investec sandbox with a restricted sandbox key. In each desktop client, confirm exactly `get_accounts`, `get_account_balance`, and `get_account_transactions` appear, invoke all three against one authorized mock account and a short date range, and confirm unknown account IDs and write-tool names fail. Record pass/fail only; do not save responses or screenshots with banking data.

If the server is missing, first validate the JSON syntax, executable path, .NET runtime availability, and process environment. Restart the desktop client completely. Check its MCP server status and stderr logs for a configuration setting name or sanitized error code. If a malformed edit prevents loading, restore the previous valid JSON entry and restart. For VS Code remote sessions or Dev Containers, the command, DLL, and credentials must exist where the Copilot harness runs. See [Docker setup](docker.md) for an alternative launch path.
