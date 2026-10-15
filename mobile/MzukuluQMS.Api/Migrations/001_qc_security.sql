-- Additive migration. Test on a disposable copy before application to any existing database.
-- No default grants; deployment owner must explicitly configure the business policy.
BEGIN;
ALTER TABLE public."QCForm" ADD COLUMN "Revision" bigint NOT NULL DEFAULT 0;
CREATE SCHEMA IF NOT EXISTS qms_security;
REVOKE ALL ON SCHEMA qms_security FROM PUBLIC;
CREATE TABLE qms_security."RolePermission" (
 "RoleName" text NOT NULL, "Permission" text NOT NULL,
 PRIMARY KEY ("RoleName","Permission")
);
CREATE TABLE qms_security."ProjectAccess" (
 "UserID" uuid NOT NULL REFERENCES public."User"("UserID"),
 "ProjectID" bigint NOT NULL REFERENCES public."Project"("ProjectID"),
 PRIMARY KEY ("UserID","ProjectID")
);
CREATE TABLE qms_security."TemplateSignatureRequirement" (
 "ChecklistTemplateVersionID" bigint NOT NULL REFERENCES public."ChecklistTemplateVersion"("ChecklistTemplateVersionID"),
 "SignOffType" text NOT NULL CHECK ("SignOffType" IN ('Inspector','Technician','Supervisor','Client')),
 PRIMARY KEY ("ChecklistTemplateVersionID","SignOffType")
);
CREATE TABLE qms_security."FormSignatureRequirement" (
 "QCFormID" bigint NOT NULL REFERENCES public."QCForm"("QCFormID"),
 "SignOffType" text NOT NULL CHECK ("SignOffType" IN ('Inspector','Technician','Supervisor','Client')),
 PRIMARY KEY ("QCFormID","SignOffType")
);
-- Legacy rows remain version 1 with their original hashes. New writes explicitly use 2.
ALTER TABLE public."QCSignOff" ADD COLUMN "HashVersion" integer NOT NULL DEFAULT 1 CHECK ("HashVersion" IN (1,2));
-- Separate additive audit stream because public.AuditEvent metadata is unavailable locally.
-- Reconcile/read both streams when the existing schema becomes available; never replace it.
CREATE TABLE qms_security."AuditEvent" (
 "AuditEventID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "ActorUserID" uuid NOT NULL REFERENCES public."User"("UserID"),
 "Action" text NOT NULL, "EntityType" text NOT NULL, "EntityID" bigint NOT NULL,
 "OccurredAt" timestamptz NOT NULL DEFAULT clock_timestamp(), "Details" jsonb NOT NULL
);
CREATE TABLE qms_security."Operation" (
 "UserID" uuid NOT NULL REFERENCES public."User"("UserID"), "OperationID" uuid NOT NULL,
 "Fingerprint" text NOT NULL, "StatusCode" integer, "ResultJson" text,
 "Revision" bigint NOT NULL DEFAULT 0, "QCFormID" bigint NOT NULL DEFAULT 0,
 PRIMARY KEY ("UserID","OperationID")
);
CREATE INDEX ON qms_security."AuditEvent" ("EntityType","EntityID","AuditEventID");
CREATE FUNCTION qms_security.reject_history_change() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Audit/signature history is append only'; END $$;
CREATE TRIGGER audit_append_only BEFORE UPDATE OR DELETE ON qms_security."AuditEvent"
 FOR EACH ROW EXECUTE FUNCTION qms_security.reject_history_change();
CREATE TRIGGER signoff_append_only BEFORE UPDATE OR DELETE ON public."QCSignOff"
 FOR EACH ROW EXECUTE FUNCTION qms_security.reject_history_change();
COMMIT;
