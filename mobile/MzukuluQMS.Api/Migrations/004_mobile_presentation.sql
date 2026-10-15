-- Additive candidate: reconcile against the actual schema and apply only to an authorised test database first.
BEGIN;
CREATE TABLE qms_core."QCFormPresentation" (
 "QCFormID" bigint PRIMARY KEY REFERENCES public."QCForm"("QCFormID"),
 "Header" jsonb NOT NULL,
 "Remarks" text NOT NULL DEFAULT '' CHECK(length("Remarks")<=10000)
);
CREATE FUNCTION qms_core.keep_execution_header() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW."Header" IS DISTINCT FROM OLD."Header" OR NEW."QCFormID"<>OLD."QCFormID" THEN
  RAISE EXCEPTION 'Execution presentation header is immutable';
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER qc_presentation_header_immutable BEFORE UPDATE ON qms_core."QCFormPresentation"
 FOR EACH ROW EXECUTE FUNCTION qms_core.keep_execution_header();
-- No historical backfill from current metadata. Missing historical headers remain explicitly unknown.
COMMIT;
