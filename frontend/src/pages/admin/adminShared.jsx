import { useState } from 'react';
import { motion } from 'framer-motion';
import { KeyRound, Search, ToggleLeft, ToggleRight, Users } from 'lucide-react';
import { EmptyState, Spinner } from '../../components/ui/Feedback';

/** Table page size, shared so both admin pages page identically. */
export const PAGE_SIZE = 10;

export function StatCard({ icon: Icon, label, value, hint, tone = 'brand' }) {
  const tones = {
    brand: 'bg-brand-100 text-brand-700',
    green: 'bg-emerald-100 text-emerald-700',
    amber: 'bg-amber-100 text-amber-700',
    slate: 'bg-slate-100 text-slate-600',
  };

  return (
    <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="card p-5">
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
    </motion.div>
  );
}

/** Submit-to-search, so typing does not fire a request per keystroke. */
export function SearchBox({ value, onSearch, placeholder = 'Search name or ID' }) {
  const [draft, setDraft] = useState(value ?? '');

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        onSearch(draft.trim());
      }}
      className="relative"
    >
      <Search
        className="pointer-events-none absolute inset-y-0 left-3 my-auto size-4 text-slate-400"
        aria-hidden="true"
      />
      <input
        className="input w-56 pl-9"
        placeholder={placeholder}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        aria-label="Search"
      />
    </form>
  );
}

export function RowActions({ busy, isActive, hasLogin, onToggle, onReset }) {
  return (
    <div className="flex items-center justify-end gap-1">
      {busy && <Spinner className="size-4 text-brand-600" />}
      <button
        type="button"
        onClick={onToggle}
        disabled={busy}
        className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 disabled:opacity-50"
        title={isActive ? 'Deactivate' : 'Activate'}
      >
        {isActive ? (
          <ToggleRight className="size-5 text-emerald-600" aria-hidden="true" />
        ) : (
          <ToggleLeft className="size-5" aria-hidden="true" />
        )}
        <span className="sr-only">{isActive ? 'Deactivate' : 'Activate'}</span>
      </button>
      <button
        type="button"
        onClick={onReset}
        disabled={busy || !hasLogin}
        className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 disabled:opacity-40"
        title={hasLogin ? 'Reset password' : 'No login to reset'}
      >
        <KeyRound className="size-4" aria-hidden="true" />
        <span className="sr-only">Reset password</span>
      </button>
    </div>
  );
}

export function PeopleTable({ loading, rows, columns, renderRow, emptyTitle }) {
  if (loading) {
    return (
      <div className="flex justify-center py-12">
        <Spinner className="size-6 text-brand-600" label="Loading…" />
      </div>
    );
  }

  if (!rows?.length) {
    return (
      <div className="p-5">
        <EmptyState icon={Users} title={emptyTitle} />
      </div>
    );
  }

  return (
    <table className="w-full min-w-3xl text-left text-sm">
      <thead className="bg-slate-50 text-xs tracking-wide text-slate-500 uppercase">
        <tr>
          {columns.map((column, index) => (
            <th
              key={column || `spacer-${index}`}
              scope="col"
              className={`px-5 py-3 font-semibold ${index === columns.length - 1 ? 'text-right' : ''}`}
            >
              {column}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>{rows.map(renderRow)}</tbody>
    </table>
  );
}

export function Pager({ page, totalPages, totalCount, onChange }) {
  if (!totalCount) return null;

  return (
    <div className="flex items-center justify-between border-t border-slate-200 px-5 py-3 text-sm">
      <p className="text-slate-500">
        Page {page} of {totalPages} · {totalCount} record{totalCount === 1 ? '' : 's'}
      </p>
      <div className="flex gap-2">
        <button
          type="button"
          className="btn-secondary"
          onClick={() => onChange(Math.max(1, page - 1))}
          disabled={page <= 1}
        >
          Previous
        </button>
        <button
          type="button"
          className="btn-secondary"
          onClick={() => onChange(Math.min(totalPages, page + 1))}
          disabled={page >= totalPages}
        >
          Next
        </button>
      </div>
    </div>
  );
}

export const totalPagesOf = (collection) =>
  Math.max(1, Math.ceil((collection?.totalCount ?? 0) / PAGE_SIZE));
