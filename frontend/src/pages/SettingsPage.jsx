import { useState } from 'react';
import { KeyRound, ShieldCheck } from 'lucide-react';
import { accountApi } from '../services/endpoints';
import { extractErrorMessage } from '../services/api';
import { useAuth } from '../context/AuthContext';
import { Alert, Badge, Spinner } from '../components/ui/Feedback';
import AdminSettings from './admin/AdminSettings';

/** Matches the API's Identity password policy, so the form fails before the request does. */
const MIN_PASSWORD_LENGTH = 8;

function ChangePasswordCard() {
  const [form, setForm] = useState({ current: '', next: '', confirm: '' });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);

  const update = (key) => (event) => setForm((prev) => ({ ...prev, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    setError(null);
    setNotice(null);

    if (form.next !== form.confirm) {
      setError('The new password and the confirmation do not match.');
      return;
    }

    if (form.next.length < MIN_PASSWORD_LENGTH) {
      setError(`Use at least ${MIN_PASSWORD_LENGTH} characters.`);
      return;
    }

    if (form.next === form.current) {
      setError('The new password has to be different from the current one.');
      return;
    }

    setSaving(true);
    try {
      const result = await accountApi.changePassword(form.current, form.next);
      setForm({ current: '', next: '', confirm: '' });
      setNotice(result?.message ?? 'Your password has been changed.');
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="card">
      <div className="border-b border-slate-200 px-5 py-4">
        <h2 className="flex items-center gap-2 font-semibold text-slate-900">
          <KeyRound className="size-5 text-brand-600" aria-hidden="true" />
          Password
        </h2>
        <p className="text-sm text-slate-500">
          Change the password you sign in with. You need your current one to do it.
        </p>
      </div>

      <form onSubmit={submit} className="space-y-4 p-5">
        {error && (
          <Alert variant="error" title="Could not change your password" onDismiss={() => setError(null)}>
            {error}
          </Alert>
        )}

        {notice && (
          <Alert variant="success" onDismiss={() => setNotice(null)}>
            {notice}
          </Alert>
        )}

        <div className="grid gap-4 sm:max-w-md">
          <div>
            <label htmlFor="current-password" className="label">
              Current password
            </label>
            <input
              id="current-password"
              className="input mt-1"
              type="password"
              autoComplete="current-password"
              required
              value={form.current}
              onChange={update('current')}
            />
          </div>

          <div>
            <label htmlFor="new-password" className="label">
              New password
            </label>
            <input
              id="new-password"
              className="input mt-1"
              type="password"
              autoComplete="new-password"
              required
              minLength={MIN_PASSWORD_LENGTH}
              value={form.next}
              onChange={update('next')}
            />
            <p className="mt-1 text-xs text-slate-500">
              At least {MIN_PASSWORD_LENGTH} characters, with an upper and lower case letter, a
              digit and a symbol.
            </p>
          </div>

          <div>
            <label htmlFor="confirm-password" className="label">
              Confirm new password
            </label>
            <input
              id="confirm-password"
              className="input mt-1"
              type="password"
              autoComplete="new-password"
              required
              value={form.confirm}
              onChange={update('confirm')}
            />
          </div>
        </div>

        <button type="submit" className="btn-primary" disabled={saving}>
          {saving ? <Spinner className="size-4" /> : <KeyRound className="size-4" aria-hidden="true" />}
          Change password
        </button>
      </form>
    </section>
  );
}

/**
 * One Settings destination for every role, so the navigation bar carries a single link.
 *
 * A student or a teacher sees their own account only. An administrator sees the same card plus
 * the school-wide settings, which is where the academic year, pass mark and self-registration
 * switch live.
 */
export default function SettingsPage() {
  const { user, roles, isAdmin } = useAuth();
  const identifier = user?.studentIdNumber ?? user?.employeeId;

  return (
    <div className="space-y-6">
      <section className="card flex flex-wrap items-center gap-4 p-5">
        <span className="grid size-12 shrink-0 place-items-center rounded-xl bg-brand-100 text-brand-700">
          <ShieldCheck className="size-6" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="font-semibold text-slate-900">{user?.fullName}</p>
          <p className="truncate text-sm text-slate-500">{user?.email}</p>
        </div>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          {roles.map((role) => (
            <Badge key={role} tone="brand">
              {role}
            </Badge>
          ))}
          {identifier && (
            <span className="rounded-full bg-slate-100 px-2.5 py-1 font-mono text-xs text-slate-600">
              {identifier}
            </span>
          )}
        </div>
      </section>

      <ChangePasswordCard />

      {isAdmin && <AdminSettings />}
    </div>
  );
}
