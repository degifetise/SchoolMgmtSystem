import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';
import {
  BadgeCheck,
  ChevronDown,
  GraduationCap,
  IdCard,
  LogOut,
  Menu,
  School,
  ShieldCheck,
  X,
} from 'lucide-react';
import { ROLES, useAuth } from '../../context/AuthContext';
import { useSchoolInfo } from '../../context/SchoolInfoContext';
import { navItemsFor } from './navigation';

/* Each role gets its own colour so the badge is recognisable before it is read. */
const ROLE_STYLE = {
  [ROLES.admin]: { chip: 'bg-amber-400/20 text-amber-100 ring-amber-300/30', Icon: ShieldCheck },
  [ROLES.teacher]: { chip: 'bg-emerald-400/20 text-emerald-100 ring-emerald-300/30', Icon: BadgeCheck },
  [ROLES.student]: { chip: 'bg-sky-400/20 text-sky-100 ring-sky-300/30', Icon: GraduationCap },
};

const FALLBACK_STYLE = { chip: 'bg-white/15 text-white ring-white/20', Icon: IdCard };

function initialsOf(name = '') {
  return (
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || '?'
  );
}

/** Coloured chip naming a role. Rendered on the dark bar, hence the light-on-dark palette. */
export function RoleBadge({ role, className = '' }) {
  const { chip, Icon } = ROLE_STYLE[role] ?? FALLBACK_STYLE;

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ring-inset ${chip} ${className}`}
    >
      <Icon className="size-3" aria-hidden="true" />
      {role}
    </span>
  );
}

/**
 * The identifier a role is known by: a student by their student number, a teacher by their
 * employee number. Shown next to the name so a teacher reading a screen over someone's
 * shoulder can tell whose account it is.
 */
function IdentityLine({ user, roles }) {
  const identifier = user?.studentIdNumber ?? user?.employeeId;

  return (
    <span className="flex items-center gap-1.5">
      {roles.map((role) => (
        <RoleBadge key={role} role={role} />
      ))}
      {identifier && (
        <span className="font-mono text-[11px] text-brand-200" title="Identifier">
          {identifier}
        </span>
      )}
    </span>
  );
}

/**
 * The portal's single navigation bar, shared by the public pages and every signed-in area.
 *
 * Structure, and why: links live on the left as a group, the account lives on the right behind
 * a separator. Logout never sits among the links — it is destructive to the session, so it is
 * visually separated from navigation both on desktop (its own control, right of the divider)
 * and on mobile (below a rule, in red, at the end of the sheet).
 */
export default function TopNavBar() {
  const { user, roles, isAuthenticated, logout } = useAuth();
  const { schoolName, academicYear } = useSchoolInfo();
  const location = useLocation();

  const [mobileOpen, setMobileOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  const items = navItemsFor(roles);
  const isStudent = roles.includes(ROLES.student);

  // Any navigation closes both overlays, including a click on the link you are already on.
  useEffect(() => {
    setMobileOpen(false);
    setMenuOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    if (!menuOpen) return undefined;

    const onPointerDown = (event) => {
      if (!menuRef.current?.contains(event.target)) setMenuOpen(false);
    };
    const onKeyDown = (event) => event.key === 'Escape' && setMenuOpen(false);

    document.addEventListener('mousedown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [menuOpen]);

  useEffect(() => {
    if (!mobileOpen) return undefined;
    const onKeyDown = (event) => event.key === 'Escape' && setMobileOpen(false);
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [mobileOpen]);

  /* The active pill is the route indicator. NavLink resolves `isActive` from the current
     location, and `end` keeps "/" from matching every path below it. */
  const deskLink = ({ isActive }) =>
    `relative rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
      isActive
        ? 'bg-white text-brand-800 shadow-sm'
        : 'text-brand-100 hover:bg-white/10 hover:text-white'
    }`;

  const sheetLink = ({ isActive }) =>
    `flex items-start gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${
      isActive ? 'bg-white text-brand-800' : 'text-brand-100 hover:bg-white/10 hover:text-white'
    }`;

  return (
    <header className="sticky top-0 z-40 bg-brand-800 shadow-sm">
      <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-3 sm:px-6 lg:px-8">
        {/* Brand. The name is read from the anonymous settings endpoint, so renaming the
            school in Settings retitles every page for every visitor. */}
        <Link to="/" className="flex min-w-0 items-center gap-3">
          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-white/15 text-white">
            <School className="size-5" aria-hidden="true" />
          </span>
          <span className="min-w-0">
            <span className="block truncate text-sm font-bold text-white sm:text-base" title={schoolName}>
              {schoolName}
            </span>
            <span className="block truncate text-xs text-brand-200">
              Grades 9 – 12{academicYear ? ` · ${academicYear}` : ''}
            </span>
          </span>
        </Link>

        <nav className="ml-4 hidden items-center gap-1 lg:flex" aria-label="Main">
          {items.map(({ to, label, shortLabel, end }) => (
            <NavLink key={to} to={to} end={end} className={deskLink}>
              {/* The student's longest label is shortened on narrow desktops only. */}
              <span className={shortLabel ? 'hidden xl:inline' : undefined}>{label}</span>
              {shortLabel && <span className="xl:hidden">{shortLabel}</span>}
            </NavLink>
          ))}
        </nav>

        <div className="ml-auto flex items-center gap-2">
          {isAuthenticated ? (
            <>
              {/* Separator: everything to the right of it is about the account, not the site. */}
              <span className="mx-1 hidden h-8 w-px bg-white/15 md:block" aria-hidden="true" />

              <div className="hidden text-right md:block">
                <p className="truncate text-sm font-semibold leading-tight text-white">
                  {user?.fullName}
                </p>
                <IdentityLine user={user} roles={roles} />
              </div>

              {/* A student's bar carries a standalone Logout button rather than a menu, so
                  signing out is one tap with no discovery needed. */}
              {isStudent ? (
                <>
                  <span className="grid size-10 shrink-0 place-items-center rounded-full bg-white/15 text-sm font-bold text-white">
                    {initialsOf(user?.fullName)}
                  </span>
                  {/* Always visible, at every width: on a narrow screen it drops to the icon
                      rather than hiding behind the menu. */}
                  <button
                    type="button"
                    onClick={() => logout({ silent: false })}
                    className="inline-flex items-center gap-2 rounded-lg bg-rose-500/90 px-2.5 py-2 text-sm font-semibold text-white transition-colors hover:bg-rose-500 sm:px-3"
                  >
                    <LogOut className="size-4" aria-hidden="true" />
                    <span className="hidden sm:inline">Logout</span>
                    <span className="sr-only sm:hidden">Logout</span>
                  </button>
                </>
              ) : (
                <div className="relative" ref={menuRef}>
                  <button
                    type="button"
                    onClick={() => setMenuOpen((open) => !open)}
                    className="flex items-center gap-1.5 rounded-full bg-white/15 py-1 pl-1 pr-2 text-white transition-colors hover:bg-white/25"
                    aria-haspopup="menu"
                    aria-expanded={menuOpen}
                    aria-label="Account menu"
                  >
                    <span className="grid size-8 place-items-center rounded-full bg-white/20 text-xs font-bold">
                      {initialsOf(user?.fullName)}
                    </span>
                    <ChevronDown
                      className={`size-4 transition-transform ${menuOpen ? 'rotate-180' : ''}`}
                      aria-hidden="true"
                    />
                  </button>

                  <AnimatePresence>
                    {menuOpen && (
                      <motion.div
                        initial={{ opacity: 0, y: -6, scale: 0.98 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: -6, scale: 0.98 }}
                        transition={{ duration: 0.14 }}
                        role="menu"
                        className="absolute right-0 mt-2 w-64 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg"
                      >
                        <div className="border-b border-slate-100 px-4 py-3">
                          <p className="truncate font-semibold text-slate-900">{user?.fullName}</p>
                          <p className="truncate text-xs text-slate-500">{user?.email}</p>
                          <div className="mt-2 flex flex-wrap gap-1">
                            {roles.map((role) => (
                              <span
                                key={role}
                                className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700"
                              >
                                {role}
                              </span>
                            ))}
                            {user?.employeeId && (
                              <span className="rounded-full bg-slate-100 px-2 py-0.5 font-mono text-[11px] text-slate-600">
                                {user.employeeId}
                              </span>
                            )}
                          </div>
                        </div>

                        <button
                          type="button"
                          role="menuitem"
                          onClick={() => {
                            setMenuOpen(false);
                            logout({ silent: false });
                          }}
                          className="flex w-full items-center gap-2 px-4 py-3 text-sm font-semibold text-rose-700 transition-colors hover:bg-rose-50"
                        >
                          <LogOut className="size-4" aria-hidden="true" />
                          Logout
                        </button>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              )}
            </>
          ) : null}

          <button
            type="button"
            onClick={() => setMobileOpen((open) => !open)}
            className="rounded-lg p-2 text-brand-100 transition-colors hover:bg-white/10 hover:text-white lg:hidden"
            aria-label={mobileOpen ? 'Close navigation' : 'Open navigation'}
            aria-expanded={mobileOpen}
            aria-controls="mobile-navigation"
          >
            <AnimatePresence mode="wait" initial={false}>
              <motion.span
                key={mobileOpen ? 'close' : 'open'}
                initial={{ rotate: -90, opacity: 0 }}
                animate={{ rotate: 0, opacity: 1 }}
                exit={{ rotate: 90, opacity: 0 }}
                transition={{ duration: 0.15 }}
                className="block"
              >
                {mobileOpen ? <X className="size-5" /> : <Menu className="size-5" />}
              </motion.span>
            </AnimatePresence>
          </button>
        </div>
      </div>

      {/* Mobile sheet. Collapses in place instead of covering the page, so the content behind
          it stays visible while choosing. */}
      <AnimatePresence>
        {mobileOpen && (
          <motion.div
            id="mobile-navigation"
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2, ease: 'easeOut' }}
            className="overflow-hidden border-t border-white/10 lg:hidden"
          >
            <nav className="px-4 py-3 sm:px-6" aria-label="Mobile">
              {isAuthenticated && (
                <div className="mb-3 flex items-center gap-3 rounded-lg bg-white/5 px-3 py-2.5">
                  <span className="grid size-9 shrink-0 place-items-center rounded-full bg-white/15 text-xs font-bold text-white">
                    {initialsOf(user?.fullName)}
                  </span>
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-semibold text-white">
                      {user?.fullName}
                    </span>
                    <IdentityLine user={user} roles={roles} />
                  </span>
                </div>
              )}

              <ul className="space-y-1">
                {items.map(({ to, label, icon: Icon, end, description }) => (
                  <li key={to}>
                    <NavLink to={to} end={end} className={sheetLink}>
                      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                      <span className="min-w-0">
                        <span className="block font-semibold">{label}</span>
                        {description && <span className="block text-xs opacity-80">{description}</span>}
                      </span>
                    </NavLink>
                  </li>
                ))}
              </ul>

              {isAuthenticated && (
                <div className="mt-3 border-t border-white/10 pt-3">
                  <button
                    type="button"
                    onClick={() => {
                      setMobileOpen(false);
                      logout({ silent: false });
                    }}
                    className="flex w-full items-center gap-3 rounded-lg bg-rose-500/15 px-3 py-2.5 text-sm font-semibold text-rose-200 transition-colors hover:bg-rose-500/25"
                  >
                    <LogOut className="size-4" aria-hidden="true" />
                    Logout
                  </button>
                </div>
              )}
            </nav>
          </motion.div>
        )}
      </AnimatePresence>
    </header>
  );
}
