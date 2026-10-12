import { Link } from 'react-router-dom';

/**
 * Fallback for any URL that matches no route pattern (typo, stale bookmark,
 * removed link, ...). Mounted inside `ProtectedRoute`/`AppShell` for an
 * authenticated user hitting an unknown path. An unauthenticated user at an
 * unknown path never reaches this component - `ProtectedRoute` redirects to
 * `/login` first. See `router.tsx`'s own comment for why a second,
 * top-level mount of this component is currently unreachable dead code.
 */
export function NotFoundPage() {
  return (
    <div className="center-page">
      <div className="card auth-card">
        <h1 className="card-title">Page not found</h1>
        <p>
          <Link to="/" className="link-subtle">
            Back to Projects
          </Link>
        </p>
      </div>
    </div>
  );
}
