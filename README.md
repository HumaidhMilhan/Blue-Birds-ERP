# Blue Birds ERP

Blue Birds ERP is a native Windows desktop ERP for a pre-processed poultry retail and wholesale shop. The product-facing name is **PoultryPro ERP**.

The scaffold follows the SRS in `docs/PoultryPro_SRS_v1.0.pdf` and is organized for POS billing, manual batch inventory purchases, credit accounts, returns, wastage, queued WhatsApp notifications, RBAC, audit logging, and partial offline POS operation.

## Technology

- .NET 8
- WPF desktop UI
- MVVM-style presentation layer
- SQLite as the primary offline-first MVP database
- PostgreSQL as an optional online sync target
- EF Core persistence and queued online-process storage

## Solution Layout

- `src/BlueBirdsERP.Desktop` - WPF shell, navigation, POS/admin screen placeholders
- `src/BlueBirdsERP.Domain` - SRS entities, enums, and core business rules
- `src/BlueBirdsERP.Application` - service contracts, DTOs, and use-case boundaries
- `src/BlueBirdsERP.Infrastructure` - EF Core contexts and adapter placeholders for database, Twilio, printing, encrypted config, and sync
- `tests/BlueBirdsERP.Tests` - unit tests for high-risk business rules
- `config` - checked-in example config only
- `database` - migration and seed placeholders
- `scripts` - developer helper placeholders

## Commands

```powershell
dotnet restore BlueBirdsERP.sln
dotnet build BlueBirdsERP.sln
dotnet test BlueBirdsERP.sln
dotnet run --project src/BlueBirdsERP.Desktop/BlueBirdsERP.Desktop.csproj
```

## Current Status

Implemented slices now cover POS billing/returns, customer credit accounts, manual batch inventory, queued WhatsApp notification rules, RBAC/authentication, SQLite-backed persistence, Admin settings, recovery access, receipt PDF generation, offline queueing, database management, and operational reporting. UI/UX screens are still pending.
