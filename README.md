# Anaya 3D Prints store

The application is an ASP.NET Core MVC store using SQLite, Stripe Checkout and
SMTP. It requires the .NET 10 SDK and persistent storage for the database and
customer uploads.

## Before deployment

- **Revoke and replace the previously committed Stripe test secret and SMTP
  password.** Removing them from configuration does not revoke them or remove
  them from Git history, existing clones, or cached deployments. Review account
  activity and follow the providers' incident-response guidance.
- Store replacement credentials in the hosting provider's secret manager, not
  in the repository. Set `StripeSettings__SecretKey`, `Stripe__WebhookSecret`,
  `EmailSettings__Username`, `EmailSettings__Password`, and
  `EmailSettings__SenderEmail`. Configure `EmailSettings__Host` and
  `EmailSettings__Port` for the SMTP provider.
- Set `Store__PublicOrigin` to the canonical HTTPS origin of the site, without a
  path. Configure a real HTTPS endpoint and a restrictive `AllowedHosts` value.
  Configure any reverse proxy to terminate TLS and handle forwarded headers
  safely; do not trust forwarded headers from arbitrary clients.
- Configure `Admin__Username` and `Admin__PasswordHash` using a secret manager.
  There is no default admin login. On an interactive terminal generate a hash
  with `dotnet run --project ThreeDPrintStore --no-launch-profile -- --generate-admin-password-hash`;
  the password is not echoed. Keep the hash and password private. Admin login is
  at `/Account/Login`.
- Apply the EF Core migrations to a persistent SQLite database before running
  the app, using the matching `dotnet-ef` tool and
  `dotnet ef database update --project ThreeDPrintStore`. Set
  `ConnectionStrings__DefaultConnection` to the persistent database location.
  Test migrations and backups before applying them to a production database.
- Configure a Stripe webhook for `/api/webhook/stripe` and supply its signing
  secret. Set the endpoint's API version to the version expected by the
  installed Stripe SDK (`StripeConfiguration.ApiVersion`, currently
  `2026-10-28.endive` for Stripe.net 53.1.0); otherwise valid
  events can be rejected. Review and update this setting when upgrading the
  SDK, and monitor and retry failed webhook deliveries. Verify checkout,
  paid-event handling, email, and the admin workflow with Stripe test mode
  before accepting real payments.

Custom-quote payment and quote emails are disabled: quote checkout does not yet
persist and reconcile checkout attempts across Stripe's idempotency retention
window, and quote shipping has not been specified. Reconcile any previously
issued payment links or open sessions in Stripe before re-enabling this flow.
The discounted local-delivery option is also unavailable until address
eligibility can be reliably verified; standard US shipping remains available.
Cart checkout reuses an open payment session for the same browser and order.
After payment is confirmed, starting another order requires an explicit action.
This protection depends on a persistent browser cookie and does not prevent
duplicate purchases from another browser or device, or after cookies are cleared.
HTTPS and persistent ASP.NET Core data-protection keys are required.

If a checkout's creation outcome cannot be resolved within Stripe's
idempotency window, checkout remains unavailable rather than risking a second
payment. Investigate and reconcile the order and Stripe activity before taking
manual action. Sessions created before the checkout-protection migration cannot
be linked to the new browser identity: reconcile outstanding payments and
sessions before deployment.

## Data protection and backups

Customer models and reference images are stored in `ThreeDPrintStore/App_Data/Uploads`
and must reside on persistent, access-controlled storage. The application blocks
public access to legacy `wwwroot/uploads` files, but previously committed files
may still be present in Git history, clones, and deployments. Review those
files and arrange a secure migration and any history cleanup with the repository
owner. Do not copy customer data into the repository.
When migrating legacy files, preserve database-referenced filenames from both
`wwwroot/uploads` and `wwwroot/uploads/references`, check for filename
collisions without overwriting, and verify authorized downloads before removing
public deployment copies. Older absolute paths from another host may require
manual mapping.

Schedule encrypted, access-controlled backups of both the SQLite database and
private uploads, and periodically test restoration. Use SQLite's online
backup facility or stop writes before taking a filesystem copy; copying only the
database file while it is active can produce an inconsistent backup. Retain
and protect ASP.NET Core data-protection keys across restarts and instances.
Configure backup scheduling, retention and monitoring in the deployment
environment; this repository does not provision them.

## Development

Run the app with `dotnet run --project ThreeDPrintStore`. Admin cookies require
HTTPS. Set development secrets using .NET user secrets or environment
variables, never committed configuration. Run the regression tests with
`dotnet test ThreeDPrintStore.Tests`.