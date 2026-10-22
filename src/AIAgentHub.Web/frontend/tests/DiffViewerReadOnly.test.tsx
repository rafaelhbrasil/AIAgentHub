import { describe, it, expect, vi } from 'vitest';
import { renderToString } from 'react-dom/server';
import { DiffViewerFooter } from '../src/components/modals/diff/DiffViewerFooter';
import { DiffSideBySideView } from '../src/components/modals/diff/DiffSideBySideView';

describe('DiffViewer ReadOnly Mode', () => {
  it('DiffViewerFooter renders only Close button when readOnly is true', () => {
    const handleClose = vi.fn();
    const handleAccept = vi.fn();
    const handleReject = vi.fn();

    const html = renderToString(
      <DiffViewerFooter
        changeCount={3}
        isEditing={false}
        isProcessing={false}
        readOnly={true}
        onClose={handleClose}
        onAccept={handleAccept}
        onReject={handleReject}
        onAcceptAll={vi.fn()}
        onRejectAll={vi.fn()}
      />
    );

    // Accept / Reject buttons must NOT exist
    expect(html).not.toContain('Accept Changes');
    expect(html).not.toContain('Reject &amp; Rollback');
    expect(html).not.toContain('Accept All');
    expect(html).not.toContain('Reject All');

    // Close button must be present
    expect(html).toContain('id="diffViewerReadOnlyCloseBtn"');
    expect(html).toContain('Close');
  });

  it('DiffViewerFooter renders Accept and Reject buttons when readOnly is false', () => {
    const html = renderToString(
      <DiffViewerFooter
        changeCount={2}
        isEditing={false}
        isProcessing={false}
        readOnly={false}
        onClose={vi.fn()}
        onAccept={vi.fn()}
        onReject={vi.fn()}
        onAcceptAll={vi.fn()}
        onRejectAll={vi.fn()}
      />
    );

    expect(html).toContain('Accept Changes');
    expect(html).toContain('Reject &amp; Rollback');
    expect(html).not.toContain('id="diffViewerReadOnlyCloseBtn"');
  });

  it('DiffSideBySideView does not render Edit buttons when onStartEditing is undefined (read-only)', () => {
    const topPaneRef = { current: null };
    const bottomPaneRef = { current: null };

    const html = renderToString(
      <DiffSideBySideView
        sideBySideLines={[
          { leftLineNumber: 1, leftText: 'old', leftKind: 0, rightLineNumber: 1, rightText: 'new', rightKind: 0 },
        ]}
        isCreated={false}
        isDeleted={false}
        isMobile={false}
        sideBySideMobileTab="split"
        topPaneRef={topPaneRef as any}
        bottomPaneRef={bottomPaneRef as any}
        onTopScroll={vi.fn()}
        onBottomScroll={vi.fn()}
        onStartEditing={undefined}
      />
    );

    expect(html).not.toContain('✏️ Edit Content');
  });
});
