# Android QC / NCR diagnosis and local verification

Date: 16 November 2026. Branch: `fix/android-qc-ncr`.
Scope: application fixes, read-only production schema/log inspection, and authorized synthetic-record tests. No production SQL writes, schema changes, deployments, pushes, record removal, queue reset, or evidence removal performed. The pre-existing change to `MzukuluQMSMobile/MzukuluQMSMobile.csproj.user` was preserved.

Latest confirmed result: read-only Supabase schema/log access is now working. The active SQLSTATE 42703 is an invalid `Revision` selection in `QCFormRepository.GetProjectFieldValidationAsync`: the field table has no such column. The parent `QCForm.Revision` exists and is still checked by `QCWorkflow`. Removed the invalid selection; the corrected SELECT succeeded against the live schema without mutation. No schema migration is needed for this defect. Latest local results: 156 API tests, 152 mobile-core tests passed. Live submission remains blocked until this API fix is deployed with approval. Earlier unavailable-access observations below are historical and superseded where noted.

## Verified causes and fixes

| Finding | Source | Repair |
|---|---|---|
| Live SQLSTATE 42703: validation selects Revision from QCFormProjectField, which has no Revision column. Supabase logs expose the exact SELECT at 12:07:53 SAST, matching the device HTTP 500. | `MzukuluQMS.Api/Data/Repositories/QCFormRepository.cs`, `GetProjectFieldValidationAsync`. | Remove that invalid projection; retain parent QCForm revision locking/checks. Corrected read-only SELECT verified on PostgreSQL. No migration. |
| Submission returned the success of running an action, even when sync returned Pending sync, Sync failed, or Conflict. | `MzukuluQMSMobile/ViewModels/FieldViewModels.cs`, `FieldFormViewModel.SubmitAsync`; `FieldViewModel.RunAsync` returns true unless an exception occurs. | Return true only when this submission operation is Completed in the durable outbox. A sync result string alone is insufficient. Retain the form and input controls on failure. |
| Successful submission reloaded the current form instead of navigating to the records list. | `MzukuluQMSMobile/Pages/FieldFormPage.cs`, `SubmitAsync`. | Navigate to an existing `FieldFormsPage`, or insert a project records list if the form was opened through the library/sync screen. Navigate only after acknowledgment. |
| A page holding a pre-sync snapshot could enqueue a subsequent write with its old revision, even after the store had acknowledged a newer revision. The preservation method only handled negative local form IDs. | `MzukuluQMS.Mobile.Core/OfflineStore.cs`, `PreserveLocalIdentityAsync`. | Preserve acknowledged revision/status for positive server form IDs as well, without overwriting entered values. Already-attempted operations retain their original fingerprint. |
| The load message equated every cached form with pending sync, including clean cached data. Field/remarks draft writes did not consistently expose their persistence state. | `FieldFormViewModel.LoadAsync`; `FieldFormPage.PersistAsync`; `InspectionWorkspace.FormsAsync`. | Derive Saved locally, Pending sync, Sync failed, Conflict, Submitted, and Saved centrally from the record/outbox. Show per-record sync state in the records list and update feedback after local writes. Authentication remains separately actionable. |
| NCR action methods returned true for pending/failed sync, and treated the global queue result as their own acknowledgment. | `MzukuluQMSMobile/ViewModels/NcrViewModel.cs`, `CorrectAsync` / `VerifyAsync`. | Confirm the specific correction/verification intent. Retain the correction draft until acknowledged. Unrelated queue failures cannot hide an acknowledged correction. |
| Correction/verification reloaded evidence even though those actions do not change it. | `NcrViewModel.ReloadAsync`. | Skip this evidence-list GET after correction/verification; retain refreshes for initial load, explicit sync, and uploads. |
| The Docker runtime had no explicit installation of the reported missing GSSAPI library. | `MzukuluQMS.Api/Dockerfile`. | Install `libgssapi-krb5-2` in the runtime layer, retaining the unprivileged runtime user. Container runtime verification remains pending; the exact hosted failure was not reproduced. |

## Lifecycle and integrity trace

`FieldFormPage` serializes input persistence through `LocalEditQueue`. Incomplete inputs are separate encrypted input drafts; valid changed values use `SaveValueAsync`, which already avoids equal-value writes. Remarks now also guard unchanged values at the workspace boundary.

`OfflineStore.SaveAndEnqueueAsync` atomically saves encrypted local JSON and a durable intent. `ClaimAsync` orders operations per entity and respects dependencies and retry backoff. Failed/Conflict operations block later work on that entity. Restart restores interrupted Processing operations to Pending. Dirty entities cannot be replaced by a remote cache refresh.

`HttpSyncTransport` sends same-origin API writes with stable operation IDs, If-Match revisions, and stable multipart boundaries. `SyncEngine` marks success only on HTTP 2xx; it preserves pending, failed, and conflict work otherwise. `CompleteAsync` updates the local workflow status only after acknowledgment. Tests demonstrate that an unsuccessful submission keeps InProgress. An offline save alone does not submit a form.

On the API, `QCWorkflow.BeginAsync` checks identity, active user, project permission, locks the form, checks idempotent replay, and checks revision before mutation. `QCFormService` permits Draft / InProgress / ReturnedForCorrection edits and submissions; submission validates required answers, project fields, and photo evidence. Approval requires Submitted, current authorized signatures and resolved blocking NCRs. Return-for-correction preserves history and requires a reason. `CompleteAsync` records audit/revision/idempotency result in the transaction before committing.

Failed inspection outcomes call `NcrService.RecordFailureAsync` inside that response transaction. Migration `002_ncr.sql` defines a partial unique index allowing one open NCR per response, and the insert uses the corresponding ON CONFLICT DO NOTHING. Migration `005_existing_audit_ncr_bridge.sql` mirrors NCRs via AFTER INSERT, so a deduplicated insert should not create an orphan legacy NCR. Corrections validate evidence ownership and both form/NCR revision; verification requires an independent actor and preserves failed verification as open. These SQL properties were inspected in source, not exercised against PostgreSQL in this session.

## Database and Render diagnosis still pending

The direct read-only SchemaProbe returned `BLOCKED: QmsDatabase not configured in User Secrets/environment.` Supabase MCP was added for project `kfrknawmyewdambgaekl` with `read_only=true`; OAuth completed successfully. Its tools were not exposed in this already-running chat. A refreshed chat/tool session is needed to use that connection. No credentials were printed or added to repository files.

No actual QMS schema dump was found locally. `desktop/Data/schema.sql` defines desktop authentication, not the QMS execution schema. Existing migrations explicitly describe themselves as candidates requiring schema reconciliation. Therefore SQLSTATE 42703's exact missing identifier is NOT diagnosed, and no speculative ALTER TABLE repair was authored or applied.

`MzukuluQMS.Api/Diagnostics/qc_ncr_read_only.sql` supplies metadata-only column, enum, index, trigger, and function inspection in a read-only transaction. Compare its results with the failing SQL and deployed commit. Inspect NCR bridge functions, snapshot-generation triggers, and all migrations 001–006; do not blindly reapply them. Inspect the full 42703 log context and any query position without exposing bound values or connection strings. Confirm deployed Dockerfile/build context/branch and native-library error timing in Render.

No Render dashboard or hosted API access was available. A configured GitHub connection does not authorize Render access. Linux missing-library errors and slow NCR processing remain reported symptoms, not reproduced hosted failures.

## Verification

| Check | Result |
|---|---|
| Baseline mobile-core tests | 126 passed |
| Baseline API tests | 147 passed |
| Updated mobile-core tests | 152 passed, 0 failed |
| Updated API tests | 156 passed, 0 failed |
| Full Android Debug build | Passed with 0 warnings / 0 errors after disk space was freed |
| ADB device discovery | Empty device list; no runtime test or logcat reproduction possible |
| PostgreSQL migrations / actual triggers | Not tested: schema access unavailable in running chat; no configured direct connection |
| Container build / Linux GSSAPI load | Not tested: Docker unavailable |

Commands run from `mobile`:

```powershell
dotnet test MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj --no-restore
dotnet test MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore
dotnet build MzukuluQMSMobile/MzukuluQMSMobile.csproj -f net10.0-android --no-restore
```

New regression tests cover unsuccessful HTTP submissions without status mutation, specific acknowledgment before navigation eligibility, blocked ordering and recovery after reopening, revision preservation after synchronization, NCR action acknowledgment, automatic failure-outcome NCR invocation, transaction completion prevention on NCR failure, and timing-log redaction. Existing NCR rule tests exercise protected forms, stale/closed cases, and independent verification. Real concurrent NCR deduplication, PostgreSQL rollback, bridge behavior, and evidence-storage integrity still need staging integration tests.

The initial Android packaging attempt failed for insufficient disk space, and one intervening test build failed on a disk write. After the user freed approximately 33 GB, tests and APK packaging succeeded. No cleanup or local data deletion was used to address this.

## Performance evidence and remaining measurements

No valid hosted before/after latency sample is available. No end-to-end speedup is claimed. Source-level request counts: NCR correction/verification post-action reload falls from three GETs (form, NCR list, evidence) to two; successful submission no longer performs the old form/history reload before navigation. The records list still refreshes on arrival. Equal field values were already filtered; unchanged remarks now also stop at the workspace boundary. No attempted intent is coalesced or rewritten.

`Diagnostics/WorkflowTimingMiddleware.cs` logs method, route template, final status, elapsed milliseconds, and trace ID for QC writes, including evidence, NCR correction and verification. It runs outside the exception handler so handled error status is measured. It excludes request bodies, query strings, authorization, and raw resource IDs. API problem responses now include traceId.

For staging, capture at least 20 comparable synthetic runs per operation before and after the candidate, with the same data volumes, device, image bytes, region, and cold/warm condition. Compare median/p95 for response writes, submission, upload, correction, and verification. Use database EXPLAIN (ANALYZE, BUFFERS) only on staging for slow reads and inspect lock waits around storage upload/form locks. Preserve atomicity and idempotency rather than parallelizing writes to the same form. Do not add speculative production indexes without plans and measurements.

## Staging deployment and rollback

1. Retain the current API image/deployed SHA and mobile package. Back up staging DB/storage and validate restore. Refresh the tool session for read-only schema/log inspection first.
2. Resolve the actual 42703 identifier against staging metadata. Only then author a versioned additive migration with explicit preconditions and test it on a populated staging copy. **No new database migration is required by this code-only patch.** The diagnostic SQL is not a migration.
3. Build the API container from the repository root using `docker build -f mobile/MzukuluQMS.Api/Dockerfile -t mzukulu-qms-api:qc-candidate .`. Verify `libgssapi_krb5.so.2` resolves in the built runtime and verify TLS/authentication without weakening configuration.
4. Deploy the candidate to staging using existing managed secrets. Check anonymous health and authenticated DB operations; health alone does not prove DB connectivity. Retain the existing authorization, private bucket and revision/idempotency contracts.
5. Install the Android build as an update on a test device. Do not uninstall, clear app data, or reset secure-storage keys. Use synthetic test records. Exercise offline answers/photos, forced restart, reconnect, auth expiry, 403/409/500, successful submit navigation from list/library/sync, correction/resubmission, automatic duplicate failure retries, corrective evidence, independent failed/passed verification, and approval guards. Confirm queue/files/history survive every failure.
6. Collect timings and trace-correlated Render/database logs. Require explicit approval before any production migration or deployment; no production deployment is authorized by this report.
7. Roll back application code by restoring the previous API image/mobile package through the established release process. This patch does not change local DB version or API wire contracts. Preserve existing DBs, keys, evidence and outbox. Previously acknowledged server actions remain historical facts; rollback must not reverse/delete them. Any future schema migration needs its own retention-safe rollback plan; do not use destructive down scripts.

Remaining blockers: live-schema / Render log access in the active tool session, connected running Android device/emulator, Docker runtime, staging PostgreSQL/storage, and comparable timing samples. These local fixes do not establish production readiness.

## Emulator follow-up

The user started Pixel 7 / Android 16 (API 36), detected as `emulator-5554`, x86_64. ADB update installation of the Debug APK succeeded (3,659 ms), preserving app data. Launch subsequently aborted with the native message `No assemblies found ... Assuming this is part of Fast Deployment`. Thus successful APK packaging did not establish standalone launchability: this Debug artifact expected Visual Studio-deployed assemblies.

The current remote `main` was verified at `66e9b8af4090a4486471b874ab85c2beefca5256`. Its `ApiConstants.cs` uses `https://xadad-project-2026.onrender.com/` for all configurations. This repair branch uses that same origin for Release, but localhost/emulator-host routing for Debug. No local server was listening on port 7044. A read-only request to Render `/health` returned HTTP 200; this does not establish authenticated database health. No live inspection records have been modified.

Standalone Release verification succeeded with `dotnet build MzukuluQMSMobile/MzukuluQMSMobile.csproj -c Release -f net10.0-android -p:AndroidPackageFormats=apk -p:RuntimeIdentifier=android-x64` (0 warnings/errors, 1m51s). ADB installed the signed APK as an update in 11,264 ms. Cold launch returned Status ok and TotalTime 2,807 ms; the app process remained running and the sign-in screen was visually verified. No fatal AndroidRuntime entry was found for that process. User sign-in/MFA is required before authenticated QC workflow testing. This closes the device-availability/startup gap, but does not yet verify submission, sync, or NCR behavior on device.

After user sign-in, authenticated Dashboard, Projects, QC record list and `QC-0013` details loaded successfully from Render. The user explicitly authorized necessary synthetic testing in response to a question naming live Demo Project, test answers/evidence, and retained audit history. Existing `QC-0013` was only read.

Creating one synthetic record using MZT-QC-317 reproduced a local creation acknowledgment Conflict, while retaining the draft. Investigation found that `develop` lacks the metadata repair already on `main` (commit `806be98`): `TemplateRevisionDraft` read repository aliases `optionsJson` / `validationRulesJson` instead of the API DTO's parsed `options` / `validationRules`. This drops dropdown choices/rules and makes strict `OfflineFormIdentity.Acknowledge` reject the actual server snapshot. Targeted repair and retention/recovery tests were brought over from `main`; the recovery reconciles the already-created server record without creating another copy, and still rejects genuine metadata/revision mismatches. Answers, operation IDs/payloads, conflict history and evidence remain intact. Initial metadata regression run: 146 mobile-core tests passed.

The related non-throwing `FormInput.ValidationError` path from `main` is used for ordinary missing-field feedback; exception-based server-write validation remains through `Validate`. This avoids raising/catching an exception for every unset required field during form rendering/validation.

On-device recovery then succeeded: the retained creation intent became Completed, sync showed Synced, and the same server record appeared as `QC-0014` (Draft / Saved centrally). The project count stayed at two (existing `QC-0013` plus synthetic `QC-0014`), establishing that recovery did not create a third record. The synthetic record is labelled through its project fields/remarks with `SYNTHETICQA14` / `SYNTHETIC_QA_20261116_TEST_ONLY_NO_REAL_INSPECTION`.

A controlled R outcome on item 1 was acknowledged and created one linked open NCR (`QMS-1ff56561-5929-4d45-9852-007e557c6b1a`). No database error was reproduced by that creation path. Local submission validation correctly highlighted the missing Panel / Cabinet field, retained entered answers and kept the form open. Remaining answers are synthetic N/A selections, not real inspection findings.

Offline runtime test: disabled emulator Wi-Fi/mobile data, saved another answer, attempted sync, and observed `Pending sync` with workflow status still Draft. Force-stopped/restarted the app offline; its secure session/cached dashboard restored, and the outbox showed all 24 pending operations retained. Network was re-enabled. A subsequent 42,094 ms polling observation ended without queue progress (2 Complete, 24 Pending); this is an observation window, NOT a completed-request latency. Repeated online attempts still showed the first project-field operation blocking later writes. Original pending UI hid the actual retry reason. Added safe pending-error presentation and `QmsSync` operation-kind/HTTP-status/attempt/elapsed diagnostics, excluding bodies, URLs, tokens, and exception payloads. Logger failures cannot change acknowledgment; latest mobile-core run: 148 passed.

The diagnostic Release rebuild passed (0 warnings/errors, 1m00s), installed successfully as an update, and restored the signed-in session. It captured `UpdateQCFormProjectField: HTTP 500; attempt 6; elapsed 505 ms`. This verifies an active hosted server failure on the first queued project-information update; subsequent answer/remarks writes and submission remain blocked by per-record ordering. Network access is restored. All 24 pending operations are retained, and the test record remains Draft. Repeated retries were stopped rather than exhausting the queue's retry limit.

The source update query at `MzukuluQMS.Api/Data/Repositories/QCFormRepository.cs:656` writes `QCFormProjectField.Value` and `UpdatedAt`, scoped by both form/field IDs. The exact failing identifier is not established without matching server logs/schema. Do not infer a missing column from HTTP 500 alone or remove audit/concurrency/transaction safeguards to get around it.

The user supplied Render's service log URL for `srv-db886d9srm7s73dok7v0`. Opening that dashboard was blocked by the browser permission gate, explicitly reporting a declined permission. No alternate browser/network route was attempted. Server-log inspection awaits enabled browser access or user-supplied sanitized error lines. Supabase MCP remains authenticated/configured but absent from this running tool inventory. No production schema changes or deployment were performed. A complete end-to-end submission/NCR correction/verification pass is NOT claimed.

## Live database diagnosis confirmed

After the tool connections refreshed, read-only Supabase access succeeded for project `kfrknawmyewdambgaekl`; health advisors returned no notices. Queried relevant column metadata, triggers, and NCR indexes. `public.QCForm.Revision` exists; `public.QCFormProjectField.Revision` does not. `QCFormProjectField.UpdatedAt` exists, so removing it or adding speculative columns would have been incorrect.

PostgreSQL logs at `2026-11-16T10:07:53.191Z` (12:07:53 SAST) show SQLSTATE `42703`, message `column "Revision" does not exist`, query position 12, and the failing SELECT from `public.QCFormProjectField` beginning with `"Revision"`. This matches the emulator's 12:07:53 project-field HTTP 500. The exact query is defined in `QCFormRepository.GetProjectFieldValidationAsync` (formerly line 1185). Its DTO does not even have a field Revision property.

Repair: remove the field Revision selection. Use equivalent explicit text casts, retaining the same ID predicates and validation metadata. The parent workflow lock, If-Match revision check, authorization, audit, idempotency, and transaction completion are unchanged. Executed the repaired SELECT read-only against synthetic form 14 / field 42 successfully. The live NCR partial unique index and AFTER INSERT bridge were also confirmed; no bridge/index mutation was needed.

`ProjectFieldColumnContractTests` executes the production SELECT against an in-memory minimal field-column contract with no Revision column, checks metadata mapping and wrong-form/wrong-field exclusion. This test uses SQLite for column resolution and does not claim PostgreSQL transaction coverage; the actual PostgreSQL read check is separate. API tests: 156 passed; Release API build: 0 warnings/errors.

Added explicit `Retry saved change` for pending attempted or failed intents. It only changes scheduling/status, preserving operation ID, bytes, expected revision, attempt history, dependencies and stored error/history. Completed/conflicting work cannot be replayed by this path. Tests verify retained fingerprint and dependency ordering. This allows pending/terminal transient failures to recover after the server repair without deleting local work or creating replacements. Mobile-core tests: 152 passed.

Final standalone Android Release build with retry support passed (0 warnings/errors, 1m33s), installed as an update in 1,311 ms and cold-started in 2,604 ms. The hosted API remains unchanged; retained operations were not retried against the known failing deployment again.

Performance advisors reported informational unindexed foreign-key and unused-index findings. These are review inputs, not evidence that they caused the observed 500 or NCR delay. No indexes were added/dropped. See [foreign-key index guidance](https://supabase.com/docs/guides/database/database-linter?lint=0001_unindexed_foreign_keys) and [unused-index guidance](https://supabase.com/docs/guides/database/database-linter?lint=0005_unused_index); use staging plans/workload measurements before changing them.

Render plugin installation/enabled state was verified. Its service tools were still absent from this tool inventory, and `codex mcp list` contained no Render server. Following [Render's current OAuth setup](https://render.com/docs/mcp-server#codex-desktop-and-cli-setup), registered the hosted Render MCP endpoint using OAuth client ID `codex`. Authorization is pending; no token/key was written into repository files or printed. The earlier declined dashboard request was not retried or bypassed.
