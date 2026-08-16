# Workspace System Folder Protection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement dual-layer (backend and frontend) validation to prevent workspace creation in or navigation to critical system folders across Windows, Linux, and macOS.

**Architecture:** A dedicated `ISystemPathValidator` service evaluates path safety against dynamically detected system drives, OS special folders, root drives, and wildcard patterns. The backend exposes `GET /api/v1/filesystem/forbidden-paths` so the frontend can validate instantly in memory with toast notifications, while the backend strictly enforces validation on workspace creation and directory browsing.

**Tech Stack:** .NET 9 (C#), ASP.NET Core Web API, React 18, TypeScript, Vitest, xUnit, NSubstitute.

## Global Constraints
- Getter-only property `IReadOnlyList<string> ForbiddenFolders { get; }` on the validator.
- Dynamically detect system drive and critical paths across Windows, Linux, and macOS.
- Frontend must give instant feedback via `useToast` without server round-trip.
- Backend must strictly reject forbidden paths with `400 Bad Request` / `ArgumentException`.
- Follow Specification-First rules and lazy senior dev (Ponytail) principles.

---

### Task 1: Create `ISystemPathValidator` and `SystemPathValidator`

**Files:**
- Create: `src/AIAgentHub.Application/Filesystem/ISystemPathValidator.cs`
- Create: `src/AIAgentHub.Application/Filesystem/SystemPathValidator.cs`
- Modify: `src/AIAgentHub.Web/DependencyInjection.cs`
- Create: `tests/AIAgentHub.Application.Tests/SystemPathValidatorTests.cs`

**Interfaces:**
- Produces:
  - `ISystemPathValidator`
  - `IReadOnlyList<string> ForbiddenFolders { get; }`
  - `bool IsPathForbidden(string? path, out string? reason)`

- [ ] **Step 1: Write failing unit tests for `SystemPathValidator`**

Create `tests/AIAgentHub.Application.Tests/SystemPathValidatorTests.cs`:
```csharp
using AIAgentHub.Application.Filesystem;
using System.Runtime.InteropServices;

namespace AIAgentHub.Application.Tests;

public sealed class SystemPathValidatorTests
{
    [Fact]
    public void ForbiddenFolders_Getter_ShouldReturnPopulatedList()
    {
        var validator = new SystemPathValidator();
        Assert.NotNull(validator.ForbiddenFolders);
        Assert.NotEmpty(validator.ForbiddenFolders);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsPathForbidden_NullOrWhitespace_ShouldReturnTrue(string? path)
    {
        var validator = new SystemPathValidator();
        var forbidden = validator.IsPathForbidden(path, out var reason);
        Assert.True(forbidden);
        Assert.NotNull(reason);
    }

    [Fact]
    public void IsPathForbidden_SystemFolders_ShouldBeForbidden()
    {
        var validator = new SystemPathValidator();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(winDir))
            {
                Assert.True(validator.IsPathForbidden(winDir, out var reason1));
                Assert.Contains("system", reason1, StringComparison.OrdinalIgnoreCase);

                var sys32 = Path.Combine(winDir, "System32");
                Assert.True(validator.IsPathForbidden(sys32, out _));

                var sys32Relative = Path.Combine(winDir, "System32", "..", "System32");
                Assert.True(validator.IsPathForbidden(sys32Relative, out _));
            }

            var rootDrive = Path.GetPathRoot(Environment.SystemDirectory);
            if (!string.IsNullOrEmpty(rootDrive))
            {
                Assert.True(validator.IsPathForbidden(rootDrive, out _));
                Assert.True(validator.IsPathForbidden(rootDrive.TrimEnd('\\'), out _));
            }

            Assert.True(validator.IsPathForbidden(@"C:\$Recycle.Bin", out _));
            Assert.True(validator.IsPathForbidden(@"C:\Recovery", out _));
            Assert.True(validator.IsPathForbidden(@"C:\System Volume Information", out _));
        }
        else
        {
            Assert.True(validator.IsPathForbidden("/", out _));
            Assert.True(validator.IsPathForbidden("/bin", out _));
            Assert.True(validator.IsPathForbidden("/etc", out _));
            Assert.True(validator.IsPathForbidden("/etc/nginx", out _));
        }
    }

    [Fact]
    public void IsPathForbidden_ValidUserDirectory_ShouldBeAllowed()
    {
        var validator = new SystemPathValidator();
        var tempUserPath = Path.Combine(Path.GetTempPath(), "agent-hub-test-workspace-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(tempUserPath);
            Assert.False(validator.IsPathForbidden(tempUserPath, out var reason));
            Assert.Null(reason);
        }
        finally
        {
            if (Directory.Exists(tempUserPath))
            {
                Directory.Delete(tempUserPath, true);
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AIAgentHub.Application.Tests/AIAgentHub.Application.Tests.csproj --filter FullyQualifiedName~SystemPathValidatorTests`
Expected: FAIL (types not defined).

- [ ] **Step 3: Implement `ISystemPathValidator` and `SystemPathValidator`**

Create `src/AIAgentHub.Application/Filesystem/ISystemPathValidator.cs`:
```csharp
namespace AIAgentHub.Application.Filesystem;

public interface ISystemPathValidator
{
    IReadOnlyList<string> ForbiddenFolders { get; }
    bool IsPathForbidden(string? path, out string? reason);
}
```

Create `src/AIAgentHub.Application/Filesystem/SystemPathValidator.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace AIAgentHub.Application.Filesystem;

public sealed class SystemPathValidator : ISystemPathValidator
{
    private readonly Lazy<IReadOnlyList<string>> _forbiddenFoldersLazy;
    private readonly Lazy<IReadOnlyList<Regex>> _compiledPatternsLazy;

    public SystemPathValidator()
    {
        _forbiddenFoldersLazy = new Lazy<IReadOnlyList<string>>(BuildForbiddenFoldersList);
        _compiledPatternsLazy = new Lazy<IReadOnlyList<Regex>>(BuildCompiledPatterns);
    }

    public IReadOnlyList<string> ForbiddenFolders => _forbiddenFoldersLazy.Value;

    public bool IsPathForbidden(string? path, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Path cannot be empty.";
            return true;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            reason = $"Invalid path format: {ex.Message}";
            return true;
        }

        var normalized = NormalizePath(fullPath);

        // Check if root drive directly (e.g. "C:\" or "/")
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && NormalizePath(root).Equals(normalized, GetComparison()))
        {
            reason = $"The root drive '{fullPath}' cannot be used as a workspace folder.";
            return true;
        }

        // Check exact match or child of any forbidden folder/pattern
        foreach (var patternRegex in _compiledPatternsLazy.Value)
        {
            if (patternRegex.IsMatch(normalized))
            {
                reason = $"Directory '{fullPath}' is a protected system folder and cannot be used as a workspace.";
                return true;
            }
        }

        return false;
    }

    private static StringComparison GetComparison() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string NormalizePath(string path)
    {
        var clean = path.Replace('\\', '/').TrimEnd('/');
        return string.IsNullOrEmpty(clean) ? "/" : clean;
    }

    private static IReadOnlyList<string> BuildForbiddenFoldersList()
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.Windows);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.System);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.SystemX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.ProgramFiles);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.ProgramFilesX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonProgramFiles);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonProgramFilesX86);
            AddWindowsSpecialFolder(list, Environment.SpecialFolder.CommonApplicationData);

            // Generic wildcards across any drive letter
            list.Add(@"*:\Windows");
            list.Add(@"*:\Program Files");
            list.Add(@"*:\Program Files (x86)");
            list.Add(@"*:\ProgramData");
            list.Add(@"*:\$Recycle.Bin");
            list.Add(@"*:\System Volume Information");
            list.Add(@"*:\Recovery");
            list.Add(@"*:\Boot");
            list.Add(@"*:\Windows.old");
        }
        else
        {
            // Linux & macOS common system roots
            list.Add("/bin");
            list.Add("/sbin");
            list.Add("/boot");
            list.Add("/dev");
            list.Add("/etc");
            list.Add("/lib");
            list.Add("/lib32");
            list.Add("/lib64");
            list.Add("/proc");
            list.Add("/root");
            list.Add("/run");
            list.Add("/sys");
            list.Add("/usr");
            list.Add("/var");
            list.Add("/opt");
            list.Add("/snap");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                list.Add("/System");
                list.Add("/Library");
                list.Add("/private");
                list.Add("/Volumes");
            }
        }

        return list.ToList();
    }

    private static void AddWindowsSpecialFolder(HashSet<string> set, Environment.SpecialFolder folder)
    {
        try
        {
            var p = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(p))
            {
                set.Add(p);
            }
        }
        catch
        {
            // Ignore if unavailable
        }
    }

    private IReadOnlyList<Regex> BuildCompiledPatterns()
    {
        var regexes = new List<Regex>();
        var isWindowsOrMac = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        var options = RegexOptions.Compiled | (isWindowsOrMac ? RegexOptions.IgnoreCase : RegexOptions.None);

        foreach (var item in ForbiddenFolders)
        {
            var norm = NormalizePath(item);
            // Replace wildcard "*:" or "*" with regex equivalent
            var pattern = "^" + Regex.Escape(norm)
                .Replace(@"\*", "[^/]+")
                + "(/.*)?$";

            regexes.Add(new Regex(pattern, options));
        }

        return regexes;
    }
}
```

- [ ] **Step 4: Register `ISystemPathValidator` in `DependencyInjection.cs`**

Modify `src/AIAgentHub.Web/DependencyInjection.cs`:
```csharp
services.AddSingleton<ISystemPathValidator, SystemPathValidator>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AIAgentHub.Application.Tests/AIAgentHub.Application.Tests.csproj --filter FullyQualifiedName~SystemPathValidatorTests`
Expected: PASS.

---

### Task 2: Integrate `ISystemPathValidator` into `WorkspaceService` & `FilesystemController`

**Files:**
- Modify: `src/AIAgentHub.Application/Workspaces/WorkspaceService.cs`
- Modify: `src/AIAgentHub.Web/Controllers/FilesystemController.cs`
- Modify: `tests/AIAgentHub.Application.Tests/ApplicationTests.cs`

**Interfaces:**
- Consumes: `ISystemPathValidator`
- Produces: `GET /api/v1/filesystem/forbidden-paths`

- [ ] **Step 1: Write failing unit test in `ApplicationTests.cs`**

Add tests to `tests/AIAgentHub.Application.Tests/ApplicationTests.cs`:
```csharp
[Fact]
public async Task WorkspaceService_CreateAsync_ForbiddenPath_ShouldThrowArgumentException()
{
    var repo = Substitute.For<IWorkspaceRepository>();
    var fs = Substitute.For<IFilesystemService>();
    var validator = Substitute.For<ISystemPathValidator>();

    string? reasonOut = "Directory is a protected system folder.";
    validator.IsPathForbidden(Arg.Any<string>(), out Arg.Any<string?>())
        .Returns(x => {
            x[1] = reasonOut;
            return true;
        });

    var service = new WorkspaceService(repo, fs, validator);
    var request = new CreateWorkspaceRequest("C:\\Windows", "WindowsWS", "local");

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
    Assert.Contains("protected system folder", ex.Message);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AIAgentHub.Application.Tests/AIAgentHub.Application.Tests.csproj --filter FullyQualifiedName~WorkspaceService_CreateAsync_ForbiddenPath`
Expected: FAIL (Constructor signature does not match).

- [ ] **Step 3: Update `WorkspaceService.cs`**

Modify `src/AIAgentHub.Application/Workspaces/WorkspaceService.cs`:
```csharp
public sealed class WorkspaceService(
    IWorkspaceRepository workspaceRepository,
    IFilesystemService filesystemService,
    ISystemPathValidator systemPathValidator) : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IFilesystemService _filesystemService = filesystemService;
    private readonly ISystemPathValidator _systemPathValidator = systemPathValidator;

    // In CreateAsync:
    public async Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedPath = request.Path?.Trim() ?? string.Empty;
        if (_systemPathValidator.IsPathForbidden(trimmedPath, out var reason))
        {
            throw new ArgumentException(reason ?? $"Directory '{trimmedPath}' is not allowed as a workspace.");
        }

        var fullPath = Path.GetFullPath(trimmedPath);
        // ... remainder of CreateAsync
```

- [ ] **Step 4: Update `FilesystemController.cs`**

Add endpoint to `src/AIAgentHub.Web/Controllers/FilesystemController.cs`:
```csharp
[HttpGet("forbidden-paths")]
public IActionResult GetForbiddenPaths([FromServices] ISystemPathValidator validator)
{
    return Ok(new { forbiddenPaths = validator.ForbiddenFolders });
}
```
And in `Browse`:
```csharp
[HttpGet("browse")]
public async Task<IActionResult> Browse([FromQuery] string? path, [FromServices] ISystemPathValidator validator, CancellationToken cancellationToken)
{
    if (!string.IsNullOrWhiteSpace(path) && validator.IsPathForbidden(path, out var reason))
    {
        return BadRequest(new { code = "forbidden_system_directory", message = reason });
    }

    var result = await _filesystemService.BrowseDirectoryAsync(path, cancellationToken);
    return Ok(result);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AIAgentHub.Application.Tests/AIAgentHub.Application.Tests.csproj`
Expected: PASS.

---

### Task 3: Implement Frontend Path Validation & Toast Notifications

**Files:**
- Create: `src/AIAgentHub.Web/frontend/src/utils/pathValidation.ts`
- Create: `src/AIAgentHub.Web/frontend/src/utils/pathValidation.test.ts`
- Modify: `src/AIAgentHub.Web/frontend/src/types/workspace.ts`
- Modify: `src/AIAgentHub.Web/frontend/src/components/modals/FolderExplorerModal.tsx`

- [ ] **Step 1: Write failing frontend unit tests for `pathValidation.ts`**

Create `src/AIAgentHub.Web/frontend/src/utils/pathValidation.test.ts`:
```typescript
import { describe, it, expect } from 'vitest';
import { isPathForbidden } from './pathValidation';

describe('pathValidation', () => {
  const forbiddenList = [
    'C:\\Windows',
    'C:\\Program Files',
    '*:\\$Recycle.Bin',
    '*:\\Recovery',
    '/bin',
    '/etc',
    '/System',
  ];

  it('should detect exact forbidden folders', () => {
    expect(isPathForbidden('C:\\Windows', forbiddenList)).toBe(true);
    expect(isPathForbidden('c:/windows', forbiddenList)).toBe(true);
    expect(isPathForbidden('/bin', forbiddenList)).toBe(true);
  });

  it('should detect child folders of forbidden roots', () => {
    expect(isPathForbidden('C:\\Windows\\System32', forbiddenList)).toBe(true);
    expect(isPathForbidden('/etc/nginx/conf.d', forbiddenList)).toBe(true);
  });

  it('should detect wildcard matches on other drive letters', () => {
    expect(isPathForbidden('D:\\$Recycle.Bin', forbiddenList)).toBe(true);
    expect(isPathForbidden('D:\\$Recycle.Bin\\S-1-5-21', forbiddenList)).toBe(true);
  });

  it('should detect root drives', () => {
    expect(isPathForbidden('C:\\', forbiddenList)).toBe(true);
    expect(isPathForbidden('C:', forbiddenList)).toBe(true);
    expect(isPathForbidden('/', forbiddenList)).toBe(true);
  });

  it('should allow valid user project folders', () => {
    expect(isPathForbidden('C:\\Projects\\MyApp', forbiddenList)).toBe(false);
    expect(isPathForbidden('/home/user/code/project', forbiddenList)).toBe(false);
  });
});
```

- [ ] **Step 2: Run frontend test to verify it fails**

Run: `npm test -- run src/utils/pathValidation.test.ts` (in `src/AIAgentHub.Web/frontend`)
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `pathValidation.ts`**

Create `src/AIAgentHub.Web/frontend/src/utils/pathValidation.ts`:
```typescript
export function normalizePath(path: string): string {
  const clean = path.trim().replace(/\\/g, '/').replace(/\/+$/, '');
  return clean === '' ? '/' : clean;
}

export function isPathForbidden(rawPath: string | null | undefined, forbiddenPatterns: string[]): boolean {
  if (!rawPath || !rawPath.trim()) return true;

  const normalized = normalizePath(rawPath);

  // Check if bare root drive (e.g. "C:" or "C:/" or "/")
  if (/^[a-zA-Z]:\/?$/.test(normalized) || normalized === '/') {
    return true;
  }

  for (const pattern of forbiddenPatterns) {
    if (!pattern) continue;
    const normPattern = normalizePath(pattern);

    // If pattern contains wildcard "*:" or "*"
    if (normPattern.includes('*')) {
      const regexPattern = '^' + normPattern
        .replace(/[.+?^${}()|[\]\\]/g, '\\$&')
        .replace(/\\\*/g, '[^/]+') + '(/.*)?$';
      const re = new RegExp(regexPattern, 'i');
      if (re.test(normalized)) {
        return true;
      }
    } else {
      // Direct prefix or exact match
      const lowerPath = normalized.toLowerCase();
      const lowerPattern = normPattern.toLowerCase();
      if (lowerPath === lowerPattern || lowerPath.startsWith(lowerPattern + '/')) {
        return true;
      }
    }
  }

  return false;
}
```

- [ ] **Step 4: Update `FolderExplorerModal.tsx`**

Integrate forbidden paths validation and toast notifications:
- Fetch forbidden list on mount via `/api/v1/filesystem/forbidden-paths`.
- In `loadDirectory(path)`: If `isPathForbidden(path, forbiddenPaths)`, trigger `showToast("The directory is a protected system folder and cannot be opened.", "error")` and return early without making network request.
- In `handleCreateWorkspace()`: If `isPathForbidden(currentPath, forbiddenPaths)`, trigger `showToast("The selected directory is a protected system folder and cannot be used as a workspace.", "error")` and return early.

- [ ] **Step 5: Run frontend tests & TypeScript build**

Run: `npm test` and `npm run build` in `src/AIAgentHub.Web/frontend`.
Expected: PASS with 0 errors.

---

### Task 4: Solution Verification & Clean Build

- [ ] **Step 1: Run all solution tests**
Run: `dotnet test`
Expected: All tests pass.

- [ ] **Step 2: Build complete solution**
Run: `dotnet build`
Expected: Build succeeded with 0 errors.
