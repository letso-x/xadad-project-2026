# Mzukulu QMS — read this first

## Purpose and installation

This folder is a complete in-repository handoff for `letso-x/xadad-project-2026`. Extract the ZIP at the repository root so these files are in `docs/codex/`. Merge carefully if that folder already exists: preserve newer status evidence and local changes. The ZIP does not contain application code, credentials, database migrations, or a claim that the application is complete.

Start Codex in the repository and give it this instruction:

> Read docs/codex/00_READ_ME_FIRST.md, 01_PROJECT_MASTER_SPEC.md, IMPLEMENTATION_STATUS.md, ACCEPTANCE_TESTS.md, and 02_BACKEND_API_AGENT.md. Inspect the actual repo, establish the API build/test baseline, then complete the backend agent scope and update the status/evidence files. Preserve existing working architecture and historical integration records. Continue through ordinary reversible implementation and verification without asking me to approve every step. Report genuine blockers precisely.

All necessary project guidance is inside this folder. Do not depend on messages outside it. Read the master spec in full; it includes the complete user-supplied checkpoint. Agent files supplement it rather than replacing its requirements.

## Source authority and uncertainty

Primary source: user-supplied “MZUKULU QUALITY MANAGEMENT SYSTEM FULL REQUIREMENTS, CURRENT IMPLEMENTATION STATE, AND REMAINING WORK”, checkpoint 12 October 2026, reproduced in `01_PROJECT_MASTER_SPEC.md`.

Secondary source: [Build Postgres Schema shared conversation](https://chatgpt.com/share/6acd375e-c0f4-83ea-b418-07a370dac4df), read during preparation. It supplies the earlier snapshot schema and two-field regression. The share shows a file and image without exposing their contents; the later pasted checkpoint is the available complete textual project context. Do not claim inspection of those attachments. No repository or live database was inspected while this package was prepared.

Current explicit user instructions govern the work. Follow applicable repository/AGENTS instructions. For project intent use the newer pasted checkpoint over older conversation examples. For what is actually implemented use inspected source/schema and test/runtime evidence. If actual behavior conflicts with requirements, record the discrepancy and implement a compatible repair. Do not silently substitute old schema examples for current migrations. Label additional engineering defaults as decisions, with rationale and tests.

## Locked architecture and ownership

MAUI Android/iOS → local SQLite/filesystem + durable sync queue → HTTPS ASP.NET Core API → PostgreSQL and Supabase Storage. WPF → same API. Supabase Auth issues sessions; API validates JWTs and maps the authenticated subject to an active QMS user. Central PostgreSQL owns server truth; local persistence owns unsynchronised mobile work.

Known API project: `mobile/MzukuluQMS.Api`; .NET 10, Dapper, Npgsql, xUnit, Moq. Discover the MAUI/test/solution paths rather than guessing. The user owns API/mobile; a teammate owns WPF. Coordinate shared DTO/API changes with documented compatibility notes; do not rewrite WPF. Preserve Dapper instead of migrating to EF solely because the original proposal mentioned EF.

## Authentication, permissions, and security

Preserve ES256 JWT signature/issuer/audience/lifetime validation, OIDC metadata, MapInboundClaims=false, NameClaimType=sub. Map sub → User.ExternalAuthID → active QMS user, including established qms_user_id and qms_role claims. QmsUser proves linkage/activity; it does not confer review, approval, Hold release, or unrestricted project access. Resolve actor identities server-side; never trust a posted user ID or role as authority.

Observed roles are Admin, Manager, Inspector, Technician, Viewer. Seven official role names and their final matrix are unconfirmed. Introduce configurable permissions/policies; do not fabricate two additional roles or imply all observed roles are final. SignOffType (Inspector, Technician, Supervisor, Client) is a separate concept. Default new protected operations to denied until explicit permissions are configured. Document development fixtures distinctly from official policy.

Use server User Secrets for local server credentials; discover deployment secret management. MAUI stores session material in SecureStorage or equivalent, never plaintext passwords. Storage is private and evidence access is authorised. Ordinary SQLite is not encrypted; encrypted mobile storage is required and remains incomplete until verified on target platforms. No secrets in documentation or test output. Fixture IDs in the master are identifiers, not authentication credentials.

## Non-negotiable business rules

- Templates/version definitions and QC execution snapshots are separate. Existing forms never render from a newer live template. Include project-information fields, sections, fields, validation/options and requiredness in snapshots.
- Field outcomes Pass/Fail/N/A are distinct from record review. Fail creates/links NCR; whole-record correction uses ReturnedForCorrection, not legacy Rejected. UnderReview persistence is not required.
- Draft/InProgress/ReturnedForCorrection allow normal edits. Submitted/Approved/Cancelled block normal edits. Submitted allows the controlled Signature evidence/signoff path. Signoff is created only after server submission; mobile can capture an image earlier and queue the ordered submission/signature/signoff steps.
- Preserve every historical signoff/hash. Current approval requires authorised required signatures matching current signable content; edited corrected records need a fresh signature. An image alone is not an electronic signoff.
- Audit significant actions with actor/action/entity/time. Enforce Hold prerequisites/authority, blocking NCR rules, independent NCR verification, and instrument calibration validity at the server.
- Preserve local files, values, pending operations, and conflict versions across restarts, retries, logout/session expiry and upgrades. Never fix local migration failures by deleting a user's working database.

## Execution order and ownership of shared work

Run specialised scopes sequentially: 02 Backend → 03 Offline Sync → 04 MAUI UI → 05 QMS Core → 06 Reporting/Handover → 07 QA → 08 Hardening/Release. These are assignment documents, not proof that separate agents have run. The backend agent owns shared API contracts, identity, lifecycle, audit foundation and permission abstraction. Sync agent coordinates required API idempotency/concurrency extensions. UI consumes those contracts. Core modules add shared guards and reporting consumes their data. QA and release verify the integrated result. Do not run overlapping agents against the same files without an explicit ownership arrangement.

## Repository and Git workflow

1. Read repository instructions; inspect Git status, branch, remotes, solution/project files, configuration examples, migrations, test projects and CI. Preserve user changes. Identify the actual target/test database without printing secrets.
2. Restore/build/test with the pinned .NET SDK and existing tools. Record baseline failures before attributing them to changes. Discover supported MAUI workloads/platform requirements; unavailable iOS tooling is a specific verification limitation, not a passing build.
3. Inspect actual SQL identifiers, enum values, connection factory and current DTOs. Use parameterised Dapper SQL and existing CreateOpenConnectionAsync(CancellationToken) unless inspected interface differs. Apply versioned migrations on a disposable environment first. Never run the old fresh-schema script against the existing database.
4. Implement small vertical slices, including validation, authorisation, persistence, audit, relevant tests and documented contracts. Keep shared API changes compatible or explicitly version/document them.
5. Run relevant acceptance paths. Use synthetic test records and captured returned IDs; do not assume sequential IDs. Preserve historical QCForm 8; inspect QCForm 9 before using it and prefer a new test record for destructive state progression.
6. Review diff for unrelated changes/secrets; update status, acceptance evidence and setup docs. Commit only intended files if authorised by repo/session instructions. Do not force-push, merge, deploy, delete branches, reset working changes, or mutate production under this handoff alone.

## Agent completion contract

Report scope, changed paths, actual schema/migrations, API/DTO changes, build/test commands and results, runtime evidence, unresolved failures, and next steps in IMPLEMENTATION_STATUS.md. Maintain acceptance case evidence in ACCEPTANCE_TESTS.md or a linked repository evidence file. Do not include secrets in evidence. A blocker must name the missing external dependency/access/decision and work that can still proceed.

An agent may finish its assigned phase with explicitly incomplete remaining phases. The project is complete only after all mandatory modules and acceptance gates pass. Notifications are an official requirement; the checkpoint permits a practical implementation if feasible, but omission must be recorded as incomplete rather than silently dropped. Never claim ISO certification or compliance solely from the application feature list.
