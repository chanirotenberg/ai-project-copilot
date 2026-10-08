/**
 * Formats an ISO deadline as DD/MM/YYYY, always, regardless of browser locale.
 *
 * Uses UTC getters (not local getters) deliberately: the deadline represents a calendar
 * day, not a precise instant, and local-timezone getters could shift the displayed day
 * depending on the viewer's timezone offset relative to the stored UTC-midnight value.
 *
 * Shared by ProjectsPage and ProjectDashboardPage - both display the same `Project.deadline`
 * value, so this lives once here rather than being duplicated per screen.
 */
export function formatDeadline(iso: string): string {
  const d = new Date(iso);
  const dd = String(d.getUTCDate()).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const yyyy = d.getUTCFullYear();
  return `${dd}/${mm}/${yyyy}`;
}
