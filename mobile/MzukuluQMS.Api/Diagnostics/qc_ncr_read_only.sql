-- Inspection only. Run with a read-only role; export metadata, never secrets.
-- This is NOT a migration and must not be included in a migration runner.
BEGIN TRANSACTION READ ONLY;
SELECT table_schema,table_name,column_name,data_type,udt_name
FROM information_schema.columns
WHERE table_schema IN ('public','qms_core','qms_security')
ORDER BY table_schema,table_name,ordinal_position;

SELECT n.nspname AS schema_name,t.typname,e.enumlabel
FROM pg_enum e JOIN pg_type t ON t.oid=e.enumtypid JOIN pg_namespace n ON n.oid=t.typnamespace
WHERE n.nspname IN ('public','qms_core','qms_security') ORDER BY n.nspname,t.typname,e.enumsortorder;

SELECT schemaname,tablename,indexname,indexdef FROM pg_indexes
WHERE schemaname IN ('public','qms_core','qms_security')
AND tablename IN ('QCForm','QCResponse','Ncr','NCR','AuditEvent','Evidence','Operation')
ORDER BY schemaname,tablename,indexname;

SELECT n.nspname AS schema_name,c.relname AS table_name,t.tgname,
 pg_get_triggerdef(t.oid) AS trigger_definition,pg_get_functiondef(t.tgfoid) AS function_definition
FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname IN ('public','qms_core','qms_security')
AND c.relname IN ('QCForm','QCResponse','Ncr','NCR','AuditEvent','Evidence')
ORDER BY n.nspname,c.relname,t.tgname;
ROLLBACK;
