import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { ChatAutocompletePopover } from '../src/components/workspaces/ChatAutocompletePopover';
import { SkillDto } from '../src/types/settings';

describe('ChatAutocompletePopover', () => {
  beforeEach(() => {
    window.HTMLElement.prototype.scrollIntoView = vi.fn();
  });
  const sampleSkills: SkillDto[] = [
    { id: 'skill-1', name: 'git-commit', description: 'Auto git commit', isEnabled: true },
    { id: 'skill-2', name: 'code-review', description: 'Code review', isEnabled: true },
  ];

  const sampleFiles = ['src/main.ts', 'src/App.tsx', 'README.md'];

  it('renders skill mode with matches and handles item selection', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    const onSelect = vi.fn();
    const onClose = vi.fn();

    await act(async () => {
      root.render(
        <ChatAutocompletePopover
          mode="skill"
          query="git"
          skills={sampleSkills}
          files={sampleFiles}
          selectedIndex={0}
          onSelect={onSelect}
          onClose={onClose}
        />
      );
    });

    expect(container.textContent).toContain('/git-commit');
    expect(container.textContent).toContain('Auto git commit');

    const itemBtn = container.querySelector('.chat-autocomplete-item') as HTMLElement;
    if (itemBtn) {
      await act(async () => {
        itemBtn.click();
      });
      expect(onSelect).toHaveBeenCalledWith(
        expect.objectContaining({
          label: '/git-commit',
          type: 'skill',
        })
      );
    }

    act(() => {
      root.unmount();
    });
    container.remove();
  });

  it('renders file mode with matches and returns null when no match', async () => {
    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    const onSelect = vi.fn();
    const onClose = vi.fn();

    await act(async () => {
      root.render(
        <ChatAutocompletePopover
          mode="file"
          query="App"
          skills={sampleSkills}
          files={sampleFiles}
          selectedIndex={0}
          onSelect={onSelect}
          onClose={onClose}
        />
      );
    });

    expect(container.textContent).toContain('@src/App.tsx');

    // No matches -> returns null
    await act(async () => {
      root.render(
        <ChatAutocompletePopover
          mode="file"
          query="nonexistent_xyz"
          skills={sampleSkills}
          files={sampleFiles}
          selectedIndex={0}
          onSelect={onSelect}
          onClose={onClose}
        />
      );
    });

    expect(container.innerHTML).toBe('');

    act(() => {
      root.unmount();
    });
    container.remove();
  });
});
