import type { AppEnvironment } from '@/config/env';

export const testEnvironment: AppEnvironment = {
  VITE_API_BASE_URL: 'https://api.example.test',
  VITE_KEYCLOAK_URL: 'https://identity.example.test',
  VITE_KEYCLOAK_REALM: 'receipt',
  VITE_KEYCLOAK_CLIENT_ID: 'receiptflow-web',
};
