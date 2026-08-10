# Contributing to Smart Parking Navigator

Thank you for contributing to Smart Parking Navigator.

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
By participating, you agree to uphold it.

## Development Setup

Prerequisites:

- .NET 10 SDK
- Git
- A container runtime supported by .NET Aspire, when required

Clone the repository and restore dependencies:

```powershell
git clone https://github.com/devkimchi/smart-parking-navigator.git
cd smart-parking-navigator
dotnet restore
```

After the solution is scaffolded, run the application through its Aspire
AppHost:

```powershell
dotnet run --project src/SmartParkingNavigator.AppHost
```

## Making Changes

1. Create a branch using `feat/`, `fix/`, `docs/`, or `chore/`.
2. Keep changes focused and add tests for behavior changes.
3. Run the local checks:

   ```powershell
   dotnet build --configuration Release
   dotnet test --configuration Release
   ```

4. Update documentation when behavior or setup changes.
5. Open a pull request and link the related issue.

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

