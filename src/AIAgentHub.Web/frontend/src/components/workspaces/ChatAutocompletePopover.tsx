import React, { useEffect, useRef } from 'react';
import { SkillDto } from '../../types/settings';

export interface AutocompleteItem {
  id: string;
  label: string;
  detail?: string;
  type: 'skill' | 'file';
}

interface ChatAutocompletePopoverProps {
  mode: 'skill' | 'file';
  query: string;
  skills: SkillDto[];
  files: string[];
  selectedIndex: number;
  onSelect: (item: AutocompleteItem) => void;
  onClose: () => void;
}

export const ChatAutocompletePopover: React.FC<ChatAutocompletePopoverProps> = ({
  mode,
  query,
  skills,
  files,
  selectedIndex,
  onSelect,
  onClose,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);

  const cleanQuery = query.toLowerCase();

  const items: AutocompleteItem[] =
    mode === 'skill'
      ? skills
          .filter((s) => s.isEnabled !== false && s.name.toLowerCase().includes(cleanQuery))
          .slice(0, 8)
          .map((s) => ({
            id: s.id,
            label: `/${s.name}`,
            detail: s.description,
            type: 'skill',
          }))
      : files
          .filter((f) => f.toLowerCase().includes(cleanQuery))
          .slice(0, 10)
          .map((f) => ({
            id: f,
            label: `@${f}`,
            type: 'file',
          }));

  // Scroll active item into view
  useEffect(() => {
    if (containerRef.current) {
      const activeEl = containerRef.current.children[selectedIndex] as HTMLElement;
      if (activeEl) {
        activeEl.scrollIntoView({ block: 'nearest' });
      }
    }
  }, [selectedIndex]);

  if (items.length === 0) {
    return null;
  }

  return (
    <div
      ref={containerRef}
      className="chat-autocomplete-popover glass"
      style={{
        position: 'absolute',
        bottom: '100%',
        left: '12px',
        right: '12px',
        marginBottom: '6px',
        maxHeight: '220px',
        overflowY: 'auto',
        background: 'var(--bg-secondary)',
        border: '1px solid var(--border-color)',
        borderRadius: '8px',
        boxShadow: 'var(--shadow-card)',
        zIndex: 50,
        padding: '4px',
      }}
    >
      <div
        style={{
          fontSize: '0.72rem',
          textTransform: 'uppercase',
          letterSpacing: '0.5px',
          color: 'var(--text-muted)',
          padding: '4px 8px 6px 8px',
          fontWeight: 600,
          borderBottom: '1px solid var(--border-color)',
          marginBottom: '4px',
          display: 'flex',
          justifyContent: 'space-between',
        }}
      >
        <span>{mode === 'skill' ? '🧩 Skills (/)' : '📄 Workspace Files (@)'}</span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{ fontSize: '0.68rem', opacity: 0.7 }}>↑↓ Navigate • Enter Select</span>
          <span
            onClick={onClose}
            title="Dismiss"
            style={{ cursor: 'pointer', opacity: 0.7, padding: '0 2px' }}
          >
            ✕
          </span>
        </div>
      </div>

      {items.map((item, index) => {
        const isSelected = index === selectedIndex;
        return (
          <div
            key={item.id}
            onClick={() => onSelect(item)}
            style={{
              padding: '6px 10px',
              borderRadius: '6px',
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              gap: '2px',
              background: isSelected ? 'var(--bg-glass)' : 'transparent',
              color: isSelected ? 'var(--text-heading)' : 'var(--text-main)',
              borderLeft: isSelected ? '3px solid var(--accent-primary)' : '3px solid transparent',
              transition: 'background 0.1s',
            }}
            onMouseEnter={() => {
              // Hover does not change selectedIndex unless clicked
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px', fontWeight: isSelected ? 600 : 500, fontSize: '0.88rem' }}>
              <span>{item.type === 'skill' ? '⚡' : '📄'}</span>
              <span>{item.label}</span>
            </div>
            {item.detail && (
              <div
                style={{
                  fontSize: '0.75rem',
                  color: 'var(--text-muted)',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  whiteSpace: 'nowrap',
                  paddingLeft: '22px',
                }}
              >
                {item.detail}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};
