# Thinking Stream & Real-Time Progress Box Specification

## Overview
When interacting with AI coding assistants (e.g. Antigravity, Claude Code, OpenAI Codex, OpenCode/DeepSeek-R1), models and agents often spend 10–40 seconds planning, executing sub-tasks, and generating internal chain-of-thought reasoning before delivering their final response.

This specification introduces **Thinking Stream & Real-Time Progress Boxes** across AI Agent Hub:
1. **Real-time Streaming**: Providers stream progress updates, tool execution steps, and reasoning tokens incrementally to the user as they occur.
2. **Unified Thinking Box**: All consecutive thinking and reasoning output before non-thinking output is grouped into a single styled, collapsible container.
3. **Explicit Disclaimer Header**: The thinking box header clearly states that it is an internal reasoning/progress stream and not the final output.
4. **Clean Non-Thinking Handoff**: Once the final response begins streaming, it renders cleanly outside and below the thinking box.

---

## Architecture & Data Flow

```
┌─────────────────────────┐
│  AI CLI / Agent Process │  (Antigravity / Codex / OpenCode / Claude)
└───────────┬─────────────┘
            │ Real-time NDJSON / stdout stream
            ▼
┌─────────────────────────┐
│  Provider Adapter       │  (Wraps thought/steps in <think>...</think>)
└───────────┬─────────────┘
            │ Real-time Token Chunks (SignalR)
            ▼
┌─────────────────────────┐
│  Frontend Markdown      │  (Groups <think> tags into <details class="thought-box">)
└───────────┬─────────────┘
            │ Live rendered HTML
            ▼
┌─────────────────────────┐
│  Chat UI View           │  (Active open thought box with pulsing icon + final response)
└─────────────────────────┘
```

---

## Functional Requirements

### 1. Tag Normalization & Provider Emittance
- Thinking tokens, intermediate chain-of-thought, and pre-response tool progress are enclosed in `<think>...</think>` tags (or `<thought>...</thought>` / `<thinking>...</thinking>`).
- **AntigravityProvider (`agy`)**:
  - Uses `--output-format stream-json`.
  - Emits real-time step updates (checkpoints, tool calls, reasoning) wrapped in `<think>...</think>`.
  - Streams `text_delta` from `agent_response` directly as final response output.
- **CodexCliProvider (`codex`)**:
  - Emits `reasoning` and command execution progress (`⚡ Running command...`) enclosed in `<think>...</think>`.
  - Streams `agent_message` as final response output.
- **OpenCodeProvider (`opencode`)**:
  - Passes through native `<think>...</think>` tokens emitted by DeepSeek-R1 / Ollama / local LLMs.
- **ClaudeCodeProvider (`claude`)**:
  - Passes through thinking tags and chain-of-thought tokens.

### 2. Frontend Grouping & Parsing (`markdown.ts`)
- **Single Box Grouping**:
  - Consecutive `<think>` blocks are aggregated into a single thinking box.
  - Active streaming handling: If a trailing `<think>` tag is opened and not yet closed (`</think>` has not arrived yet because the model is actively thinking), the parser treats all content after `<think>` as the active thought stream.
- **HTML Container Structure**:
  ```html
  <details class="thought-box" open>
    <summary class="thought-summary">
      <span class="thought-icon">🧠</span>
      <span class="thought-title">Thinking Stream</span>
      <span class="thought-notice">— (Internal reasoning &amp; progress, not final output)</span>
    </summary>
    <div class="thought-content">
      <!-- Formatted thought content with markdown/code rendering -->
    </div>
  </details>
  ```
- **Post-Thinking Content**: Any text outside the `<think>` blocks is rendered below the thinking box as standard markdown response content.

### 3. Visual Styling & Aesthetics (`index.css`)
- Glassmorphic card design matching the dark theme:
  - Container: Translucent dark slate background (`rgba(15, 23, 42, 0.6)`), 1px border (`rgba(99, 102, 241, 0.25)`), left glowing accent (`3px solid #6366f1`).
  - Header: Indigo title, muted italic notice (`#94a3b8`), pointer cursor, and subtle hover transition.
  - Icon: Pulsing brain icon (`🧠`) when streaming is active.
  - Content: Muted slate text (`#cbd5e1`), slightly italic or dimmed to distinguish from final answer text.

---

## Verification Plan

### Automated Tests
1. **Frontend Unit Tests (`tests/markdown.test.ts`)**:
   - Verify parsing of complete `<think>...</think>` block.
   - Verify parsing of unclosed `<think>...` during active streaming.
   - Verify grouping of multiple `<think>` blocks into a single thought box.
   - Verify box title and disclaimer notice.
   - Verify that DOMPurify preserves `<details class="thought-box" open>`.
2. **Backend Unit / Integration Tests**:
   - Verify `AntigravityProvider` and `CodexCliProvider` argument construction and buffer processing.
