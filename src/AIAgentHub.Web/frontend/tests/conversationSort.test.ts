import { describe, it, expect } from 'vitest';
import { ConversationDto } from '../src/types/conversation';

describe('Conversation Sorting with Pinned Precedence and Recency', () => {
  it('should sort pinned conversations before unpinned conversations', () => {
    const unpinnedRecent: ConversationDto = {
      id: '1',
      workspaceId: 'ws-1',
      title: 'Recent unpinned',
      providerId: 'gemini',
      createdAtUtc: '2026-08-28T12:00:00Z',
      updatedAtUtc: '2026-08-28T12:00:00Z',
      lastUserInteractionAtUtc: '2026-08-28T12:00:00Z',
      messageCount: 5,
      fileChangeCount: 0,
      isPinned: false,
    };

    const pinnedOlder: ConversationDto = {
      id: '2',
      workspaceId: 'ws-1',
      title: 'Older pinned',
      providerId: 'gemini',
      createdAtUtc: '2026-08-20T10:00:00Z',
      updatedAtUtc: '2026-08-20T10:00:00Z',
      lastUserInteractionAtUtc: '2026-08-20T10:00:00Z',
      messageCount: 2,
      fileChangeCount: 0,
      isPinned: true,
    };

    const pinnedNewer: ConversationDto = {
      id: '3',
      workspaceId: 'ws-1',
      title: 'Newer pinned',
      providerId: 'gemini',
      createdAtUtc: '2026-08-25T10:00:00Z',
      updatedAtUtc: '2026-08-25T10:00:00Z',
      lastUserInteractionAtUtc: '2026-08-25T10:00:00Z',
      messageCount: 3,
      fileChangeCount: 0,
      isPinned: true,
    };

    const convs = [unpinnedRecent, pinnedOlder, pinnedNewer];

    const sortByInteraction = (a: ConversationDto, b: ConversationDto) => {
      const timeA = new Date(a.lastUserInteractionAtUtc || a.updatedAtUtc || a.createdAtUtc).getTime();
      const timeB = new Date(b.lastUserInteractionAtUtc || b.updatedAtUtc || b.createdAtUtc).getTime();
      return timeB - timeA;
    };

    const pinned = convs.filter((c) => c.isPinned).sort(sortByInteraction);
    const unpinned = convs.filter((c) => !c.isPinned).sort(sortByInteraction);
    const result = [...pinned, ...unpinned];

    expect(result[0].id).toBe('3'); // pinned newer
    expect(result[1].id).toBe('2'); // pinned older
    expect(result[2].id).toBe('1'); // unpinned recent
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
