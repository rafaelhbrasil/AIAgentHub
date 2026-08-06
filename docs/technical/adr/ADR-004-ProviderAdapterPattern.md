# ADR-004

# Provider Adapter Pattern

**Status:** Accepted

---

# Context

Every AI provider exposes different:

- CLIs
- authentication flows
- capabilities
- models
- configuration
- execution methods

Directly depending on provider implementations would tightly couple the application to each vendor.

---

# Decision

Every AI provider shall be integrated through an Adapter implementing a common abstraction.

The application communicates exclusively with provider interfaces.

Provider-specific implementation details remain isolated inside adapters.

During early implementation, `IProvider` interface signatures are refined iteratively alongside adapter capabilities to avoid premature locking and documentation drift.

Example:

```
IProvider

├── CodexProvider
├── GeminiProvider
├── ClaudeProvider
└── OpenCodeProvider
```

---

# Consequences

## Positive

- Provider independence.
- Easier testing.
- Easier addition of new providers.
- Reduced coupling.
- Clear separation of responsibilities.

## Negative

- Slightly more abstraction.
- Some provider-specific features require adapter extensions.

---

# Alternatives Considered

## Provider-specific code throughout the application

Rejected.

Creates tight coupling and increases maintenance effort.

---

## Lowest-common-denominator abstraction

Rejected.

Would hide valuable provider-specific capabilities.

The adapter model should expose common functionality while allowing optional provider-specific extensions.

---

# References

- Product.md
- Architecture.md
- DevelopmentStandards.md