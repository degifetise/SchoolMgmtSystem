import { useCallback } from 'react';
import { motion } from 'framer-motion';
import { CalendarClock, CalendarDays, Megaphone, Pin, RefreshCw } from 'lucide-react';
import { publicApi } from '../services/endpoints';
import { useApiResource } from '../hooks/useApiResource';
import { EmptyState, ErrorState, Skeleton, Spinner } from '../components/ui/Feedback';

const dateFormat = { year: 'numeric', month: 'long', day: 'numeric' };

function formatDate(value) {
  return new Date(value).toLocaleDateString(undefined, dateFormat);
}

/** Days until a notice expires, or null when it never does. */
function daysRemaining(expiresAt) {
  if (!expiresAt) return null;
  const diff = new Date(expiresAt).getTime() - Date.now();
  return Math.max(0, Math.ceil(diff / 86_400_000));
}

export default function EventsPage() {
  const fetchEvents = useCallback(() => publicApi.events(50), []);
  const { data, error, loading, reload, reloading } = useApiResource(fetchEvents);

  const events = data ?? [];

  return (
    <div className="mx-auto max-w-4xl px-4 py-10 sm:px-6 lg:px-8">
      <header className="mb-8 flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold tracking-wide text-brand-700 uppercase">Noticeboard</p>
          <h1 className="mt-2 text-3xl font-bold text-slate-900">Explore events</h1>
          <p className="mt-2 max-w-2xl text-slate-600">
            Everything the school has published for the whole community. Notices aimed at a single
            class or role stay inside the portal and appear on the relevant dashboard instead.
          </p>
        </div>

        <button type="button" onClick={reload} className="btn-secondary" disabled={loading || reloading}>
          {reloading ? <Spinner className="size-4" /> : <RefreshCw className="size-4" aria-hidden="true" />}
          Refresh
        </button>
      </header>

      {error ? (
        <ErrorState
          title="Could not load the noticeboard"
          message={error}
          onRetry={reload}
          retrying={reloading}
        />
      ) : loading ? (
        <ul className="space-y-4">
          {Array.from({ length: 3 }).map((_, index) => (
            <li key={index} className="card space-y-3 p-6">
              <Skeleton className="h-5 w-1/2" />
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-1/3" />
            </li>
          ))}
        </ul>
      ) : events.length === 0 ? (
        <EmptyState
          icon={Megaphone}
          title="Nothing on the board"
          description="No school-wide notices are published at the moment. Check back soon."
        />
      ) : (
        <ul className="space-y-4">
          {events.map((item, index) => {
            const remaining = daysRemaining(item.expiresAt);

            return (
              <motion.li
                key={item.id}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.25, delay: Math.min(index * 0.04, 0.2) }}
                className={`card p-6 ${item.isPinned ? 'border-brand-200 bg-brand-50/40' : ''}`}
              >
                <div className="flex flex-wrap items-center gap-2">
                  {item.isPinned && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-brand-100 px-2.5 py-1 text-xs font-semibold text-brand-800">
                      <Pin className="size-3" aria-hidden="true" />
                      Pinned
                    </span>
                  )}
                  <span className="inline-flex items-center gap-1.5 text-xs text-slate-500">
                    <CalendarDays className="size-3.5" aria-hidden="true" />
                    {formatDate(item.postedAt)}
                  </span>
                  {remaining !== null && (
                    <span className="inline-flex items-center gap-1.5 text-xs text-amber-700">
                      <CalendarClock className="size-3.5" aria-hidden="true" />
                      {remaining === 0 ? 'Closes today' : `${remaining} day${remaining === 1 ? '' : 's'} left`}
                    </span>
                  )}
                </div>

                <h2 className="mt-3 text-lg font-bold text-slate-900">{item.title}</h2>
                {/* Announcement bodies are plain text; whitespace-pre-line keeps the author's
                    paragraph breaks without rendering anything as markup. */}
                <p className="mt-2 whitespace-pre-line text-slate-700">{item.content}</p>
              </motion.li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
