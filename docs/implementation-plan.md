# Investec MCP V1 execution plan

This document turns the tickets in [GITHUB_BACKLOG.md](../GITHUB_BACKLOG.md) into parallel workstreams with explicit handoffs. The backlog remains the source of ticket acceptance criteria. Issue identifiers below refer to its planning IDs.

## Current state

- **Implemented and locally verified:** stdio server and child-process discovery (#01–02), configuration validation (#04), token provider and read-only gateway (#05–06), account authorization refresh (#07), DTO parsing (#08), typed upstream errors and retry coverage (#09), the three read tools (#10–12), tool allowlist and security checks (#13, #16, #18), token lifecycle tests (#15), and native protocol coverage (#17).
- **Partially complete:** API research (#03), transaction bounds across future paging behavior (#14), and CI and supply-chain checks (#19). The dependency audit and secret scan run locally; a hosted CI run awaits a repository.
- **V1 acceptance:** Codex registration and all three mock sandbox read calls passed. A separate Codex run confirmed local account restriction and safe failure. Native and Docker synthetic protocol tests passed. Claude Desktop, Copilot, production-key comparison, and hosted CI are post-V1 work.
- **Verified locally:** The .NET tests and formatting passed, the production and synthetic Docker images built, and container protocol checks passed with restricted runtime flags. Production image inspection showed a non-root user and no exposed ports. The Docker tests exercise containers when image variables are set.

## Parallel workstreams and handoffs

| Workstream | Tickets and start condition | Work and handoff |
| --- | --- | --- |
| API contract | #03, active now | The local `sa-pb-account-information.yaml` OpenAPI document declares the three read scopes and omitted-date defaults, but no transaction paging request parameter. A mock sandbox smoke test passed account, balance, and transaction reads. Verify date limits, paging, errors, and production key permissions separately. |
| Transaction completion | #12, #14; proceed with reported incompleteness while #03 paging details are pending | Preserve current date validation and bounds. Once #03 verifies paging, implement page retrieval through the fixed gateway and carry item/byte limits across pages. Otherwise continue returning the existing incomplete-response error when more pages exist. Test limits at exact boundary values and one over. |
| Authorization verification | #07; locally verified | Five-minute cache expiry, revoked account rejection after refresh, failed-refresh handling, and restrictive local allowlisting are tested. Restart the server after urgent bank-side revocation to clear cached authorization. |
| CI and supply chain | #19; local checks complete | Locked restore, high/critical NuGet audit gate, redacted Git-history secret scan, build, tests, and Docker smoke checks are configured. Run equivalent local checks for V1; confirm a remote PR run and enable branch protections after a repository exists. |
| Codex acceptance | #27; V1 release requirement | Docker-backed Codex registration, exact discovery, all three mock sandbox read calls, local account restriction, and safe failure passed. Native and Docker synthetic protocol checks passed separately. Evidence records pass/fail without banking data. |
| Claude Desktop and VS Code/Copilot | #20–21; post-V1 | Verify the documented client configuration and all three read calls when these clients are installed. Record observed diagnostics without banking data. |
| Docker delivery | #22–23; locally verified | Production image discovery and synthetic image discovery/calls pass over stdin/stdout without a TTY. The tests use no network, a read-only filesystem, dropped capabilities, and no-new-privileges. CI runs the container checks after building both images. |
| Operational documentation | #25; complete for V1 | README and setup docs cover native and Docker launch, sandbox exploration, troubleshooting, environments, credential rotation, hosted-model financial-data disclosure, and observed Codex acceptance. |
| Release | #26; only after all V1 dependencies pass | Review all revised V1 acceptance criteria, record unresolved release blockers and known limitations, verify local checks, Codex, container, and sandbox results, and tag `v0.1.0` only after the release gate passes. |

## Integration sequence

1. Continue API research, CI scanning, and Codex/Docker acceptance in parallel.
2. Use verified #03 results to finish paging and limit behavior in #12/#14. Keep DTO, gateway, and MCP tool contracts aligned.
3. Merge Docker protocol smoke coverage into CI after #23 is ready. Re-run the full native and container checks.
4. Consolidate documentation from verified Codex results, then complete #24 and the #26 release review.

## Safety and evidence rules

- Use sandbox credentials only against `https://openapisandbox.investec.com`. Request only `accounts balances transactions` and call only the three read tools. Never exercise sandbox write scopes or endpoints.
- Do not commit credentials, access tokens, or returned banking data. Record only synthetic fixtures and sanitized pass/fail results.
- Treat sandbox observations as sandbox evidence. Production key permissions and behavior remain unconfirmed until separately verified.
- Each workstream handoff includes changed files, checks run, evidence, and unresolved items. Update the matching ticket checkboxes in the backlog only when its acceptance criteria are verified.
