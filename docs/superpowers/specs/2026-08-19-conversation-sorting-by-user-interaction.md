# Specification: Conversation Sorting by User Interaction (Recency)

## Overview
Conversations in each workspace must be listed sorted by most recent user interaction first.
When a user sends a prompt, creates a conversation, or renames a conversation, that conversation immediately becomes the most recent and appears at the top of the conversation list. Background AI streaming, model response completion, and file-change detection events must NOT alter this user-interaction-driven order.

## Domain Model Updates
1. **`Conversation` Entity**:
   - Introduce `LastUserInteractionAtUtc` property (`DateTimeOffset`).
   - Initialized to `DateTimeOffset.UtcNow` upon `Conversation.Create(...)`.
   - Updated to `DateTimeOffset.UtcNow` when:
     - The user sends a prompt / message (`AddMessage` with `MessageRole.User`).
     - The user renames the conversation (`Rename(...)`).
     - The user updates conversation model / provider / effort settings.
   - When the AI generates or finishes a response (`MessageRole.Assistant`), or attaches file changes, `UpdatedAtUtc` is updated for audit/telemetry, but `LastUserInteractionAtUtc` remains untouched.

2. **Database & Query Sorting**:
   - `IConversationRepository.GetByWorkspaceIdAsync` orders conversations by `LastUserInteractionAtUtc` descending.
   - `IConversationService.GetByWorkspaceIdAsync` and `SearchAsync` order results by `LastUserInteractionAtUtc` descending.

3. **API & DTO Contract**:
   - `ConversationDto` and `ConversationDetailDto` expose `LastUserInteractionAtUtc` (ISO-8601 string in JSON).

4. **Frontend Behavior**:
   - Sidebar conversation list renders conversations ordered by most recent user interaction first.
   - When the user sends a prompt, the active conversation's `lastUserInteractionAtUtc` is immediately updated in state and moved to the top (index 0) of the list without waiting for AI streaming or completion.
   - When the user creates a new conversation, it is inserted at the top of the list.
   - When background SignalR events (`conversation.completed`, `streamChunk`) arrive, the conversation ordering remains stable.
