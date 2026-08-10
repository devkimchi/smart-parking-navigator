# Smart Parking Navigator

A real-time parking discovery and recommendation app using Singapore public data.

## Overview

Smart Parking Navigator helps drivers find suitable HDB car parks by combining
live lot availability with location and facility information. The application
is planned as a .NET Aspire solution with a Blazor frontend and an ASP.NET Core
Web API.

## Planned Capabilities

- Show nearby car parks and live availability on a map
- Search around a destination and rank suitable alternatives
- Filter by lot type, free parking, night parking, car park type, and gantry height
- Display data freshness and availability by vehicle type
- Add traffic and weather context from Singapore public APIs
- Build historical occupancy data for alerts and future availability forecasts

## Planned Architecture

- **.NET Aspire AppHost** for local orchestration and observability
- **Blazor web app** for the interactive map and parking experience
- **ASP.NET Core Web API** for data ingestion, normalization, and recommendations
- **Service Defaults** for shared resilience, health checks, and telemetry
- **Automated tests** for API integration and recommendation behavior

## Data Sources

The project uses Singapore public data, including HDB car park information and
real-time car park availability from [data.gov.sg](https://data.gov.sg/).

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A container runtime supported by .NET Aspire, when required by backing services

Application projects have not yet been scaffolded. After the Aspire solution is
added, the standard development workflow will be:

```powershell
dotnet restore
dotnet build
dotnet test
```

Run the AppHost project to start the complete application:

```powershell
dotnet run --project src/SmartParkingNavigator.AppHost
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and pull request
guidelines.

## Security

See [SECURITY.md](SECURITY.md) to report a vulnerability privately.

## License

Licensed under the [MIT License](LICENSE).
