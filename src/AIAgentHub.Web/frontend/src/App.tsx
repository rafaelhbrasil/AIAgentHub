import React, { useState, useEffect } from 'react';
import { useAuth } from './context/AuthContext';
import { useModal } from './context/ModalContext';
import { signalRService } from './services/signalrService';
import { Header, NavTab } from './components/common/Header';
import { ToastContainer } from './components/common/ToastContainer';
import { Modal } from './components/common/Modal';
import { LoadingOverlay } from './components/common/LoadingOverlay';
import { SignInPage } from './components/auth/SignInPage';
import { SetupWizardModal } from './components/auth/SetupWizardModal';
import { DashboardView } from './components/dashboard/DashboardView';
import { WorkspacesView } from './components/workspaces/WorkspacesView';
import { ProvidersView } from './components/providers/ProvidersView';
import { ToolsView } from './components/tools/ToolsView';
import { SettingsView } from './components/settings/SettingsView';

const parseUrlRoute = (): { tab: NavTab; workspaceId: string | null } => {
  const path = window.location.pathname;
  const parts = path.split('/').filter(Boolean);

  if (parts.length === 0 || parts[0] === 'dashboard') {
    return { tab: 'dashboard', workspaceId: null };
  } else if (parts[0] === 'workspaces') {
    return { tab: 'workspaces', workspaceId: parts.length >= 2 ? parts[1] : null };
  } else if (parts[0] === 'providers') {
    return { tab: 'providers', workspaceId: null };
  } else if (parts[0] === 'tools' || parts[0] === 'mcps') {
    return { tab: 'tools', workspaceId: null };
  } else if (parts[0] === 'settings') {
    return { tab: 'settings', workspaceId: null };
  }
  return { tab: 'dashboard', workspaceId: null };
};

export const App: React.FC = () => {
  const { isSetupCompleted, isAuthenticated, isLoading } = useAuth();
  const { showModal, hideModal } = useModal();
  const [activeTab, setActiveTab] = useState<NavTab>(() => parseUrlRoute().tab);
  const [targetWorkspaceId, setTargetWorkspaceId] = useState<string | null>(() => parseUrlRoute().workspaceId);

  // Parse path on popstate
  const handleUrlRoute = () => {
    const route = parseUrlRoute();
    setActiveTab(route.tab);
    setTargetWorkspaceId(route.workspaceId);
  };

  useEffect(() => {
    window.addEventListener('popstate', handleUrlRoute);
    return () => window.removeEventListener('popstate', handleUrlRoute);
  }, []);

  // Initialize SignalR when authenticated
  useEffect(() => {
    if (isAuthenticated) {
      signalRService.start();
      return () => {
        signalRService.stop();
      };
    } else {
      signalRService.stop();
    }
  }, [isAuthenticated]);

  // Show setup wizard if not completed
  useEffect(() => {
    if (!isLoading && !isSetupCompleted) {
      showModal(
        'Initial Server Setup — Setup Mode',
        <SetupWizardModal onComplete={hideModal} />
      );
    }
  }, [isLoading, isSetupCompleted, showModal, hideModal]);

  const navigateTo = (tab: NavTab, subPath?: string) => {
    setActiveTab(tab);
    let path = `/${tab}`;
    if (tab === 'dashboard') path = '/';
    if (subPath) path = `/${tab}/${subPath}`;

    window.history.pushState({ path }, '', path);
  };

  const handleOpenWorkspace = (workspaceId: string) => {
    setTargetWorkspaceId(workspaceId);
    navigateTo('workspaces', workspaceId);
  };

  if (isLoading) {
    return <LoadingOverlay isVisible={true} text="Initializing AI Agent Hub..." />;
  }

  if (!isAuthenticated) {
    return (
      <div className="app-root">
        <Header activeTab={activeTab} onNavigate={navigateTo} />
        <main className="app-main">
          <SignInPage />
        </main>
        <ToastContainer />
        <Modal />
      </div>
    );
  }

  return (
    <div className="app-root">
      <Header activeTab={activeTab} onNavigate={(tab) => navigateTo(tab)} />

      <main className="app-main" id="mainContent">
        {activeTab === 'dashboard' && (
          <DashboardView onOpenWorkspace={handleOpenWorkspace} />
        )}
        {activeTab === 'workspaces' && (
          <WorkspacesView
            initialWorkspaceId={targetWorkspaceId}
            onNavigateToWorkspace={(id) => {
              setTargetWorkspaceId(id);
              window.history.pushState({}, '', `/workspaces/${id}`);
            }}
            onBackToWorkspaces={() => {
              setTargetWorkspaceId(null);
              window.history.pushState({}, '', '/workspaces');
            }}
          />
        )}
        {activeTab === 'providers' && <ProvidersView />}
        {activeTab === 'tools' && <ToolsView />}
        {activeTab === 'settings' && <SettingsView />}
      </main>

      <ToastContainer />
      <Modal />
    </div>
  );
};
export default App;
