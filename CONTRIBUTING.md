# Contributing to Smart Parking Navigator

Thank you for contributing to Smart Parking Navigator.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
By participating, you agree to uphold it.

## Development Setup

Prerequisites:

- .NET 10 SDK
- Git
- A Chromium-family browser
- Google Maps and data.gov.sg API keys for local end-to-end use
- A container runtime supported by .NET Aspire for packaging or deployment

Clone the repository and restore dependencies:

```powershell
git clone https://github.com/devkimchi/smart-parking-navigator.git
cd smart-parking-navigator
dotnet restore CarparkAvailability.slnx
```

Configure development credentials by following
[Google Maps API Key Setup](docs/google-maps-api-key.md) and
[data.gov.sg API Key Setup](docs/data-gov-sg-api-key.md).

Run the complete application through its Aspire AppHost:

```powershell
dotnet run --project src\CarparkAvailability.AppHost
```

## Making Changes

1. Create a branch using `feat/`, `fix/`, `docs/`, or `chore/`.
2. Keep changes focused and add tests for behavior changes.
3. Run the local checks:

   ```powershell
   dotnet restore CarparkAvailability.slnx
   dotnet build CarparkAvailability.slnx --no-restore --configuration Release
   dotnet test CarparkAvailability.slnx --no-build --configuration Release
   ```

4. Update documentation when behavior or setup changes.
5. Commit each completed and validated coherent step before starting the next.
6. Open a pull request and link the related issue.

## OpenAPI Client Generation

ApiApp generates its data.gov.sg transport client from
`data\CarparkAvailability.json`. WebApp generates its internal API client from
`contracts\carpark-availability-api.openapi.yaml`. Both generated clients live
under `obj` and must not be edited or committed.

To validate generation explicitly:

```powershell
dotnet msbuild src\CarparkAvailability.ApiApp\CarparkAvailability.ApiApp.csproj `
  -target:GenerateDataGovSgApiClient -property:Configuration=Release `
  -property:TargetFramework=net10.0
dotnet msbuild src\CarparkAvailability.WebApp\CarparkAvailability.WebApp.csproj `
  -target:GenerateCarparkApiClient -property:Configuration=Release `
  -property:TargetFramework=net10.0
```

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` new functionality
- `fix:` bug fixes
- `docs:` documentation-only changes
- `test:` test additions or updates
- `refactor:` behavior-preserving code changes
- `chore:` maintenance and dependency changes

## Reporting Bugs and Requesting Features

Use the structured forms in `.github/ISSUE_TEMPLATE/` and include enough
context to reproduce or evaluate the request.
