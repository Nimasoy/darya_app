import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'space.darya.app',
  appName: 'Darya',
  webDir: 'dist',
  server: {
    androidScheme: 'https'
  }
};

export default config;
