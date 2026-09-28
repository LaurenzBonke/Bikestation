import { defineConfig } from 'vite'

export default defineConfig({
  server: {
    proxy: {
      '/api.php': 'http://127.0.0.1:8000',
    },
  },
})