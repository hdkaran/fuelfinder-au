// App Service Plan + App Service (API) + Azure Container Registry for FuelFinder AU
// Runs the .NET 8 Minimal API as a Docker container on Linux B2.
// System-assigned managed identity is enabled to pull secrets from Key Vault.

@description('Base name prefix for resource names. Must be ≥5 chars — ACR names require a minimum length of 5.')
@minLength(5)
param baseName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('App Insights connection string — injected into app settings')
param appInsightsConnectionString string

@description('Key Vault name — used to construct Key Vault reference URIs in app settings')
param keyVaultName string

// Azure Container Registry — Basic SKU
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  // ACR names must be alphanumeric (no hyphens), 5–50 chars.
  // baseName must be ≥5 chars (e.g. "fuelfinder") to satisfy ACR minimum length.
  name: '${baseName}cr'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false  // Use managed identity to pull images, not admin credentials
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Disabled'
  }
}

// App Service Plan — B2 Linux
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${baseName}-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B2'
    tier: 'Basic'
    size: 'B2'
    family: 'B'
    capacity: 1
  }
  properties: {
    reserved: true  // Required for Linux plans
  }
}

// Unique 6-char suffix derived from the resource group ID — avoids global name collisions
// for App Service (names must be globally unique across all of Azure).
var uniqueSuffix = take(uniqueString(resourceGroup().id), 6)

// App Service — Docker container hosting the .NET API
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: '${baseName}-api-${uniqueSuffix}'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'  // Managed identity for Key Vault secret access
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/${baseName}-api-${uniqueSuffix}:latest'
      acrUseManagedIdentityCreds: true  // Pull from ACR using managed identity
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      // App settings — Key Vault references use the @Microsoft.KeyVault() syntax
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // Key Vault reference — resolves SqlConnectionString secret at runtime
          name: 'ConnectionStrings__SqlConnection'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=SqlConnectionString)'
        }
        {
          // Key Vault reference — resolves RedisConnectionString secret at runtime
          name: 'ConnectionStrings__Redis'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=RedisConnectionString)'
        }
        {
          // Key Vault reference — resolves GoogleMapsApiKey secret at runtime
          name: 'GoogleMaps__ApiKey'
          value: '@Microsoft.KeyVault(VaultName=${keyVaultName};SecretName=GoogleMapsApiKey)'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acr.properties.loginServer}'
        }
      ]
    }
  }
}

// Grant ACR Pull role to the App Service managed identity
// Role definition ID for "AcrPull" = 7f951dda-4ed3-4680-a7ca-43fe172d538d
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appService.id, '7f951dda-4ed3-4680-a7ca-43fe172d538d')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServiceName string = appService.name
output principalId string = appService.identity.principalId
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
