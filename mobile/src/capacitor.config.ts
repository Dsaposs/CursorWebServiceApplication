import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.ttrpg.table',
  appName: 'TTRPG Table',
  webDir: '.output/public',
  // Dev server tunnelling for live reload against a local API during development
  server: {
    androidScheme: 'https',
  },
  plugins: {
    PushNotifications: {
      presentationOptions: ['badge', 'sound', 'alert'],
    },
    StatusBar: {
      style: 'Dark',
      backgroundColor: '#1a1a2e',
    },
  },
};

export default config;
