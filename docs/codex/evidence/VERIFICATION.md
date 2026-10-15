# Candidate verification — 12 October 2026

Base ea90e96 on back-end-first-revamp plus uncommitted working tree. Windows 10, .NET SDK 10.0.401. No production/test database was configured or changed. Historical forms/signoffs were not accessed or mutated.

## Measured commands

| Command | Result |
|---|---|
| dotnet test mobile/MzukuluQMS.Api.Tests/MzukuluQMS.Api.Tests.csproj --no-restore | Baseline 19 passed; expanded 76 passed, 0 failed, 0 skipped |
| dotnet test mobile/MzukuluQMS.Mobile.Core.Tests/MzukuluQMS.Mobile.Core.Tests.csproj --no-restore | 11 passed, 0 failed, 0 skipped |
| dotnet build mobile/MzukuluQMSMobile/MzukuluQMSMobile.csproj -f net10.0-android --no-restore -t:Compile | Updated source compile succeeded, 0 warnings/errors; not APK/device verification |
| dotnet build mobile/MzukuluQMSMobile/MzukuluQMSMobile.csproj -f net10.0-android --no-restore | Baseline before UI changes succeeded, 0 warnings/errors, 15m47s |
| dotnet run --project tools/SchemaProbe/SchemaProbe.csproj --no-restore | BLOCKED: QmsDatabase not configured in User Secrets/environment |
| where.exe psql / where.exe adb | Neither executable found on PATH; no DB/device runtime check performed |
| git diff --check | Passed after removing introduced trailing whitespace; CRLF conversion warnings only |

Final build and latest rerun outcomes are appended in IMPLEMENTATION_STATUS.md. TRX evidence: [backend final](backend-final.trx), [offline final](offline-final.trx). Preserved failures: [early auth configuration](backend-startup-failed.trx), [synchronous test scope disposal](backend-scope-disposal-failed.trx). These failures were corrected and rerun, not deleted from the history.

## Evidence boundaries

The 76 API tests include existing project/validator tests, lifecycle/signing orchestration mocks, canonical hashing, mapping of active/inactive identities, NCR/ITP/calibration domain rules, fingerprint binding, real TestServer anonymous 401 responses and service graph resolution. They do not exercise PostgreSQL constraints, transactional audit/rollback, race behavior, Supabase JWT verification with real tokens, storage or the end-to-end correction loop.

The 11 offline tests use actual Windows SQLite files and AES-GCM evidence payloads, exercise reopen/Processing recovery, retained dirty values/conflicts, dependency ordering, acknowledgement and mocked transport status/account handling. Reopening a store in the same test process is not a forced mobile process-kill/device-restart or upgrade test. No SQLCipher claim: direct header inspection of a synthetic DB reads SQLite format 3\0, so the encrypted database requirement is unmet.

No all-project acceptance case or release gate is promoted to PASS from unit/build results alone.

### Final recheck and encryption dependency boundary — 12 October 2026

- Repeated `dotnet build mobile/MzukuluQMS.Api/MzukuluQMS.Api.csproj --no-restore`: succeeded, 0 warnings, 0 errors, 10.09 seconds. `git diff --check`: exit 0 (line-ending notices only).
- Investigated supported SQLite encryption packages before changing the working provider. The SQLitePCLRaw maintainer states that the free encryption-enabled bundle is deprecated in v3 and recommends licensed encryption builds: [maintainer migration notes](https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md). [NuGet bundle page](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher/) lists 2.1.11. No obsolete provider substitution, purchase, key rotation or local database conversion was performed. Full database encryption remains INCOMPLETE and needs a supported native provider, platform validation and non-destructive conversion/recovery design; this does not block independent API work.
