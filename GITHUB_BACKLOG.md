# GitHub Implementation Backlog — Investec MCP V1

**Target repository:** New user-controlled repository (not specified; do not write issues to upstream).  
**Baseline:** `TECHNICAL_SPEC.md` (2026-11-24).  
**Process:** Create six milestone labels (M0–M5), epics as tracking issues, and the issue-ready tasks below. All tasks link to the technical specification.  
**Estimation:** S ≤ 2h, M 2–4h, L 4–8h, XL >8h (split XL if discovered).

## Milestones / critical path

| Milestone | Goal | Issues | Gate |
|---|---|---|---|
| M0 — Architecture and baseline | Establish repo and API feasibility | 01–04 | Compiles, contract fixtures available |
| M1 — Read-only Investec foundation | Credential and banking gateway | 05–09 | No write paths, fake account GET works |
| M2 — MCP read tools | Reliable client-facing contracts | 10–14 | Three tools, bounds and auth enforced |
| M3 — Security and testing | Robust protocol and adversarial coverage | 15–19 | Automated tests and clean logs |
| M4 — Desktop and Docker | Deploy/install locally | 20–23 | Both AI clients plus Docker pass |
| M5 — Release | Final validation, documentation | 24–26 | V1 definition of done achieved |

**Dependency chain:** 01 → 02 → 05 → 06 → 07 → 10 → 11/12 → 13 → 17 → 20/21 → 24 → 26. Other tasks are parallelizable as indicated.

## Label taxonomy

- **type:** `type:feature`, `type:security`, `type:test`, `type:docs`, `type:chore`, `type:research`, `type:ci`
- **area:** `area:hosting`, `area:investec-api`, `area:auth`, `area:tools`, `area:testing`, `area:docker`, `area:docs`
- **priority:** `priority:P0` (release blocker), `priority:P1` (required for polish), `priority:P2` (deferred)
- **size:** `size:S`, `size:M`, `size:L`
- **status/project board:** `Backlog`, `Ready`, `In Progress`, `Review`, `Done` (prefer Projects fields, not labels)

## Issue-ready tickets

### #01 — Bootstrap .NET 11 solution and baseline repository
**Labels:** `type:chore`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M0  
**Depends on:** None

**Description:** Create a new repository with one production `.NET 11` executable project and one test project. Add `.slnx`, pinned SDK `global.json`, `.editorconfig`, gitignore, dependency lock policy, and base README. No Python in runtime. 

**Acceptance criteria:**
- [ ] `dotnet restore`, `dotnet build -c Release`, `dotnet test` run on clean checkout.
- [x] Production app uses Generic Host, DI and official `ModelContextProtocol` SDK for `stdio`.
- [x] No listener is opened on an HTTP port.
- [x] stdout reserved exclusively for MCP protocol messages.
- [ ] No secrets in example config or history.

### #02 — Register MCP stdio server and deterministic tool discovery test
**Labels:** `type:feature`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M0  
**Depends on:** 01

**Description:** Configure server hosting using current pinned C# SDK; add a test-only tool first and protocol harness; later replace with approved tools.

**Acceptance criteria:**
- [x] MCP `initialize` and `tools/list` succeed via a real child process.
- [x] Server starts/stops cleanly under cancellation.
- [x] Stderr logs do not corrupt stdout JSON-RPC.
- [x] Tool discovery test wired into CI.

### #03 — Investigate current Investec API contract and credential permissions
**Labels:** `type:research`, `area:investec-api`, `priority:P0`, `size:M`  
**Milestone:** M0  
**Depends on:** None

**Description:** Verify account, balance, transaction, token endpoint request and response schema, permissions, pagination, filtering, transaction date rules, errors, sandbox availability. Capture **synthetic** fixtures only.

**Research:** [Investec Private Banking API contract](docs/investec-api-contract.md)

**Acceptance criteria:**
- [x] Document source URLs and verified endpoint schemas.
- [ ] Confirm exact permitted scopes and key-level privileges for personal read-only access.
- [ ] Confirm account ID handling and transaction history/date limits.
- [x] Record unresolved differences from Python upstream as issues.
- [x] No real tokens or financial data committed.

**Remaining work:** The local Private Bank OpenAPI 1.0.1 file declares the three read scopes and omitted-date defaults, but key permission labels, live scope enforcement, transaction date limits, and upstream error details need sandbox or production verification. The document declares no transaction paging request parameter despite returning `meta.totalPages`; obtain that contract from Investec before automatic paging.

### #04 — Configuration validation and safe local secret loading
**Labels:** `type:security`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M0  
**Depends on:** 01

**Description:** Typed options; fail-fast when credentials missing, write scopes configured, base URL invalid, range/response caps unsafely large. Adopt environment variables and optional OS secret-store documentation.

**Acceptance criteria:**
- [x] Only approved production/sandbox base URL is accepted.
- [x] `transfers` and `beneficiarypayments` scopes are rejected.
- [x] No full secrets displayed in exceptions or logs.
- [x] `.env` excluded by Git and Docker contexts.
- [x] Tests cover invalid/missing settings.

### #05 — Implement client-credentials token provider
**Labels:** `type:feature`, `area:auth`, `priority:P0`, `size:L`  
**Milestone:** M1  
**Depends on:** 03,04

**Description:** OAuth client credentials flow against fixed Investec token URL with `x-api-key`; cache in memory only; refresh with `TimeProvider`, expiry buffer and concurrency lock.

**Acceptance criteria:**
- [x] Correct form data and auth headers in mock handler.
- [x] Multiple simultaneous requests perform one token refresh.
- [x] Expired token refreshes and invalid credentials fail safely.
- [x] No token leak into logs/output.
- [x] Cancellation and timeout covered.

### #06 — Build read-only endpoint gateway with HttpClientFactory
**Labels:** `type:security`, `area:investec-api`, `priority:P0`, `size:L`  
**Milestone:** M1  
**Depends on:** 03,05

**Description:** Implement allowlisted GET requests for accounts, balances, transactions, independent of MCP tools; use fixed origin and typed endpoint builders. Token POST remains isolated within auth service.

**Acceptance criteria:**
- [x] No generic arbitrary-method or arbitrary-URL API exposed.
- [x] No POST/PUT/PATCH/DELETE banking requests can be produced.
- [x] HTTPS origin and endpoint whitelist verified in fake HTTP tests.
- [x] Timeouts and cancellation propagate; 429/5xx retry bounded.
- [x] Mocked transaction/balance GET returns typed envelopes.

### #07 — Implement account discovery and authorization policy
**Labels:** `type:security`, `area:investec-api`, `priority:P0`, `size:M`  
**Milestone:** M1  
**Depends on:** 06

**Description:** Derive account IDs from authenticated account endpoint and intersect with optional user-configured additional allowlist. No tool may bypass this policy.

**Acceptance criteria:**
- [x] Unknown account IDs rejected without network call to account-specific endpoint.
- [x] Account IDs never treated as raw URL path segments.
- [x] Empty allowed set denies account-specific access.
- [x] Restrictive local allowlist cannot increase bank-provided authorization.
- [x] Refresh behavior documented and tested.

### #08 — Typed Investec DTOs and response contract mapping
**Labels:** `type:feature`, `area:investec-api`, `priority:P0`, `size:L`  
**Milestone:** M1  
**Depends on:** 03,06

**Description:** Model account, balance, transaction response envelopes; preserve decimals, dates, currency, transaction state and API pagination fields. Distinguish missing from zero.

**Acceptance criteria:**
- [x] Synthetic Investec fixture deserialization tests pass.
- [x] Unknown upstream JSON fields tolerated, required field problems surfaced safely.
- [x] Money never parsed through binary floating point.
- [x] Raw account numbers excluded from model output by default.
- [x] No false completeness if API returns limited data.

### #09 — Typed sanitized upstream errors and transient retry policy
**Labels:** `type:security`, `area:investec-api`, `priority:P0`, `size:M`  
**Milestone:** M1  
**Depends on:** 05,06

**Description:** Convert HTTP errors into safe stable error codes and structured logs; optional bounded 401 refresh retry; honor 429 limits.

**Acceptance criteria:**
- [x] Defined mapping for 400,401,403,404,429,5xx, network timeout, malformed response.
- [x] Upstream response bodies not surfaced to AI unchecked.
- [x] No unbounded retry loop.
- [x] All messages redact a supplied secret sentinel.

### #10 — Implement `get_accounts` MCP tool
**Labels:** `type:feature`, `area:tools`, `priority:P0`, `size:M`  
**Milestone:** M2  
**Depends on:** 02,07,08

**Description:** No-argument tool emits sanitized list of authorized accounts with stable schema and read-only annotation.

**Acceptance criteria:**
- [x] Tool listed as `get_accounts` with no user arguments.
- [x] Returns account ID and safe labels, no raw credentials.
- [x] Empty list valid.
- [x] Unauthorized accounts filtered by policy.
- [x] Contract tests match schema.

### #11 — Implement `get_account_balance` MCP tool
**Labels:** `type:feature`, `area:tools`, `priority:P0`, `size:M`  
**Milestone:** M2  
**Depends on:** 07,08,10

**Description:** Authorized account input; typed balance response with retrieved timestamp and correct currency.

**Acceptance criteria:**
- [x] Unauthorized account rejected pre-request.
- [x] Current/available balances differentiated, absent fields not made up.
- [x] Precision/currency tests pass.
- [x] Tool annotated read-only.

### #12 — Implement `get_account_transactions` MCP tool
**Labels:** `type:feature`, `area:tools`, `priority:P0`, `size:L`  
**Milestone:** M2  
**Depends on:** 07,08,10

**Description:** Bounded account transaction retrieval with required ISO dates, correct date semantics, and truthfully reported completeness.

**Acceptance criteria:**
- [x] Enforce valid dates, `from <= to`, max window default 90 days.
- [x] Correct query parameter casing/format verified against current API.
- [x] Multi-page behavior handled or explicitly reported incomplete.
- [x] Output bounded by items and bytes; truncated flag or continuation present.
- [x] Preserve distinct transaction date vs posting date.
- [x] Tool annotated read-only.

**Remaining work:** Implement page retrieval after #03 confirms the paging contract. Until then, keep reporting incomplete results when additional pages exist.

### #13 — Strict tool allowlist and forbidden-write security tests
**Labels:** `type:security`, `area:tools`, `priority:P0`, `size:M`  
**Milestone:** M2  
**Depends on:** 10,11,12

**Description:** Test protocol `tools/list` exact set, and fake banking gateway for all tool paths. No payments, transfers, profiles, documents or generic HTTP tools in V1.

**Acceptance criteria:**
- [x] Exact approved tool list is `get_accounts`, `get_account_balance`, `get_account_transactions`.
- [x] Invoking `transfer_money`, `pay_beneficiary`, `transfer_multiple`, `pay_multiple` fails as unknown tool.
- [x] No banking-resource non-GET request in any scenario.
- [x] Test guards against accidental future auto-discovery/registration.

### #14 — Add transaction response bounds and pagination safeguards
**Labels:** `type:security`, `area:tools`, `priority:P0`, `size:M`  
**Milestone:** M2  
**Depends on:** 12

**Description:** Enforce configurable results and bytes ceilings, small sensible defaults, and no silent truncation.

**Acceptance criteria:**
- [x] Oversized fixture cannot cause unbounded return.
- [x] Explicit `truncated`/continuation metadata or safe error provided.
- [x] No claim of full monthly history when data incomplete.
- [x] Limits tested at boundaries for a single page.

**Remaining work:** Verify item and byte limits against paging behavior from #03 and #12, then test boundary cases across all pages. Contradictory `currentPage` metadata on a purported single-page result is rejected now.

### #15 — Test token lifecycle, retries, failures and cancellation
**Labels:** `type:test`, `area:testing`, `priority:P0`, `size:L`  
**Milestone:** M3  
**Depends on:** 05,09

**Description:** Simulated time + fake HttpMessageHandler covering refresh races, expiry, retries and cancellation.

**Acceptance criteria:**
- [x] Concurrency test ensures one refresh for simultaneous requests.
- [x] Timeout, 401,403,429,500 behaviors verified.
- [x] Tests deterministic with no external calls.
- [x] Retry attempts bounded and inspectable.

### #16 — Add privacy/redaction and prompt-injection tests
**Labels:** `type:security`, `area:testing`, `priority:P0`, `size:M`  
**Milestone:** M3  
**Depends on:** 08,12

**Description:** Synthetic transactions contain malicious instruction strings; upstream error body contains secret markers; test logs and tool responses for leaks.

**Acceptance criteria:**
- [x] Tool does not treat transaction descriptions as executable instructions.
- [x] Secret sentinel absent from stdout/stderr/test snapshots.
- [x] Account numbers masked or omitted in MCP output.
- [x] No sensitive data in normal logs.

### #17 — MCP end-to-end protocol harness
**Labels:** `type:test`, `area:testing`, `priority:P0`, `size:L`  
**Milestone:** M3  
**Depends on:** 13,14

**Description:** Spawn compiled server, use a real MCP client or framing harness to initialize, list and call all read tools backed by controlled fake Investec responses.

**Acceptance criteria:**
- [x] Tool invocation and structured response schema pass.
- [x] Protocol stdout is clean; logs only on stderr.
- [x] Startup/shutdown/cancellation pass on target OS.
- [x] No live Investec credentials required.

### #18 — Security and input fuzz cases
**Labels:** `type:test`, `area:testing`, `priority:P0`, `size:M`  
**Milestone:** M3  
**Depends on:** 07,12,13

**Description:** Exercise malformed account IDs, path separators, unauthorized IDs, dates, large inputs and forbidden tools.

**Acceptance criteria:**
- [x] No request reaches unintended route/host.
- [x] Error shape stable for invalid inputs.
- [x] Unsupported methods never issued.
- [x] Date and response limits cannot be bypassed by adversarial arguments.

### #19 — GitHub Actions CI and supply chain checks
**Labels:** `type:ci`, `area:testing`, `priority:P0`, `size:M`  
**Milestone:** M3  
**Depends on:** 01,17

**Description:** CI workflow to restore locked dependencies, format, build, test, scan secrets, build the Docker image, and run the Docker protocol smoke test after #23 is ready.

**Acceptance criteria:**
- [x] PR/main runs without banking credentials.
- [x] Failing tests stop pipeline.
- [x] Logs contain only synthetic data.
- [x] Dependency and secret scanning policy documented.

### #20 — Claude Desktop local installation and manual acceptance
**Labels:** `type:docs`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M4  
**Depends on:** 17

**Description:** Document native publish, Claude MCP config (`mcpServers`), secure secret provision, restart, logs and troubleshooting. Test with synthetic backend first.

**Acceptance criteria:**
- [ ] Claude enumerates exactly three tools.
- [ ] All three tools work against mock/sandbox backend.
- [x] A malformed config has documented recovery steps.
- [x] No secrets embedded in shared example.

**Remaining work:** Complete the documented setup and manual acceptance using the synthetic backend, then sandbox if available.

Sandbox onboarding is documented in [Local configuration](docs/configuration.md). Verify the Claude Desktop launch with sandbox credentials during acceptance.

### #21 — VS Code / Copilot local installation and acceptance
**Labels:** `type:docs`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M4  
**Depends on:** 17

**Description:** Document user-profile MCP server config (`servers`), setup, tool permission review and troubleshooting on Windows and macOS/Linux.

**Acceptance criteria:**
- [ ] VS Code Copilot discovers and invokes exactly the three approved tools.
- [x] User-profile installation avoids committing finance-related workspace config.
- [x] Restart and output diagnostics documented.
- [ ] No real credentials in issue attachments.

**Remaining work:** Complete the documented setup and manual acceptance using the synthetic backend, then sandbox if available.

Sandbox onboarding is documented in [Local configuration](docs/configuration.md). Verify the VS Code/Copilot launch with sandbox credentials during acceptance.

### #22 — Docker multi-stage, non-root, stdio image
**Labels:** `type:feature`, `area:docker`, `priority:P0`, `size:L`  
**Milestone:** M4  
**Depends on:** 01,17

**Description:** Build secure .NET 11 runtime image; no listening ports; no secret baked in; container processes MCP on stdin/stdout.

**Acceptance criteria:**
- [x] Multi-stage build and non-root runtime.
- [x] `.dockerignore` excludes `.env`, test artifacts, git history.
- [x] `docker run --rm -i ...` works without `-t` or `-d`.
- [x] `docker image inspect` reveals no embedded secrets or exposed ports.
- [x] Tests with `--read-only`, `--cap-drop=ALL`, `no-new-privileges` (adapt runtime temp paths if needed).

### #23 — Docker protocol smoke test and client config
**Labels:** `type:test`, `area:docker`, `priority:P0`, `size:M`  
**Milestone:** M4  
**Depends on:** 22

**Description:** Protocol tests over Docker stdio with fake backend, example Claude and VS Code launch JSON, no host port mapping.

**Acceptance criteria:**
- [x] Container responds to initialize/list/call.
- [x] No protocol corruption or ANSI startup noise.
- [x] No network port published.
- [x] Docker stdout and stderr isolated.

**Verification:** `protocol-test` target passes initialize, discovery, and tool calls against a synthetic gateway with network disabled and restricted runtime flags. The default production image remains separate.

### #24 — Sandbox/live read-only manual verification
**Labels:** `type:test`, `area:investec-api`, `priority:P0`, `size:M`  
**Milestone:** M5  
**Depends on:** 20,21,23,03

**Description:** Verify sandbox mock-data account, balance, and transaction calls first. Then confirm a newly provisioned read-only production key works with authorized personal accounts and compare selected balances and short transaction periods to authoritative Investec view.

**Acceptance criteria:**
- [ ] Only selected account IDs accessible.
- [ ] Account and balance response shape verified.
- [ ] Small date range transactions validated, including date meaning and pagination.
- [ ] No real data persisted to repo, issues, or CI.
- [ ] Record only non-sensitive pass/fail and divergences.

**Remaining work:** Run the read-only sandbox verification using the documented sandbox mode. For production validation, use a restricted personal key and record only sanitized results.

### #25 — V1 documentation, troubleshooting, threat model
**Labels:** `type:docs`, `area:docs`, `priority:P0`, `size:M`  
**Milestone:** M5  
**Depends on:** 20,21,22

**Description:** Consolidate README, local setup, Docker instructions, security/privacy posture, commands and maintenance guidance for .NET developer.

**Acceptance criteria:**
- [x] One-command restore/test/publish instructions.
- [x] Claude/VS Code native and Docker configuration examples.
- [x] Clear disclosure that models may send financial data to hosted AI providers.
- [x] Supported environments, permission management and secret rotation documented.
- [x] Upgrade path to remote HTTP kept as future ADR, not implemented.

**Remaining work:** Reconcile observed Claude Desktop and Copilot behavior after manual acceptance. Current setup and security guides are documented but desktop behavior remains unverified.

Document the sandbox as the recommended exploration mode, including its mock-data and stateless behavior and the connector's read-only tools.

### #26 — V1 release gate and tagged build
**Labels:** `type:chore`, `area:hosting`, `priority:P0`, `size:M`  
**Milestone:** M5  
**Depends on:** 13,17,19,20,21,23,24,25

**Description:** Run and document V1 DoD, review security exceptions, tag `v0.1.0` when criteria are met, publish no secrets.

**Acceptance criteria:**
- [ ] All P0 issues complete or documented release blocker.
- [ ] CI green, synthetic end-to-end and Docker tests green.
- [ ] Claude Desktop and Copilot acceptance passed.
- [ ] No payment permissions or tools.
- [ ] Manual live smoke report contains no private banking data.
- [ ] Release notes and known limitations published.

**Remaining work:** Review and record every V1 release criterion. Create the `v0.1.0` tag only after the gate passes.

## Optional post-V1 issues (do not put in V1 release milestone)
- Pending transaction endpoint, once verified.
- Deterministic `get_spending_summary` / categorization and transfer exclusion rules.
- Secure local persistence and retention lifecycle.
- Streamable HTTP, remote MCP authorization and ChatGPT connectivity.
- Desktop packaging / native installer / MCP desktop extension.

## GitHub project configuration

**Issue templates** (after repository is created): feature, security, defect. **Suggested Project fields:** Status, Priority, Milestone, Size, Target Release. **Views:** Board grouped by Status; Table grouped by Milestone; Blocked issues filter. **Branch naming:** `feat/NN-short-description`, `security/NN-short-description`, `test/NN-short-description`. **PR checks:** CI pass, linked issue, tests for changed behavior, security checklist for banking/API changes.

**Suggested epics (optional):** EPIC-A Secure banking foundation (#03–09), EPIC-B MCP tools (#10–14), EPIC-C Verification and CI (#15–19), EPIC-D Desktop delivery and container (#20–26). Issue numbers above are *planning identifiers*, not actual GitHub issue numbers. Actual numbers are assigned when created.

## GitHub CLI import guidance

When a user-owned destination repository is selected, create labels/milestones and issues there. No issues have been created yet. To avoid guessing issue numbers or assuming dependency identifiers, import in order, then rewrite dependency links using the assigned GitHub issue numbers. An optional machine-readable backlog JSON is included separately for scripting.
