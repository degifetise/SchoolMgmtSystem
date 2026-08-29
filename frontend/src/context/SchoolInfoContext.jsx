import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { schoolApi } from '../services/endpoints';

const FALLBACK = {
  schoolName: 'Halade High School',
  contactEmail: null,
  academicYear: '',
  allowSelfRegistration: false,
};

const SchoolInfoContext = createContext(null);

/**
 * Loads the anonymous /api/system-settings/school-info payload once, so the login
 * screen and the dashboard header can show the school's identity without a token.
 * A failure is not fatal: the UI falls back to the default name.
 */
export function SchoolInfoProvider({ children }) {
  const [info, setInfo] = useState(FALLBACK);
  const [loading, setLoading] = useState(true);

  const refresh = useCallback(async () => {
    try {
      const data = await schoolApi.getInfo();
      setInfo({ ...FALLBACK, ...data });
      return data;
    } catch {
      setInfo(FALLBACK);
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const value = useMemo(() => ({ ...info, loading, refresh }), [info, loading, refresh]);

  return <SchoolInfoContext.Provider value={value}>{children}</SchoolInfoContext.Provider>;
}

export function useSchoolInfo() {
  const context = useContext(SchoolInfoContext);
  if (!context) {
    throw new Error('useSchoolInfo must be used inside a <SchoolInfoProvider>.');
  }
  return context;
}
