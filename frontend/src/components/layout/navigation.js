import {
  CalendarDays,
  ClipboardCheck,
  GraduationCap,
  Home,
  Info,
  LogIn,
  Settings,
  Users,
} from 'lucide-react';
import { ROLES } from '../../context/AuthContext';

/**
 * The navigation surface, one row per link.
 *
 * `roles` is the audience: `null` means everybody including signed-out visitors, `[]` means
 * signed-out visitors only, and a list of roles means exactly those roles. Every `to` resolves
 * to a route declared in AppRoutes, so the bar cannot advertise a link that 404s.
 *
 * The per-role result:
 *   Guest    Home · About · Explore Events · Login
 *   Student  Home · My Academic Results · Settings          (deliberately minimal)
 *   Teacher  Home · About · Explore Events · Students · Settings
 *   Admin    Home · About · Explore Events · Students · Accounts · Settings
 *
 * Logout is not in this list. It is an account action, not a destination, so the bar renders it
 * separately from the links.
 */
const NAV_ITEMS = [
  {
    to: '/',
    label: 'Home',
    icon: Home,
    end: true,
    roles: null,
  },
  {
    to: '/about',
    label: 'About',
    icon: Info,
    // Not the student: their bar stays short so results are one glance away.
    roles: [ROLES.admin, ROLES.teacher],
    guest: true,
  },
  {
    to: '/events',
    label: 'Explore Events',
    icon: CalendarDays,
    roles: [ROLES.admin, ROLES.teacher],
    guest: true,
  },
  {
    to: '/student/results',
    label: 'My Academic Results',
    shortLabel: 'My Results',
    icon: GraduationCap,
    description: 'Weighted totals, letter grades and your report card',
    roles: [ROLES.student],
  },
  {
    to: '/teacher/students',
    label: 'Students',
    icon: Users,
    description: 'Your class rosters and their grades',
    roles: [ROLES.teacher],
  },
  {
    to: '/admin/students',
    label: 'Students',
    icon: Users,
    description: 'Enrolment, roster and class metrics',
    roles: [ROLES.admin],
  },
  {
    to: '/admin/accounts',
    label: 'Accounts',
    icon: ClipboardCheck,
    description: 'Registration approvals and account provisioning',
    roles: [ROLES.admin],
  },
  {
    to: '/settings',
    label: 'Settings',
    icon: Settings,
    description: 'Your password and account details',
    roles: [ROLES.admin, ROLES.teacher, ROLES.student],
  },
  {
    to: '/login',
    label: 'Login',
    icon: LogIn,
    // Signed-out only: an authenticated visitor gets the profile menu instead.
    roles: [],
    guest: true,
  },
];

/**
 * Links for the current audience, in declaration order.
 * @param roles roles from the session, empty for a signed-out visitor.
 */
export function navItemsFor(roles = []) {
  const signedIn = roles.length > 0;

  return NAV_ITEMS.filter((item) => {
    if (item.roles === null) return true;
    if (!signedIn) return item.guest === true || item.roles.length === 0;
    return item.roles.some((role) => roles.includes(role));
  });
}

/** True when the link points at a page only signed-in users can open. */
export function isPrivate(item) {
  return item.roles !== null && item.roles.length > 0 && item.guest !== true;
}

/**
 * Headings for pages that are reachable but deliberately absent from the bar, so they are not
 * left untitled. Mark entry is reached from a teacher's class list rather than the navigation.
 */
const OFF_NAV_HEADINGS = [
  {
    to: '/teacher/marks',
    label: 'Mark Entry',
    description: 'Enter and publish scores for one assessment at a time',
  },
];

/** The heading strip for the current route: its label and, where useful, a line of context. */
export function pageHeadingFor(pathname, roles = []) {
  const matches = (item) =>
    item.end ? pathname === item.to : pathname === item.to || pathname.startsWith(`${item.to}/`);

  return (
    navItemsFor(roles).filter(isPrivate).find(matches) ?? OFF_NAV_HEADINGS.find(matches) ?? null
  );
}
