# Investec MCP — Version 1 Technical Specification

**Status:** Implemented V1 baseline; local release acceptance passed
**Date:** 2026-11-24  
**Target:** Single-user, local `stdio` MCP server for Codex with native and Docker protocol checks
**Technology:** .NET 11, C#, official `ModelContextProtocol` C# SDK, `HttpClient`, xUnit or equivalent test stack  
**Source reference:** https://github.com/Nicolaas0411/investec-mcp  
**Important:** This is a new .NET implementation based on the behavior of the Python upstream project, not a change to the upstream repo. API shapes and permission details must be contract-tested with Investec's current documentation/sandbox before production banking data is used. The local `docs/sa-pb-account-information.yaml` is the OpenAPI contract reference for the three V1 read endpoints.

## 1. Problem and intended outcome

The upstream Python project provides Investec API access through MCP, but the intended maintainer uses C# and TypeScript. V1 reimplements the *read-only* subset as a manageable single .NET project. Codex launches the server over `stdio` to retrieve authorized Investec account lists, balances, and transaction history without any exposed mechanism to transfer funds. Claude Desktop and VS Code/Copilot setup remains documented for a later acceptance phase.

### Success criteria
1. Codex can register the server and discover exactly the approved tools over `stdio`; native and Docker protocol tests invoke all three tools against synthetic responses.
2. The published Investec mock sandbox supports the three read calls. Production access, when enabled, requires a key restricted to selected accounts and read scopes.
3. A payment/transfer cannot be triggered by any MCP tool or reachable Investec HTTP client method, including a malicious tool argument or prompt injection in a transaction description.
4. Account/balance/transaction responses are typed, bounded, sanitized, and verified against controlled fixtures and a read-only mock sandbox smoke test.
5. A non-root Docker container can be launched interactively with `-i` to serve MCP over standard input/output.
6. Unit, protocol, and security tests pass locally without live credentials. The CI workflow is ready for a future hosted repository.

### Non-goals
No payments/transfers, beneficiaries, profiles, documents, databases, persistent transaction caching, autonomous analytics, scheduling, public hosting, OAuth login for MCP clients, marketplace publication, multi-tenancy, or ChatGPT remote connector in V1. Claude Desktop/Copilot acceptance, production-key comparison, and hosted GitHub CI are post-V1 validation work. Do not add generic HTTP request or arbitrary URL tools. Financial aggregation can be added later as deterministic services.

## 2. Source review and migration decisions

Inspected source files: `src/main.py`, `src/utils.py`, `src/tools/__init__.py`, `src/tools/accounts.py`, `src/tools/profiles.py`, `src/tools/payments.py`, `src/tools/documents.py`, `pyproject.toml`, `Dockerfile`, `.env.example`, `README.md`.

| Upstream fact | Risk or implication | V1 decision |
|---|---|---|
| `src/main.py` creates FastMCP; default transport is SSE with host `0.0.0.0` | Network exposure by default | Only `stdio` hosting; no HTTP listener |
| `register_all_tools` always registers account, profile, beneficiary, payment, document tools | Prompt can reach write tools | Explicit registration of *account* tools only; test exact advertised tool set |
| `src/utils.py` requests `accounts balances transactions transfers beneficiarypayments documents.statements documents.taxcertificates` | Unnecessary privileges | Provision a new account-restricted read-only Investec key; request only enabled read scopes |
| `_make_request(method, endpoint, **kwargs)` accepts arbitrary methods/paths internally | Write pathways could reappear later | Make private GET-only helper with endpoint templates/allowlist and no public generic request capability |
| Token expiry calculated with `time.time`, new `httpx.AsyncClient` per request | Fragile refresh and connection reuse | `IHttpClientFactory`, `TimeProvider`, single-flight token refresh, cancellation |
| MCP tools catch exceptions and return `str(e)` | Internal errors may reach model | Typed error mapping, safe messages, sanitized logging |
| JSON serialized as string in tools | Poor contract clarity | Typed responses / structured tool content as supported by pinned SDK; stable JSON shapes |
| Account transactions accept optional dates, type, pending flag | Unbounded queries and inconsistent pending behavior | Required date range with server-side upper bound; pending a separate later tool |
| Python 3.12 Dockerfile installs Python project | Rebuild needed | Multi-stage .NET 11 image, non-root, no listening port |

The upstream project documents 16 tools. The three V1 P0 tools map to its `get_accounts`, `get_account_balance`, and `get_account_transactions`. `get_pending_transactions` is P1 *only after* the current Investec endpoint is validated.

## 3. Architecture

```
Codex
       │ stdin/stdout (MCP JSON-RPC framing)
       ▼
InvestecMcp (.NET 11 Generic Host)
  ├─ MCP tool classes (3 exposed; annotations read-only)
  ├─ Request validators (date/account/range)
  ├─ Response mapper/redactor (typed output)
  ├─ Account authorization policy (configured + discovered account IDs)
  ├─ Investec read-only gateway (GET-only allowlisted requests)
  ├─ Token provider (client_credentials; refresh lock)
  └─ HttpClientFactory (HTTPS to fixed Investec hosts)
       │ HTTPS
       ▼
Investec Open API
```

One executable, one project, folders by responsibility. An optional test project is acceptable; **do not split production code into many class libraries**. Hosting is `stdio` only. The client application starts and stops the process. Never print diagnostics, banners, or secrets to stdout; use stderr for sanitized logging.

### Proposed repository structure

```
InvestecMcp/
├── src/InvestecMcp/
│   ├── InvestecMcp.csproj
│   ├── Program.cs
│   ├── Configuration/InvestecOptions.cs
│   ├── Configuration/OptionsValidation.cs
│   ├── Banking/InvestecReadClient.cs
│   ├── Banking/InvestecTokenProvider.cs
│   ├── Banking/InvestecEndpoints.cs
│   ├── Banking/InvestecException.cs
│   ├── Security/AccountAccessPolicy.cs
│   ├── Security/ResponseRedactor.cs
│   ├── Models/Accounts.cs
│   ├── Models/Balances.cs
│   ├── Models/Transactions.cs
│   ├── Tools/AccountTools.cs
│   └── Tools/TransactionTools.cs
├── tests/InvestecMcp.Tests/
│   ├── ApiContractTests.cs
│   ├── AuthTests.cs
│   ├── AuthorizationTests.cs
│   ├── ToolDiscoveryTests.cs
│   ├── ToolValidationTests.cs
│   ├── SecurityTests.cs
│   ├── Fixtures/  # synthetic only
│   └── Fakes/
├── docs/LOCAL_SETUP.md
├── docs/SECURITY.md
├── docs/OPERATIONS.md
├── .github/workflows/ci.yml
├── .dockerignore
├── .gitignore
├── Dockerfile
├── README.md
└── InvestecMcp.slnx
```

### Technology choices
- .NET 11 (choose currently supported SDK and pin via `global.json`); C# language version defaults to chosen SDK.
- `ModelContextProtocol` for host/DI/stdio tool discovery. Pin and verify current stable package version when creating the project; SDK APIs evolve.
- Microsoft.Extensions.Hosting / DependencyInjection / Options + typed `HttpClient`.
- `System.Text.Json` with explicit DTOs, tolerant of unknown response fields but strict on essential fields.
- `Microsoft.Extensions.Http.Resilience` only if needed; keep retry policy narrow.
- xUnit + fake `HttpMessageHandler` (or an equivalent .NET test framework); protocol-level test using MCP client and spawned child process.
- No database, web app, message queue, secrets manager service, or separate container service.

## 4. Investec authentication and network access

### Investec credentials
User obtains an Investec Developer client ID, client secret, and API key through Individual Connections; API key must select only permitted accounts and read permissions (account identity, balance, transactions). Check availability and exact names of the `accounts`, `balances`, and `transactions` scopes in the current API/environment. **Never fall back to requesting all scopes**.

Production endpoint templates inherited from upstream, subject to contract verification:
- OAuth `POST https://openapi.investec.com/identity/v2/oauth2/token`
- Base `https://openapi.investec.com/za/pb/v1`
- `GET /accounts`
- `GET /accounts/{accountId}/balance`
- `GET /accounts/{accountId}/transactions` (date query parameters)

The OAuth POST is a necessary exception to GET-only *banking-resource* requests. Token acquisition MUST call only the fixed OAuth endpoint. Banking operations MUST be GET-only, with an explicit route allowlist.

### Token provider specification
- Request `grant_type=client_credentials` with configured scopes, Basic client credentials, `x-api-key`.
- Keep token in process memory only; never log token or full authorization headers.
- Calculate expiry via injected `TimeProvider`, with safety margin and concurrency protection (`SemaphoreSlim` or equivalent).
- On a 401, allow at most one token refresh and retry after verifying failure is consistent with expiry; no infinite loop.
- Give requests finite timeout (e.g., 15 seconds to start, configurable), propagate cancellation, and sanitize failure messages.
- Avoid retrying arbitrary authentication failures; apply bounded retry with jitter only for clear transient cases (`429`, selected `5xx`) and honor `Retry-After` where present.

### Network egress
Fixed allowed host `openapi.investec.com` (plus a specifically configured, separately validated sandbox host for tests). Do not let MCP tool parameters construct origins, URL schemes or arbitrary endpoint paths. Fail configuration validation if base URL contains unexpected origin, credentials, query or fragment.

## 5. Tool contracts

**Tool naming:** preserve upstream names for easy comparison, but use an explicit allowlist. All advertised tools marked read-only/non-destructive using SDK-supported annotations where available; annotations are descriptive, not a security boundary.

### 5.1 `get_accounts`
**Input:** None. **Output:** list of account objects with `accountId` (opaque ID for subsequent tools), `displayName`, `productName`, `currency` where provided, and an optional masked account reference. No full account number unless explicitly required and approved. Include `retrievedAtUtc` and `count`.

### 5.2 `get_account_balance`
**Input:** `accountId: string` (must match one authorized ID; not arbitrary path component). **Output:** `accountId`, currency, current and available balance where provided, relevant extra named balance types if supported, `retrievedAtUtc`. Preserve numerical precision (decimal for money). Never invent missing values.

### 5.3 `get_account_transactions`
**Input:** `accountId: string`, `fromDate: YYYY-MM-DD`, `toDate: YYYY-MM-DD`, optional `transactionType` only if upstream API defines accepted values. V1 excludes generic `includePending` until the precise Investec behavior is verified. **Constraints:** required dates; `fromDate <= toDate`; upper bound default 90 days (configurable tighter), future end date rejected or capped with an explicit indication; maximum results/response bytes enforced; page/continuation handling only after API contract verification. A result must explicitly indicate `truncated` and `nextPage` or equivalent if the source is incomplete; never silently present a partial result as complete.

**Output:** account ID, date range, list of normalized transactions preserving transaction ID (if provided), transaction date, posting date, description, signed amount, currency, status, and source metadata needed to distinguish duplicates/reversals; `retrievedAtUtc`, `count`, `truncated`/pagination metadata. Preserve description as untrusted text; it must never be interpreted as instructions by server logic.

### Optional P1: `get_pending_transactions`
Include only after endpoint, semantics, posting behavior, and de-duplication rules are confirmed. Do not conflate posted and pending in financial summaries.

### Uniform errors
Return structured stable code and safe description, e.g. `INVALID_DATE_RANGE`, `UNKNOWN_ACCOUNT`, `NOT_AUTHORIZED`, `INVESTEC_UNAVAILABLE`, `INVESTEC_RATE_LIMITED`, `INVESTEC_AUTH_FAILED`, `RESPONSE_TOO_LARGE`. Use MCP error signaling supported by SDK, not uncontrolled exception strings. Make expected no-data a successful empty list.

## 6. Account authorization

- On startup or first use, call authorized `GET /accounts` and derive the canonical set of account IDs. Store in memory with short bounded cache; refresh on explicit failure or short TTL.
- Additionally allow optional user configuration `INVESTEC_ALLOWED_ACCOUNT_IDS` to further *restrict* (never expand) the API-authorized set.
- Reject unauthorized IDs **before** constructing a request. Only accept exact opaque IDs from the canonical set (string comparison; no path injection).
- Do not expose a tool to change the authorization policy or list secrets.
- Ensure cross-process launches do not accidentally share credentials via checked-in launch configurations.

## 7. Privacy, risk and threat model

| Threat | Control / verification |
|---|---|
| AI attempts payment | No payments tool, no write-enabled gateway, read-only key. Protocol discovery and fake-HTTP tests assert zero transfer/pay requests |
| Malicious transaction description prompt injection | Treat as untrusted data; tests containing instruction-like text; no dynamic tool registration |
| Account ID manipulation | Compare to authorized IDs; use URI-safe construction and reject unknown IDs |
| Token or account leak in logs | Structured redaction, no HTTP body/header dumps; tests with sentinel secrets |
| Hosted model sees sensitive data | Return minimum required fields, document that locally hosted MCP does **not** imply local AI processing; obtain user acceptance of provider data handling |
| Unbounded transaction retrieval | Date and result/byte limits, response truncation signal |
| API outage/expiry | Controlled retries, timeout, specific errors, expiry handling |
| Docker privilege exposure | Non-root, read-only filesystem where feasible, no exposed ports, non-persistent container |
| Malicious local process | OS user permissions and secret store; do not claim process isolation solely because stdio is local |

### Logging
- Use structured logs to stderr only with event ID, operation, elapsed time, success/failure category, and correlation ID.
- Exclude account IDs by default from logs, or emit irreversible short hashes only when justified; never emit tokens, secrets, full descriptions, raw banking JSON, request auth headers, or raw stack traces to the MCP client.
- Provide opt-in verbose debug logging with identical redaction rules.

### Credentials
Prefer per-user OS secret storage or a private environment file outside Git. Configuration examples must use placeholders and avoid embedding actual credentials. Do not bake `.env` into Docker image or use Docker build arguments for secrets. `.gitignore` includes `.env*` except `.env.example`; CI secret scan. Real credentials should not be present in issue descriptions, test snapshots, logs, traces, or PR comments.

## 8. Runtime configuration

| Setting | Required | Semantics |
|---|---|---|
| `INVESTEC_CLIENT_ID` | yes | Investec client ID (secret handling) |
| `INVESTEC_CLIENT_SECRET` | yes | confidential client credential |
| `INVESTEC_API_KEY` | yes | restricted API key |
| `INVESTEC_SCOPES` | yes/default | exact approved read scopes; fail if contains write scope |
| `INVESTEC_ALLOWED_ACCOUNT_IDS` | no | optional additional restrictive allowlist |
| `INVESTEC_API_ENVIRONMENT` | no | explicit production/sandbox choice; no arbitrary URL |
| `INVESTEC_HTTP_TIMEOUT_SECONDS` | no | bounded default 15 |
| `INVESTEC_MAX_TRANSACTION_DAYS` | no | bounded default 90 |
| `INVESTEC_MAX_TRANSACTION_ITEMS` | no | bounded default; choose via fixture and API verification |
| `INVESTEC_MAX_RESPONSE_BYTES` | no | safe model/transport response bound |
| `LOG_LEVEL` | no | safe logging verbosity |

Validate options at startup. For MCP `stdio`, avoid console logs to stdout. No `PORT`, `HOST`, or `TRANSPORT` setting required for V1.

## 9. Packaging and client integration

### Native build
`dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`, `dotnet publish -c Release -o ./publish`. With pinned official SDK, configure `Host.CreateApplicationBuilder`, `AddMcpServer().WithStdioServerTransport().WithTools...` per current SDK API. Verify tool metadata and clean stdout.

### Codex V1 setup

Use the Docker registration in [Codex setup](docs/codex.md). V1 acceptance records actual Codex tool discovery and read calls against the read-only mock sandbox, plus native and Docker protocol calls against the synthetic backend.

### Post-V1 Claude Desktop sample (`claude_desktop_config.json`)
```json
{
  "mcpServers": {
    "investecPersonal": {
      "command": "dotnet",
      "args": ["C:/ABSOLUTE/PATH/InvestecMcp.dll"]
    }
  }
}
```
Ensure environment variables are supplied securely to the launched process; do not put literal secrets in shared examples. On Windows, config is typically `%APPDATA%\\Claude\\claude_desktop_config.json`; on macOS, `~/Library/Application Support/Claude/claude_desktop_config.json`. Verify local desktop app build/settings.

### Post-V1 VS Code/Copilot user `mcp-config.json` sample
```json
{
  "mcpServers": {
    "investecPersonal": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["C:/ABSOLUTE/PATH/InvestecMcp.dll"],
      "tools": ["get_accounts", "get_account_balance", "get_account_transactions"]
    }
  }
}
```
Prefer the **Copilot user** config at `~/.copilot/mcp-config.json` (or `$COPILOT_HOME/mcp-config.json`) for personal banking to avoid sharing a sensitive connector setting in a workspace repo. Current Copilot sessions use the portable `mcpServers` format; the older `.vscode/mcp.json` `servers` format is editor-managed compatibility. Ensure the `dotnet` executable is available to both hosts. For development, process startup can use project `dotnet run`, but published DLL is the repeatable target.

### Docker
- Multi-stage Dockerfile: .NET 11 SDK build, .NET 11 runtime final; pinned base image digest in release CI when feasible.
- Run as non-root; use `ENTRYPOINT ["dotnet","InvestecMcp.dll"]`.
- Container uses `stdin/stdout` directly; **no `EXPOSE`** and no SSE/HTTP port.
- Launch: `docker run --rm -i --env-file /PRIVATE/PATH/investec.env --read-only --cap-drop=ALL --security-opt=no-new-privileges investec-mcp:v1` (adjust writable temp requirements and rootless support per host).
- When configured through MCP clients, pass `command: docker` and `args: ["run","--rm","-i", ...]`; no `-d`, no `-t`. Host clients must be able to access Docker daemon; document Docker Desktop constraints.
- `docker build` never copies `.env`, keys or test recordings.
- Add smoke test: Docker process starts, responds to `initialize` and tool discovery using synthetic/fake banking backend or contract harness, and stdout contains only protocol messages.

## 10. Tests and quality gates

### Unit
Options validation (reject payment scopes and malformed URLs); token refresh/expiry and parallel refresh; HTTP timeout; retries and throttling; monetary JSON parsing; field redaction; date windows; unauthorized account IDs; empty results and malformed API responses.

### Integration / protocol
Start compiled executable with a real MCP client library or protocol harness; call `initialize`, `tools/list`, and `tools/call` using fake Investec responses. Assert exactly three P0 tools, correct input schemas, structured result shapes, no stdout pollution, cancellation and clean shutdown. Test both native and Docker builds.

### Security/adversarial
Send crafted path/account IDs, far-future/unbounded date ranges, script-like and model-instruction transaction descriptions, sentinel `CLIENT_SECRET` in upstream exception bodies, 401/403/429/500, massive payload, attempted payment tool invocation. Assert no banking write requests and no leak in output/logs.

### Production API manual smoke tests (post-V1, explicit opt-in)
With a newly created *read-only* personal API key and only consented accounts: validate live accounts, balances, and bounded transactions against Investec app/export; redact screenshots, do not record raw data in CI. Sandbox first when possible. No automated live CI.

### CI workflow
On PR and main: restore with locked dependencies, format check, build, unit + protocol tests, security static scan and secret scan, Docker build + synthetic smoke test. No secrets needed. Set branch protection / required green checks after repo exists.

For V1 without a hosted repository, run the equivalent local checks and inspect the workflow. A hosted green run becomes an acceptance gate when a repository is created.

## 11. Definition of Done for V1

- [x] .NET 11 app starts correctly under Codex through Docker; native and Docker MCP protocol checks pass.
- [x] Exactly three read-only MCP tools discovered and functioning.
- [x] Mock sandbox access and synthetic fixtures confirm local selected-account restrictions; unknown IDs rejected pre-HTTP.
- [x] Payment/transfer paths unavailable in tools, service interface, and route allowlist; production key scopes require post-V1 validation.
- [x] Credential/token lifecycle, timeouts, limits and errors are tested.
- [x] No sensitive stdout logging; secret sanitization verified.
- [x] Native and Docker E2E tests pass with synthetic fixtures.
- [x] `README`, local setup, security caveats, Docker launch and troubleshooting documented.
- [x] Read-only mock sandbox smoke test recorded as pass/fail only (no raw financial data).
- [x] Local build, test, format, dependency, secret-scan, and Docker checks pass; hosted CI is required once a GitHub repository exists.

Claude Desktop/Copilot manual acceptance and production-key validation are post-V1 checks. Production access requires fresh confirmation of key scopes, account restrictions, date and paging behavior, and sanitized pass/fail evidence.

## 12. Decision records

**ADR-001 C# instead of Python:** Developer maintainability and extension speed outweigh small initial port cost. Python upstream serves as reference, not production dependency.  
**ADR-002 Single app:** One .NET executable minimizes deployment complexity. Tests separate as needed.  
**ADR-003 Local stdio:** Compatible with Codex without remote attack surface or OAuth server burden.
**ADR-004 Read-only by construction:** Three independent controls: Investec key permissions, gateway GET/route allowlist, MCP tool discovery.  
**ADR-005 No database:** Avoid data duplication and retention risk in personal connector V1.  
**ADR-006 Docker optional runtime:** Native execution remains primary for Codex; Docker must be equally secure and testable.

## 13. Delivery sequence and risks

1. Confirm Investec key provisioning and API contract in sandbox.
2. Bootstrap .NET host and protocol tests.
3. Implement secure token acquisition + read-only client.
4. Implement typed account and transaction tools and validation.
5. Establish security/adversarial tests and CI.
6. Validate Codex native use and synthetic protocol calls.
7. Package/test Docker stdio; complete read-only mock sandbox smoke test.
8. After V1, validate Claude/Copilot and a restricted production key when those environments exist.

**Estimated engineering effort:** 28–45 hours for a maintainable .NET implementation and tests; +4–8 hours for Docker/integration troubleshooting, contingent on API availability. Estimates are planning ranges, not guarantees.

### Open questions to verify before implementation
- Exact current Investec permissions/scope values for account, balance and transaction endpoints.
- Actual response envelope and pagination semantics; transaction history depth and rate limits.
- Whether pending-transactions endpoint is available for the personal banking API product.
- Current official MCP C# SDK stable package APIs/annotations (pin at implementation time).
- Claude Desktop and VS Code versions/enabled MCP features on a future target machine (post-V1).

## 14. Source references

- Upstream: https://github.com/Nicolaas0411/investec-mcp (inspected source listed in §2)
- Investec individual API and credential permissions: https://developer.investec.com/individuals
- Investec authorization reference: https://developerstaging.investec.com/api-reference/SA%20Open%20API%20-%20Authorization
- Official MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- Official MCP C# SDK documentation: https://csharp.sdk.modelcontextprotocol.io/concepts/getting-started.html
- VS Code MCP configuration: https://code.visualstudio.com/docs/agents/reference/mcp-configuration
- Claude local MCP setup: https://support.claude.com/en/articles/10949351-getting-started-with-local-mcp-servers-on-claude-desktop
