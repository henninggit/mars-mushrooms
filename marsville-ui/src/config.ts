/**
 * Runtime configuration derived from build-time environment variables.
 *
 * In development the Vite dev-server proxy forwards /api/* and /hubs/* to
 * localhost:5181, so apiBase can be an empty string (relative paths).
 *
 * In production set VITE_API_BASE_URL to the full Azure App Service origin,
 * e.g. https://marsville-backend.azurewebsites.net
 */
const rawBase = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '';

export const apiBase = rawBase.replace(/\/$/, '');
export const hubUrl = `${apiBase}/hubs/game`;
