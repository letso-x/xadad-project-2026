# Reporting and handover agent

## Outcome

Implement M9 handover dossier, M10 dashboards/reports and M11 internal audit, plus practical notifications. Inspect existing packages: proposal mentions QuestPDF and ClosedXML, but preserve working package choices and verify suitability/licensing before adding dependencies. Document any real external constraint rather than inventing installed capabilities.

## Reports and exports

Use authorised server-side aggregate/query endpoints, scoped to allowed projects/portfolio. Report QC status/progress, ITP progress, Hold status, open/closed NCRs, calibration and training/competency. Define denominators and count semantics; handle zero forms/fields and avoid multiplicative joins from evidence/signoffs. Include filters, pagination where needed, as-of timestamps and sensible loading/empty/error states. Do not download the entire database to MAUI for aggregation.

Generate completed QC PDF, project quality report PDF and handover dossier PDF. QC export includes project/form number, checklist/version/document information, project fields, sections, responses, evidence references, current status, historical/current signoffs, approval and timestamps where available. Use execution snapshots; never silently substitute current template labels/options. Clearly distinguish stale historical signatures from signatures valid for current content. Use private authorised file retrieval and handle missing evidence honestly.

Excel exports: QC register, NCR register, project status and ITP activity register. Preserve dates/numbers, filters and clear headings; avoid formula injection from user-entered text. Test meaningful sample output, long content and empty datasets. Inspect generated PDF pages and Excel contents; successful HTTP download alone is not proof of usable layout/data. Do not expose connection strings/tokens/private signed URLs in exports.

## Handover completeness

Derive required records/signatures/NCR conditions from explicit project/template/ITP rules, not merely from whichever records already exist. Return structured missing/invalid/blocking reasons. Mandatory missing records, invalid/stale signatures or unresolved blocking NCRs prevent readiness or clearly flag an incomplete draft. Final release must enforce the readiness gate, with authority, audit and consistent version/snapshot of the compiled dossier. Do not label an incomplete compilation final/ready. Preserve previously issued dossier history and trace included revisions/evidence/signoffs.

## Internal audit

Implement programme, checklist, execution, findings, follow-up/corrective actions, responsible person, status and closure with traceable history, permissions and evidence. Distinguish this business module from AuditEvent technical/lifecycle trail. Reuse applicable NCR concepts deliberately without confusing their identities or closure rules. The proposal references ISO 19011:2018; software features do not constitute certification.

## Notifications

Provide a practical notification slice for actionable events such as returned forms, assigned/open NCRs, Hold action and calibration due status, according to confirmed/configured policy. Authorised in-app notification state is a reasonable engineering default if no delivery channel is specified. Do not send email/SMS or create external subscriptions without session authorisation. Record chosen channel, recipient scoping, duplicate prevention and outstanding delivery requirements. Notifications remain a required module feature if deferred.

## Verification and handoff

Run AT-23 through AT-26 plus authorisation/audit cases. Verify export layouts and content against fixture records, readiness failures and success, private evidence access and stable compiled history. Record actual artifact-generation dependencies and deployment configuration, runtime evidence, unfinished policy details and module integration points for QA. Do not report final readiness from placeholder counts.
## Mandatory operating contract

Read `00_READ_ME_FIRST.md`, `01_PROJECT_MASTER_SPEC.md`, `IMPLEMENTATION_STATUS.md`, and `ACCEPTANCE_TESTS.md` before editing. All paths here are relative to the repository root unless stated otherwise. Inspect repository instructions, source, migrations, and actual database metadata; the checkpoint is evidence reported on 12 October 2026, not a fresh repository inspection. Preserve existing working behavior and the Dapper/Npgsql architecture. Never invent existing tables, methods, routes, enum values, or package capabilities.

Use Supabase Auth through the established API identity mapping. Both MAUI and teammate-owned WPF use the API; neither connects to PostgreSQL. Never place tokens, passwords, connection strings, or service-role keys in client code, logs, commits, screenshots, or handoff notes. Keep historical signatures, preserve unsynchronised work, and never silently overwrite conflicts. Business permissions and resource access must be enforced by the API.

Run the relevant build/tests before and after changes. Compilation is not runtime verification. Update `IMPLEMENTATION_STATUS.md` with exact commands, outcomes, environment, affected paths, migrations, remaining work, and acceptance evidence. Use VERIFIED WORKING, IMPLEMENTED BUT NOT VERIFIED, NOT IMPLEMENTED / INCOMPLETE, or BLOCKED. Preserve unverified and failed results honestly. Make small reviewable changes; do not reset unrelated work, deploy, change production data, or replace architecture merely to simplify the task. End with a concrete handoff to the next agent. Perform your assigned scope without waiting for approval of each routine reversible step.
