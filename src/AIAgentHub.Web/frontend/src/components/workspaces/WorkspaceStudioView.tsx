import React from 'react';
import { WorkspaceDto } from '../../types/workspace';
import { ChatMessageList } from './ChatMessageList';
import { ChatInputBar } from './ChatInputBar';
import { ChangesOverviewBar } from './ChangesOverviewBar';
import { useWorkspaceStudio } from './studio/useWorkspaceStudio';
import { StudioHeader } from './studio/StudioHeader';
import { StudioSidebar } from './studio/StudioSidebar';
import { StudioEmptyState } from './studio/StudioEmptyState';

interface WorkspaceStudioViewProps {
  workspace: WorkspaceDto;
  initialConversationId?: string | null;
  onConversationChanged?: (conversationId: string | null) => void;
  onBack: () => void;
  onRemoveWorkspace?: (workspace: WorkspaceDto) => void;
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
                  disabled={isStreaming}
                  isStreaming={isStreaming}
                  onAbort={handleAbort}
                />
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};
