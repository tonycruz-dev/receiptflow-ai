import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type Keycloak from 'keycloak-js';
import { StrictMode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { AuthProvider, type KeycloakFactory } from '@/providers/auth-provider';
import { useAuth } from '@/providers/use-auth';
import { testEnvironment } from '@/test/environment';

function createKeycloakMock(
  initResult: Promise<boolean>,
  options?: {
    loginResult?: Promise<void>;
    refreshResult?: Promise<boolean>;
  },
) {
  const init = vi.fn().mockReturnValue(initResult);
  const login = vi
    .fn()
    .mockReturnValue(options?.loginResult ?? Promise.resolve());
  const logout = vi.fn().mockResolvedValue(undefined);
  const updateToken = vi
    .fn()
    .mockReturnValue(options?.refreshResult ?? Promise.resolve(false));
  const keycloak = {
    authenticated: true,
    token: 'test-access-token',
    init,
    login,
    logout,
    updateToken,
  } as unknown as Keycloak;
  return { init, keycloak, login, logout, updateToken };
}

function ApiRequestProbe() {
  const { apiClient, login, logout } = useAuth();
  return (
    <div>
      <button
        onClick={() => {
          void login();
        }}
      >
        Sign in again
      </button>
      <button
        onClick={() => {
          void apiClient.getCurrentUser().catch(() => undefined);
        }}
      >
        Load account
      </button>
      <button
        onClick={() => {
          void Promise.allSettled([
            apiClient.getCurrentUser(),
            apiClient.getCurrentUser(),
          ]);
        }}
      >
        Load account twice
      </button>
      <button
        onClick={() => {
          void logout();
        }}
      >
        Sign out
      </button>
    </div>
  );
}

describe('AuthProvider', () => {
  it('initializes Keycloak with login-required and PKCE S256', async () => {
    const { init, keycloak } = createKeycloakMock(Promise.resolve(true));
    const factory: KeycloakFactory = vi.fn(() => keycloak);

    render(
      <StrictMode>
        <AuthProvider environment={testEnvironment} keycloakFactory={factory}>
          <p>Protected content</p>
        </AuthProvider>
      </StrictMode>,
    );

    expect(screen.getByText('Securing your workspace…')).toBeVisible();
    expect(await screen.findByText('Protected content')).toBeVisible();
    expect(init).toHaveBeenCalledWith({
      onLoad: 'login-required',
      flow: 'standard',
      pkceMethod: 'S256',
      checkLoginIframe: false,
      redirectUri: `${window.location.origin}/`,
    });
    expect(init).toHaveBeenCalledOnce();
    expect(screen.queryByText('test-access-token')).not.toBeInTheDocument();
  });

  it('treats an unauthenticated initialization as an error without a second login', async () => {
    const { keycloak, login } = createKeycloakMock(Promise.resolve(false));

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <p>Protected content</p>
      </AuthProvider>,
    );

    expect(
      await screen.findByRole('heading', {
        name: 'Sign-in could not be started',
      }),
    ).toBeVisible();
    expect(login).not.toHaveBeenCalled();
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
  });

  it('renders a safe initialization failure', async () => {
    const { keycloak } = createKeycloakMock(
      Promise.reject(new Error('secret detail')),
    );

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <p>Protected content</p>
      </AuthProvider>,
    );

    expect(
      await screen.findByRole('heading', {
        name: 'Sign-in could not be started',
      }),
    ).toBeVisible();
    expect(screen.queryByText('secret detail')).not.toBeInTheDocument();
  });

  it('refreshes the Keycloak token before an authenticated API request', async () => {
    const { keycloak, updateToken } = createKeycloakMock(Promise.resolve(true));
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          userId: 'user-bob',
          username: 'bob',
          email: 'bob@example.test',
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } },
      ),
    );
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Load account' }),
    );
    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledOnce();
    });
    expect(updateToken).toHaveBeenCalledWith(30);
  });

  it('logs out through Keycloak with a valid post-logout redirect', async () => {
    const { keycloak, logout } = createKeycloakMock(Promise.resolve(true));
    const user = userEvent.setup();
    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(await screen.findByRole('button', { name: 'Sign out' }));
    expect(logout).toHaveBeenCalledWith({
      redirectUri: `${window.location.origin}/`,
    });
  });

  it('uses the stable origin redirect for login instead of the current route', async () => {
    const originalPath = `${window.location.pathname}${window.location.search}`;
    window.history.replaceState(null, '', '/receipts/receipt-1?from=search');
    const { keycloak, login } = createKeycloakMock(Promise.resolve(true));
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Sign in again' }),
    );
    expect(login).toHaveBeenCalledWith({
      redirectUri: `${window.location.origin}/`,
    });
    expect(login).not.toHaveBeenCalledWith({
      redirectUri: window.location.href,
    });
    window.history.replaceState(null, '', originalPath);
  });

  it('completes a Keycloak callback without repeated initialization or login', async () => {
    const originalUrl = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    window.history.replaceState(
      null,
      '',
      '/#state=redacted&session_state=redacted&iss=redacted&code=redacted',
    );
    const { init, keycloak, login } = createKeycloakMock(Promise.resolve(true));
    init.mockImplementation(() => {
      window.history.replaceState(null, '', '/');
      return Promise.resolve(true);
    });

    render(
      <StrictMode>
        <AuthProvider
          environment={testEnvironment}
          keycloakFactory={() => keycloak}
        >
          <p>Callback complete</p>
        </AuthProvider>
      </StrictMode>,
    );

    expect(await screen.findByText('Callback complete')).toBeVisible();
    expect(init).toHaveBeenCalledOnce();
    expect(login).not.toHaveBeenCalled();
    expect(window.location.hash).toBe('');
    window.history.replaceState(null, '', originalUrl || '/');
  });

  it('deduplicates concurrent explicit login requests', async () => {
    let resolveLogin: (() => void) | undefined;
    const pendingLogin = new Promise<void>((resolve) => {
      resolveLogin = resolve;
    });
    const { keycloak, login } = createKeycloakMock(Promise.resolve(true), {
      loginResult: pendingLogin,
    });
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    const button = await screen.findByRole('button', {
      name: 'Sign in again',
    });
    await Promise.all([user.click(button), user.click(button)]);
    expect(login).toHaveBeenCalledOnce();
    resolveLogin?.();
  });

  it('coordinates concurrent API 401 handling without a redirect loop', async () => {
    let resolveLogin: (() => void) | undefined;
    const pendingLogin = new Promise<void>((resolve) => {
      resolveLogin = resolve;
    });
    const { keycloak, login } = createKeycloakMock(Promise.resolve(true), {
      loginResult: pendingLogin,
    });
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockImplementation(() => {
      keycloak.authenticated = false;
      return Promise.resolve(new Response(null, { status: 401 }));
    });
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Load account twice' }),
    );
    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalledTimes(2);
    });
    expect(login).toHaveBeenCalledOnce();
    resolveLogin?.();
  });

  it('shows an authorization error without login when an authenticated API call returns 401', async () => {
    const { keycloak, login } = createKeycloakMock(Promise.resolve(true));
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(null, { status: 401 }),
    );
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Load account' }),
    );
    expect(
      await screen.findByRole('heading', {
        name: 'ReceiptFlow API access was denied',
      }),
    ).toBeVisible();
    expect(login).not.toHaveBeenCalled();
  });

  it('does not trigger login for an API 403 response', async () => {
    const { keycloak, login } = createKeycloakMock(Promise.resolve(true));
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(null, { status: 403 }),
    );
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Load account' }),
    );
    await waitFor(() => {
      expect(login).not.toHaveBeenCalled();
    });
    expect(
      screen.queryByRole('heading', {
        name: 'ReceiptFlow API access was denied',
      }),
    ).not.toBeInTheDocument();
  });

  it('handles concurrent token refresh failures once', async () => {
    let resolveLogin: (() => void) | undefined;
    const pendingLogin = new Promise<void>((resolve) => {
      resolveLogin = resolve;
    });
    const { keycloak, login, updateToken } = createKeycloakMock(
      Promise.resolve(true),
      { loginResult: pendingLogin },
    );
    updateToken.mockRejectedValue(new Error('refresh failed'));
    const user = userEvent.setup();

    render(
      <AuthProvider
        environment={testEnvironment}
        keycloakFactory={() => keycloak}
      >
        <ApiRequestProbe />
      </AuthProvider>,
    );

    await user.click(
      await screen.findByRole('button', { name: 'Load account twice' }),
    );
    await waitFor(() => {
      expect(updateToken).toHaveBeenCalledTimes(2);
    });
    expect(login).toHaveBeenCalledOnce();
    resolveLogin?.();
  });
});
