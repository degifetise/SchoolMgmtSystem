import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  BookOpenCheck,
  ClipboardList,
  Eye,
  Save,
  Send,
  Users,
} from 'lucide-react';
import { assessmentApi, markApi, teacherApi } from '../../services/endpoints';
import { extractErrorMessage } from '../../services/api';
import { Alert, Badge, EmptyState, ErrorState, LoadingPanel, Spinner } from '../../components/ui/Feedback';

/** The DisplayName column of the AssessmentTypes lookup table, so both roles see one wording. */
const TYPE_LABELS = {
  Quiz: 'Quiz',
  Assignment: 'Assignment',
  Test: 'Test',
  MidExam: 'Mid Exam',
  FinalExam: 'Final Exam',
};

export default function EnterMarks() {
  /* The class rosters link here with ?assignment=<id> so a teacher arrives on the class they
     were just looking at instead of picking it out of the list a second time. */
  const [searchParams] = useSearchParams();
  const requestedAssignment = searchParams.get('assignment');

  const [assignments, setAssignments] = useState([]);
  const [assignmentId, setAssignmentId] = useState('');
  const [assessments, setAssessments] = useState([]);
  const [assessmentId, setAssessmentId] = useState('');
  const [gradebook, setGradebook] = useState(null);

  const [drafts, setDrafts] = useState({});
  const [loading, setLoading] = useState(true);
  const [loadingAssessments, setLoadingAssessments] = useState(false);
  const [loadingGradebook, setLoadingGradebook] = useState(false);
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [error, setError] = useState(null);
  const [classesError, setClassesError] = useState(null);
  const [notice, setNotice] = useState(null);

  const assignment = assignments.find((item) => String(item.id) === String(assignmentId));

  /* Tracked apart from the general error banner: an empty list and a failed request look
     identical downstream, and telling a teacher they have no classes when the request simply
     failed would be worse than showing nothing at all. */
  const loadClasses = useCallback(async () => {
    setLoading(true);
    setClassesError(null);

    try {
      const data = await teacherApi.myClasses();
      setAssignments(data);

      const requested = data.find((item) => String(item.id) === requestedAssignment);
      if (requested) setAssignmentId(String(requested.id));
      else if (data.length === 1) setAssignmentId(String(data[0].id));
    } catch (err) {
      setClassesError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, [requestedAssignment]);

  useEffect(() => {
    loadClasses();
  }, [loadClasses]);

  /* Assessments depend on the chosen class. The API also returns assessments with a
     null section, which apply to every section taking the subject. */
  useEffect(() => {
    if (!assignment) {
      setAssessments([]);
      setAssessmentId('');
      return undefined;
    }

    let cancelled = false;
    setLoadingAssessments(true);
    setAssessmentId('');
    setGradebook(null);

    (async () => {
      try {
        const data = await assessmentApi.list({
          subjectId: assignment.subjectId,
          sectionId: assignment.sectionId,
        });
        if (!cancelled) setAssessments(data);
      } catch (err) {
        if (!cancelled) setError(err.friendlyMessage ?? extractErrorMessage(err));
      } finally {
        if (!cancelled) setLoadingAssessments(false);
      }
    })();

    return () => { cancelled = true; };
  }, [assignment]);

  const loadGradebook = useCallback(async (id) => {
    setLoadingGradebook(true);
    setError(null);

    try {
      const data = await markApi.gradebook(id);
      setGradebook(data);
      setDrafts(
        Object.fromEntries(
          data.rows.map((row) => [
            row.studentId,
            { score: row.score ?? '', remark: row.remark ?? '' },
          ]),
        ),
      );
    } catch (err) {
      setGradebook(null);
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setLoadingGradebook(false);
    }
  }, []);

  useEffect(() => {
    if (assessmentId) loadGradebook(assessmentId);
  }, [assessmentId, loadGradebook]);

  const maxScore = gradebook?.maxScore ?? 0;

  /* A score above MaxScore is rejected by a database trigger, so it is caught here
     rather than letting the whole batch fail on the server. */
  const invalidRows = useMemo(
    () =>
      Object.entries(drafts).filter(([, draft]) => {
        if (draft.score === '' || draft.score === null) return false;
        const value = Number(draft.score);
        return Number.isNaN(value) || value < 0 || value > maxScore;
      }),
    [drafts, maxScore],
  );

  const filledEntries = useMemo(
    () =>
      Object.entries(drafts)
        .filter(([, draft]) => draft.score !== '' && draft.score !== null)
        .map(([studentId, draft]) => ({
          studentId: Number(studentId),
          score: Number(draft.score),
          remark: draft.remark?.trim() ? draft.remark.trim() : null,
        })),
    [drafts],
  );

  function updateDraft(studentId, key, value) {
    setDrafts((prev) => ({ ...prev, [studentId]: { ...prev[studentId], [key]: value } }));
  }

  async function save({ publish }) {
    if (!gradebook || filledEntries.length === 0) return;

    publish ? setPublishing(true) : setSaving(true);
    setError(null);
    setNotice(null);

    try {
      const result = await markApi.saveBulk({
        assessmentId: gradebook.assessmentId,
        entries: filledEntries,
        isPublished: publish,
      });

      setNotice(
        `Saved ${result.created} new and ${result.updated} updated score${
          result.created + result.updated === 1 ? '' : 's'
        }${publish ? ' and published them to students.' : '. Students cannot see them until you publish.'}`,
      );

      await loadGradebook(gradebook.assessmentId);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setSaving(false);
      setPublishing(false);
    }
  }

  async function togglePublish(isPublished) {
    if (!gradebook) return;

    setPublishing(true);
    setError(null);
    setNotice(null);

    try {
      const result = await markApi.publish(gradebook.assessmentId, isPublished);
      setNotice(
        `${isPublished ? 'Published' : 'Unpublished'} ${result.affectedMarks} mark${
          result.affectedMarks === 1 ? '' : 's'
        }.`,
      );
      await loadGradebook(gradebook.assessmentId);
    } catch (err) {
      setError(err.friendlyMessage ?? extractErrorMessage(err));
    } finally {
      setPublishing(false);
    }
  }

  if (loading) return <LoadingPanel label="Loading your classes…" />;

  if (classesError) {
    return (
      <ErrorState
        title="Could not load your classes"
        message={classesError}
        onRetry={loadClasses}
      />
    );
  }

  if (assignments.length === 0) {
    return (
      <EmptyState
        icon={BookOpenCheck}
        title="No classes assigned yet"
        description="An administrator needs to assign you a subject and section before you can enter marks."
      />
    );
  }

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-slate-900">Enter marks</h1>
        <p className="text-sm text-slate-500">
          Choose a class and assessment, type the scores, then publish when you are ready for
          students to see them.
        </p>
      </header>

      {error && (
        <Alert variant="error" title="Could not complete that" onDismiss={() => setError(null)}>
          {error}
        </Alert>
      )}

      {notice && (
        <Alert variant="success" onDismiss={() => setNotice(null)}>
          {notice}
        </Alert>
      )}

      {/* Filters */}
      <section className="card grid gap-4 p-5 sm:grid-cols-2">
        <div>
          <label className="label" htmlFor="assignment">
            Class
          </label>
          <select
            id="assignment"
            className="input"
            value={assignmentId}
            onChange={(event) => setAssignmentId(event.target.value)}
          >
            <option value="">Select a subject and section…</option>
            {assignments.map((item) => (
              <option key={item.id} value={item.id}>
                {item.subjectCode} · {item.subjectName} — {item.gradeLevelName} {item.sectionName}
              </option>
            ))}
          </select>
          {assignment && (
            <p className="mt-1.5 text-xs text-slate-500">
              <Users className="mr-1 inline size-3" aria-hidden="true" />
              {assignment.studentCount} student{assignment.studentCount === 1 ? '' : 's'} · academic
              year {assignment.academicYear}
            </p>
          )}
        </div>

        <div>
          <label className="label" htmlFor="assessment">
            Assessment
          </label>
          <select
            id="assessment"
            className="input"
            value={assessmentId}
            onChange={(event) => setAssessmentId(event.target.value)}
            disabled={!assignment || loadingAssessments}
          >
            <option value="">
              {loadingAssessments
                ? 'Loading assessments…'
                : assessments.length
                  ? 'Select an assessment…'
                  : 'No assessments for this class yet'}
            </option>
            {assessments.map((item) => (
              <option key={item.id} value={item.id}>
                {item.title} — {TYPE_LABELS[item.assessmentType] ?? item.assessmentType} (
                {item.weightPercentage}%, out of {item.maxScore})
              </option>
            ))}
          </select>
        </div>
      </section>

      {loadingGradebook && <LoadingPanel label="Loading the class list…" />}

      {gradebook && !loadingGradebook && (
        <motion.section
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          className="card"
        >
          <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 px-5 py-4">
            <div className="min-w-0">
              <h2 className="truncate font-semibold text-slate-900">{gradebook.assessmentTitle}</h2>
              <p className="text-xs text-slate-500">
                {gradebook.subjectCode} · {gradebook.subjectName}
                {gradebook.sectionName ? ` · ${gradebook.sectionName}` : ' · all sections'}
              </p>
            </div>

            <div className="ml-auto flex flex-wrap items-center gap-2">
              <Badge tone="brand">
                {TYPE_LABELS[gradebook.assessmentType] ?? gradebook.assessmentType}
              </Badge>
              <Badge tone="slate">Weight {gradebook.weightPercentage}%</Badge>
              <Badge tone="slate">Out of {gradebook.maxScore}</Badge>
              <Badge tone={gradebook.markedCount === gradebook.totalStudents ? 'green' : 'amber'}>
                {gradebook.markedCount}/{gradebook.totalStudents} marked
              </Badge>
            </div>
          </div>

          {invalidRows.length > 0 && (
            <div className="px-5 pt-4">
              <Alert variant="warning" title="Fix the highlighted scores">
                {invalidRows.length} score{invalidRows.length === 1 ? '' : 's'} fall outside 0 –{' '}
                {maxScore}. The database rejects a score above the maximum, so saving is blocked
                until they are corrected.
              </Alert>
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full min-w-2xl text-left text-sm">
              <thead className="bg-slate-50 text-xs tracking-wide text-slate-500 uppercase">
                <tr>
                  <th scope="col" className="px-5 py-3 font-semibold">Student</th>
                  <th scope="col" className="px-5 py-3 font-semibold">ID</th>
                  <th scope="col" className="px-5 py-3 font-semibold">Section</th>
                  <th scope="col" className="w-36 px-5 py-3 font-semibold">
                    Score / {maxScore}
                  </th>
                  <th scope="col" className="px-5 py-3 font-semibold">Remark</th>
                  <th scope="col" className="px-5 py-3 font-semibold">Visible</th>
                </tr>
              </thead>
              <tbody>
                {gradebook.rows.map((row) => {
                  const draft = drafts[row.studentId] ?? { score: '', remark: '' };
                  const value = draft.score === '' ? null : Number(draft.score);
                  const invalid =
                    draft.score !== '' && (Number.isNaN(value) || value < 0 || value > maxScore);

                  return (
                    <tr key={row.studentId} className="border-t border-slate-100">
                      <td className="px-5 py-2.5 font-medium text-slate-900">{row.studentName}</td>
                      <td className="px-5 py-2.5 text-slate-600">{row.studentIdNumber}</td>
                      <td className="px-5 py-2.5 text-slate-600">{row.sectionName}</td>
                      <td className="px-5 py-2">
                        <input
                          type="number"
                          inputMode="decimal"
                          step="0.01"
                          min="0"
                          max={maxScore}
                          className={`input ${invalid ? 'border-rose-400 focus:border-rose-500 focus:ring-rose-200' : ''}`}
                          value={draft.score}
                          onChange={(event) => updateDraft(row.studentId, 'score', event.target.value)}
                          aria-label={`Score for ${row.studentName}`}
                          aria-invalid={invalid}
                          placeholder="—"
                        />
                      </td>
                      <td className="px-5 py-2">
                        <input
                          type="text"
                          maxLength={300}
                          className="input"
                          value={draft.remark}
                          onChange={(event) => updateDraft(row.studentId, 'remark', event.target.value)}
                          aria-label={`Remark for ${row.studentName}`}
                          placeholder="Optional"
                        />
                      </td>
                      <td className="px-5 py-2.5">
                        {row.markId ? (
                          <Badge tone={row.isPublished ? 'green' : 'amber'}>
                            {row.isPublished ? 'Published' : 'Draft'}
                          </Badge>
                        ) : (
                          <Badge tone="slate">Unmarked</Badge>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="flex flex-wrap items-center gap-3 border-t border-slate-200 px-5 py-4">
            <p className="text-sm text-slate-500">
              {filledEntries.length} of {gradebook.totalStudents} score
              {filledEntries.length === 1 ? '' : 's'} ready to save. Blank rows are left untouched.
            </p>

            <div className="ml-auto flex flex-wrap gap-2">
              <button
                type="button"
                className="btn-secondary"
                onClick={() => togglePublish(false)}
                disabled={publishing || gradebook.markedCount === 0}
                title="Hide these marks from students again"
              >
                <Eye className="size-4" aria-hidden="true" />
                Unpublish
              </button>

              <button
                type="button"
                className="btn-secondary"
                onClick={() => save({ publish: false })}
                disabled={saving || publishing || invalidRows.length > 0 || filledEntries.length === 0}
              >
                {saving ? <Spinner className="size-4" /> : <Save className="size-4" aria-hidden="true" />}
                Save as draft
              </button>

              <button
                type="button"
                className="btn-primary"
                onClick={() => save({ publish: true })}
                disabled={saving || publishing || invalidRows.length > 0 || filledEntries.length === 0}
              >
                {publishing ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />}
                Save and publish
              </button>
            </div>
          </div>
        </motion.section>
      )}

      {!gradebook && !loadingGradebook && assignment && assessments.length > 0 && (
        <EmptyState
          icon={ClipboardList}
          title="Pick an assessment"
          description="Choose one of this class's assessments to load the mark sheet."
        />
      )}
    </div>
  );
}
