/**
 * Shareable demo: optimized production build, simulated API, and hash-based routes
 * (#/week) so the app works from any static host or link without server rewrites.
 */
export const environment = {
  production: true,
  apiBaseUrl: '/api/v1',
  useMockApi: true,
  useHashRouting: true,
};
