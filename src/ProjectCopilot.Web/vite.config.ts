import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // No `globals: true`: test files explicitly import `describe`/`it`/
    // `expect`/`vi` from 'vitest' rather than relying on ambient globals.
    env: {
      // import.meta.env.VITE_API_BASE_URL for tests (Vitest's `test.env`
      // populates both process.env and import.meta.env). Not a secret —
      // same placeholder local value as .env.example.
      VITE_API_BASE_URL: 'http://localhost:5015',
    },
  },
});
