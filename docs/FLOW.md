# TimeTrack flow

The Mermaid sources also live in `docs/diagrams/*.mmd`. To edit them in draw.io: Arrange › Insert › Advanced › Mermaid.

## User flow

Every user logs hours from My week. Managers also get the Approvals tab, where submitted entries arrive grouped by person.

```mermaid
flowchart TD
  open([Open TimeTrack]) --> guard{Valid session?}
  guard -- No --> login[Sign in]
  login --> creds{Email and password OK?}
  creds -- "No: 401, or 429 after too many tries" --> login
  creds -- Yes --> week
  guard -- Yes --> week

  subgraph everyone [Every user]
    week[My week<br/>hours per day, by status] --> form[Log hours]
    form --> project[Search project as you type]
    project --> details[Date, hours, description<br/>checked as you type: 15-minute steps,<br/>max 8 h per entry, max 12 h per day]
    details --> valid{Valid?}
    valid -- No --> details
    valid -- Yes --> save{Save how?}
    save -- Save draft --> draft([Draft])
    save -- Save and send --> submitted([Awaiting approval])
    draft -- "Send all drafts" --> submitted
    draft -- "Edit or delete" --> form
  end

  subgraph managers [Managers only]
    queue[Approvals<br/>queue grouped by person] --> decide{Decision}
    decide -- Approve --> approved([Approved, locked])
    decide -- Ask for changes --> note[Write a note for the consultant]
    note --> rejected([Needs changes])
  end

  week -. "Approvals tab" .-> queue
  submitted --> queue
  rejected -- "Consultant edits and resubmits" --> form
  expired[[Session expires: any call returns 401]] -.-> login

  classDef draftState fill:#6b7385,stroke:#6b7385,color:#ffffff
  classDef submittedState fill:#b7790f,stroke:#b7790f,color:#ffffff
  classDef approvedState fill:#2f855a,stroke:#2f855a,color:#ffffff
  classDef rejectedState fill:#c53030,stroke:#c53030,color:#ffffff
  class draft draftState
  class submitted submittedState
  class approved approvedState
  class rejected rejectedState
```

## Entry lifecycle

Status only changes through methods on the `TimeEntry` aggregate, so these are the only transitions the API allows.

```mermaid
stateDiagram-v2
  direction LR
  state "Draft" as Draft
  state "Awaiting approval" as Submitted
  state "Approved" as Approved
  state "Needs changes" as Rejected

  [*] --> Draft: Log hours
  Draft --> Draft: Edit
  Draft --> [*]: Delete
  Draft --> Submitted: Send for approval
  Submitted --> Approved: Manager approves
  Submitted --> Rejected: Manager asks for changes
  Rejected --> Draft: Consultant edits
  Rejected --> Submitted: Resubmit
  Approved --> [*]

  note right of Approved
    Locked: can't be edited or deleted
  end note
  note left of Rejected
    A note from the manager is required
  end note
```

## Saving an entry, step by step

Angular checks the form first for fast feedback; the API checks everything again because it is the source of truth.

```mermaid
sequenceDiagram
  autonumber
  actor U as Consultant
  participant A as Angular entry form
  participant I as authInterceptor
  participant P as API pipeline
  participant S as TimeEntryService
  participant D as TimeEntry (domain)
  participant DB as EF Core and SQL Server

  U->>A: Save and send for approval
  Note right of A: Form validators: 15-minute steps, max 8 h, no future date
  A->>I: POST /api/v1/time-entries
  I->>P: Adds the Authorization: Bearer token
  Note right of P: Rate limit, validate JWT, check authorization
  P->>S: CreateAsync(request)
  Note right of S: FluentValidation checks the request shape
  S->>DB: Is the project active? Hours already logged that day?
  DB-->>S: Project and daily total
  S->>D: TimeEntry.Create(...) applies the business rules
  D-->>S: New entry in Draft
  S->>DB: SaveChanges
  S-->>A: 201 Created with the entry
  A->>I: POST /time-entries/{id}/submit, chained with switchMap
  I->>P: Same token
  P->>S: SubmitAsync(id)
  S->>D: entry.Submit()
  S->>DB: SaveChanges, checking the ConcurrencyStamp
  S-->>A: 200 OK, status Awaiting approval
  A-->>U: Toast: Hours saved and sent for approval

  Note over P,D: Any exception goes to GlobalExceptionHandler and returns ProblemDetails: 400, 404, 409 or 422
```

| Response | Cause | What the user sees |
| --- | --- | --- |
| 400 | The request is malformed (FluentValidation) | The message under the matching form field |
| 401 | Token missing or expired | The interceptor ends the session and opens Sign in |
| 403 | A consultant calls an approvals endpoint | Nothing: the Approvals tab is hidden, and the API refuses anyway |
| 404 | The entry doesn't exist or belongs to someone else | "This entry doesn't exist or you can't open it" |
| 409 | Someone changed the entry since it was loaded | A warning with a Reload entry button |
| 422 | A business rule is broken, e.g. more than 12 h in one day | The rule's message above the form |
| 429 | Too many sign-in attempts from the same address | An error on the Sign in page |
