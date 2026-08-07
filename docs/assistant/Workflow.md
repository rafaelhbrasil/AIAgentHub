# AI Agent Hub

# Workflow

**Version:** 0.1 Draft

---

# Purpose

This document defines the expected workflow for AI assistants contributing to AI Agent Hub.

Its purpose is to ensure that every implementation follows the same engineering process, regardless of which AI assistant is used.

This workflow complements, but never replaces, the project's technical documentation.

---

# General Principles

Every task should prioritize:

* Correctness
* Simplicity
* Maintainability
* Security
* Consistency

Favor small, incremental improvements over large refactorings.

---

# Standard Workflow

Every implementation should follow this sequence.

```
Understand

↓

Plan

↓

Implement

↓

Validate

↓

Document

↓

Complete
```

---

# Step 1 — Understand

Before writing code:

* Read the task carefully.
* Identify the affected modules.
* Read the relevant documentation.
* Review applicable ADRs.
* Verify existing architecture.

If requirements are ambiguous, ask for clarification before implementing.

Do not invent requirements.

---

# Step 2 — Plan

Before modifying code:

* Identify the affected projects.
* Identify impacted APIs.
* Identify affected tests.
* Determine whether documentation will require updates.

Prefer the smallest implementation that fully satisfies the requirements.

---

# Step 2.5 — Update Spec First

Before writing any code:

* Update the affected specification documents (Product, Release, ADRs).
* Spec changes must be committed separately or co-committed with code — but never after.
* Code that changes behavior without corresponding spec updates is incomplete.

This rule takes precedence. No exception for "small" or "obvious" changes.

---

# Step 3 — Implement

During implementation:

* Follow the architecture.
* Respect accepted ADRs.
* Use terminology from the Glossary.
* Keep methods and classes focused.
* Avoid unnecessary abstractions.
* Avoid introducing new dependencies unless justified.

Do not implement functionality outside the requested scope.

---

# Step 4 — Validate

Before considering the task complete:

* Build the solution.
* Resolve compiler warnings.
* Execute affected tests.
* Verify that behavior matches the requested requirements.

Never ignore failing tests.

---

# Step 5 — Documentation

Documentation is part of the implementation.

Update documentation whenever required.

Examples include:

* Product documentation
* Release documentation
* Changelog
* Architecture
* ADRs

Do not update documents unnecessarily.

Only modify documentation affected by the implemented change.

---

# Step 6 — Complete

Before finishing, verify the following checklist.

## Code

* Solution builds successfully.
* No new compiler warnings.
* No unrelated changes.
* Code follows project conventions.

## Tests

* Existing tests continue to pass.
* New functionality includes appropriate tests.

## Documentation

* Documentation updated where necessary.
* Release notes updated if applicable.
* ADR created if an architectural decision changed.

---

# Working with Existing Code

Prefer extending existing components over introducing new ones.

Avoid duplicate implementations.

Before creating:

* a new service
* a new abstraction
* a new helper
* a new utility

verify whether an appropriate implementation already exists.

---

# Naming

Always use the terminology defined in:

```
docs/Product/Glossary.md
```

Do not introduce alternative terminology.

---

# Architecture

Never violate accepted ADRs.

If a requested implementation appears to conflict with an accepted ADR:

* stop;
* explain the conflict;
* request clarification.

Do not silently ignore architectural decisions.

---

# Security

Always consider:

* authentication
* authorization
* input validation
* secret handling
* logging

Never expose sensitive information.

---

# Dependencies

Before introducing a new dependency, evaluate:

* necessity
* maintenance burden
* licensing
* security
* ecosystem maturity

Prefer existing project dependencies whenever practical.

---

# Refactoring

Refactoring is encouraged when it:

* improves readability;
* reduces duplication;
* simplifies maintenance;

provided it does not introduce unrelated behavioral changes.

Large refactorings should be proposed separately from feature work whenever practical.

---

# AI-Specific Guidance

AI assistants should:

* avoid assumptions;
* preserve existing architecture;
* keep responses technically accurate;
* avoid speculative implementations;
* explain trade-offs when relevant.

When uncertain, ask for clarification instead of guessing.

---

# Definition of Done

A task is considered complete when:

* The requested functionality has been implemented.
* The solution builds successfully.
* Tests pass.
* Documentation has been updated where required.
* No accepted ADR has been violated.
* No unnecessary changes have been introduced.

Completing the requested functionality is not sufficient if the implementation leaves the project in a less maintainable or less consistent state.

---

# Continuous Improvement

If recurring issues or better practices are identified during development, update this document so future contributors can benefit from them.
