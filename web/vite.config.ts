import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig, loadEnv } from 'vite'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Django local por defecto. Se puede apuntar a otro host con VITE_BACKEND_URL
  // en el .env (por ejemplo si el backend corre en Docker o en otro puerto).
  const env = loadEnv(mode, process.cwd(), '')
  const backend = env.VITE_BACKEND_URL || 'http://127.0.0.1:8000'

  return {
    plugins: [react()],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    server: {
      port: 5173,
      // Sin CORS en desarrollo: /api/... se reenvía tal cual al backend, que ya
      // sirve sus rutas bajo /api (ver DOCS_JSON_API.md).
      proxy: { '/api': { target: backend, changeOrigin: true } },
    },
  }
})
