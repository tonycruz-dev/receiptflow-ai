import { createContext } from 'react';
import type { ReceiptFlowApiClient } from '@/api/api-client';

export interface AuthContextValue {
  isAuthenticated: true;
  apiClient: ReceiptFlowApiClient;
  login: () => Promise<void>;
  logout: () => Promise<void>;
}

export const AuthContext = createContext<AuthContextValue | null>(null);
