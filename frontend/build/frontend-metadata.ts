import { createHash } from 'node:crypto'
import type { Plugin } from 'vite'

export function frontendMetadata(preview: boolean): Plugin {
  return {
    name: 'xke-frontend-metadata',
    generateBundle(_options, bundle) {
      const digest = createHash('sha256')
      for (const name of Object.keys(bundle).sort()) {
        const entry = bundle[name]!
        digest.update(name)
        digest.update(entry.type === 'chunk' ? entry.code : entry.source)
      }
      this.emitFile({ type: 'asset', fileName: 'frontend-build.json',
        source: JSON.stringify({ mode: preview ? 'preview' : 'runtime', build: digest.digest('hex').slice(0, 16) }) })
    },
  }
}
