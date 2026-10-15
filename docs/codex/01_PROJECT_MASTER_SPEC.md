# Mzukulu QMS — project master specification

## How to use this specification

Read `00_READ_ME_FIRST.md` for mandatory execution/security/Git rules. The full checkpoint below is authoritative project intent and reported history, not fresh verification. Original numbering is retained even where inconsistent. No local attachment path or external chat access is needed to use the checkpoint. Later observed repository/schema evidence must be recorded in IMPLEMENTATION_STATUS.md; it does not erase unmet requirements.

## Earlier schema context and integration cautions

The earlier shared conversation separates ChecklistTemplate → ChecklistTemplateVersion → ChecklistSection → ChecklistField from QCForm → QCFormSection → QCFormField → QCResponse. Evidence/NCR/QCSignOff attach to forms, with User, AuditEvent, SyncOperation, SyncConflict, Client, Project and cable registers supporting them. Server business IDs are commonly bigint identities; User and sync identifiers are UUIDs. JSONB stores options/validation/response values. Those physical definitions are historical examples, not a migration to run now.

The newer checkpoint adds ChecklistTemplateProjectField and QCFormProjectField, ContentHash, ReturnedForCorrection and other working behavior. Verify real enums, columns and functions. The earlier SQL omitted these additions and did not fully enforce all relationships. Inspect and address these engineering risks as applicable:

- Enforce template/version match, field/form ownership for responses and evidence, and signature evidence/form identity. Separate foreign keys do not alone prove same-form membership.
- Make form creation and snapshot generation atomic and retry-safe. The earlier generate_qcform_structure loop could duplicate sections/fields on repeated calls and accepted a version argument independently of the form. Preserve section requiredness and exclude inactive fields according to confirmed policy.
- Freeze published definitions or generate a new controlled version. Snapshot all render/validation metadata and project-field definitions; do not mutate historical forms when definitions change.
- Preserve stable ordering, including tie-breakers. Ensure empty-form/empty-section retrieval and aggregate counts are correct; earlier inner-join views could hide empty structures and left-join counts could count a phantom unanswered row.
- Add deliberate concurrency/version and idempotency contracts for offline writes. UpdatedAt alone must not be assumed a sufficient monotonic sync cursor. Review historical cascade-delete rules before using them on quality records.

Regression fixture: create Equipment Inspection version 1 with Inspection Details containing equipment_number (required Text) and inspection_result (required Dropdown with Pass, Fail, N/A). Start a form and verify both snapshotted fields appear in API and MAUI; publish a later version and verify the existing form remains unchanged. Use returned IDs, never hard-coded identity assumptions.

## Full user-supplied requirements and checkpoint (verbatim)

MZUKULU QUALITY MANAGEMENT SYSTEM
FULL REQUIREMENTS, CURRENT IMPLEMENTATION STATE, AND REMAINING WORK
Current checkpoint: 12 October 2026
1. PROJECT OVERVIEW
The project is the Mzukulu Quality Management System.
The purpose of the system is to replace Mzukulu Technologies’ paper-based and spreadsheet-based quality-management processes with an integrated software system.
The solution consists of:
- Windows desktop application
- .NET MAUI mobile application
- ASP.NET Core Web API
- PostgreSQL central database
- Supabase Storage
- Supabase Authentication
- SQLite local mobile database for offline use
The system is intended to support Mzukulu’s quality-management processes in accordance with ISO 9001:2015.
Internal-audit functionality is also associated with ISO 19011:2018 in the original proposal.
The user is responsible primarily for:
- ASP.NET Core API
- .NET MAUI mobile application
A teammate owns the Windows desktop application.
Repository:
letso-x/xadad-project-2026
Known API project:
mobile/MzukuluQMS.Api
Current API stack:
- .NET 10
- ASP.NET Core
- Dapper
- Npgsql
- PostgreSQL
- Supabase
- Supabase Storage
- Supabase Auth
- JWT authentication
- xUnit
- Moq
Important implementation decision:
The proposal mentioned Entity Framework Core, but the actual backend implementation uses Dapper and Npgsql.
Do not rewrite the working repository architecture to Entity Framework merely to match the proposal.
2. LOCKED SYSTEM ARCHITECTURE
The intended architecture is:
                     SUPABASE
                PostgreSQL + Storage
                       ▲
                       │
                ASP.NET Core API
                       ▲
                       │ HTTPS
          ┌────────────┴────────────┐
          │                         │
     MAUI Mobile              Windows Desktop
     Android + iOS                 WPF
          │
    ┌─────┴─────┐
    │           │
 SQLite       API Client
    │           │
    │           ▼
    │      ASP.NET Core API
    │
 Sync Queue
    │
    └──────► API when online

Architecture rules:
- MAUI must not connect directly to PostgreSQL.
- WPF must not connect directly to PostgreSQL.
- MAUI and WPF both communicate through the ASP.NET Core API.
- PostgreSQL is the central server-side source of truth.
- Supabase Storage stores server-side evidence files.
- SQLite is the temporary local operational source of truth when mobile is offline.
- The API owns server-side validation and business rules.
- The mobile app may perform local validation for user experience, but server validation remains authoritative.
- Mobile must not contain a Supabase service-role key.
- Mobile must not contain a database connection string.
- Historical QC forms must use their execution snapshot, not the current live checklist template.
- Offline conflicts must not silently overwrite central data.
- Unsynchronised local data must not be silently discarded.
- Historical electronic signatures must remain available for audit.
3. OFFICIAL REQUIRED MODULES
The official proposal requires the following modules.
M1: SECURITY AND AUDIT
Required functionality:
- authentication
- users
- roles
- permissions
- electronic signatures
- signature authority
- audit trail
- approval history
- traceability of significant actions
The audit trail must identify:
- who performed the action
- what action was performed
- which record was affected
- when the action occurred
M2: PROJECTS AND DOCUMENTS
Required functionality:
- clients
- project register
- project master information
- controlled-document register
- document numbering
- document revisions
- revision history
- project-document relationships
M3: INSPECTION AND TEST PLAN
Required functionality:
- create ITP
- revise ITP
- sequential activities
- dependencies
- Hold points
- Witness points
- Review points
- execution progress
- submission
- approval
- controlled release
Hold points must prevent progression until the required conditions and authority are satisfied.
M4: CHECK SHEET ENGINE
Required functionality:
- configurable quality check-sheet templates
- QCS-01 through QCS-11
- sections
- dynamic fields
- validation rules
- Pass
- Fail
- N/A
- numeric readings
- tolerances
- comments
- references
- photographs
- supporting evidence
- electronic signatures
- dual signatures where required
Failed inspection items must create or link an NCR.
Templates must be configurable/extensible by an authorised quality administrator.
M5: MOBILE FIELD CAPTURE
Required functionality:
- Android and iOS support through .NET MAUI
- field inspections
- project information
- dynamic check sheets
- Pass/Fail/N/A
- numeric readings
- comments
- photographs
- evidence
- signatures
- offline operation
- local SQLite persistence
- later synchronisation
- conflict recording
M6: NCR / PUNCH LIST
Required functionality:
- NCR register
- punch list
- link NCR to source inspection
- link to source field where applicable
- severity
- classification
- disposition
- corrective action
- corrective evidence
- independent verification
- close-out
- timestamps
- complete traceability
If verification fails, the NCR must remain open.
Unresolved blocking NCRs should prevent approval where required.
M7: CALIBRATION REGISTER
Required functionality:
- equipment/instrument register
- instrument identifier
- serial number
- calibration certificate
- calibration date
- next due date
- expiry
- validity status
Where calibrated equipment is required, expired or out-of-calibration equipment must not be selectable.
M8: TRAINING AND COMPETENCY
Required functionality:
- training sessions
- courses
- attendance
- attendance signatures
- competency assessments
- competency records
M9: HANDOVER DOSSIER
Required functionality:
- completeness validation
- required-record checking
- signature checking
- NCR checking
- quality-record compilation
- final handover dossier
- PDF generation
Missing mandatory records, signatures or open blocking NCRs must prevent or clearly flag handover readiness.
M10: DASHBOARDS AND REPORTING
Required functionality:
- project dashboards
- portfolio dashboards
- inspection progress
- ITP progress
- Hold-point status
- NCR status
- calibration status
- training status
- PDF export
- Excel export
- notifications
M11: INTERNAL AUDIT
Required functionality:
- audit programme
- audit checklist
- audit execution
- findings
- follow-up actions
- corrective actions
- status
- closure
4. HIGH-LEVEL BUSINESS PROCESS
The official proposed system process is:
1. Authentication and authorisation
2. Project setup
3. ITP definition
4. Inspection preparation
5. Inspection execution
6. NCR management
7. Review and approval
8. Offline synchronisation
9. Monitoring and reporting
10. Handover completeness and dossier
11. CHECKLIST TEMPLATE ARCHITECTURE
Checklist configuration and inspection execution must remain separate.
Template definition:
ChecklistTemplate
    ↓
ChecklistTemplateVersion
    ├── ChecklistTemplateProjectField
    └── ChecklistSection
         └── ChecklistField
Execution snapshot:
QCForm
├── QCFormProjectField
├── QCFormSection
│   └── QCFormField
│       └── QCResponse
├── Evidence
└── QCSignOff
When a QC form is created, the structure must be generated from a specific checklist-template version.
After that point, the QC form must retain its own structure.
If the checklist template changes later, an old QC form must not suddenly change.
6. QC INSPECTION USE CASE
Primary actor:
Inspector / QC Site user
Preconditions:
- user authenticated
- user authorised
- project exists
- checklist template exists
Main flow:
1. User chooses project.
2. User chooses/starts inspection.
3. Check sheet loads.
4. User enters Pass/Fail/N/A.
5. User enters readings.
6. User enters comments.
7. User attaches photos/evidence.
8. User signs.
9. User submits.
Failure flow:
If an inspection item fails:
- create or link NCR/punch item
Offline flow:
If device is offline:
- retain inspection locally
- preserve evidence locally
- synchronise when connectivity returns
Postcondition:
The inspection must contain:
- responses
- evidence
- signature
- audit information
- submission state
7. QC REVIEW AND APPROVAL USE CASE
Functional actors include:
- Managing Contractor / Reviewer
- Client / Employer Representative
Preconditions:
- QC record submitted
- required inspection signature exists
- reviewer has appropriate authority
Reviewer must be able to:
- inspect results
- inspect evidence
- inspect NCR status
- approve
- return for correction
Where required:
- reviewer applies electronic approval signature
If information is incomplete or unresolved NCRs exist:
- return for correction rather than approve
Postcondition:
- review decision stored
- signatures stored
- audit history stored
- approved record contributes to handover completion
8. QCFORM STATE MODEL
The correct intended lifecycle is:
Draft
  ↓
InProgress
  ↓
Submitted
  ├── Approved
  │
  └── ReturnedForCorrection
          ↓
        edit
          ↓
       Submitted
Important distinction:
The PostgreSQL form_status enum currently also contains:
Rejected
However, Rejected should be treated as legacy/unused for normal whole-record review.
ReturnedForCorrection is the correct state when the reviewer needs the inspector to fix the record.
A persisted UnderReview state is not currently required.
9. FIELD RESULT VS RECORD REVIEW
These are different concepts.
Field-level outcome:
- Pass
- Fail
- N/A
Field Fail:
Fail
 ↓
NCR
 ↓
Corrective action
 ↓
Verification
 ↓
Close
Whole QC record review:
Submitted
 ├── Approved
 └── ReturnedForCorrection
Do not turn a field-level Fail into whole-form Rejected status.
10. AUTHENTICATION ARCHITECTURE
Authentication uses Supabase Auth.
The API validates Supabase ES256 JWTs.
Known configuration includes:
- Supabase OpenID/OIDC metadata
- audience validation
- issuer/signature/lifetime validation
- MapInboundClaims = false
- NameClaimType = sub
The JWT sub claim identifies the Supabase user.
The API maps:
JWT sub
→ User.ExternalAuthID
→ QMS User record
Known QMS-specific claims:
- qms_user_id
- qms_role
Known policy:
QmsUser
The QmsUser policy proves that the authenticated Supabase user is linked to an active QMS user.
Known endpoint:
GET /api/auth/me
Known working QMS test user:
UserID:
4724a990-4bdb-4313-a62d-0a10247beeb1
ExternalAuthID:
d1377b26-49b4-4a57-9a3f-531b5c7236d7
Role:
Inspector
IsActive:
true
Security rules:
- never commit bearer tokens
- never commit passwords
- never commit Supabase service-role keys
- never commit database passwords
- never put real secrets into source-controlled appsettings files
- server secrets currently use User Secrets
- do not expose tokens in logs
- do not embed database credentials in MAUI
11. ROLES AND PERMISSIONS
The original documentation says seven roles exist.
The final names of all seven roles are not fully confirmed.
Existing database/application roles observed:
- Admin
- Manager
- Inspector
- Technician
- Viewer
These should not automatically be treated as the final seven official roles.
The architecture should move toward:
User
 ↓
Role
 ↓
RolePermission
 ↓
Permission
Do not scatter hard-coded role comparisons throughout controllers.
Important:
QMS Role is not the same thing as QC SignOffType.
Known QC SignOffType values:
- Inspector
- Technician
- Supervisor
- Client
12. CLIENTS CURRENT STATUS
Clients functionality is substantially implemented.
Known functionality:
- GET clients
- GET client by ID
- POST client
- PUT client
- active/inactive handling
13. PROJECTS CURRENT STATUS
Projects were the first API vertical slice implemented.
Known functionality:
- GET projects
- GET project by ID
- POST project
- validate Client exists
- validate Client active
- duplicate/business validation
Known working ProjectID:
1
14. CHECKLIST TEMPLATE CURRENT STATUS
Known working template:
ChecklistTemplateID:
5
Active ChecklistTemplateVersionID:
5
Known project template fields:
Field 4:
area_location
required
Text
maxLength 100
Field 5:
drawing_number
optional
maxLength 50
Known section:
Cable Installation Details
Known checklist fields:
ChecklistFieldID 18:
cable_number
required
ChecklistFieldID 19:
from_location
required
ChecklistFieldID 20:
to_location
required
ChecklistFieldID 21:
installation_result
Dropdown
required
Options:
Pass
Fail
N/A
ChecklistFieldID 22:
installation_photo
Photo
required
RequiresPhoto = true
15. KNOWN DYNAMIC FIELD TYPES
Known field types include:
- Text
- LongText
- Number
- Decimal
- Boolean
- YesNo
- Date
- DateTime
- Dropdown
- MultiSelect
- Photo
- Signature
- Reference
An existing FieldValueValidator already exists and should be reused/extended rather than duplicated.
16. QCFORM CREATION CURRENT STATUS
Known endpoint:
POST /api/qc-forms
Current working behavior:
- validates request
- resolves active checklist-template version
- creates QCForm
- records authenticated StartedByUserID
- generates snapshot structure
- returns nested QCForm
Known PostgreSQL function:
generate_qcform_structure(
    p_qcform_id bigint,
    p_template_version_id bigint
) RETURNS void
17. QCFORM RETRIEVAL
Nested QCForm GET functionality works.
The API can return:
- QCForm
- project fields
- sections
- fields
- responses
- evidence/signoff-related information as implemented
18. RESPONSE UPDATE CURRENT STATUS
Known endpoint:
PUT /api/qc-forms/{id}/responses/{responseId}
Current functionality:
- update dynamic field response
- FieldValueValidator
- authenticated AnsweredByUserID
- AnsweredAt / relevant timestamps
19. PROJECT FIELD UPDATE CURRENT STATUS
Known endpoint:
PUT /api/qc-forms/{id}/project-fields/{fieldId}
Current functionality:
- update QC form project-information fields
- enforce editability rules
- validate required/metadata behavior
20. EDITABILITY RULES
Editable:
- Draft
- InProgress
- ReturnedForCorrection
Normal response/project-field editing blocked:
- Submitted
- Approved
- Cancelled
Submitted forms may still permit Signature evidence/signoff.
21. EVIDENCE CURRENT STATUS
Known endpoint:
POST /api/qc-forms/{id}/evidence
Important:
The endpoint is POST, not PUT.
Request uses multipart/form-data.
Known form fields:
EvidenceType
QCFormFieldID, when applicable
File
Known server storage bucket:
qms-evidence
Known path format:
qc-forms/{qcFormId}/{field-id-or-form}/{unique-file-name}
Known Evidence table fields:
EvidenceID bigint
QCFormID bigint
QCFormFieldID nullable bigint
EvidenceType enum
StorageBucket nullable
StoragePath
FileName nullable
ContentType nullable
FileSizeBytes nullable bigint
Latitude nullable numeric
Longitude nullable numeric
CapturedAt nullable timestamptz
UploadedByUserID nullable uuid
CreatedAt timestamptz
Known working evidence types:
- Photo
- Signature
The service may reference Document, but the actual PostgreSQL enum must be verified before relying on Document.
Existing evidence service behavior includes:
- validate form exists
- validate field belongs to form
- validate file
- validate type
- upload file to Supabase Storage
- insert evidence metadata
- record authenticated uploader
- if DB metadata insert fails after storage upload, delete uploaded storage object
This compensation behavior should be preserved.
22. EVIDENCE STATUS RULES
Intended behavior:
Draft:
normal evidence allowed
InProgress:
normal evidence allowed
ReturnedForCorrection:
normal evidence allowed
Submitted:
normal photo/document mutation blocked
Signature evidence may be allowed
Approved:
evidence mutation blocked
Cancelled:
evidence mutation blocked
23. SUBMISSION CURRENT STATUS
Known endpoint:
POST /api/qc-forms/{id}/submit
Submission validation currently checks:
- required QCFormProjectFields
- required non-photo/signature responses
- required photo evidence
Allowed source states:
- Draft
- InProgress
- ReturnedForCorrection
On success:
- Status becomes Submitted
- SubmittedAt is set
- UpdatedAt is updated
Submission has already been successfully exercised.
24. ELECTRONIC SIGNATURE CURRENT STATUS
Known endpoint:
POST /api/qc-forms/{id}/sign-offs
Known QCSignOff fields:
QCSignOffID bigint
QCFormID bigint
UserID uuid nullable
SignOffType
SignatureEvidenceID nullable
SignedAt
Comments
ContentHash varchar(64)
Known signoff types:
- Inspector
- Technician
- Supervisor
- Client
Current signoff rules include:
- form must be Submitted
- Signature Evidence must belong to same QCForm
- EvidenceType must be Signature
- Signature Evidence must have been uploaded by the signing user
- UserID is server-owned
- SignedAt is server-owned
- ContentHash is server-generated
25. CONTENT HASH CURRENT STATUS
The system has a QCFormContentHashService.
It calculates a SHA-256 hash of signable content.
Signable content currently includes:
- QCForm header/details
- project field values
- QCResponse values
- non-signature evidence information
Signature evidence itself is excluded.
Stored result:
64-character lowercase hexadecimal SHA-256 hash.
Known successful QCForm 8 hash:
5155df27b038feacde92fc75b2829b05b1d16381ce48e37f3d4f8346fab155e2
26. CRITICAL SIGNATURE PROBLEM STILL TO FIX
The current unresolved issue is stale signatures after correction.
Example:
Submitted
 ↓
Inspector signs
 ↓
Reviewer returns for correction
 ↓
Inspector changes response
 ↓
Inspector resubmits
The old signature must remain for audit history.
However:
The old ContentHash represents the old content.
Therefore:
- the old signoff must not be considered a valid signature for the corrected content
- the corrected content must produce a new current hash
- a new signoff must be required
- approval must validate current signatures against the current content hash
Do not delete old signoffs.
Do not overwrite old ContentHash values.
27. APPROVAL CURRENT STATUS
Known endpoint:
POST /api/qc-forms/{id}/approve
Current behavior:
- only Submitted forms can be approved
- repository update checks current status
- invalid state returns conflict
QCForm 8 successfully completed:
Submitted
→ Approved
QCForm 8 should be retained as a known successful historical integration record.
28. RETURN FOR CORRECTION CURRENT STATUS
Known endpoint:
POST /api/qc-forms/{id}/return-for-correction
Implemented source state:
Submitted
Target state:
ReturnedForCorrection
Implemented behavior includes:
- status transition
- returned form editability
- evidence editability
- resubmission eligibility
However, the full runtime loop has not yet been fully verified.
Still needs runtime proof:
Submitted
→ sign
→ ReturnedForCorrection
→ edit
→ verify old signature stale
→ resubmit
→ new signoff
→ approve
29. CURRENT QCFORM 8 STATUS
QCFormID:
8
Known successfully exercised:
- project-field updates
- response updates
- photo evidence
- submission
- signature evidence
- QCSignOff
- content hash
- approval
Current status:
Approved
Known signoff:
QCSignOffID:
2
SignatureEvidenceID:
10
ContentHash:
5155df27b038feacde92fc75b2829b05b1d16381ce48e37f3d4f8346fab155e2
30. CURRENT QCFORM 9 STATUS
QCFormID:
9
ProjectID:
1
ChecklistTemplateID:
5
ChecklistTemplateVersionID:
5
FormNumber:
QC-0009
Last known status:
Draft
Project fields:
QCFormProjectFieldID 8:
area_location
required
QCFormProjectFieldID 9:
drawing_number
optional
Responses:
QCResponseID 33
QCFormFieldID 35
cable_number
QCResponseID 34
QCFormFieldID 36
from_location
QCResponseID 35
QCFormFieldID 37
to_location
QCResponseID 36
QCFormFieldID 38
installation_result
QCResponseID 37
QCFormFieldID 39
installation_photo
The last manual issue was with Postman photo upload.
Postman showed a selected image filename, but diagnostics reported:
File: undefined
The API had already successfully accepted photo uploads for QCForm 8.
Therefore, the QCForm 9 photo issue was not proven to be an API bug.
Likely causes included Postman local file access, Desktop/Web Agent behavior, or local path permissions.
The API should not be modified solely because of that Postman symptom unless a real server exception proves an API defect.
31. API EXCEPTION HANDLING CURRENT STATUS
Known mapping:
ArgumentException
→ 400
KeyNotFoundException
→ 404
ConflictException
→ 409
InvalidOperationException
→ 409
Unknown exception
→ 500
The broad InvalidOperationException mapping may be refined later, but should not be rewritten just for style.
32. DATABASE CONNECTION FACTORY
Known correct method:
CreateOpenConnectionAsync(CancellationToken)
Do not invent:
CreateConnectionAsync
unless the actual interface has changed.
33. AUDIT CURRENT STATUS
An AuditEvent table already exists in PostgreSQL.
Its actual schema has not yet been inspected during this development pass.
Therefore:
Do not invent its columns.
Before implementing audit:
- inspect the real AuditEvent schema
- inspect any existing models/repositories
Minimum significant actions to audit:
- QCFormCreated
- QCResponseUpdated
- QCFormProjectFieldUpdated
- EvidenceUploaded
- QCFormSubmitted
- QCFormReturnedForCorrection
- QCFormSigned
- QCFormApproved
- NCRCreated
- NCR corrective action
- NCR verification
- NCR close
- ITP changes
- Hold release
- controlled document revision
- calibration changes
Audit must at minimum answer:
- actor
- action
- affected entity
- timestamp
34. PERMISSION MODEL STILL NEEDED
Current QmsUser policy only confirms:
- authenticated
- linked QMS user
- active user
More specific business permissions are still required.
Examples:
- ManageProjects
- ManageTemplates
- PerformInspection
- SubmitInspection
- ReviewInspection
- ApproveInspection
- ReleaseHoldPoint
- ManageCalibration
- ManageDocuments
Do not hard-code the final seven roles.
Prefer a configurable permission abstraction.
35. NCR MODULE: STILL REQUIRED
NCR/Punch List is not yet implemented as a complete vertical slice.
Required flow:
QC field = Fail
 ↓
Create or link NCR
 ↓
Severity/classification
 ↓
Disposition
 ↓
Corrective action
 ↓
Corrective evidence
 ↓
Independent verification
 ↓
Close
NCR should retain traceability to:
- project
- QCForm
- QCFormField/QCResponse where applicable
- creator
- corrective action
- verifier
- timestamps
Important rule:
Repeated saving of the same failed field must not create endless duplicate NCRs.
An existing open NCR should be reused/linked where appropriate.
Approval must eventually block when a blocking NCR remains unresolved.
36. ITP MODULE: STILL REQUIRED
Inspection and Test Plan functionality is not yet implemented as a complete module.
Required entities/concepts:
- ITP
- project
- revision/version
- activities
- sequence/order
- dependencies
- Hold
- Witness
- Review
- execution state
- submission
- approval
- release
Dependencies must be enforced server-side.
Example:
Activity B depends on Activity A.
B must not progress if A has not met its required state.
Hold point requirements:
- authorised user only
- prerequisites satisfied
- release actor recorded
- release time recorded
- release comments/outcome where appropriate
Hold release must be an enforced business rule, not merely a UI label.
37. CONTROLLED DOCUMENTS: STILL REQUIRED
The system still needs a proper controlled-document workflow.
Required:
- project association
- document number
- title
- revision
- revision history
- current revision
- status
- file/storage reference if used
- created/modified identity
- audit information
Historical revisions must not be overwritten without traceability.
38. CALIBRATION: STILL REQUIRED
Required:
- instrument register
- identifier
- serial number
- type/description
- calibration certificate
- calibration date
- due/expiry date
- validity
Critical rule:
Expired or out-of-calibration equipment must be rejected by server-side validation where calibrated equipment is required.
39. TRAINING AND COMPETENCY: STILL REQUIRED
Required minimum:
- training session/course
- participant
- attendance
- attendance signature
- competency assessment
- competency result/status
40. REPORTING: STILL REQUIRED
Required reporting includes:
- project quality status
- QCForm status counts
- inspection progress
- ITP progress
- Hold status
- open NCRs
- closed NCRs
- calibration status
- training/competency status
- project dashboards
- portfolio dashboards
- PDF
- Excel
- notifications
Server-side aggregate/reporting endpoints are preferable to downloading the entire database into MAUI for calculations.
41. PDF REQUIREMENTS
The original proposal mentioned QuestPDF.
Before adding anything:
Inspect current repository packages.
Useful PDF outputs:
- completed QC inspection record
- project quality report
- final handover dossier
A QC PDF should include, where available:
- project
- form number
- checklist/document information
- project fields
- sections
- responses
- evidence metadata/references
- status
- signoffs
- approval
- timestamps
42. EXCEL REQUIREMENTS
The original proposal mentioned ClosedXML.
Again, inspect current packages first.
Useful exports include:
- QC register
- NCR register
- project status
- ITP activity register
43. HANDOVER: STILL REQUIRED
Before creating final dossier, the system must check:
- required QC records
- required signatures
- required approvals
- required evidence
- unresolved blocking NCRs
- project quality completeness
Useful completeness result:
- IsReady
- missing records
- missing signatures
- open NCRs
- outstanding requirements
Do not generate a “complete” handover dossier when the system knows the project is incomplete.
44. INTERNAL AUDIT: STILL REQUIRED
This is different from the technical AuditEvent trail.
Internal Audit business module requires:
- audit programme
- audit record
- audit checklist
- findings
- follow-up actions
- responsible person
- status
- closure
45. OFFLINE MOBILE REQUIREMENT
Offline mobile support is mandatory.
The user must be able to:
1. load/cached inspection data
2. lose internet access
3. continue working
4. update responses
5. update project fields
6. capture photos
7. capture signatures
8. close the app
9. reopen the app
10. still see the unsynchronised work
11. reconnect
12. synchronise safely
13. TARGET MOBILE LAYERING
Recommended architecture:
Page
 ↓
ViewModel
 ↓
Application Service
 ↓
Local Repository / API Client
 ↓
SQLite + Filesystem + Sync Queue
Do not place raw SQL directly in MAUI Pages.
Do not place large amounts of direct HTTP orchestration in Pages.
47. MOBILE LOCAL DATA
SQLite should contain only what the mobile app needs.
Likely local entities include:
- LocalClient or client summary
- LocalProject
- LocalQCForm
- LocalQCFormProjectField
- LocalQCFormSection
- LocalQCFormField
- LocalQCResponse
- LocalEvidence
- SyncOperation
- possibly SyncConflict
- cached template/form-library metadata
Do not mirror every PostgreSQL table without reason.
48. LOCAL ID STRATEGY
Server entities commonly use bigint IDs.
Offline-created entities may not yet have server IDs.
Recommended strategy:
LocalId = Guid
ServerId = nullable long
Downloaded server records preserve their server ID.
Offline-created objects receive LocalId immediately.
ServerId is filled once synchronisation succeeds.
49. OFFLINE EVIDENCE DESIGN
Recommended process:
capture/select photo or signature
 ↓
copy into app-controlled local filesystem
 ↓
save metadata into SQLite
 ↓
queue upload
 ↓
reconnect
 ↓
POST to ASP.NET API
 ↓
receive server EvidenceID
 ↓
update local record
 ↓
mark operation complete
Do not upload directly from mobile to Supabase using a server secret.
Prefer filesystem files over storing large image blobs in SQLite.
50. SYNC QUEUE REQUIREMENT
A persistent sync queue is needed.
Suggested SyncOperation data:
- LocalOperationId Guid
- EntityType
- EntityLocalId
- EntityServerId nullable
- OperationType
- Payload
- CreatedAt
- RetryCount
- LastAttemptAt
- LastError
- Status
Suggested statuses:
- Pending
- Processing
- Completed
- Failed
- Conflict
51. MINIMUM SYNC OPERATIONS
At minimum:
- UpdateQCFormProjectField
- UpdateQCResponse
- UploadEvidence
Likely also required for full mobile workflow:
- CreateQCForm
- SubmitQCForm
- CreateSignOff
Possibly later:
- NCR operations
52. LOCAL-FIRST WRITE MODEL
When user edits while offline:
user changes value
 ↓
write to SQLite
 ↓
queue SyncOperation
 ↓
update UI immediately
Do not make the mobile UI wait for a server round-trip for every field edit.
53. SYNC PROCESS
When online:
pending operation
 ↓
call API
 ↓
success
 ↓
update local state/server ID
 ↓
mark completed
Transient error:
- keep operation
- increment RetryCount
- save LastError
- retry later
Authentication error:
- pause sync
- refresh/re-authenticate if possible
Business conflict:
- preserve local data
- preserve server data
- mark Conflict
- surface conflict to user
54. CONNECTIVITY RULE
MAUI NetworkAccess.Internet does not guarantee the API is reachable.
Sync must handle:
- timeout
- DNS failure
- connection refused
- connection reset
- 401
- 403
- 409
- 500
- unavailable API
55. IDEMPOTENCY STILL NEEDED
Retries can create duplicates.
Example:
1. photo upload reaches API
2. API completes upload
3. network drops before response reaches device
4. device retries
5. duplicate evidence created
Practical idempotency should be implemented for dangerous retry operations, especially:
- evidence upload
- offline-created QCForms
- potentially queued signoffs
A client-generated operation ID / idempotency key is recommended.
Do not over-engineer a generic distributed-systems framework.
56. MOBILE AUTHENTICATION REQUIREMENT
Do not store plaintext passwords.
Sensitive session/token information should use SecureStorage or equivalent.
If refresh is available:
- refresh session where possible
If authentication can no longer be restored:
- pause sync
- require login when online
Never put Supabase server secrets in MAUI.
57. MOBILE SQLITE ENCRYPTION REQUIREMENT
The official requirement calls for encrypted SQLite/local mobile storage.
The implementation still needs to inspect available packages/platform capabilities.
Important:
Do not falsely claim ordinary SQLite is encrypted.
If a practical encrypted SQLite implementation cannot be safely completed:
- document limitation
- protect session secrets with SecureStorage
- minimise sensitive information in SQLite
- mark encrypted database as incomplete rather than pretending it exists
58. EARLIER MAUI IMPLEMENTATION CONTEXT
Earlier MAUI application contained pages including:
- Dashboard
- Clients
- Projects
- Form Library
- Search
- Checklist Builder
- MainPage
Earlier local entities included:
- LocalClient
- LocalProject
- LocalChecklistTemplate
- LocalChecklistSection
- LocalChecklistItem
- LocalQCForm
Earlier issues included:
- SQLite “Cannot add a PRIMARY KEY column”
- Shell relative-routing errors
- Clients/Search appearing blank
- crashes opening projects
- crashes creating checklists
- fields created in Checklist Builder not appearing in initiated QC forms
The current repository must be inspected rather than assuming these old issues still exist.
59. REQUIRED MAUI SCREENS
Minimum useful field workflow:
- Login
- Dashboard
- Projects
- Project details
- Form Library / available forms
- Existing QC Forms / Resume
- Dynamic QC Form
- Evidence capture
- Signature capture
- Sync status
- Conflict UI
Clients may remain if relevant to navigation.
60. MAUI LOGIN REQUIREMENTS
Login should:
- authenticate through Supabase-supported flow
- obtain valid token/session
- verify linked QMS user through API
- store session securely
- enter application only when authorised
Handle:
- invalid credentials
- unlinked Supabase user
- inactive QMS user
- API unavailable
- expired session
61. MAUI DASHBOARD REQUIREMENTS
Useful dashboard data includes:
- projects
- forms in progress
- returned-for-correction forms
- pending sync
- submitted forms
- approved forms
- NCR requiring attention where available
62. MAUI PROJECT FLOW
User should be able to:
- see cached/available projects
- open project
- see existing QC forms
- start new form
- resume existing Draft/InProgress/ReturnedForCorrection form
Cached projects/forms should remain accessible offline.
63. MAUI DYNAMIC FORM RENDERER
Do not hard-code the cable installation form.
Render from QCForm snapshot:
- QCFormProjectFields
- QCFormSections
- QCFormFields
- QCResponses
Controls should support known field types.
Examples:
Text:
Entry
LongText:
Editor
Number/Decimal:
numeric entry/keyboard
Boolean/YesNo:
switch/radio/select
Date:
DatePicker
DateTime:
date/time controls
Dropdown:
Picker
MultiSelect:
appropriate multi-selection UI
Photo:
capture/select control
Signature:
signature capture
Reference:
appropriate reference display/input depending metadata
Respect:
- label
- field key
- required
- options
- display order
- validation rules
- numeric constraints
- photo requirement
- help/placeholder metadata when present
64. PHOTO UX REQUIREMENTS
Support:
- camera capture
- gallery/file selection
- local file persistence
- preview
- pending upload
- successful upload
- retry/failure state
Required photos should be visually obvious.
65. SIGNATURE UX REQUIREMENTS
Provide finger/touch signature capture.
User must be able to:
- draw
- clear
- redraw
- save locally
- queue/upload Signature evidence
Important:
The drawn image is only the visual evidence.
QCSignOff is the actual server-side identity/content-binding electronic-signature record.
66. QCFORM STATUS UX
Clearly show:
- Draft
- InProgress
- Submitted
- ReturnedForCorrection
- Approved
- Cancelled
Behavior:
Draft:
editable
InProgress:
editable
ReturnedForCorrection:
editable
Submitted:
normal fields read-only
Approved:
read-only
Cancelled:
read-only unless explicit business rule says otherwise
67. RETURN-FOR-CORRECTION UX
When ReturnedForCorrection:
- status clearly visible
- reviewer comments/reason shown if available
- editable fields unlocked
- historical signatures remain visible/history-safe
- user can resubmit
- fresh signature required if content changed
68. OFFLINE UX
Show:
- Offline
- Pending sync
- Syncing
- Synced
- Sync failed
- Conflict
Do not claim Synced if pending operations still exist.
69. CONFLICT UX
When server rejects offline operation because state changed:
- preserve local value
- preserve server value
- mark conflict
- tell user central record changed
- allow user to understand which action failed
Do not fake an automatic merge of quality records.
70. TEST BASELINE
At an earlier checkpoint:
19 automated tests were passing.
Known major tested areas:
- FieldValueValidator
- ProjectService
Newer functionality such as authentication, evidence, signatures, lifecycle, audit, NCR, ITP and offline sync needs much broader coverage.
71. REQUIRED BACKEND TEST COVERAGE
Authentication:
- linked active user
- unlinked user
- inactive user
Projects:
- valid client
- missing client
- inactive client
QCForm:
- creation
- snapshot
- required project fields
- required responses
- required photo
- submit
- submitted edit lock
- return for correction
- edit after return
- resubmit
- approval
- invalid transitions
Evidence:
- valid photo
- wrong field
- invalid type
- status restrictions
- uploader
- storage cleanup on DB failure
Signature:
- same form
- same user
- Submitted-only signoff
- hash generation
- stale signature after correction
- fresh signature
NCR:
- auto creation/linking
- duplicate prevention
- close-out
- approval blocking
ITP:
- dependencies
- Hold release
- unauthorised release
Calibration:
- expired instrument rejected
72. REQUIRED OFFLINE TESTS
Must verify:
- data cached
- API goes unavailable
- local edit succeeds
- restart retains edit
- evidence file survives restart
- sync queue survives restart
- reconnect syncs
- transient failure remains queued
- successful operation stops retrying
- idempotency prevents duplicates
- conflict preserves local work
73. REQUIRED END-TO-END QC FLOW
The final solution should demonstrate:
login
 ↓
project
 ↓
start/resume QCForm
 ↓
complete project fields
 ↓
complete dynamic responses
 ↓
capture required photo
 ↓
work offline
 ↓
close/reopen app
 ↓
data still present
 ↓
reconnect
 ↓
sync
 ↓
submit
 ↓
sign
 ↓
return for correction
 ↓
edit
 ↓
resubmit
 ↓
old signature no longer valid for current content
 ↓
fresh signature
 ↓
approve
 ↓
audit history exists
74. REQUIRED END-TO-END NCR FLOW
Fail inspection field
 ↓
NCR created/linked
 ↓
corrective action
 ↓
corrective evidence
 ↓
independent verification
 ↓
close NCR
 ↓
traceability preserved
75. REQUIRED ITP FLOW
Example:
Activity A
 ↓
Activity B depends on A
Verify:
- B cannot progress before A where dependency applies
For Hold point:
Hold reached
 ↓
ordinary unauthorised user cannot release
 ↓
authorised user releases after prerequisites
 ↓
release user/time recorded
 ↓
workflow may proceed
76. CURRENT MAJOR REMAINING WORK
Highest-priority remaining backend work:
1. Verify full QCForm ReturnedForCorrection runtime loop.
2. Implement current-content signoff validation.
3. Require fresh signoff after corrected content.
4. Make approval validate current valid signoff.
5. Inspect AuditEvent schema.
6. Implement core lifecycle audit.
7. Add fine-grained permission foundation.
8. Implement NCR.
9. Implement automatic/link NCR on Fail.
10. Block approval for unresolved blocking NCR.
11. Implement ITP.
12. Implement Hold/Witness/Review enforcement.
Highest-priority mobile/offline work:
13. Stabilise SQLite schema/versioning.
14. Build proper local repositories.
15. Cache projects and QCForms.
16. Local-first response/project-field updates.
17. Persist photo/signature files locally.
18. Persistent SyncOperation queue.
19. Retry handling.
20. Authentication/session handling.
21. Conflict handling.
22. Idempotency.
23. Restart recovery.
24. Login UI.
25. Project/form navigation.
26. Dynamic QC form renderer.
27. Photo capture/upload UX.
28. Signature capture UX.
29. Sync-state UX.
30. ReturnedForCorrection/resubmission UX.
Remaining official modules:
31. Controlled Documents.
32. Calibration.
33. Training/Competency.
34. Reporting/dashboard.
35. PDF export.
36. Excel export.
37. Handover completeness.
38. Handover dossier PDF.
39. Internal Audit.
40. Practical notifications if time permits.
Final engineering work:
41. Expand automated tests.
42. Full API build.
43. Full MAUI build.
44. Runtime end-to-end validation.
45. Dependency-injection review.
46. Async/concurrency review.
47. SQL/transaction review.
48. Security/secrets review.
49. Remove temporary/debug code.
50. Update setup/readme documentation.
51. Run final acceptance checklist.
52. SUGGESTED AGENT EXECUTION ORDER
For Codex/agentic development, use specialised agents sequentially rather than one enormous agent attempting everything at once.
Recommended order:
Agent 1:
Backend API Agent
Responsibilities:
- inspect backend
- establish build/test baseline
- QC lifecycle
- stale-signature fix
- approval validation
- AuditEvent
- permission foundation
- list/resume APIs
- tests
Agent 2:
Offline Sync Agent
Responsibilities:
- inspect MAUI/local DB
- SQLite schema/versioning
- local entities
- local repositories
- filesystem evidence
- SyncOperation
- retries
- conflicts
- idempotency
- offline session
- restart persistence
Agent 3:
MAUI UI Agent
Responsibilities:
- login
- dashboard
- projects
- form library
- start/resume form
- dynamic form renderer
- Pass/Fail/N/A
- photo
- signature
- offline status
- sync UI
- conflict UI
- correction/resubmission UI
Agent 4:
QMS Core Modules Agent
Responsibilities:
- NCR
- NCR approval blocking
- ITP
- Hold/Witness/Review
- Controlled Documents
- Calibration
- Training
Agent 5:
Reporting and Handover Agent
Responsibilities:
- reporting endpoints
- dashboards
- PDF
- Excel
- handover completeness
- dossier
- internal audit
- simple notifications if feasible
Agent 6:
Test/QA Agent
Responsibilities:
- run complete builds
- expand tests
- endpoint verification
- offline testing
- conflict testing
- end-to-end flows
- fix regressions
Agent 7:
Hardening/Release Agent
Responsibilities:
- final build
- final tests
- DI review
- async review
- DB review
- security review
- logging
- exception handling
- startup stability
- remove debug/dead code
- README/setup/demo instructions
- final acceptance checklist
78. IMPORTANT RULES FOR ALL AGENTS
Agents must:
- inspect repository before editing
- build before assuming current state
- run tests
- work from actual schema
- avoid inventing endpoints/tables/enums
- preserve working architecture
- update implementation status
- distinguish implemented from verified
- run relevant acceptance paths
Agents must not:
- rewrite project from scratch
- replace Dapper with EF without a real reason
- let MAUI connect directly to DB
- put service-role key in MAUI
- hard-code the cable form as the only form
- delete historical signatures
- silently overwrite sync conflicts
- silently delete failed operations
- invent final seven-role matrix
- mark code as working solely because it compiles
79. IMPLEMENTATION STATUS CATEGORIES
Every agent should report using:
VERIFIED WORKING
For functionality with actual test/runtime evidence.
IMPLEMENTED BUT NOT VERIFIED
For code written but not fully exercised.
NOT IMPLEMENTED / INCOMPLETE
For remaining required functionality.
BLOCKED
Only for genuine external blockers.
80. CURRENT VERIFIED WORKING SUMMARY
Verified or substantially proven:
- ASP.NET Core API exists/builds
- Dapper/Npgsql architecture
- Supabase authentication
- QMS user mapping
- QmsUser policy
- Clients APIs
- Projects APIs
- Checklist-template retrieval
- Checklist-template-version retrieval
- QCForm creation
- QCForm snapshot generation
- nested QCForm retrieval
- project-field editing
- response editing
- dynamic validation
- photo evidence upload
- Signature evidence upload
- Supabase private storage
- evidence metadata
- storage compensation on metadata failure
- submission validation
- Submitted edit lock
- QCSignOff
- signer identity
- SHA-256 ContentHash
- Submitted → Approved
- ReturnedForCorrection code exists
- ReturnedForCorrection edit/resubmit behavior is implemented in code
81. CURRENT IMPLEMENTED BUT NOT FULLY VERIFIED SUMMARY
    - full ReturnedForCorrection runtime loop
    - returned form edit → resubmit → re-sign → approve
    - stale-signature rejection against changed content
    - approval requiring current signature
82. CURRENT NOT IMPLEMENTED / INCOMPLETE SUMMARY
    - AuditEvent integration
    - configurable fine-grained permissions
    - NCR full workflow
    - auto NCR on Fail
    - NCR approval blocking
    - ITP
    - Hold/Witness/Review
    - Controlled Documents
    - Calibration
    - Training/Competency
    - full reporting
    - PDF/Excel
    - handover dossier
    - Internal Audit
    - production offline SQLite architecture
    - sync queue
    - retry
    - conflicts
    - idempotency
    - full MAUI production workflow
    - broad automated test coverage
83. CURRENT BLOCKERS
No genuine external blocker has been confirmed.
The last Postman photo issue appeared to be client/tooling-related because Evidence upload had already worked successfully on QCForm 8.
84. DEFINITION OF DONE
The project must not be declared complete merely because it compiles.
A final acceptable state should include:
- API builds
- MAUI builds
- automated tests pass
- authentication works
- QMS identity works
- projects work
- templates work
- QC snapshot works
- dynamic forms work
- photos work
- signatures work
- submission works
- correction works
- stale signatures are invalid after changes
- fresh signatures work
- approval works
- audit trail works
- Fail creates/links NCR
- NCR corrective action/verification/closure works
- unresolved NCR blocks approval where required
- ITP works
- Hold enforcement works
- offline work survives app restart
- reconnect synchronises
- retry does not lose data
- conflicts do not silently overwrite data
- no server secret exists in MAUI
- required reports generate
- handover validation works
- no known critical crash exists in the primary demo flow
85. IMMEDIATE PRIORITY
The immediate engineering path should be:
1. Backend agent inspects the actual current repository.
2. Build API.
3. Run all tests.
4. Verify actual QCForm 9/current lifecycle state.
5. Finish stale-signature validation.
6. Require current signoff before approval.
7. Inspect AuditEvent table.
8. Implement audit.
9. Add/verify backend tests.
10. Move to offline/sync agent.
11. Build offline persistence and sync.
12. Move to MAUI UI agent.
13. Build usable mobile field workflow.
14. Implement NCR and ITP.
15. Implement remaining required modules.
16. Run full QA.
17. Run hardening/release pass.
This is the complete current project context, requirements, confirmed implementation state, and remaining work that should be used as the source material for generating the Codex markdown instruction files.
