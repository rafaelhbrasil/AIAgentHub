# Deploy Zip Archive & Checksum Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `--zip` (`-z`) flag to `npm run deploy` (`scripts/deploy.mjs`) to compress published build files into `./archive/AIAgentHub.zip` and generate a companion `./archive/SHA512.txt` checksum file.

**Architecture:** Install `archiver` as a devDependency in root `package.json`, extend CLI parsing and execution in `scripts/deploy.mjs` to stream `targetPublishDir` into a zip archive, calculate the SHA-512 digest with Node's built-in `node:crypto`, write the checksum to `SHA512.txt`, and update documentation.

**Tech Stack:** Node.js, `archiver`, `node:crypto`, `node:fs`, `scripts/deploy.mjs`.

## Global Constraints
- Pure Node.js implementation with `archiver` devDependency.
- Checksum format: `<sha512_hex>  <zip_filename>\n`.
- `SHA512.txt` must be saved in the archive folder, outside the zip file.
- Backward compatibility: running `npm run deploy` without `--zip` behaves exactly as before.

---

### Task 1: Add `archiver` dependency to `package.json`

**Files:**
- Modify: `package.json`

- [ ] **Step 1: Install `archiver` in root devDependencies**

Run: `npm install -D archiver @types/archiver`

---

### Task 2: Implement `--zip` packaging and SHA512 calculation in `scripts/deploy.mjs`

**Files:**
- Modify: `scripts/deploy.mjs`

- [ ] **Step 1: Add `--zip`, `--archive-dir`, `--zip-name` arguments and archiver logic**
- [ ] **Step 2: Add async `createZipArchive(sourceDir, zipPath)` helper**
- [ ] **Step 3: Add `generateSha512(zipPath, checksumPath)` helper**

---

### Task 3: Update documentation and skills

**Files:**
- Modify: `README.md`
- Modify: `.agents/skills/deploy/SKILL.md`

- [ ] **Step 1: Update `README.md`**
- [ ] **Step 2: Update `.agents/skills/deploy/SKILL.md`**

---

### Task 4: End-to-End Verification

- [ ] **Step 1: Run `npm run deploy -- --zip`**
- [ ] **Step 2: Verify `archive/AIAgentHub.zip` and `archive/SHA512.txt`**
- [ ] **Step 3: Validate checksum matching `Get-FileHash -Algorithm SHA512 archive/AIAgentHub.zip`**
