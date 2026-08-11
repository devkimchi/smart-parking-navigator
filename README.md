# Smart Parking Navigator

Smart Parking Navigator is a mobile-first web application for finding suitable
HDB car parks near a destination in Singapore. It combines static HDB car park
information with live lot availability to search within 500 metres, filter by
vehicle type and parking conditions, rank compatible options, and show nearby
alternatives when a car park is full.

> [!NOTE]
> The repository currently contains the initial .NET solution and application
> scaffolding. The approved product and technical designs are documented in
> [PRD.md](PRD.md) and [TRD.md](TRD.md); parking, map, and Azure deployment
> features are not implemented yet.

## Architecture

```mermaid
flowchart LR
    User[Driver in a Chromium browser]
    Maps[Google Maps Platform]
    Availability[data.gov.sg<br/>Car Park Availability API]
    Csv[HDBCarparkInformation.csv]
    Monitor[Azure Monitor<br/>Application Insights]

    subgraph ACA[Azure Container Apps]
        Web[Blazor Web App<br/>Interactive Server]
        Api[ASP.NET Core Web API]
    end

    User <-->|HTTPS and Blazor SignalR| Web
    User <-->|Maps, places, and geocoding| Maps
    Web -->|Generated OpenAPI client| Api
    Api -->|Load at startup| Csv
    Api -->|Poll every 60 seconds| Availability
    Web -.->|OpenTelemetry| Monitor
    Api -.->|OpenTelemetry| Monitor
```

The solution uses:

- **.NET 10** and **.NET Aspire** for application composition and observability.
- **Blazor Web App** with Interactive Server render mode.
- **ASP.NET Core Web API** for data ingestion, search, and recommendations.
- **Microsoft Fluent UI Blazor** for the user interface.
- **OpenAPI-first** frontend/backend communication.
- **xUnit v3** and **Playwright for .NET** for automated testing.
- **Azure Container Apps** as the approved deployment target.

## Prerequisites

### Local application development

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Chromium-family browser
- A [Google Cloud project](https://console.cloud.google.com/) with billing
  enabled for a standard Google Maps Platform key
- A container runtime supported by Aspire when local container resources are
  introduced

### Google Maps API key

Google Maps Platform requires an API key for authentication and billing.

1. Create or select a project in the
   [Google Cloud console](https://console.cloud.google.com/).
2. Enable billing for the project.
3. Enable the
   [Maps JavaScript API](https://console.cloud.google.com/google/maps-apis/api-list)
   and the Places API required for destination search.
4. Create an API key from
   [Google Maps Platform credentials](https://console.cloud.google.com/google/maps-apis/credentials).
5. Add **Website** application restrictions for the local and deployed origins,
   for example `https://localhost:*` during development and the production
   application origin after deployment.
6. Add API restrictions so the key can call only the enabled Maps APIs.

The browser must receive the Maps JavaScript API key, so the key cannot be
treated as a confidential server secret. Protect it with website and API
restrictions, monitor its usage, and never commit it to the repository. See
[Google's API security best practices](https://developers.google.com/maps/api-security-best-practices).

The configuration contract will use `GoogleMaps:ApiKey`. Until the integration
is implemented, the key is not consumed by the application. Once available,
set it for the current PowerShell session without writing it to source:

```powershell
$env:GoogleMaps__ApiKey = "<your-restricted-api-key>"
```

### Azure deployment

- An active [Azure subscription](https://azure.microsoft.com/free/)
- [Azure Developer CLI (`azd`)](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
- Permission to create resource groups, Azure Container Apps, Azure Container
  Registry, Log Analytics, Application Insights, and managed identities

GitHub Actions deployment uses OpenID Connect workload identity rather than a
long-lived Azure client secret.

## Getting Started

### Local development

1. Clone the repository and enter its directory.
2. Configure `GoogleMaps__ApiKey` when working on map integration.
3. Restore, build, and test the solution:

   ```powershell
   dotnet restore CarparkAvailability.slnx
   dotnet build CarparkAvailability.slnx --no-restore
   dotnet test CarparkAvailability.slnx --no-build
   ```

4. Run the Aspire AppHost:

   ```powershell
   dotnet run --project src\CarparkAvailability.AppHost
   ```

5. Open the Aspire dashboard URL printed in the terminal.

The AppHost does not register the WebApp and ApiApp yet. After the P0
application composition is implemented, running the AppHost will start and
connect the complete local application.

### Azure deployment

Azure deployment is not configured in the current scaffold. The approved
deployment model keeps Azure Container Apps resources in the Aspire AppHost and
uses `azd`; no separately maintained Bicep files are required.

After the AppHost deployment model and `azure.yaml` are implemented:

```powershell
azd auth login
azd env new
azd provision --preview
azd up
```

`azd provision --preview` must be reviewed before provisioning changes.
`azd up` provisions the AppHost-defined Azure resources and deploys the
application. The deployment output provides the public WebApp URL.

The GitHub Actions workflow will run restore, build, tests, OpenAPI validation,
browser tests, container packaging, security scanning, and deployment. Pull
requests validate only; merges to `main` deploy to development, while production
uses a protected GitHub environment and manual approval.

See
[Deploy an Aspire project to Azure Container Apps](https://learn.microsoft.com/dotnet/aspire/deployment/azure/aca-deployment)
for the platform workflow.

## Repository Structure

```text
/
|-- contracts/                              # Internal OpenAPI source of truth
|-- data/                                   # Versioned HDB static data
|-- src/
|   |-- CarparkAvailability.ApiApp/         # ASP.NET Core Web API
|   |-- CarparkAvailability.AppHost/        # Aspire orchestration
|   |-- CarparkAvailability.ServiceDefaults/ # Shared telemetry and resilience
|   `-- CarparkAvailability.WebApp/         # Blazor Web App
|-- test/
|   |-- CarparkAvailability.ApiApp.Tests/
|   |-- CarparkAvailability.AppHost.Tests/
|   `-- CarparkAvailability.WebApp.Tests/
|-- IDEATION.md
|-- PRD.md
`-- TRD.md
```

Some planned paths, including `contracts/`, are created during implementation.

## Further Reading

- [Product Requirements Document](PRD.md)
- [Technical Requirements Document](TRD.md)
- [Initial Product Ideation](IDEATION.md)
- [.NET Aspire documentation](https://aspire.dev/)
- [ASP.NET Core Blazor render modes](https://learn.microsoft.com/aspnet/core/blazor/components/render-modes)
- [ASP.NET Core OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
- [Microsoft Fluent UI Blazor](https://www.fluentui-blazor.net/)
- [xUnit.net v3 documentation](https://xunit.net/docs/getting-started/v3/getting-started)
- [Playwright for .NET](https://playwright.dev/dotnet/)
- [Maps JavaScript API](https://developers.google.com/maps/documentation/javascript)
- [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/)

## Data Acknowledgement

This project uses:

- [Car Park Availability](https://data.gov.sg/datasets?formats=API&sort=relevancy&resultId=d_ca933a644e55d34fe21f28b8052fac63)
- [HDB Car Park Information](https://data.gov.sg/datasets/d_23f946fa557947f93a8043bbef41dd09/view)

Contains information from **Car Park Availability** and **HDB Car Park
Information**, accessed on 11 August 2026 from
[data.gov.sg](https://data.gov.sg/), which is made available under the terms of
the
[Singapore Open Data Licence version 1.0](https://data.gov.sg/open-data-licence).

The datasets are provided on an "as is" and "as available" basis. This project
is not endorsed by data.gov.sg, HDB, or the Singapore Government.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and pull request
guidelines.

## Security

See [SECURITY.md](SECURITY.md) to report a vulnerability privately.

## Licence

The project source code is licensed under the [MIT License](LICENSE). Data
remains subject to the Singapore Open Data Licence described above.
