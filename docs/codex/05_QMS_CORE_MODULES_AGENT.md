# QMS core modules agent

## Outcome

Implement missing M2/M3/M6/M7/M8 vertical slices: NCR/punch, ITP, controlled documents, calibration and training/competency. Inspect actual schema/source first and use versioned additive migrations where practical. Table/route names below are concepts, not a claim they already exist. Reuse shared permissions, validation, audit, evidence storage, API client and UI patterns.

## NCR / punch list — first priority

Persist project/form and field/response source links where applicable, number, severity/classification, disposition, corrective action/evidence, creator/assignee, independent verifier, decisions and timestamps. Fail must create or link the appropriate open NCR transactionally with the inspection update; repeated saves/retries/concurrent requests must not create endless duplicates. Define uniqueness/reopening behavior without erasing old cases. A later Pass does not silently close a prior NCR.

Implement corrective action → evidence → independent verification → close. Verify authority and independence server-side, reject failed verification closure, and preserve history. Add an explicit blocking classification/policy and approval guard for unresolved blocking NCRs. Do not assume every NCR blocks every record or that a free-text severity alone defines blocking. Document the selected policy/configuration and test it. Apply equivalent checks in handover readiness.

## ITP

Persist project, revision/version, activities/order/dependencies, Hold/Witness/Review points, execution state, submission, approval and controlled release. Validate dependency references and cycles. Activity B cannot progress until required predecessor A conditions hold. Hold release requires permission, prerequisites and recorded actor/time/comments/outcome; enforce in service/database transaction, not UI labels. Define Witness/Review obligations explicitly from configured rules rather than treating them all as Hold. Retain controlled historical versions and audit every revision/release.

## Controlled documents

Implement project links, document numbering/title, revisions/history/current revision, status, private file reference, creators/modifiers and audit. Do not overwrite earlier revision files or metadata without history. Verify number/revision uniqueness, authorised release and historical links from forms/ITP/dossier. Reuse existing storage contracts and limit file access to authorised users.

## Calibration

Implement instrument identifier, serial number, type/description, certificate, calibration date, due/expiry date and validity. Enforce expiry/out-of-calibration rejection server-side wherever calibrated instruments are required, including stale cached mobile selection. Define date/time boundary interpretation and test it; preserve the inspected instrument/certificate association for historical records. Record changes in audit. Do not rely only on filtering a picker.

## Training and competency

Implement course/session, participant, attendance, attendance signature, assessment, competency result/status and history. Attendance signatures must retain authorised identity and relevant record binding; do not reuse QCForm-only endpoints for unrelated records without a deliberate contract. Use permissions for assessments and controlled records. Feed reporting without exposing unrelated users' data.

## Shared integration and verification

Add permission policies, server guards, migrations, DTOs, validation/error handling, audit, UI/service integration and tests for each slice. Document new contracts and required configuration in status. Coordinate offline support: QC Fail can be captured offline, but queued/replayed responses must still produce one traceable NCR; show pending creation until acknowledged. New offline NCR operations must preserve dependency/idempotency rules.

Run AT-19 through AT-22, AT-07, AT-10, AT-11, AT-16, AT-25 relevant cases. Handoff readiness rules, reportable statuses, joins/aggregates and controlled history to reporting agent. Do not mark modules complete with CRUD scaffolding alone.
## Mandatory operating contract

Read `00_READ_ME_FIRST.md`, `01_PROJECT_MASTER_SPEC.md`, `IMPLEMENTATION_STATUS.md`, and `ACCEPTANCE_TESTS.md` before editing. All paths here are relative to the repository root unless stated otherwise. Inspect repository instructions, source, migrations, and actual database metadata; the checkpoint is evidence reported on 12 October 2026, not a fresh repository inspection. Preserve existing working behavior and the Dapper/Npgsql architecture. Never invent existing tables, methods, routes, enum values, or package capabilities.

Use Supabase Auth through the established API identity mapping. Both MAUI and teammate-owned WPF use the API; neither connects to PostgreSQL. Never place tokens, passwords, connection strings, or service-role keys in client code, logs, commits, screenshots, or handoff notes. Keep historical signatures, preserve unsynchronised work, and never silently overwrite conflicts. Business permissions and resource access must be enforced by the API.

Run the relevant build/tests before and after changes. Compilation is not runtime verification. Update `IMPLEMENTATION_STATUS.md` with exact commands, outcomes, environment, affected paths, migrations, remaining work, and acceptance evidence. Use VERIFIED WORKING, IMPLEMENTED BUT NOT VERIFIED, NOT IMPLEMENTED / INCOMPLETE, or BLOCKED. Preserve unverified and failed results honestly. Make small reviewable changes; do not reset unrelated work, deploy, change production data, or replace architecture merely to simplify the task. End with a concrete handoff to the next agent. Perform your assigned scope without waiting for approval of each routine reversible step.
