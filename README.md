# Blue Birds ERP

Blue Birds ERP is a native Windows desktop ERP for a pre-processed poultry retail and wholesale shop. The product-facing name is **PoultryPro ERP**.

The scaffold follows the SRS in `docs/BlueBird_ERP_SRS.md` and is organized for POS billing, manual batch inventory purchases, credit accounts, returns, wastage, queued WhatsApp notifications, RBAC, and audit logging.

## Technology

- .NET 8
- WPF desktop UI (CommunityToolkit.Mvvm)
- MVVM-style presentation layer
- SQLite as the local database
- EF Core persistence

## Solution Layout

- `src/BlueBirdsERP.Desktop` - WPF shell, navigation, views, and ViewModels
- `src/BlueBirdsERP.Domain` - SRS entities, enums, and core business rules
- `src/BlueBirdsERP.Application` - service contracts, DTOs, and use-case implementations
- `src/BlueBirdsERP.Infrastructure` - EF Core contexts, security, printing, reporting, and DI wiring
- `tests/BlueBirdsERP.Tests` - unit tests for business rules and services
- `config` - example configuration
- `database` - migration and seed placeholders
- `scripts` - developer helper placeholders

## Commands

```powershell
dotnet restore BlueBirdsERP.sln
dotnet build BlueBirdsERP.sln
dotnet test BlueBirdsERP.sln
dotnet run --project src/BlueBirdsERP.Desktop/BlueBirdsERP.Desktop.csproj
```

## Configuring Twilio WhatsApp Notifications

PoultryPro uses Twilio's WhatsApp API to send payment reminders, overdue alerts, and daily sales summaries. Follow these steps to configure:

### 1. Create a Twilio Account

1. Go to [https://www.twilio.com](https://www.twilio.com) and sign up.
2. Verify your phone number during registration.

### 2. Get Your Credentials

1. In the Twilio Console, find your **Account SID** and **Auth Token** on the dashboard.
2. Copy both values.

### 3. Set Up WhatsApp Sandbox

1. In the Twilio Console, navigate to **Messaging > Try it out > Send a WhatsApp message**.
2. Follow the instructions to join the WhatsApp sandbox (send the join code to the Twilio sandbox number from your WhatsApp).
3. Note the sandbox WhatsApp number (e.g., `whatsapp:+14155238886`).

### 4. Configure PoultryPro

Edit `config/appsettings.example.json` (or your local `appsettings.json`) and fill in the Twilio section:

```json
{
  "Twilio": {
    "AccountSid": "YOUR_ACCOUNT_SID",
    "AuthToken": "YOUR_AUTH_TOKEN",
    "WhatsAppSender": "whatsapp:+14155238886"
  }
}
```

- **AccountSid** — Your Twilio Account SID (starts with `AC`).
- **AuthToken** — Your Twilio Auth Token.
- **WhatsAppSender** — The Twilio WhatsApp sandbox number in the format `whatsapp:+1234567890`.

### 5. Configure Notification Settings

In the same config file, set the owner's WhatsApp number and preferences:

```json
{
  "Notifications": {
    "OwnerWhatsAppNumber": "+94771234567",
    "DailyReportTime": "20:00",
    "PaymentReminderLeadDays": 3,
    "MaxRetryCount": 3,
    "RetryIntervalMinutes": 10
  }
}
```

- **OwnerWhatsAppNumber** — The shop owner's WhatsApp number (with country code, e.g., `+94771234567`).
- **DailyReportTime** — Time to send the daily sales summary (24-hour format).
- **PaymentReminderLeadDays** — Days before due date to send payment reminders.
- **MaxRetryCount** — Number of retry attempts for failed messages.
- **RetryIntervalMinutes** — Minutes between retry attempts.

### Notes

- The Twilio WhatsApp sandbox is for testing only. For production, you need to apply for WhatsApp Business API access through Twilio.
- All notification templates can be customized from the Settings page in the app.
- Failed notifications are logged and can be retried from the Settings page.

## Current Status

Implemented slices cover POS billing/returns (with product catalog pricing), customer credit accounts (business accounts + walk-in creditors with full CRUD), manual batch inventory, WhatsApp notification rules, RBAC/authentication, SQLite-backed persistence, Admin settings, recovery access, receipt PDF generation, and operational reporting.
