export default defineNuxtConfig({
  compatibilityDate: '2025-01-01',
  devtools: { enabled: false },
  css: ['~/assets/css/app.css'],
  routeRules: {
    '/sessions': { ssr: false },
    '/sessions/**': { ssr: false },
  },
  runtimeConfig: {
    apiBaseUrl: process.env.NUXT_API_BASE_URL || 'http://localhost:5294',
    llmBaseUrl: process.env.NUXT_LLM_BASE_URL || '',
    public: {
      appVersion: process.env.UI_VERSION || '2.0.0',
    },
  },
  app: {
    head: {
      title: 'TTRPG Table',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1.0' },
      ],
    },
  },
});
