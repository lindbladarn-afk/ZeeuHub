# Test project structure

Tests are grouped by portal feature so that production code and its verification are easy to find together. New feature-specific tests should be placed in the matching directory, while genuinely shared infrastructure tests may remain at the project root.

Current feature directories include:

- `AI`
- `ApprovalChains`
- `CustomerSync`
- `Dashboard`
- `Observability`
- `Purchase`
- `Security`
- `WebApproval`

File names should match the production type or behavior under test and end with `Tests.cs`. Moving a test into a feature directory does not require changing its namespace.

Run the complete suite from the repository root:

```bash
dotnet test ZeeU.CustomerPortal.sln
```
