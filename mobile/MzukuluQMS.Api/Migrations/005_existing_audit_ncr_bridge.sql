-- Candidate only. Supplied schema is reference, not proof of the live database. Apply to a backed-up disposable copy first.
BEGIN;
-- Do not assume an undocumented enum label: stop migration unless this mapping can be represented.
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_enum e JOIN pg_attribute a ON a.atttypid=e.enumtypid
   WHERE a.attrelid='public."NCR"'::regclass AND a.attname='Status' AND e.enumlabel='Closed') THEN
  RAISE EXCEPTION 'NCR Closed enum mapping is unavailable; reconcile actual ncr_status before migration';
 END IF;
END $$;
LOCK TABLE qms_security."AuditEvent" IN SHARE ROW EXCLUSIVE MODE;
ALTER TABLE public."AuditEvent" ADD COLUMN "CoreAuditEventID" bigint UNIQUE REFERENCES qms_security."AuditEvent"("AuditEventID");
INSERT INTO public."AuditEvent" ("UserID","EntityType","EntityID","Action","NewValues","CreatedAt","CoreAuditEventID")
 SELECT "ActorUserID","EntityType","EntityID"::text,"Action","Details","OccurredAt","AuditEventID" FROM qms_security."AuditEvent";
CREATE FUNCTION qms_security.mirror_business_audit() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 INSERT INTO public."AuditEvent" ("UserID","EntityType","EntityID","Action","NewValues","CreatedAt","CoreAuditEventID")
 VALUES(NEW."ActorUserID",NEW."EntityType",NEW."EntityID"::text,NEW."Action",NEW."Details",NEW."OccurredAt",NEW."AuditEventID");
 RETURN NEW;
END $$;
CREATE TRIGGER mirror_business_audit AFTER INSERT ON qms_security."AuditEvent" FOR EACH ROW EXECUTE FUNCTION qms_security.mirror_business_audit();
CREATE TRIGGER public_audit_append_only BEFORE UPDATE OR DELETE ON public."AuditEvent" FOR EACH ROW EXECUTE FUNCTION qms_security.reject_history_change();
ALTER TABLE qms_core."Ncr" ADD COLUMN "LegacyNCRID" bigint UNIQUE REFERENCES public."NCR"("NCRID");
CREATE FUNCTION qms_core.link_public_ncr() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE legacy_id bigint;
BEGIN
 INSERT INTO public."NCR" ("QCFormID","NCRNumber","Description","Severity","CorrectiveAction","RaisedByUserID","RaisedAt","ClosedAt","Status")
 VALUES(NEW."QCFormID",'QMS-'||gen_random_uuid()::text,'Inspection failure at response '||NEW."QCResponseID"::text||', field '||NEW."QCFormFieldID"::text,
   NEW."Severity",NEW."CorrectiveAction",NEW."CreatedByUserID",NEW."CreatedAt",NEW."ClosedAt",
   CASE WHEN NEW."ClosedAt" IS NULL THEN 'Open'::public.ncr_status ELSE 'Closed'::public.ncr_status END)
 RETURNING "NCRID" INTO legacy_id;
 UPDATE qms_core."Ncr" SET "LegacyNCRID"=legacy_id WHERE "NcrID"=NEW."NcrID";
 RETURN NEW;
END $$;
-- AFTER INSERT is essential: a deduplicated ON CONFLICT DO NOTHING must not create an orphan public NCR.
CREATE TRIGGER link_public_ncr AFTER INSERT ON qms_core."Ncr" FOR EACH ROW EXECUTE FUNCTION qms_core.link_public_ncr();
CREATE FUNCTION qms_core.sync_public_ncr() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 UPDATE public."NCR" SET "Severity"=NEW."Severity","CorrectiveAction"=NEW."CorrectiveAction","ClosedAt"=NEW."ClosedAt",
  "ResolvedAt"=CASE WHEN NEW."VerificationPassed" THEN NEW."VerifiedAt" ELSE NULL END,
  "Status"=CASE WHEN NEW."ClosedAt" IS NULL THEN 'Open'::public.ncr_status ELSE 'Closed'::public.ncr_status END
 WHERE "NCRID"=NEW."LegacyNCRID";
 RETURN NEW;
END $$;
CREATE TRIGGER sync_public_ncr AFTER UPDATE OF "Severity","CorrectiveAction","ClosedAt","VerificationPassed" ON qms_core."Ncr"
 FOR EACH ROW EXECUTE FUNCTION qms_core.sync_public_ncr();
-- Link any existing core cases without changing or guessing the source of old public NCRs.
DO $$ DECLARE item qms_core."Ncr"%ROWTYPE; legacy_id bigint;
BEGIN
 FOR item IN SELECT * FROM qms_core."Ncr" WHERE "LegacyNCRID" IS NULL LOOP
  INSERT INTO public."NCR" ("QCFormID","NCRNumber","Description","Severity","CorrectiveAction","RaisedByUserID","RaisedAt","ClosedAt","Status")
  VALUES(item."QCFormID",'QMS-'||gen_random_uuid()::text,'Inspection failure at response '||item."QCResponseID"::text||', field '||item."QCFormFieldID"::text,
    item."Severity",item."CorrectiveAction",item."CreatedByUserID",item."CreatedAt",item."ClosedAt",
    CASE WHEN item."ClosedAt" IS NULL THEN 'Open'::public.ncr_status ELSE 'Closed'::public.ncr_status END) RETURNING "NCRID" INTO legacy_id;
  UPDATE qms_core."Ncr" SET "LegacyNCRID"=legacy_id WHERE "NcrID"=item."NcrID";
 END LOOP;
END $$;
COMMIT;
