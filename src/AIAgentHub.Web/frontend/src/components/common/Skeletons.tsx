import React from 'react';

export const DashboardSkeletons: React.FC = () => {
  return (
    <>
      <div className="grid-cols-3">
        <div className="skeleton-card">
          <div className="skeleton skeleton-line skeleton-line-medium"></div>
          <div className="skeleton skeleton-line skeleton-line-short"></div>
          <div className="skeleton skeleton-stat"></div>
        </div>
        <div className="skeleton-card">
          <div className="skeleton skeleton-line skeleton-line-medium"></div>
          <div className="skeleton skeleton-line skeleton-line-short"></div>
          <div className="skeleton skeleton-stat"></div>
        </div>
        <div className="skeleton-card">
          <div className="skeleton skeleton-line skeleton-line-medium"></div>
          <div className="skeleton skeleton-line skeleton-line-short"></div>
          <div className="skeleton skeleton-stat"></div>
        </div>
      </div>
      <div className="skeleton-card" style={{ marginBottom: '24px' }}>
        <div className="skeleton skeleton-line skeleton-line-long"></div>
        <div style={{ marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
          {Array(5)
            .fill('')
            .map((_, i) => (
              <div
                key={i}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '12px 16px',
                  background: 'rgba(0,0,0,0.25)',
                  borderRadius: '6px',
                }}
              >
                <div style={{ flex: 1 }}>
                  <div className="skeleton skeleton-line skeleton-line-medium"></div>
                  <div className="skeleton skeleton-line skeleton-line-short" style={{ marginTop: '6px' }}></div>
                </div>
                <div className="skeleton skeleton-badge" style={{ marginLeft: '12px' }}></div>
              </div>
            ))}
        </div>
      </div>
    </>
  );
};

export const ProviderSkeletons: React.FC = () => {
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <h2>AI Providers</h2>
        <button className="btn btn-secondary" disabled>
          🔄 Refresh All Providers
        </button>
      </div>
      <div className="grid-cols-3">
        {Array(4)
          .fill('')
          .map((_, i) => (
            <div key={i} className="skeleton-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div className="skeleton skeleton-line skeleton-line-medium"></div>
                <div className="skeleton skeleton-badge"></div>
              </div>
              <div className="skeleton skeleton-line skeleton-line-short" style={{ marginTop: '8px' }}></div>
              <div className="skeleton skeleton-line skeleton-line-long" style={{ marginTop: '12px' }}></div>
              <div style={{ display: 'flex', gap: '8px', marginTop: '12px' }}>
                <div className="skeleton skeleton-badge"></div>
              </div>
            </div>
          ))}
      </div>
    </>
  );
};
