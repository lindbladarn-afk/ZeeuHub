# Current architecture state

## What is active today

The repository currently runs from the root project layout:

- Solution: `ZeeU.CustomerPortal.sln`
- Web app: `WebApp/WebApp.csproj`
- Automated tests: `WebApp.Tests/WebApp.Tests.csproj`
- Docker build: `WebApp/Dockerfile`
- Compose build: `docker-compose.yml`

## What is risky today

- Some feature files, especially Flow Engine, remain large even though controller ownership is now separated by feature.
- External integrations are still called directly in user-facing request paths.
- Configuration and tenant connection handling are spread across multiple layers.
- Older shared view models in `Entities` still couple persistence contracts to presentation-shaped data.

## Enforced project direction

The active dependency direction is:

`WebApp` → `Repository` → `Entities`

- `Entities` contains shared data shapes and must remain free from persistence and web dependencies.
- `Repository` owns shared SQL execution and Dapper mappings. It may reference `Entities`, but never `WebApp`.
- `WebApp` owns presentation, feature orchestration and feature-local repositories.
- `WebApp.Tests/Architecture` verifies the backward-dependency rules.

## Recommended near-term direction

1. Continue moving Flow Engine command orchestration from its controller partial into dedicated application services.
2. Isolate external integrations behind clearer service boundaries with retries and fallbacks.
3. Standardize configuration access through typed options and factories.
4. Move remaining tests into feature directories as the currently active Bank Reconciliation and Excel Import changes settle.

## Repository boundary

The current repository split is intentional for now:

- `Repository/` should be treated as the legacy/core data-access layer for admin, application context, purchase, web approval, dashboard, and shared execution helpers.
- `WebApp/Repositories/` should be treated as the active feature-oriented layer for newer and more isolated modules.

The recommended migration path is selective:

1. Move only clearly feature-scoped repositories.
2. Avoid mass moves of central legacy repositories.
3. Prefer moving customer-facing read modules first, then reassess.

## Non-goal right now

Do not start with a full rewrite to React or microservices. The higher-value step is architectural cleanup inside the current .NET solution first.
