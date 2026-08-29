import { useState } from 'react';
import { UserPlus } from 'lucide-react';
import { authApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Spinner } from '../ui/Feedback';

/**
 * Public application for a place at the school.
 *
 * No password is collected: the school issues the sign-in address and a temporary password when
 * an administrator approves the application, and emails them to the address given here. Nothing
 * is signed in on submit, so this form reports back instead of redirecting.
 *
 * @param gradeLevels grades from /api/public/overview.
 * @param sections    sections from the same payload.
 * @param onSubmitted called with the API's receipt message once the queue accepts it.
 */
export default function StudentRegistrationForm({ gradeLevels = [], sections = [], onSubmitted }) {
  const [form, setForm] = useState({
    fullName: '',
    email: '',
    gradeLevelId: '',
    sectionId: '',
  });
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const update = (key) => (event) => setForm((prev) => ({ ...prev, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      const receipt = await authApi.registerStudent({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        gradeLevelId: Number(form.gradeLevelId),
        sectionId: Number(form.sectionId),
      });

      onSubmitted(
        receipt?.message ??
          'Your registration has been received and is waiting for the school to review it.',
      );
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate className="space-y-4">
      {error && (
        <Alert variant="error" title="Registration failed" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      <div>
        <label className="label" htmlFor="register-name">
          Full name
        </label>
        <input
          id="register-name"
          className="input"
          required
          maxLength={150}
          autoComplete="name"
          value={form.fullName}
          onChange={update('fullName')}
        />
      </div>

      <div>
        <label className="label" htmlFor="register-email">
          Your email address
        </label>
        <input
          id="register-email"
          type="email"
          className="input"
          required
          autoComplete="email"
          value={form.email}
          onChange={update('email')}
          placeholder="you@example.com"
        />
        <p className="mt-1 text-xs text-slate-500">
          Where the school will send your student number, school sign-in address and temporary
          password once your registration is approved.
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="label" htmlFor="register-grade">
            Grade
          </label>
          <select
            id="register-grade"
            className="input"
            required
            value={form.gradeLevelId}
            onChange={update('gradeLevelId')}
          >
            <option value="">Choose…</option>
            {gradeLevels.map((grade) => (
              <option key={grade.id} value={grade.id}>
                {grade.name}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="label" htmlFor="register-section">
            Section
          </label>
          <select
            id="register-section"
            className="input"
            required
            value={form.sectionId}
            onChange={update('sectionId')}
          >
            <option value="">Choose…</option>
            {sections.map((section) => (
              <option key={section.id} value={section.id}>
                {section.name}
              </option>
            ))}
          </select>
          <p className="mt-1 text-xs text-slate-500">The school may move you to another section.</p>
        </div>
      </div>

      <button type="submit" className="btn-primary w-full" disabled={submitting}>
        {submitting ? <Spinner className="size-4" /> : <UserPlus className="size-4" aria-hidden="true" />}
        {submitting ? 'Submitting…' : 'Submit registration'}
      </button>
    </form>
  );
}
