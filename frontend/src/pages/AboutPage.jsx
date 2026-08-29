import { useCallback } from 'react';
import { BookOpen, Layers, Mail, ScrollText, Users } from 'lucide-react';
import { publicApi } from '../services/endpoints';
import { useApiResource } from '../hooks/useApiResource';
import { ErrorState, Skeleton } from '../components/ui/Feedback';

export default function AboutPage() {
  const fetchOverview = useCallback(() => publicApi.overview(), []);
  const { data, error, loading, reload, reloading } = useApiResource(fetchOverview);

  if (error) {
    return (
      <div className="mx-auto max-w-7xl px-4 py-10 sm:px-6 lg:px-8">
        <ErrorState
          title="Could not load the school profile"
          message={error}
          onRetry={reload}
          retrying={reloading}
        />
      </div>
    );
  }

  const totals = data?.totals;

  return (
    <div className="mx-auto max-w-7xl space-y-10 px-4 py-10 sm:px-6 lg:px-8">
      <header>
        <p className="text-sm font-semibold tracking-wide text-brand-700 uppercase">About us</p>
        <h1 className="mt-2 text-3xl font-bold text-slate-900">
          {loading ? <Skeleton className="h-9 w-80" /> : data.schoolName}
        </h1>
        <p className="mt-4 max-w-3xl text-slate-600">
          A secondary school serving Grades 9 to 12. This portal is the single record of what is
          taught, who teaches it and how every student is progressing, so a question about a mark
          has one answer wherever it is asked.
        </p>
      </header>

      <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          { icon: Users, label: 'Students enrolled', value: totals?.students },
          { icon: Users, label: 'Teaching staff', value: totals?.teachers },
          { icon: BookOpen, label: 'Subjects offered', value: totals?.subjects },
          { icon: Layers, label: 'Sections', value: totals?.sections },
        ].map(({ icon: Icon, label, value }) => (
          <div key={label} className="card p-5">
            <span className="grid size-10 place-items-center rounded-lg bg-brand-100 text-brand-700">
              <Icon className="size-5" aria-hidden="true" />
            </span>
            <p className="mt-3 text-xs font-medium tracking-wide text-slate-500 uppercase">{label}</p>
            {loading ? (
              <Skeleton className="mt-1 h-8 w-16" />
            ) : (
              <p className="text-2xl font-bold text-slate-900">{value}</p>
            )}
          </div>
        ))}
      </section>

      <section>
        <h2 className="text-lg font-bold text-slate-900">What each grade studies</h2>
        <p className="mt-1 mb-4 text-sm text-slate-500">
          Subject counts come straight from the curriculum held in the portal.
        </p>

        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {loading
            ? Array.from({ length: 4 }).map((_, index) => (
                <div key={index} className="card space-y-3 p-5">
                  <Skeleton className="h-5 w-24" />
                  <Skeleton className="h-3 w-32" />
                  <Skeleton className="h-3 w-20" />
                </div>
              ))
            : data.gradeLevels.map((grade) => (
                <div key={grade.id} className="card p-5">
                  <p className="font-bold text-slate-900">{grade.name}</p>
                  {grade.description && (
                    <p className="mt-1 text-sm text-slate-600">{grade.description}</p>
                  )}
                  <p className="mt-3 inline-flex items-center gap-1.5 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-semibold text-brand-700">
                    <BookOpen className="size-3.5" aria-hidden="true" />
                    {grade.subjectCount} subjects
                  </p>
                </div>
              ))}
        </div>
      </section>

      <section className="grid gap-6 lg:grid-cols-2">
        <div className="card p-6">
          <span className="grid size-10 place-items-center rounded-lg bg-brand-50 text-brand-700">
            <ScrollText className="size-5" aria-hidden="true" />
          </span>
          <h2 className="mt-3 text-lg font-bold text-slate-900">Our grading policy</h2>
          <p className="mt-1 text-sm text-slate-600">
            Every subject is marked out of 100. Each component carries a fixed share of the final
            result, and the weights below are the ones the portal actually applies.
          </p>

          <ul className="mt-4 space-y-2">
            {loading
              ? Array.from({ length: 5 }).map((_, index) => <Skeleton key={index} className="h-9 w-full" />)
              : data.gradingWeights.map((weight) => (
                  <li key={weight.name}>
                    <div className="mb-1 flex items-baseline justify-between text-sm">
                      <span className="font-medium text-slate-700">{weight.displayName}</span>
                      <span className="font-bold text-brand-700">{weight.weightPercentage}%</span>
                    </div>
                    <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                      <div
                        className="h-full rounded-full bg-brand-500"
                        style={{ width: `${weight.weightPercentage}%` }}
                      />
                    </div>
                  </li>
                ))}
          </ul>
        </div>

        <div className="card p-6">
          <span className="grid size-10 place-items-center rounded-lg bg-brand-50 text-brand-700">
            <Mail className="size-5" aria-hidden="true" />
          </span>
          <h2 className="mt-3 text-lg font-bold text-slate-900">Get in touch</h2>

          <dl className="mt-4 space-y-4 text-sm">
            <div>
              <dt className="font-medium text-slate-500">Academic year</dt>
              <dd className="text-slate-900">
                {loading ? <Skeleton className="h-4 w-24" /> : data.academicYear}
              </dd>
            </div>
            <div>
              <dt className="font-medium text-slate-500">School office</dt>
              <dd className="text-slate-900">
                {loading ? (
                  <Skeleton className="h-4 w-48" />
                ) : data.contactEmail ? (
                  <a href={`mailto:${data.contactEmail}`} className="text-brand-700 hover:underline">
                    {data.contactEmail}
                  </a>
                ) : (
                  'No contact address has been published yet.'
                )}
              </dd>
            </div>
            <div>
              <dt className="font-medium text-slate-500">Getting an account</dt>
              <dd className="text-slate-900">
                {loading ? (
                  <Skeleton className="h-4 w-56" />
                ) : data.allowSelfRegistration ? (
                  'Students may register themselves. Staff accounts are created by an administrator.'
                ) : (
                  'Accounts are created by the school administrator. Contact the office if you cannot sign in.'
                )}
              </dd>
            </div>
          </dl>
        </div>
      </section>
    </div>
  );
}
