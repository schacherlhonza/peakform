import { describe, expect, it } from 'vitest';
import { jwtDecode } from './jwtDecode';

function makeToken(payload: Record<string, unknown>): string {
  const base64url = (obj: unknown) =>
    btoa(unescape(encodeURIComponent(JSON.stringify(obj))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
  return `${base64url({ alg: 'HS256', typ: 'JWT' })}.${base64url(payload)}.fakesignature`;
}

describe('jwtDecode', () => {
  it('decodes claims from a well-formed token', () => {
    const token = makeToken({ sub: 'user-1', role: 'Coach' });
    expect(jwtDecode(token)).toMatchObject({ sub: 'user-1', role: 'Coach' });
  });

  it('decodes Czech diacritics correctly (UTF-8 safe)', () => {
    const token = makeToken({ name: 'Jana Procházková' });
    expect(jwtDecode(token).name).toBe('Jana Procházková');
  });

  it('throws for a malformed token', () => {
    expect(() => jwtDecode('not-a-jwt')).toThrow();
  });
});
