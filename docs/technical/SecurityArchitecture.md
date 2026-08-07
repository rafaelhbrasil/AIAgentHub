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

For the browser to trust it without warnings, the certificate (or its issuing CA) must be installed in the local machine's trusted store. On the Server's own machine this follows the behavior of `dotnet dev-certs`.

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

# References

Related documents:

* Product/Product.md
* Technical/Architecture.md
* Technical/DevelopmentStandards.md
* Technical/ApiDesign.md
* Technical/ADR-011-CryptographyAndSecrets.md
* Technical/ADR/
