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

export const App: React.FC = () => {
  const { isSetupCompleted, isAuthenticated, isLoading } = useAuth();
  const { showModal, hideModal } = useModal();
  const [activeTab, setActiveTab] = useState<NavTab>('dashboard');
  const [targetWorkspaceId, setTargetWorkspaceId] = useState<string | null>(null);

  // Parse path on initial load & popstate
  const handleUrlRoute = () => {
    const path = window.location.pathname;
    const parts = path.split('/').filter(Boolean);

    if (parts.length === 0 || parts[0] === 'dashboard') {
      setActiveTab('dashboard');
      setTargetWorkspaceId(null);
    } else if (parts[0] === 'workspaces') {
      setActiveTab('workspaces');
      if (parts.length >= 2) {
        setTargetWorkspaceId(parts[1]);
      } else {
        setTargetWorkspaceId(null);
      }
    } else if (parts[0] === 'providers') {
      setActiveTab('providers');
      setTargetWorkspaceId(null);
    } else if (parts[0] === 'tools' || parts[0] === 'mcps') {
      setActiveTab('tools');
      setTargetWorkspaceId(null);
    } else if (parts[0] === 'settings') {
      setActiveTab('settings');
      setTargetWorkspaceId(null);
    }
  };

  useEffect(() => {
    handleUrlRoute();
    window.addEventListener('popstate', handleUrlRoute);
    return () => window.removeEventListener('popstate', handleUrlRoute);
  }, []);

  // Initialize SignalR when authenticated
  useEffect(() => {
    if (isAuthenticated) {
      signalRService.start();
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
