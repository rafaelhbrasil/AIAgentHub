import React, { useState, useMemo } from 'react';
import { WorkspaceDto, FileTreeNode } from '../../types/workspace';
import { ChatMessageList } from './ChatMessageList';
import { ChatInputBar } from './ChatInputBar';
import { ChangesOverviewBar } from './ChangesOverviewBar';
import { useWorkspaceStudio } from './studio/useWorkspaceStudio';
import { StudioHeader } from './studio/StudioHeader';
import { StudioSidebar } from './studio/StudioSidebar';
import { GitWorkbench } from './studio/git/GitWorkbench';
import { StudioEmptyState } from './studio/StudioEmptyState';
import { ConversationStatus } from '../../types/conversation';
import { GitBranchIcon } from '../common/Icons';

interface WorkspaceStudioViewProps {
  workspace: WorkspaceDto;
  initialConversationId?: string | null;
  onConversationChanged?: (conversationId: string | null) => void;
  onBack: () => void;
  onRemoveWorkspace?: (workspace: WorkspaceDto) => void;
}

function extractFilePaths(node: FileTreeNode | null): string[] {
  if (!node) return [];
  const results: string[] = [];
  const traverse = (current: FileTreeNode) => {
    if (!current.isDirectory && current.relativePath) {
      results.push(current.relativePath);
    }
    if (current.children) {
      for (const child of current.children) {
        traverse(child);
      }
    }
  };
  traverse(node);
  return results;
}

export const WorkspaceStudioView: React.FC<WorkspaceStudioViewProps> = ({
  workspace,
  initialConversationId,
  onConversationChanged,
  onBack,
  onRemoveWorkspace,
}) => {
  const {
    treeData,
    conversations,
    activeConversation,
    fileChanges,
    isChangesOverviewOpen,
    setIsChangesOverviewOpen,
    models,
    streamingContent,
    heartbeatMessages,
    isStreaming,
    isRefreshingTree,
    mobileTab,
    setMobileTab,
    showActionsMenu,
    setShowActionsMenu,
    fetchFileTree,
    selectConversationById,
    handleCreateConversation,
    handleDeleteConversation,
    handleTogglePin,
    handleOpenSwitchProvider,
    handlePreviewFile,
    handleOpenDiffs,
    handleAcceptAllChanges,
    handleRejectAllChanges,
    handleModelChange,
    handleEffortChange,
    handleAbort,
    handleSendPrompt,
    handleDownloadZip,
    gitStatus,
    handleOpenEditor,
  } = useWorkspaceStudio({
    workspace,
    initialConversationId,
    onConversationChanged,
    onRemoveWorkspace,
  });

  const [studioMode, setStudioMode] = useState<'chat' | 'git'>('chat');
  const workspaceFiles = useMemo(() => extractFilePaths(treeData), [treeData]);
  const isSwitching = activeConversation?.status === ConversationStatus.SwitchingProvider || activeConversation?.status === 1;

  return (
    <div className="studio-root">
      {/* Consolidated Compact Header */}
      <StudioHeader
        workspace={workspace}
        activeConversation={activeConversation}
        models={models}
        showActionsMenu={showActionsMenu}
        isStreaming={isStreaming}
        gitStatus={gitStatus}
        studioMode={studioMode}
        onSwitchStudioMode={(mode) => setStudioMode(mode)}
        onBack={onBack}
        onModelChange={handleModelChange}
        onToggleActionsMenu={() => setShowActionsMenu((prev) => !prev)}
        onCloseActionsMenu={() => setShowActionsMenu(false)}
        onNewConversation={handleCreateConversation}
        onOpenDiffs={() => handleOpenDiffs()}
        onDownloadZip={handleDownloadZip}
        onSwitchProvider={handleOpenSwitchProvider}
        onEffortChange={handleEffortChange}
        onDeleteConversation={handleDeleteConversation}
        onToggleGit={() => {
          if (mobileTab === 'git') {
            setMobileTab('chat');
            setStudioMode('chat');
          } else {
            const nextMode = studioMode === 'git' ? 'chat' : 'git';
            setStudioMode(nextMode);
            if (nextMode === 'git') setMobileTab('git');
          }
        }}
      />

      {/* Mobile Switcher Tab (Chats vs Files vs Git) */}
      <div className="studio-mobile-nav">
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'chat' && studioMode === 'chat' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => {
            setMobileTab('chat');
            setStudioMode('chat');
          }}
        >
          💬 Chat
        </button>
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'sidebar' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => setMobileTab('sidebar')}
        >
          📁 Files ({conversations.length})
        </button>
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'git' || studioMode === 'git' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center', display: 'inline-flex', alignItems: 'center', gap: '5px' }}
          onClick={() => {
            setMobileTab('git');
            setStudioMode('git');
          }}
        >
          <GitBranchIcon size={13} /> Git {gitStatus?.currentBranch ? `(${gitStatus.currentBranch})` : ''}
        </button>
      </div>

      <div className="studio-layout">
        {/* Left Panel: Explorer & Conversations */}
        <StudioSidebar
          mobileTab={mobileTab}
          workspaceId={workspace.id}
          treeData={treeData}
          conversations={conversations}
          activeConversation={activeConversation}
          isRefreshingTree={isRefreshingTree}
          onDownloadZip={handleDownloadZip}
          onRefreshFiles={fetchFileTree}
          onPreviewFile={handlePreviewFile}
          onOpenEditor={handleOpenEditor}
          onCreateConversation={handleCreateConversation}
          onSelectConversation={(id) => {
            selectConversationById(id);
            setMobileTab('chat');
            setStudioMode('chat');
          }}
          onDeleteConversation={handleDeleteConversation}
          onTogglePin={handleTogglePin}
        />

        {/* Right Panel: Conversation Studio OR Git Workbench */}
        {studioMode === 'git' || mobileTab === 'git' ? (
          <div
            className={`git-workbench-container glass ${mobileTab !== 'git' && mobileTab !== 'chat' ? 'mobile-hidden' : ''}`}
            id="gitWorkbenchPanel"
            style={{ flex: 1, height: '100%', overflow: 'hidden' }}
          >
            <GitWorkbench
              workspaceId={workspace.id}
              onFileSelect={handleOpenEditor}
            />
          </div>
        ) : (
          <div
            className={`chat-container glass ${mobileTab !== 'chat' ? 'mobile-hidden' : ''}`}
            id="chatPanel"
          >
            {!activeConversation ? (
              <StudioEmptyState onCreateConversation={handleCreateConversation} />
            ) : (
              <>
                <ChatMessageList
                  messages={activeConversation.messages || []}
                  providerId={activeConversation.providerId}
                  streamingContent={streamingContent}
                  isStreaming={isStreaming}
                  heartbeatMessages={heartbeatMessages}
                  status={activeConversation.status}
                />

                <div className="chat-bottom-dock">
                  <ChangesOverviewBar
                    fileChanges={fileChanges}
                    isOpen={isChangesOverviewOpen}
                    onToggleOpen={() => setIsChangesOverviewOpen((prev) => !prev)}
                    onSelectFile={(fileChangeId) => handleOpenDiffs(fileChangeId)}
                    onOpenFullDiff={() => handleOpenDiffs()}
                    onAcceptAll={handleAcceptAllChanges}
                    onRejectAll={handleRejectAllChanges}
                  />

                  <ChatInputBar
                    onSend={handleSendPrompt}
                    disabled={isSwitching}
                    isStreaming={isStreaming}
                    onAbort={handleAbort}
                    workspaceFiles={workspaceFiles}
                  />
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
