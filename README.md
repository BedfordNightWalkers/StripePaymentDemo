# Stripe Payment Demo

An ASP.NET Core MVC application that shows how to take one-off card payments with
[Stripe Checkout](https://docs.stripe.com/payments/checkout). It is a learning project:
it supports GBP only and is meant to be run against Stripe **test mode**.

## What it does

1. **Product list** – the home page lists active products from a SQL Server database.
2. **Checkout page** – choosing a product shows its name and price, read from the database.
3. **Create a Checkout Session** – when the user clicks *Pay*, the app:
   - loads the product again from the database, so the price always comes from the server
     and never from the browser;
   - saves a `Payment` record with status `Pending` **before** calling Stripe;
   - asks Stripe for a Checkout Session, using an idempotency key based on the payment ID
     so a retry can't create a second session;
   - stores the Stripe session ID on the payment and redirects the user to Stripe's hosted
     payment page.
4. **Return pages** – Stripe sends the user back to `/Checkout/Success` or `/Checkout/Cancel`.
   These pages do **not** confirm the payment; a redirect back from Stripe is not proof that
   the payment succeeded.

### Not implemented yet

- **Webhooks.** `StripeWebhookProcessor` is an empty placeholder, so payments currently stay
  `Pending`. Marking them `Paid` or `Failed` needs a handler for Stripe's
  `checkout.session.completed` and related events.
- `PaymentService.GetPaymentAsync` throws `NotImplementedException`.

## Solution structure

| Project | Responsibility |
|---|---|
| `StripePaymentDemo.Domain` | Entities (`Payment`, `Product`) and the `PaymentStatus` enum. No dependencies. |
| `StripePaymentDemo.Application` | `PaymentService` (the checkout steps above), repository and Stripe client interfaces, and the application exceptions. |
| `StripePaymentDemo.Infrastructure` | EF Core `PaymentDbContext`, repositories, migrations, and `StripeCheckoutClient` (Stripe.net). |
| `StripePaymentDemo.Web` | ASP.NET Core MVC controllers, views and dependency injection setup. |
| `StripePaymentDemo.Tests` | xUnit tests using FakeItEasy, FluentAssertions and the EF Core InMemory provider. |

## Prerequisites

| Requirement | Notes |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | All projects target `net10.0`. |
| SQL Server | Any edition. On macOS or Linux, run it in Docker (see below). On Windows, LocalDB also works. |
| [Docker](https://www.docker.com/) | Only if you run SQL Server in a container. |
| EF Core CLI tools | `dotnet tool install --global dotnet-ef`, used to apply migrations. |
| A [Stripe account](https://dashboard.stripe.com/register) | Free. You need the **test mode** secret key (`sk_test_...`) from *Developers → API keys*. |
| `sqlcmd` or a SQL client | Optional, for loading the sample products. The SQL Server Docker image includes `sqlcmd`. |

## Getting started

### 1. Start SQL Server in Docker

Skip this step if you already have a SQL Server instance.

```bash
docker run -d --name sql_server_container \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=<YourStrong!Passw0rd>" \
  -p 1434:1433 \
  mcr.microsoft.com/mssql/server:2025-latest
```

This maps the container to host port `1434`. Change it if you prefer the default `1433`.

### 2. Configure secrets

Keep the connection string and the Stripe key out of `appsettings.json` by using
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets). They are loaded
automatically when `ASPNETCORE_ENVIRONMENT` is `Development`.

```bash
dotnet user-secrets set "ConnectionStrings:PaymentDb" \
  "Server=localhost,1434;Database=StripePaymentDemo;User Id=sa;Password=<YourStrong!Passw0rd>;TrustServerCertificate=True" \
  --project StripePaymentDemo.Web

dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project StripePaymentDemo.Web
```

The other Stripe settings are in `StripePaymentDemo.Web/appsettings.json`:

| Key | Purpose |
|---|---|
| `Stripe:SecretKey` | Stripe test secret key. If it is missing, every `/Checkout` request fails with `Stripe:SecretKey is not configured`. |
| `Stripe:SuccessUrl` | Where Stripe sends the user after paying, e.g. `https://localhost:7033/Checkout/Success`. |
| `Stripe:CancelUrl` | Where Stripe sends the user after cancelling, e.g. `https://localhost:7033/Checkout/Cancel`. |

The URLs must match the address the app is running on (see
`StripePaymentDemo.Web/Properties/launchSettings.json`).

### 3. Create the database

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --project StripePaymentDemo.Infrastructure \
  --startup-project StripePaymentDemo.Web
```

This creates the `StripePaymentDemo` database with the `Payments` and `Products` tables.

### 4. Load sample products

The migrations create empty tables. To add the sample products, run
`StripePaymentDemo.Infrastructure/Migrations/InsertProducts.sql`. For the Docker container:

```bash
docker exec -i sql_server_container /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P '<YourStrong!Passw0rd>' -C -d StripePaymentDemo \
  < StripePaymentDemo.Infrastructure/Migrations/InsertProducts.sql
```

### 5. Run the application

```bash
dotnet run --project StripePaymentDemo.Web --launch-profile https
```

Open <https://localhost:7033>, pick a product and pay with a
[Stripe test card](https://docs.stripe.com/testing#cards), for example `4242 4242 4242 4242`
with any future expiry date and any CVC.

If the browser warns about the certificate, trust the .NET development certificate once:

```bash
dotnet dev-certs https --trust
```

## Running the tests

```bash
dotnet test
```

The tests need neither a database nor a Stripe account: the repositories run against the EF
Core InMemory provider, and the Stripe client tests only cover validation that happens before
any call to Stripe.
