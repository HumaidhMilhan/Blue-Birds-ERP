# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Blue Birds ERP (product name: **PoultryPro ERP**) — a .NET 8 desktop ERP for pre-processed poultry retail/wholesale shops. WPF frontend with MVVM, offline-first with SQLite. SRS lives in `docs/PoultryPro_SRS_v1.0.pdf`.

## Build & Run Commands

```bash
dotnet build BlueBirdsERP.sln
dotnet test BlueBirdsERP.sln

# Run the desktop app
dotnet run --project src/BlueBirdsERP.Desktop/BlueBirdsERP.Desktop.csproj

# Run a single test class
dotnet test tests/BlueBirdsERP.Tests/ --filter "FullyQualifiedName~ClassName"
```

## Architecture

Clean Architecture with four layers:

```
Desktop (WPF) → Infrastructure → Application → Domain
```

- **Domain** (`src/BlueBirdsERP.Domain/`): 17 entities, enums, and static business rules. Pure C#, no external dependencies.
- **Application** (`src/BlueBirdsERP.Application/`): Service interfaces, DTOs (records), and use-case implementations. Depends only on Domain.
- **Infrastructure** (`src/BlueBirdsERP.Infrastructure/`): EF Core DbContext, Twilio WhatsApp stub, PDF generation, BCrypt security, sync queue. Depends on Application and Domain.
- **Desktop** (`src/BlueBirdsERP.Desktop/`): WPF shell, views, ViewModels. Uses CommunityToolkit.Mvvm, Microsoft.Extensions.Hosting. Depends on Application and Infrastructure.

## Desktop / WPF Specifics

- **MVVM framework:** CommunityToolkit.Mvvm (source-generator-based `ObservableObject`, `RelayCommand`, `[ObservableProperty]`)
- **DI:** `Microsoft.Extensions.Hosting` — `App.xaml.cs` builds a host, registers all services and ViewModels via `services.AddInfrastructure()`
- **Navigation:** `INavigationService` + `NavigationService` — ViewModels resolve from DI, `MainViewModel.CurrentView` drives `ContentControl` binding
- **Theme:** Modern flat / Material-inspired. Colors in `Resources/Colors.xaml`, styles in `Resources/Styles.xaml`
- **Sidebar:** Role-based — `MainViewModel.BuildNavigationItems()` filters nav items by `RbacPermission`
- **Session:** `DispatcherTimer` checks `ISessionService.InactivityTimeout` every minute, auto-logs out after 15 min inactivity
- **Dialogs:** Modal `Window` subclasses for batch picker, product picker, price input, add product, record purchase
- **PasswordBox:** Cannot be data-bound in WPF — code-behind passes password to ViewModel command parameter
- **`Application` namespace conflict:** `BlueBirdsERP.Application` namespace conflicts with `System.Windows.Application` — use fully qualified `System.Windows.Application` in `App.xaml.cs`

## Data Access Pattern

Uses **interface segregation** instead of a generic repository. Five data store interfaces in Application, all implemented by a single `EfCoreDataStore` class (Scoped):

- `IPOSDataStore` — invoices, payments, returns, wastage, batch deduction
- `IInventoryDataStore` — products, categories, batches, stock levels, alerts
- `ICustomerAccountDataStore` — customers, business accounts, payment history
- `INotificationDataStore` — notifications, templates
- `ISecurityDataStore` — users, audit logs, system settings

DI wiring is in `Infrastructure/DependencyInjection.cs` via `AddInfrastructure()` extension method.

## Database Specifics

- **Primary DB:** SQLite (`bluebirds-mvp.sqlite3`), offline-first MVP
- **Dual DbContext:** `PoultryProDbContext` (full schema, 17 entities) and `LocalPosDbContext` (minimal POS-only subset)
- **Code-first:** Uses `EnsureCreatedAsync()` — no EF Core migration files exist yet
- **DateTimeOffset stored as `long`** (Unix milliseconds) via custom `ValueConverter` for SQLite compatibility — always use `DateTimeOffset` in C# code, never raw `long`
- **Decimal precision:** Money fields use `(10, 2)` or `(12, 2)` in Fluent API — always configure precision for new decimal columns

## Authentication & Authorization

- BCrypt password hashing (`BCrypt.Net-Next`)
- **Two roles:** `Admin`, `Cashier`
- **13 permissions** defined in the `RbacPermission` enum: `PosBilling`, `PaymentRecording`, `SalesReturns`, `CustomerReadOnlyLookup`, `InventoryManagement`, `BatchManagement`, `CustomerAccountManagement`, `CreditManagement`, `WhatsAppConfiguration`, `Reporting`, `UserManagement`, `SystemConfiguration`, `AuditLogRead`
- `RbacAuthorizationService` maps roles to permission sets
- In-memory session tracking with 15-min inactivity timeout (`InMemorySessionService`)
- Break-glass recovery via SHA-256 hashed recovery key

## Key Conventions

- **`ISystemClock`** abstraction for testable time — never use `DateTimeOffset.UtcNow` directly in business logic
- **`ITransactionRunner`** for explicit transaction boundaries in services
- **`IAuditLogger`** — every security-sensitive operation writes to `AuditLog` with before/after JSON snapshots
- **C# latest** language version, nullable reference types enabled globally (`Directory.Build.props`)
- Invoice numbers follow format `INV-YYYYMMDD[R|W]-NNNN` (channel + daily sequence), generated by `PoultryBusinessRules.CreateInvoiceNumber`

## Testing

Tests use xUnit with coverlet for coverage. All test files are in `tests/BlueBirdsERP.Tests/Domain/`:
- `POSCheckoutServiceTests` — most comprehensive, covers full checkout/return/void flows
- `PoultryBusinessRulesTests` — payment validation, stock deduction, credit limits
- `SecurityServiceTests` — auth, user management, recovery
- `CustomerAccountServiceTests` — business accounts, credit, payments
- `InventoryServiceTests` — products, batches, wastage, stock levels
- `NotificationServiceTests` — queuing, reminders, templates
- `InvoiceNumberTests` — invoice number format validation
- `MvpBackendFinalizationTests` — integration/end-to-end readiness checks

Tests use `ISystemClock` and `ITransactionRunner` abstractions to control time and transactions.

## Gaps / Known TODOs

- PostgreSQL sync not implemented (queue exists, `FlushAsync` only marks items as Processing)
- Twilio WhatsApp sender is a stub
- No CI/CD, Docker, linter, or `.editorconfig`
- `TreatWarningsAsErrors` is `false` in Directory.Build.props
- Backend lacks a "get product catalog with prices" API — POS currently prompts for price manually
- Customer account dialogs (Create Business Account, Create Walk-in Creditor, Record Payment) are placeholder stubs
