# V1 release gate status

This is a sanitized acceptance record for `v0.1.0`. V1 targets Codex and native and Docker protocol checks.

## Locally verified

- Locked restore and Release build passed with zero warnings. The .NET suite passed 65 tests, including process-level MCP discovery, read calls against synthetic data, account restrictions, and forbidden write-tool checks. Two Docker tests exercise containers when their image environment variables are set; both passed with those variables set.
- The production Docker image builds as a non-root stdio process with no exposed ports. Both production discovery and synthetic three-tool container protocol checks pass under restricted runtime flags.
- Formatting, the pinned Gitleaks history and working-tree scans, and production image isolation checks pass locally. CI includes high/critical NuGet audit failures, a redacted secret scan, native tests, and Docker checks.
- Client setup, sandbox exploration, security posture, and troubleshooting are documented without real credentials or banking responses.
- A read-only MCP smoke test passed against Investec's published mock sandbox environment for account discovery, balance, and a short transaction range. It recorded pass/fail only and emitted no response payloads.
- Codex has an enabled local `investecSandbox` stdio server entry. An isolated Codex session called all three read tools against the mock sandbox and reported pass/fail only; its private run log contained no credential values.
- A second isolated Codex session used a restrictive local account allowlist. Account discovery returned no allowed accounts, and a balance request for a blocked synthetic ID failed before a banking read. Its temporary server entry and private logs were removed.

## Known limitations and follow-up

- This checkout has no GitHub remote, so hosted CI and repository protections are unverified. V1 is scoped to local Codex and protocol checks; hosted CI remains a delivery follow-up.
- Claude Desktop and Copilot are not installed for acceptance on this machine. Their setup guides remain for a later phase and are outside the revised V1 gate.
- Sandbox date boundaries, pagination behavior, and bank-side account restriction still need verification beyond the successful short-range smoke test. Local account restriction was verified in Codex.
- The local OpenAPI file declares the three read scopes, but live key permission labels and enforcement, transaction date limits, and pagination request behavior remain unconfirmed. Additional or contradictory transaction page metadata returns an incomplete-response error. Full history is not claimed.
- A restricted production key and live read-only smoke comparison have not been verified. Record only pass/fail and sanitized divergences when this is done.

The local V1 gate passed on 2026-11-24. [Release notes](release-notes-v0.1.0.md) record the included behavior and limitations. Hosted CI, other clients, and production access have separate post-V1 acceptance gates.
