# Local configuration

Create a personal Direct API key in Investec Online with access to only the intended accounts and read permissions. Supply credentials to the MCP process through private environment variables or a per-user OS secret store that launches the process with those variables. Keep local credential files outside the repository and protect them with user-only permissions.

Required environment variables are `INVESTEC_CLIENT_ID`, `INVESTEC_CLIENT_SECRET`, `INVESTEC_API_KEY`, and `INVESTEC_SCOPES`. Set `INVESTEC_SCOPES` explicitly to the verified read scope names granted to the key. Current configuration accepts only `accounts`, `balances`, and `transactions` as candidate read scope names. The exact production grant for the three reads still needs confirmation in [the API contract notes](investec-api-contract.md).

Optional settings are `INVESTEC_API_ENVIRONMENT` (`production` by default or `sandbox`), `INVESTEC_ALLOWED_ACCOUNT_IDS` (comma-separated additional restrictions), `INVESTEC_HTTP_TIMEOUT_SECONDS` (1–60, default 15), `INVESTEC_MAX_TRANSACTION_DAYS` (1–90, default 90), `INVESTEC_MAX_TRANSACTION_ITEMS` (1–1000, default 200), and `INVESTEC_MAX_RESPONSE_BYTES` (1024–1048576, default 65536). The environment determines the fixed Investec origin. Arbitrary base and token URLs are rejected.

Restart the MCP process after changing settings. Startup errors name the invalid setting and omit its value. Never copy a real token, account response, or credential into a test fixture or issue.
