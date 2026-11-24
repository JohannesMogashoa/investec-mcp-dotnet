# Investec MCP V1 execution plan

This document turns the tickets in [GITHUB_BACKLOG.md](../GITHUB_BACKLOG.md) into parallel workstreams with explicit handoffs. The backlog remains the source of ticket acceptance criteria. Issue identifiers below refer to its planning IDs.

## Current state

- **Implemented and locally verified:** stdio server and child-process discovery (#01–02), configuration validation (#04), token provider and read-only gateway (#05–06), account authorization refresh (#07), DTO parsing (#08), typed upstream errors and retry coverage (#09), the three read tools (#10–12), tool allowlist and security checks (#13, #16, #18), token lifecycle tests (#15), and native protocol coverage (#17).
- **Partially complete:** API research (#03), transaction bounds across future paging behavior (#14), and CI and supply-chain checks (#19). The dependency audit and secret scan run locally; a remote CI run remains to be confirmed.
- **Still awaiting acceptance:** Claude Desktop and VS Code/Copilot setup (#20–21), sandbox verification (#24), consolidated operational documentation (#25), and the release gate (#26).
- **Verified locally so far:** 61 tests passed in a normal .NET run, formatting passed, the production and synthetic Docker images built, and both container protocol checks passed with restricted runtime flags. Production image inspection showed a non-root user and no exposed ports. The Docker tests only exercise containers when image variables are set. These checks do not establish remote CI or desktop acceptance.

## Parallel workstreams and handoffs

| Workstream | Tickets and start condition | Work and handoff |
| --- | --- | --- |
| API contract | #03, active now | The local Private Bank OpenAPI 1.0.1 document declares the three read scopes and omitted-date defaults, but no transaction paging request parameter. Verify the sandbox read grant, date limits, paging, and error behavior with sandbox credentials when available. Keep production permission claims separate from sandbox evidence. |
| Transaction completion | #12, #14; proceed with reported incompleteness while #03 paging details are pending | Preserve current date validation and bounds. Once #03 verifies paging, implement page retrieval through the fixed gateway and carry item/byte limits across pages. Otherwise continue returning the existing incomplete-response error when more pages exist. Test limits at exact boundary values and one over. |
| Authorization verification | #07; locally verified | Five-minute cache expiry, revoked account rejection after refresh, failed-refresh handling, and restrictive local allowlisting are tested. Restart the server after urgent bank-side revocation to clear cached authorization. |
| CI and supply chain | #19; local checks complete | Locked restore, high/critical NuGet audit gate, redacted Git-history secret scan, build, tests, and Docker smoke checks are configured. Confirm a remote PR run is green and enable hosted repository protections. |
| Claude Desktop | #20; setup and recovery documented | Verify client configuration and discovery using the synthetic backend, then the Investec sandbox. Record observed diagnostics without banking data. |
| VS Code/Copilot | #21; current native user configuration documented | Verify Copilot user configuration, discovery, and all three read calls on the target platform. Record observed diagnostics without banking data. |
| Docker delivery | #22–23; locally verified | Production image discovery and synthetic image discovery/calls pass over stdin/stdout without a TTY. The tests use no network, a read-only filesystem, dropped capabilities, and no-new-privileges. CI runs the container checks after building both images. |
| Operational documentation | #25; drafted | README and setup docs cover native and Docker launch, sandbox exploration, troubleshooting, environments, credential rotation, and hosted-model financial-data disclosure. Reconcile them with desktop acceptance results. |
| Release | #26; only after all release dependencies pass | Review all V1 acceptance criteria, record unresolved release blockers and known limitations, verify CI/client/container/sandbox results, and tag `v0.1.0` only after the release gate passes. |

## Integration sequence

1. Continue API research, authorization refresh tests, CI scanning, and client/Docker acceptance in parallel.
2. Use verified #03 results to finish paging and limit behavior in #12/#14. Keep DTO, gateway, and MCP tool contracts aligned.
3. Merge Docker protocol smoke coverage into CI after #23 is ready. Re-run the full native and container checks.
4. Consolidate documentation from verified acceptance results, then complete #24 and the #26 release review.

## Safety and evidence rules

- Use sandbox credentials only against `https://openapisandbox.investec.com`. Request only `accounts balances transactions` and call only the three read tools. Never exercise sandbox write scopes or endpoints.
- Do not commit credentials, access tokens, or returned banking data. Record only synthetic fixtures and sanitized pass/fail results.
- Treat sandbox observations as sandbox evidence. Production key permissions and behavior remain unconfirmed until separately verified.
- Each workstream handoff includes changed files, checks run, evidence, and unresolved items. Update the matching ticket checkboxes in the backlog only when its acceptance criteria are verified.
