import api from './api';

/**
 * One place that knows the API's URL shapes, so a route change never has to be
 * chased through the pages. Every function resolves to the response body.
 */
const unwrap = (promise) => promise.then(({ data }) => data);

export const schoolApi = {
  /** Anonymous: used by the login screen and the dashboard header. */
  getInfo: () => unwrap(api.get('/api/system-settings/school-info')),
};

/** The anonymous surface behind the Home, About and Explore events pages. */
export const publicApi = {
  overview: () => unwrap(api.get('/api/public/overview')),
  events: (take) => unwrap(api.get('/api/public/events', { params: take ? { take } : {} })),
};

export const healthApi = {
  dbCheck: () => unwrap(api.get('/api/health/db-check')),
};

export const authApi = {
  login: (email, password) => unwrap(api.post('/api/auth/login', { email, password })),
  me: () => unwrap(api.get('/api/auth/me')),
  logout: (refreshToken) => unwrap(api.post('/api/auth/logout', { refreshToken })),
  /**
   * Anonymous application for a place. Resolves to a receipt, not a session: no account exists
   * until an administrator approves it and issues credentials.
   */
  registerStudent: (payload) => unwrap(api.post('/api/auth/register-student', payload)),
};

/** The signed-in user's own account, whatever their role. */
export const accountApi = {
  changePassword: (currentPassword, newPassword) =>
    unwrap(api.post('/api/account/change-password', { currentPassword, newPassword })),
};

/** The administrator's student registration queue. */
export const registrationApi = {
  /** Applications by review state: Pending, Approved or Rejected. */
  list: (status = 'Pending') =>
    unwrap(api.get('/api/admin/registration-requests', { params: { status } })),
  /** Resolves to the issued credentials, including the one-time temporary password. */
  approve: (id, note) =>
    unwrap(api.post(`/api/admin/registration-requests/${id}/approve`, { note: note ?? null })),
  reject: (id, note) =>
    unwrap(api.post(`/api/admin/registration-requests/${id}/reject`, { note: note ?? null })),
};

export const gradeLevelApi = {
  list: () => unwrap(api.get('/api/grade-levels')),
};

export const sectionApi = {
  list: () => unwrap(api.get('/api/sections')),
};

export const subjectApi = {
  list: (params = {}) => unwrap(api.get('/api/subjects', { params })),
  mine: () => unwrap(api.get('/api/subjects/mine')),
};

export const studentApi = {
  list: (params = {}) => unwrap(api.get('/api/students', { params })),
  get: (id) => unwrap(api.get(`/api/students/${id}`)),
  create: (payload) => unwrap(api.post('/api/students', payload)),
  update: (id, payload) => unwrap(api.put(`/api/students/${id}`, payload)),
  setStatus: (id, isActive) => unwrap(api.put(`/api/students/${id}/status`, { isActive })),
  resetPassword: (id) => unwrap(api.post(`/api/students/${id}/reset-password`)),
  summary: () => unwrap(api.get('/api/students/summary')),

  /**
   * The signed-in student's own results: subjects with component scores, weighted totals,
   * the grade summary and the weighting behind them, in one call.
   */
  myResults: () => unwrap(api.get('/api/students/my-results')),
};

export const teacherApi = {
  list: (params = {}) => unwrap(api.get('/api/teachers', { params })),
  create: (payload) => unwrap(api.post('/api/teachers', payload)),
  setStatus: (id, isActive) => unwrap(api.put(`/api/teachers/${id}/status`, { isActive })),
  resetPassword: (id) => unwrap(api.post(`/api/teachers/${id}/reset-password`)),
  /** The signed-in teacher's own subject/section assignments. */
  myClasses: () => unwrap(api.get('/api/teachers/me/classes')),
  /** Class list for one of those assignments, with each student's weighted standing. */
  classRoster: (assignmentId) => unwrap(api.get(`/api/teachers/me/classes/${assignmentId}/students`)),
};

export const assessmentApi = {
  list: (params = {}) => unwrap(api.get('/api/assessments', { params })),
  create: (payload) => unwrap(api.post('/api/assessments', payload)),
  update: (id, payload) => unwrap(api.put(`/api/assessments/${id}`, payload)),
  remove: (id) => unwrap(api.delete(`/api/assessments/${id}`)),
};

export const markApi = {
  /** Class list for one assessment, with any scores already entered. */
  gradebook: (assessmentId) => unwrap(api.get(`/api/marks/assessment/${assessmentId}`)),
  saveBulk: (payload) => unwrap(api.post('/api/marks/bulk', payload)),
  publish: (assessmentId, isPublished) =>
    unwrap(api.put(`/api/marks/assessment/${assessmentId}/publish`, { isPublished })),
  /** Student's own published marks. Their report card comes from studentApi.myResults. */
  mine: (params = {}) => unwrap(api.get('/api/marks/me', { params })),
  weights: () => unwrap(api.get('/api/marks/weights')),
};

export const announcementApi = {
  list: (params = {}) => unwrap(api.get('/api/announcements', { params })),
  create: (payload) => unwrap(api.post('/api/announcements', payload)),
  remove: (id) => unwrap(api.delete(`/api/announcements/${id}`)),
};

export const systemSettingsApi = {
  get: () => unwrap(api.get('/api/system-settings')),
  update: (payload) => unwrap(api.put('/api/system-settings', payload)),
};
