import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Build output goes straight into WebApp's own wwwroot (served via app.UseStaticFiles(), see
// WebApp/Program.cs) - WebApp is the one admin site/dashboard in this solution; RssService and
// ApiService are headless background workers with no SPA of their own. The dev server proxies
// /api and /hubs to WebApp (see WebApp/Properties/launchSettings.json's http profile, port 5095)
// so `npm run dev` talks to live data - and the error-monitor's SignalR connection - without CORS
// either. /hubs needs `ws: true` since SignalR's WebSocket transport is a protocol upgrade, not a
// plain HTTP request.
export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: '../WebApp/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5095',
      '/hubs': { target: 'http://localhost:5095', ws: true },
    },
  },
})
