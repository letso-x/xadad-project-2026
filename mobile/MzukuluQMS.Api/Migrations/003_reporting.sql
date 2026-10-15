BEGIN;
CREATE TABLE qms_core."HandoverRequirement" (
 "ProjectID" bigint NOT NULL REFERENCES public."Project"("ProjectID"),
 "ChecklistTemplateVersionID" bigint NOT NULL REFERENCES public."ChecklistTemplateVersion"("ChecklistTemplateVersionID"),
 "RequiredCount" integer NOT NULL CHECK ("RequiredCount">0),
 PRIMARY KEY ("ProjectID","ChecklistTemplateVersionID")
);
CREATE TABLE qms_core."Notification" (
 "NotificationID" bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 "UserID" uuid NOT NULL REFERENCES public."User"("UserID"),
 "QCFormID" bigint NOT NULL REFERENCES public."QCForm"("QCFormID"),
 "FormRevision" bigint NOT NULL,"Kind" text NOT NULL,"CreatedAt" timestamptz NOT NULL DEFAULT clock_timestamp(),"ReadAt" timestamptz,
 UNIQUE("UserID","QCFormID","FormRevision","Kind")
);
COMMIT;
