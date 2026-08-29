import axios from 'axios';

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5006';

export const STORAGE_KEYS = {
  accessToken: 'halade.accessToken',
  refreshToken: 'halade.refreshToken',
  user: 'halade.user',
};

/* ---------------------------------------------------------------------------
   Token storage
   Kept in localStorage so a reload keeps the session. The refresh token is
   rotated by the API on every use, so a stolen copy stops working as soon as
   the legitimate client refreshes.
   --------------------------------------------------------------------------- */
export const tokenStore = {
  getAccessToken: () => localStorage.getItem(STORAGE_KEYS.accessToken),
  getRefreshToken: () => localStorage.getItem(STORAGE_KEYS.refreshToken),

  getUser() {
    const raw = localStorage.getItem(STORAGE_KEYS.user);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  },

  save({ accessToken, refreshToken, user }) {
    if (accessToken) localStorage.setItem(STORAGE_KEYS.accessToken, accessToken);
    if (refreshToken) localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken);
    if (user) localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(user));
  },

  clear() {
    Object.values(STORAGE_KEYS).forEach((key) => localStorage.removeItem(key));
  },
};

/* ---------------------------------------------------------------------------
   Session expiry notification
   api.js cannot import the router or the auth context without creating a cycle,
   so AuthContext subscribes here and decides what to do when a session dies.
   --------------------------------------------------------------------------- */
const sessionExpiredHandlers = new Set();

export function onSessionExpired(handler) {
  sessionExpiredHandlers.add(handler);
  return () => sessionExpiredHandlers.delete(handler);
}

function notifySessionExpired() {
  sessionExpiredHandlers.forEach((handler) => {
    try {
      handler();
    } catch {
      // A misbehaving subscriber must not stop the others from being told.
    }
  });
}

const api = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30000,
});

/** Bare client for refreshing, so the interceptors below cannot recurse. */
const refreshClient = axios.create({ baseURL: BASE_URL, timeout: 30000 });

api.interceptors.request.use((config) => {
  const token = tokenStore.getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/* One refresh at a time. Without this, a dashboard that fires six requests on
   mount would send six refreshes, and rotation would invalidate five of them. */
let refreshPromise = null;

function refreshAccessToken() {
  if (refreshPromise) return refreshPromise;

  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return Promise.reject(new Error('No refresh token stored.'));

  refreshPromise = refreshClient
    .post('/api/auth/refresh', { refreshToken })
    .then(({ data }) => {
      tokenStore.save({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        user: data.user,
      });
      return data.accessToken;
    })
    .finally(() => {
      refreshPromise = null;
    });

  return refreshPromise;
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { response, config } = error;

    if (!response) {
      // No response at all: the API is down, blocked by CORS, or the request timed out.
      return Promise.reject(
        Object.assign(error, {
          friendlyMessage:
            'Cannot reach the server. Check that the API is running on ' + BASE_URL + '.',
        }),
      );
    }

    const isAuthCall = config?.url?.includes('/api/auth/');

    if (response.status === 401 && !config._retried && !isAuthCall) {
      config._retried = true;

      try {
        const token = await refreshAccessToken();
        config.headers = { ...config.headers, Authorization: `Bearer ${token}` };
        return api(config);
      } catch {
        tokenStore.clear();
        notifySessionExpired();
        return Promise.reject(
          Object.assign(error, { friendlyMessage: 'Your session has expired. Please sign in again.' }),
        );
      }
    }

    if (response.status === 401 && isAuthCall) {
      return Promise.reject(
        Object.assign(error, { friendlyMessage: extractErrorMessage(error) }),
      );
    }

    if (response.status === 403) {
      return Promise.reject(
        Object.assign(error, {
          friendlyMessage:
            extractErrorMessage(error) ?? 'You do not have permission to perform this action.',
        }),
      );
    }

    return Promise.reject(Object.assign(error, { friendlyMessage: extractErrorMessage(error) }));
  },
);

/**
 * Flattens the API's ProblemDetails and ValidationProblemDetails shapes into one
 * readable sentence, so every screen can show `err.friendlyMessage` and be done.
 */
export function extractErrorMessage(error) {
  const data = error?.response?.data;

  if (!data) return error?.message ?? 'Something went wrong.';
  if (typeof data === 'string') return data;

  if (data.errors && typeof data.errors === 'object') {
    const messages = Object.values(data.errors).flat().filter(Boolean);
    if (messages.length) return messages.join(' ');
  }

  return data.detail ?? data.title ?? error.message ?? 'Something went wrong.';
}

export { BASE_URL };
export default api;
