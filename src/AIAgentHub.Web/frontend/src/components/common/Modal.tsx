import React, { useEffect } from 'react';
import { useModal } from '../../context/ModalContext';

export const Modal: React.FC = () => {
  const { modal, hideModal } = useModal();

  useEffect(() => {
    if (!modal) return;

    const originalOverflow = document.body.style.overflow;
    const originalTouchAction = document.body.style.touchAction;
    const originalDocOverflow = document.documentElement.style.overflow;

    document.body.style.overflow = 'hidden';
    document.body.style.touchAction = 'none';
    document.documentElement.style.overflow = 'hidden';

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        hideModal();
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => {
      document.body.style.overflow = originalOverflow;
      document.body.style.touchAction = originalTouchAction;
      document.documentElement.style.overflow = originalDocOverflow;
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [modal, hideModal]);

  if (!modal) return null;

  const sizeClass = modal.size ? `modal-${modal.size}` : 'modal-md';

  return (
    <div
      id="modalContainer"
      className="modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) hideModal();
      }}
    >
      <div className={`modal-box glass ${sizeClass}`} id="modalBox">
        <div className="modal-header">
          <h3 id="modalTitle">{modal.title}</h3>
          <button className="modal-close" id="modalCloseBtn" onClick={hideModal} aria-label="Close modal">
            &times;
          </button>
        </div>
        <div className="modal-body" id="modalBody">
          {modal.content}
        </div>
        {modal.footer && (
          <div className="modal-footer" id="modalFooter">
            {modal.footer}
          </div>
        )}
      </div>
    </div>
  );
};
