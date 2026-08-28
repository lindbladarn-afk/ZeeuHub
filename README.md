

# ZeeU Customer Portal

## Active project structure

The repository has one active source tree:

- `WebApp/` contains the ASP.NET Core application and feature-oriented modules.
- `Repository/` contains shared and legacy data access that has not yet moved behind feature boundaries.
- `Entities/` contains shared application models that are being narrowed over time.
- `LoggerService/`, `MailService/`, and `NotificationService/` contain cross-cutting adapters used by the web application.
- `WebApp.Tests/` contains the automated test suite.

The active build and deploy path in this repository uses the root structure:

- Solution: `ZeeU.CustomerPortal.sln` -> `WebApp/WebApp.csproj`
- Automated tests: `WebApp.Tests/WebApp.Tests.csproj` (included in the solution)
- Docker: `WebApp/Dockerfile`
- Docker Compose: `docker-compose.yml` -> `WebApp/Dockerfile`

## Stable local build

Use this command sequence from the repository root when you want a reproducible local WebApp build:

```bash
dotnet restore WebApp/WebApp.csproj --disable-parallel
dotnet build WebApp/WebApp.csproj --no-restore -p:BuildProjectReferences=false -p:RazorCompileOnBuild=false -p:UseSharedCompilation=false -nr:false -m:1 -v minimal
```

The build intentionally runs with:

- `--disable-parallel` on restore
- `-p:UseSharedCompilation=false`
- `-nr:false`
- `-m:1`

because the local environment has intermittently hung when NuGet restore parallelism, MSBuild node reuse, or shared compilation are left enabled.

Run the complete automated test suite from the repository root with:

```bash
dotnet test ZeeU.CustomerPortal.sln
```

## Repository structure rule

The active codebase currently uses two repository layers on purpose:

- `Repository/` = legacy/core data-access used by broad platform flows such as admin, application context, purchase, web approval, and shared Jeeves execution.
- `WebApp/Repositories/` = feature-oriented repositories for newer or more isolated modules such as invoices, orders, customer activity, excel import, integrations, and similar read-focused features.

Current cleanup direction:

- Do not mass-move everything into `WebApp/`.
- Keep `Repository/` for broad core/legacy areas until there is a specific reason to refactor them.
- Move clearly feature-scoped repositories into `WebApp/Repositories/<Feature>` when they are already isolated enough to do so safely.

## Legacy code findings

Current verified findings:

- The old parallel `src/Presentation/WebApp` tree was verified as unused and removed. Its history remains available in Git.
- The old `Licenses` admin entry has been removed from the active admin UI, but the license domain model is still used in the active company/session model and is therefore not safe to delete yet.

# Setup

## Customer env
In order to get access to the customer Jeeves on prem database the Azure Hybrid Connector needs to be installed on the SQL server.
Then the connection must be added in the ZeeURelay Hybrid Connections in Azure. And the customers on prem connection must be pointed to the URL provided in the ZeeURelay post.

## Jeeves Setup

<b>WebApproval</b>
There are two parameters that needs to be added in the Jeeves program [jvsp] under the application area code "Customizations".
- custom_portal_1 - This is the DBMail profile that should be used to send the approval mail.
- custom_portal_2 - This is the Company ID from the Azure Identity database. (This is used to create part of the URL link of the approval mail)

The approval flows needs to be set in Jeeves, the perssign assigned to the user in CustomerPortal needs to have the correct approval rights in the program q_zu_approval_chains.
Flow 0 is purchase approvals
Flow 1 is sales approvals

<b>Portal Expense</b>
You need to add some fields in Jeeves in order to make this module work.

[ar] or artikel
- q_zu_default_account (string)
The Account number you add on a article in the field q_zu_default_account must exist in the current year in the program [ko] and [beko]
- q_zu_default_costcenter (string)
The Cost center you add on a article in the field q_zu_default_costcenter must exist in the current year in the programs [kt] and [bekt]
- q_zu_expense_item (boolean)

[le] or leverantörer
- q_zu_expense_supplier (boolean)

Create a product account named Expense
Create a new order type named Expense
Create a new article with the procuct account Expense and a default account
Create a contact (cr.godsaviskod)


## SQL Setup

<b>Portal Expense</b>
The expense module is dependent on two StoredProcedures in the ZeeU.API. They need to be installed in order to be able to create purchase order and purchase order rows.

They are located in the ZeeU.API repository under /Database/ZeeU/Inköpsportal
- q_zu_restapi_inkop_post
- q_zu_restapi_inkoprader_post

## Portal Company and User
Log in to the portal as an administrator, under administration you can add the company and a user.

<b>Company</b>
The ConnectionString is used for connecting to the Jeeves database within the customers environment.

## Local development

The application uses .NET user secrets for local credentials. Never add passwords, tokens, client secrets or production connection strings to tracked settings files.

For a complete Docker onboarding flow with local `CustomerPortal` and `Jeeves6` backups, follow [docs/local-hub-onboarding.md](docs/local-hub-onboarding.md). The included `scripts/jeeves-db.sh` tool creates, verifies and safely restores both development databases.

Configure the minimum required Identity connection locally:

```bash
dotnet user-secrets set "ConnectionStrings:PortalIdentity" "<local-or-approved-development-connection-string>" --project WebApp
```

Add feature-specific secrets in the same way when needed, for example Jeeves, OpenAI or Akeneo credentials. ASP.NET Core environment variables are also supported by replacing `:` with `__`, such as `ConnectionStrings__PortalIdentity`.

The base `docker-compose.yml` defines the web application. The checked-in `docker-compose.override.yml` adds local SQL Server and hot-reload services; Docker Compose loads it automatically. Credentials and optional integration settings are supplied through a local `.env` file and are never committed.

Create the local environment file and replace every `CHANGE_ME` value before starting containers:

```bash
cp .env.example .env
```

Validate and start the checked-in Compose configuration with:

```bash
docker compose config --quiet
docker compose up --build
```

Data Protection keys under `WebApp/App_Data/DataProtectionKeys` are runtime state. They must be persisted securely by the deployment environment and must never be committed to Git.
