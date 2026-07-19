import Keycloak from 'keycloak-js';
import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';
import { createApiClient } from '@/api/api-client';
import { Wordmark } from '@/components/layout/wordmark';
import { ErrorState } from '@/components/shared/error-state';
import type { AppEnvironment } from '@/config/env';
import {
  getSafeTokenClaims,
  reportAuthDiagnostic,
} from '@/providers/auth-diagnostics';
import { AuthContext, type AuthContextValue } from '@/providers/auth-context';

export type KeycloakFactory = (
  configuration: ConstructorParameters<typeof Keycloak>[0],
) => Keycloak;

interface AuthProviderProps extends PropsWithChildren {
  environment: AppEnvironment;
  keycloakFactory?: KeycloakFactory | undefined;
}

type InitializationState =
  'initializing' | 'authenticated' | 'authorization-error' | 'error';

const initializationPromises = new WeakMap<Keycloak, Promise<boolean>>();
const authenticationLifecycles = new WeakMap<
  Keycloak,
  AuthenticationLifecycle
>();

interface AuthenticationLifecycle {
  state: InitializationState;
  mounted: boolean;
  authenticationLossHandled: boolean;
  unauthorizedHandled: boolean;
}

class LoginCoordinator {
  private pending: Promise<void> | null = null;

  public begin(keycloak: Keycloak, redirectUri: string) {
    this.pending ??= keycloak
      .login({ redirectUri })
      .finally(() => (this.pending = null));
    return this.pending;
  }
}

export function AuthProvider({
  children,
  environment,
  keycloakFactory = (configuration) => new Keycloak(configuration),
}: AuthProviderProps) {
  const [keycloak] = useState(() => {
    const instance = keycloakFactory({
      url: environment.VITE_KEYCLOAK_URL,
      realm: environment.VITE_KEYCLOAK_REALM,
      clientId: environment.VITE_KEYCLOAK_CLIENT_ID,
    });
    authenticationLifecycles.set(instance, {
      state: 'initializing',
      mounted: false,
      authenticationLossHandled: false,
      unauthorizedHandled: false,
    });
    return instance;
  });
  const [state, setState] = useState<InitializationState>('initializing');
  const [loginCoordinator] = useState(() => new LoginCoordinator());
  const redirectUri = `${window.location.origin}/`;

  const transitionTo = useCallback(
    (nextState: InitializationState) => {
      setLifecycleState(keycloak, nextState);
      if (isProviderMounted(keycloak)) setState(nextState);
    },
    [keycloak],
  );

  const login = useCallback(async () => {
    reportAuthDiagnostic('login requested');
    await loginCoordinator.begin(keycloak, redirectUri);
  }, [keycloak, loginCoordinator, redirectUri]);

  const logout = useCallback(async () => {
    reportAuthDiagnostic('logout requested');
    await keycloak.logout({ redirectUri });
  }, [keycloak, redirectUri]);

  const handleAuthenticationLoss = useCallback(() => {
    if (
      !isProviderMounted(keycloak) ||
      getLifecycleState(keycloak) !== 'authenticated' ||
      hasHandledAuthenticationLoss(keycloak)
    ) {
      return;
    }

    markAuthenticationLossHandled(keycloak);
    transitionTo('initializing');
    void login();
  }, [keycloak, login, transitionTo]);

  const handleUnauthorized = useCallback(() => {
    if (!isProviderMounted(keycloak) || hasHandledUnauthorized(keycloak)) {
      return;
    }

    markUnauthorizedHandled(keycloak);
    if (keycloak.authenticated) {
      transitionTo('authorization-error');
      return;
    }

    handleAuthenticationLoss();
  }, [handleAuthenticationLoss, keycloak, transitionTo]);

  useEffect(() => {
    let active = true;
    setProviderMounted(keycloak, true);

    const removeCallbacks = configureKeycloakCallbacks(
      keycloak,
      handleAuthenticationLoss,
    );

    void initializeKeycloak(keycloak, {
      onLoad: 'login-required',
      flow: 'standard',
      pkceMethod: 'S256',
      checkLoginIframe: false,
      redirectUri,
    })
      .then((authenticated) => {
        if (!active) return;
        reportAuthDiagnostic('initialization completed', { authenticated });
        if (authenticated) {
          reportAuthDiagnostic(
            'access token claims',
            getSafeTokenClaims(keycloak.tokenParsed),
          );
          transitionTo('authenticated');
        } else {
          transitionTo('error');
        }
      })
      .catch(() => {
        reportAuthDiagnostic('initialization failed');
        if (active) transitionTo('error');
      });

    return () => {
      active = false;
      setProviderMounted(keycloak, false);
      removeCallbacks();
    };
  }, [handleAuthenticationLoss, keycloak, redirectUri, transitionTo]);

  const getAccessToken = useCallback(async () => {
    try {
      reportAuthDiagnostic('token refresh requested');
      await keycloak.updateToken(30);
    } catch {
      reportAuthDiagnostic('token refresh failed');
      handleAuthenticationLoss();
      throw new Error('Authentication refresh failed.');
    }

    if (!keycloak.authenticated || !keycloak.token) {
      handleAuthenticationLoss();
      throw new Error('Authentication is required.');
    }

    return keycloak.token;
  }, [handleAuthenticationLoss, keycloak]);

  const apiClient = useMemo(
    () =>
      createApiClient({
        baseUrl: environment.VITE_API_BASE_URL,
        getAccessToken,
        onUnauthorized: handleUnauthorized,
      }),
    [environment.VITE_API_BASE_URL, getAccessToken, handleUnauthorized],
  );

  const value = useMemo<AuthContextValue>(
    () => ({ isAuthenticated: true, apiClient, login, logout }),
    [apiClient, login, logout],
  );

  if (state === 'initializing') return <AuthenticationLoading />;
  if (state === 'authorization-error') return <AuthorizationError />;
  if (state === 'error') return <AuthenticationError />;

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function initializeKeycloak(
  keycloak: Keycloak,
  options: Parameters<Keycloak['init']>[0],
) {
  const pending = initializationPromises.get(keycloak);
  if (pending) return pending;

  reportAuthDiagnostic('initialization started');
  const initialization = keycloak.init(options);
  initializationPromises.set(keycloak, initialization);
  return initialization;
}

function getAuthenticationLifecycle(keycloak: Keycloak) {
  const lifecycle = authenticationLifecycles.get(keycloak);
  if (!lifecycle) {
    throw new Error('Keycloak authentication lifecycle is unavailable.');
  }
  return lifecycle;
}

function setProviderMounted(keycloak: Keycloak, mounted: boolean) {
  getAuthenticationLifecycle(keycloak).mounted = mounted;
}

function isProviderMounted(keycloak: Keycloak) {
  return getAuthenticationLifecycle(keycloak).mounted;
}

function setLifecycleState(keycloak: Keycloak, state: InitializationState) {
  getAuthenticationLifecycle(keycloak).state = state;
}

function getLifecycleState(keycloak: Keycloak) {
  return getAuthenticationLifecycle(keycloak).state;
}

function markAuthenticationLossHandled(keycloak: Keycloak) {
  getAuthenticationLifecycle(keycloak).authenticationLossHandled = true;
}

function hasHandledAuthenticationLoss(keycloak: Keycloak) {
  return getAuthenticationLifecycle(keycloak).authenticationLossHandled;
}

function markUnauthorizedHandled(keycloak: Keycloak) {
  getAuthenticationLifecycle(keycloak).unauthorizedHandled = true;
}

function hasHandledUnauthorized(keycloak: Keycloak) {
  return getAuthenticationLifecycle(keycloak).unauthorizedHandled;
}

function configureKeycloakCallbacks(
  keycloak: Keycloak,
  redirectToLogin: () => void,
) {
  keycloak.onAuthLogout = () => {
    reportAuthDiagnostic('logout event');
    redirectToLogin();
  };
  keycloak.onAuthRefreshError = redirectToLogin;
  keycloak.onTokenExpired = () => {
    reportAuthDiagnostic('token refresh requested');
    void keycloak.updateToken(30).catch(() => {
      reportAuthDiagnostic('token refresh failed');
      redirectToLogin();
    });
  };

  return () => {
    delete keycloak.onAuthLogout;
    delete keycloak.onAuthRefreshError;
    delete keycloak.onTokenExpired;
  };
}

function AuthorizationError() {
  return (
    <main className="grid min-h-screen place-items-center bg-background p-6">
      <ErrorState
        title="ReceiptFlow API access was denied"
        description="Your identity session is valid, but the API rejected its access token. Check the client audience configuration and try again."
        actionLabel="Try again"
        onAction={() => {
          window.location.reload();
        }}
      />
    </main>
  );
}

function AuthenticationLoading() {
  return (
    <main
      className="grid min-h-screen place-items-center bg-background p-6"
      aria-busy="true"
    >
      <div className="text-center" role="status">
        <div className="flex justify-center">
          <Wordmark />
        </div>
        <div className="mx-auto mt-6 size-8 animate-spin rounded-full border-2 border-muted border-t-primary motion-reduce:animate-none" />
        <p className="mt-3 text-sm text-muted-foreground">
          Securing your workspace…
        </p>
      </div>
    </main>
  );
}

function AuthenticationError() {
  return (
    <main className="grid min-h-screen place-items-center bg-background p-6">
      <ErrorState
        title="Sign-in could not be started"
        description="ReceiptFlow could not connect to the identity service. Check the service status and try again."
        actionLabel="Try again"
        onAction={() => {
          window.location.reload();
        }}
      />
    </main>
  );
}
