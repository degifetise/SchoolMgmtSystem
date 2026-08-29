import { useCallback, useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import {
  Check,
  ClipboardCheck,
  Copy,
  Inbox,
  KeyRound,
  RefreshCw,
  UserPlus,
  X,
} from 'lucide-react';
import { registrationApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { useSchoolInfo } from '../../context/SchoolInfoContext';
import { Alert, Badge, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';
import { PAGE_SIZE, Pager, PeopleTable, RowActions, SearchBox, totalPagesOf } from './adminShared';

const REVIEW_TABS = [
  { key: 'Pending', label: 'Awaiting review' },
  { key: 'Approved', label: 'Approved' },
  { key: 'Rejected', label: 'Declined' },
];

function NewTeacherForm({ onCreated, onError }) {
  const [form, setForm] = useState({ fullName: '', email: '', specialization: '' });
  const [saving, setSaving] = useState(false);

  const update = (key) => (event) => setForm((prev) => ({ ...prev, [key]: event.target.value }));

  async function submit(event) {
    event.preventDefault();
    setSaving(true);
    onError(null);

    try {
      const result = await teacherApi.create({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        specialization: form.specialization.trim() || null,
      });
      setForm({ fullName: '', email: '', specialization: '' });
      onCreated(result);
    } catch (err) {
      onError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} className="grid gap-3 border-t border-slate-200 p-4 sm:grid-cols-4">
      <input
        className="input"
        placeholder="Full name"
        required
        value={form.fullName}
        onChange={update('fullName')}
        aria-label="Full name"
      />
      <input
        className="input"
        type="email"
        placeholder="teacher@haladehighschool.edu"
        required
        value={form.email}
        onChange={update('email')}
        aria-label="Email"
      />
      <input
        className="input"
        placeholder="Specialization (optional)"
        value={form.specialization}
        onChange={update('specialization')}
        aria-label="Specialization"
      />
      <button type="submit" className="btn-primary" disabled={saving}>
        {saving ? <Spinner className="size-4" /> : <UserPlus className="size-4" aria-hidden="true" />}
        Add teacher
      </button>
    </form>
  );
}

/**
 * The credentials an approval issued. Kept on screen until dismissed rather than shown as a
 * passing toast: the temporary password is not stored in readable form, so if it is lost here
 * the only way back is a password reset.
 */
function IssuedCredentials({ issued, onDismiss }) {
  const [copied, setCopied] = useState(false);

  const summary = [
    `Student: ${issued.fullName}`,
    `Student number: ${issued.studentIdNumber}`,
    `Sign-in address: ${issued.issuedEmail}`,
    `Temporary password: ${issued.temporaryPassword}`,
    `Class: ${issued.gradeLevelName} ${issued.sectionName}`,
  ].join('\n');

  async function copy() {
    try {
      await navigator.clipboard.writeText(summary);
      setCopied(true);
    } catch {
      // Clipboard access can be refused; the values stay selectable on screen either way.
      setCopied(false);
    }
  }

  return (
    <motion.section
      initial={{ opacity: 0, y: -6 }}
      animate={{ opacity: 1, y: 0 }}
      className="card border-emerald-300 bg-emerald-50/60 p-5"
    >
      <div className="flex flex-wrap items-start gap-3">
        <KeyRound className="size-5 shrink-0 text-emerald-700" aria-hidden="true" />
        <div className="min-w-0 flex-1">
          <h2 className="font-semibold text-emerald-900">
            {issued.fullName} is enrolled — send these credentials now
          </h2>
          <p className="mt-1 text-sm text-emerald-800">{issued.message}</p>

          <dl className="mt-3 grid gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
                Student number
              </dt>
              <dd className="font-mono text-slate-900">{issued.studentIdNumber}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
                Sign-in address
              </dt>
              <dd className="font-mono break-all text-slate-900">{issued.issuedEmail}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
                Temporary password
              </dt>
              <dd className="font-mono text-slate-900">{issued.temporaryPassword}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold uppercase tracking-wide text-emerald-700">
                Send to
              </dt>
              <dd className="break-all text-slate-900">{issued.contactEmail}</dd>
            </div>
          </dl>

          <div className="mt-4 flex flex-wrap items-center gap-2">
            <button type="button" className="btn-secondary" onClick={copy}>
              <Copy className="size-4" aria-hidden="true" />
              {copied ? 'Copied' : 'Copy details'}
            </button>
            <button
              type="button"
              className="text-sm font-semibold text-emerald-800 underline-offset-2 hover:underline"
              onClick={onDismiss}
            >
              I have sent these
            </button>
          </div>
        </div>
      </div>
    </motion.section>
  );
}

/** One applicant, with the class they asked for and how full it is. */
function RegistrationCard({ row, busy, onApprove, onReject }) {
  const [note, setNote] = useState('');
  const [noteOpen, setNoteOpen] = useState(false);
  const isPending = row.status === 'Pending';
  const full = row.sectionOccupancy >= row.sectionCapacity;

  return (
    <motion.li
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-lg border border-slate-200 p-4"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-semibold text-slate-900">{row.fullName}</p>
          <p className="text-sm text-slate-500">{row.contactEmail}</p>
          <p className="mt-1 text-xs text-slate-500">
            Applied for {row.gradeLevelName} {row.sectionName} ·{' '}
            {new Date(row.submittedAt).toLocaleDateString()}
            {row.studentIdNumber ? (
              <>
                {' · enrolled as '}
                <span className="font-mono">{row.studentIdNumber}</span>
              </>
            ) : null}
          </p>
          {row.issuedEmail && (
            <p className="mt-1 text-xs text-slate-500">
              Signs in as <span className="font-mono break-all">{row.issuedEmail}</span>
            </p>
          )}
        </div>

        <div className="flex flex-col items-end gap-1">
          <Badge tone={isPending ? 'amber' : row.status === 'Approved' ? 'green' : 'red'}>
            {row.status}
          </Badge>
          {/* Seats matter at the moment of approval, so it is shown next to the decision. */}
          <span className={`text-xs ${full ? 'font-semibold text-rose-600' : 'text-slate-500'}`}>
            {row.sectionOccupancy}/{row.sectionCapacity} seats taken
          </span>
        </div>
      </div>

      {row.reviewedAt && (
        <p className="mt-3 rounded-md bg-slate-50 px-3 py-2 text-xs text-slate-600">
          {row.status} on {new Date(row.reviewedAt).toLocaleString()}
          {row.reviewedByName ? ` by ${row.reviewedByName}` : ''}
          {row.reviewNote ? ` — ${row.reviewNote}` : ''}
        </p>
      )}

      {isPending && (
        <div className="mt-3 space-y-2">
          {noteOpen && (
            <input
              className="input"
              placeholder="Note for the record (optional)"
              value={note}
              maxLength={300}
              onChange={(event) => setNote(event.target.value)}
              aria-label="Review note"
            />
          )}

          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="btn-primary"
              disabled={busy}
              onClick={() => onApprove(row, note.trim() || null)}
            >
              {busy ? <Spinner className="size-4" /> : <Check className="size-4" aria-hidden="true" />}
              Approve
            </button>
            <button
              type="button"
              disabled={busy}
              onClick={() => onReject(row, note.trim() || null)}
              className="inline-flex items-center gap-2 rounded-lg border border-rose-200 bg-white px-4 py-2 text-sm font-semibold text-rose-700 transition-colors hover:bg-rose-50 disabled:opacity-50"
            >
              <X className="size-4" aria-hidden="true" />
              Decline
            </button>
            <button
              type="button"
              className="text-xs font-semibold text-slate-500 underline-offset-2 hover:underline"
              onClick={() => setNoteOpen((open) => !open)}
            >
              {noteOpen ? 'Hide note' : 'Add a note'}
            </button>
          </div>
        </div>
      )}
    </motion.li>
  );
}

/**
 * Accounts: the registration approval queue, and provisioning for staff.
 *
 * Two jobs, one page, because both answer "who is allowed into the portal". Student enrolment
 * figures and the student roster live on the Students page.
 */
export default function AdminAccounts() {
  const { allowSelfRegistration } = useSchoolInfo();

  const [reviewTab, setReviewTab] = useState('Pending');
  const [registrations, setRegistrations] = useState([]);
  const [pendingCount, setPendingCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [queueError, setQueueError] = useState(null);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [issued, setIssued] = useState(null);
  const [busyId, setBusyId] = useState(null);

  const [teachers, setTeachers] = useState({ items: [], totalCount: 0, page: 1 });
  const [teacherSearch, setTeacherSearch] = useState('');
  const [teacherPage, setTeacherPage] = useState(1);
  const [teachersLoading, setTeachersLoading] = useState(false);
  const [showCreate, setShowCreate] = useState(false);

  const loadQueue = useCallback(async () => {
    setLoading(true);
    setQueueError(null);

    try {
      // The pending count is always fetched, so the tab badge is right whichever tab is open.
      const [rows, pending] = await Promise.all([
        registrationApi.list(reviewTab),
        reviewTab === 'Pending' ? null : registrationApi.list('Pending'),
      ]);

      setRegistrations(rows);
      setPendingCount(reviewTab === 'Pending' ? rows.length : pending.length);
    } catch (err) {
      setQueueError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [reviewTab]);

  useEffect(() => {
    loadQueue();
  }, [loadQueue]);

  const loadTeachers = useCallback(async () => {
    setTeachersLoading(true);
    try {
      setTeachers(
        await teacherApi.list({
          page: teacherPage,
          pageSize: PAGE_SIZE,
          includeInactive: true,
          search: teacherSearch || undefined,
        }),
      );
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setTeachersLoading(false);
    }
  }, [teacherPage, teacherSearch]);

  useEffect(() => {
    loadTeachers();
  }, [loadTeachers]);

  async function review(row, note, approve) {
    setBusyId(row.id);
    setError(null);
    try {
      if (approve) {
        // The response carries the only copy of the temporary password, so it goes to the
        // credentials panel rather than a notice that can be missed.
        setIssued(await registrationApi.approve(row.id, note));
        setNotice(null);
      } else {
        await registrationApi.reject(row.id, note);
        setNotice(`${row.fullName}'s registration was declined. No account was created.`);
      }
      await loadQueue();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function toggleTeacher(teacher) {
    setBusyId(`teacher-${teacher.id}`);
    setError(null);
    try {
      await teacherApi.setStatus(teacher.id, !teacher.isActive);
      setNotice(`${teacher.fullName} is now ${teacher.isActive ? 'deactivated' : 'active'}.`);
      await loadTeachers();
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  async function resetTeacherPassword(teacher) {
    setBusyId(`teacher-${teacher.id}`);
    setError(null);
    try {
      const { temporaryPassword } = await teacherApi.resetPassword(teacher.id);
      setNotice(`Temporary password for ${teacher.fullName}: ${temporaryPassword}`);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="space-y-6">
      {error && (
        <Alert variant="error" title="Something went wrong" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      {notice && (
        <Alert variant="success" onDismiss={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      {issued && <IssuedCredentials issued={issued} onDismiss={() => setIssued(null)} />}

      {/* Registration approvals */}
      <section className="card">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="flex items-center gap-2 font-semibold text-slate-900">
              <ClipboardCheck className="size-5 text-brand-600" aria-hidden="true" />
              Registration approvals
              {pendingCount > 0 && (
                <span className="rounded-full bg-amber-100 px-2 py-0.5 text-xs font-bold text-amber-800">
                  {pendingCount}
                </span>
              )}
            </h2>
            <p className="text-sm text-slate-500">
              Approving issues a student number, a school sign-in address and a temporary password.
            </p>
          </div>

          <button type="button" onClick={loadQueue} className="btn-secondary ml-auto" title="Refresh">
            <RefreshCw className="size-4" aria-hidden="true" />
            Refresh
          </button>
        </div>

        <div className="flex flex-wrap items-center gap-3 px-5 pt-4">
          <div className="flex rounded-lg bg-slate-100 p-1" role="tablist">
            {REVIEW_TABS.map(({ key, label }) => (
              <button
                key={key}
                type="button"
                role="tab"
                aria-selected={reviewTab === key}
                onClick={() => setReviewTab(key)}
                className={`rounded-md px-4 py-1.5 text-sm font-semibold transition-colors ${
                  reviewTab === key ? 'bg-white text-brand-700 shadow-sm' : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          {!allowSelfRegistration && (
            <p className="text-xs text-slate-500">
              Self-registration is currently switched off in Settings, so no new applications will
              arrive.
            </p>
          )}
        </div>

        <div className="p-5">
          {loading ? (
            <LoadingPanel label="Loading registrations…" />
          ) : queueError ? (
            <ErrorState
              title="Could not load the approval queue"
              message={queueError}
              onRetry={loadQueue}
            />
          ) : registrations.length === 0 ? (
            <EmptyState
              icon={Inbox}
              title={
                reviewTab === 'Pending'
                  ? 'Nothing waiting for review'
                  : `No ${reviewTab.toLowerCase()} registrations`
              }
              description={
                reviewTab === 'Pending'
                  ? 'Applications appear here the moment they are submitted.'
                  : undefined
              }
            />
          ) : (
            <ul className="space-y-3">
              {registrations.map((row) => (
                <RegistrationCard
                  key={row.id}
                  row={row}
                  busy={busyId === row.id}
                  onApprove={(target, note) => review(target, note, true)}
                  onReject={(target, note) => review(target, note, false)}
                />
              ))}
            </ul>
          )}
        </div>
      </section>

      {/* Staff provisioning */}
      <section className="card">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
          <div>
            <h2 className="font-semibold text-slate-900">Staff accounts</h2>
            <p className="text-sm text-slate-500">
              Provision teachers, reset a password, or switch an account off.
            </p>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            <SearchBox
              value={teacherSearch}
              onSearch={(value) => {
                setTeacherPage(1);
                setTeacherSearch(value);
              }}
            />
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setShowCreate((value) => !value)}
              aria-expanded={showCreate}
            >
              <UserPlus className="size-4" aria-hidden="true" />
              New teacher
            </button>
          </div>
        </div>

        {showCreate && (
          <NewTeacherForm
            onError={setError}
            onCreated={async (result) => {
              setNotice(
                result.temporaryPassword
                  ? `${result.teacher.fullName} added as ${result.teacher.employeeId}. Temporary password: ${result.temporaryPassword}`
                  : `${result.teacher.fullName} added as ${result.teacher.employeeId}.`,
              );
              await loadTeachers();
            }}
          />
        )}

        <div className="overflow-x-auto">
          <PeopleTable
            loading={teachersLoading}
            rows={teachers.items}
            emptyTitle="No teachers match this search"
            columns={['Teacher', 'Employee ID', 'Specialization', 'Classes', 'Status', '']}
            renderRow={(teacher) => (
              <tr key={teacher.id} className="border-t border-slate-100">
                <td className="px-5 py-3">
                  <p className="font-medium text-slate-900">{teacher.fullName}</p>
                  <p className="text-xs text-slate-500">{teacher.email ?? 'No login'}</p>
                </td>
                <td className="px-5 py-3 text-slate-600">{teacher.employeeId}</td>
                <td className="px-5 py-3 text-slate-600">{teacher.specialization ?? '—'}</td>
                <td className="px-5 py-3 text-slate-600">{teacher.assignmentCount}</td>
                <td className="px-5 py-3">
                  <Badge tone={teacher.isActive ? 'green' : 'red'}>
                    {teacher.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </td>
                <td className="px-5 py-3">
                  <RowActions
                    busy={busyId === `teacher-${teacher.id}`}
                    isActive={teacher.isActive}
                    hasLogin={teacher.hasLogin}
                    onToggle={() => toggleTeacher(teacher)}
                    onReset={() => resetTeacherPassword(teacher)}
                  />
                </td>
              </tr>
            )}
          />
        </div>

        <Pager
          page={teacherPage}
          totalPages={totalPagesOf(teachers)}
          totalCount={teachers.totalCount}
          onChange={setTeacherPage}
        />
      </section>
    </div>
  );
}
