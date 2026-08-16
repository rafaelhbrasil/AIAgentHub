import React from 'react';
import { useModal } from '../../context/ModalContext';

export const Modal: React.FC = () => {
  const { modal, hideModal } = useModal();

  if (!modal) return null;

  const sizeClass = modal.size ? `modal-${modal.size}` : 'modal-md';

  return (
    <div id="modalContainer" className="modal-overlay">
      <div className={`modal-box glass ${sizeClass}`} id="modalBox">
        <div className="modal-header">
          <h3 id="modalTitle">{modal.title}</h3>
          <button className="modal-close" id="modalCloseBtn" onClick={hideModal}>
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
