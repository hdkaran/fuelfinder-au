/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import sitemap from 'vite-plugin-sitemap';
import { suburbCentroids } from './src/data/suburbCentroids';

const suburbRoutes = suburbCentroids.map(
  (c) => `/suburbs/${c.state.toLowerCase()}/${c.slug}`,
);

export default defineConfig({
  plugins: [
    react(),
    sitemap({
      hostname: 'https://fuelstock.com.au',
      dynamicRoutes: [
        '/fuel-shortage-australia',
        ...suburbRoutes,
      ],
      exclude: ['/report', '/report/*'],
      changefreq: 'hourly',
      priority: 0.8,
      outDir: 'dist',
    }),
  ],
  server: {
    proxy: {
      '/api': 'http://localhost:5000',
      '/health': 'http://localhost:5000',
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
});
