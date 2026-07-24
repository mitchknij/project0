import { defineConfig } from 'vite';
import { resolve } from 'node:path';

export default defineConfig({
  base: './',
  build: {
    rollupOptions: {
      input: {
        decisions: resolve(__dirname, 'index.html'),
        game: resolve(__dirname, 'game.html')
      }
    }
  }
});
