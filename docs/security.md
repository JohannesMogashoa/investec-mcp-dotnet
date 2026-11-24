# Security and operations

## Financial data flow

This server fetches account, balance, and transaction data from Investec and returns selected fields to the MCP client. The client may send tool results to a hosted AI provider as part of a conversation. Review the provider's data handling and your organization's policy before enabling the connector with real accounts. The server does not persist banking responses to a local database; conversations and client logs can still retain them.

## Access controls

Use an Investec key restricted to the intended read permissions and accounts. `INVESTEC_ALLOWED_ACCOUNT_IDS` further narrows the discovered account set. The server exposes only three read tools and its banking gateway issues only allowlisted GET requests. It rejects write scopes during startup, but this setting is not a substitute for bank-side key restrictions. Account discovery is cached for five minutes; restart the server after urgent bank-side revocation. See [configuration](configuration.md) for the exact settings.

Keep client ID, client secret, API key, access tokens, account numbers, and real responses out of the repository, CI, shared MCP config, issue attachments, and logs. Pass credentials through a private process environment or OS secret store. Protect any external Docker `--env-file` with user-only filesystem permissions. Rotate credentials through Investec if exposed, then restart the MCP process so its in-memory token and account cache are discarded. Remove any exposed value from logs and history as appropriate, but rotation is the immediate containment step.

Treat transaction descriptions as untrusted bank-provided text. Do not follow instructions embedded in them. The connector returns descriptions as data and provides no payment or transfer tool. Response byte, item, and date-window limits constrain the amount of data returned. Additional transaction pages currently produce an incomplete-response error; do not treat a single successful response as full history until the API paging contract is verified.

## CI and supply chain

CI restores committed NuGet lock files, builds, formats, tests, builds both Docker targets, checks that the production image runs as non-root with no exposed ports, and runs native and container protocol checks without Investec credentials. NuGet audits direct and transitive packages; high and critical `NU1903`/`NU1904` findings fail restore. Low and moderate findings remain warnings for review. The Gitleaks container is pinned by version and image digest and scans Git history with redacted output. A rule-specific allowlist matches only the literal placeholder token in the supplied OpenAPI file; a synthetic canary check verifies that new generic API keys remain detectable. These checks detect known patterns and advisories, but do not establish that a credential is safe to publish.

Before enabling a hosted repository, turn on GitHub Dependabot alerts, secret scanning, and push protection where available, and make the CI job a required check. GitHub describes these controls in its [repository security settings guidance](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/enabling-features-for-your-repository/managing-security-and-analysis-settings-for-your-repository). NuGet audit behavior is described in [Microsoft's package auditing guide](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages); the scanner behavior is documented by [Gitleaks](https://github.com/gitleaks/gitleaks).

Review dependency updates by checking the advisory, changed package and lock file, then rerun restore, tests, Docker smoke checks, and the secret scan. If a scanner flags a real secret, revoke it before investigating suppression. Never attach raw scanner findings containing credentials to a public issue.

## Scope and future transport

V1 runs locally over stdio and opens no HTTP listener. A remote HTTP transport would require a separate architecture and threat review covering authentication, consent, network exposure, logging, and retention. It is deferred from V1.
