# Architecture Notes

Blue Birds ERP is scaffolded as a layered .NET solution:

- `Domain` owns entities, enums, and business rules that must remain valid regardless of UI or database choice.
- `Application` owns use-case contracts and DTOs.
- `Infrastructure` owns EF Core persistence, adapter implementations, and integration boundaries.
- `Desktop` owns the WPF shell and view models.

The POS path should remain capable of using local SQLite when internet connectivity is unavailable, then sync queued work to the central PostgreSQL database after reconnection.

