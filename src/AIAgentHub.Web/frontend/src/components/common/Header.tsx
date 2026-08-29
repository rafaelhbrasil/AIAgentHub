import React, { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useTheme } from '../../context/ThemeContext';
import { apiFetch } from '../../services/apiClient';

export type NavTab = 'dashboard' | 'workspaces' | 'providers' | 'tools' | 'settings';

interface HeaderProps {
  activeTab: NavTab;
  onNavigate: (tab: NavTab) => void;
}

interface SystemVersionInfo {
  version: string;
  informationalVersion?: string;
  isDevelopment?: boolean;
  environment?: string;
}

const BASE_APP_VERSION = typeof __APP_VERSION__ !== 'undefined' ? __APP_VERSION__ : 'v0.1.0';

export const Header: React.FC<HeaderProps> = ({ activeTab, onNavigate }) => {
  const { isAuthenticated, username, logout } = useAuth();
  const { theme, setTheme } = useTheme();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [versionText, setVersionText] = useState<string>(BASE_APP_VERSION);
  const [versionTooltip, setVersionTooltip] = useState<string>('AI Agent Hub');

  useEffect(() => {
    apiFetch<SystemVersionInfo>('/api/v1/system/version')
      .then((res) => {
        if (res.ok && res.data?.version) {
          const v = res.data.version;
          setVersionText(`v${v}`);
          if (res.data.informationalVersion) {
            setVersionTooltip(`Version ${res.data.informationalVersion} (${res.data.environment || 'Production'})`);
          }
        }
      })
      .catch(() => {
        // Keep baseline version
      });
  }, []);

  const handleTabClick = (tab: NavTab) => {
    onNavigate(tab);
    setMobileMenuOpen(false);
  };

  return (
    <>
      <header className="app-header">
        <div className="header-left">
          <div className="logo-badge" onClick={() => handleTabClick('dashboard')}>
            <span className="logo-icon">⚡</span>
            <div className="logo-text">
              <span className="logo-title">AI Agent Hub</span>
              <span className="logo-version" title={versionTooltip}>{versionText}</span>
            </div>
          </div>

          {isAuthenticated && (
            <nav className="nav-links desktop-nav" id="mainNav">
              <button
                className={`nav-btn ${activeTab === 'dashboard' ? 'active' : ''}`}
                onClick={() => handleTabClick('dashboard')}
                data-tab="dashboard"
              >
                Dashboard
              </button>
              <button
                className={`nav-btn ${activeTab === 'workspaces' ? 'active' : ''}`}
                onClick={() => handleTabClick('workspaces')}
                data-tab="workspaces"
              >
                Workspaces
              </button>
              <button
                className={`nav-btn ${activeTab === 'providers' ? 'active' : ''}`}
                onClick={() => handleTabClick('providers')}
                data-tab="providers"
              >
                Providers
              </button>
              <button
                className={`nav-btn ${activeTab === 'tools' ? 'active' : ''}`}
                onClick={() => handleTabClick('tools')}
                data-tab="tools"
              >
                MCPs & Skills
              </button>
              <button
                className={`nav-btn ${activeTab === 'settings' ? 'active' : ''}`}
                onClick={() => handleTabClick('settings')}
                data-tab="settings"
              >
                Settings
              </button>
            </nav>
          )}
        </div>

        <div className="header-right">
          <button
            type="button"
            className="icon-btn theme-toggle-btn"
            title={`Theme: ${theme.charAt(0).toUpperCase() + theme.slice(1)} (Click to change)`}
            aria-label="Toggle theme mode"
            onClick={() => {
              const next = theme === 'dark' ? 'light' : theme === 'light' ? 'system' : 'dark';
              setTheme(next);
            }}
          >
            {theme === 'dark' ? (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
              </svg>
            ) : theme === 'light' ? (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="5"></circle>
                <line x1="12" y1="1" x2="12" y2="3"></line>
                <line x1="12" y1="21" x2="12" y2="23"></line>
                <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                <line x1="1" y1="12" x2="3" y2="12"></line>
                <line x1="21" y1="12" x2="23" y2="12"></line>
                <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
              </svg>
            ) : (
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="2" y="3" width="20" height="14" rx="2" ry="2"></rect>
                <line x1="8" y1="21" x2="16" y2="21"></line>
                <line x1="12" y1="17" x2="12" y2="21"></line>
              </svg>
            )}
          </button>

          <div className="status-indicator online" id="serverStatus" title="Server online on HTTPS port 5432">
            <span className="status-dot"></span>
            <span className="status-label">HTTPS :5432</span>
          </div>

          {isAuthenticated && (
            <div className="user-menu" id="userMenu">
              <span className="user-avatar" id="userAvatar" title={username || 'admin'}>
                {username ? username.charAt(0).toUpperCase() : 'A'}
              </span>
              <span className="user-name" id="userNameLabel">
                {username || 'admin'}
              </span>
              <button
                className="icon-btn desktop-only"
                id="logoutBtn"
                title="Sign Out"
                aria-label="Sign Out"
                onClick={logout}
              >
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
                  <polyline points="16 17 21 12 16 7"></polyline>
                  <line x1="21" y1="12" x2="9" y2="12"></line>
                </svg>
              </button>
              <button
                type="button"
                className="icon-btn burger-menu-btn"
                aria-label="Toggle mobile menu"
                onClick={() => setMobileMenuOpen((prev) => !prev)}
              >
                {mobileMenuOpen ? '✕' : '☰'}
              </button>
            </div>
          )}
        </div>
      </header>

      {/* Mobile Drawer Dropdown */}
      {mobileMenuOpen && isAuthenticated && (
        <>
          <div className="mobile-nav-backdrop" onClick={() => setMobileMenuOpen(false)}></div>
          <nav className="mobile-nav-drawer glass">
            <button
              className={`mobile-nav-btn ${activeTab === 'dashboard' ? 'active' : ''}`}
              onClick={() => handleTabClick('dashboard')}
            >
              📊 Dashboard
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'workspaces' ? 'active' : ''}`}
              onClick={() => handleTabClick('workspaces')}
            >
              📁 Workspaces
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'providers' ? 'active' : ''}`}
              onClick={() => handleTabClick('providers')}
            >
              ⚡ Providers
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'tools' ? 'active' : ''}`}
              onClick={() => handleTabClick('tools')}
            >
              🧩 MCPs & Skills
            </button>
            <button
              className={`mobile-nav-btn ${activeTab === 'settings' ? 'active' : ''}`}
              onClick={() => handleTabClick('settings')}
            >
              ⚙️ Settings
            </button>
            <div style={{ borderTop: '1px solid var(--border-color)', margin: '8px 0' }}></div>
            <button
              className="mobile-nav-btn"
              style={{ color: '#ef4444' }}
              onClick={() => {
                setMobileMenuOpen(false);
                logout();
              }}
            >
              🚪 Sign Out ({username || 'admin'})
            </button>
          </nav>
        </>
      )}
    </>
  );
};
