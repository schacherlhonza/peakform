import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { jwtDecode } from './jwtDecode';
import { getAccessToken, setTokens, clearTokens } from './tokenStore';
import type { AppRole } from '../api/generated/models';

interface CurrentUser {
  userId: string;
  role: AppRole;
}

interface AuthContextValue {
  user: CurrentUser | null;
  isAuthenticated: boolean;
  login: (accessToken: string, refreshToken: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function readUserFromToken(): CurrentUser | null {
  const token = getAccessToken();
  if (!token) return null;

  try {
    const claims = jwtDecode(token);
    const userId = claims['sub'] ?? claims['nameid'];
    const role = claims['role'] as AppRole | undefined;
    if (!userId || !role) return null;
    return { userId, role };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(() => readUserFromToken());

  useEffect(() => {
    // Keep state in sync if tokens change in another tab.
    const onStorage = () => setUser(readUserFromToken());
    window.addEventListener('storage', onStorage);
    return () => window.removeEventListener('storage', onStorage);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      login: (accessToken, refreshToken) => {
        setTokens(accessToken, refreshToken);
        setUser(readUserFromToken());
      },
      logout: () => {
        clearTokens();
        setUser(null);
      },
    }),
    [user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
