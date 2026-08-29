import { useCallback, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import {
  Award,
  CalendarDays,
  ChevronDown,
  GraduationCap,
  Percent,
  TrendingUp,
} from 'lucide-react';
import { studentApi } from '../../services/endpoints';
import { useApiResource } from '../../hooks/useApiResource';
import { Badge, EmptyState, ErrorState, LoadingPanel } from '../../components/ui/Feedback';

/** Mirrors the CASE expression in vw_StudentSubjectPerformance. */
const GRADE_TONES = { A: 'green', B: 'green', C: 'amber', D: 'amber', F: 'red' };


const GRADE_POINTS = { A: 4, B: 3, C: 2, D: 1, F: 0 };

function formatScore(value) {
  if (value === null || value === undefined) return '—';
  return Number(value).toFixed(2);
}

function SummaryCard({ icon: Icon, label, value, hint, tone = 'brand' }) {
  const tones = {
    brand: 'bg-brand-100 text-brand-700',
    green: 'bg-emerald-100 text-emerald-700',
    amber: 'bg-amber-100 text-amber-700',
    slate: 'bg-slate-100 text-slate-600',
  };

  return (
    <div className="card p-5">
      <div className="flex items-center gap-3">
        <span className={`grid size-10 place-items-center rounded-lg ${tones[tone]}`}>
          <Icon className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</p>
          <p className="text-2xl font-bold text-slate-900">{value}</p>
        </div>
      </div>
      {hint && <p className="mt-3 text-xs text-slate-500">{hint}</p>}
    </div>
  );
}

/** Stacked bar showing how each weighted component contributed to the subject total. */
function WeightBreakdown({ subject, weights }) {
  const parts = [
    { key: 'Quiz', value: subject.quizScore, className: 'bg-brand-400' },
    { key: 'Assignment', value: subject.assignmentScore, className: 'bg-brand-500' },
    { key: 'Test', value: subject.testScore, className: 'bg-brand-600' },
    { key: 'MidExam', value: subject.midExamScore, className: 'bg-brand-700' },
    { key: 'FinalExam', value: subject.finalExamScore, className: 'bg-brand-800' },
  ];

  return (
    <div>
      <div
        className="flex h-2.5 w-full overflow-hidden rounded-full bg-slate-200"
        role="img"
        aria-label={`Weighted total ${formatScore(subject.totalScore)} out of 100`}
      >
        {parts.map(({ key, value, className }) =>
          value ? (
            <div
              key={key}
              className={className}
              style={{ width: `${Math.min(100, Number(value))}%` }}
              title={`${key}: ${formatScore(value)} points`}
            />
          ) : null,
        )}
      </div>

      <dl className="mt-3 grid grid-cols-2 gap-2 text-xs sm:grid-cols-5">
        {parts.map(({ key, value }) => {
          const weight = weights.find((item) => item.name === key);
          return (
            <div key={key} className="rounded-lg bg-slate-50 px-2.5 py-2">
              <dt className="text-slate-500">
                {weight?.displayName ?? key}
                {weight && <span className="text-slate-400"> · {weight.weightPercentage}%</span>}
              </dt>
              <dd className="font-semibold text-slate-800">
                {value === null || value === undefined ? 'Not marked' : `${formatScore(value)} pts`}
              </dd>
            </div>
          );
        })}
      </dl>
    </div>
  );
}

export default function MyResults() {
  const [expanded, setExpanded] = useState(null);

  // One call: the endpoint returns the subject rows together with the weighting they were
  // derived from, and a component score means nothing without the share it carries.
  const fetchResults = useCallback(() => studentApi.myResults(), []);

  const { data: reportCard, error, loading, reload, reloading } = useApiResource(fetchResults);

  const weights = reportCard?.gradingWeights ?? [];

  // GPA is the one figure the API does not carry: the 4.0 mapping is a presentation choice.
  const stats = useMemo(() => {
    const subjects = reportCard?.subjects ?? [];
    if (subjects.length === 0) return null;

    const summary = reportCard.summary ?? {};
    const points = subjects.reduce(
      (sum, subject) => sum + (GRADE_POINTS[subject.letterGrade] ?? 0),
      0,
    );

    return {
      passed: summary.subjectsPassed ?? 0,
      failed: subjects.length - (summary.subjectsPassed ?? 0),
      gpa: (points / subjects.length).toFixed(2),
      bestName: summary.strongestSubject,
      bestTotal: summary.strongestSubjectTotal,
    };
  }, [reportCard]);

  if (loading) return <LoadingPanel label="Loading your report card…" />;

  if (error) {
    return (
      <ErrorState
        title="Could not load your results"
        message={error}
        onRetry={reload}
        retrying={reloading}
      />
    );
  }

  const subjects = reportCard?.subjects ?? [];

  return (
    <div className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">My results</h1>
          <p className="text-sm text-slate-500">
            {reportCard?.studentName} · {reportCard?.studentIdNumber} · {reportCard?.gradeLevelName}{' '}
            {reportCard?.sectionName}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {reportCard?.academicYear && (
            <Badge tone="slate">
              <CalendarDays className="size-3" aria-hidden="true" />
              {reportCard.academicYear}
            </Badge>
          )}
          <Badge tone="slate">
            <Percent className="size-3" aria-hidden="true" />
            Pass mark {formatScore(reportCard?.passMarkPercentage)}
          </Badge>
        </div>
      </header>

      {subjects.length === 0 ? (
        <EmptyState
          icon={GraduationCap}
          title="No published results yet"
          description="Your teachers have not published any marks for this academic year. Results appear here as soon as they do."
        />
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <SummaryCard
              icon={TrendingUp}
              label="Weighted average"
              value={formatScore(reportCard.summary?.weightedAverage)}
              hint="Mean of every subject's weighted total, out of 100"
            />
            <SummaryCard
              icon={Award}
              label="GPA (4.0 scale)"
              value={stats.gpa}
              hint="Derived from the same thresholds as your letter grades"
              tone="green"
            />
            <SummaryCard
              icon={GraduationCap}
              label="Subjects passed"
              value={`${stats.passed} / ${subjects.length}`}
              hint={stats.failed ? `${stats.failed} below the pass mark` : 'All subjects passed'}
              tone={stats.failed ? 'amber' : 'green'}
            />
            <SummaryCard
              icon={Percent}
              label="Strongest subject"
              value={formatScore(stats.bestTotal)}
              hint={stats.bestName}
              tone="slate"
            />
          </div>

          <section className="card">
            <div className="border-b border-slate-200 px-5 py-4">
              <h2 className="font-semibold text-slate-900">Subject breakdown</h2>
              <p className="text-xs text-slate-500">
                Each subject is scored out of 100: quizzes 10%, assignments 10%, tests 20%, mid exam
                30% and final exam 30%. Select a subject to see how the components contributed.
              </p>
            </div>

            <ul className="divide-y divide-slate-100">
              {subjects.map((subject) => {
                const isOpen = expanded === subject.subjectId;

                return (
                  <li key={subject.subjectId}>
                    <button
                      type="button"
                      onClick={() => setExpanded(isOpen ? null : subject.subjectId)}
                      aria-expanded={isOpen}
                      className="flex w-full flex-wrap items-center gap-3 px-5 py-4 text-left hover:bg-slate-50"
                    >
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-semibold text-slate-900">{subject.subjectName}</p>
                        <p className="text-xs text-slate-500">{subject.subjectCode}</p>
                      </div>

                      <div className="flex items-center gap-2">
                        <Badge tone={subject.isPass ? 'green' : 'red'}>
                          {subject.isPass ? 'Pass' : 'Fail'}
                        </Badge>
                        <Badge tone={GRADE_TONES[subject.letterGrade] ?? 'slate'}>
                          Grade {subject.letterGrade}
                        </Badge>
                        <span className="w-20 text-right text-lg font-bold text-slate-900">
                          {formatScore(subject.totalScore)}
                        </span>
                        <ChevronDown
                          className={`size-4 text-slate-400 transition-transform ${isOpen ? 'rotate-180' : ''}`}
                          aria-hidden="true"
                        />
                      </div>
                    </button>

                    {isOpen && (
                      <motion.div
                        initial={{ opacity: 0, height: 0 }}
                        animate={{ opacity: 1, height: 'auto' }}
                        className="overflow-hidden px-5 pb-5"
                      >
                        <WeightBreakdown subject={subject} weights={weights} />
                      </motion.div>
                    )}
                  </li>
                );
              })}
            </ul>
          </section>
        </>
      )}
    </div>
  );
}
