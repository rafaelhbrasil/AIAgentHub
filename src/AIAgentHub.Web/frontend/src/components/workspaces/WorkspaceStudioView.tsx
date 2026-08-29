import React, { useMemo } from 'react';
import { WorkspaceDto, FileTreeNode } from '../../types/workspace';
import { ChatMessageList } from './ChatMessageList';
import { ChatInputBar } from './ChatInputBar';
import { ChangesOverviewBar } from './ChangesOverviewBar';
import { useWorkspaceStudio } from './studio/useWorkspaceStudio';
import { StudioHeader } from './studio/StudioHeader';
import { StudioSidebar } from './studio/StudioSidebar';
import { StudioEmptyState } from './studio/StudioEmptyState';
import { ConversationStatus } from '../../types/conversation';

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
  } = useWorkspaceStudio({
    workspace,
    initialConversationId,
    onConversationChanged,
    onRemoveWorkspace,
  });

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
      />

      {/* Mobile Switcher Tab (Chats vs Files) */}
      <div className="studio-mobile-nav">
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'chat' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => setMobileTab('chat')}
        >
          💬 Chat Studio
        </button>
        <button
          type="button"
          className={`btn compact-btn ${mobileTab === 'sidebar' ? 'btn-primary' : 'btn-secondary'}`}
          style={{ flex: 1, justifyContent: 'center' }}
          onClick={() => setMobileTab('sidebar')}
        >
          📁 Files & Chats ({conversations.length})
        </button>
      </div>

      <div className="studio-layout">
        {/* Left Panel: Explorer & Conversations */}
        <StudioSidebar
          mobileTab={mobileTab}
          treeData={treeData}
          conversations={conversations}
          activeConversation={activeConversation}
          isRefreshingTree={isRefreshingTree}
          onDownloadZip={handleDownloadZip}
          onRefreshFiles={fetchFileTree}
          onPreviewFile={handlePreviewFile}
          onCreateConversation={handleCreateConversation}
          onSelectConversation={(id) => {
            selectConversationById(id);
            setMobileTab('chat');
          }}
          onDeleteConversation={handleDeleteConversation}
          onTogglePin={handleTogglePin}
        />

        {/* Right Panel: Conversation Studio */}
        <div
          className={`chat-container glass ${mobileTab === 'sidebar' ? 'mobile-hidden' : ''}`}
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
                  disabled={isStreaming || isSwitching}
                  isStreaming={isStreaming}
                  onAbort={handleAbort}
                  workspaceFiles={workspaceFiles}
                />
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
