import { useCallback, useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, Eye, EyeOff, LogIn, Mail, School } from 'lucide-react';
import { homeRouteFor, useAuth } from '../context/AuthContext';
import { useSchoolInfo } from '../context/SchoolInfoContext';
import { publicApi } from '../services/endpoints';
import { useApiResource } from '../hooks/useApiResource';
import { extractErrorMessage } from '../services/api';
import { Alert, Skeleton, Spinner } from '../components/ui/Feedback';
import StudentRegistrationForm from '../components/auth/StudentRegistrationForm';

export default function LoginPage() {
  const { login, isAuthenticated, roles, initialising, sessionMessage, clearSessionMessage } = useAuth();
  const { schoolName, contactEmail, academicYear, allowSelfRegistration } = useSchoolInfo();
  const navigate = useNavigate();
  const location = useLocation();

  // The grading policy is read from the API so this panel cannot drift from the weights the
  // portal actually applies. A failure here leaves the panel out; it must never block sign-in.
  const fetchOverview = useCallback(() => publicApi.overview(), []);
  const overview = useApiResource(fetchOverview);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  /* Registration lives here rather than in the navigation bar: it is the same errand as signing
     in, and a visitor who cannot get in is already on this screen. */
  const [registering, setRegistering] = useState(false);
  const [submittedNotice, setSubmittedNotice] = useState(null);

  if (!initialising && isAuthenticated) {
    const target = location.state?.from?.pathname ?? homeRouteFor(roles);
    return <Navigate to={target} replace />;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const profile = await login(email.trim(), password);
      clearSessionMessage();
      navigate(location.state?.from?.pathname ?? homeRouteFor(profile.roles), { replace: true });
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      {/* Brand panel */}
      <div className="relative hidden flex-col justify-between bg-brand-800 p-10 text-white lg:flex">
        <div className="flex items-center gap-3">
          <span className="grid size-11 place-items-center rounded-xl bg-white/15">
            <School className="size-6" aria-hidden="true" />
          </span>
          <div>
            <p className="text-lg font-bold">{schoolName}</p>
            <p className="text-sm text-brand-200">Grades 9 – 12</p>
          </div>
        </div>

        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.4 }}
        >
          <h1 className="text-3xl font-bold leading-tight">
            One portal for lessons,
            <br /> marks and report cards.
          </h1>
          <p className="mt-4 max-w-md text-brand-100">
            Administrators manage the roster, teachers record weighted assessments, and students
            follow their own results as soon as they are published.
          </p>

          {!overview.error && (
            <dl className="mt-8 grid grid-cols-3 gap-4 text-center">
              {overview.loading
                ? Array.from({ length: 6 }).map((_, index) => (
                    <div key={index} className="rounded-lg bg-white/10 px-2 py-3">
                      <Skeleton className="mx-auto h-3 w-14 bg-white/25" />
                      <Skeleton className="mx-auto mt-2 h-5 w-10 bg-white/25" />
                    </div>
                  ))
                : [
                    ...(overview.data?.gradingWeights ?? []).map((weight) => ({
                      term: weight.displayName,
                      detail: `${weight.weightPercentage}%`,
                    })),
                    {
                      term: 'Total',
                      detail: `${(overview.data?.gradingWeights ?? []).reduce(
                        (sum, weight) => sum + weight.weightPercentage,
                        0,
                      )}%`,
                    },
                  ].map(({ term, detail }) => (
                    <div key={term} className="rounded-lg bg-white/10 px-2 py-3">
                      <dt className="text-xs text-brand-200">{term}</dt>
                      <dd className="text-lg font-bold">{detail}</dd>
                    </div>
                  ))}
            </dl>
          )}
        </motion.div>

        <p className="text-xs text-brand-300">
          {academicYear && <>Academic year {academicYear}</>}
        </p>
      </div>

      {/* Form panel */}
      <div className="flex items-center justify-center bg-slate-100 px-4 py-12">
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          className="w-full max-w-md"
        >
          <div className="mb-6 flex items-center gap-3 lg:hidden">
            <span className="grid size-10 place-items-center rounded-xl bg-brand-700 text-white">
              <School className="size-5" aria-hidden="true" />
            </span>
            <div>
              <p className="font-bold text-slate-900">{schoolName}</p>
              <p className="text-xs text-slate-500">Grades 9 – 12 portal</p>
            </div>
          </div>

          {/* This page has no navigation bar, so it needs its own way back to the public site. */}
          <Link
            to="/"
            className="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-brand-700"
          >
            <ArrowLeft className="size-4" aria-hidden="true" />
            Back to home
          </Link>

          <div className="card p-6 sm:p-8">
            <h2 className="text-xl font-bold text-slate-900">
              {registering ? 'Register as a student' : 'Sign in'}
            </h2>
            <p className="mt-1 mb-6 text-sm text-slate-500">
              {registering
                ? 'The school reviews every registration and emails your sign-in details once yours is approved.'
                : 'Use the email address the school issued to you.'}
            </p>

            {sessionMessage && (
              <Alert variant="warning" className="mb-4" onDismiss={clearSessionMessage}>
                {sessionMessage}
              </Alert>
            )}

            {submittedNotice && (
              <Alert
                variant="success"
                title="Registration received"
                className="mb-4"
                onDismiss={() => setSubmittedNotice(null)}
              >
                {submittedNotice}
              </Alert>
            )}

            {error && !registering && (
              <Alert variant="error" title="Sign in failed" className="mb-4">
                {error}
              </Alert>
            )}

            {registering ? (
              <StudentRegistrationForm
                gradeLevels={overview.data?.gradeLevels ?? []}
                sections={overview.data?.sections ?? []}
                onSubmitted={(message) => {
                  setSubmittedNotice(message);
                  setRegistering(false);
                }}
              />
            ) : (
            <form onSubmit={handleSubmit} noValidate>
              <div className="mb-4">
                <label className="label" htmlFor="email">
                  Email address
                </label>
                <input
                  id="email"
                  type="email"
                  className="input"
                  autoComplete="username"
                  required
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="you@haladehighschool.edu"
                />
              </div>

              <div className="mb-6">
                <label className="label" htmlFor="password">
                  Password
                </label>
                <div className="relative">
                  <input
                    id="password"
                    type={showPassword ? 'text' : 'password'}
                    className="input pr-11"
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((value) => !value)}
                    className="absolute inset-y-0 right-0 grid w-11 place-items-center text-slate-400 hover:text-slate-600"
                    aria-label={showPassword ? 'Hide password' : 'Show password'}
                  >
                    {showPassword ? (
                      <EyeOff className="size-4.5" aria-hidden="true" />
                    ) : (
                      <Eye className="size-4.5" aria-hidden="true" />
                    )}
                  </button>
                </div>
              </div>

              <button type="submit" className="btn-primary w-full" disabled={submitting}>
                {submitting ? (
                  <Spinner className="size-4" />
                ) : (
                  <LogIn className="size-4" aria-hidden="true" />
                )}
                {submitting ? 'Signing in…' : 'Sign in'}
              </button>
            </form>
            )}

            {allowSelfRegistration && (
              <button
                type="button"
                onClick={() => {
                  setRegistering((value) => !value);
                  setError(null);
                }}
                className="mt-5 w-full rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-50"
              >
                {registering ? 'Back to sign in' : 'New student? Register here'}
              </button>
            )}

            <p className="mt-6 text-center text-xs text-slate-500">
              {allowSelfRegistration
                ? 'An administrator reviews each registration and issues your credentials; staff accounts are created for you.'
                : 'Accounts are created by the school administrator.'}
              {contactEmail && (
                <>
                  {' '}
                  <a
                    href={`mailto:${contactEmail}`}
                    className="inline-flex items-center gap-1 font-medium text-brand-700 hover:underline"
                  >
                    <Mail className="size-3" aria-hidden="true" />
                    Contact the office
                  </a>
                </>
              )}
            </p>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
