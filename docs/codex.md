# Codex setup and acceptance

V1 targets Codex as its interactive MCP client. Build the production image from the repository root:

```sh
docker build -t investec-mcp:local .
```

Create a private environment file outside the repository containing the settings in [local configuration](configuration.md). For the published mock sandbox, set `INVESTEC_API_ENVIRONMENT=sandbox` and `INVESTEC_SCOPES=accounts balances transactions`, then use the sandbox client ID, secret, and API key from the [Private Bank OpenAPI document](sa-pb-account-information.yaml). Set file permissions to user read/write only. Never paste credentials into `codex mcp add` arguments or a shared config file.

Register the stdio server with Codex, replacing the path with the absolute path to your private environment file:

```sh
codex mcp add investecSandbox -- docker run --rm -i --env-file /PRIVATE/PATH/investec-sandbox.env --read-only --cap-drop=ALL --security-opt=no-new-privileges --tmpfs /tmp investec-mcp:local
codex mcp get investecSandbox
```

The registered command contains a file path but no credential values. The container uses stdin/stdout for MCP and opens no listening port. It needs outbound HTTPS to reach Investec's sandbox, so do not add `--network=none` for this live mock-data check. Restart an existing Codex session after registration so it discovers the new server.

In Codex, confirm the three tools `get_accounts`, `get_account_balance`, and `get_account_transactions` are available. Use `get_accounts` to obtain a sandbox mock account ID, then call the balance tool and a one-day transaction range for that ID. Review tool results before allowing them into a conversation. The sandbox returns mock data, but conversations can still be retained by the client or model provider.

For a credential-free protocol check, run `dotnet test -c Release --filter FullyQualifiedName~StdioBankingTests`. For a read-only sandbox check that records only test pass/fail, set `INVESTEC_SANDBOX_SMOKE_ENV_FILE` to the private file and run `dotnet test -c Release --filter FullyQualifiedName~SandboxSmokeTests`. The test does not save response payloads. Full native and Docker synthetic checks are described in [Docker setup](docker.md).

If the server is unavailable, check `codex mcp get investecSandbox`, Docker daemon access, the image tag, the absolute environment-file path, and the file permissions. Startup configuration failures name a setting without printing its value. A new Codex session is needed after a config edit. Rebuild the image after code changes.

Local acceptance on 2026-11-24: the registered Codex sandbox server completed account, balance, and short-range transaction tool calls and returned a pass/fail-only result. The private run log contained all three tool events and no sandbox credential values. This verifies Codex with mock sandbox data; it does not establish production-key permissions or complete transaction history.
