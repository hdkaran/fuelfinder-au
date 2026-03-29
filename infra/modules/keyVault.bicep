// Azure Key Vault for FuelFinder AU
// Stores secrets for SQL, Redis, and Google Maps API key.
// The App Service managed identity is granted the "Key Vault Secrets User" role.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('Principal ID of the App Service managed identity — used to grant secret access.')
param appServicePrincipalId string

@description('SQL Server fully qualified domain name (e.g. fuelfindersql.database.windows.net)')
param sqlFqdn string

@description('SQL database name')
param sqlDatabaseName string

@description('SQL administrator login username')
param sqlAdminLogin string

@description('SQL administrator login password — written to Key Vault as part of the connection string.')
@secure()
param sqlAdminPassword string

@description('Redis Cache hostname (e.g. fuelfinder-redis.redis.cache.windows.net)')
param redisHostName string

@description('Redis Cache primary access key')
@secure()
param redisPrimaryKey string

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

// SQL Connection String — constructed from SQL module outputs + admin password
resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'SqlConnectionString'
  properties: {
    value: 'Server=tcp:${sqlFqdn},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
    attributes: {
      enabled: true
    }
  }
}

// Redis Connection String — constructed from Redis module outputs + listKeys
resource redisSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'RedisConnectionString'
  properties: {
    value: '${redisHostName}:6380,password=${redisPrimaryKey},ssl=True,abortConnect=False'
    attributes: {
      enabled: true
    }
  }
}

// Google Maps API Key — set manually after deploy; placeholder used on first deploy only.
// Update via: az keyvault secret set --vault-name fuelfinder-kv --name GoogleMapsApiKey --value <key>
resource googleMapsSecret 'Microsoft.KeyVault/vaults/secrets@2023-02-01' = {
  parent: keyVault
  name: 'GoogleMapsApiKey'
  properties: {
    value: 'PLACEHOLDER_SET_MANUALLY'
    attributes: {
      enabled: true
    }
  }
}

// RBAC: Grant App Service managed identity "Key Vault Secrets User" role
// Role definition ID for "Key Vault Secrets User" = 4633458b-17de-408a-b874-0445c86b69e6
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
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
