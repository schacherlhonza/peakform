/** Minimal JWT payload decoder — no signature verification (the backend already verified it;
 * the frontend only reads claims for UI purposes, never for authorization decisions). */
export function jwtDecode(token: string): Record<string, string> {
  const payload = token.split('.')[1];
  if (!payload) throw new Error('Neplatný token.');

  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
  const json = decodeURIComponent(
    atob(padded)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  );

  return JSON.parse(json) as Record<string, string>;
}
