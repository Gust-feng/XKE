import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { frontendMetadata } from './build/frontend-metadata.ts'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  const preview = mode === 'preview' || env.VITE_XKE_PREVIEW === 'true'
  return {
    plugins: [react(), frontendMetadata(preview)],
    define: { __XKE_PREVIEW__: JSON.stringify(preview) },
    base: './',
    server: { host: '127.0.0.1', port: 5182, strictPort: true },
    preview: { host: '127.0.0.1', port: 4182, strictPort: true },
    // Preview output never occupies the desktop runtime's frontend/dist path.
    build: { outDir: preview ? 'dist-preview' : 'dist', target: 'es2022' },
  }
})
