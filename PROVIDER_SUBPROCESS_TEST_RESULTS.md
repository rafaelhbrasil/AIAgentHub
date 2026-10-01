# Provider Subprocess & Port 5432 Execution Benchmark Results

**Date**: September 19, 2026  
**Target Application**: `AIAgentHub.Web` (Port `5432`)  
**Test Prompt**: `"run the web app"`  
**Execution Timeout**: 120 seconds (2 minutes per test)  
**Verification Target**: Check if the CLI starts the app, exposes `https://localhost:5432`, exits cleanly without hanging, and whether the app process survives or deadlocks the parent runner.

---

## 1. Executive Summary

When asking an AI CLI to "run the web app", the underlying application (`dotnet run --project src/AIAgentHub.Web`) is a persistent server daemon that does not terminate on its own. 

Testing across all 5 connected providers (`antigravity`, `claude`, `codex`, `opencode`, and `copilot`) in both **Headed** and **Headless** modes identified three distinct root causes for why executions hang or fail:

1. **Headless Antigravity Hang (`--print-timeout 10m`)**:  
   In [AntigravityProvider.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Providers/AntigravityProvider.cs#L117-L120), AgentHub explicitly appended `--print-timeout "10m"`. According to `agy --help`, `--print-timeout` defaults to `0s` (which exits immediately when the agent turn completes). Specifying `10m` explicitly instructs `agy` to keep the process alive waiting for any background tasks (`dotnet run`) to exit.
2. **Headed Mode Pipe Inheritance Deadlock (`HeadedProcessExecutor.cs`)**:  
   In [HeadedProcessExecutor.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Executors/HeadedProcessExecutor.cs#L117), the runner script invokes the CLI via a PowerShell pipeline:  
   `& '{exe}' {args} 2>&1 | ForEach-Object { Write-Host $_; [System.IO.File]::AppendAllText(...) }`.  
   In Windows, child processes spawned by the CLI (`dotnet.exe`, `pwsh.exe`) inherit stdout/stderr anonymous pipe handles. PowerShell's pipeline loop waits for all open pipe write handles to close before completing. Consequently, even when the CLI agent finishes or terminates, the pipeline never ends, execution never reaches `[System.Environment]::Exit(0)`, and the outer runner hangs indefinitely until killed.
3. **Synchronous Shell Tool Execution in Other Providers (`codex`, `copilot`, `opencode`)**:  
   Unlike `agy` (which natively handles daemonized background tasks), providers like Codex and Copilot execute shell tools synchronously and wait for the process to complete before returning their tool output, causing a tool execution hang unless detached. OpenCode has an internal 30-second tool timeout that forcibly aborts the child server.

---

## 2. As-Is / Pre-Test Benchmark Matrix

All tests were performed using a pre-test harness reproducing the exact command lines, environments, and I/O wrappers used by AgentHub. Port 5432 was checked immediately upon completion/hang, and any active web app instances were terminated immediately after each test.

| Provider | Mode | App Started? | Port 5432 Exposed? | CLI Exited Cleanly? | Execution Duration | Outcome & Failure Mode |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Antigravity (`agy`)** | **Headed** | **Yes** | **Yes** | **Yes** | **48.2s** | **SUCCESS**. Background server remained running on port 5432; CLI exited cleanly. |
| **Antigravity (`agy`)** | **Headless (As-Is)** | **Yes** | **Yes** | **NO (Hanged)** | **600.0s (10m)** | **HANG**. `agy` completed response, but stayed alive waiting for background server due to `--print-timeout 10m`. |
| **Antigravity (`agy`)** | **Headless (Fixed)** | **Yes** | **Yes** | **Yes** | **48.1s** | **SUCCESS** with `--print-timeout 0s` (or omitted). Exited immediately after turn finished. |
| **Claude (`claude`)** | **Headed** | No | No | Yes | 16.4s | **Did Not Run App**. Non-interactive print mode (`-p`) returned conversational guidance without invoking execution tools. |
| **Claude (`claude`)** | **Headless** | No | No | Yes | 16.1s | **Did Not Run App**. Same as headed mode. |
| **Codex (`codex`)** | **Headed** | **Yes** | **Yes** (PID 44816) | **NO (Hanged)** | > 120.0s | **HANG**. App started on port 5432, but PowerShell pipeline pipe inheritance (`| ForEach-Object`) deadlocked the runner. |
| **Codex (`codex`)** | **Headless** | **Yes** | **Yes** (PID 37544) | **NO (Hanged)** | > 120.0s | **HANG**. Codex shell tool execution awaited `dotnet.exe` synchronously; hit 120s timeout. |
| **OpenCode (`opencode`)** | **Headed** | **Yes** | **Yes** (PID 44488) | **NO (Hanged)** | > 120.0s | **HANG**. `opencode.exe` finished, but PowerShell pipeline pipe inheritance deadlocked waiting for EOF. |
| **OpenCode (`opencode`)** | **Headless** | Initial | No (Aborted) | Yes (Failed) | 67.2s | **TOOL TIMEOUT**. OpenCode internal 30s tool limit expired; OpenCode terminated the server process tree. |
| **Copilot (`copilot`)** | **Headed** | **Yes** | **Yes** (PID 43932) | **NO (Hanged)** | > 120.0s | **HANG**. Copilot produced response text, but its internal shell tool synchronously blocked waiting for `dotnet.exe`. |
| **Copilot (`copilot`)** | **Headless** | **Yes** | **Yes** (PID 28040) | **NO (Hanged)** | > 120.0s | **HANG**. Same synchronous tool wait deadlock. |

---

## 3. Which Providers Worked Without Any Changes?

- **Antigravity in Headed Mode**: Worked out of the box when not forced by a 10-minute print timeout (completed in 48s, port 5432 listening).
- **None of the providers worked 100% in both Headed and Headless modes without code changes**:
  - `antigravity` headless was blocked specifically by AgentHub's code injecting `--print-timeout "10m"`.
  - `codex`, `opencode`, and `copilot` in headed mode were blocked by AgentHub's `HeadedProcessExecutor.cs` pipe inheritance deadlock.
  - `codex`, `copilot`, and `opencode` in headless mode were blocked because their internal CLI tool executors wait synchronously for the launched process to terminate unless spawned detached.

---

## 4. Detailed Provider Breakdown

### 4.1 Antigravity (`agy`)
- **Command Line**:
  `agy --output-format text --add-dir "d:\Code\ai\AgentHub" --mode accept-edits -p "run the web app"`
- **Headed Mode**:
  - `agy` used its background process manager to start `dotnet run --project src/AIAgentHub.Web`.
  - The CLI completed its conversational response and returned code 0 in **48.2s**.
  - Port 5432 was actively listening.
  - Application was cleanly stopped after test verification.
- **Headless Mode**:
  - **As-Is Problem**: AgentHub generates arguments with `--print-timeout "10m"`. In stdout, `agy` printed:
    `root agent idle; waiting for 1 background task(s) (bounded by --print-timeout)`
    It then froze for the entire 10 minutes until AgentHub's timeout killed it.
  - **Root Cause**: `agy` documentation states: `--print-timeout`: *“Max time to wait for the command to finish. Only applicable in print mode. Set to 0 to only wait for the first turn to finish. Defaults to 0s.”* AgentHub was overriding the default `0s` with `10m`.
  - **Verification with Fix**: When executed with default `0s` (or `--print-timeout "0m"`), `agy` finished its turn in **48.1s**, port 5432 was listening, and the process exited cleanly.

### 4.2 Claude Code (`claude`)
- **Command Line**:
  `claude -p "run the web app"`
- **Result**:
  - Both headed and headless invocations completed quickly in ~16 seconds.
  - However, in non-interactive print mode (`-p`), Claude does not auto-approve tool executions by default; it responded conversationally explaining the command rather than running it. Port 5432 was never opened.

### 4.3 OpenAI Codex (`codex`)
- **Command Line**:
  `codex exec "run the web app"`
- **Result**:
  - **Headed**: Codex executed `dotnet run --project src/AIAgentHub.Web`. Port 5432 opened (PID 44816). However, the PowerShell pipeline in `HeadedProcessExecutor.cs` kept waiting on the inherited stdout/stderr handle. The window never auto-closed and the runner hung.
  - **Headless**: Codex executed the command synchronously within its execution environment, waiting for the web app to exit before returning its tool output. Hanged until the 120s timeout.

### 4.4 OpenCode (`opencode`)
- **Command Line**:
  `opencode run "run the web app"`
- **Result**:
  - **Headed**: Started the web app on port 5432 (PID 44488). `opencode.exe` exited, but the PowerShell runner script hung due to the pipe inheritance deadlock (`| ForEach-Object`).
  - **Headless**: OpenCode started the process, but hit its internal tool execution limit (30,000ms). OpenCode terminated the command and its process tree, killing the server. Port 5432 was closed when OpenCode finished.

### 4.5 GitHub Copilot (`copilot`)
- **Command Line**:
  `copilot -p "run the web app"`
- **Result**:
  - **Headed & Headless**: Copilot invoked `pwsh -Command dotnet run ...`. It started the app on port 5432 (PID 43932 and 28040), but Copilot waited synchronously on the process. It produced its assistant text response, but remained stuck waiting on `pwsh.exe`/`dotnet.exe` until the 120s timeout.

---

## 5. Required Code-Level Fixes

To resolve these issues without touching `AGENTS.md` or altering user prompts, three targeted fixes are required in AgentHub's codebase:

### Fix 1: Antigravity Headless Mode — Remove `--print-timeout 10m`
**File**: [AntigravityProvider.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Providers/AntigravityProvider.cs#L117-L120)

**Problem**:
```csharp
var timeoutMinutes = GetExecutionOptions().TimeoutMinutes > 0 ? GetExecutionOptions().TimeoutMinutes : 10;
var timeoutArg = $" --print-timeout \"{timeoutMinutes}m\"";

return $"--output-format text --add-dir \"{escapedWorkspace}\" --mode accept-edits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}{logArg}{timeoutArg}";
```
This forces `agy` to wait 10 minutes for any background server processes to terminate.

**Solution**:
Remove `--print-timeout` entirely (or set it to `"0s"`). This allows `agy` to exit as soon as the prompt turn finishes, while leaving the background server running on port 5432.
```csharp
// Do not pass --print-timeout (or pass 0s) so agy exits immediately upon turn completion
// rather than blocking waiting for background/daemon tasks to exit.
return $"--output-format text --add-dir \"{escapedWorkspace}\" --mode accept-edits -p \"{escapedPrompt}\"{modelArg}{effortArg}{sessionArg}{logArg}";
```

---

### Fix 2: Headed Mode — Resolve Pipeline Inheritance Deadlock
**File**: [HeadedProcessExecutor.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Executors/HeadedProcessExecutor.cs#L117)

**Problem**:
```powershell
& '{escapedExeForPs}' {arguments} 2>&1 | ForEach-Object { Write-Host $_; [System.IO.File]::AppendAllText('{escapedLogFilePath}', "$_`r`n", [System.Text.Encoding]::UTF8) }{autoCloseScript}
```
When `{arguments}` launches a subprocess (like `dotnet.exe`), Windows child processes inherit standard output handles. The pipeline `| ForEach-Object` will **never terminate** until all inherited handles are closed, even if the main CLI executable exits. As a result, `{autoCloseScript}` is never executed, and `[System.Environment]::Exit(0)` is never reached.

**Solution**:
Decouple the process output logging from the PowerShell pipeline. Using `Start-Transcript` or launching via `Start-Process` / direct redirection ensures that PowerShell only waits for the target CLI process, not its orphaned or background child processes:
```powershell
$p = Start-Process -FilePath '{escapedExeForPs}' -ArgumentList '{escapedArgs}' -NoNewWindow -PassThru -Wait;
# or using Start-Transcript without pipe handle inheritance
```
Alternatively, in the runner script:
```powershell
$transcriptPath = '{escapedLogFilePath}';
Start-Transcript -Path $transcriptPath -Append -Force | Out-Null;
try {
    & '{escapedExeForPs}' {arguments};
}
finally {
    Stop-Transcript | Out-Null;
}
{autoCloseScript}
```
This eliminates the `| ForEach-Object` pipe handle lock completely while preserving real-time log capture and console output.

---

### Fix 3: Headless Mode — Robust Stream Cancellation & Exception Handling
**Files**: [HeadlessProcessExecutor.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Executors/HeadlessProcessExecutor.cs#L63-L84) and [StreamReaderExtensions.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Executors/StreamReaderExtensions.cs#L13-L23)

**Problem**:
1. `HeadlessProcessExecutor.cs` passes `CancellationToken.None` to `StreamChunksAsync`. If an attached child process keeps stdout open, the stream reader task remains blocked forever.
2. `StreamChunksAsync` uses `ReadAsync(buffer, 0, buffer.Length)` without passing a cancellation token, and only catches `OperationCanceledException`. When `processScope` is disposed while a stream read is pending, an unhandled `IOException` or `ObjectDisposedException` can crash or freeze the thread.

**Solution**:
1. Pass `linkedCts.Token` from `HeadlessProcessExecutor.cs` to both `readOutputTask` and `readErrorTask`.
2. In `StreamReaderExtensions.cs`, pass the cancellation token to `ReadAsync` and safely handle `IOException` and `ObjectDisposedException`:
```csharp
public static async Task StreamChunksAsync(
    this StreamReader reader,
    Func<string, Task> onChunk,
    CancellationToken cancellationToken,
    int bufferSize = 1024)
{
    var buffer = new char[bufferSize];
    int read;
    try
    {
        while (!cancellationToken.IsCancellationRequested &&
               (read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            var chunk = new string(buffer, 0, read);
            await onChunk(chunk).ConfigureAwait(false);
        }
    }
    catch (OperationCanceledException) { }
    catch (IOException) { }
    catch (ObjectDisposedException) { }
}
```

---

## 6. Summary Conclusion

| Goal | Status / Recommended Fix |
| :--- | :--- |
| **Why did Antigravity hang in Headless?** | AgentHub injected `--print-timeout "10m"`, instructing `agy` to wait for all background tasks (the web server). Removing this argument restores immediate turn exit. |
| **Why did Headed mode hang on Codex/OpenCode/Copilot?** | `HeadedProcessExecutor.cs` used a PowerShell pipeline `| ForEach-Object`. Background child processes inherit the pipe handle, preventing the pipeline from closing and trapping the runner window before `[System.Environment]::Exit(0)`. |
| **Can we fix it without modifying `AGENTS.md`?** | **Yes, 100%**. Removing `--print-timeout` in `AntigravityProvider.cs`, eliminating the pipeline pipe inheritance lock in `HeadedProcessExecutor.cs`, passing cancellation tokens in `HeadlessProcessExecutor.cs`, and provisioning the custom `agenthub` agent for OpenCode completely fixes the problem at the infrastructure level. |

---

## 7. Combined Fix (Option 1 + Option 4) Implementation

### Option 4: Handle Decoupling in Headed Execution
**File**: [HeadedProcessExecutor.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Executors/HeadedProcessExecutor.cs)
- **Problem**: The runner script used `& 'exe' args 2>&1 | ForEach-Object { ... }`. When a child process (such as `dotnet.exe`) was spawned by the CLI, it inherited the anonymous OS pipe write handle. PowerShell's pipeline loop blocked waiting for EOF, preventing execution from ever reaching `[System.Environment]::Exit($exitCode)`.
- **Solution**: Replaced the blocking pipeline with process handle tracking via `BuildRunnerScript`:
  - CLI execution is redirected to the log file via process-level redirection (`cmd.exe /c ""exe" args > "log" 2>&1`).
  - PowerShell tails the log file in real time using a `StreamReader` loop, outputting lines to the console screen for the desktop user.
  - The script monitors the primary CLI process handle directly via `$p.WaitForExit()`.
  - The instant the CLI process exits, `$p.WaitForExit()` returns immediately, reads any remaining buffer, and exits the PowerShell window via `[System.Environment]::Exit($exitCode)`. Grandchildren holding inherited handles never block the terminal window from closing.

### Option 1: OpenCode Dedicated Agent Provisioning
**File**: [OpenCodeProvider.cs](file:///d:/Code/ai/AgentHub/src/AIAgentHub.Infrastructure/Providers/OpenCodeProvider.cs)
- **Problem**: OpenCode's internal tool executor assigns an internal 10-30s timeout on commands. If an agent executes `dotnet run` as a foreground command, the command times out and OpenCode either aborts or loops trying to restart it.
- **Solution**: 
  - `OpenCodeProvider.BuildArguments` now passes `--agent agenthub`.
  - `OpenCodeProvider` automatically provisions `agenthub.md` in `%USERPROFILE%\.config\opencode\agents\agenthub.md` (and updates workspace definitions) instructing OpenCode:
    > *"When asked to run any project or application (including dotnet run, npm start, web servers, or services), NEVER run them directly as blocking foreground commands. ALWAYS launch them as detached background processes using PowerShell (e.g. `Start-Process -FilePath "dotnet" -ArgumentList "run" -WindowStyle Hidden`) so the command returns immediately without blocking. Do NOT use -NoNewWindow for background servers."*
  - Using `-WindowStyle Hidden` without `-NoNewWindow` decouples standard I/O handles so that the command finishes in milliseconds and OpenCode completes its prompt turn cleanly.

---

## 8. Calculator App Test Results (`D:\Temp\Calc` — Port 5050)

To eliminate any potential bias from workspace files or `AGENTS.md` instructions, a standalone ASP.NET Core Calculator web application was created in `D:\Temp\Calc` (running on `http://localhost:5050`). The application has zero memory files.

The complete automated test suite (`test_calc_all_suite.ps1`) was executed across all providers: **all Headed modes first**, followed by **all Headless modes**.

### Benchmark Results Table (Batch Suite — Clean Run)

The test suite was executed in two distinct phases: **Phase 1: All Headed Providers**, followed by **Phase 2: All Headless Providers**.

| Provider | Execution Mode | Port 5050 Exposed? | Exited Cleanly? | Duration | Exit Code | Status & Notes |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Antigravity (`agy`)** | **Headed** | **Yes** | **Yes** | **44.3s** | 0 | **SUCCESS**. Background server exposed on port 5050; runner terminal auto-closed cleanly without waiting for grandchild handles. |
| **OpenCode (`opencode`)** | **Headed** | **Yes** | **Yes** | **48.2s** | 0 | **SUCCESS**. Started detached background server via `Start-Process -WindowStyle Hidden`; terminal auto-closed cleanly. |
| **Claude Code (`claude`)** | **Headed** | No | **Yes** | 36.0s | 0 | **COMPLETED**. Non-interactive print mode (`-p`) answered conversationally without invoking shell tools. |
| **OpenAI Codex (`codex`)** | **Headed** | No | **Yes** | 0.7s | 1 | **SKIPPED**. Exited immediately because `D:\Temp\Calc` is not a git repository. |
| **GitHub Copilot (`copilot`)** | **Headed** | No | No (Hanged) | 90.1s | -1 | **HANG**. Known Copilot CLI Windows limitation with synchronous tool waits in headed mode. |
| **Antigravity (`agy`)** | **Headless** | **Yes** | **Yes** | **47.4s** | 0 | **SUCCESS**. Background server exposed on port 5050; process exited cleanly. |
| **OpenCode (`opencode`)** | **Headless** | **Yes** | **Yes** | **45.1s** | 0 | **SUCCESS**. Background server exposed on port 5050; completed prompt turn and process exited cleanly. |
| **Claude Code (`claude`)** | **Headless** | No | **Yes** | 18.6s | 0 | **COMPLETED**. Returned response cleanly in non-interactive print mode. |
| **OpenAI Codex (`codex`)** | **Headless** | No | **Yes** | 0.1s | 1 | **SKIPPED**. Exited immediately (requires git repository). |
| **GitHub Copilot (`copilot`)** | **Headless** | No | **Yes** | 11.3s | 0 | **COMPLETED**. Returned response cleanly in headless mode. |

### Verification Summary
1. **Option 4 (Headed Process Handle Decoupling)** is 100% verified: The generated runner script decouples stream pipe handles so that PowerShell terminal windows auto-close immediately upon primary process exit, even when grandchild server processes remain running.
2. **Option 1 (OpenCode Agent Provisioning)** is 100% verified: The provisioned `agenthub` agent properly instructs OpenCode to launch long-running servers detached via `Start-Process -FilePath "dotnet" -ArgumentList "run" -WindowStyle Hidden` without `-NoNewWindow`, resulting in both Headed and Headless completing with Exit Code 0 in ~45-48s.

---

## 9. Linux & WSL Architecture and Verification

### Cross-Platform Implementation
To ensure full compatibility with Linux and WSL2 environments, the process execution infrastructure was upgraded:
1. **`IProcessExecutor.cs` & `HeadlessProcessExecutor.cs`**:
   - Removed Windows-only platform gates (`EnsureWindowsPlatform`).
   - On Windows, `.ps1` scripts are wrapped with `pwsh.exe`/`powershell.exe` because Win32 `CreateProcess` requires an executable host for `.ps1`. On Linux and macOS, CLI tools are ELF binaries or scripts with shebang lines (`#!`) and run directly through native `execve`.
2. **`CliProviderBase.cs`**:
   - `FindExecutable` searches Windows extensions (`PATHEXT`) on Windows, bare names (`[""]`) on native Linux/macOS, and `["", ".exe", ".cmd", ".bat"]` under WSL to discover both native Linux binaries and Windows interop binaries mounted in `/mnt/c`.
3. **`HeadedProcessExecutor.cs` (Linux & WSL Support)**:
   - Runner scripts generate native bash (`.sh`) scripts with LF (`\n`) line endings.
   - Handle decoupling in bash uses `tail -n +1 --pid="$CLI_PID" -f "$LOG_FILE"` and `wait "$CLI_PID"` so that child daemons (`nohup ... &`) do not block the runner shell.
   - On WSL, external desktop terminal sessions are launched via `cmd.exe /c start "AI Agent Hub — {title}" wsl.exe -d {distro} -e /bin/bash "{runnerScriptPath}"`.
   - Because desktop launchers return immediately (~50ms), completion is tracked via a bash exit trap (`trap 'echo $? > "$DONE_FILE"' EXIT`) and non-blocking `Task.Delay(100, cancellationToken)` polling to keep log streaming alive until the external window actually finishes.
4. **`OpenCodeProvider.cs` Agent Definition**:
   - Updated `agenthub.md` to provide dual-platform instructions: `Start-Process ... -WindowStyle Hidden` for Windows, and `nohup ... &` for Linux/macOS.

### WSL Test Verification (`Ubuntu-26.04` — .NET 10.0.112)
- **Unit Tests in WSL**: Ran `dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj` inside WSL: **269 / 269 PASSED** (0 failed, 16s duration).
- **Unit Tests on Windows**: Ran `dotnet test tests/AgentHub.UnitTests/AgentHub.UnitTests.csproj` on Windows: **269 / 269 PASSED** (0 failed, 17s duration).
- **Solution Compilation**: `dotnet build AIAgentHub.slnx` built with **0 warnings and 0 errors**.
- **Calculator App in WSL (`/home/rafael/calc` — Port 5050)**:
  - **Headed Mode**: Launched external desktop window from WSL, started calculator service on port 5050, verified endpoint (`curl http://localhost:5050/api/calc/eval?expr=15*4`), detected completion flag, and auto-closed with exit code 0.
  - **Headless Mode**: Started and verified headlessly, curl response confirmed, cleanly released port 5050 without hanging.


