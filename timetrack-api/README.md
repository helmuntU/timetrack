# TimeTrack API (.NET 10)

Back end of TimeTrack. See the [main README](../README.md) for the full picture.

```bash
dotnet run --project src/TimeTrack.Api   # http://localhost:5080, API reference at /scalar/v1
dotnet test                              # all test projects
```

`src/TimeTrack.Api/TimeTrack.Api.http` has ready-to-run requests (login, search, create, approvals) for Visual Studio, Rider or the VS Code REST Client.

## Endpoints

| Method | Route | Who |
| --- | --- | --- |
| POST | `/api/v1/auth/login` | Anyone (rate limited) |
| GET | `/api/v1/auth/me` | Signed in |
| GET | `/api/v1/projects?search=&page=&pageSize=` | Signed in |
| GET | `/api/v1/projects/{id}` | Signed in |
| POST | `/api/v1/projects` | Manager |
| GET | `/api/v1/time-entries?from=&to=&status=` | Signed in (own entries) |
| GET / PUT / DELETE | `/api/v1/time-entries/{id}` | Owner |
| POST | `/api/v1/time-entries` | Signed in |
| POST | `/api/v1/time-entries/{id}/submit` | Owner |
| GET | `/api/v1/approvals/pending` | Manager |
| POST | `/api/v1/approvals/{id}/approve` | Manager |
| POST | `/api/v1/approvals/{id}/reject` | Manager |
| GET | `/api/v1/dashboard/weekly-summary?weekOf=` | Signed in |
| GET | `/health` | Anyone |

## Error responses

| Status | When | Body |
| --- | --- | --- |
| 400 | Request shape is invalid (FluentValidation) | `ValidationProblemDetails` with camelCase field names |
| 401 | Missing or expired token, wrong credentials | `ProblemDetails` |
| 403 | Signed in, but not allowed (e.g. consultant on approvals) | `ProblemDetails` |
| 404 | Not found, or not yours | `ProblemDetails` |
| 409 | Stale `concurrencyStamp` | `ProblemDetails` |
| 422 | Business rule broken | `ProblemDetails` with `code`, e.g. `TimeEntry.DailyLimitExceeded` |

## Configuration

| Key | Default | Notes |
| --- | --- | --- |
| `Database:Provider` | `Sqlite` | `SqlServer` in Docker / Azure. |
| `ConnectionStrings:TimeTrack` | `Data Source=timetrack.db` | |
| `Database:SeedDemoData` | `true` in Development | |
| `Jwt:SigningKey` | (empty) | Required, 32+ characters. The app refuses to start without it. |
| `Cors:AllowedOrigins` | `http://localhost:4200` | |
| `RateLimiting:LoginPermitLimit` | `5` per minute per IP | |
