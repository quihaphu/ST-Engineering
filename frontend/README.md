# Frontend

React + TypeScript + Vite + Material UI product management UI.

See the [root README](../README.md) for stack, environment variables, and how to run with Docker Compose.

```bash
npm ci
npm run dev      # http://localhost:5173 — expects API at VITE_API_BASE_URL
npm run lint
npm run build
```

Default API base URL: `http://localhost:8080` (Compose). Override with `VITE_API_BASE_URL`.
