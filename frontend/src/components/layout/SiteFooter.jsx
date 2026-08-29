import { Link } from 'react-router-dom';
import { Mail } from 'lucide-react';
import { useSchoolInfo } from '../../context/SchoolInfoContext';

/** Shared footer. Contact details come from the anonymous settings endpoint. */
export default function SiteFooter() {
  const { schoolName, contactEmail, academicYear } = useSchoolInfo();

  return (
    <footer className="mt-auto border-t border-slate-200 bg-white">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-4 gap-y-2 px-4 py-5 text-xs text-slate-500 sm:px-6 lg:px-8">
        <p className="font-semibold text-slate-700">{schoolName}</p>
        {academicYear && <p>Academic year {academicYear}</p>}

        {contactEmail && (
          <a
            href={`mailto:${contactEmail}`}
            className="inline-flex items-center gap-1.5 hover:text-brand-700"
          >
            <Mail className="size-3.5" aria-hidden="true" />
            {contactEmail}
          </a>
        )}

        <nav className="ml-auto flex gap-4" aria-label="Footer">
          <Link to="/" className="hover:text-brand-700">Home</Link>
          <Link to="/about" className="hover:text-brand-700">About</Link>
          <Link to="/events" className="hover:text-brand-700">Events</Link>
        </nav>
      </div>
    </footer>
  );
}
