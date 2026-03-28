# FuelFinder AU

**Crowdsourced petrol availability tracker for Australia — know before you go.**

During fuel shortage crises, FuelFinder AU lets drivers report and find which petrol stations near them have fuel, what types are available, and how long the queues are — updated in real time by the community.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | React 18 + TypeScript 5 (strict) + Vite + RTK Query + React Router v6 + CSS Modules |
| Backend | .NET 8 Minimal API + EF Core 8 |
| Database | Azure SQL (Basic 5 DTU) |
| Cache | Azure Redis Cache (C0 250 MB) |
| Infrastructure | Azure App Service (B2 Linux), Static Web Apps, Key Vault, CDN, App Insights |
| CI/CD | GitHub Actions (ci.yml, cd.yml, infra.yml) |

---

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) — `az --version` ≥ 2.50
- [Node.js 20](https://nodejs.org/) — `node --version`
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — `dotnet --version`
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) — running locally for API container builds

---

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/YOUR_ORG/fuelfinder-au.git
cd fuelfinder-au
```

### 2. Create an Azure Service Principal and add GitHub secrets

Create the Service Principal and generate credentials:

```bash
az ad sp create-for-rbac \
  --name "fuelfinder-au-github-actions" \
  --role Contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP_NAME> \
  --sdk-auth
```

Copy the entire JSON output. Add the following secrets to your GitHub repository under **Settings → Secrets and variables → Actions**:

| Secret | Value |
|---|---|
| `AZURE_CREDENTIALS` | Full JSON output from `az ad sp create-for-rbac --sdk-auth` above |
| `AZURE_RESOURCE_GROUP` | Name of your Azure resource group (e.g. `fuelfinder-rg`) |
| `ACR_NAME` | Azure Container Registry name — set to `fuelfindercr` (or your baseName + `cr`) |
| `DB_CONNECTION_STRING` | Leave blank for now — populated in step 4 |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Leave blank for now — populated after first infra deploy |
| `GOOGLE_MAPS_API_KEY` | Your Google Maps JavaScript API key |

> **Create the resource group first if it doesn't exist:**
> ```bash
> az group create --name fuelfinder-rg --location australiaeast
> ```

### 3. Push to main → infra.yml provisions all Azure resources

Any push to `main` that changes files under `infra/` will trigger `infra.yml`, which runs:

```bash
az deployment group create \
  --resource-group fuelfinder-rg \
  --template-file infra/main.bicep \
  --parameters @infra/parameters.json
```

This creates: App Service Plan + App Service + ACR + SQL Server + SQL Database + Redis + Static Web App + Key Vault + App Insights + CDN.

Monitor the deployment in the GitHub Actions tab or:

```bash
az deployment group list --resource-group fuelfinder-rg --output table
```

### 4. Copy DB_CONNECTION_STRING → add to GitHub secrets

After the SQL Server is provisioned:

```bash
# Get the connection string (replace values as needed)
az sql db show-connection-string \
  --server fuelfindersql \
  --name fuefinderdb \
  --client ado.net
```

Add this as the `DB_CONNECTION_STRING` secret in GitHub. Also update the `SqlConnectionString` secret in Key Vault:

```bash
az keyvault secret set \
  --vault-name fuelfinder-kv \
  --name SqlConnectionString \
  --value "<your-connection-string>"
```

Repeat for `RedisConnectionString` (get from Azure Portal → Redis → Access keys).

### 5. Push any change to main → CD deploys the app

Once all secrets are set, push to `main`. The `cd.yml` workflow will:

1. Build and push the Docker image to ACR
2. Run EF Core database migrations
3. Deploy the API container to App Service
4. Build and deploy the React frontend to Static Web Apps
5. Smoke-test the `/health` endpoint

---

## Local Development

### API

```bash
cd src/FuelFinder.Api
dotnet run
# API available at https://localhost:5001 / http://localhost:5000
```

Ensure your `appsettings.Development.json` has `ConnectionStrings__SqlConnection` set to a local SQL or Azure SQL connection string.

### Frontend

```bash
cd src/web
npm install
npm run dev
# Dev server at http://localhost:5173
# Vite proxies /api → http://localhost:5000 automatically
```

TypeScript strict mode is enforced — `npm run build` will fail on type errors.

---

## Contributing

- **Branch naming:** `phase/N-short-description` (e.g. `phase/1-api-scaffold`, `phase/4-home-ui`)
- All changes to `main` require a pull request — direct pushes are blocked.
- CI must pass (lint, type-check, tests, build) before merging.
- Tag PRs with the relevant phase label.
