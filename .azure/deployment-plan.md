# Azure deployment plan

## Status

Validation blocked

## Environment

| Setting | Value |
|---|---|
| Subscription | Refer to `azd` environment variable `AZURE_SUBSCRIPTION_ID` |
| Location | Refer to `azd` environment variable `AZURE_LOCATION` |
| Tenant | Refer to `azd` environment variable `AZURE_TENANT_ID` |
| Workload | Production, small scale |

## Architecture

Deploy the .NET Aspire application to Azure Container Apps through Azure
Developer CLI. The Aspire AppHost is the only maintained infrastructure model.

| Resource | Configuration |
|---|---|
| Container Apps environment | One environment in the selected Azure region |
| WebApp | External HTTPS ingress; one replica |
| ApiApp | Internal ingress only; one replica |
| Container Apps secrets | Stores the confidential data.gov.sg API key |
| Observability | Aspire service defaults and Container Apps platform telemetry |

The Google Maps browser key is supplied to WebApp and protected with Google
Maps website and API restrictions. It is intentionally not treated as a
confidential server secret. The data.gov.sg key is supplied to ApiApp through a
Container Apps `secretRef` in published environments. Local development reads
both values from AppHost user secrets.

## Delivery

1. Generate deployment metadata with `azd init --from-code`.
2. Keep tenant, subscription, location, and environment values in the
   gitignored `azd` environment.
3. Validate generated infrastructure and application configuration.
4. Provision and deploy only after explicit approval.

No separately maintained Bicep, Terraform, Dockerfiles, or duplicate
infrastructure definitions are permitted.

## Validation Checklist

- [ ] All validation checks pass
  - [x] AZD installation
  - [x] `azure.yaml` schema validation
  - [x] AZD environment setup
  - [x] Azure authentication
  - [x] Subscription, tenant, and location configuration
  - [x] Aspire pre-provisioning checks
  - [x] Provision preview
  - [x] Release build
  - [x] Container build context
  - [x] Application packaging
  - [x] Azure Policy compatibility
  - [x] Aspire post-provisioning prerequisites documented
  - [ ] External service credentials verified

## Role Assignment Verification

- Status: Not required for the data.gov.sg secret flow
- Identities checked: None
- Roles confirmed: None; the secret is stored natively by Container Apps
- Issues: None

## Validation Proof

- `azd` 1.30.0 is installed and authenticated against the selected environment.
- `azure.yaml` schema validation passed.
- `azd provision --preview` completed successfully without creating resources.
- `dotnet build CarparkAvailability.slnx --no-restore --configuration Release`
  completed with zero warnings and zero errors.
- `dotnet test CarparkAvailability.slnx --no-build --configuration Release`
  passed all 52 tests.
- The NSwag-generated data.gov.sg v1 client retrieved 2,000 live records
  through the AppHost secret configuration with no upstream error.
- `azd package --no-prompt` packaged the final application successfully.
- Generated Aspire infrastructure was inspected for ingress, health probes,
  replica limits, and native Container Apps secret references.
- No applicable Azure Policy assignments were found for the selected scope.

## Validation Blockers

- Google Maps loads successfully, but the Geocoding API is not enabled for the
  configured Google Cloud project.

The documented data.gov.sg v1 endpoint was verified with the generated client
and returned fresh live availability. The plan must not advance to `Validated`
until the remaining external-service check passes. Provisioning and deployment
still require explicit approval.
