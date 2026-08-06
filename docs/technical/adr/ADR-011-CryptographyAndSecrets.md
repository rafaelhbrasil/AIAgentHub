# ADR-011

# Cryptography & Secret Storage

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

AI Agent Hub manages sensitive data that must never be stored in plain text:

- account passwords
- provider credentials
- API keys / OAuth tokens ("Secrets")
- session material

SecurityArchitecture.md requires that passwords are hashed, secrets are encrypted, and that significant security decisions are recorded as ADRs. Until now the hashing and at-rest encryption algorithms were stated there but not fixed as an architecture decision.

---

# Decision

## Password Hashing

- **Recommended:** Argon2id.
- **Acceptable:** bcrypt, PBKDF2.
- **Rejected:** MD5, SHA-1, and plain SHA-256 (not suitable without a dedicated password-hashing construction).

A per-password random salt and a work factor appropriate for the deployment are required. Passwords are never encrypted; they are always hashed.

## Secret (Provider Credential) Encryption at Rest

Secrets (API keys, OAuth tokens, provider credentials) are **encrypted before being written to disk**.

- Where practical, use the operating system's secure store:
  - Windows DPAPI
  - macOS Keychain
  - Linux Secret Service
- Where an OS store is not available or applicable, use an application-level symmetric encryption key, itself stored/secured via the OS store or a protected configuration location.

Encryption keys are never derived from a user password alone.

## Transport

HTTPS is mandatory for all traffic; the Server never exposes authenticated endpoints over HTTP (see ADR-008).

---

# Consequences

## Positive

- Consistent, auditable crypto policy across password and credential handling.
- Robust defaults (Argon2id) with a documented acceptable fallback (bcrypt).
- Credentials protected both at rest and in transit.

## Negative

- OS secure-store integration adds platform-specific code (DPAPI / Keychain / Secret Service).
- Non-standard (non-OS-store) environments require key-survival handling (backup/rotasta).

---

# Alternatives Considered

## Plain SHA-256 password storage

Rejected — not key strengthening; demonstrated weak against offline attacks.

## Reusing a single hard-coded key for credential encryption

Rejected — provides obfuscation rather than security; fails least-privilege and key-rotation.

## No OS secure store (encrypt-only with embedded key)

Rejected — the OS-provided secure storage is preferred wherever available.

---

# Implementation Notes

- The concrete v0.1 at-rest mechanism (single Master Encryption Key wrapped by DPAPI on Windows; Keychain/Secret Service planned) is described in SecurityArchitecture.md §Master Encryption Key / §Master Key Protection.
- Hashing primitives and the OS-store integrations live in Infrastructure (never in Domain/Application), per Architecture.md and DevelopmentStandards.md.
- Sensitive values must never be written to logs (see SecurityArchitecture.md §Logging).
- Key-rotation and secret-role-dedication can be revisited without changing business logic (SecurityArchitecture.md §Future Security Features).

---

# References

- SecurityArchitecture.md
- ADR-008 (Security Model)
- ADR-010 (SignalR)
- DevelopmentStandards.md