-- MIGRATION REQUIRES MANUAL APPLICATION. Never run automatically from startup.
-- Additive MZT-QC-203 fields. Existing rows remain unclassified (NULL flags); no backfill/reseed/grants.
BEGIN;
ALTER TABLE public."CableRegister"
 ADD COLUMN IF NOT EXISTS "Revision" bigint NOT NULL DEFAULT 0,
 ADD COLUMN IF NOT EXISTS "RegisterRevision" text,
 ADD COLUMN IF NOT EXISTS "DrawingReference" text,
 ADD COLUMN IF NOT EXISTS "ScheduleReference" text,
 ADD COLUMN IF NOT EXISTS "AreaLocation" text,
 ADD COLUMN IF NOT EXISTS "Notes" text,
 ADD COLUMN IF NOT EXISTS "MaintainedByUserID" uuid REFERENCES public."User"("UserID"),
 ADD COLUMN IF NOT EXISTS "VerifiedByUserID" uuid REFERENCES public."User"("UserID"),
 ADD COLUMN IF NOT EXISTS "VerifiedAt" timestamptz;
ALTER TABLE public."CableRecord"
 ADD COLUMN IF NOT EXISTS "Revision" bigint NOT NULL DEFAULT 0,
 ADD COLUMN IF NOT EXISTS "CableSize" text,
 ADD COLUMN IF NOT EXISTS "Cores" text,
 ADD COLUMN IF NOT EXISTS "DrumNumber" text,
 ADD COLUMN IF NOT EXISTS "Pulled" boolean,
 ADD COLUMN IF NOT EXISTS "Glanded" boolean,
 ADD COLUMN IF NOT EXISTS "Terminated" boolean,
 ADD COLUMN IF NOT EXISTS "IrCertificateNumber" text,
 ADD COLUMN IF NOT EXISTS "ContinuityCertificateNumber" text,
 ADD COLUMN IF NOT EXISTS "Complete" boolean;
COMMIT;
-- Verification (read only):
-- SELECT table_name,column_name,data_type FROM information_schema.columns
-- WHERE table_schema='public' AND table_name IN ('CableRegister','CableRecord') ORDER BY table_name,ordinal_position;
-- SELECT count(*) FROM public."CableRegister"; SELECT count(*) FROM public."CableRecord";
