import React, { useState, useRef, useEffect, useCallback } from 'react';
import { ChatAutocompletePopover, AutocompleteItem } from './ChatAutocompletePopover';
import { SkillDto } from '../../types/settings';
import { apiFetch } from '../../services/apiClient';

interface ChatInputBarProps {
  onSend: (prompt: string) => void;
  disabled?: boolean;
  isStreaming?: boolean;
  onAbort?: () => void;
  workspaceFiles?: string[];
}

export const ChatInputBar: React.FC<ChatInputBarProps> = ({
  onSend,
  disabled,
  isStreaming,
  onAbort,
  workspaceFiles = [],
}) => {
  const [text, setText] = useState<string>('');
  const [skills, setSkills] = useState<SkillDto[]>([]);
  const [autocompleteState, setAutocompleteState] = useState<{
    isOpen: boolean;
    mode: 'skill' | 'file';
    query: string;
    triggerIndex: number;
    selectedIndex: number;
  }>({
    isOpen: false,
    mode: 'skill',
    query: '',
    triggerIndex: -1,
    selectedIndex: 0,
  });

  const textareaRef = useRef<HTMLTextAreaElement>(null);

  // Fetch skills for autocomplete once
  useEffect(() => {
    apiFetch<SkillDto[]>('/api/v1/skills')
      .then((res) => {
        if (res.ok && res.data) {
          setSkills(res.data);
        }
      })
      .catch(() => {});
  }, []);

  const isMobileDevice = () => {
    return typeof window !== 'undefined' && (window.innerWidth <= 768 || 'ontouchstart' in window);
  };

  const adjustTextareaHeight = () => {
    const textarea = textareaRef.current;
    if (!textarea) return;
    textarea.style.height = 'auto';
    const vh = typeof window !== 'undefined'
      ? (window.visualViewport?.height ?? window.innerHeight)
      : 200;
    const maxHeight = vh * 0.3;
    const newHeight = Math.min(textarea.scrollHeight, maxHeight);
    textarea.style.height = `${Math.max(40, newHeight)}px`;
  };

  useEffect(() => {
    adjustTextareaHeight();
  }, [text]);

  useEffect(() => {
    const vp = window.visualViewport;
    if (!vp) return;
    const handleResize = () => adjustTextareaHeight();
    vp.addEventListener('resize', handleResize);
    return () => vp.removeEventListener('resize', handleResize);
  }, []);

  // Detect autocomplete triggers (/ or @)
  const checkAutocomplete = (currentText: string, cursorPos: number) => {
    const textBeforeCursor = currentText.slice(0, cursorPos);

    // Check for /skill (at start of text or after whitespace)
    const skillMatch = /(?:^|\s)\/([a-zA-Z0-9_-]*)$/.exec(textBeforeCursor);
    if (skillMatch) {
      const matchStart = textBeforeCursor.length - skillMatch[1].length - 1;
      setAutocompleteState({
        isOpen: true,
        mode: 'skill',
        query: skillMatch[1],
        triggerIndex: matchStart,
        selectedIndex: 0,
      });
      return;
    }

    // Check for @file (at start of text or after whitespace)
    const fileMatch = /(?:^|\s)@([^\s]*)$/.exec(textBeforeCursor);
    if (fileMatch) {
      const matchStart = textBeforeCursor.length - fileMatch[1].length - 1;
      setAutocompleteState({
        isOpen: true,
        mode: 'file',
        query: fileMatch[1],
        triggerIndex: matchStart,
        selectedIndex: 0,
      });
      return;
    }

    setAutocompleteState((prev) => (prev.isOpen ? { ...prev, isOpen: false } : prev));
  };

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const newText = e.target.value;
    const cursorPos = e.target.selectionStart || newText.length;
    setText(newText);
    checkAutocomplete(newText, cursorPos);
  };

  const getFilteredItems = useCallback((): AutocompleteItem[] => {
    if (!autocompleteState.isOpen) return [];
    const cleanQuery = autocompleteState.query.toLowerCase();
    if (autocompleteState.mode === 'skill') {
      return skills
        .filter((s) => s.isEnabled !== false && s.name.toLowerCase().includes(cleanQuery))
        .slice(0, 8)
        .map((s) => ({ id: s.id, label: `/${s.name}`, detail: s.description, type: 'skill' }));
    } else {
      return workspaceFiles
        .filter((f) => f.toLowerCase().includes(cleanQuery))
        .slice(0, 10)
        .map((f) => ({ id: f, label: `@${f}`, type: 'file' }));
    }
  }, [autocompleteState, skills, workspaceFiles]);

  const handleSelectAutocomplete = (item: AutocompleteItem) => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const before = text.slice(0, autocompleteState.triggerIndex);
    const cursorPos = textarea.selectionStart || text.length;
    const after = text.slice(cursorPos);

    const replacement = `${item.label} `;
    const updated = before + replacement + after;
    setText(updated);
    setAutocompleteState((prev) => ({ ...prev, isOpen: false }));

    setTimeout(() => {
      textarea.focus();
      const newPos = before.length + replacement.length;
      textarea.setSelectionRange(newPos, newPos);
    }, 10);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (autocompleteState.isOpen) {
      const items = getFilteredItems();
      if (items.length > 0) {
        if (e.key === 'ArrowDown') {
          e.preventDefault();
          setAutocompleteState((prev) => ({
            ...prev,
            selectedIndex: (prev.selectedIndex + 1) % items.length,
          }));
          return;
        }
        if (e.key === 'ArrowUp') {
          e.preventDefault();
          setAutocompleteState((prev) => ({
            ...prev,
            selectedIndex: (prev.selectedIndex - 1 + items.length) % items.length,
          }));
          return;
        }
        if (e.key === 'Tab' || (e.key === 'Enter' && !e.shiftKey)) {
          e.preventDefault();
          const target = items[autocompleteState.selectedIndex] || items[0];
          if (target) {
            handleSelectAutocomplete(target);
          }
          return;
        }
        if (e.key === 'Escape') {
          e.preventDefault();
          setAutocompleteState((prev) => ({ ...prev, isOpen: false }));
          return;
        }
      }
    }

    // On mobile devices, Enter inserts a line break and does NOT send
    if (isMobileDevice()) {
      return;
    }

    // On desktop, Enter sends, Shift+Enter inserts line break
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      if (!isStreaming) {
        handleSubmit();
      }
    }
  };

  const handleSubmit = () => {
    const trimmed = text.trim();
    if (!trimmed || disabled || isStreaming) return;
    onSend(trimmed);
    setText('');
    setAutocompleteState((prev) => ({ ...prev, isOpen: false }));
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
    }
  };

  return (
    <div className="chat-input-bar" style={{ position: 'relative' }}>
      {autocompleteState.isOpen && (
        <ChatAutocompletePopover
          mode={autocompleteState.mode}
          query={autocompleteState.query}
          skills={skills}
          files={workspaceFiles}
          selectedIndex={autocompleteState.selectedIndex}
          onSelect={handleSelectAutocomplete}
          onClose={() => setAutocompleteState((prev) => ({ ...prev, isOpen: false }))}
        />
      )}

      <div className="chat-input-row">
        <textarea
          ref={textareaRef}
          className="form-textarea chat-textarea-auto"
          id="chatInput"
          placeholder="Type prompt, /skill or @file for AI assistant..."
          value={text}
          onChange={handleChange}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          rows={1}
        />
        {isStreaming ? (
          onAbort && (
            <button
              type="button"
              className="round-abort-btn btn-danger abort-pulse"
              id="abortBtn"
              onClick={onAbort}
              title="Cancel ongoing response"
              aria-label="Cancel ongoing response"
            >
              <span className="abort-btn-icon">⏹</span>
            </button>
          )
        ) : (
          <button
            type="button"
            className="round-send-btn btn-primary"
            id="sendPromptBtn"
            onClick={handleSubmit}
            disabled={disabled || !text.trim()}
            title="Send Prompt"
            aria-label="Send Prompt"
          >
            <span className="send-btn-icon">➤</span>
          </button>
        )}
      </div>

      <div className="input-actions desktop-hint-only">
        <span className="input-hint-text">
          Press <strong>Enter</strong> to send, <strong>Shift + Enter</strong> for line break • Type <strong>/</strong> for skills, <strong>@</strong> for files
        </span>
      </div>
    </div>
  );
};
