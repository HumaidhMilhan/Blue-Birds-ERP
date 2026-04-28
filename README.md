# Blue Birds ERP

Blue Birds ERP is a native Windows desktop ERP for a pre-processed poultry retail and wholesale shop. The product-facing name is **PoultryPro ERP**.

The scaffold follows the SRS in `docs/PoultryPro_SRS_v1.0.pdf` and is organized for POS billing, batch inventory, supplier GRNs, credit accounts, returns, wastage, Twilio WhatsApp notifications, RBAC, audit logging, and partial offline POS operation.

## Technology

- .NET 8
- WPF desktop UI
- MVVM-style presentation layer
- PostgreSQL as the default central database
- SQLite for local offline POS storage
- EF Core persistence placeholders

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

This is the initial project structure. It intentionally includes interfaces and placeholders before full feature implementation so the project can grow module by module without mixing UI, business rules, and infrastructure concerns.
