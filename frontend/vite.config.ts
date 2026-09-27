import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// The browser always calls the RELATIVE path /api/... :
//  - `npm run dev`      -> Vite proxies /api to the .NET API (no CORS needed)
//  - docker / nginx     -> nginx proxies /api to the api container
//  - cloud              -> Front Door / Static Web Apps route /api to the API
// Override the local target with VITE_API_PROXY_TARGET if the API runs elsewhere.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});
