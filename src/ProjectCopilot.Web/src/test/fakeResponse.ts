/**
 * Minimal fetch-compatible `Response` stand-in shared across test files
 * (avoids relying on a global `Response` constructor under jsdom).
 */
export function fakeResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: {
      get: (name: string) => (name.toLowerCase() === 'content-type' ? 'application/json' : null),
    },
    json: async () => body,
  } as Response;
}
