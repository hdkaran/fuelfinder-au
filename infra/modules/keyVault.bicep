// Azure Key Vault for FuelFinder AU
// Stores secrets for SQL, Redis, and Google Maps API key.
// The App Service managed identity is granted the "Key Vault Secrets User" role.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('Principal ID of the App Service managed identity — used to grant secret access. Leave empty on first deploy.')
param appServicePrincipalId string = ''

// Key Vault — Standard SKU, RBAC-based access
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: '${baseName}-kv'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true  // Use RBAC instead of access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Placeholder secret — SQL Connection String
// TODO: Replace placeholder value with real connection string after SQL is provisioned.
resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'PLACEHOLDER_REPLACE_AFTER_DEPLOY'
    attributes: {
      enabled: true
    }
  }
}

// Placeholder secret — Redis Connection String
resource redisSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'RedisConnectionString'
  properties: {
    value: 'PLACEHOLDER_REPLACE_AFTER_DEPLOY'
    attributes: {
      enabled: true
    }
  }
}

// Placeholder secret — Google Maps API Key
resource googleMapsSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleMapsApiKey'
  properties: {
    value: 'PLACEHOLDER_REPLACE_AFTER_DEPLOY'
    attributes: {
      enabled: true
    }
  }
}

// RBAC: Grant App Service managed identity "Key Vault Secrets User" role
// Role definition ID for "Key Vault Secrets User" = 4633458b-17de-408a-b874-0445c86b69e6
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(appServicePrincipalId)) {
  name: guid(keyVault.id, appServicePrincipalId, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: appServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
