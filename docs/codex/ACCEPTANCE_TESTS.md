# Acceptance tests — Mzukulu QMS

## Execution and evidence rules

Read 00_READ_ME_FIRST.md and the complete master specification first. All cases start NOT RUN FOR CURRENT REPOSITORY. Source-reported historical success is recorded in IMPLEMENTATION_STATUS.md and does not change these fresh acceptance results. Run against an explicitly identified disposable/non-production environment, authorised users and synthetic records. Preserve QCForm 8. Discover actual endpoints/DTOs/schema; use returned IDs. Never log bearer tokens, passwords, secret configuration or private evidence URLs.

For each case record date, revision, environment/device/OS, preconditions/fixture IDs, exact command or UI steps, expected/actual result, PASS/FAIL/NOT RUN/BLOCKED, and redacted test/runtime evidence. Write evidence in the ledger below or a repository evidence file linked from it; update IMPLEMENTATION_STATUS.md. Unit tests, integration tests and device runtime observations are complementary; mark their scope. Missing iOS tooling is not an iOS pass. Failures and skips must remain visible.

## AT-01 — baseline, configuration and identity (M1)

Discover solution/API/MAUI/tests, pinned SDK, workload/platform requirements and configuration names. Restore/build API and supported MAUI targets; run all existing tests. Record baseline failures. With valid Supabase session call actual /api/auth/me: active linked user receives QMS identity; missing/expired/bad issuer/audience/signature token rejected; unlinked/inactive QMS user denied protected operations. Client-posted actor/role cannot impersonate. Verify ES256 metadata validation and no secret in client bundles/logs. Test actual middleware HTTP semantics, not only mocked services.

## AT-02 — clients/projects/resource access (M1/M2)

Exercise client list/detail/create/update/inactive behavior and project list/detail/create. Missing/inactive client and duplicate/business-invalid project are rejected. Authorised users see permitted records; unauthorised resource access is denied even if an ID is guessed. Pagination/empty results work. Capture exact current routes and validation responses.

## AT-03 — template snapshot and two-field regression (M4)

Create authorised Equipment Inspection version 1, Inspection Details, equipment_number required Text and inspection_result required Dropdown [Pass,Fail,N/A], with project fields/options/validation metadata. Start form from that version. API nested GET and MAUI show both fields, stable order and metadata. Publish a later version adding/changing fields; existing form remains version 1 and unchanged. Repeated/concurrent create with same operation key yields one form/snapshot. Wrong template/version and cross-form field/response/evidence references fail. Failed snapshot generation leaves no partial form. Verify required section/project-field metadata is retained.

## AT-04 — dynamic values and local/server validation (M4/M5)

Exercise Text/LongText, Number/Decimal, Boolean/YesNo, Date/DateTime, Dropdown, MultiSelect, Photo, Signature and Reference. Required null/empty rejected where applicable; false and zero are valid answered values when allowed. Check maxLength, options, ranges/tolerances, N/A semantics and numeric/date culture. Illegal JSON/type/value is rejected by server FieldValueValidator even if UI is bypassed. Unknown field type fails visibly without crashing or pretending a response was saved.

## AT-05 — evidence and private access (M1/M4/M5)

POST multipart EvidenceType, optional QCFormFieldID and File to /api/qc-forms/{id}/evidence. Verify Photo/Signature, size/type/content validation, wrong form/field denial, authenticated uploader, private qms-evidence storage and safe unique path. Verify Document against real enum before testing it as supported. Force metadata DB failure after upload: storage object cleaned or explicit recoverable cleanup issue retained. Unauthorised evidence retrieval denied. Reproduce multipart failure with server evidence rather than interpreting Postman File: undefined as an API bug.

## AT-06 — submission and editability (M4/M5)

Draft/InProgress/ReturnedForCorrection: normal response/project-field/evidence edits allowed under authority. Submit with missing required project field, non-photo/signature answer or photo: validation fails, no partial transition. Complete data: submit produces Submitted and timestamps. Submitted ordinary edits/photos/documents denied; controlled Signature path allowed. Approved/Cancelled deny normal/evidence mutation. Invalid transitions return meaningful conflict. Submission enables signing and does not require a signoff that can only be created after Submitted.

## AT-07 — permissions and approval guards (M1/M4/M6)

QmsUser alone cannot approve/review/release Hold. Test explicit allowed/denied permissions and project scope. Only Submitted can approve. Missing required signature, stale hash, wrong signing authority/dual-signature policy or open blocking NCR denies approval. Valid current authorised signatures and resolved prerequisites permit approval; status/audit update atomic. Concurrent state/edit/approval attempts cannot bypass guards.

## AT-08 — signoff identity, hash and determinism (M1/M4)

Submitted form accepts authorised signoff referencing same-form Signature evidence uploaded by signer. Wrong type/form/uploader and client attempts to own UserID/SignedAt/ContentHash rejected or ignored according to documented contract. Hash is deterministic 64-char lowercase SHA-256 over canonical signable content. Changes to signable responses/project values/non-signature evidence change hash; signature upload/signoff/approval metadata alone does not invalidate the signed content unexpectedly. Historical hashes/rows remain immutable. Record hash-contract compatibility if algorithm repair is needed.

## AT-09 — correction loop and stale signatures (M1/M4/M5)

Fresh form: complete → submit → upload/sign → return for correction with reason → edit answer/project value or non-signature evidence → resubmit. Old signoff still visible in history with unchanged hash but is not current. Approval before fresh required signoff fails. Capture fresh signing consent/evidence/signoff; approval succeeds only after all guards. Verify persisted reason, actor/time and history. Test no-content-change and repeated correction behavior against documented current-signature policy; never rewrite old hashes to force validity.

## AT-10 — business audit (M1 and all modules)

For form create, answer/project edit, evidence, submit, correction, sign, approve, NCR actions, ITP edits/Hold release, document revisions and calibration changes, verify actor/action/affected entity/timestamp and preserved sequence. Client cannot forge audit actor/time. Critical database state transitions and required audit writes are atomic; injected audit failure cannot produce unaudited success. Audit access is authorised and logs contain no session secrets. Internal audit module is tested separately.

## AT-11 — actual SQL constraints and migration safety (all)

Inspect actual enums/schema/function signatures; test migrations from current populated version in disposable DB. Form/version and field/form/evidence/signature relationships enforced; no duplicate snapshot on generation replay. Parameterised SQL, transaction rollback and conditional updates verified against PostgreSQL. Preserve historical signoffs/forms/revisions/audit. Empty forms/sections remain retrievable and summary counts are accurate without phantom unanswered rows or duplicate joins. Test rollback/recovery plan rather than assuming old CREATE IF NOT EXISTS script upgrades safely.

## AT-12 — list/resume API contracts (M2/M4/M5)

Scoped/paginated project/form/template libraries support start/resume, ReturnedForCorrection and relevant statuses. DTO metadata matches renderer/local repository, error shapes and concurrency token documented. Nested form loads project fields/sections/fields/responses/evidence/signoffs without replacing a snapshot with live template. Cancelled/approved are visible only under permitted read rules and not resumable for normal edit.

## AT-13 — offline persistence and forced restart (M5)

Authenticate/cache eligible projects/templates/forms, make API unreachable, edit responses/project fields and save photo/signature locally. Terminate the process without graceful shutdown, reopen: values/files/operations remain and correct pending states shown. No raw SQL in Pages and no network requirement for each field save. Test device restart and schema upgrade of dirty local DB; no reset/deletion to resolve migration errors.

## AT-14 — sync success, dependency order and restart recovery (M5)

Reconnect and sync new form mapping → values/evidence → submission/signature/signoff in authorised order. Local IDs become correct server mappings; form/field ownership remains valid. Successful acknowledged operations stop retrying; interrupted Processing resumes safely after process kill. Cache refresh does not overwrite dirty local values. User can close/reopen mid-sync without losing operation/file or claiming synced prematurely.

## AT-15 — transport/auth failure recovery (M1/M5)

Simulate timeout, DNS failure, refused/reset connection, API unreachable with NetworkAccess.Internet, 500, 401, 403 and business validation failure. Transient errors retain data and retry with bounded policy; auth refresh/relogin pauses and resumes correctly; forbidden/permanent failures surface action rather than endless retry. Passwords never persisted plaintext; tokens in SecureStorage. Logout/session expiry retains unsynced work securely; switching users cannot upload or display another user's protected work.

## AT-16 — lost-response idempotency and concurrency (M4/M5/M6)

Server completes create/evidence/signoff or Fail response, then drop response and retry same operation ID. Exactly one logical result/side effect and stable mapping; same key/different payload conflicts. Concurrent duplicate requests safe. Test storage/DB split failures and cleanup. Distinct legitimate operations remain distinct. Capture server evidence: local queue flags alone do not prove idempotency.

## AT-17 — conflict preservation and encrypted local storage (M1/M5)

Edit cached record offline while central record/version/status changes. Sync rejects stale write as conflict; local and server values/context preserved across restart, user can inspect failed action, no silent merge/overwrite/discard. Verify encrypted SQLite on Android/iOS with documented inspection method and secure key handling; ordinary SQLite plus SecureStorage fails the DB encryption requirement. Verify local evidence protection and note platform/recovery limits. If unavailable mark requirement incomplete/release gate, never PASS.

## AT-18 — mobile navigation and capture UX (M5)

Login → dashboard → cached projects/details → form library → start/resume dynamic form. Camera/gallery/signature clear/redraw/save, permissions denied/cancelled, long fields, validation, back navigation and app resume work without critical crashes. Historical blank Clients/Search, project crashes/checklist crashes and Shell routing checked where relevant. Status and pending/conflict/error indicators reflect real state. Android runtime success and iOS runtime/build evidence recorded separately.

## AT-19 — NCR end to end and independent close-out (M6)

Fail a field → one linked NCR/punch with source project/form/field/response → severity/classification/disposition → corrective action/evidence → independent authorised verification → close with timestamps/audit. Repeated save/retry/concurrent Fail reuses appropriate open NCR. Failed verification remains open; same-person verification blocked where independence required. Later Pass does not erase history. Unresolved blocking case prevents approval/handover where configured; authorised successful closure releases applicable gate.

## AT-20 — ITP dependencies and Hold/Witness/Review (M3)

Create/revise controlled ITP with A then B dependent on A. B cannot progress until A prerequisite satisfied; invalid/cyclic dependencies rejected. Hold reached: ordinary/unauthorised user denied; authorised release with missing prerequisite denied; valid release records actor/time/comments/outcome and permits progression. Verify Witness/Review configured obligations and submission/approval/release history. UI bypass cannot progress contrary to server rules.

## AT-21 — controlled documents and calibration (M2/M7)

Create numbered project document, revise/release, retrieve current and historical revisions with private file references; previous revisions remain traceable and unchanged. Instrument register/certificate/dates/validity usable; expired/out-of-calibration instrument rejected server-side when calibration required, including offline-cached selection. Test due-date boundary/timezone and valid instrument, with audit/history and document permission denial.

## AT-22 — training and competency (M8)

Create course/session, participant attendance/signature and assessment/result. Verify signature identity/record binding, assessment authority, status/history and authorised access. Dashboard/report totals reconcile with fixtures; updates preserve historical competency/attendance evidence.

## AT-23 — reports, dashboards and exports (M10)

Fixture known QC/ITP/Hold/NCR/calibration/training statuses and reconcile project/portfolio aggregates, zero states, filters and permissions. Export QC/project report PDF and QC/NCR/project/ITP Excel registers. Inspect actual PDF pages for readable complete content, long sections/fields, signature validity labels and evidence references. Inspect Excel numbers/dates/headers/rows and malicious formula-like text handling. Exports use historical snapshots, not live template substitution, and contain no secret/private public exposure. No placeholder counts represented as facts.

## AT-24 — integrated full mobile QC demonstration (M1/M4/M5/M6)

Login → project → start/resume → project fields → dynamic responses → required photo → offline edit → close/reopen with data intact → reconnect/sync → submit → sign → return for correction → edit → resubmit → old signoff stale → fresh sign → approve → inspect audit. Verify every persisted step via API/local runtime evidence, including NCR gating for Fail. Do not mutate QCForm 8 for this flow. Passing a subset does not pass this case.

## AT-25 — security, resilience and startup (all)

Review distributed client artifacts/source/config/logs for server secrets, token leaks, unsafe SQL, public evidence, debug auth bypass and posted actor trust. Verify DI/startup, cancellation/disposal, error response safety, permission denial and race guards. Run relevant automated/integration checks; no known critical crash in primary flow. Backup/restore/migration/file cleanup recovery tested in disposable environment. Confirm locked API architecture for both MAUI and WPF without rewriting teammate-owned desktop.

## AT-26 — handover, internal audit and notifications (M9/M11/M10)

Define explicit required-record/signature/NCR readiness matrix. Missing mandatory records/signatures, stale signatures or blocking NCR yields specific incomplete reasons; final dossier release blocked or draft clearly marked incomplete. Complete fixture yields authorised traceable PDF dossier with stable included revisions/history. Internal audit programme → checklist/execution → findings → responsible follow-up/corrective actions → verified closure preserves history. Practical configured notifications reach only intended authorised users, avoid duplicates and expose read/action state; record missing channel requirements honestly.

## AT-27 — final release gate and reproducible setup (all)

From documented setup/configuration names and migrations, restore/build/test candidate revision and run acceptance/demo on available target platforms. Verify all M1–M11 requirements, encryption, histories, offline loss protection, reports/handover and security checks. No mandatory requirement silently waived; no failed/unrun critical case counted PASS. README includes actual paths/commands, platform/tooling/configuration prerequisites, safe permission bootstrap, demo and recovery instructions without credentials. Record READY only when required evidence supports it; otherwise NOT READY with specific gaps. Production deployment requires separate session authorisation.

## Evidence ledger — initial state

All AT-01–AT-27: NOT RUN FOR CURRENT REPOSITORY. No live repo tests were executed to produce this documentation package. Historical checkpoint success belongs in the implementation ledger, not as a fresh PASS here.

Append entries in this format:

### AT-xx — title / date

- Candidate revision / environment / platform:
- Preconditions / synthetic fixture IDs:
- Commands / runtime steps:
- Expected and actual:
- Result: PASS / FAIL / NOT RUN / BLOCKED
- Evidence paths (redacted) / test names / pass-fail-skip counts:
- Defect or limitation / next owner:

## Requirements coverage map

| Module | Acceptance cases |
|---|---|
| M1 Security/audit/signatures | 01, 02, 05–11, 15, 17, 24, 25, 27 |
| M2 Projects/documents | 02, 12, 21, 27 |
| M3 ITP | 20, 23, 26, 27 |
| M4 Check-sheet engine QCS-01–QCS-11 | 03–12, 16, 24, 27 |
| M5 Mobile/offline | 04–06, 09, 12–18, 24, 27 |
| M6 NCR/punch | 07, 16, 19, 23, 26, 27 |
| M7 Calibration | 21, 23, 27 |
| M8 Training/competency | 22, 23, 27 |
| M9 Handover | 26, 27 |
| M10 Reporting/exports/notifications | 23, 26, 27 |
| M11 Internal audit | 26, 27 |

QCS-01–QCS-11 must be accounted for individually in the implementation evidence: configurable definitions and required metadata/validation/signatures, or explicit missing authoritative definitions. One cable form does not prove the entire official check-sheet set. Do not invent paper form contents when not supplied; inspect repo assets or request the specific missing definitions while progressing the generic engine.

### AT-01 baseline — 2026-10-12 / Windows / SDK 10.0.401

- Existing API/test source compiled by dotnet test mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore. 19 passed, 0 failed, 0 skipped (validator/project unit tests only).
- Standalone concurrent build failed CS2012 due to competing compilation; rerun sequentially required.
- Overall AT-01 NOT RUN in full: real authentication and mobile runtime still unverified. No broader acceptance case promoted to PASS.

### AT-06–10 / AT-25 — backend unit evidence, 2026-10-12

- Expanded suite: dotnet test mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore: 44 passed, 0 failed, 0 skipped before additional signoff/identity tests.
- QCWorkflowServiceTests cover editable/locked statuses, submit prerequisites, no pre-submit signature deadlock, guard denial preventing approval, correction reason and surfaced audit failure. ContentHashTests cover determinism, workflow-metadata exclusion, primitive types and changed values/evidence.
- These cases remain NOT RUN in full: no configured PostgreSQL/Supabase identity/storage/device. Migration untested and unapplied. No claimed runtime correction loop or transactional rollback proof. Historical fixtures untouched.

### AT-13–17 — partial Windows offline-core evidence / 2026-10-12

- dotnet test mobile/MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj --no-restore: 5 passed / 0 failed / 0 skipped. Actual temporary SQLite files; OfflineStoreTests cover reopen, Processing recovery, dirty refresh protection, queue ordering/acknowledgement, durable conflicts, account isolation and encrypted file bytes.
- Whole cases NOT RUN: forced mobile process death/device restart, full create/reconcile/sync, PostgreSQL lost response, auth transport faults and platform SQLCipher verification. AT-17 encrypted database requirement remains FAIL/INCOMPLETE; metadata DB is ordinary SQLite.

## Current candidate acceptance assessment — 12 October 2026

Base ea90e96 + working tree, Windows 10 / SDK 10.0.401. See [verification evidence](evidence/VERIFICATION.md) and [setup/recovery](SETUP_AND_RECOVERY.md). Partial unit/build results are not full acceptance PASS.

| Case | Current result and exact limit |
|---|---|
| AT-01 | PARTIAL: API build, startup/DI and anonymous real HTTP verified; actual Supabase tokens/linked-user runtime and final mobile packaging unrun |
| AT-02 | PARTIAL: project service tests/anonymous denial; real CRUD, scoped data and pagination unrun |
| AT-03 | NOT RUN: actual snapshot function/immutability/duplicate/membership SQL; generator replay and section metadata remain gaps |
| AT-04 | PARTIAL: existing validator tests pass; full field/culture/tolerance/UI suite unrun |
| AT-05 | NOT RUN: real private storage/multipart/cleanup; new PNG/JPEG header/size rules implemented only |
| AT-06 | PARTIAL: editable/locked states and required submit orchestration tested; PostgreSQL transitions unrun |
| AT-07 | PARTIAL: approval/current-signature/authority guards tested in code; real SQL grants/project scope/races/NCR gates unrun |
| AT-08 | PARTIAL: canonical hash and applicability rules tested; real stored hashes/signatures and field binding SQL unrun |
| AT-09 | PARTIAL: changed content rejects old hash, fresh signature applicability and history preservation tested; full signed correction runtime unrun |
| AT-10 | NOT RUN: transactional audit failure/rollback against DB; historical public audit integration incomplete |
| AT-11 | BLOCKED verification: no disposable populated database/configuration; all three additive migrations unapplied |
| AT-12 | PARTIAL: scoped form paging/resume/signoff/audit code; real routes/data and full resource libraries unrun/incomplete |
| AT-13 | PARTIAL: actual Windows SQLite reopen/value/outbox/file retention tests; forced mobile process/device restart unrun |
| AT-14 | PARTIAL: queue dependencies/recovery/acknowledgement tested; new form mapping and full submit/signature flow incomplete |
| AT-15 | PARTIAL: mocked transport 401/403/409/500/account isolation tested; real network/auth refresh/device logout unrun |
| AT-16 | PARTIAL: fingerprint binding tested; actual concurrent/lost-response server idempotency/storage split unrun |
| AT-17 | FAIL/INCOMPLETE: synthetic DB has plaintext SQLite format 3 header; payload/file encryption is not database encryption. Windows conflict retention tested |
| AT-18 | PARTIAL: updated Android source compiles; navigation/device/camera/signature UX unrun or incomplete |
| AT-19 | PARTIAL: Fail-link/unique open NCR/correction/verification/approval code, pure independence rules tested; full DB/UI flow unrun |
| AT-20 | INCOMPLETE: dependency/Hold domain rules tested; persisted ITP revisions/obligations/server flow missing |
| AT-21 | INCOMPLETE: calibration boundary domain tests only; document control/instrument association/endpoints missing |
| AT-22 | NOT IMPLEMENTED: training/competency attendance/assessment/signatures |
| AT-23 | INCOMPLETE: scoped QC/NCR counts code; full metrics and PDF/Excel exports missing |
| AT-24 | NOT RUN: integrated mobile/offline/correction/sign/approval/audit demonstration |
| AT-25 | PARTIAL: DI, anonymous middleware, identity/hash/error/connection/source review; DB/storage/binary/recovery/races unrun |
| AT-26 | INCOMPLETE: fail-closed readiness and returned-form in-app notifications; dossier/internal audit/notification coverage missing |
| AT-27 | NOT READY: mandatory modules, encryption and runtime gates remain unmet; no deployment authorised/performed |

Preserved QA failures: evidence/backend-startup-failed.trx (61 pass/6 fail), evidence/backend-scope-disposal-failed.trx (66 pass/1 fail). Both causes repaired; latest final counts recorded below. No historical signatures or unsynchronised user data were modified.

### Final automated evidence — 12 October 2026

- Backend: 76 passed / 0 failed / 0 skipped — [TRX](evidence/backend-final.trx). Includes direct production SignatureRules stale/fresh/legacy/fresh-consent/authority/required-type/field tests and real TestServer anonymous HTTP/DI checks.
- Offline core: 11 passed / 0 failed / 0 skipped — [TRX](evidence/offline-final.trx).
- API standalone build and updated Android compile-only: 0 warnings / 0 errors. No final Android APK/iOS/device runtime claim.
- Database/Supabase end-to-end gates BLOCKED by absent test configuration; migrations unapplied. AT-17 remains incomplete/failed for plaintext SQLite metadata. Overall release NOT READY.
