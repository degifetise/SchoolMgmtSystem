import { AlertTriangle, CheckCircle2, Info, Loader2, RefreshCw, XCircle } from 'lucide-react';
import { motion } from 'framer-motion';

const ALERT_STYLES = {
  error: { wrap: 'border-rose-200 bg-rose-50 text-rose-800', Icon: XCircle },
  success: { wrap: 'border-emerald-200 bg-emerald-50 text-emerald-800', Icon: CheckCircle2 },
  warning: { wrap: 'border-amber-200 bg-amber-50 text-amber-800', Icon: AlertTriangle },
  info: { wrap: 'border-brand-200 bg-brand-50 text-brand-800', Icon: Info },
};

export function Alert({ variant = 'info', title, children, onDismiss, className = '' }) {
  const { wrap, Icon } = ALERT_STYLES[variant] ?? ALERT_STYLES.info;

  return (
    <motion.div
      initial={{ opacity: 0, y: -6 }}
      animate={{ opacity: 1, y: 0 }}
      role={variant === 'error' ? 'alert' : 'status'}
      className={`flex items-start gap-3 rounded-lg border px-4 py-3 text-sm ${wrap} ${className}`}
    >
      <Icon className="mt-0.5 size-5 shrink-0" aria-hidden="true" />
      <div className="min-w-0 flex-1">
        {title && <p className="font-semibold">{title}</p>}
        {children && <div className="break-words">{children}</div>}
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          className="shrink-0 rounded p-0.5 opacity-70 transition-opacity hover:opacity-100"
          aria-label="Dismiss"
        >
          <XCircle className="size-4" aria-hidden="true" />
        </button>
      )}
    </motion.div>
  );
}

export function Spinner({ className = 'size-5', label }) {
  return (
    <span className="inline-flex items-center gap-2" role="status">
      <Loader2 className={`animate-spin ${className}`} aria-hidden="true" />
      {label && <span className="text-sm text-slate-500">{label}</span>}
      <span className="sr-only">Loading</span>
    </span>
  );
}

export function LoadingPanel({ label = 'Loading…' }) {
  return (
    <div className="flex items-center justify-center rounded-xl border border-slate-200 bg-white px-6 py-16">
      <Spinner className="size-6 text-brand-600" label={label} />
    </div>
  );
}

/** Grey placeholder that occupies the same space as the content it stands in for. */
export function Skeleton({ className = 'h-4 w-full' }) {
  return <div className={`animate-pulse rounded bg-slate-200 ${className}`} aria-hidden="true" />;
}

/** Card-shaped skeleton for metric grids. */
export function SkeletonCard({ lines = 2 }) {
  return (
    <div className="card p-5" aria-hidden="true">
      <div className="flex items-center gap-3">
        <Skeleton className="size-10 rounded-lg" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-6 w-16" />
        </div>
      </div>
      {Array.from({ length: lines }).map((_, index) => (
        <Skeleton key={index} className="mt-3 h-3 w-full" />
      ))}
    </div>
  );
}

/**
 * Terminal failure with a way out. Prefer this over a bare Alert whenever the page has no
 * content to show, so the user is never left on a blank screen with no next step.
 */
export function ErrorState({ title = 'Could not load this page', message, onRetry, retrying }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-rose-200 bg-rose-50 px-6 py-14 text-center">
      <span className="mb-3 rounded-full bg-rose-100 p-3 text-rose-600">
        <AlertTriangle className="size-6" aria-hidden="true" />
      </span>
      <p className="font-semibold text-rose-900">{title}</p>
      {message && <p className="mt-1 max-w-lg text-sm text-rose-800">{message}</p>}
      {onRetry && (
        <button type="button" onClick={onRetry} className="btn-primary mt-4" disabled={retrying}>
          {retrying ? <Spinner className="size-4" /> : <RefreshCw className="size-4" aria-hidden="true" />}
          Try again
        </button>
      )}
    </div>
  );
}

export function EmptyState({ icon: Icon = Info, title, description, action }) {
  return (
    <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-slate-300 bg-white px-6 py-14 text-center">
      <span className="mb-3 rounded-full bg-slate-100 p-3 text-slate-500">
        <Icon className="size-6" aria-hidden="true" />
      </span>
      <p className="font-semibold text-slate-800">{title}</p>
      {description && <p className="mt-1 max-w-md text-sm text-slate-500">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

export function Badge({ children, tone = 'slate', className = '' }) {
  const tones = {
    slate: 'bg-slate-100 text-slate-700',
    brand: 'bg-brand-100 text-brand-800',
    green: 'bg-emerald-100 text-emerald-800',
    red: 'bg-rose-100 text-rose-800',
    amber: 'bg-amber-100 text-amber-800',
  };

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold ${
        tones[tone] ?? tones.slate
      } ${className}`}
    >
      {children}
    </span>
  );
}
