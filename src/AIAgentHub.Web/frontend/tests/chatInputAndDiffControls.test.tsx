import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { ChatInputBar } from '../src/components/workspaces/ChatInputBar';
import { DiffControlsBar } from '../src/components/modals/diff/DiffControlsBar';
import { StudioHeader } from '../src/components/workspaces/studio/StudioHeader';
import { ToastProvider } from '../src/context/ToastContext';
import { FileChangeDto, DiffChangeType } from '../src/types/diff';
import { WorkspaceDto } from '../src/types/workspace';
import { ConversationDetailDto } from '../src/types/conversation';
import { ChangeHunk } from '../src/components/modals/diff/diffViewerUtils';

describe('ChatInputBar & DiffControlsBar & StudioHeader', () => {
  describe('ChatInputBar', () => {
    it('renders Send button and hides Abort button when isStreaming is false', () => {
      const html = renderToString(
        <ChatInputBar
          onSend={vi.fn()}
          isStreaming={false}
          onAbort={vi.fn()}
        />
      );

      expect(html).toContain('id="sendPromptBtn"');
      expect(html).not.toContain('id="abortBtn"');
    });

    it('renders Abort button and hides Send button when isStreaming is true', () => {
      const html = renderToString(
        <ChatInputBar
          onSend={vi.fn()}
          isStreaming={true}
          onAbort={vi.fn()}
        />
      );

      expect(html).toContain('id="abortBtn"');
      expect(html).not.toContain('id="sendPromptBtn"');
    });
  });

  describe('DiffControlsBar', () => {
    const sampleDiff: FileChangeDto = {
      id: 'diff-1',
      conversationId: 'conv-1',
      relativePath: 'src/app.ts',
      changeType: DiffChangeType.Modified,
      oldContent: 'const a = 1;',
      newContent: 'const a = 2;',
      additionsCount: 1,
      deletionsCount: 1,
      isBinary: false,
    };

    const sampleHunks: ChangeHunk[] = [
      { id: 1, startIndex: 0, endIndex: 1 },
      { id: 2, startIndex: 5, endIndex: 7 },
      { id: 3, startIndex: 10, endIndex: 11 },
    ];

    it('renders 1 / 3 directly instead of 3 diffs when activeHunkIndex is -1', () => {
      const html = renderToString(
        <DiffControlsBar
          activeDiff={sampleDiff}
          changes={[sampleDiff]}
          currentFileIndex={0}
          isFullscreen={false}
          isEditing={false}
          changeHunks={sampleHunks}
          activeHunkIndex={-1}
          isWordWrap={false}
          viewMode="sideBySide"
          sideBySideMobileTab="split"
          onPrevFile={vi.fn()}
          onNextFile={vi.fn()}
          onSelectChange={vi.fn()}
          onPrevChange={vi.fn()}
          onNextChange={vi.fn()}
          onToggleWordWrap={vi.fn()}
          onSetViewMode={vi.fn()}
          onSetFullscreen={vi.fn()}
          onSetSideBySideMobileTab={vi.fn()}
        />
      );

      expect(html).toContain('1 / 3');
      expect(html).not.toContain('3 diffs');
      expect(html).toContain('app.ts');
      expect(html).toContain('src/');
    });

    it('renders 2 / 3 when activeHunkIndex is 1', () => {
      const html = renderToString(
        <DiffControlsBar
          activeDiff={sampleDiff}
          changes={[sampleDiff]}
          currentFileIndex={0}
          isFullscreen={false}
          isEditing={false}
          changeHunks={sampleHunks}
          activeHunkIndex={1}
          isWordWrap={false}
          viewMode="sideBySide"
          sideBySideMobileTab="split"
          onPrevFile={vi.fn()}
          onNextFile={vi.fn()}
          onSelectChange={vi.fn()}
          onPrevChange={vi.fn()}
          onNextChange={vi.fn()}
          onToggleWordWrap={vi.fn()}
          onSetViewMode={vi.fn()}
          onSetFullscreen={vi.fn()}
          onSetSideBySideMobileTab={vi.fn()}
        />
      );

      expect(html).toContain('2 / 3');
    });
  });

  describe('StudioHeader', () => {
    const sampleWorkspace: WorkspaceDto = {
      id: 'ws-1',
      name: 'Test WS',
      path: 'C:\\test\\ws',
      createdAtUtc: '2026-08-27T00:00:00Z',
      updatedAtUtc: '2026-08-27T00:00:00Z',
      conversationCount: 1,
    };

    const sampleConversation: ConversationDetailDto = {
      id: 'conv-1',
      workspaceId: 'ws-1',
      title: 'Fix issue',
      providerId: 'antigravity',
      createdAtUtc: '2026-08-27T00:00:00Z',
      updatedAtUtc: '2026-08-27T00:00:00Z',
      messages: [],
    };

    it('disables provider switching badge when isStreaming is true', () => {
      const html = renderToString(
        <ToastProvider>
          <StudioHeader
            workspace={sampleWorkspace}
            activeConversation={sampleConversation}
            models={[]}
            showActionsMenu={false}
            isStreaming={true}
            onBack={vi.fn()}
            onModelChange={vi.fn()}
            onToggleActionsMenu={vi.fn()}
            onCloseActionsMenu={vi.fn()}
            onNewConversation={vi.fn()}
            onOpenDiffs={vi.fn()}
            onDownloadZip={vi.fn()}
            onSwitchProvider={vi.fn()}
            onEffortChange={vi.fn()}
            onDeleteConversation={vi.fn()}
          />
        </ToastProvider>
      );

      expect(html).toContain('disabled=""');
      expect(html).toContain('Cannot switch provider while command is running');
    });
  });
});
