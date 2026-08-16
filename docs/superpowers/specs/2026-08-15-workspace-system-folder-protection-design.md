# Workspace System Folder Protection Specification

## Overview
To prevent accidental corruption, security exposure, and unbounded filesystem scanning of critical operating system directories, AI Agent Hub enforces path constraints when creating workspaces and navigating filesystem directories:

1. **Workspace Root Selection**: Bare drive roots (`C:\`, `D:\`, `/`) and critical system folders (`Windows`, `Program Files`, `Recovery`, `$Recycle.Bin`, `/bin`, `/boot`, `/dev`, `/etc`, `/lib`, `/proc`, `/sys`, `/usr`, `/root`, `/System`, `/Library`, `/private`) CANNOT be selected as workspace roots.
2. **Directory Browsing Navigation**: Root drives (`C:\`, `D:\`, `/`) ARE navigable to allow users to navigate into user-created subdirectories (such as `D:\Code` or `C:\Users`). However, navigating into protected system folders (e.g. `C:\Windows`) is blocked.

The protection operates in two layers:
1. **Frontend Pre-Validation (Instant Feedback)**: In-memory pattern check using the forbidden folder list fetched from the backend on init. Blocks navigation into forbidden folders and blocks selection of root drives / forbidden folders with toast error notifications (`useToast`).
2. **Backend Hard Enforcement (Server Security)**: Server-side validation via `ISystemPathValidator` during workspace creation (`WorkspaceService.CreateAsync`) and filesystem browsing (`FilesystemService.BrowseDirectoryAsync`), rejecting forbidden paths with a `400 Bad Request`.

---

## 1. System Path Validator (`ISystemPathValidator`)

Located in `AIAgentHub.Application.Filesystem`:

```csharp
namespace AIAgentHub.Application.Filesystem;

public interface ISystemPathValidator
{
    /// <summary>
    /// Getter-only property returning the active list of forbidden directory paths and wildcard patterns.
    /// </summary>
    IReadOnlyList<string> ForbiddenFolders { get; }

    /// <summary>
    /// Validates whether a given path is forbidden from being chosen as a workspace root.
    /// Returns true for root drives (e.g. C:\, /) and protected system folders.
    /// </summary>
    bool IsForbiddenForWorkspace(string? path, out string? reason);

    /// <summary>
    /// Validates whether a given path is forbidden from being navigated to during browsing.
    /// Returns true for protected system folders (e.g. C:\Windows, /bin), but allows root drives (C:\, /).
    /// </summary>
    bool IsForbiddenForBrowsing(string? path, out string? reason);
}
```

### OS-Specific System Path Detection:
- **Windows**:
  - Root of system drive: `Path.GetPathRoot(Environment.SystemDirectory)` (e.g. `C:\`). Bare drive roots (e.g. `C:\`, `D:\`) are protected from being selected directly as workspace root.
  - Windows directories: `Environment.GetFolderPath(Environment.SpecialFolder.Windows)` (`C:\Windows`), `Environment.SpecialFolder.ProgramFiles` (`C:\Program Files`), `Environment.SpecialFolder.ProgramFilesX86` (`C:\Program Files (x86)`), `Environment.SpecialFolder.CommonProgramFiles`, `Environment.SpecialFolder.CommonProgramFilesX86`, `Environment.SpecialFolder.System` (`C:\Windows\System32`), `Environment.SpecialFolder.SystemX86`.
  - Windows System & Recovery Wildcard Patterns: `*:\Windows*`, `*:\Program Files*`, `*:\Program Files (x86)*`, `*:\ProgramData*`, `*:\$Recycle.Bin*`, `*:\System Volume Information*`, `*:\Recovery*`, `*:\Boot*`, `*:\Windows.old*`.
- **Linux**:
  - Root: `/`.
  - System Folders: `/bin`, `/sbin`, `/boot`, `/dev`, `/etc`, `/lib`, `/lib32`, `/lib64`, `/proc`, `/root`, `/run`, `/sys`, `/usr`, `/var`, `/opt`, `/snap`.
- **macOS**:
  - Root: `/`.
  - System Folders: `/System`, `/Library`, `/bin`, `/sbin`, `/usr`, `/private`, `/dev`, `/etc`, `/var`.

### Path Normalization and Matching Algorithm:
- Normalizes paths using `Path.GetFullPath(...)` (resolving relative segments like `..` and `./`).
- Strips trailing directory separators (`/`, `\`).
- Compares case-insensitively on Windows/macOS and case-sensitively on Linux.
- A path is forbidden if:
  1. It is a bare root path (e.g., `C:\`, `/`).
  2. It exactly matches any forbidden folder or wildcard pattern.
  3. It is a subfolder/child of any forbidden system folder (e.g., `C:\Windows\System32\drivers` or `/etc/nginx`).

---

## 2. API Contract

### Filesystem Endpoints

#### `GET /api/v1/filesystem/forbidden-paths`
- **Response**: `200 OK`
```json
{
  "forbiddenPaths": [
    "C:\\Windows",
    "C:\\Program Files",
    "C:\\Program Files (x86)",
    "C:\\ProgramData",
    "*:\\$Recycle.Bin",
    "*:\\System Volume Information",
    "*:\\Recovery",
    "/bin",
    "/boot",
    "/dev",
    "/etc",
    "/lib",
    "/proc",
    "/root",
    "/run",
    "/sys",
    "/usr",
    "/var",
    "/System",
    "/Library",
    "/private"
  ]
}
```

#### `GET /api/v1/filesystem/browse?path={path}`
- If `path` matches `IsPathForbidden`:
  - Returns `400 Bad Request`:
    ```json
    {
      "code": "forbidden_system_directory",
      "message": "Access to system directory 'C:\\Windows' is restricted."
    }
    ```

#### `POST /api/v1/workspaces`
- If `request.Path` matches `IsPathForbidden`:
  - Returns `400 Bad Request`:
    ```json
    {
      "code": "workspace_creation_failed",
      "message": "Directory 'C:\\Windows' is a critical system folder and cannot be used as a workspace."
    }
    ```

---

## 3. Frontend Architecture

### State & Pre-Validation (`FolderExplorerModal.tsx`)
- Fetches `forbiddenPaths` on mount from `GET /api/v1/filesystem/forbidden-paths`.
- Helper function `isPathForbidden(path: string, forbiddenPaths: string[]): boolean` checks exact matches, wildcard expansions, and child path containment.
- On user interactions:
  - **Tile Click**: If target subfolder is forbidden, prevent navigation and call `showToast("The directory '{name}' is a protected system folder.", "error")`.
  - **Breadcrumb Click**: If target breadcrumb is forbidden (or root), prevent navigation and toast.
  - **Direct Path Input (Enter Key)**: If input path is forbidden, prevent navigation and toast.
  - **"Open Workspace" Button**: If `currentPath` is forbidden, prevent submission and toast.

---

## 4. Verification Plan

### Automated Tests
1. **Unit Tests (`AIAgentHub.Application.Tests`)**:
   - `SystemPathValidatorTests`: Tests drive detection, exact system folder matching, wildcard folder patterns, child directories of system folders, bare root protection, and valid user project folders (`C:\Projects\MyApp`, `/home/user/code`).
   - `WorkspaceServiceTests`: Verifies `WorkspaceService.CreateAsync` throws `ArgumentException` when given a forbidden path.
2. **Frontend Vitest Tests / TypeScript Build**:
   - Verify `isPathForbidden` helper correctly evaluates paths and wildcards.
   - Run `npm test` and `npm run build` in `src/AIAgentHub.Web/frontend`.
