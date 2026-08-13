# AGENTS.md

This file provides repository-wide instructions for coding agents working on
Smart Parking Navigator.

Smart Parking Navigator is a mobile-first web application that helps drivers
find suitable HDB car parks near a destination in Singapore. It combines static
car park information with live lot availability to support filtering, ranking,
and nearby alternatives.

## Scope and Current State

These instructions apply to the entire repository.

The P0 functional vertical slice is implemented. It includes HDB catalogue
loading, data.gov.sg availability polling and manual refresh, geospatial search,
filters, ranking, alternatives, the Blazor map/list experience, Google Places
destination discovery, generated OpenAPI clients, Aspire composition, and Azure
Container Apps deployment preparation.

Post-P0 performance, browser-compatibility, accessibility, custom observability,
and production-deployment gates are not complete. The approved product behavior
is documented in `PRD.md`, and the approved technical design is documented in
`TRD.md`. Treat those documents as requirements, but verify the current source
tree before assuming a planned capability exists.

## Repository Structure

```text
.
├── src/
│   ├── CarparkAvailability.ApiApp/            # ASP.NET Core Web API
│   ├── CarparkAvailability.AppHost/           # .NET Aspire orchestration
│   ├── CarparkAvailability.ServiceDefaults/   # Shared service defaults
│   └── CarparkAvailability.WebApp/            # Blazor Interactive Server UI
├── test/
│   ├── CarparkAvailability.ApiApp.Tests/
│   ├── CarparkAvailability.AppHost.Tests/
│   └── CarparkAvailability.WebApp.Tests/       # xUnit v3 component/state tests
├── data/                                       # Versioned source/sample data
├── contracts/                                  # Internal OpenAPI contract
├── .azure/deployment-plan.md                   # Tracked deployment status
├── aspire.config.json                          # Aspire CLI AppHost selection
├── azure.yaml                                  # Azure Developer CLI entry point
├── Directory.Build.props                       # Solution-wide .NET settings
├── Directory.Packages.props                    # Central package versions
└── CarparkAvailability.slnx                    # Restore/build/test entry point
```

The AppHost registers ApiApp and WebApp, injects both external API keys from
AppHost configuration, keeps ApiApp internal, exposes WebApp, configures health
probes, and publishes both projects as single-replica Azure Container Apps.
ServiceDefaults contains shared service discovery, resilience, health check,
logging, and OpenTelemetry configuration.

## Technology and Architecture Constraints

- Target .NET 10 as pinned by `global.json`.
- Keep nullable reference types and implicit usings enabled.
- Use .NET Aspire for local application composition and shared observability.
- Keep browser-facing UI in the Blazor web project and backend data and search
  behavior in the API project.
- Preserve the implemented OpenAPI-first boundary between the web and API
  projects.
- Put cross-service telemetry, health, resilience, and service-discovery
  defaults in `CarparkAvailability.ServiceDefaults` rather than duplicating
  them across application projects.
- Do not add separately maintained Azure infrastructure that conflicts with
  the approved Aspire and Azure Developer CLI deployment model.
- Keep external service access behind clear abstractions so it can be tested
  without calling production APIs.

## Source of Truth

Use the following precedence when requirements appear inconsistent:

1. The current task or linked issue.
2. `PRD.md` for product behavior and acceptance criteria.
3. `TRD.md` for architecture and implementation constraints.
4. Existing source and tests for implemented behavior.
5. `README.md` and `CONTRIBUTING.md` for setup and contributor workflows.

Do not silently resolve a material conflict between these sources. Document the
conflict and obtain clarification before making a behavior or architecture
decision.

## Development Commands

Run commands from the repository root:

```powershell
dotnet restore CarparkAvailability.slnx
dotnet build CarparkAvailability.slnx --no-restore --configuration Release
dotnet test CarparkAvailability.slnx --no-build --configuration Release
dotnet run --project src\CarparkAvailability.AppHost
```

The AppHost starts and connects the web and API projects. Configure
`GoogleMaps:ApiKey` and `DataGovSg:ApiKey` in its user-secrets store before
running external-service journeys.

Use the narrowest relevant test project while iterating, then run the solution
build and tests before completing a code change.

## Change Guidelines

- Prefer the simplest implementation that fully satisfies the current
  requirements.
- Avoid over-engineering, speculative abstractions, premature extensibility,
  and infrastructure for unapproved future features.
- Reuse existing patterns and platform capabilities before adding new layers,
  helpers, or dependencies.
- Keep changes focused and proportional to the problem.
- Preserve project boundaries and avoid unrelated cleanup.
- Implement the complete vertical path required by the task; do not leave
  disconnected API, UI, configuration, or orchestration changes.
- Keep failures explicit. Do not swallow exceptions, return success-shaped
  fallbacks, or silently ignore invalid input.
- Update documentation when behavior, architecture, configuration, or setup
  changes.

## Coding Conventions

Follow `.editorconfig` and the existing project style:

- Use four spaces for C# and two spaces for XML, JSON, and project files.
- Prefer file-scoped namespaces and `using` directives outside namespaces.
- Use explicit local types rather than `var`, in accordance with the configured
  C# style.
- Use PascalCase for types and members, `I`-prefixed PascalCase for interfaces,
  camelCase for locals and parameters, `_camelCase` for private instance
  fields, and `s_camelCase` for private static fields.
- Prefer readonly fields and static local or anonymous functions where
  applicable.
- Preserve nullable annotations and address warnings with types and guards
  rather than null-forgiving operators or unsafe casts.
- Add comments only when intent or a non-obvious constraint is not clear from
  the code.

Use `dotnet format` only when needed and avoid broad formatting changes in
otherwise focused work.

## Dependencies

- Declare package versions centrally in `Directory.Packages.props`.
- Use the repository's approved major-version floating ranges for centrally
  managed packages unless a task explicitly requires a narrower version.
- Add versionless `PackageReference` entries to individual project files.
- Reuse packages already present in the repository when they meet the need.
- Add a dependency only when the platform or existing packages cannot solve the
  requirement simply and reliably.
- Do not update unrelated package versions as part of a feature or bug fix.

## Testing Expectations

- Add or update tests for every behavior change.
- Place tests in the project corresponding to the changed application surface.
- Prefer deterministic tests that do not depend on live Google Maps,
  data.gov.sg, Azure, time, randomness, or network availability.
- Use Aspire hosting tests for application composition and cross-service
  behavior.
- Cover success, validation, empty-result, and external-service failure paths
  where relevant.
- Do not enable placeholder tests without replacing their example resource
  names and wiring them to real AppHost resources.

## Data, Configuration, and Security

- Treat files under `data/` as versioned inputs or fixtures. Preserve their
  provenance and Singapore Open Data Licence acknowledgement.
- Avoid rewriting large source datasets unless the task explicitly requires it.
- Never commit credentials, API keys, connection strings, or local environment
  files.
- Supply the browser-visible Google Maps key through
  `GoogleMaps__ApiKey`. Protect it with Google Maps website and API
  restrictions; do not describe it as a confidential server secret.
- Keep `GoogleMaps:ApiKey` and `DataGovSg:ApiKey` in the AppHost user-secrets
  store for local development; AppHost maps them to service environment
  configuration.
- Use environment variables or the established .NET user-secrets mechanism for
  local configuration.
- Validate external input and avoid logging sensitive configuration values.

## Git and Pull Request Conventions

- Follow Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `refactor:`,
  or `chore:`.
- After completing and validating each coherent implementation step, commit it
  before starting the next step. Do not leave completed steps uncommitted or
  combine multiple completed steps into one later commit.
- Make each commit one coherent, independently reviewable unit of work.
- Do not mix features, refactoring, formatting, tests, or documentation unless
  they directly support the same change.
- Keep the repository buildable and tests passing after each commit.
- Include directly related tests and required documentation in the same commit
  as the behavior change.
- Write the commit subject to describe that single unit of work.
- Keep pull requests focused, link the relevant issue, and use the repository's
  pull request template.
- Do not commit generated build output, secrets, or unrelated local changes.

## Documentation

- Keep `README.md` accurate for currently available setup and behavior.
- Update `PRD.md` only when approved product requirements change.
- Update `TRD.md` when an approved architecture decision changes.
- Clearly label planned behavior instead of documenting it as implemented.
- Use the actual AppHost path,
  `src\CarparkAvailability.AppHost`, in commands and examples.
