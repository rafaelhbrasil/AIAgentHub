import { describe, it, expect } from 'vitest';
import { ConversationDto } from '../src/types/conversation';

describe('Conversation Sorting by User Interaction Recency', () => {
  it('should sort conversations by lastUserInteractionAtUtc descending', () => {
    const conv1: ConversationDto = {
      id: '1',
      workspaceId: 'ws-1',
      title: 'Older Conv with recent user interaction',
      providerId: 'gemini',
      createdAtUtc: '2026-08-19T10:00:00Z',
      updatedAtUtc: '2026-08-19T10:05:00Z',
      lastUserInteractionAtUtc: '2026-08-19T12:00:00Z',
      messageCount: 5,
      fileChangeCount: 0,
    };

    const conv2: ConversationDto = {
      id: '2',
      workspaceId: 'ws-1',
      title: 'Newer Conv with older user interaction',
      providerId: 'gemini',
      createdAtUtc: '2026-08-19T11:00:00Z',
      updatedAtUtc: '2026-08-19T12:05:00Z', // e.g. AI completed later
      lastUserInteractionAtUtc: '2026-08-19T11:00:00Z',
      messageCount: 2,
      fileChangeCount: 0,
    };

    const convs = [conv2, conv1];
    const sorted = [...convs].sort((a, b) => {
      const timeA = new Date(a.lastUserInteractionAtUtc || a.updatedAtUtc || a.createdAtUtc).getTime();
      const timeB = new Date(b.lastUserInteractionAtUtc || b.updatedAtUtc || b.createdAtUtc).getTime();
      return timeB - timeA;
    });

    expect(sorted[0].id).toBe('1');
    expect(sorted[1].id).toBe('2');
  });

  it('should fallback to updatedAtUtc or createdAtUtc if lastUserInteractionAtUtc is missing', () => {
    const conv1: ConversationDto = {
      id: '1',
      workspaceId: 'ws-1',
      title: 'Legacy Conv 1',
      providerId: 'gemini',
      createdAtUtc: '2026-08-19T10:00:00Z',
      updatedAtUtc: '2026-08-19T10:30:00Z',
      messageCount: 1,
      fileChangeCount: 0,
    };

    const conv2: ConversationDto = {
      id: '2',
      workspaceId: 'ws-1',
      title: 'Legacy Conv 2',
      providerId: 'gemini',
      createdAtUtc: '2026-08-19T11:00:00Z',
      updatedAtUtc: '2026-08-19T11:30:00Z',
      messageCount: 1,
      fileChangeCount: 0,
    };

    const sorted = [conv1, conv2].sort((a, b) => {
      const timeA = new Date(a.lastUserInteractionAtUtc || a.updatedAtUtc || a.createdAtUtc).getTime();
      const timeB = new Date(b.lastUserInteractionAtUtc || b.updatedAtUtc || b.createdAtUtc).getTime();
      return timeB - timeA;
    });

    expect(sorted[0].id).toBe('2');
    expect(sorted[1].id).toBe('1');
  });
});
