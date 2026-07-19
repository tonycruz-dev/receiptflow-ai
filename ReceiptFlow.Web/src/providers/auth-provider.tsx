import { createContext, useMemo, type PropsWithChildren } from 'react';

export interface AuthUser {
  displayName: string;
  email: string;
}

export interface AuthContextValue {
  isAuthenticated: boolean;
  isLoading: boolean;
  user: AuthUser | null;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Authentication boundary for the future Keycloak adapter. It deliberately
 * provides an anonymous local shell and performs no identity network calls.
 */
export function AuthProvider({ children }: PropsWithChildren) {
  const value = useMemo<AuthContextValue>(
    () => ({ isAuthenticated: false, isLoading: false, user: null }),
    [],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
