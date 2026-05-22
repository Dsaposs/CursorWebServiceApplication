// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  compatibilityDate: '2024-11-01',

  // Devtools must be disabled in production builds — its dependencies
  // are not installed when NODE_ENV=production (--omit=dev).
  devtools: { enabled: false },

  modules: ['@nuxtjs/tailwindcss'],

  // Capacitor requires a fully static build
  ssr: false,

  app: {
    head: {
      meta: [
        { name: 'viewport', content: 'viewport-fit=cover, width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no' },
        { name: 'format-detection', content: 'telephone=no' },
        { name: 'msapplication-tap-highlight', content: 'no' },
      ],
      link: [
        { rel: 'stylesheet', href: 'https://unpkg.com/ionicons@7/dist/ionicons/ionicons.css' },
      ],
    },
  },

  runtimeConfig: {
    public: {
      apiBaseUrl: process.env.NUXT_PUBLIC_API_BASE_URL
        || process.env.NUXT_API_BASE_URL
        || 'http://localhost:5294',
    },
  },

  tailwindcss: {
    config: {
      theme: {
        extend: {
          colors: {
            // Ionic-aligned dark palette
            surface: { DEFAULT: '#1a1a2e', raised: '#16213e', overlay: '#0f3460' },
            primary: { DEFAULT: '#7c3aed', light: '#a78bfa' },
            danger: '#ef4444',
          },
          fontFamily: {
            sans: ['-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
          },
        },
      },
    },
  },
});
