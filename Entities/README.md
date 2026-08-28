# Entities project boundary

`Entities` contains shared data shapes that must be referenced by more than one project: application entities, cross-project DTOs, repository contracts and legacy shared view models.

Keep this project free from database access, ASP.NET controllers, service orchestration and infrastructure-specific mapping. New UI-only view models belong in `WebApp`; new database execution and column mapping belong in `Repository`.

Some existing view models remain here because both `WebApp` and `Repository` use them. They can be migrated toward feature-owned contracts when those areas are changed, but new presentation-only models should not expand this dependency.
