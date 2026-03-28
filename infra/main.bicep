// FuelFinder AU — Main Bicep deployment orchestrator
// Deploys all Azure resources for the fuelfinder-au application.
// Usage: az deployment group create --resource-group <rg> --template-file main.bicep --parameters @parameters.json

@description('Base name prefix used for all resource names (e.g. "fuelfinder")')
param baseName string = 'fuelfinder'

@description('Azure region to deploy all resources into')
param location string = 'australiaeast'

@description('Environment tag value')
param environment string = 'prod'

// Shared tags applied to every resource
var tags = {
  project: 'fuelfinder-au'
  environment: environment
}

// ---------- App Insights + Log Analytics ----------
module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

// ---------- Key Vault ----------
module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

// ---------- SQL Server + Database ----------
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

// ---------- Redis Cache ----------
module redis 'modules/redis.bicep' = {
  name: 'redis'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

// ---------- App Service (API) ----------
module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    baseName: baseName
    location: location
    tags: tags
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultName: keyVault.outputs.keyVaultName
  }
}

// ---------- Key Vault role assignment (must come after both KV and App Service) ----------
module keyVaultRoleAssignment 'modules/keyVault.bicep' = {
  name: 'keyVaultRoleAssignment'
  params: {
    baseName: baseName
    location: location
    tags: tags
    appServicePrincipalId: appService.outputs.principalId
  }
}

// ---------- Static Web App ----------
module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

// ---------- CDN ----------
module cdn 'modules/cdn.bicep' = {
  name: 'cdn'
  params: {
    baseName: baseName
    location: location
    tags: tags
    staticWebAppHostname: staticWebApp.outputs.defaultHostname
  }
}

// ---------- Outputs ----------
output appServiceUrl string = appService.outputs.appServiceUrl
output staticWebAppUrl string = staticWebApp.outputs.defaultHostname
output acrLoginServer string = appService.outputs.acrLoginServer
output keyVaultName string = keyVault.outputs.keyVaultName
