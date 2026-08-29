import { useCallback, useEffect, useRef, useState } from 'react';
import { extractErrorMessage } from '../services/api';

/**
 * Runs a fetcher and exposes the three states every screen needs: loading, error and data,
 * plus a reload for the retry button. Keeps the loading and error handling identical
 * everywhere instead of repeating a try/catch in each page.
 *
 * @param fetcher must be stable - wrap it in useCallback, or leave it as a module-level function.
 * @param deps    re-runs the fetcher when these change, like useEffect's dependency array.
 */
export function useApiResource(fetcher, deps = []) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [reloading, setReloading] = useState(false);

  // A late response from a superseded request must not overwrite the current one.
  const requestId = useRef(0);

  const run = useCallback(
    async ({ isReload = false } = {}) => {
      const id = ++requestId.current;

      if (isReload) setReloading(true);
      else setLoading(true);
      setError(null);

      try {
        const result = await fetcher();
        if (id === requestId.current) setData(result);
        return result;
      } catch (err) {
        if (id === requestId.current) {
          setError(err.friendlyMessage ?? extractErrorMessage(err));
        }
        return null;
      } finally {
        if (id === requestId.current) {
          setLoading(false);
          setReloading(false);
        }
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [fetcher, ...deps],
  );

  useEffect(() => {
    run();
    // Cancels any in-flight result on unmount by invalidating the current request id.
    return () => { requestId.current += 1; };
  }, [run]);

  return {
    data,
    error,
    loading,
    reloading,
    reload: () => run({ isReload: true }),
    setData,
  };
}
