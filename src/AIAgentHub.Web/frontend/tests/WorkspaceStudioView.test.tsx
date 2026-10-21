import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { WorkspaceStudioView } from '../src/components/workspaces/WorkspaceStudioView';
import { WorkspaceDto } from '../src/types/workspace';
import { ToastProvider } from '../src/context/ToastContext';
import { ModalProvider } from '../src/context/ModalContext';
import * as studioHook from '../src/components/workspaces/studio/useWorkspaceStudio';

describe('WorkspaceStudioView', () => {
  const mockWorkspace: WorkspaceDto = {
    id: 'ws-test-123',
    name: 'Test Workspace',
    path: 'D:/Code/TestWS',
    conversationCount: 2,
    createdAtUtc: '2026-10-18T00:00:00Z',
    updatedAtUtc: '2026-10-18T00:00:00Z',
  };

  const defaultStudioState: ReturnType<typeof studioHook.useWorkspaceStudio> = {
    treeData: {
      name: 'root',
      fullPath: 'D:/Code/TestWS',
      relativePath: '',
      isDirectory: true,
      children: [
        { name: 'file1.ts', fullPath: 'D:/Code/TestWS/file1.ts', relativePath: 'file1.ts', isDirectory: false },
        {
          name: 'sub',
          fullPath: 'D:/Code/TestWS/sub',
          relativePath: 'sub',
          isDirectory: true,
          children: [
            { name: 'file2.ts', fullPath: 'D:/Code/TestWS/sub/file2.ts', relativePath: 'sub/file2.ts', isDirectory: false },
          ],
        },
      ],
    },
    conversations: [
      {
        id: 'conv-1',
        workspaceId: mockWorkspace.id,
        title: 'Conversation 1',
        provider: 'gemini',
        model: 'gemini-1.5-pro',
        createdAt: '2026-10-18T00:00:00Z',
        updatedAt: '2026-10-18T00:00:00Z',
        messages: [],
      } as any,
    ],
    activeConversation: {
      id: 'conv-1',
      workspaceId: mockWorkspace.id,
      title: 'Conversation 1',
      provider: 'gemini',
      model: 'gemini-1.5-pro',
      createdAt: '2026-10-18T00:00:00Z',
      updatedAt: '2026-10-18T00:00:00Z',
      messages: [],
    } as any,
    fileChanges: [],
    isChangesOverviewOpen: false,
    setIsChangesOverviewOpen: vi.fn(),
    models: [{ id: 'gemini-1.5-pro', displayName: 'Gemini 1.5 Pro' }],
    streamingContent: '',
    heartbeatMessages: [],
    isStreaming: false,
    isRefreshingTree: false,
    mobileTab: 'chat',
    setMobileTab: vi.fn(),
    showActionsMenu: false,
    setShowActionsMenu: vi.fn(),
    fetchFileTree: vi.fn(),
    fetchFileChanges: vi.fn(),
    fetchWorkspaceData: vi.fn(),
    selectConversationById: vi.fn(),
    handleCreateConversation: vi.fn(),
    handleDeleteConversation: vi.fn(),
    handleTogglePin: vi.fn(),
    handleOpenSwitchProvider: vi.fn(),
    handlePreviewFile: vi.fn(),
    handleOpenDiffs: vi.fn(),
    handleAcceptAllChanges: vi.fn(),
    handleRejectAllChanges: vi.fn(),
    handleModelChange: vi.fn(),
    handleEffortChange: vi.fn(),
    handleAbort: vi.fn(),
    handleSendPrompt: vi.fn(),
    handleDownloadZip: vi.fn(),
    isGitOpen: false,
    setIsGitOpen: vi.fn(),
    fetchGitStatus: vi.fn(),
    gitStatus: {
      isGitInstalled: true,
      isRepository: true,
      currentBranch: 'main',
      ahead: 0,
      behind: 0,
      staged: [],
      modified: [],
      untracked: [],
    },
    handleOpenEditor: vi.fn(),
  };

  beforeEach(() => {
    vi.restoreAllMocks();
    window.HTMLElement.prototype.scrollIntoView = vi.fn();
  });

  it('renders StudioHeader and mobile switcher tabs and switches studio mode', async () => {
    vi.spyOn(studioHook, 'useWorkspaceStudio').mockReturnValue(defaultStudioState);

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(
        <ToastProvider>
          <ModalProvider>
            <WorkspaceStudioView workspace={mockWorkspace} onBack={vi.fn()} />
          </ModalProvider>
        </ToastProvider>
      );
    });

    expect(container.querySelector('.studio-root')).not.toBeNull();
    expect(container.querySelector('.studio-mobile-nav')).not.toBeNull();

    // Verify mobile switcher buttons
    const chatBtn = container.querySelectorAll('.studio-mobile-nav button')[0] as HTMLButtonElement;
    const filesBtn = container.querySelectorAll('.studio-mobile-nav button')[1] as HTMLButtonElement;
    const gitBtn = container.querySelectorAll('.studio-mobile-nav button')[2] as HTMLButtonElement;

    expect(chatBtn.textContent).toContain('Chat');
    expect(filesBtn.textContent).toContain('Files');
    expect(gitBtn.textContent).toContain('Git');

    await act(async () => {
      gitBtn.click();
    });
    expect(defaultStudioState.setMobileTab).toHaveBeenCalledWith('git');

    await act(async () => {
      filesBtn.click();
    });
    expect(defaultStudioState.setMobileTab).toHaveBeenCalledWith('sidebar');

    await act(async () => {
      chatBtn.click();
    });
    expect(defaultStudioState.setMobileTab).toHaveBeenCalledWith('chat');

    act(() => {
      root.unmount();
    });
    container.remove();
  });
});
