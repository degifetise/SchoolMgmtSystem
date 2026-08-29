import { useCallback } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  ArrowRight,
  BookOpen,
  CalendarDays,
  ClipboardList,
  GraduationCap,
  Layers,
  Pin,
  Users,
  UsersRound,
} from 'lucide-react';
import { publicApi } from '../services/endpoints';
import { useApiResource } from '../hooks/useApiResource';
import { homeRouteFor, useAuth } from '../context/AuthContext';
import { ErrorState, Skeleton, SkeletonCard } from '../components/ui/Feedback';

function StatTile({ icon: Icon, label, value, hint }) {
  return (
    <div className="card p-5">
      <div className="flex items-center gap-3">
        <span className="grid size-10 place-items-center rounded-lg bg-brand-100 text-brand-700">
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

export default function HomePage() {
  const { isAuthenticated, roles, user } = useAuth();

  const fetchOverview = useCallback(() => publicApi.overview(), []);
  const fetchEvents = useCallback(() => publicApi.events(3), []);

  const overview = useApiResource(fetchOverview);
  const events = useApiResource(fetchEvents);

  const data = overview.data;
  const totals = data?.totals;

  return (
    <div>
      {/* Hero */}
      <section className="bg-brand-800 text-white">
        <div className="mx-auto max-w-7xl px-4 py-14 sm:px-6 lg:px-8 lg:py-20">
          <motion.div initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.4 }}>
            {overview.loading ? (
              <Skeleton className="h-4 w-40 bg-white/20" />
            ) : (
              <p className="text-sm font-semibold tracking-wide text-brand-200 uppercase">
                {data?.academicYear ? `Academic year ${data.academicYear}` : 'Grades 9 – 12'}
              </p>
            )}

            <h1 className="mt-3 max-w-3xl text-3xl font-bold leading-tight sm:text-4xl lg:text-5xl">
              {overview.loading ? (
                <Skeleton className="h-12 w-full max-w-2xl bg-white/20" />
              ) : (
                <>
                  {data?.schoolName}
                  <span className="block text-brand-200">Grades 9 to 12, one portal.</span>
                </>
              )}
            </h1>

            <p className="mt-5 max-w-2xl text-brand-100">
              Lessons, weighted assessments and report cards in one place. Administrators manage the
              roster, teachers record marks against the school's grading policy, and students see
              their results the moment they are published.
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              {isAuthenticated ? (
                <Link to={homeRouteFor(roles)} className="btn-primary bg-white text-brand-800 hover:bg-brand-50">
                  Go to my dashboard
                  <ArrowRight className="size-4" aria-hidden="true" />
                </Link>
              ) : (
                <Link to="/login" className="btn-primary bg-white text-brand-800 hover:bg-brand-50">
                  Sign in
                  <ArrowRight className="size-4" aria-hidden="true" />
                </Link>
              )}

              <Link
                to="/events"
                className="btn inline-flex border border-white/30 text-white hover:bg-white/10"
              >
                <CalendarDays className="size-4" aria-hidden="true" />
                Explore events
              </Link>
            </div>

            {isAuthenticated && user?.fullName && (
              <p className="mt-5 text-sm text-brand-200">Signed in as {user.fullName}.</p>
            )}
          </motion.div>
        </div>
      </section>

      <div className="mx-auto max-w-7xl space-y-10 px-4 py-10 sm:px-6 lg:px-8">
        {/* Figures straight from the API */}
        <section>
          <h2 className="mb-4 text-lg font-bold text-slate-900">The school today</h2>

          {overview.error ? (
            <ErrorState
              title="Could not load the school figures"
              message={overview.error}
              onRetry={overview.reload}
              retrying={overview.reloading}
            />
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {overview.loading ? (
                Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} lines={1} />)
              ) : (
                <>
                  <StatTile
                    icon={Users}
                    label="Students enrolled"
                    value={totals.students}
                    hint={`Across ${totals.gradeLevels} grades and ${totals.sections} sections`}
                  />
                  <StatTile
                    icon={UsersRound}
                    label="Teaching staff"
                    value={totals.teachers}
                    hint="Active teacher accounts"
                  />
                  <StatTile
                    icon={BookOpen}
                    label="Subjects offered"
                    value={totals.subjects}
                    hint="Active subjects across all grades"
                  />
                  <StatTile
                    icon={Layers}
                    label="Grades"
                    value={totals.gradeLevels}
                    hint="Grade 9 through Grade 12"
                  />
                </>
              )}
            </div>
          )}
        </section>

        {/* Grading policy, read from the AssessmentTypes lookup table */}
        <section>
          <h2 className="mb-1 text-lg font-bold text-slate-900">How a subject is graded</h2>
          <p className="mb-4 text-sm text-slate-500">
            Every subject is scored out of 100 using these weights, taken live from the school's
            grading policy.
          </p>

          <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-5">
            {overview.loading
              ? Array.from({ length: 5 }).map((_, index) => (
                  <div key={index} className="card space-y-2 p-4">
                    <Skeleton className="h-3 w-20" />
                    <Skeleton className="h-7 w-14" />
                  </div>
                ))
              : (data?.gradingWeights ?? []).map((weight) => (
                  <div key={weight.name} className="card p-4">
                    <p className="text-xs font-medium tracking-wide text-slate-500 uppercase">
                      {weight.displayName}
                    </p>
                    <p className="text-2xl font-bold text-brand-700">{weight.weightPercentage}%</p>
                  </div>
                ))}
          </div>
        </section>

        {/* Latest notices */}
        <section>
          <div className="mb-4 flex items-end justify-between">
            <h2 className="text-lg font-bold text-slate-900">Latest from the school</h2>
            <Link to="/events" className="text-sm font-semibold text-brand-700 hover:underline">
              See all events
            </Link>
          </div>

          {events.error ? (
            <ErrorState
              title="Could not load the noticeboard"
              message={events.error}
              onRetry={events.reload}
              retrying={events.reloading}
            />
          ) : events.loading ? (
            <div className="space-y-3">
              {Array.from({ length: 2 }).map((_, index) => (
                <div key={index} className="card space-y-3 p-5">
                  <Skeleton className="h-4 w-1/3" />
                  <Skeleton className="h-3 w-full" />
                  <Skeleton className="h-3 w-2/3" />
                </div>
              ))}
            </div>
          ) : events.data?.length ? (
            <ul className="space-y-3">
              {events.data.map((item) => (
                <li key={item.id} className="card p-5">
                  <div className="flex items-start gap-3">
                    {item.isPinned && (
                      <Pin className="mt-1 size-4 shrink-0 text-brand-600" aria-label="Pinned" />
                    )}
                    <div className="min-w-0">
                      <p className="font-semibold text-slate-900">{item.title}</p>
                      <p className="mt-1 line-clamp-2 text-sm text-slate-600">{item.content}</p>
                      <p className="mt-2 text-xs text-slate-400">
                        {new Date(item.postedAt).toLocaleDateString(undefined, {
                          year: 'numeric',
                          month: 'long',
                          day: 'numeric',
                        })}
                      </p>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="card p-5 text-sm text-slate-500">
              No school-wide notices are published right now.
            </p>
          )}
        </section>

        {/* Role signposts */}
        <section className="grid gap-4 sm:grid-cols-3">
          {[
            {
              icon: ClipboardList,
              title: 'For teachers',
              body: 'Enter a whole class of scores against one assessment, then publish when you are ready.',
            },
            {
              icon: GraduationCap,
              title: 'For students',
              body: 'Follow your weighted total per subject, your letter grade and what still counts.',
            },
            {
              icon: Users,
              title: 'For administrators',
              body: 'Enrol students, manage staff accounts and set the academic year and pass mark.',
            },
          ].map(({ icon: Icon, title, body }) => (
            <div key={title} className="card p-5">
              <span className="grid size-10 place-items-center rounded-lg bg-brand-50 text-brand-700">
                <Icon className="size-5" aria-hidden="true" />
              </span>
              <p className="mt-3 font-semibold text-slate-900">{title}</p>
              <p className="mt-1 text-sm text-slate-600">{body}</p>
            </div>
          ))}
        </section>
      </div>
    </div>
  );
}
