# Deploy Zip Archive & SHA512 Checksum Generation Specification

## Overview
This specification defines the `--zip` deployment packaging feature for AI Agent Hub. When enabled during deployment (`npm run deploy -- --zip` or `node scripts/deploy.mjs --zip`), the deployment pipeline compresses the published application artifacts into a single `.zip` file inside a designated `archive` folder located at the same level as publish directory (`<publishParentDir>/archive`, by default `src/AIAgentHub.Web/bin/Release/archive/`) and generates a standalone `SHA512.txt` checksum file for integrity verification.

---

## Functional Requirements

### 1. Command-Line Options
The `scripts/deploy.mjs` deployment script accepts the following options:
- `-z`, `--zip`: Enables packaging the published files into a zip archive and computing its SHA512 hash.
- `--archive-dir <path>`: (Optional) Destination folder for the archive (default: `<publishParentDir>/archive`, e.g. `src/AIAgentHub.Web/bin/Release/archive/`).
- `--zip-name <filename>`: (Optional) Name of the generated zip file (default: `AIAgentHub.zip`).

### 2. Archiving Process
- **Timing**: Archiving takes place immediately after `dotnet publish` successfully completes.
- **Source**: The contents of `targetPublishDir` (e.g. `src/AIAgentHub.Web/bin/Release/publish/`).
- **Archive Format**: Standard `.zip` containing all published files and subdirectories at the root level of the archive (no wrapper directory).
- **Tooling**: Pure JavaScript/Node.js stream compression using the `archiver` package to ensure 100% cross-platform consistency across Windows, macOS, and Linux runners.

### 3. SHA512 Checksum Generation
- **Digest Algorithm**: SHA-512 (`node:crypto`).
- **Destination File**: `<archive-dir>/SHA512.txt`.
- **Format**: Standard `sha512sum` output format:
  ```text
  <sha512_hex_lowercase>  <zip_filename>
  ```
  Example:
  ```text
  e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855...  AIAgentHub.zip
  ```
- **Separation**: The `SHA512.txt` file resides in `<archive-dir>/` next to the zip file and is **not** included inside the zip archive itself.

### 4. Console Summary
When `--zip` is executed, the script logs:
```text
📦 Creating deployment archive...
✅ Archive created: src/AIAgentHub.Web/bin/Release/archive/AIAgentHub.zip (XX.X MB)
🔒 Checksum saved: src/AIAgentHub.Web/bin/Release/archive/SHA512.txt
🔑 SHA512: <sha512_hex>
```

---

## Documentation & Skill Updates
1. **`README.md`**: Add `--zip` option to the Deploy & Publish documentation section.
2. **`.agents/skills/deploy/SKILL.md`**: Update supported options to include `-z, --zip` and default archive location.
3. **`package.json`**: Add `archiver` and `@types/archiver` to root `devDependencies`.

---

## Verification Plan
1. Run `npm install` to install `archiver`.
2. Execute `npm run deploy -- --zip` and verify:
   - `src/AIAgentHub.Web/bin/Release/archive/AIAgentHub.zip` exists and contains published files.
   - `src/AIAgentHub.Web/bin/Release/archive/SHA512.txt` exists with valid checksum format.
   - Verify checksum matches `Get-FileHash -Algorithm SHA512 src/AIAgentHub.Web/bin/Release/archive/AIAgentHub.zip` (Windows) or `sha512sum src/AIAgentHub.Web/bin/Release/archive/AIAgentHub.zip` (Linux/macOS).
