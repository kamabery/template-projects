import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import { VitePWA } from 'vite-plugin-pwa'

/**
 * Where to proxy `/api`.
 *
 * Under the Aspire AppHost, `WithReference(api)` injects the API's resolved
 * endpoints as `API_HTTP` / `API_HTTPS`. Outside it, fall back to the http
 * profile in template.api/Properties/launchSettings.json.
 */
const API_TARGET =
  process.env.API_HTTPS ??
  process.env.API_HTTP ??
  process.env.VITE_API_TARGET ??
  'http://localhost:5058'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    // Must come before the react plugin so generated routes are transformed.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    /**
     * Progressive web app: installable, with an offline app shell.
     *
     * `registerType: 'autoUpdate'` + `injectRegister: 'auto'` is what keeps
     * src/main.tsx free of any registration code - the plugin injects the
     * registration script into index.html instead. autoUpdate is also the right
     * pairing with a service worker that is live in dev (below): it calls
     * skipWaiting, which is the escape hatch from a worker serving a stale
     * module over HMR.
     *
     * The colors are static snapshots of the tokens in src/index.css, because a
     * manifest cannot express `light-dark()`. They are placeholder brand assets
     * - a scaffolded project replaces them.
     */
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: 'auto',
      includeAssets: ['favicon.svg', 'icon-maskable.svg'],
      manifest: {
        name: 'template.ui',
        short_name: 'template.ui',
        description: 'Aspire + React reference front end.',
        id: '/',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        background_color: '#fafcfe',
        theme_color: '#2b7ad6',
        icons: [
          {
            src: 'favicon.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'any',
          },
          {
            src: 'icon-maskable.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,svg,woff2}'],
        navigateFallback: '/index.html',
        // The API is same-origin here - the proxy below in dev, YARP after
        // PublishAsStaticWebsite. Without this denylist the worker answers
        // /api/* navigations with the app shell.
        navigateFallbackDenylist: [/^\/api\//],
        // No runtimeCaching: API responses are deliberately never cached.
      },
      // Live in dev too. Vite serves unhashed ES modules there, so this worker
      // precaches nothing - it registers and supplies navigateFallback only.
      // If it ever does serve something stale: Application > Service Workers >
      // Unregister, then hard reload.
      devOptions: { enabled: true, type: 'module', navigateFallback: 'index.html' },
    }),
  ],
  /**
   * `WithOtlpExporter(OtlpProtocol.HttpProtobuf)` in the AppHost sets the
   * standard `OTEL_*` variables on this process. Vite only forwards prefixed
   * variables to the bundle, so widen the prefix list to let browser RUM read
   * them from `import.meta.env` (see src/lib/telemetry.ts). Outside the
   * AppHost none are set, which leaves RUM switched off.
   */
  envPrefix: ['VITE_', 'OTEL_'],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: Number(process.env.PORT) || 5173,
    proxy: {
      // Keeps the browser same-origin, so the API needs no CORS config and
      // `traceparent` flows from browser spans into the API's spans.
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
