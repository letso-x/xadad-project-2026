-- MIGRATION REQUIRES MANUAL APPLICATION. No grants, seed data or existing-row changes.
BEGIN;
CREATE TABLE qms_core."ItpPlan" (
 "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "ProjectId" bigint NOT NULL REFERENCES public."Project"("ProjectID"),
 "Number" text NOT NULL CHECK(length(btrim("Number")) BETWEEN 1 AND 100),
 "CurrentVersionId" bigint,
 UNIQUE("Id","ProjectId")
);
CREATE UNIQUE INDEX itp_project_number ON qms_core."ItpPlan"("ProjectId",lower(btrim("Number")));
CREATE TABLE qms_core."ItpVersion" (
 "Id" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "PlanId" bigint NOT NULL REFERENCES qms_core."ItpPlan"("Id"),
 "Revision" integer NOT NULL CHECK("Revision">0),
 "Title" text NOT NULL CHECK(length(btrim("Title")) BETWEEN 1 AND 300),
 "Status" text NOT NULL DEFAULT 'Draft' CHECK("Status" IN ('Draft','Submitted','Approved')),
 "Stamp" bigint NOT NULL DEFAULT 0 CHECK("Stamp">=0),
 "CreatedBy" uuid NOT NULL REFERENCES public."User"("UserID"),"CreatedAt" timestamptz NOT NULL DEFAULT clock_timestamp(),
 "SubmittedBy" uuid REFERENCES public."User"("UserID"),"SubmittedAt" timestamptz,
 "ApprovedBy" uuid REFERENCES public."User"("UserID"),"ApprovedAt" timestamptz,
 UNIQUE("PlanId","Revision"), UNIQUE("Id","PlanId"),
 CHECK(("Status"='Draft' AND "SubmittedAt" IS NULL AND "SubmittedBy" IS NULL AND "ApprovedAt" IS NULL AND "ApprovedBy" IS NULL)
 OR ("Status"='Submitted' AND "SubmittedAt" IS NOT NULL AND "SubmittedBy" IS NOT NULL AND "ApprovedAt" IS NULL AND "ApprovedBy" IS NULL)
 OR ("Status"='Approved' AND "SubmittedAt" IS NOT NULL AND "SubmittedBy" IS NOT NULL AND "ApprovedAt" IS NOT NULL AND "ApprovedBy" IS NOT NULL))
);
ALTER TABLE qms_core."ItpPlan" ADD CONSTRAINT itp_current_same_plan FOREIGN KEY("CurrentVersionId","Id") REFERENCES qms_core."ItpVersion"("Id","PlanId");
CREATE UNIQUE INDEX itp_one_pending_revision ON qms_core."ItpVersion"("PlanId") WHERE "Status"<>'Approved';
CREATE TABLE qms_core."ItpActivity" (
 "VersionId" bigint NOT NULL REFERENCES qms_core."ItpVersion"("Id"),"Id" bigint NOT NULL CHECK("Id">0),
 "Sequence" integer NOT NULL CHECK("Sequence">0),"Code" text NOT NULL,"Title" text NOT NULL,"Description" text NOT NULL,"Criteria" text NOT NULL,
 "PointType" text NOT NULL CHECK("PointType" IN ('None','Hold','Witness','Review')),
 "ChecklistTemplateVersionId" bigint REFERENCES public."ChecklistTemplateVersion"("ChecklistTemplateVersionID"),
 "RequireQc" boolean NOT NULL,"WitnessRequired" boolean NOT NULL,"IndependentHoldRelease" boolean NOT NULL,
 PRIMARY KEY("VersionId","Id"), UNIQUE("VersionId","Sequence"),UNIQUE("VersionId","Code"),
 CHECK(NOT "RequireQc" OR "ChecklistTemplateVersionId" IS NOT NULL),
 CHECK(length(btrim("Code")) BETWEEN 1 AND 100 AND length(btrim("Title")) BETWEEN 1 AND 300 AND length("Description")<=4000 AND length("Criteria")<=4000)
);
CREATE TABLE qms_core."ItpDependency" (
 "VersionId" bigint NOT NULL,"PredecessorId" bigint NOT NULL,"SuccessorId" bigint NOT NULL,
 PRIMARY KEY("VersionId","PredecessorId","SuccessorId"),CHECK("PredecessorId"<>"SuccessorId"),
 FOREIGN KEY("VersionId","PredecessorId") REFERENCES qms_core."ItpActivity"("VersionId","Id"),
 FOREIGN KEY("VersionId","SuccessorId") REFERENCES qms_core."ItpActivity"("VersionId","Id")
);
CREATE TABLE qms_core."ItpExecution" (
 "VersionId" bigint NOT NULL,"ActivityId" bigint NOT NULL,
 "CompletedBy" uuid REFERENCES public."User"("UserID"),"CompletedAt" timestamptz,"CompletionComments" text,
 "ReleasedBy" uuid REFERENCES public."User"("UserID"),"ReleasedAt" timestamptz,"ReleaseComments" text,
 "WitnessedBy" uuid REFERENCES public."User"("UserID"),"WitnessedAt" timestamptz,"WitnessComments" text,
 "ReviewedBy" uuid REFERENCES public."User"("UserID"),"ReviewedAt" timestamptz,"ReviewComments" text,
 "QcFormId" bigint REFERENCES public."QCForm"("QCFormID"),
 PRIMARY KEY("VersionId","ActivityId"), FOREIGN KEY("VersionId","ActivityId") REFERENCES qms_core."ItpActivity"("VersionId","Id")
);
CREATE FUNCTION qms_core.itp_definition_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE v bigint;
BEGIN
 v:=CASE WHEN TG_OP='DELETE' THEN OLD."VersionId" ELSE NEW."VersionId" END;
 IF (SELECT "Status" FROM qms_core."ItpVersion" WHERE "Id"=v)<>'Draft' THEN RAISE EXCEPTION 'Submitted and approved ITP definitions are immutable'; END IF;
 IF TG_OP='UPDATE' AND NEW."VersionId"<>OLD."VersionId" THEN RAISE EXCEPTION 'ITP version cannot be moved'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER itp_activity_definition_guard BEFORE INSERT OR UPDATE OR DELETE ON qms_core."ItpActivity" FOR EACH ROW EXECUTE FUNCTION qms_core.itp_definition_guard();
CREATE TRIGGER itp_dependency_definition_guard BEFORE INSERT OR UPDATE OR DELETE ON qms_core."ItpDependency" FOR EACH ROW EXECUTE FUNCTION qms_core.itp_definition_guard();
CREATE FUNCTION qms_core.itp_version_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'ITP revision history cannot be deleted'; END IF;
 IF NEW."PlanId"<>OLD."PlanId" OR NEW."Revision"<>OLD."Revision" OR NEW."CreatedBy"<>OLD."CreatedBy" OR NEW."CreatedAt"<>OLD."CreatedAt" THEN RAISE EXCEPTION 'ITP revision identity is immutable'; END IF;
 IF OLD."Status"='Approved' AND (to_jsonb(NEW)-'Stamp')<>(to_jsonb(OLD)-'Stamp') THEN RAISE EXCEPTION 'Approved ITP revision is immutable'; END IF;
 IF OLD."Status"='Submitted' AND (NEW."Title"<>OLD."Title" OR NEW."Status" NOT IN ('Submitted','Approved') OR NEW."SubmittedBy" IS DISTINCT FROM OLD."SubmittedBy" OR NEW."SubmittedAt" IS DISTINCT FROM OLD."SubmittedAt") THEN RAISE EXCEPTION 'Submitted ITP definition is immutable'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER itp_version_guard BEFORE UPDATE OR DELETE ON qms_core."ItpVersion" FOR EACH ROW EXECUTE FUNCTION qms_core.itp_version_guard();
CREATE FUNCTION qms_core.itp_execution_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE v bigint; state text; current_id bigint;
BEGIN
 v:=CASE WHEN TG_OP='DELETE' THEN OLD."VersionId" ELSE NEW."VersionId" END;
 SELECT x."Status",p."CurrentVersionId" INTO state,current_id FROM qms_core."ItpVersion" x JOIN qms_core."ItpPlan" p ON p."Id"=x."PlanId" WHERE x."Id"=v;
 IF state='Draft' THEN
  IF TG_OP<>'DELETE' AND (NEW."CompletedAt" IS NOT NULL OR NEW."ReleasedAt" IS NOT NULL OR NEW."WitnessedAt" IS NOT NULL OR NEW."ReviewedAt" IS NOT NULL OR NEW."QcFormId" IS NOT NULL) THEN RAISE EXCEPTION 'Draft ITP cannot be executed'; END IF;
 ELSE
  IF state<>'Approved' OR current_id IS DISTINCT FROM v OR TG_OP<>'UPDATE' THEN RAISE EXCEPTION 'Historical or submitted ITP execution is immutable'; END IF;
  IF NEW."VersionId"<>OLD."VersionId" OR NEW."ActivityId"<>OLD."ActivityId" THEN RAISE EXCEPTION 'Execution identity is immutable'; END IF;
  IF OLD."CompletedAt" IS NOT NULL AND (NEW."CompletedAt" IS DISTINCT FROM OLD."CompletedAt" OR NEW."CompletedBy" IS DISTINCT FROM OLD."CompletedBy" OR NEW."CompletionComments" IS DISTINCT FROM OLD."CompletionComments" OR NEW."QcFormId" IS DISTINCT FROM OLD."QcFormId") THEN RAISE EXCEPTION 'Completion history is immutable'; END IF;
  IF OLD."ReleasedAt" IS NOT NULL AND (NEW."ReleasedAt" IS DISTINCT FROM OLD."ReleasedAt" OR NEW."ReleasedBy" IS DISTINCT FROM OLD."ReleasedBy" OR NEW."ReleaseComments" IS DISTINCT FROM OLD."ReleaseComments") THEN RAISE EXCEPTION 'Release history is immutable'; END IF;
  IF OLD."WitnessedAt" IS NOT NULL AND (NEW."WitnessedAt" IS DISTINCT FROM OLD."WitnessedAt" OR NEW."WitnessedBy" IS DISTINCT FROM OLD."WitnessedBy" OR NEW."WitnessComments" IS DISTINCT FROM OLD."WitnessComments") THEN RAISE EXCEPTION 'Witness history is immutable'; END IF;
  IF OLD."ReviewedAt" IS NOT NULL AND (NEW."ReviewedAt" IS DISTINCT FROM OLD."ReviewedAt" OR NEW."ReviewedBy" IS DISTINCT FROM OLD."ReviewedBy" OR NEW."ReviewComments" IS DISTINCT FROM OLD."ReviewComments") THEN RAISE EXCEPTION 'Review history is immutable'; END IF;
 END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; ELSE RETURN NEW; END IF;
END $$;
CREATE TRIGGER itp_execution_guard BEFORE INSERT OR UPDATE OR DELETE ON qms_core."ItpExecution" FOR EACH ROW EXECUTE FUNCTION qms_core.itp_execution_guard();
CREATE FUNCTION qms_core.itp_plan_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'ITP history cannot be deleted'; END IF;
 IF NEW."ProjectId"<>OLD."ProjectId" OR NEW."Number"<>OLD."Number" THEN RAISE EXCEPTION 'ITP project and number are immutable'; END IF;
 IF NEW."CurrentVersionId" IS DISTINCT FROM OLD."CurrentVersionId" THEN
  IF NOT EXISTS(SELECT 1 FROM qms_core."ItpVersion" WHERE "Id"=NEW."CurrentVersionId" AND "PlanId"=NEW."Id" AND "Status"='Approved') THEN RAISE EXCEPTION 'Current ITP must be an approved revision of this plan'; END IF;
  IF OLD."CurrentVersionId" IS NOT NULL AND (SELECT "Revision" FROM qms_core."ItpVersion" WHERE "Id"=NEW."CurrentVersionId")<=(SELECT "Revision" FROM qms_core."ItpVersion" WHERE "Id"=OLD."CurrentVersionId") THEN RAISE EXCEPTION 'Current ITP must advance to a newer revision'; END IF;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER itp_plan_guard BEFORE UPDATE OR DELETE ON qms_core."ItpPlan" FOR EACH ROW EXECUTE FUNCTION qms_core.itp_plan_guard();
REVOKE ALL ON qms_core."ItpPlan",qms_core."ItpVersion",qms_core."ItpActivity",qms_core."ItpDependency",qms_core."ItpExecution" FROM PUBLIC;
COMMIT;
