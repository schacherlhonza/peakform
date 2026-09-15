import axios from 'axios';

const ACCESS_TOKEN_KEY = 'traincoach.accessToken';
const REFRESH_TOKEN_KEY = 'traincoach.refreshToken';

const baseURL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080';

export function getAccessToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

export function setTokens(accessToken: string, refreshToken: string): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
}

export function clearTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
}

/** Plain axios (not the intercepted instance) to avoid a recursive refresh loop. */
export async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    return null;
  }

  try {
    const response = await axios.post<{ accessToken: string; refreshToken: string }>(
      `${baseURL}/api/auth/refresh`,
      { refreshToken },
    );
    setTokens(response.data.accessToken, response.data.refreshToken);
    return response.data.accessToken;
  } catch {
    clearTokens();
    return null;
  }
}
