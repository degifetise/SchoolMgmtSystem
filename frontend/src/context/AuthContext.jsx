import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { authApi } from '../services/endpoints';
import { onSessionExpired, tokenStore } from '../services/api';

export const ROLES = {
  admin: 'Admin',
  teacher: 'Teacher',
  student: 'Student',
};

const AuthContext = createContext(null);

/**
 * Reads a JWT payload without verifying it. The signature is the API's business;
 * the client only needs the expiry so it can avoid firing calls it knows will 401.
 */
export function decodeToken(token) {
  if (!token || typeof token !== 'string') return null;

  const segments = token.split('.');
  if (segments.length !== 3) return null;

  try {
    const base64 = segments[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((char) => '%' + char.charCodeAt(0).toString(16).padStart(2, '0'))
        .join(''),
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

export function isTokenExpired(token, skewSeconds = 30) {
  const payload = decodeToken(token);
  if (!payload?.exp) return true;
  return payload.exp * 1000 <= Date.now() + skewSeconds * 1000;
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => tokenStore.getUser());
  const [initialising, setInitialising] = useState(true);
  const [sessionMessage, setSessionMessage] = useState(null);

  /* Guards the async callbacks below against updating an unmounted provider. It must be
     re-armed on every mount: StrictMode mounts, cleans up, then mounts again, so a flag
     only cleared on cleanup would stay false and silently drop the post-login setUser. */
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const logout = useCallback(async ({ silent = true } = {}) => {
    const refreshToken = tokenStore.getRefreshToken();

    if (refreshToken) {
      try {
        await authApi.logout(refreshToken);
      } catch {
        // A failed revoke must not trap the user in a session they asked to leave.
      }
    }

    tokenStore.clear();
    if (mounted.current) {
      setUser(null);
      if (!silent) setSessionMessage('You have been signed out.');
    }
  }, []);

  /* The stored profile renders the shell instantly, then /api/auth/me confirms the
     token is still good and picks up role or class changes made by an admin. */
  useEffect(() => {
    let cancelled = false;

    async function restore() {
      const token = tokenStore.getAccessToken();

      if (!token) {
        if (!cancelled) setInitialising(false);
        return;
      }

      // Unusable access token and nothing to refresh with: end the session here rather
      // than firing a request that is certain to 401, and say why on the login screen.
      if (isTokenExpired(token) && !tokenStore.getRefreshToken()) {
        tokenStore.clear();
        if (!cancelled) {
          setUser(null);
          setSessionMessage('Your session has expired. Please sign in again.');
          setInitialising(false);
        }
        return;
      }

      try {
        const profile = await authApi.me();
        tokenStore.save({ user: profile });
        if (!cancelled) setUser(profile);
      } catch {
        // The interceptor already cleared storage if the refresh failed.
        if (!cancelled) setUser(tokenStore.getUser());
      } finally {
        if (!cancelled) setInitialising(false);
      }
    }

    restore();
    return () => { cancelled = true; };
  }, []);

  // The Axios interceptor tells us when a refresh failed for good.
  useEffect(
    () =>
      onSessionExpired(() => {
        if (!mounted.current) return;
        setUser(null);
        setSessionMessage('Your session has expired. Please sign in again.');
      }),
    [],
  );

  const login = useCallback(async (email, password) => {
    const data = await authApi.login(email, password);

    tokenStore.save({
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      user: data.user,
    });

    if (mounted.current) {
      setUser(data.user);
      setSessionMessage(null);
    }

    return data.user;
  }, []);

  const value = useMemo(() => {
    const roles = user?.roles ?? [];

    return {
      user,
      roles,
      initialising,
      sessionMessage,
      clearSessionMessage: () => setSessionMessage(null),
      isAuthenticated: Boolean(user),
      hasRole: (...wanted) => wanted.flat().some((role) => roles.includes(role)),
      isAdmin: roles.includes(ROLES.admin),
      isTeacher: roles.includes(ROLES.teacher),
      isStudent: roles.includes(ROLES.student),
      login,
      logout,
    };
  }, [user, initialising, sessionMessage, login, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used inside an <AuthProvider>.');
  }
  return context;
}

/** Where each role lands after signing in: the first job each one has to do. */
export function homeRouteFor(roles = []) {
  if (roles.includes(ROLES.admin)) return '/admin/students';
  if (roles.includes(ROLES.teacher)) return '/teacher/students';
  if (roles.includes(ROLES.student)) return '/student/results';
  return '/login';
}
