# Guía para presentar TimeTrack en la entrevista

Las explicaciones están en español. Lo que está en bloques de cita (`>`) es lo que dices en inglés.

## Cómo presentarlo (sé honesto con el origen)

Preséntalo como un proyecto personal que construiste para mostrar tu forma de trabajar, no como un proyecto de cliente. Es más creíble, y te deja hablar de cada decisión como propia.

> This is a personal project I built to show how I structure a full-stack application with .NET and Angular. The domain is a timesheet and approval system for a consulting firm: consultants log hours against client projects, and managers approve them. I chose this domain because it has real business rules, roles and a workflow, not just CRUD.

Antes de la entrevista:

- [ ] Corre `npm run start:mock` y deja la app abierta en el login.
- [ ] Abre el repo en VS Code o Visual Studio con estos archivos en pestañas: `TimeEntry.cs`, `GlobalExceptionHandler.cs`, `project-picker.ts`, `week.page.ts`.
- [ ] Si tienes el SDK de .NET 10, corre `dotnet test` una vez para verificar que todo pasa en tu máquina.

## Demo de 5 minutos

### 1. La app funcionando (1 minuto)

Entra con `consultant@timetrack.dev` y `Demo123!`, muestra la semana anterior con el botón ‹ y haz clic en el jueves.

> Here's the consultant's week. Each column is a day, stacked by status: green is approved, amber is waiting for approval, red needs changes, and the striped part is still a draft. The dashed line is the 8-hour target, and the scale goes to 12, which is the daily limit.

Ve a "Log hours", escribe "azu" en el proyecto y pon 8 horas en un día que ya tenga horas.

> The project search waits until I stop typing, and the panel on the right warns me before I go over the 12-hour limit. The API enforces the same rule, so the UI warning is just for a better experience.

Cierra sesión, entra con `manager@timetrack.dev` y `Demo123!`, y aprueba algo.

> The manager sees the queue grouped by person, and can approve a whole week or send an entry back with a note.

### 2. Arquitectura del back-end (1,5 minutos)

Abre `TimeEntry.cs`.

> The back end follows Clean Architecture: Domain, Application, Infrastructure and Api. The domain has no dependencies. All the business rules live in the TimeEntry aggregate: you can't change the status directly, you have to call Submit, Approve or Reject, and each method checks its rules. So the rules can't be bypassed from a controller or another service.

Abre `GlobalExceptionHandler.cs`.

> Errors are handled in one place and returned as ProblemDetails: 400 for validation with field errors, 422 for business rules with a code, 409 for concurrency conflicts. Controllers don't have any try-catch.

### 3. Angular (1,5 minutos)

Esta es la parte clave para el líder de TCS. Abre `project-picker.ts`.

> This is a custom form control. It implements ControlValueAccessor, so it works with formControlName like a native input. The search uses debounceTime, distinctUntilChanged and switchMap, so if the user keeps typing, the previous request is cancelled and an old response never overwrites a new one. The catchError is inside the switchMap, so one failed request doesn't kill the stream.

Abre `week.page.ts`.

> For reads I use httpResource. When the week signal changes, it fetches again automatically and gives me value, isLoading and error as signals, with no manual subscriptions. The selected day is a linkedSignal: it resets when the week changes, but the user can still pick another day. And the project breakdown uses @defer on viewport, so it only loads when you scroll to it.

### 4. Calidad y DevOps (1 minuto)

> There are unit tests for the domain, tests for the application services with mocks, and integration tests that call the real API through WebApplicationFactory. In Angular, Vitest tests cover the interceptor and the debounce and cancellation of the search with fake timers. The Azure DevOps pipeline runs everything on every pull request, and docker compose starts the whole stack with SQL Server.

## Repreguntas probables y qué responder

**"Why didn't you use MediatR?"**

> For an application this size, plain application services are simpler to read and debug. MediatR adds indirection. Also, MediatR and AutoMapper moved to commercial licenses, so I prefer not to depend on them unless they add real value.

**"How do you handle two people editing the same entry?"**

> Optimistic concurrency. Each entry has a concurrency stamp that changes on every modification. The client sends the stamp it read; if it doesn't match, the API returns 409 and the UI asks the user to reload. EF Core also checks the stamp in the UPDATE statement, so even two requests at the exact same moment can't overwrite each other.

**"Why is the validation duplicated in the front end and the back end?"**

> The front-end validation is for user experience: instant feedback. The back end is the source of truth, because anyone can call the API directly. The validators in FluentValidation check the shape of the request, and the domain checks the business rules.

**"Why signals and not only RxJS?"**

> Signals are for state: a value that exists now. RxJS is for events over time. I use RxJS where the time dimension matters, like the search with debounce and cancellation, and signals for state in the templates. toSignal connects both.

**"Why zoneless?"**

> It's the default since Angular 21. Without zone.js, Angular doesn't check the whole tree after every event; it updates only what depends on the signal that changed. The bundle is smaller and the app is faster. Combined with OnPush everywhere, change detection is very predictable.

**"How would you deploy this to Azure?"**

> The API to Azure App Service or Container Apps, with Azure SQL Database, and the Angular app to Static Web Apps. Secrets like the JWT key and the connection string go to Key Vault, accessed with Managed Identity, so there are no credentials in the configuration. The pipeline would add a deploy stage with approvals between environments.

**"What would you improve?"**

> I would add refresh tokens and sign-in with Microsoft Entra ID, teams so managers only see their own people, and a deploy stage in the pipeline.

**"Did you use TDD in this project?"**

Ojo: la respuesta honesta es que el proyecto tiene tests de cada regla, pero no se escribió test-first. No digas que sí.

> The architecture is designed for it: the domain has no dependencies, the clock is injected with TimeProvider, and every repository is an interface, so the business rules can be developed test-first and tested in milliseconds. In this project every rule in TimeEntry has a test, and the test names read like the specification, for example Approve_OwnEntry_Throws. When the logic is complex, I like to start from the failing test: red, green, refactor.

**"How do you prevent N+1 queries?"**

> In the repository, I load the entries first, and then the project names and user names with one query each, using IN with all the ids. So it's always three queries, no matter how many entries there are.

## Si te preguntan algo que no hiciste

> I haven't implemented that in this project, but this is how I would approach it: …

Nunca inventes que algo está implementado. El código está ahí y te lo pueden pedir.
