// FuelFinder AU — Main Bicep deployment orchestrator
// Deploys all Azure resources for the fuelfinder-au application.
// Usage: az deployment group create --resource-group <rg> --template-file master.bicep --parameters @parameters.json

@description('Base name prefix used for all resource names (e.g. "fuelfinder")')
param baseName string = 'fuelfinder'

@description('Azure region to deploy all resources into')
param location string = 'eastasia'

@description('Environment tag value')
param environment string = 'prod'

@description('Optional custom domain for the frontend SWA (e.g. "fuelstock.com.au"). Leave empty to use Azure default hostnames.')
param customDomain string = ''

@description('Optional custom domain for the API (e.g. "api.fuelstock.com.au"). DNS CNAME must exist first.')
param customDomainApi string = ''

@description('SWA domain ownership validation token. Get from Azure Portal after first deploy.')
param swaValidationToken string = ''

@description('SQL administrator password. Required on every deploy to prevent Bicep from resetting it to a new random value.')
@secure()
param sqlAdminPassword string

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

// ---------- SQL Server + Database ----------
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    baseName: baseName
    location: location
    tags: tags
    administratorLoginPassword: sqlAdminPassword
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
// Deployed before Key Vault so we can pass the managed identity principal ID.
module appService 'modules/appService.bicep' = {
  name: 'appService'
  params: {
    baseName: baseName
    location: location
    tags: tags
    appInsightsConnectionString: appInsights.outputs.connectionString
    keyVaultName: '${baseName}-kv'  // Derived name — avoids circular dependency with keyVault module
    customDomainApi: customDomainApi
    // CORS origins — always allow the SWA default hostname; add custom domain when set
    allowedOrigins: empty(customDomain)
      ? ['https://${staticWebApp.outputs.defaultHostname}']
      : [
          'https://${staticWebApp.outputs.defaultHostname}'
          'https://${customDomain}'
          'https://www.${customDomain}'
        ]
  }
}

// ---------- Key Vault ----------
// Deployed after App Service (to get principalId), SQL, and Redis (to get real connection strings).
// listKeys() retrieves the Redis primary access key inline during the Bicep deployment.
module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    baseName: baseName
    location: location
    tags: tags
    appServicePrincipalId: appService.outputs.principalId
    sqlFqdn: sql.outputs.sqlServerFqdn
    sqlDatabaseName: sql.outputs.sqlDatabaseName
    sqlAdminLogin: 'fueladmin'
    sqlAdminPassword: sqlAdminPassword
    redisHostName: redis.outputs.redisHostName
    redisPrimaryKey: listKeys(resourceId('Microsoft.Cache/Redis', '${baseName}-redis'), '2023-04-01').primaryKey
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

// ---------- Azure DNS Zone ----------
// Only deployed when a custom domain is configured.
// Outputs the 4 Azure nameservers to set at your registrar (VentraIP / Crazy Domains).
module dns 'modules/dns.bicep' = if (!empty(customDomain)) {
  name: 'dns'
  params: {
    apexDomain: customDomain
    staticWebAppHostname: staticWebApp.outputs.defaultHostname
    staticWebAppId: staticWebApp.outputs.staticWebAppId
    appServiceHostname: replace(appService.outputs.appServiceUrl, 'https://', '')
    swaValidationToken: swaValidationToken
    tags: tags
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
output dnsNameServers array = !empty(customDomain) ? dns.outputs.nameServers : []
output registrarInstructions string = !empty(customDomain) ? dns.outputs.registrarInstructions : 'No custom domain configured'
