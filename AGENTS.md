# Repository Guidelines

## Project Structure

This checkout contains the V1 design and backlog. Use `TECHNICAL_SPEC.md` for architecture and acceptance criteria, `GITHUB_BACKLOG.md` for ticket dependencies, and `ISSUES.json` for issue import data.

The planned .NET 11 solution is `InvestecMcp.slnx`. Put the MCP server in `src/InvestecMcp/` and keep tool handlers in `Tools/`, Investec API access in `Banking/`, and account authorization and redaction in `Security/`. Place tests in `tests/InvestecMcp.Tests/`, synthetic payloads in `Fixtures/`, and test doubles in `Fakes/`. Put setup and operations guides in `docs/`.

## Build and Local Development

After the solution is scaffolded, run these commands from the repository root.

- `dotnet restore` restores dependencies. CI should use `dotnet restore --locked-mode` once lock files are committed.
- `dotnet build -c Release` compiles the solution.
- `dotnet test -c Release` runs the test suite.
- `dotnet run --project src/InvestecMcp` starts the stdio server for local development. Configure an MCP client to launch this command.

## Coding Style

Use four-space indentation, PascalCase for C# types and methods, and camelCase for parameters and locals. Preserve the specified snake_case MCP names, such as `get_account_balance`. Follow the project's `.editorconfig` once added and use `dotnet format --verify-no-changes` to check formatting.

## Testing

Use xUnit with a fake `HttpMessageHandler` for Investec responses. Group tests by behavior and use descriptive names such as `GetBalance_RejectsUnauthorizedAccount`. Cover token refresh, account restrictions, redaction, response limits, and MCP discovery and calls through a spawned process. The spec defines behavioral checks without a numeric coverage target. Keep routine tests synthetic and reserve real API smoke tests for explicit opt-in.

## Commits and Pull Requests

Use short, imperative commit subjects. Link the relevant backlog ticket in each PR, summarize the behavior change, and report the checks you ran. Call out changes to MCP contracts or permissions.

## Security and Configuration

Keep V1 read-only and stdio-only. Reserve stdout for MCP messages and send sanitized diagnostics to stderr. Keep credentials and real banking data out of commits, fixtures, and CI logs.
