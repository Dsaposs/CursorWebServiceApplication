export default defineNuxtConfig({
  compatibilityDate: '2025-01-01',
  css: ['~/assets/css/app.css'],
  runtimeConfig: {
    apiBaseUrl: process.env.NUXT_API_BASE_URL || 'http://localhost:5294',
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
