type AuthDiagnosticDetails = Record<
  string,
  boolean | number | string | string[] | undefined
>;

export function reportAuthDiagnostic(
  event: string,
  details?: AuthDiagnosticDetails,
) {
  if (import.meta.env.MODE !== 'development') return;

  console.info(`[ReceiptFlow auth] ${event}`, details ?? {});
}

export function getSafeTokenClaims(
  token: Record<string, unknown> | undefined,
): AuthDiagnosticDetails {
  const realmAccess = isRecord(token?.realm_access)
    ? token.realm_access
    : undefined;
  const roles = Array.isArray(realmAccess?.roles)
    ? realmAccess.roles.filter(
        (role): role is string => typeof role === 'string',
      )
    : [];

  return {
    iss: typeof token?.iss === 'string' ? token.iss : undefined,
    aud: normalizeAudience(token?.aud),
    azp: typeof token?.azp === 'string' ? token.azp : undefined,
    subPresent: typeof token?.sub === 'string' && token.sub.length > 0,
    exp: typeof token?.exp === 'number' ? token.exp : undefined,
    roles,
  };
}

function normalizeAudience(value: unknown) {
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) {
    return value.filter(
      (audience): audience is string => typeof audience === 'string',
    );
  }
  return undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
