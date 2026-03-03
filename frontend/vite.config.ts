import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// When running via Vite dev server, proxy BFF/API calls to the BffGateway backend.
// In production the SPA is served from BffGateway itself, so no proxy is needed.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/bff': 'http://localhost:5200',
      '/api': 'http://localhost:5200',
      '/health': 'http://localhost:5200',
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
