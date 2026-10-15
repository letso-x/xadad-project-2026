# MAUI UI agent

## Outcome and layering

Deliver a usable Android/iOS field-inspection workflow using Page → ViewModel → application service → local repository/API client → SQLite/filesystem/outbox. Consume the sync agent's durable services; do not place SQL or large HTTP workflows in Pages. Inspect existing Shell routes, navigation, binding/DI and reusable controls before replacing anything. Preserve unrelated UI work.

## Screens and behavior

- Login: supported Supabase flow, secure session, API linked active QMS identity verification. Handle invalid credentials, unlinked/inactive users, unavailable API and expired session distinctly. Do not admit fresh unauthorised logins based on a cached role. Use documented offline session policy for returning users.
- Dashboard: projects, forms in progress, returned forms, submitted/approved forms, pending sync and NCR attention where available. Show honest loading/empty/error/cached states; do not present placeholder counts as server facts.
- Projects/details: cached and available projects, existing forms, start form from eligible template version, resume Draft/InProgress/ReturnedForCorrection. Discover navigation routes; test deep navigation/back behavior and historical blank Clients/Search/project crash issues where still present.
- Form Library and Resume: version/revision visible; existing form renders its own snapshot. Avoid creating a new form every time a user resumes. Present offline availability honestly.
- Dynamic QC form: QCFormProjectFields, ordered sections, QCFormFields and responses. Support Text/LongText, Number/Decimal, Boolean/YesNo, Date/DateTime, Dropdown, MultiSelect, Photo, Signature, Reference. Respect requiredness, options, validation/tolerances, display order, labels, help/placeholder and evidence requirements. Distinguish null/unanswered from false, zero or N/A. Use stable field identities, not labels as keys. Do not hard-code the cable checklist.
- Photos/evidence: capture and selection, permission denial/cancellation, local durable save, preview, required indicators and pending/uploaded/retry states. Do not claim upload success until API acknowledgement. Do not expose private storage URLs as permanent public links.
- Signature: draw/clear/redraw/save locally, capture explicit signing intent, queue/upload Signature evidence and server signoff in correct lifecycle order. Explain pending vs server-bound signoff. A drawn image alone does not establish signer authority/content binding. Show historical signatures and whether they match current content; fresh consent/signoff is required for corrected content.
- Status/review: clearly show Draft, InProgress, Submitted, ReturnedForCorrection, Approved, Cancelled. Normal edits unlock only in first two and ReturnedForCorrection. Submitted permits controlled signing; Approved/Cancelled read-only. Show correction reason if available; edit/resubmit/fresh sign loop. Review/approval UI only for configured authority; server enforces rules regardless of visibility. Field Fail triggers/links NCR, never whole-form Rejected.
- Sync status: Offline, Pending sync, Syncing, Synced, Sync failed, Conflict with counts and actions from actual queue. Show failure reason without secrets. Never show Synced while relevant operations are pending or conflicting.
- Conflict UI: display local/server values or failed operation context, record/state change and actionable resolution. Preserve both versions while awaiting authorised resolution. Avoid automatic approval/signature merge and avoid discarding values merely to dismiss a message.

## User-flow verification

Run AT-03, AT-04, AT-06 through AT-09, AT-13 through AT-18, AT-24 and relevant device coverage. Verify numeric/date culture handling, accessibility labels, keyboard/focus, long checklists, rotation/resume and camera/signature permissions. Bind async commands safely, avoid duplicate taps/submissions and show validation near affected fields. Test app restart in the middle of capture/edit/sync.

Handoff implemented screens/routes, service dependencies, platform build/runtime evidence and unfinished modules. Android success does not prove iOS support. Do not fabricate unsupported platform verification or hide missing encryption/conflict behavior behind a polished UI.
## Mandatory operating contract

Read `00_READ_ME_FIRST.md`, `01_PROJECT_MASTER_SPEC.md`, `IMPLEMENTATION_STATUS.md`, and `ACCEPTANCE_TESTS.md` before editing. All paths here are relative to the repository root unless stated otherwise. Inspect repository instructions, source, migrations, and actual database metadata; the checkpoint is evidence reported on 12 October 2026, not a fresh repository inspection. Preserve existing working behavior and the Dapper/Npgsql architecture. Never invent existing tables, methods, routes, enum values, or package capabilities.

Use Supabase Auth through the established API identity mapping. Both MAUI and teammate-owned WPF use the API; neither connects to PostgreSQL. Never place tokens, passwords, connection strings, or service-role keys in client code, logs, commits, screenshots, or handoff notes. Keep historical signatures, preserve unsynchronised work, and never silently overwrite conflicts. Business permissions and resource access must be enforced by the API.

Run the relevant build/tests before and after changes. Compilation is not runtime verification. Update `IMPLEMENTATION_STATUS.md` with exact commands, outcomes, environment, affected paths, migrations, remaining work, and acceptance evidence. Use VERIFIED WORKING, IMPLEMENTED BUT NOT VERIFIED, NOT IMPLEMENTED / INCOMPLETE, or BLOCKED. Preserve unverified and failed results honestly. Make small reviewable changes; do not reset unrelated work, deploy, change production data, or replace architecture merely to simplify the task. End with a concrete handoff to the next agent. Perform your assigned scope without waiting for approval of each routine reversible step.
