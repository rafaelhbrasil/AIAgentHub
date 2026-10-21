import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import React, { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { useWorkspaceStudio, UseWorkspaceStudioProps } from '../src/components/workspaces/studio/useWorkspaceStudio';
import { WorkspaceDto } from '../src/types/workspace';
import { ConversationDto, ConversationDetailDto, MessageRole } from '../src/types/conversation';
import { ProviderStatus } from '../src/types/provider';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import * as apiClient from '../src/services/apiClient';
import { signalRService } from '../src/services/signalrService';

describe('useWorkspaceStudio Hook', () => {
  let container: HTMLDivElement;
  let root: Root | null = null;

  beforeEach(() => {
    vi.restoreAllMocks();
    container = document.createElement('div');
    document.body.appendChild(container);

    vi.spyOn(signalRService, 'joinConversation').mockImplementation(() => {});
    vi.spyOn(signalRService, 'leaveConversation').mockImplementation(() => {});
  });

  afterEach(() => {
    if (root) {
      act(() => {
        root?.unmount();
      });
      root = null;
    }
    container.remove();
  });

  const mockWorkspace: WorkspaceDto = {
    id: 'ws-test-101',
    name: 'Test Project',
    path: '/path/to/project',
    createdAtUtc: new Date().toISOString(),
    settings: {
      defaultProviderId: 'claude-code',
      defaultModelId: 'claude-3-5-sonnet',
    },
  };

  const mockConvs: ConversationDto[] = [
    {
      id: 'conv-101',
      workspaceId: 'ws-test-101',
      title: 'Active Chat',
      providerId: 'claude-code',
      modelId: 'claude-3-5-sonnet',
      isPinned: false,
      messageCount: 2,
      createdAtUtc: new Date(Date.now() - 10000).toISOString(),
      updatedAtUtc: new Date(Date.now() - 1000).toISOString(),
    },
  ];

  const mockDetail: ConversationDetailDto = {
    id: 'conv-101',
    workspaceId: 'ws-test-101',
    title: 'Active Chat',
    providerId: 'claude-code',
    modelId: 'claude-3-5-sonnet',
    isPinned: false,
    isRunning: false,
    messages: [
      {
        id: 'msg-1',
        conversationId: 'conv-101',
        role: MessageRole.User,
        content: 'Hello AI',
        createdAtUtc: new Date().toISOString(),
      },
    ],
    createdAtUtc: new Date().toISOString(),
  };

  const setupApiMocks = () => {
    vi.spyOn(apiClient, 'apiFetch').mockImplementation(async (url: string, opts?: any) => {
      if (url.includes('/api/v1/filesystem/tree')) {
        return {
          ok: true,
          status: 200,
          data: {
            name: 'root',
            relativePath: '',
            isDirectory: true,
            children: [{ name: 'index.ts', relativePath: 'index.ts', isDirectory: false }],
          },
        };
      }
      if (url.includes('/api/v1/conversations?workspaceId=')) {
        return { ok: true, status: 200, data: mockConvs };
      }
      if (url.includes('/api/v1/git/status')) {
        return {
          ok: true,
          status: 200,
          data: {
            isGitInstalled: true,
            isRepository: true,
            currentBranch: 'main',
            ahead: 0,
            behind: 0,
            staged: [],
            modified: [],
            untracked: [],
          },
        };
      }
      if (url.includes('/api/v1/conversations/conv-101/prompt')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/conversations/conv-101/abort')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/conversations/conv-101/pin')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/conversations/conv-101/model')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/conversations/conv-101')) {
        return { ok: true, status: 200, data: mockDetail };
      }
      if (url.includes('/api/v1/diffs?conversationId=')) {
        return { ok: true, status: 200, data: [] };
      }
      if (url.includes('/api/v1/diffs/accept-all')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/diffs/reject-all')) {
        return { ok: true, status: 200, data: {} };
      }
      if (url.includes('/api/v1/providers/claude-code/models')) {
        return {
          ok: true,
          status: 200,
          data: [{ id: 'claude-3-5-sonnet', displayName: 'Claude 3.5 Sonnet', isDisplayed: true }],
        };
      }
      if (url.includes('/api/v1/providers/claude-code/status')) {
        return {
          ok: true,
          status: 200,
          data: { providerId: 'claude-code', status: ProviderStatus.Ready },
        };
      }
      return { ok: true, status: 200, data: {} };
    });
  };

  it('fetches workspace data, selects target conversation, and joins SignalR room', async () => {
    setupApiMocks();
    let hookResult: ReturnType<typeof useWorkspaceStudio> | undefined;

    const TestHarness: React.FC<UseWorkspaceStudioProps> = (props) => {
      hookResult = useWorkspaceStudio(props);
      return <div>Active: {hookResult.activeConversation?.title}</div>;
    };

    await act(async () => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <ModalProvider>
            <TestHarness workspace={mockWorkspace} initialConversationId="conv-101" />
          </ModalProvider>
        </ToastProvider>
      );
    });

    expect(hookResult?.activeConversation?.id).toBe('conv-101');
    expect(hookResult?.conversations.length).toBe(1);
    expect(hookResult?.treeData?.children?.length).toBe(1);
    expect(signalRService.joinConversation).toHaveBeenCalledWith('conv-101');
  });

  it('handles sending prompt, updating local messages, and aborting stream', async () => {
    setupApiMocks();
    let hookResult: ReturnType<typeof useWorkspaceStudio> | undefined;

    const TestHarness: React.FC<UseWorkspaceStudioProps> = (props) => {
      hookResult = useWorkspaceStudio(props);
      return (
        <div>
          <button id="sendBtn" onClick={() => hookResult?.handleSendPrompt('New user prompt')}>
            Send
          </button>
          <button id="abortBtn" onClick={() => hookResult?.handleAbort()}>
            Abort
          </button>
        </div>
      );
    };

    await act(async () => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <ModalProvider>
            <TestHarness workspace={mockWorkspace} initialConversationId="conv-101" />
          </ModalProvider>
        </ToastProvider>
      );
    });

    const sendBtn = container.querySelector('#sendBtn') as HTMLButtonElement;
    await act(async () => {
      sendBtn.click();
    });

    expect(hookResult?.isStreaming).toBe(true);
    expect(hookResult?.activeConversation?.messages.some((m) => m.content === 'New user prompt')).toBe(true);

    const abortBtn = container.querySelector('#abortBtn') as HTMLButtonElement;
    await act(async () => {
      abortBtn.click();
    });

    expect(hookResult?.isStreaming).toBe(false);
  });

  it('handles model and reasoning effort adjustments', async () => {
    setupApiMocks();
    let hookResult: ReturnType<typeof useWorkspaceStudio> | undefined;

    const TestHarness: React.FC<UseWorkspaceStudioProps> = (props) => {
      hookResult = useWorkspaceStudio(props);
      return (
        <div>
          <button id="modelBtn" onClick={() => hookResult?.handleModelChange('claude-3-opus')}>
            Set Model
          </button>
          <button id="effortBtn" onClick={() => hookResult?.handleEffortChange('high')}>
            Set Effort
          </button>
        </div>
      );
    };

    await act(async () => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <ModalProvider>
            <TestHarness workspace={mockWorkspace} initialConversationId="conv-101" />
          </ModalProvider>
        </ToastProvider>
      );
    });

    await act(async () => {
      (container.querySelector('#modelBtn') as HTMLButtonElement).click();
    });
    expect(hookResult?.activeConversation?.modelId).toBe('claude-3-opus');

    await act(async () => {
      (container.querySelector('#effortBtn') as HTMLButtonElement).click();
    });
    expect(hookResult?.activeConversation?.effort).toBe('high');
  });

  it('handles pinning conversation and diffs accept-all and reject-all', async () => {
    setupApiMocks();
    let hookResult: ReturnType<typeof useWorkspaceStudio> | undefined;

    const TestHarness: React.FC<UseWorkspaceStudioProps> = (props) => {
      hookResult = useWorkspaceStudio(props);
      return (
        <div>
          <button id="pinBtn" onClick={() => hookResult?.handleTogglePin('conv-101', true)}>
            Pin
          </button>
          <button id="acceptBtn" onClick={() => hookResult?.handleAcceptAllChanges()}>
            Accept All
          </button>
          <button id="rejectBtn" onClick={() => hookResult?.handleRejectAllChanges()}>
            Reject All
          </button>
        </div>
      );
    };

    await act(async () => {
      root = createRoot(container);
      root.render(
        <ToastProvider>
          <ModalProvider>
            <TestHarness workspace={mockWorkspace} initialConversationId="conv-101" />
          </ModalProvider>
        </ToastProvider>
      );
    });

    await act(async () => {
      (container.querySelector('#pinBtn') as HTMLButtonElement).click();
    });
    expect(hookResult?.activeConversation?.isPinned).toBe(true);

    await act(async () => {
      (container.querySelector('#acceptBtn') as HTMLButtonElement).click();
    });
    expect(hookResult?.isChangesOverviewOpen).toBe(false);

    await act(async () => {
      (container.querySelector('#rejectBtn') as HTMLButtonElement).click();
    });
    expect(hookResult?.isChangesOverviewOpen).toBe(false);
  });
});
