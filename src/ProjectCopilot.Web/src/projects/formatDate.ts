/**
 * Formats an ISO date as DD/MM/YYYY, always, regardless of browser locale.
 *
 * Uses UTC getters (not local getters) deliberately: the value represents a calendar
 * day, not a precise instant, and local-timezone getters could shift the displayed day
 * depending on the viewer's timezone offset relative to the stored UTC-midnight value.
 *
 * Shared by ProjectsPage/ProjectDashboardPage (`Project.deadline`) and the Tasks UI
 * (`Task.dueDate`) - both are the same kind of calendar-day value, so this lives once
 * here rather than being duplicated per screen/feature.
 */
export function formatDate(iso: string): string {
  const d = new Date(iso);
  const dd = String(d.getUTCDate()).padStart(2, '0');
  const mm = String(d.getUTCMonth() + 1).padStart(2, '0');
  const yyyy = d.getUTCFullYear();
  return `${dd}/${mm}/${yyyy}`;
}
