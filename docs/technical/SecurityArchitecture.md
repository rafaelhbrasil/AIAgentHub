# AI Agent Hub

# Security Architecture

**Version:** 0.1 Draft

---

# Purpose

This document defines the security architecture of AI Agent Hub.

Its purpose is to establish the security principles, authentication model, authorization model, cryptography strategy and secret management used throughout the application.

Security requirements described here apply to every component unless explicitly documented otherwise.

---

# Security Principles

AI Agent Hub follows the following principles:

* Secure by default
* Least privilege
* Defense in depth
* Explicit authorization
* Privacy first
* Transparency
* Security by design

Security must never depend solely on users making the correct decision.

---

# Security Goals

The security model aims to:

* Protect administrator credentials
* Protect AI provider credentials
* Protect Workspace data
* Protect conversations
* Prevent unauthorized access
* Prevent privilege escalation
* Protect secrets stored on disk
* Minimize the attack surface

---

# Trust Model

Version 0.1 assumes:

* The Server is trusted.
* Remote Stations become trusted only after successful authentication.
* AI Providers are external processes.
* Operating system security services are trusted for protecting the Master Encryption Key.

---

# First Run Experience

On the first application startup, if no administrator account exists, AI Agent Hub enters **Setup Mode**.

The setup wizard is responsible for:

* Creating the initial administrator account.
* Generating the Master Encryption Key.
* Protecting the Master Key using the operating system.
* Initializing the configuration database.
* Disabling Setup Mode after successful completion.

Subsequent executions always require administrator authentication.

Only one administrator account exists in Version 0.1.

---

# Authentication

Authentication is mandatory.

Anonymous access is never permitted.

Version 0.1 supports:

* One administrator account
* Username/password authentication
* Cookie-based sessions
* **Account Lockout Policy**: 3 consecutive failed password attempts temporarily lock the account for 10 minutes. During this period, further login attempts are blocked. After the 10-minute duration has elapsed, login attempts are permitted again. Successful authentication immediately resets the failed attempts counter.
* **HTTP Rate Limiting**: Authentication and recovery endpoints (`/api/v1/auth/login`, `/api/v1/auth/recover`, `/api/v1/auth/setup/initialize`) are protected by an IP-based fixed window rate limiter (maximum 10 requests per minute) to prevent Denial of Service via Argon2id computation exhaustion.
* **Recovery Code Hashing & Verification**: 16-character alphanumeric recovery codes are stored hashed via SHA-256 and verified using constant-time equality comparisons (`CryptographicOperations.FixedTimeEquals`).

Future versions may introduce:

* Multiple users
* OAuth
* OpenID Connect
* LDAP
* Active Directory
* Multi-factor authentication

---

# Password Storage

Hashing/encryption choices are governed by **ADR-011 (Cryptography & Secret Storage)**.

Passwords must never be stored in plain text.

Passwords must never be encrypted.

Passwords must always be stored using a dedicated password hashing algorithm.

AI Agent Hub adopts:

* Argon2id
* Unique cryptographically secure random salt per account

Future versions may adjust Argon2 parameters as hardware evolves.

Acceptable alternatives include:

* PBKDF2
* bcrypt

Unsuitable algorithms include:

* MD5
* SHA-1
* SHA-256 used directly

---

# Session Management

Version 0.1 uses secure cookie-based authentication.

Session cookies must be configured as:

* Secure
* HttpOnly
* SameSite=Strict

Sessions should:

* Expire automatically after inactivity.
* Be revocable.
* Be regenerated after successful authentication.

JWT bearer authentication is intentionally not used because AI Agent Hub is primarily a server-hosted web application rather than a public API platform.

Future versions may introduce additional authentication mechanisms if required.

---

# Authorization

Version 0.1 contains a single administrator.

All authenticated operations execute with administrator privileges.

Authorization is enforced by default across the entire application:
* ASP.NET Core `FallbackPolicy` requires an authenticated user for all endpoints and SignalR Hub connections.
* Only explicitly designated initialization and authentication endpoints are annotated with `[AllowAnonymous]`.
* Direct controller actions and SignalR groups are protected from unauthenticated access.
* **Unauthenticated Request Handling**:
  * **Browser Navigation (Page Requests)**: Direct browser GET requests for UI routes (accepting `text/html` outside of `/api/` and `/hubs/`) redirect (`HTTP 302`) to the root page (`/`) where the Sign In view or Setup Wizard is rendered.
  * **Non-Page Requests (API & Real-Time)**: All API endpoints (`/api/*`), SignalR hub connections (`/hubs/*`), and non-HTML requests are rejected immediately with `HTTP 401 Unauthorized` without redirecting, allowing API clients and SignalR connection handlers to detect authentication state.

Future versions may introduce:

* Multiple users
* Roles
* Permissions
* Workspace-level authorization

Authorization decisions must always be enforced on the Server.

---

# Secret Management

Secrets include:

* AI Provider API Keys
* OAuth Refresh Tokens
* Personal Access Tokens
* Provider Credentials

Secrets must remain recoverable by the application.

Therefore, they are encrypted rather than hashed.

AI Agent Hub adopts:

* AES-256-GCM

Secrets are encrypted before being written to persistent storage.

---

# Cryptography

AI Agent Hub separates password protection from secret protection.

Passwords use one-way hashing.

Secrets use authenticated encryption.

Current design:

```text
Administrator Password
        │
        ▼
     Argon2id
        │
        ▼
 Password Hash

--------------------------------

Provider Secrets
        │
        ▼
   AES-256-GCM
        │
        ▼
Encrypted SQLite Storage
```

---

# Master Encryption Key

Version 0.1 uses a single Master Encryption Key.

The Master Key is responsible for encrypting every application secret.

The Master Key itself is **never stored unprotected**.

Copying the SQLite database alone must not allow recovery of encrypted secrets.

The at-rest secret scheme (including this Master Key usage) is recorded in **ADR-011 (Cryptography & Secret Storage)**.

---

# Master Key Protection

The Master Encryption Key is protected using operating system facilities.

## Windows (Version 0.1)

Windows DPAPI (Data Protection API) is used to protect the Master Encryption Key.

The encrypted Master Key may be stored alongside the application configuration, but only in its DPAPI-protected form.

## Linux (Future)

Future versions should integrate with:

* Secret Service API
* libsecret

## macOS (Future)

Future versions should integrate with:

* Apple Keychain

These implementations are planned to provide platform-native protection equivalent to Windows DPAPI.

---

# HTTPS

HTTPS is mandatory.

Authenticated endpoints must never be exposed over HTTP.

## Certificate Provisioning

Version 0.1 supports a single certificate mode.

### Automatic Self-Signed Certificate (Version 0.1 only)

The application generates a self-signed certificate on first launch.

Suitable for localhost and quick-start usage.

For the browser to trust it without warnings, the certificate (or its issuing CA) must be installed in the local machine's trusted store. On the Server's own machine this follows the behavior of `dotnet dev-certs` (see [CertificateTrustGuide.md](CertificateTrustGuide.md)).

## Operator-supplied Certificates & Deployment Options (Version 0.2+)

Version 0.2 expands certificate handling to include:

- operator-supplied certificates (PFX file path or operating-system store thumbprint)
- certificates issued by an internal or trusted CA for LAN deployments
- TLS termination at a reverse proxy
- internal ACME CA (e.g. step-ca) or reverse proxy with an internal CA (e.g. Caddy `tls internal`)
- Let's Encrypt for deployments with a public domain

Public Certificate Authorities cannot issue certificates for bare LAN IP addresses or `localhost`, so warning-free LAN access with operator-supplied certificates requires trust distribution on client machines (e.g. mkcert / local CA, enterprise / Active Directory CA, Tailscale certificates).

Detailed deployment guidance remains a deployment/infrastructure concern, not an application responsibility.

See Release-v0.2.md §Security Improvements.

## Hard Requirement (applies to every option)

**The leaf certificate Subject Alternative Names (SANs) must include *all* addresses the Server is reachable at** — at minimum `localhost`, the machine hostname, and every configured LAN/listening IP.

This applies to the Version 0.1 self-signed certificate as well: it must cover every address it is used with.

Without matching SANs, browsers report hostname/IP certificate mismatches even when the CA is trusted, degrading both correctness and security perception. The Server should therefore bind only to addresses that are covered by the certificate SANs.

## Future

Future versions may provide:

* Let's Encrypt integration
* Automatic certificate renewal
* Certificate management wizard

---

# Network Security

The Server may expose itself through:

* localhost
* LAN
* Selected network interfaces

Network mode restrictions are enforced on the Server by `NetworkModeMiddleware`:
- In **Localhost** mode, connections with remote IP addresses other than loopback (127.0.0.1, ::1) are immediately rejected with HTTP 403 Forbidden.
- In **LAN** mode, connections from all local network interfaces are permitted.
- In **Selected Interfaces** mode, only connections from loopback or IP addresses within the subnets of explicitly selected network interfaces are permitted.

The application must clearly display every exposed address.

The application must never silently expose additional interfaces.

---

# Remote Stations

Remote Stations communicate exclusively through HTTPS.

Business logic always remains on the Server.

Remote Stations never receive:

* Provider credentials
* Master Encryption Key
* Administrator password

---

# Workspace Permissions

Version 0.1 grants the administrator full access to every Workspace.

Future versions may define permissions such as:

* Read
* Write
* Execute AI
* Manage Providers
* Manage Conversations
* Manage Workspaces

Permissions must always be evaluated on the Server.

---

# Provider Permissions

Future versions may restrict:

* Available Providers
* Models
* MCP Servers
* Skills
* Workspace access

Restrictions should be configurable per user and Workspace.

---

# AI Providers

Providers are treated as external processes.

The application should:

* Validate provider availability.
* Monitor execution.
* Isolate failures.
* Detect abnormal termination.
* Prevent provider failures from compromising the Server.

---

# File System Access

AI Providers receive access only to explicitly authorized Workspaces.

The application should:

* Avoid exposing arbitrary filesystem locations.
* Validate configured paths.
* Prevent path traversal.
* Prevent unauthorized access outside authorized Workspace roots.

---

# Permission Requests

Potentially destructive operations require explicit user approval.

Examples include:

* Deleting files
* Overwriting files
* Executing commands
* Accessing additional directories

Permission dialogs should clearly explain:

* What will happen.
* Why it is required.
* Which Provider requested the permission.

---

# Logging

Security-related events should be logged.

Examples include:

* Authentication
* Failed authentication
* Permission approval
* Permission denial
* Provider failures
* Session creation
* Session termination

Sensitive information must never appear in logs.

Passwords, API Keys and decrypted secrets must never be logged.

---

# Privacy

AI Agent Hub is privacy-first.

Version 0.1 performs:

* No telemetry
* No analytics upload
* No cloud synchronization

All data remains under user control.

---

# Secure Defaults

Whenever multiple behaviors are possible, the most secure reasonable default should be chosen.

Examples include:

* HTTPS enabled
* Localhost by default
* Authentication required
* Permissions denied until explicitly granted
* Workspace path restrictions against critical system folders

---

# Filesystem & Workspace Path Security

To protect host operating systems from unbounded filesystem traversals, accidental modification, and unauthorized system access, AI Agent Hub enforces strict path constraints on workspace creation and filesystem browsing:

## Forbidden Directories & Patterns
- **Root Drives**: Bare root directories (`C:\`, `/`) cannot be targeted as workspace roots.
- **Windows Critical Folders**: `Windows`, `Program Files`, `Program Files (x86)`, `ProgramData`, `Recovery`, `$Recycle.Bin`, `System Volume Information`, `Boot`, `Windows.old` across all system drives.
- **Unix & macOS Critical Folders**: `/bin`, `/sbin`, `/boot`, `/dev`, `/etc`, `/lib`, `/lib32`, `/lib64`, `/proc`, `/root`, `/run`, `/sys`, `/usr`, `/var`, `/opt`, `/snap`, `/System`, `/Library`, `/private`.

## Dual-Layer Enforcement
- **Frontend Validation**: Instant in-memory check against patterns fetched from `GET /api/v1/filesystem/forbidden-paths`, blocking navigation/selection and triggering user toast warnings.
- **Backend Validation**: `ISystemPathValidator` verifies all workspace creation and browsing operations, returning `400 Bad Request` upon violation.
- **Workspace Boundary Containment**: All file read operations (e.g. preview rendering), file writes (diff acceptance), and snapshot rollbacks verify that canonical target paths strictly reside within the designated workspace root directory (`Path.GetFullPath`), preventing arbitrary path traversal.

## Frontend Content Sanitization (XSS Prevention)
- All Markdown and ANSI terminal outputs rendered in the web UI are sanitized using `DOMPurify` before insertion into the DOM via `dangerouslySetInnerHTML`, neutralizing script injection, event handlers, and malicious iframe/object embeddings.

---

# Future Security Features

Potential future enhancements include:

* Multi-factor authentication
* API Keys
* IP allow lists
* IP deny lists
* Audit logs
* External identity providers
* Hardware-backed encryption
* Master Key rotation
* Session management UI
* Encrypted backups
* Certificate management wizard
* Automatic certificate renewal

---

# Security Reviews

Changes affecting:

* Authentication
* Authorization
* Cryptography
* Networking
* Secret storage

should undergo additional review before merging.

Significant architectural security decisions should be documented through ADRs.

---

# System Recovery & Emergency Reset

Emergency reset and account recovery follow strict security boundaries:

## Sign-In Page & Setup Button Visibility
- **First Run (No Users)**: The Sign In page displays the `#resetSetupBtn` ("Run Setup Wizard") allowing initial creation of the administrator account. The `#recoverLink` is hidden.
- **Normal Operation (User Exists)**: Once an administrator account exists, `#resetSetupBtn` is permanently hidden from unauthenticated users. The `#recoverLink` is displayed instead.
- **Enforced Authentication**: The Sign In screen is presented as a non-dismissible full page without a close button. Unauthenticated requests cannot bypass authentication.

## Recovery Code Authentication
- Standard password reset requires the 16-character recovery code generated during initial setup.
- Recovery modal explicitly includes help instructions regarding server startup options (`--recovery`).

## Emergency Unassisted Recovery (`--recovery` CLI Flag) & Safe Client IP (`--safe-client <IP>`)
- An unassisted reset option (resetting without a recovery code) is ONLY available when:
  1. The server process is launched with the command-line flag `--recovery`.
  2. The HTTP request originates from either:
     - The local host (loopback interface `127.0.0.1` / `::1`).
     - A trusted Safe Client IP explicitly specified at startup via the `--safe-client <IP>` parameter (or aliases `--safeclient`, `-safe-client`, `/safe-client`, `--safe-ip`).
- Connection attempts from unauthorized remote IPs attempting unassisted recovery are strictly forbidden (`403 Forbidden`).
- When executed, unassisted recovery requires **double confirmation** from the user, explicitly warning that all database records (workspaces, user accounts, conversations, secrets, and settings) will be forcefully erased.

## Safe Client IP Parameter (`--safe-client <IP>`)
- The server CLI supports `--safe-client <IP>`:
  - **Localhost-Only Bypass**: When network mode is set to Localhost-only (`NetworkMode.Localhost`), incoming connections originating from the configured Safe Client IP are permitted through `NetworkModeMiddleware`, exactly like loopback connections.
  - **First-Access Setup Wizard**: If initial setup is pending, the Safe Client IP can run the setup wizard to create the initial administrator account.
  - **Emergency Recovery Access**: When combined with `--recovery`, the Safe Client IP possesses the same administrative recovery rights as `localhost` to reset the administrator or wipe the database.
  - **Without Parameter**: If started without `--safe-client`, only `localhost` (loopback) retains local-only permissions and recovery access.
  - **LAN Mode Interaction**: If LAN connections are enabled (`NetworkMode.Lan`), all LAN devices can connect normally, while `localhost` and the designated `--safe-client` IP retain recovery privileges.

---

# References

Related documents:

* Product/Product.md
* Technical/Architecture.md
* Technical/DevelopmentStandards.md
* Technical/ApiDesign.md
* Technical/ADR-011-CryptographyAndSecrets.md
* Technical/ADR/
