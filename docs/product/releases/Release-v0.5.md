# Version 0.5 — Ecosystem, Analytics & Server Operations

## Objectives

Version 0.5 expands tool ecosystem integration, local usage analytics, and server operations:

- Cross-provider skill sharing via filesystem symlinks and directory junctions
- Advanced MCP Server lifecycle management and startup options
- 100% local usage analytics and cost estimation (zero external telemetry)
- Operator-supplied HTTPS certificates (PFX, Windows cert store, Let's Encrypt / ACME)
- Server backup, restore, and portability export

---

# Skills & Tool Ecosystem

## Cross-Provider Skill Sharing

- Automatically share custom skills across compatible providers.
- Maintain a single source of truth in `.agents/skills` or global custom skill directories.
- Automatically create native filesystem symlinks / directory junctions to provider-specific CLI folders (e.g. `~/.gemini/antigravity/skills`, `~/.opencode/skills`, `~/.claude/skills`).

## Advanced MCP Management

- Configure custom environment variables and startup flags per MCP server.
- Restart individual MCP processes on demand.
- Real-time MCP health status and provider compatibility matrix.

---

# Local Usage Analytics

- **100% Local**: No telemetry sent outside the local server host.
- **Metrics Tracked**:
  - Total prompts, conversations, and active sessions
  - Usage distribution across providers and models
  - Workspace activity history
  - Estimated token consumption and cost modeling based on published provider pricing

---

# Server Operations & Security

## Operator-Supplied HTTPS Certificates

- Custom PFX certificate file path with optional password.
- Windows OS Certificate Store thumbprint binding.
- Internal ACME CA integration (e.g., `step-ca`, Caddy `tls internal`).
- Let's Encrypt automated ACME renewal for public domain deployments.

## Backup & Restore

- Export complete server configurations, workspace metadata, conversation history, and provider settings into an encrypted archive.
- Restore backups on another machine for seamless migration.

## Update Checker

- Periodic notifications when newer versions of AI Agent Hub or CLI providers are detected.

