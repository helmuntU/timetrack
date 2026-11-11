/**
 * Demo mode: the app runs without the .NET API. An HTTP interceptor answers every
 * request from an in-memory database with the same rules and error shapes as the API.
 */
export const environment = {
  production: false,
  apiBaseUrl: '/api/v1',
  useMockApi: true,
  useHashRouting: false,
};
