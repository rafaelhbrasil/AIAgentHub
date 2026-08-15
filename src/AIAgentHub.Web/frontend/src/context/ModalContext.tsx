import React, { createContext, useContext, useState, useCallback, ReactNode } from 'react';

interface ModalConfig {
  title: string;
  content: ReactNode;
  footer?: ReactNode;
}

interface ModalContextType {
  modal: ModalConfig | null;
  showModal: (title: string, content: ReactNode, footer?: ReactNode) => void;
  hideModal: () => void;
}

const ModalContext = createContext<ModalContextType | undefined>(undefined);

export const ModalProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [modal, setModal] = useState<ModalConfig | null>(null);

  const showModal = useCallback((title: string, content: ReactNode, footer?: ReactNode) => {
    setModal({ title, content, footer });
  }, []);

  const hideModal = useCallback(() => {
    setModal(null);
  }, []);

  return (
    <ModalContext.Provider value={{ modal, showModal, hideModal }}>
      {children}
    </ModalContext.Provider>
  );
};

export function useModal(): ModalContextType {
  const context = useContext(ModalContext);
  if (!context) {
    throw new Error('useModal must be used within a ModalProvider');
  }
  return context;
}
