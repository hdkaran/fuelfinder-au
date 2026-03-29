// FuelFinder AU — Main Bicep deployment orchestrator
// Deploys all Azure resources for the fuelfinder-au application.
// Usage: az deployment group create --resource-group <rg> --template-file master.bicep --parameters @parameters.json

@description('Base name prefix used for all resource names (e.g. "fuelfinder")')
param baseName string = 'fuelfinder'

@description('Azure region to deploy all resources into')
param location string = 'eastasia'

@description('Environment tag value')
param environment string = 'prod'

@description('Optional custom domain (e.g. "fuelfinder.com.au"). Leave empty to use Azure default hostnames.')
param customDomain string = ''

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
    appServicePrincipalId: '' // empty on first deploy — App Service doesn't exist yet
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

// ---------- Static Web App ----------
// Note: Static Web Apps only available in specific regions — using eastasia (closest to AU)
module staticWebApp 'modules/staticWebApp.bicep' = {
  name: 'staticWebApp'
  params: {
    baseName: baseName
    location: 'eastasia'
    tags: tags
    customDomain: customDomain
  }
}

// ---------- NOTE: CDN ----------
// Azure CDN Standard from Microsoft (classic) no longer accepts new profile creation.
// Static Web Apps Free tier already ships with built-in global CDN, so a separate
// CDN profile is not required for this app.
// If you need Azure Front Door Standard for custom WAF/routing rules in the future,
// add a front-door.bicep module and wire it here.

// ---------- Outputs ----------
output appServiceUrl string = appService.outputs.appServiceUrl
output staticWebAppUrl string = 'https://${staticWebApp.outputs.defaultHostname}'
output acrLoginServer string = appService.outputs.acrLoginServer
output keyVaultName string = keyVault.outputs.keyVaultName
