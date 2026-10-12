# Current candidate setup and recovery

Candidate base: ea90e96, branch back-end-first-revamp, with uncommitted implementation changes on 12 October 2026. This is NOT a release-ready system. See IMPLEMENTATION_STATUS.md and ACCEPTANCE_TESTS.md for measured results and missing modules.

## Build and test

Use .NET SDK 10.0.401 or a compatible .NET 10 SDK. API uses Dapper/Npgsql, not EF. Android/iOS MAUI workloads are required for those targets; iOS packaging/runtime needs Mac/Xcode and appropriate signing. Windows success does not verify mobile targets.

Run builds/restores sequentially when they share a project. Do not pass a global TargetFrameworks override to the portable core or run competing builds against the same obj directory.

    dotnet restore mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj
    dotnet build mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj --no-restore
    dotnet test mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore
    dotnet restore mobile/MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj
    dotnet test mobile/MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj --no-restore
    dotnet restore mobile/MzukuluQMSMobile/MzukuluQMSMobile.csproj
    dotnet build mobile/MzukuluQMSMobile/MzukuluQMSMobile.csproj -f net10.0-android --no-restore

Compile-only (-t:Compile) checks do not prove APK packaging or device runtime. There is no pinned global.json. WPF remains teammate-owned and was not rewritten.

## Server configuration

Configure server values through the API project's User Secrets for local development; use deployment secret management outside source control. Required names:

- ConnectionStrings:QmsDatabase
- SupabaseAuth:MetadataAddress (HTTPS OIDC discovery endpoint)
- SupabaseAuth:Audience
- SupabaseStorage:BaseUrl, SupabaseStorage:SecretKey, SupabaseStorage:Bucket

Never paste real tokens/passwords/service keys into docs, tests, appsettings or chat. The API project UserSecretsId remains unchanged. tools/SchemaProbe is read-only and emits metadata/fixture status without the connection string. No connection was configured when this candidate was developed.

Supabase qms-evidence bucket must be private. Confirm the service role has only required storage/database rights. Real upload/cleanup/private-access behavior remains unverified. App constants contain the public Supabase URL/anon key; only public client configuration belongs there. Configure API HTTPS URLs/certificates per device; do not bypass certificate validation to hide emulator problems. Current loopback/emulator URLs are development configuration.

## Additive migrations and policy bootstrap

Migrations are NOT APPLIED and NOT PostgreSQL-tested. First inspect the real schema/enums/generate_qcform_structure/AuditEvent and existing NCR/reporting tables against these assumptions. Use a disposable populated copy, including historical signoffs/audit/dirty migration fixtures. Apply in order only after reconciliation:

1. mobile/MzukuluQMS.Api/Migrations/001_qc_security.sql
2. mobile/MzukuluQMS.Api/Migrations/002_ncr.sql
3. mobile/MzukuluQMS.Api/Migrations/003_reporting.sql

These create explicit new qms_security/qms_core schemas. They do not replace public.AuditEvent or unknown legacy module tables. Historical audit reconciliation is unfinished. They are single-use versioned scripts, not replay-safe CREATE IF NOT EXISTS upgrades. Never run the historical clean-schema script or automatically reapply these migrations.

All grants are denied initially. Deployment owner must explicitly populate RolePermission for actual observed roles, ProjectAccess for active QMS users, TemplateSignatureRequirement before creating new forms, NcrPolicy and HandoverRequirement. No guessed seven-role matrix or implicit Admin bypass is supplied. Grant the API database role only required privileges on these schemas/tables/sequences. Browser/Supabase client roles must not have direct write access. Do not grant UPDATE/DELETE on audit/signoff history. The migration adds append-only triggers.

Business permissions: ReadProjects, ReadClients, ReadTemplates, ManageProjects, ManageTemplates, ReadInspection, PerformInspection, SubmitInspection, ReviewInspection, ApproveInspection, SignInspection, SignInspection:Inspector/Technician/Supervisor/Client, ReadAudit, ReadNcr, CorrectNcr, VerifyNcr, ReadReports, ReadHandover, DatabaseHealth. A generic SignInspection grant does not grant any signing type. Each signoff type requires its own explicit grant plus project access.

Signature requirements are snapshotted at form creation. Existing legacy forms are not auto-backfilled from today's template policy; a controlled business migration is needed for any eligible existing Submitted/Draft forms. Preserve approved QCForm 8, legacy signoff rows and original hashes. HashVersion 1 remains historical. New signoffs use v2. Approval requires current authorised required types, a fresh submission/signature evidence and current hash. Even a no-content-change correction requires renewed submission/signing consent.

Before production migration: verify backup and restoration in a disposable environment; compare row counts and hashes; test audit-failure rollback and competing edit/sign/approve; validate storage cleanup. Recovery must restore matched database/storage backups under operational authority. No destructive down migration is provided because dropping audit/permissions/signature columns can lose history. Do not deploy this candidate before those gates pass.

## API compatibility and demo

Existing QC route prefix remains /api/qc-forms. Form GET/list returns Revision; GET sets ETag. All existing-form HTTP writes require If-Match: quoted numeric current revision. A successful write returns updated ETag; 409 retains the client's local work for review. Optional UUID Idempotency-Key binds user/key to method/path/If-Match/body bytes. Retry identical bytes/key; a changed payload requires a new operation. Multipart boundary/order must remain stable for a retry. Keys/results are persisted in the same database transaction as state/audit. Storage upload may still leave an orphan on process death or an indeterminate commit; reconciliation is required.

Correction request is now JSON {reason:string}, max 2000 chars. Coordinate these intentional changes with WPF. Create accepts optional cached ChecklistTemplateVersionID and conflicts if it no longer matches the active version. Value writes retain {value: JSON}. Photo/Signature multipart uses EvidenceType, optional QCFormFieldID and File; PNG/JPEG header checks and 20 MiB maximum apply. Document upload is not enabled pending real enum/validation support.

Use a fresh synthetic form with returned IDs: fill project fields/responses/photos, submit, upload fresh signature, create signoff with SignOffType/SignatureEvidenceID, return with reason, edit/resubmit, verify old history remains and cannot approve, upload fresh evidence/sign, approve only after current required signatures and no blocking/unclassified NCR, inspect audit. Never assume identity sequences or mutate forms 8/9 for this demo.

Fail Dropdown outcome creates/reuses an open source NCR atomically. Correct/verify endpoints require form If-Match and NCR ExpectedRevision. Failed verification stays open; creator/corrector cannot independently verify. An unclassified or explicit blocking open NCR prevents approval. A later Pass never erases it.

## Local recovery and retention

Mobile stores owner-specific qms-{owner}.db and encrypted evidence files. AES-GCM payload/file keys are kept in SecureStorage; keys are never removed on logout. Logout removes only session material and disposes the active store. Another account uses a different database/queue. A missing key for an existing DB raises a recovery error and preserves files. Platform SecureStorage backup/restore/key-recovery needs device verification; no key rotation or cross-device recovery is claimed.

The SQLite metadata file is NOT encrypted. Payload/file encryption is not SQLCipher and does not satisfy the encrypted-database gate. Do not label the app compliant or release ready on that basis. The schema rejects newer versions without reset/deletion. Interrupted Processing operations return to Pending on reopen; failed/conflicting intents and local/server versions are retained. Cache refresh cannot replace dirty local JSON. Transient retries back off, permanent/auth/permission errors remain actionable. Evidence capture helper can leave retained orphan files when persistence fails; automatic cleanup/upload-intent atomicity is not complete. Never delete evidence referenced by pending/failed/conflicting work.

Returning offline access currently requires a previously linked, unexpired securely stored session. Expired sessions require online login; unsynchronised data remains locked/retained. New offline form creation/remapping, signature consent/signoff orchestration, camera UI and conflict resolution UI are unfinished. Current field UI supports cached known-server forms and local value/submit queueing.

## Remaining release gates

Actual PostgreSQL schema/migration/transaction/storage/identity verification; full mobile forced-restart and signed correction flow; encrypted SQLite on Android/iOS; snapshot-generation replay/section metadata constraints; complete ITP/Hold/Witness/Review, document control, calibration integration, training, exports/dossier/internal audit; notification coverage; full platform accessibility, signing, backup/restore and operational cleanup.

Handover readiness currently always reports not ready with specific missing gates. No complete dossier/PDF/Excel export is claimed. Notifications are authorised in-app returned-form alerts only. No deployment, merge, push, historical-data mutation or external notifications were performed.
