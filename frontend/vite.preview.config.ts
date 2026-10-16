// Standalone design preview; not a production/runtime build.
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { viteSingleFile } from 'vite-plugin-singlefile'
import { frontendMetadata } from './build/frontend-metadata.ts'

export default defineConfig({
  plugins: [react(), viteSingleFile(), frontendMetadata(true)],
  define: { __XKE_PREVIEW__: 'true' },
  build: { outDir: 'preview', emptyOutDir: true },
})
