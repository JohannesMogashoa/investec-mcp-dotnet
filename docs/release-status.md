# V1 release gate status

This is a sanitized progress record for the planned `v0.1.0` release. No tag has been created.

## Locally verified

- The normal .NET run reports 60 passing tests, including process-level MCP discovery, read calls against synthetic data, and forbidden write-tool checks. Two Docker tests only exercise containers when their image environment variables are set; both were also run separately with those variables set.
- The production Docker image builds as a non-root stdio process with no exposed ports. Both production discovery and synthetic three-tool container protocol checks pass under restricted runtime flags.
- Locked restore, formatting, and the pinned Gitleaks history and working-tree scans pass locally. CI includes high/critical NuGet audit failures, a redacted secret scan, native tests, and Docker checks.
- Client setup, sandbox exploration, security posture, and troubleshooting are documented without real credentials or banking responses.

## Release blockers

- The GitHub repository has no configured remote in this checkout, so a hosted PR CI run and required check settings are unverified.
- Claude Desktop and Copilot have not been manually accepted with the three tools on the target desktop clients.
- No Investec sandbox credentials are available in this process environment; sandbox account, balance, and transaction calls have not been verified.
- The local OpenAPI file declares the three read scopes, but live key permission labels and enforcement, transaction date limits, and pagination request behavior remain unconfirmed. Additional or contradictory transaction page metadata returns an incomplete-response error. Full history is not claimed.
- A restricted production key and live read-only smoke comparison have not been verified. Record only pass/fail and sanitized divergences when this is done.

The release gate stays open until these items are resolved or explicitly recorded as release blockers under the backlog criteria. Do not create the `v0.1.0` tag from local test results alone.
