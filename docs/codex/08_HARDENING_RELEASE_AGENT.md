# Hardening / release agent

## Outcome

Prepare a reproducible, reviewable release candidate and honest readiness decision. This file authorises engineering preparation, not an automatic production deployment, public publication, merge or irreversible database change.

## Final review

- Verify API/MAUI builds, complete automated tests and acceptance evidence against the exact candidate revision. Check Android/iOS platform limitations explicitly. Repair startup/DI errors and critical primary-flow crashes.
- Review async/cancellation/disposal, UI threading, timeout handling, duplicate taps, concurrent SQL/lifecycle guards, transaction boundaries, parameterised SQL and connection factory use. Inspect aggregate joins, constraints, indices, migrations and rollback/recovery plans using actual schema.
- Review JWT validation, QMS identity/activity, permission/resource checks, signing authority/hash freshness, audit completeness, private storage and uploaded-file validation. Remove debug bypasses, secrets and sensitive logs. Verify server service-role/DB credentials cannot enter MAUI/WPF distributions. Keep unexpected errors generic with correlation data for diagnosis.
- Verify durable outbox/files, retry/idempotency, conflicts, account isolation, restart/upgrade preservation and encrypted local storage. Missing verified encryption is a requirement gap, not solved by saying SQLite is secure. Review logout/re-authentication behavior without discarding unsynced records.
- Validate migration ordering/backward compatibility, existing data preservation and backup/restore testing in a disposable environment. Never run the historical clean-schema script as an upgrade. Preserve signatures/hashes, audit, revisions and known historical records.
- Remove temporary/debug/dead code only when verified unused and within scope. Avoid style-only rewrites that expand release risk. Review dependencies and actual PDF/Excel runtime prerequisites.

## Repository documentation to finish

Update existing README/setup docs with supported SDK/workloads/platforms, restore/build/test steps, safe configuration names (no secret values), server User Secrets/deployment configuration, migrations, storage bucket/access policy setup, permission/bootstrap procedure, API/mobile run instructions and test environment setup. Use discovered exact project paths/commands. Provide a demo script: login → project/form → offline capture/restart → sync → submit/sign → correction/edit/resubmit/fresh sign → approve → audit; also Fail/NCR close-out and Hold release.

Document evidence retention, operational logging, retry/conflict recovery, backup/restore, storage orphan cleanup, encrypted-key/local-data recovery limitations and platform-specific packaging/signing requirements. Do not put release-signing secrets or certificates in the repo. Keep README instructions and this handoff mutually consistent.

## Release readiness and completion

Run AT-01 through AT-27, recording verified, failed, unrun and externally blocked results. Check every M1–M11 obligation in the master, including document control, calibration, training, reporting/exports, handover, internal audit and notifications. A successful demo alone does not complete all required modules.

Update IMPLEMENTATION_STATUS.md with candidate revision, migrations, checks, platforms, security/operational findings and unresolved defects. State READY only when mandatory requirements and gates are satisfied with evidence; otherwise state NOT READY with concrete remaining work. Do not claim ISO certification. Produce reviewable changes and setup/demo docs; request any separately required deployment/production authorisation only after the candidate is concrete.
## Mandatory operating contract

Read `00_READ_ME_FIRST.md`, `01_PROJECT_MASTER_SPEC.md`, `IMPLEMENTATION_STATUS.md`, and `ACCEPTANCE_TESTS.md` before editing. All paths here are relative to the repository root unless stated otherwise. Inspect repository instructions, source, migrations, and actual database metadata; the checkpoint is evidence reported on 12 October 2026, not a fresh repository inspection. Preserve existing working behavior and the Dapper/Npgsql architecture. Never invent existing tables, methods, routes, enum values, or package capabilities.

Use Supabase Auth through the established API identity mapping. Both MAUI and teammate-owned WPF use the API; neither connects to PostgreSQL. Never place tokens, passwords, connection strings, or service-role keys in client code, logs, commits, screenshots, or handoff notes. Keep historical signatures, preserve unsynchronised work, and never silently overwrite conflicts. Business permissions and resource access must be enforced by the API.

Run the relevant build/tests before and after changes. Compilation is not runtime verification. Update `IMPLEMENTATION_STATUS.md` with exact commands, outcomes, environment, affected paths, migrations, remaining work, and acceptance evidence. Use VERIFIED WORKING, IMPLEMENTED BUT NOT VERIFIED, NOT IMPLEMENTED / INCOMPLETE, or BLOCKED. Preserve unverified and failed results honestly. Make small reviewable changes; do not reset unrelated work, deploy, change production data, or replace architecture merely to simplify the task. End with a concrete handoff to the next agent. Perform your assigned scope without waiting for approval of each routine reversible step.
