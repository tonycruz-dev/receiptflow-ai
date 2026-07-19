const requiredEnvironmentKeys = [
  'VITE_API_BASE_URL',
  'VITE_KEYCLOAK_URL',
  'VITE_KEYCLOAK_REALM',
  'VITE_KEYCLOAK_CLIENT_ID',
] as const;

type EnvironmentKey = (typeof requiredEnvironmentKeys)[number];

export type AppEnvironment = Record<EnvironmentKey, string>;

function parseUrl(name: EnvironmentKey, value: string) {
  try {
    return new URL(value).toString().replace(/\/$/, '');
  } catch {
    throw new Error(`${name} must be a valid absolute URL.`);
  }
}

export function validateEnvironment(
  environment: Record<string, unknown>,
): AppEnvironment {
  const missing = requiredEnvironmentKeys.filter((key) => {
    const value = environment[key];
    return typeof value !== 'string' || value.trim().length === 0;
  });

  if (missing.length > 0) {
    throw new Error(
      `Missing required environment configuration: ${missing.join(', ')}.`,
    );
  }

  return {
    VITE_API_BASE_URL: parseUrl(
      'VITE_API_BASE_URL',
      environment.VITE_API_BASE_URL as string,
    ),
    VITE_KEYCLOAK_URL: parseUrl(
      'VITE_KEYCLOAK_URL',
      environment.VITE_KEYCLOAK_URL as string,
    ),
    VITE_KEYCLOAK_REALM: (environment.VITE_KEYCLOAK_REALM as string).trim(),
    VITE_KEYCLOAK_CLIENT_ID: (
      environment.VITE_KEYCLOAK_CLIENT_ID as string
    ).trim(),
  };
}
