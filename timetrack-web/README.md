# TimeTrack Web (Angular 22)

Front end of TimeTrack. See the [main README](../README.md) for the full picture.

```bash
npm install
npm run start:mock   # demo mode: no API needed, data simulated in the browser
npm start            # against the API at http://localhost:5080
npm run test:ci      # Vitest unit tests
npm run build        # production build to dist/
```

## Angular features worth looking at

| Where | What it shows |
| --- | --- |
| `app.config.ts` | Zoneless app, functional interceptors, router with component input binding. |
| `app.routes.ts` | Every page lazy loaded with `loadComponent`; functional guards. |
| `core/auth/auth.service.ts` | Session state as signals (`computed`), safe `localStorage` access. |
| `core/auth/auth.interceptor.ts` | Adds the JWT only to our API; logs out on 401 (except on the login call). |
| `features/week/week.page.ts` | `httpResource` re-fetching when the week changes, `linkedSignal` for the selected day, `@defer (on viewport)`, `forkJoin` to submit a whole week. |
| `features/week/week-strip.ts` | Presentational component: signal inputs and outputs, no services. |
| `features/entries/project-picker.ts` | `ControlValueAccessor` + RxJS search (`debounceTime`, `distinctUntilChanged`, `switchMap`, `catchError`) + accessible combobox. |
| `features/entries/entry-form.page.ts` | Typed reactive form, custom validators, server errors mapped onto controls, 409 conflict handling, `switchMap` to save then submit. |
| `features/entries/entries.page.ts` | Filters debounced with RxJS, converted with `toSignal`, feeding an `httpResource`. |
| `core/mock/` | Demo backend implemented as an HTTP interceptor with the same rules as the API. |
