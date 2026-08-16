import React from 'react';

export interface SpinnerProps {
  size?: number | string;
  isSpinning?: boolean;
  className?: string;
  style?: React.CSSProperties;
}

export const Spinner: React.FC<SpinnerProps> = ({
  size = 16,
  isSpinning = false,
  className = '',
  style,
}) => {
  return (
    <svg
      className={`${isSpinning ? 'spin-anim' : ''} ${className}`.trim()}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.2"
      strokeLinecap="round"
      strokeLinejoin="round"
      style={{ display: 'inline-block', verticalAlign: 'middle', ...style }}
    >
      <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
      <path d="M21 3v5h-5" />
      <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" />
      <path d="M3 21v-5h5" />
    </svg>
  );
};
