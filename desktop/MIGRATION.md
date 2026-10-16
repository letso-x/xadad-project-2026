# Desktop → shared API migration (16 October 2026)

The existing WinForms .NET Framework 4.7.2 client is retained. The API is
`https://xadad-project-2026.onrender.com/`. No historical JSON is imported,
overwritten or deleted. The API, not the desktop cache, owns shared data.

| Current file/class | Current source | Replacement (existing route) | Mapping | Risk / gap |
| --- | --- | --- | --- | --- |
| Repository | AppData/MzukuluQC/qc-db-v1.json | API-backed in-memory presentation cache | Numeric server IDs represented as strings in existing views | Old local records are preserved on disk; migration/import requires separate reconciliation |
| Auth, PostgresUserStore, DbConfig | Local PBKDF2 accounts / direct PostgreSQL | Supabase Auth REST + api/auth/me + api/auth/permissions | Supabase subject → ExternalAuthID → QMS user/permissions | Existing local-only accounts need Supabase provisioning/linkage by administrator; no API user-administration endpoint |
| MfaService, DeviceTrust | Locally issued email code / device bypass | Supabase TOTP challenge/verify, AAL2 | Verified factors and upgraded token | No local MFA bypass; unfinished enrollment must not delete verified factors |
| ClientsView | Repository.Db.Clients | GET/POST/PUT api/clients | ClientID, ClientName, contact components | Preserve hidden address/code fields on edits |
| ProjectsView | Repository.Db.Projects | GET/POST api/projects | ProjectID, ClientID, project/site/order names | No project PUT endpoint; API decides initial status |
| TemplatesView, TemplateEditorView, LibraryView | Local template definitions | api/checklist-templates, api/checklist-template-versions/{id}, revisions | Version IDs, dynamic field types/options/rules and sign capacities | Legacy editor cannot losslessly edit arbitrary dynamic templates; preserve unsupported fields |
| FormFillView | Local form and typed initials | api/qc-forms and response/project-field/submit/sign-off/evidence/correction/approve routes | Execution snapshot, response IDs, typed JSON values, revision | Signatures require actual image evidence; never translate typed initials into a verified signature |
| NcrView, NcrService | Local NCR state machine | api/qc-forms/{id}/ncrs, correct, verify | NCR identity/revision/evidence, server lifecycle | No standalone NCR creation or arbitrary state-edit endpoint; reject outcomes raise NCR server-side |
| RegisterView | Local cable register | api/projects/{id}/cable-registers, cables, verify | Register and cable revisions, server IDs | No cable deletion endpoint; optimistic concurrency must be preserved |
| ITP | No desktop implementation | api/projects/{id}/itps, progress, version, audit, actions | Version definition/executions/dependencies | Bind existing server model; do not recreate hold/release rules locally |
| AuditView, ReportsView, Analytics | Local fabricated audit and aggregates | QC form audit/audit-history; api/quality dashboards/readiness/notifications | Actor names/actions/timestamps, server summaries | No organisation-wide audit endpoint; scope audit to accessible QC forms |
| SettingsView, UsersView | Local organisation policy / users | No existing administration endpoint | Local display/idle preferences only | Shared policy/account edits unavailable; cannot save locally as substitute |

Build baseline: failed before edits (missing Npgsql dependency; ValueTuple conflict).
Implementation/build and runtime evidence is appended here as each slice completes.


## Implemented boundary

- The target remains WinForms/.NET Framework 4.7.2. Existing navigation, cards,
  dialogs, client/project lists and cable grid remain WinForms. Dynamic QC fields
  reuse the existing styled TextInput and A/R/N/A ResponseSelector. Client contact
  editing now binds name/email/phone separately to avoid corrupting structured DTOs.
- ApiConfiguration is the one desktop API-origin definition. HttpClient instances
  are reused; redirects are disabled for bearer-token requests. No localhost option
  or local database fallback exists.
- Supabase password login → verified TOTP factor or explicit one-time manual-key
  enrollment → challenge/verify → AAL2 → api/auth/me/permissions. Session material
  stays in memory. Refresh verifies subject/AAL2/factor continuity. Sign-out clears
  the presentation cache and rejects responses from the previous session.
- Clients and projects use returned server identities; QC numbering is allocated
  by the API. Writes never post an acting user ID as authority.
- QC reads the execution snapshot, including scope, acceptance, project fields,
  sections, field types/options/requiredness and answers. Writes use If-Match;
  acknowledged ETags advance the revision. Submission, drawn-signature evidence,
  signoff, correction and approval use the existing server workflow. The desktop
  does not decide approval validity or replace historical signatures.
- NCRs come from server outcomes. Correction uploads actual evidence before posting
  classification/disposition/correction; verification sends the NCR revision and
  passes independent-verifier authority checks to the service.
- Cable writes send both register and cable revisions and preserve hidden size/cores
  and untouched nullable values. Verification records the authenticated server actor.
- ITP is available from Project → Inspection & Test Plans. Definitions, dependencies,
  linked QC, stamps, submit/approve/revise and execution actions use existing routes;
  Hold/Witness/Review decisions remain server-side.
- Reporting uses server portfolio/project/readiness results. Audit shows server actor
  names/action/entity/time. Core events are paginated; the historical bridge returns
  the latest 100 per QC form. Local fabricated audit entries/quality policy are retired.
  Notification service wrappers exist; no new notifications screen was introduced.

## Exact endpoint contracts consumed

| Provider | Methods and routes |
| --- | --- |
| Supabase Auth | POST /auth/v1/token?grant_type=password; POST /auth/v1/token?grant_type=refresh_token; GET/PUT /auth/v1/user; POST /auth/v1/factors; DELETE /auth/v1/factors/{id} only for this flow's stale unverified enrollments at AAL1; POST /auth/v1/factors/{id}/challenge and /verify; POST /auth/v1/logout?scope=local |
| Identity | GET api/auth/me; GET api/auth/permissions |
| Clients | GET/POST api/clients; PUT api/clients/{clientId} |
| Projects | GET/POST api/projects |
| Templates | GET/POST api/checklist-templates; GET api/checklist-template-versions/{versionId}; POST api/checklist-templates/{templateId}/revisions?expectedVersionId={versionId} |
| QC | GET api/qc-forms?projectId={id}&page={page}&pageSize=100; GET/POST api/qc-forms[/{id}]; PUT api/qc-forms/{id}/responses/{responseId}, /project-fields/{fieldId}, /remarks; POST /submit, /approve, /return-for-correction; GET/POST /sign-offs; POST /evidence (multipart) |
| NCR | GET api/qc-forms/{formId}/ncrs; POST /{ncrId}/correct and /verify |
| Cable | GET/POST api/projects/{projectId}/cable-registers; GET/PUT /{registerId}; POST /{registerId}/cables?registerRevision={r}; PUT /{registerId}/cables/{cableId}?registerRevision={r}; POST /{registerId}/verify?revision={r} |
| ITP | GET/POST api/projects/{projectId}/itps; GET /progress; GET/PUT /{versionId}; GET /{versionId}/audit; POST /{versionId}/{action}, where action is submit, approve, revise, complete, release, witness, review or link-qc |
| Reporting/audit | GET api/quality/dashboard; GET api/quality/projects/{projectId}/dashboard and /handover-readiness; GET api/qc-forms/{id}/audit?afterId={cursor}&limit=100 and /audit-history |

`GET/POST api/qc-forms[/{id}]` above means GET list/detail and POST collection only;
there is no POST detail endpoint. Notification wrappers (not exercised by desktop UI):
GET api/quality/notifications; POST api/quality/notifications/{id}/read.

## Local data and retained gaps

- No active direct PostgreSQL access, Npgsql reference, DB connection configuration,
  PBKDF2 account store, SMTP-MFA service, device-MFA bypass or seed/reset path remains.
  Neither the backend nor mobile source was changed. No database migration is needed.
- `%LOCALAPPDATA%/MzukuluQC/qc-db-v1.json` is historical, untouched and no longer read.
  Its records/accounts have **not** been imported. Import requires explicit identity,
  numbering, snapshot and historical-signature reconciliation; it must not silently
  overwrite or duplicate the shared database.
- Only local inactivity preferences use plaintext JSON:
  `%LOCALAPPDATA%/MzukuluQC/desktop-preferences.json`.
- Unacknowledged QC values are cached as DPAPI CurrentUser-encrypted JSON bytes under
  `%LOCALAPPDATA%/MzukuluQC/api-drafts/{QmsUserId}/{formId}.bin`, with the base revision.
  This is a pending-input cache, not an alternate shared database. Failed/conflicting
  writes retain input; unreadable drafts are not overwritten. Discard requires an
  explicit user action. Tokens/passwords/setup keys are not persisted there.
- No existing project-update, user-administration, organisation-policy-write,
  standalone NCR-create or cable-delete endpoint was found. Unsupported actions
  explain their limitation instead of saving a local substitute.
- The legacy checklist editor publishes its inspection-item schema and preserves
  supported fields' evidence/rules. Arbitrary dynamic templates are usable but
  read-only in that editor; revise those with the existing mobile builder.
- Desktop registration is retired. Provision/link users through the existing
  Supabase/QMS administration process. Password change uses Supabase; password
  recovery currently directs users to the existing mobile recovery flow or their
  administrator because a desktop recovery callback is not registered.
- There is no organisation-wide audit endpoint, nor server equivalents for the old
  first-pass-yield/average-close-time calculations. The desktop does not fabricate
  those metrics or claim local export actions were centrally audited.

## Validation evidence — 16 October 2026

| Stage | Result |
| --- | --- |
| Before changes | FAIL: missing Npgsql symbols/dependency chain; ValueTuple assembly conflict |
| 1: API/session infrastructure | Build attempted; pre-existing driver dependency still prevented compilation |
| 2: Supabase auth/session retirement | Release PASS after retiring the direct-DB/local identity dependencies |
| 3: Clients | Release PASS |
| 4: Projects | Release PASS |
| 5: Templates/Form Library | Release PASS |
| 6: QC | Release PASS |
| 7: NCR | Release PASS |
| 8: Cable | Release PASS |
| 9: ITP | Release PASS |
| 10: Audit/reporting | Release PASS; temporary unused legacy-view warnings were removed during cleanup |
| Final Debug (isolated output) | PASS, 0 warnings / 0 errors |
| Final Release (isolated output) | PASS, 0 warnings / 0 errors |
| Migration regression executable | 47 passed / 0 failed (mocked HTTP + actual WinForms controls + Windows DPAPI) |
| Compile-input/source/binary audit | PASS: every compile input exists; live Render origin; no client localhost origin, direct DB provider/config, server credential marker or local account hashing |
| Public Render health | HTTP 200, {"status":"ok"} |
| Live user acceptance | User opened the verification flow and reported “it matches the data” after being asked to sign in/MFA and inspect Clients, Projects, Library, QC, NCR and ITP |

The normal Debug output could not be overwritten because an existing
`desktop/bin/Debug/MzuApplication.exe` was running (MSB3027/MSB3021). It was not killed.
The same source built in an isolated Debug output with zero compiler warnings/errors.
Close the running Debug application/debugger before rebuilding that normal path.

The live confirmation establishes shared-data visibility. Full live create/edit,
submit/sign/correct/resubmit/re-sign/approve, cable verification, ITP execution,
restart/offline-failure and desktop first-time MFA enrollment were **not** all run
against Render in this task. The regression harness verifies their request contracts,
state guards and retention behavior; it is not a claim of full end-to-end acceptance.

Outputs/logs (generated, gitignored):

- `artifacts/desktop-release/MzuApplication.exe` + its adjacent .config (final Release).
- `artifacts/desktop-debug/MzuApplication.exe` (isolated Debug).
- `artifacts/desktop-migration-tests-2026-10-16.log`.
- `artifacts/desktop-debug-isolated-2026-10-16.log`.
- `artifacts/desktop-release-isolated-2026-10-16.log`.
- `artifacts/desktop-source-audit-2026-10-16.json`.

Reproduce from a Visual Studio Developer PowerShell at the repository root:

```powershell
msbuild desktop/MzuApplication.csproj /p:Configuration=Release
msbuild desktop/MzuApplication.Tests/MzuApplication.Tests.csproj /p:Configuration=Release
& ./desktop/MzuApplication.Tests/bin/Release/MzuApplication.Tests.exe
```

The regression project is a self-contained .NET Framework console harness and does
not require a new NuGet test framework. All new runtime source files are explicitly
included in MzuApplication.csproj. Include the added files in any later commit/copy.

## Changed-file inventory

- `M .gitignore`
- `M desktop/App.config`
- `D desktop/Data/Analytics.cs`
- `M desktop/Data/Audit.cs`
- `M desktop/Data/Auth.cs`
- `D desktop/Data/DbConfig.cs`
- `D desktop/Data/DeviceTrust.cs`
- `D desktop/Data/EmailConfig.cs`
- `D desktop/Data/EmailSender.cs`
- `M desktop/Data/Exporter.cs`
- `D desktop/Data/MfaService.cs`
- `M desktop/Data/NcrService.cs`
- `D desktop/Data/PostgresUserStore.cs`
- `M desktop/Data/Repository.cs`
- `D desktop/Data/Seed.cs`
- `M desktop/Format.cs`
- `M desktop/Forms/ChangePasswordForm.cs`
- `M desktop/Forms/Dialog.cs`
- `M desktop/Forms/LoginForm.cs`
- `M desktop/Forms/MainForm.cs`
- `M desktop/Forms/MfaForm.cs`
- `D desktop/Forms/RegisterForm.cs`
- `M desktop/Models/Enterprise.cs`
- `M desktop/Models/Models.cs`
- `M desktop/MzuApplication.csproj`
- `M desktop/MzuApplication.slnx`
- `M desktop/Program.cs`
- `M desktop/Properties/AssemblyInfo.cs`
- `M desktop/Views/AuditView.cs`
- `M desktop/Views/ClientsView.cs`
- `M desktop/Views/DashboardView.cs`
- `M desktop/Views/FormFillView.cs`
- `M desktop/Views/LibraryView.cs`
- `M desktop/Views/NcrView.cs`
- `M desktop/Views/ProjectDetailView.cs`
- `M desktop/Views/ProjectsView.cs`
- `M desktop/Views/RegisterView.cs`
- `M desktop/Views/ReportsView.cs`
- `M desktop/Views/SettingsView.cs`
- `M desktop/Views/TemplateEditorView.cs`
- `M desktop/Views/TemplatesView.cs`
- `M desktop/Views/UsersView.cs`
- `M desktop/Views/ViewBase.cs`
- `D desktop/database.config.sample`
- `D desktop/email.config.sample`
- `D desktop/packages.config`
- `?? desktop/Controls/ApiFieldEditor.cs`
- `?? desktop/Data/ApiClient.cs`
- `?? desktop/Data/CableData.cs`
- `?? desktop/Data/DesktopSession.cs`
- `?? desktop/Data/FormData.cs`
- `?? desktop/Data/ItpData.cs`
- `?? desktop/Data/LocalDrafts.cs`
- `?? desktop/Data/LocalPreferences.cs`
- `?? desktop/Data/NcrData.cs`
- `?? desktop/Data/ReportingData.cs`
- `?? desktop/Data/SharedData.cs`
- `?? desktop/Data/TemplateData.cs`
- `?? desktop/Forms/SignatureForm.cs`
- `?? desktop/MIGRATION.md`
- `?? desktop/MzuApplication.Tests/MzuApplication.Tests.csproj`
- `?? desktop/MzuApplication.Tests/Program.cs`
- `?? desktop/Views/ItpView.cs`
