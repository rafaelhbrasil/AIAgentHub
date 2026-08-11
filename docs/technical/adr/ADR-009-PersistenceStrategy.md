# ADR-009

# Persistence Strategy

**Status:** Accepted

**Date:** 2026-08-06

---

# Context

AI Agent Hub requires persistent storage for application data, including:

- Workspaces metadata
- Conversations
- Messages
- Provider configuration
- User settings
- Authentication data
- MCP configuration
- Skills configuration

The application is designed primarily for local and self-hosted deployments.

Installation should require minimal user intervention while remaining reliable, performant and portable across supported operating systems.

The persistence mechanism should also support future migration to more powerful database systems without affecting business logic.

---

# Decision

AI Agent Hub adopts the following persistence strategy.

## Object-Relational Mapping & Database Migrations

Entity Framework Core is the persistence framework used throughout the application.

The Domain and Application layers remain independent from Entity Framework Core.

Persistence concerns belong exclusively to the Infrastructure layer.

Schema evolution and versioning use EF Core Code-First Migrations (`Microsoft.EntityFrameworkCore.Migrations`). 

On application startup, database initialization invokes `Database.MigrateAsync()` to automatically apply all pending migrations against the SQLite database file without manual table inspection or custom ALTER statements.

---

## Default Database

SQLite is the default database engine.

SQLite was selected because it:

- requires no installation
- stores all structured data in a single database file
- is cross-platform
- provides ACID transactions
- integrates seamlessly with Entity Framework Core
- offers sufficient performance for the expected workload
- greatly simplifies backup and restore

---

## File Storage

Not every piece of data belongs in the database.

The file system remains responsible for storing:

- Workspace source code
- attachments
- images
- exported files
- logs
- temporary files
- cache

The database stores metadata only.

---

## Data Ownership

The database owns application metadata.

Examples include:

- Workspace information
- conversation metadata
- conversation messages
- provider settings
- authentication settings
- application configuration

The Workspace continues to own the project files themselves.

---

## Repository Pattern

All persistence operations shall be accessed through repository abstractions.

Neither the Domain layer nor the Application layer may directly depend on:

- SQLite
- SQL
- Entity Framework Core

These implementation details remain isolated within Infrastructure.

---

## Database Location

By default, the SQLite database should be stored inside the application's data directory.

Example:

```
Data/

    AIAgentHub.db
```

The exact location depends on the operating system and may evolve over time.

---

# Consequences

## Positive

- Zero-install database.
- Cross-platform compatibility.
- Simple deployment.
- Easy backups.
- Excellent EF Core support.
- Mature and reliable technology.
- Clear separation between metadata and project files.

## Negative

- Database file corruption affects all stored metadata.
- SQLite is not intended for high-concurrency server workloads.
- Very large deployments may eventually require another database provider.

These limitations are acceptable for the intended deployment model.

---

# Alternatives Considered

## JSON Files

Rejected.

Although simple, JSON files would require implementing custom indexing, searching, filtering and relationship management.

As the number of conversations grows, performance and maintainability would degrade significantly.

---

## LiteDB

Rejected.

LiteDB provides a pleasant developer experience but offers a smaller ecosystem, fewer mature tools and less flexibility than SQLite.

Entity Framework Core integration is also considerably stronger with SQLite.

---

## SQL Server

Rejected.

Requires installation and administration that are unnecessary for the target audience.

May become an optional provider in future enterprise-oriented deployments.

---

## PostgreSQL

Rejected.

Provides capabilities beyond the needs of the initial product.

Its operational complexity is not justified for local and self-hosted installations.

---

## File System Only

Rejected.

Using only the file system would complicate querying, sorting, filtering, searching and maintaining relationships between entities.

A relational database is better suited for structured application metadata.

---

# Future Evolution

The persistence architecture intentionally allows replacing SQLite with another Entity Framework Core provider.

Potential future providers include:

- SQL Server
- PostgreSQL
- MySQL

Such a migration should not require changes to the Domain or Application layers.

---

# References

Related documents:

- Architecture.md
- DevelopmentStandards.md
- RepositoryStructure.md
- SecurityArchitecture.md