import { describe, expect, it } from 'vitest';
import { validateEnvironment } from '@/config/env';

const validEnvironment = {
  VITE_API_BASE_URL: 'https://localhost:7001',
  VITE_KEYCLOAK_URL: 'https://localhost:6001',
  VITE_KEYCLOAK_REALM: 'receipt',
  VITE_KEYCLOAK_CLIENT_ID: 'receiptflow-web',
};

describe('environment validation', () => {
  it('accepts required configuration', () => {
    expect(validateEnvironment(validEnvironment)).toEqual(validEnvironment);
  });

  it('reports missing configuration without rendering its values', () => {
    expect(() => validateEnvironment({})).toThrow(
      'Missing required environment configuration',
    );
  });
});
