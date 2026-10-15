-- Explicit new core schema; reconcile existing legacy module tables before deployment.
BEGIN;
CREATE SCHEMA IF NOT EXISTS qms_core;
REVOKE ALL ON SCHEMA qms_core FROM PUBLIC;
CREATE TABLE qms_core."NcrPolicy" (
 "ChecklistTemplateVersionID" bigint PRIMARY KEY REFERENCES public."ChecklistTemplateVersion"("ChecklistTemplateVersionID"),
 "DefaultBlocking" boolean NOT NULL
);
CREATE TABLE qms_core."Ncr" (
 "NcrID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "ProjectID" bigint NOT NULL REFERENCES public."Project"("ProjectID"),
 "QCFormID" bigint NOT NULL REFERENCES public."QCForm"("QCFormID"),
 "QCResponseID" bigint NOT NULL REFERENCES public."QCResponse"("QCResponseID"),
 "QCFormFieldID" bigint NOT NULL REFERENCES public."QCFormField"("QCFormFieldID"),
 "CreatedByUserID" uuid NOT NULL REFERENCES public."User"("UserID"),
 "CreatedAt" timestamptz NOT NULL DEFAULT clock_timestamp(),
 "Severity" text NOT NULL DEFAULT 'Unclassified', "Classification" text NOT NULL DEFAULT 'Unclassified',
 "IsBlocking" boolean, "Disposition" text, "CorrectiveAction" text,
 "CorrectedByUserID" uuid REFERENCES public."User"("UserID"),
 "CorrectiveEvidenceID" bigint REFERENCES public."Evidence"("EvidenceID"),
 "VerifierUserID" uuid REFERENCES public."User"("UserID"), "VerifiedAt" timestamptz,
 "VerificationPassed" boolean, "VerificationComments" text,
 "ClosedAt" timestamptz, "Revision" bigint NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX ncr_one_open_source ON qms_core."Ncr"("QCResponseID") WHERE "ClosedAt" IS NULL;
CREATE INDEX ON qms_core."Ncr"("QCFormID") WHERE "ClosedAt" IS NULL;
COMMIT;
