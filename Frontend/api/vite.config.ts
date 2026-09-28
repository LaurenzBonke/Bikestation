import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Die C#-API läuft lokal auf Port 5137 (siehe api/bikestation/bikestation/Properties/launchSettings.json).
// Alle Aufrufe auf /api werden im Dev-Server dorthin weitergeleitet.
// Anderer Port/Host (z. B. Raspberry Pi): Umgebungsvariable API_URL setzen.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': process.env.API_URL ?? 'http://localhost:5137',
    },
  },
})
