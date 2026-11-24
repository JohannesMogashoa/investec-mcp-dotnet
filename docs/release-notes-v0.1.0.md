# v0.1.0 release notes

The local V1 acceptance record is in [release-status.md](release-status.md).

## Included

- A local .NET 11 MCP server using stdio with three read-only tools: `get_accounts`, `get_account_balance`, and `get_account_transactions`.
- Fixed-origin Investec token and GET-only banking gateway, account discovery and a restrictive local allowlist, bounded transaction dates and responses, sanitized error handling, and synthetic protocol tests.
- Native and Docker launch guides, plus CI checks for locked dependencies, high and critical NuGet advisories, secret patterns, formatting, tests, and Docker protocol behavior.
- Codex Docker-backed setup, observed three-tool read acceptance, and a read-only mock sandbox smoke run covering the three read paths.

## Known limitations

- The local Private Bank OpenAPI file returns `meta.totalPages` but defines no request parameter for subsequent transaction pages. The connector reports an incomplete-response error when more pages are indicated; it does not claim full history in that case.
- The default maximum transaction window is 90 calendar days. Live Investec lookback, boundary, and timezone behavior has not been verified.
- Account discovery is cached for five minutes. Restart the server after urgent bank-side account or key revocation.
- Codex acceptance used the published mock sandbox. Native and Docker synthetic protocol tests passed separately. Local account restrictions were observed in a separate Codex run; bank-side account restriction is unverified.
- Claude Desktop and VS Code/Copilot acceptance, hosted CI, full sandbox date/paging verification, live API acceptance, and production permission verification are post-V1 checks. No GitHub repository or production key is currently configured for those checks.
