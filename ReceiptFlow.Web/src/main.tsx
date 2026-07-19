import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app';
import { validateEnvironment } from '@/config/env';
import '@/styles.css';

const environment = validateEnvironment(import.meta.env);

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error('ReceiptFlow root element was not found.');
}

createRoot(rootElement).render(
  <StrictMode>
    <App environment={environment} />
  </StrictMode>,
);
