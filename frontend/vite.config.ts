import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { quasar, transformAssetUrls } from '@quasar/vite-plugin'

export default defineConfig(({ command, mode }) => {
  const repositoryRoot = resolve(__dirname, '..')
  const environment = loadEnv(mode, repositoryRoot, '')
  const useLocalHttps = command === 'serve' && mode !== 'test'

  if (useLocalHttps && !environment.CERT_PASSWORD) {
    throw new Error('CERT_PASSWORD no está configurado en el archivo .env del repositorio.')
  }

  return {
    plugins: [
      vue({ template: { transformAssetUrls } }),
      quasar({ sassVariables: resolve(__dirname, 'src/quasar-variables.scss') }),
    ],
    server: useLocalHttps
      ? {
          host: 'localhost',
          port: 5173,
          strictPort: true,
          https: {
            pfx: readFileSync(resolve(repositoryRoot, '.local/https/legaria-local.pfx')),
            passphrase: environment.CERT_PASSWORD,
          },
        }
      : undefined,
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/test/setup.ts'],
      css: true,
    },
  }
})
