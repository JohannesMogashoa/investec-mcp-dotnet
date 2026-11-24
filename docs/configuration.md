# Local configuration

## Account authorization refresh

The server discovers account IDs from Investec and intersects them with `INVESTEC_ALLOWED_ACCOUNT_IDS` when that setting is present. The discovered list is cached for five minutes per server process. At expiry, the next account listing or account-specific call refreshes it before checking authorization. An account removed by Investec can therefore remain usable until the cached list expires; restart the server to force an immediate refresh after revoking an account. If refresh fails, the call fails and the expired list is not used. An empty intersection denies all account-specific calls.

## Try the connector with Investec's sandbox

Sandbox mode points the same three read-only MCP tools at Investec's mock-data API. It is stateless, so repeated calls return mock data and balances do not change after sandbox transfers. The connector still exposes only account listing, balances, and transaction history.

Set these environment variables in the MCP process environment using the sandbox credentials supplied by Investec:

```text
INVESTEC_API_ENVIRONMENT=sandbox
INVESTEC_CLIENT_ID=<sandbox client ID>
INVESTEC_CLIENT_SECRET=<sandbox client secret>
INVESTEC_API_KEY=<sandbox API key>
INVESTEC_SCOPES=accounts balances transactions
```

Keep the credentials in a private environment or OS secret store. Do not paste them into the client configuration file, source tree, or logs. The sandbox also offers transfer, beneficiary payment, and document scopes. This connector accepts only its three read scopes and does not expose those write or document operations.

Launch the server through your MCP client's usual stdio configuration with these variables available to its process, then call `get_accounts`, `get_account_balance`, and `get_account_transactions`. Use this mode for exploration and mock-data testing. Change `INVESTEC_API_ENVIRONMENT` explicitly to `production` when using a production key.

## Production credentials

Create a personal Direct API key in Investec Online with access to only the intended accounts and read permissions. Supply credentials to the MCP process through private environment variables or a per-user OS secret store that launches the process with those variables. Keep local credential files outside the repository and protect them with user-only permissions.

Required environment variables are `INVESTEC_CLIENT_ID`, `INVESTEC_CLIENT_SECRET`, `INVESTEC_API_KEY`, and `INVESTEC_SCOPES`. The [Private Bank OpenAPI document](sa-pb-account-information.yaml) declares `accounts`, `balances`, and `transactions` for the three read operations; configuration accepts only these scopes. Set `INVESTEC_SCOPES` to the scopes granted to the restricted key. Their live enforcement and the personal key creation labels still need confirmation in [the API contract notes](investec-api-contract.md).

Optional settings are `INVESTEC_API_ENVIRONMENT` (`production` by default or `sandbox`), `INVESTEC_ALLOWED_ACCOUNT_IDS` (comma-separated additional restrictions), `INVESTEC_HTTP_TIMEOUT_SECONDS` (1–60, default 15), `INVESTEC_MAX_TRANSACTION_DAYS` (1–90, default 90), `INVESTEC_MAX_TRANSACTION_ITEMS` (1–1000, default 200), and `INVESTEC_MAX_RESPONSE_BYTES` (1024–1048576, default 65536). The environment determines the fixed Investec origin. Arbitrary base and token URLs are rejected.

Restart the MCP process after changing settings. Startup errors name the invalid setting and omit its value. Never copy a real token, account response, or credential into a test fixture or issue.
