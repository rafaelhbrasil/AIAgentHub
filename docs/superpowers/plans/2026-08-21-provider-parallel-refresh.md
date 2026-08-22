# Provider Fast Pre-Check, Parallel Loading & Real-time Progress Streaming Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement fast pre-check discovery of installed providers, parallel status/model detection, and real-time SSE progress streaming with an interactive glassmorphic progress modal in the AIAgentHub web interface.

**Architecture:** 
- Backend `IProvider` exposes `IsInstalledFastCheck()` implemented generically in `CliProviderBase` via `FindExecutable(ExecutableName)`.
- `ProviderManager` exposes `StreamRefreshAllAsync` which filters installed providers, fires parallel background detection tasks (`Task.WhenAll`), and yields SSE events incrementally as tasks complete.
- `ProvidersController` exposes `GET /api/v1/providers/refresh-stream` returning `text/event-stream`.
- Frontend `ProviderRefreshModal` connects to the SSE stream, renders an animated 0-100% progress bar and a dynamic checklist of installed providers transitioning from spinners to status badges, remaining open on completion for user review. Single provider refresh remains inline on `ProviderCard`.

**Tech Stack:** C# .NET 9, ASP.NET Core Web API (SSE), SQLite, TypeScript, React, Vanilla CSS (Glassmorphic), Vitest, xUnit.

## Global Constraints

- Never execute slow subprocesses (`--version`, `auth status`, etc.) for uninstalled providers.
- Use generic `ExecutableName` check in `CliProviderBase` without duplicated provider logic.
- Single provider refresh on `ProviderCard` must remain localized (inline spinner) without opening the modal.
- Modal must remain open upon 100% completion with a "Fechar" / "Concluído" button.
- Uninstalled providers are omitted from the progress checklist.
- Specification-first rule: Document in `docs/` and verify builds cleanly via `dotnet build` and `npm run build`.

---

### Task 1: Fast Pre-Check Detection in `IProvider` & `CliProviderBase`

**Files:**
- Modify: `src/AIAgentHub.Application/Providers/IProvider.cs`
- Modify: `src/AIAgentHub.Infrastructure/Providers/CliProviderBase.cs`
- Modify: `src/AIAgentHub.Infrastructure/Providers/AntigravityProvider.cs`
- Test: `tests/AIAgentHub.Infrastructure.Tests/Providers/CliProviderBaseTests.cs` (or create new unit test file)

**Interfaces:**
- Consumes: `ExecutableName`, `FindExecutable(string name)`
- Produces: `bool IsInstalledFastCheck()` on `IProvider`

- [ ] **Step 1: Write the failing test for `IsInstalledFastCheck`**

```csharp
// tests/AIAgentHub.Infrastructure.Tests/Providers/FastPreCheckTests.cs
using AIAgentHub.Infrastructure.Providers;
using AIAgentHub.Infrastructure.Executors;
using AIAgentHub.Domain.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AIAgentHub.Infrastructure.Tests.Providers;

public class FastPreCheckTests
{
    [Fact]
    public void IsInstalledFastCheck_WhenExecutableExists_ReturnsTrue()
    {
        // Arrange
        var mockExecutor = new Mock<IProcessExecutor>();
        var mockLogger = new Mock<IPromptLogger>();
        var options = Options.Create(new CliExecutionOptions());

        var provider = new TestableCliProvider(options, mockLogger.Object, mockExecutor.Object, "cmd");

        // Act
        var result = provider.IsInstalledFastCheck();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsInstalledFastCheck_WhenExecutableDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var mockExecutor = new Mock<IProcessExecutor>();
        var mockLogger = new Mock<IPromptLogger>();
        var options = Options.Create(new CliExecutionOptions());

        var provider = new TestableCliProvider(options, mockLogger.Object, mockExecutor.Object, "non_existent_binary_xyz_999");

        // Act
        var result = provider.IsInstalledFastCheck();

        // Assert
        Assert.False(result);
    }

    private class TestableCliProvider(
        IOptions<CliExecutionOptions> options,
        IPromptLogger logger,
        IProcessExecutor executor,
        string exeName) : CliProviderBase(options, logger, executor)
    {
        public override string Id => "test";
        public override string ExecutableName => exeName;
        public override string? InstallCommand => null;
        public override AIAgentHub.Domain.Providers.ProviderCapability Capabilities => AIAgentHub.Domain.Providers.ProviderCapability.None;
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compiler error: `IsInstalledFastCheck` not yet declared)**

Run: `dotnet test tests/AIAgentHub.Infrastructure.Tests --filter FastPreCheckTests`
Expected: FAIL (compilation error)

- [ ] **Step 3: Implement `IsInstalledFastCheck()` in `IProvider.cs` and `CliProviderBase.cs`**

In `IProvider.cs`:
```csharp
bool IsInstalledFastCheck();
```

In `CliProviderBase.cs`:
```csharp
public virtual bool IsInstalledFastCheck() => !string.IsNullOrEmpty(FindExecutable(ExecutableName));
```

In `AntigravityProvider.cs`:
```csharp
public override bool IsInstalledFastCheck() =>
    !string.IsNullOrEmpty(FindExecutable(ExecutableName)) || !string.IsNullOrEmpty(FindExecutable("antigravity"));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AIAgentHub.Infrastructure.Tests --filter FastPreCheckTests`
Expected: PASS (2 passed)

- [ ] **Step 5: Commit changes**

```bash
git add src/AIAgentHub.Application/Providers/IProvider.cs src/AIAgentHub.Infrastructure/Providers/CliProviderBase.cs src/AIAgentHub.Infrastructure/Providers/AntigravityProvider.cs tests/AIAgentHub.Infrastructure.Tests/Providers/FastPreCheckTests.cs
git commit -m "feat(providers): add fast pre-check installation detection on IProvider and CliProviderBase"
```

---

### Task 2: Provider Manager Parallel Streaming & SSE Controller Endpoint

**Files:**
- Create: `src/AIAgentHub.Domain/Providers/ProviderRefreshEvent.cs`
- Modify: `src/AIAgentHub.Application/Providers/IProviderManager.cs`
- Modify: `src/AIAgentHub.Application/Providers/ProviderManager.cs`
- Modify: `src/AIAgentHub.Web/Controllers/ProvidersController.cs`
- Test: `tests/AIAgentHub.Application.Tests/Providers/ProviderManagerStreamingTests.cs`
- Test: `tests/AIAgentHub.Web.Tests/Controllers/ProvidersControllerStreamingTests.cs`

**Interfaces:**
- Consumes: `IProvider.IsInstalledFastCheck()`, `IProvider.DetectDetailedAsync()`, `IProvider.GetModelsAsync()`
- Produces: `IAsyncEnumerable<ProviderRefreshEvent> StreamRefreshAllAsync(CancellationToken cancellationToken)`
- Produces: `GET /api/v1/providers/refresh-stream` (SSE)

- [ ] **Step 1: Define `ProviderRefreshEvent` types**

Create `src/AIAgentHub.Domain/Providers/ProviderRefreshEvent.cs`:
```csharp
namespace AIAgentHub.Domain.Providers;

public sealed record ProviderHeader(string Id, string DisplayName);

public abstract record ProviderRefreshEvent(string Type);

public sealed record ProviderRefreshInitEvent(
    int TotalInstalled,
    IReadOnlyList<ProviderHeader> Providers) : ProviderRefreshEvent("init");

public sealed record ProviderRefreshProgressEvent(
    ProviderInfo Provider,
    int CompletedCount,
    int TotalInstalled,
    int Percentage) : ProviderRefreshEvent("provider_completed");

public sealed record ProviderRefreshCompletedEvent(
    IReadOnlyList<ProviderInfo> Providers) : ProviderRefreshEvent("completed");
```

- [ ] **Step 2: Write failing unit test for `StreamRefreshAllAsync` in `ProviderManager`**

```csharp
// tests/AIAgentHub.Application.Tests/Providers/ProviderManagerStreamingTests.cs
using AIAgentHub.Application.Providers;
using AIAgentHub.Domain.Providers;
using Moq;
using Xunit;

namespace AIAgentHub.Application.Tests.Providers;

public class ProviderManagerStreamingTests
{
    [Fact]
    public async Task StreamRefreshAllAsync_FiltersUninstalledAndStreamsProgress()
    {
        // Arrange
        var mockInstalledProvider = new Mock<IProvider>();
        mockInstalledProvider.Setup(p => p.Id).Returns("installed1");
        mockInstalledProvider.Setup(p => p.DisplayName).Returns("Installed Provider");
        mockInstalledProvider.Setup(p => p.IsInstalledFastCheck()).Returns(true);
        mockInstalledProvider.Setup(p => p.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderInfo { Id = "installed1", DisplayName = "Installed Provider", Status = ProviderStatus.Ready });
        mockInstalledProvider.Setup(p => p.DetectDetailedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderDetectionResult(ProviderStatus.Ready, "Ready", null));
        mockInstalledProvider.Setup(p => p.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModelInfo>());

        var mockUninstalledProvider = new Mock<IProvider>();
        mockUninstalledProvider.Setup(p => p.Id).Returns("uninstalled1");
        mockUninstalledProvider.Setup(p => p.DisplayName).Returns("Uninstalled Provider");
        mockUninstalledProvider.Setup(p => p.IsInstalledFastCheck()).Returns(false);

        var manager = new ProviderManager(
            [mockInstalledProvider.Object, mockUninstalledProvider.Object],
            null!,
            null!);

        // Act
        var events = new List<ProviderRefreshEvent>();
        await foreach (var evt in manager.StreamRefreshAllAsync(CancellationToken.None))
        {
            events.Add(evt);
        }

        // Assert
        Assert.Contains(events, e => e is ProviderRefreshInitEvent init && init.TotalInstalled == 1);
        Assert.Contains(events, e => e is ProviderRefreshProgressEvent prog && prog.Provider.Id == "installed1");
        Assert.Contains(events, e => e is ProviderRefreshCompletedEvent);
        mockUninstalledProvider.Verify(p => p.DetectDetailedAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/AIAgentHub.Application.Tests --filter ProviderManagerStreamingTests`
Expected: FAIL (compilation error: method not yet on interface/class)

- [ ] **Step 4: Implement `StreamRefreshAllAsync` in `IProviderManager` and `ProviderManager`**

In `IProviderManager.cs`:
```csharp
IAsyncEnumerable<ProviderRefreshEvent> StreamRefreshAllAsync(CancellationToken cancellationToken = default);
```

In `ProviderManager.cs`:
```csharp
public async IAsyncEnumerable<ProviderRefreshEvent> StreamRefreshAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var installedProviders = new List<IProvider>();
    var uninstalledProviders = new List<IProvider>();

    foreach (var provider in _providers)
    {
        if (provider.IsInstalledFastCheck())
        {
            installedProviders.Add(provider);
        }
        else
        {
            uninstalledProviders.Add(provider);
        }
    }

    // Immediately record uninstalled providers in DB without running child processes
    foreach (var uninstalled in uninstalledProviders)
    {
        var notInstalledInfo = new ProviderInfo
        {
            Id = uninstalled.Id,
            DisplayName = uninstalled.DisplayName,
            Description = uninstalled.Description,
            IsInstalled = false,
            IsAuthenticated = false,
            Status = ProviderStatus.NotInstalled,
            Message = $"{uninstalled.DisplayName} is not installed.",
            Capabilities = uninstalled.Capabilities,
            SupportedModels = [],
            InstallInstructions = uninstalled.InstallInstructions,
            InstallCommand = uninstalled.InstallCommand,
            AuthCommand = uninstalled.AuthCommand,
            DocumentationUrl = uninstalled.DocumentationUrl
        };
        await PersistDetectionResultAsync(uninstalled.Id, notInstalledInfo, cancellationToken);
    }

    var totalInstalled = installedProviders.Count;
    var headers = installedProviders.Select(p => new ProviderHeader(p.Id, p.DisplayName)).ToList();
    
    yield return new ProviderRefreshInitEvent(totalInstalled, headers);

    if (totalInstalled == 0)
    {
        var allCached = await GetAllAsync(cancellationToken);
        yield return new ProviderRefreshCompletedEvent(allCached);
        yield break;
    }

    var completedChannel = Channel.CreateUnbounded<(ProviderInfo Info, int CompletedCount)>();
    var completedCounter = 0;

    var tasks = installedProviders.Select(async provider =>
    {
        ProviderInfo info;
        try
        {
            var detailed = await provider.DetectDetailedAsync(cancellationToken);
            await PersistDetailedResultAsync(provider.Id, detailed, cancellationToken);

            var models = detailed.Status == ProviderStatus.Ready
                ? await GetModelsAsync(provider.Id, forceRefresh: true, cancellationToken)
                : await GetModelsAsync(provider.Id, forceRefresh: false, cancellationToken);

            info = new ProviderInfo
            {
                Id = provider.Id,
                DisplayName = provider.DisplayName,
                Description = provider.Description,
                IsInstalled = detailed.Status != ProviderStatus.NotInstalled,
                IsAuthenticated = detailed.Status == ProviderStatus.Ready,
                Status = detailed.Status,
                Message = detailed.Message,
                QuotaResetsAt = detailed.QuotaResetsAt,
                Capabilities = provider.Capabilities,
                SupportedModels = [.. models],
                InstallInstructions = provider.InstallInstructions,
                InstallCommand = provider.InstallCommand,
                AuthCommand = provider.AuthCommand,
                DocumentationUrl = provider.DocumentationUrl
            };
            await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
        }
        catch (Exception ex)
        {
            info = new ProviderInfo
            {
                Id = provider.Id,
                DisplayName = provider.DisplayName,
                Description = provider.Description,
                IsInstalled = true,
                IsAuthenticated = false,
                Status = ProviderStatus.Error,
                Message = $"Detection failed: {ex.Message}",
                Capabilities = provider.Capabilities,
                SupportedModels = [],
                InstallInstructions = provider.InstallInstructions,
                InstallCommand = provider.InstallCommand,
                AuthCommand = provider.AuthCommand,
                DocumentationUrl = provider.DocumentationUrl
            };
            await PersistDetectionResultAsync(provider.Id, info, cancellationToken);
        }

        var count = Interlocked.Increment(ref completedCounter);
        await completedChannel.Writer.WriteAsync((info, count), cancellationToken);
    });

    _ = Task.WhenAll(tasks).ContinueWith(_ => completedChannel.Writer.Complete(), cancellationToken);

    while (await completedChannel.Reader.WaitToReadAsync(cancellationToken))
    {
        while (completedChannel.Reader.TryRead(out var item))
        {
            var percentage = (int)Math.Round((double)item.CompletedCount / totalInstalled * 100.0);
            yield return new ProviderRefreshProgressEvent(item.Info, item.CompletedCount, totalInstalled, percentage);
        }
    }

    var allFinal = await GetAllAsync(cancellationToken);
    yield return new ProviderRefreshCompletedEvent(allFinal);
}
```

- [ ] **Step 5: Add SSE endpoint in `ProvidersController.cs`**

```csharp
[HttpGet("refresh-stream")]
public async Task RefreshStream(CancellationToken cancellationToken)
{
    Response.Headers.Append("Content-Type", "text/event-stream");
    Response.Headers.Append("Cache-Control", "no-cache, no-transform");
    Response.Headers.Append("Connection", "keep-alive");

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    await foreach (var evt in _providerManager.StreamRefreshAllAsync(cancellationToken))
    {
        var json = JsonSerializer.Serialize((object)evt, evt.GetType(), jsonOptions);
        var message = $"event: {evt.Type}\ndata: {json}\n\n";
        await Response.WriteAsync(message, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AIAgentHub.Application.Tests --filter ProviderManagerStreamingTests`
Expected: PASS

- [ ] **Step 7: Commit changes**

```bash
git add src/AIAgentHub.Domain/Providers/ProviderRefreshEvent.cs src/AIAgentHub.Application/Providers/IProviderManager.cs src/AIAgentHub.Application/Providers/ProviderManager.cs src/AIAgentHub.Web/Controllers/ProvidersController.cs tests/AIAgentHub.Application.Tests/Providers/ProviderManagerStreamingTests.cs
git commit -m "feat(providers): add parallel provider refresh streaming and SSE endpoint"
```

---

### Task 3: Frontend Interactive Progress Modal & ProvidersView Integration

**Files:**
- Create: `src/AIAgentHub.Web/frontend/src/components/providers/ProviderRefreshModal.tsx`
- Modify: `src/AIAgentHub.Web/frontend/src/components/providers/ProvidersView.tsx`
- Modify: `src/AIAgentHub.Web/frontend/src/components/providers/ProviderCard.tsx`
- Test: `src/AIAgentHub.Web/frontend/tests/providerRefreshModal.test.ts` (or equivalent Vitest test)

**Interfaces:**
- Consumes: `/api/v1/providers/refresh-stream`
- Produces: Live animated modal showing progress bar and status icons per installed provider (`✅`, `⚠️`, `❌`, `⏳`, `⏹️`).

- [ ] **Step 1: Create `ProviderRefreshModal.tsx`**

Create `src/AIAgentHub.Web/frontend/src/components/providers/ProviderRefreshModal.tsx`:
```tsx
import React, { useState, useEffect } from 'react';
import { ProviderDto, ProviderStatusDto } from '../../types/provider';

interface ProviderItemState {
  id: string;
  displayName: string;
  isCompleted: boolean;
  status?: ProviderStatusDto;
  message?: string;
}

interface ProviderRefreshModalProps {
  onComplete: (providers: ProviderDto[]) => void;
  onClose: () => void;
}

export const ProviderRefreshModal: React.FC<ProviderRefreshModalProps> = ({ onComplete, onClose }) => {
  const [items, setItems] = useState<ProviderItemState[]>([]);
  const [completedCount, setCompletedCount] = useState<number>(0);
  const [totalInstalled, setTotalInstalled] = useState<number>(0);
  const [percentage, setPercentage] = useState<number>(0);
  const [isDone, setIsDone] = useState<boolean>(false);
  const [statusText, setStatusText] = useState<string>('Detectando providers instalados...');

  useEffect(() => {
    let isCancelled = false;
    const eventSource = new EventSource('/api/v1/providers/refresh-stream');

    eventSource.addEventListener('init', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        setTotalInstalled(data.totalInstalled || 0);
        if (data.providers && data.providers.length > 0) {
          setItems(
            data.providers.map((p: { id: string; displayName: string }) => ({
              id: p.id,
              displayName: p.displayName,
              isCompleted: false,
            }))
          );
          setStatusText(`Verificando ${data.totalInstalled} providers em paralelo...`);
        } else {
          setStatusText('Nenhum provider instalado detectado.');
        }
      } catch (err) {
        console.error('Error parsing init event', err);
      }
    });

    eventSource.addEventListener('provider_completed', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        setCompletedCount(data.completedCount || 0);
        setPercentage(data.percentage || 0);
        
        setItems((prev) =>
          prev.map((item) =>
            item.id === data.provider?.id
              ? {
                  ...item,
                  isCompleted: true,
                  status: data.provider?.status,
                  message: data.provider?.message,
                }
              : item
          )
        );
      } catch (err) {
        console.error('Error parsing provider_completed event', err);
      }
    });

    eventSource.addEventListener('completed', (e: MessageEvent) => {
      if (isCancelled) return;
      try {
        const data = JSON.parse(e.data);
        setPercentage(100);
        setIsDone(true);
        setStatusText('Atualização concluída com sucesso.');
        eventSource.close();
        if (data.providers) {
          onComplete(data.providers);
        }
      } catch (err) {
        console.error('Error parsing completed event', err);
        eventSource.close();
      }
    });

    eventSource.onerror = (err) => {
      if (isCancelled) return;
      console.warn('SSE stream closed or encountered error', err);
      eventSource.close();
      setIsDone(true);
    };

    return () => {
      isCancelled = true;
      eventSource.close();
    };
  }, [onComplete]);

  const renderStatusBadge = (item: ProviderItemState) => {
    if (!item.isCompleted) {
      return (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px', color: 'var(--text-muted)' }}>
          <span className="spinner-sm"></span> Verificando...
        </span>
      );
    }

    const status = item.status;
    if (status === 'Ready') {
      return <span className="badge badge-ready">✅ Operational</span>;
    }
    if (status === 'Unauthenticated') {
      return <span className="badge badge-warning">⚠️ Not Authenticated</span>;
    }
    if (status === 'QuotaExceeded') {
      return <span className="badge badge-error">⏳ Quota Exceeded</span>;
    }
    if (status === 'Discontinued') {
      return <span className="badge badge-error">⏹️ Discontinued</span>;
    }
    return <span className="badge badge-error">❌ Failed</span>;
  };

  return (
    <div style={{ padding: '4px 0', minWidth: '420px' }}>
      <div style={{ marginBottom: '16px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px', fontSize: '0.88rem' }}>
          <span>{statusText}</span>
          <strong>{totalInstalled > 0 ? `${completedCount} / ${totalInstalled} (${percentage}%)` : `${percentage}%`}</strong>
        </div>
        <div
          style={{
            width: '100%',
            height: '10px',
            backgroundColor: 'rgba(255, 255, 255, 0.1)',
            borderRadius: '6px',
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: `${percentage}%`,
              height: '100%',
              background: 'linear-gradient(90deg, #3b82f6, #10b981)',
              transition: 'width 0.3s ease',
            }}
          />
        </div>
      </div>

      <div
        style={{
          maxHeight: '260px',
          overflowY: 'auto',
          border: '1px solid var(--border-color)',
          borderRadius: '8px',
          padding: '10px 14px',
          marginBottom: '20px',
          background: 'rgba(0, 0, 0, 0.2)',
        }}
      >
        {items.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '16px', color: 'var(--text-muted)' }}>
            <span className="spinner-sm" style={{ marginRight: '8px' }}></span> Detectando providers instalados no sistema...
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
            {items.map((item) => (
              <div
                key={item.id}
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  padding: '6px 0',
                  borderBottom: '1px solid rgba(255, 255, 255, 0.05)',
                }}
              >
                <span style={{ fontWeight: 500 }}>{item.displayName}</span>
                {renderStatusBadge(item)}
              </div>
            ))}
          </div>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <button
          type="button"
          className="btn btn-primary"
          onClick={onClose}
          disabled={!isDone}
        >
          {isDone ? 'Concluído' : 'Aguarde...'}
        </button>
      </div>
    </div>
  );
};
```

- [ ] **Step 2: Connect `ProviderRefreshModal` in `ProvidersView.tsx`**

Update `ProvidersView.tsx` to open `ProviderRefreshModal` when the user triggers "Refresh All Providers" or on initial cache-less load:
```tsx
const openRefreshProgressModal = () => {
  showModal(
    '🔄 Refreshing AI Providers',
    <ProviderRefreshModal
      onComplete={(updatedProviders) => {
        setProviders(updatedProviders);
        showToast('All providers refreshed successfully.', 'success');
      }}
      onClose={hideModal}
    />
  );
};
```

- [ ] **Step 3: Run Vitest frontend test suite**

Run: `cd src/AIAgentHub.Web/frontend && npm test`
Expected: PASS

- [ ] **Step 4: Build frontend bundle**

Run: `cd src/AIAgentHub.Web/frontend && npm run build`
Expected: PASS

- [ ] **Step 5: Commit changes**

```bash
git add src/AIAgentHub.Web/frontend/src/components/providers/ProviderRefreshModal.tsx src/AIAgentHub.Web/frontend/src/components/providers/ProvidersView.tsx src/AIAgentHub.Web/frontend/src/components/providers/ProviderCard.tsx
git commit -m "feat(providers): add ProviderRefreshModal with progress bar, status checklist, and live SSE updates"
```

---

### Task 4: Full Solution Build & Verification

**Files:**
- Solution-wide build & test verification

- [ ] **Step 1: Run complete backend build and test suite**

Run: `dotnet build AIAgentHub.slnx`
Expected: Build succeeded with 0 errors.

Run: `dotnet test AIAgentHub.slnx`
Expected: All tests pass.

- [ ] **Step 2: Run frontend build**

Run: `cd src/AIAgentHub.Web/frontend && npm run build`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit any final sync adjustments**

```bash
git commit --allow-empty -m "chore(providers): verify full solution build and tests pass for provider parallel refresh"
```
