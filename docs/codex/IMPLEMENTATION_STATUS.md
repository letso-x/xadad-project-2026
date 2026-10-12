# Implementation status and evidence ledger

## Current candidate summary — 12 October 2026

Base ea90e96 on back-end-first-revamp plus uncommitted changes. **NOT READY / scope incomplete.**

- VERIFIED: API build (0 warnings/errors); API suite 76 passed / 0 failed / 0 skipped; offline-core suite 11 passed / 0 failed / 0 skipped; updated Android source compile-only 0 warnings/errors. Actual TestServer anonymous requests and DI graph tested.
- Implemented priority code: transactional QC correction/sign/approval and audit; immutable historical signoffs with hash v2; direct stale/current/legacy/authority/dual-signature applicability tests; explicit permissions/project scope; optimistic revisions/idempotency; NCR approval guards.
- Continued sequentially through documents 03–08 with partial offline/UI/core/reporting implementation, QA repairs and release documentation. These phases are not claimed complete.
- BLOCKED verification: no configured disposable populated PostgreSQL database or Supabase test identity/storage; actual schema, migrations, transactions, races and the signed correction loop remain unverified. All migrations unapplied; forms 8/9 and historical hashes untouched.
- INCOMPLETE: encrypted SQLite (plaintext SQLite header measured), complete capture/signature/conflict UI and offline new-form remapping, snapshot SQL replay/section constraints, full ITP/documents/calibration/training, exports/dossier/internal audit and notification coverage.
- Evidence: [verification](evidence/VERIFICATION.md), [acceptance ledger](ACCEPTANCE_TESTS.md), [setup and recovery](SETUP_AND_RECOVERY.md). Earlier entries below are preserved history, superseded by this summary where counts differ.

Checkpoint date: 12 October 2026. Package preparation uses the full pasted requirements and earlier shared schema discussion. No repository checkout, database, build, automated suite or device was inspected during package preparation. The initial entries below are source-reported history, not fresh verification. Every implementing agent must reconcile them with the actual repo before upgrading status.

## Status vocabulary

- VERIFIED WORKING: actual test/runtime evidence, with scope/environment/revision/date. Initial source-reported entries are explicitly historical until reverified.
- IMPLEMENTED BUT NOT VERIFIED: code exists or is reported to exist, but full required behavior is not proven. This category is not evidence that all acceptance criteria are implemented.
- NOT IMPLEMENTED / INCOMPLETE: required functionality missing or partially built.
- BLOCKED: a genuine external dependency prevents a named action; document the dependency and work that can proceed. Do not label ordinary remaining work blocked.

## Baseline ledger (source-reported)

| Area | Source-reported category | Evidence and limits | Next owner |
|---|---|---|---|
| API / Dapper / Npgsql / .NET 10 | VERIFIED WORKING — historical | API exists/builds per checkpoint; no build rerun in this package | Backend |
| Supabase Auth / QMS mapping / QmsUser / auth-me | VERIFIED WORKING — historical | Linked active Inspector exercised; no new token/auth runtime test here | Backend |
| Clients / projects | VERIFIED WORKING — historical | GET/create/update clients and GET/create projects substantially implemented; client existence/activity validation | Backend |
| Template/version retrieval / form creation / snapshot / nested retrieval | VERIFIED WORKING — historical | Template/version 5 and forms 8/9 reported; not inspected now | Backend |
| Project fields / response updates / dynamic validation | VERIFIED WORKING — historical | FieldValueValidator and authenticated editing exercised | Backend |
| Photo/Signature evidence / metadata/private storage / compensation | VERIFIED WORKING — historical | QCForm 8 upload proven; cleanup behavior reported; rerun failure path | Backend |
| Submission / Submitted edit lock | VERIFIED WORKING — historical | Required fields/photos checked and submit exercised | Backend |
| QCSignOff / signer identity / SHA-256 | VERIFIED WORKING — historical | QCForm 8 signoff 2, signature evidence 10; historical hash below | Backend |
| Submitted → Approved | VERIFIED WORKING — historical | QCForm 8 is reported Approved; does not prove current-signature approval guard | Backend |
| ReturnedForCorrection edits/resubmit code | IMPLEMENTED BUT NOT VERIFIED | Code reported; full runtime loop unproven | Backend / QA |
| Current-content signature validity / stale rejection / fresh signature / guarded approval | IMPLEMENTED BUT NOT VERIFIED | Checkpoint groups these as unverified but also calls stale-signature issue unresolved; implementation completeness must be inspected, not assumed | Backend |
| AuditEvent lifecycle integration | NOT IMPLEMENTED / INCOMPLETE | Table exists; actual schema/integration not inspected | Backend / Core |
| Fine-grained configurable permissions | NOT IMPLEMENTED / INCOMPLETE | QmsUser identity gate is not business authority | Backend |
| NCR / auto-link / verification / blocking approval | NOT IMPLEMENTED / INCOMPLETE | Full vertical slice required | Core |
| ITP / Hold / Witness / Review | NOT IMPLEMENTED / INCOMPLETE | Dependencies/authority/release enforcement required | Core |
| Controlled documents / calibration / training | NOT IMPLEMENTED / INCOMPLETE | Official modules remain required | Core |
| Reporting / dashboards / PDF / Excel / notifications | NOT IMPLEMENTED / INCOMPLETE | Inspect packages; avoid placeholder output | Reporting |
| Handover completeness / dossier | NOT IMPLEMENTED / INCOMPLETE | Mandatory record/signature/NCR gates required | Reporting |
| Internal audit business module | NOT IMPLEMENTED / INCOMPLETE | Distinct from technical AuditEvent trail | Reporting |
| Durable offline SQLite / files / outbox / retry / conflict / idempotency | NOT IMPLEMENTED / INCOMPLETE | Earlier local code is not production durability proof | Sync |
| Encrypted local DB / evidence protection | NOT IMPLEMENTED / INCOMPLETE | Required; ordinary SQLite is not encrypted | Sync / Release |
| Full MAUI mobile workflow | NOT IMPLEMENTED / INCOMPLETE | Earlier pages/issues reported; inspect current source | UI |
| Automated coverage / platform runtime QA | NOT IMPLEMENTED / INCOMPLETE | Earlier 19 passing tests, mostly validator/project service; current count unknown | QA |
| Hardening / setup / acceptance / release readiness | NOT IMPLEMENTED / INCOMPLETE | Final candidate unverified | Release |

## Known integration fixtures — inspect before use

These are checkpoint identifiers, not seed instructions or proof of current state. Use fresh synthetic records for state-changing tests; capture returned IDs. Preserve QCForm 8 and its historical signoff. Do not connect to production simply to find these IDs.

- QMS UserID: 4724a990-4bdb-4313-a62d-0a10247beeb1; ExternalAuthID: d1377b26-49b4-4a57-9a3f-531b5c7236d7; role Inspector; active=true. No credentials are included.
- ProjectID 1; ChecklistTemplateID 5; active ChecklistTemplateVersionID 5.
- Template project fields: 4 area_location, required Text maxLength 100; 5 drawing_number, optional maxLength 50.
- Cable Installation Details fields: 18 cable_number required; 19 from_location required; 20 to_location required; 21 installation_result required Dropdown Pass/Fail/N/A; 22 installation_photo required Photo, RequiresPhoto=true.
- QCForm 8: reported Approved; QCSignOffID 2; SignatureEvidenceID 10; historical ContentHash 5155df27b038feacde92fc75b2829b05b1d16381ce48e37f3d4f8346fab155e2. Preserve this hash exactly; do not recompute/overwrite history to match a new algorithm.
- QCForm 9: last reported Draft, QC-0009, project/template/version 1/5/5. Project fields 8 area_location required and 9 drawing_number optional. Response/field pairs: 33/35 cable_number; 34/36 from_location; 35/37 to_location; 36/38 installation_result; 37/39 installation_photo.
- Postman showed selected image but File: undefined for form 9. Earlier form 8 upload succeeded. This does not prove an API defect; reproduce real multipart request/server error before changing API.

## Current blockers and open decisions

No genuine external implementation blocker was confirmed by the checkpoint. Missing repo inspection/runtime access during package preparation is a verification limitation, not a claim the developer will be blocked.

Resolve by inspection/configuration: actual routes/DTOs/schema/enums; AuditEvent columns; evidence Document enum; final seven-role names/matrix; required signer/dual-signature authority; project resource access; instrument expiry boundary; blocking NCR classification; Witness/Review obligations; required handover record matrix; local encrypted storage package/platform support; supported offline session policy; notification channel. Do not fabricate official policy. Implement configurable foundations and explicit test fixtures while recording policy gaps; ask for a business decision only when correctness truly depends on it.

## Phase completion ledger

| Phase | Initial state | Required handoff |
|---|---|---|
| 02 Backend | Not started in this package | Baseline, lifecycle/hash/audit/permission proof, real DTO/error/concurrency contracts |
| 03 Offline Sync | Not started in this package | Local schema/migrations, encryption, services, queue/retry/conflict/idempotency proof |
| 04 MAUI UI | Not started in this package | Screens/routes, platform builds, offline/correction UX evidence |
| 05 Core | Not started in this package | NCR/ITP/docs/calibration/training contracts, guards/audit and tests |
| 06 Reporting | Not started in this package | Aggregates, export inspection, handover gates, internal audit/notifications |
| 07 QA | Not started in this package | Complete acceptance results, reproducible defects, candidate evidence |
| 08 Release | Not started in this package | Exact revision, reproducible setup/demo, security/operations review, readiness |

## Append one entry after every agent phase or material correction

Copy this entry structure and fill it with actual results. Preserve earlier entries and clearly supersede outdated conclusions.

### Phase entry — date / agent / scope

- Revision/branch and inspected paths:
- Baseline commands/results:
- Actual schema and existing contracts inspected:
- Changed paths and purpose:
- Migrations (apply order, test environment, recovery/data preservation):
- API/local-service/DTO/hash/permission contract changes:
- VERIFIED WORKING (acceptance IDs + test/runtime evidence + platform):
- IMPLEMENTED BUT NOT VERIFIED (exact missing verification):
- NOT IMPLEMENTED / INCOMPLETE (requirement IDs + owner/next action):
- BLOCKED (external dependency, impact, work still possible):
- Commands after changes / pass/fail/skip counts:
- Runtime artifacts or redacted evidence links:
- Security/data-retention concerns:
- Next agent handoff:

## Release decision

Initial decision: NOT READY / NOT ASSESSED. This is a handoff package, not a tested application release. Replace only after all mandatory M1–M11 requirements and acceptance gates are evaluated against a specific revision. Report incomplete/blocked platforms and encryption honestly; do not declare the project complete from build success or historical fixtures alone.

### 2026-10-12 — repository baseline / backend in progress

- Inspected actual API, tests and MAUI tree. No AGENTS.md or tracked SQL migrations found. Initial Git status: untracked docs/ only; preserved. SDK 10.0.401, no global.json.
- Baseline: dotnet test mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore: 19 passed, 0 failed, 0 skipped.
- Initial concurrent API build/test collided on obj DLL (CS2012); this was a verification orchestration error. Subsequent builds/tests will run sequentially. Test command compiled API successfully.
- Confirmed defects: approval has no signature guard; hash includes status/submission timestamp; evidence disallows ReturnedForCorrection; signing/checking and mutations use separate connections without a shared transaction; only QmsUser gate on QC routes.
- Database probe pending; no database mutation or historical record changes.
- Phase 02 INCOMPLETE; phases 03–08 pending sequential execution.

### 2026-10-12 — phase 02 implementation handoff to 03

- Backend changed: QC services/repositories share QmsDbSession transaction and form row lock; create/edit/evidence/submit/sign/correct/approve append audit in same transaction. Evidence correction editability repaired. Signature evidence must belong to signer/form, be fresh since submission and unused. Approval checks required snapshotted signoff types, v2 current hash, current authority/project access and fresh submission signatures.
- Additive migration: mobile/MzukuluQMS.Api/Migrations/001_qc_security.sql. Adds revision, HashVersion default 1, separate qms_security permission/project-access/signature-policy/audit tables and append-only history guards. NOT APPLIED; no connection configured. Existing public AuditEvent columns unavailable; new audit stream is explicit, integration with historical stream remains INCOMPLETE. No historical hash is recomputed.
- Hash v2: canonical JSON ordinal object keys, stable ordered arrays, typed values, normalised decimals and UTC SQL timestamps; excludes status/submittedAt and signature evidence/signoff collections. Snapshot fields/sections and required signoff policy are bound. Legacy hashes stay version 1 and are never treated as v2.
- Contract changes requiring WPF/mobile coordination: form GET/list includes Revision; form GET emits ETag; existing-form HTTP mutations require If-Match numeric revision. Correction now requires JSON {reason:string}, max 2000 chars. Create accepts optional ChecklistTemplateVersionID, rejecting a cached version that is no longer active. Added GET /api/qc-forms?projectId=&page=&pageSize=&status=, GET /{id}/sign-offs and GET /{id}/audit?afterId=&limit=. History applicability is separate from full approval authority.
- Fine-grained role grants and project access default to DENIED. QC permissions: ReadInspection, PerformInspection, SubmitInspection, ReviewInspection, ApproveInspection, SignInspection, SignInspection:TYPE, ReadAudit. Project/client/template routes now require QmsUser plus explicit ReadProjects/ReadClients/ReadTemplates and ManageProjects on writes. Project reads scoped to ProjectAccess. No final role matrix invented.
- Value requests retain {value: JSON primitive/array/object per snapshot validator}; argument=400, missing=404, conflict/invalid state=409, auth=401, forbidden=403, unexpected=500 with safe details. Photo/signature require PNG/JPEG headers and max 20 MiB; Document remains unsupported pending real enum inspection. Storage failure body no longer returned to callers; compensation failure logged without secrets.
- VERIFIED WORKING: baseline 19 and expanded 44 unit tests passed. Added signoff/identity tests pending current run. Backend build passed with one subsequently removed unused-parameter warning.
- IMPLEMENTED BUT NOT VERIFIED: database transaction/SQL/audit/grants/hash-version migration, real correction/sign/approval loop, storage cleanup and current-signature enforcement against PostgreSQL. Unit orchestration tests are not DB proof.
- BLOCKED: no ConnectionStrings:QmsDatabase in API User Secrets/environment; no psql found. SchemaProbe restore first failed restricted NuGet socket access; approved dotnet restore succeeded, probe ran and reported configuration absent. QCForms 8/9 not read or changed. Need a disposable populated DB copy/configuration, Supabase test identity/storage for runtime gates.
- INCOMPLETE: actual snapshot function replay/section-requiredness constraints, comprehensive project/template pagination, existing audit stream reconciliation and core NCR approval guards. Phase 02 is partial, not complete. Continuing independent phase 03 code now; no release claim.

### 2026-10-12 — phase 03 partial implementation / handoff to 04

- Added mobile/MzukuluQMS.Mobile.Core and .Tests: owner-scoped SQLite version 1; encrypted JSON payloads/evidence files; atomic local edit+outbox; dependency/record ordering; interrupted Processing recovery; dirty-cache preservation; bounded transient retry/auth pause/failed/conflict states; retained local/server conflict payloads; no reset/deletion migration path.
- VERIFIED WORKING (Windows SQLite tests only): dotnet test mobile/MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj --no-restore: 5 passed, 0 failed, 0 skipped. Reopen durability, dependencies, acknowledgement, conflict retention, owner separation, encrypted evidence tested. Not a forced Android/iOS process-kill result.
- Server idempotency added to workflow migration: optional UUID Idempotency-Key; same user/key and identical method/path/If-Match/body bytes replays persisted result; different bytes conflict. Result is persisted in the same transaction as state/audit. Multipart transport uses deterministic boundary/order. This is IMPLEMENTED BUT NOT VERIFIED against PostgreSQL.
- SyncRequest/HttpSyncTransport/SyncEngine handle known-server QC routes, JSON and multipart requests, current bearer from session callback, If-Match, idempotency and conflict snapshot retrieval. New-form snapshot ID reconciliation and automatic dependent-route remapping remain INCOMPLETE; UI must not pretend offline-created forms can sync fully yet.
- NuGet restore initially hit sandbox socket restriction; escalated restore succeeded. Initial cached SQLite native bundle 2.1.11 reported NU1903. Replaced with 3.0.5; restore completed without advisory warning (package page: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/).
- Release gaps: SQLite metadata is plaintext; encrypted payloads/files do not satisfy encrypted SQLite. SQLCipher/platform verification remains INCOMPLETE. Evidence capture helper does not yet atomically queue upload intent; no cleanup/deletion performed. Key storage platform adapter/UI wiring pending phase 04. No current unsynchronised data altered.
- Backend current unit suite: 54 passed; idempotency/race/migration runtime tests still absent. Phase 03 partial, not complete.

### 2026-10-12 — phase 04 partial implementation / handoff to 05

- Added FieldSession, InspectionWorkspace, FieldLoginPage/FieldProjectsPage/FieldFormsPage/FieldFormPage; App now opens authenticated navigation. Existing ProjectsPage/ViewModel preserved.
- Login uses Supabase password/refresh HTTP flow, checks /api/auth/me before initial admission and stores sessions only in SecureStorage. Account-specific local keys are retained on logout; absent key for an existing DB raises recovery error instead of generating a replacement. Conservative offline re-entry requires an unexpired previously linked session.
- Cached projects, known-server form list/resume and dynamic snapshot values use OfflineStore/application service. Saves queue JSON response/project-field writes; status locks and pending messages are visible. Submission queues behind values. No SQL/HTTP orchestration in pages. Conflict storage available in core; UI comparison/resolution not yet wired.
- INCOMPLETE: start/form library, full pagination navigation, photo/camera/gallery/preview, signature touch capture and consent/signoff sequence, review controls/reason retrieval, full conflict/sync screens, native accessibility/device verification, client metadata validation and new-form reconciliation. Photo/signature controls explicitly state unavailable rather than fake success.
- Android baseline build started before UI edits; outcome pending. Android restore/build after core reference pending. iOS requires a Mac/Xcode build host; no such runtime verified here. Phase 04 is not complete.

### 2026-10-12 — phase 05 partial core implementation / handoff to 06

- Added unapplied migration 002_ncr.sql creating an explicit new qms_core NCR stream/policy. Does not claim or overwrite unknown existing NCR tables. Reconciliation with real schema remains required.
- Dropdown Fail response updates create/reuse one open source NCR in the same QC transaction. Partial unique source-response index prevents duplicate open cases. Later Pass preserves existing case. Absent blocking policy creates unclassified blocking status; authorised correction explicitly classifies blocking/nonblocking. DefaultBlocking is configurable by template version.
- Added scoped GET /api/qc-forms/{formId}/ncrs, POST /{ncrId}/correct and /verify. Correct requires severity/classification/disposition/action/non-signature same-form evidence and NCR ExpectedRevision; Verify requires independent actor distinct from creator/corrector, revision and comments. Passing verification closes; failed verification remains open. All writes use form If-Match, permission, transaction and audit.
- Approval now rejects unresolved blocking or unclassified NCRs. Permissions: ReadNcr, CorrectNcr, VerifyNcr. All grants remain explicit.
- CoreRules adds tested dependency cycle/prerequisite/Hold and inclusive calibration date rules, but these are NOT connected to ITP/calibration endpoints/persistence; do not count those modules as implemented.
- INCOMPLETE: NCR UI/separate corrective-document storage, existing module reconciliation, full NCR PostgreSQL/runtime tests; ITP controlled versions/Witness/Review/server endpoints; controlled documents; instrument persistence/selection integration; training/competency. These remain implementation work, not external blockers.
- BLOCKED verification: real schema and disposable DB configuration still unavailable. No production/historical data touched.

### 2026-10-12 — phase 06 partial reporting / handoff to QA

- Added unapplied 003_reporting.sql: explicit project/version required-record matrix and in-app notifications, preserving existing unknown reporting data.
- Added authorised GET /api/quality/projects/{id}/dashboard (real QC counts and open NCR total; absent module metrics explicitly listed), /handover-readiness (missing matrix/approved-current-signature/NCR reasons), /notifications and POST /notifications/{id}/read. ReadReports/ReadHandover plus project scope required.
- Readiness deliberately remains IsReady=false because mandatory ITP/document/calibration/training/evidence completeness gates are not integrated. No fake final dossier endpoint or final/complete export provided.
- Returned-form notification generated transactionally for active started/assigned users with current ReadInspection/project grants; per-user/form/revision/kind uniqueness prevents duplicate alerts. In-app only; no external email/SMS sent. NCR/other notification triggers remain incomplete.
- INCOMPLETE: portfolio/full module metrics, PDF/Excel exports and visual verification, final dossier/history, internal audit programme/findings/follow-up closure, complete notification coverage. No PDF/Excel dependency introduced without a usable export slice. Phase 06 partial, not complete.

### QA repair — startup configuration / 2026-10-12

- Latest pre-repair run: 61 passed, 6 failed, 0 skipped (67 total); all six real TestServer startup/HTTP tests failed because Program read/validated auth configuration before host test configuration could apply. Failure retained at evidence/backend-startup-failed.trx. This is not a green result.
- Repaired configuration timing: SupabaseAuth now binds/validates HTTPS metadata and audience through DI options with ValidateOnStart; JWT options consume validated configuration. ES256/issuer/audience/signature/lifetime/MapInboundClaims/sub behavior retained. No test-only authentication bypass introduced. Rerun pending.
- Offline suite now 11 passed / 0 failed / 0 skipped, including actual SQLite persistence and sync HTTP-status/account-isolation orchestration.
- Android source compile-only after UI integration: dotnet build mobile/MzukuluQMSMobile/MzukuluQMSMobile.csproj -f net10.0-android --no-restore -t:Compile: 0 warnings / 0 errors. Earlier baseline full Android build completed 0/0 before UI changes; no final packaging/runtime claim. TargetFrameworks override attempts caused NU1201/NETSDK1005 assets errors; normal multi-target restore repaired assets.

- Startup rerun after DI options repair: 66 passed, 1 failed. Remaining failure was the test synchronously disposing an async-only QmsDbSession scope; corrected test to use CreateAsyncScope/await using. Kept evidence/backend-scope-disposal-failed.trx. Five actual anonymous HTTP tests now returned 401. Final rerun pending.
- Direct synthetic local DB header inspection returned SQLite format 3\0, confirming the encrypted-SQLite gate is unmet. No adb executable on PATH; no device runtime verification was performed.

### Phase 07–08 review — 2026-10-12 / candidate ea90e96 + working tree

- Reviewed DI/startup/auth, shared transaction/row locks, idempotency ordering, current signature fields/types, revision handling, cancellation/connection disposal, retained conflicts and client secret patterns. Snapshot/SQL/storage behavior still needs an actual disposable PostgreSQL environment.
- Repaired review findings: required signature field evidence/authority guard, signature evidence field eligibility, Signature upload uses SignInspection permission, required empty strings/arrays denied on submit while zero/false remain valid, optional Photo handling, assigned-user active/project access, database-owned SignedAt, connection cleanup on open/begin failure, bounded evidence copy and failed local UI save restoration.
- Added real TestServer anonymous HTTP and DI tests, fingerprint request/resource tests, dependency/Hold/calibration domain tests, NCR independence tests and sync HTTP/auth/account tests. Failed startup runs preserved; final results pending below.
- Mobile source scan found no service_role or PostgreSQL URL pattern in current client tree. Public anon-key constant is a placeholder/non-JWT; no real credential/session/device login was used. This narrow scan is not a full shipped-binary security audit.
- Setup/recovery/migration/compatibility/demo/retention instructions added at SETUP_AND_RECOVERY.md; README now distinguishes design goals from measured state and uses actual .NET 10 guidance. No production migration, deploy, merge, push or commit performed.
- Release remains NOT READY. Phases 02–06 are partial implementations; QA/release review records gaps without waiving mandatory requirements. Live DB/auth/storage, Android/iOS runtime, SQLCipher encryption, full signature/mobile flows and remaining modules are release gates.

### Final measured results / next implementation handoff — 2026-10-12

- API tests: 76 passed / 0 failed / 0 skipped; offline tests: 11 passed / 0 failed / 0 skipped. Final TRX files copied to evidence/. No failing tests suppressed. Previous startup failures retained separately.
- Production SignatureRules now directly checks v2 hash equality, submission/evidence freshness, active signer, generic and type-specific authority, project access, same-form/uploader/type evidence, required types and required signature field IDs. Tests demonstrate stale refusal, fresh replacement without history rewrite, legacy exclusion, renewed consent after no-change resubmission, authority/evidence denial and dual signatures. PostgreSQL query integration remains unverified.
- API standalone build and updated Android compile-only both succeeded with 0 warnings/errors. Android full packaging after modifications and all device/iOS runtime remain unverified.
- No QCS-01–QCS-11 PDFs/authoritative form definitions found in repository asset search; definitions cannot be invented. Existing prototype/WPF ownership not modified.
- Intentional client compatibility changes: mandatory current If-Match on existing-form mutations; correction JSON reason; protected project/client/template routes; new HashVersion; optional Idempotency-Key. Both clients need these contracts configured before runtime use.
- Next required external input: configure an explicitly disposable populated PostgreSQL copy through server User Secrets/environment, with Supabase test identity/private storage. Inspect real AuditEvent/generator/enums and reconcile new schemas before any migration. Then run the synthetic signed correction/concurrent/idempotency/audit-rollback flows without modifying fixtures 8/9.
- Independent remaining implementation: metadata-preserving snapshot replay guard after schema inspection; SQLCipher/platform key recovery, complete evidence/signature/conflict/new-form mobile paths; full mandatory core/report/export/internal-audit modules. All remain listed as incomplete, not waived or counted working.
- Release decision: NOT READY. No commit, push, merge, deployment, production mutation or external notification performed.

### Final recheck and encryption dependency boundary — 12 October 2026

- Repeated `dotnet build mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 10.09 seconds. `git diff --check`: exit 0 (line-ending notices only).
- Investigated supported SQLite encryption packages before changing the working provider. The SQLitePCLRaw maintainer states that the free encryption-enabled bundle is deprecated in v3 and recommends licensed encryption builds: [maintainer migration notes](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md). [NuGet bundle page](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher/) lists 2.1.11. No obsolete provider substitution, purchase, key rotation or local database conversion was performed. Full database encryption remains INCOMPLETE and needs a supported native provider, platform validation and non-destructive conversion/recovery design; this does not block independent API work.
