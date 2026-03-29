// Azure SQL Server + Database for FuelFinder AU
// Basic tier, 5 DTU — suitable for MVP load.
// Firewall rule allows all Azure services to connect.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

@description('SQL administrator login username')
param administratorLogin string = 'fueladmin'

@description('SQL administrator login password. Must be supplied on every deploy — never left as a default — to avoid Bicep resetting the password on re-deploy.')
@secure()
param administratorLoginPassword string

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2022-11-01-preview' = {
  name: '${baseName}sql'
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Firewall rule — allow all Azure services (0.0.0.0 → 0.0.0.0 is the Azure-services magic range)
resource sqlFirewallAzureServices 'Microsoft.Sql/servers/firewallRules@2022-11-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// SQL Database — Basic SKU, 5 DTU
resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-11-01-preview' = {
  parent: sqlServer
  name: '${baseName}db'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648  // 2 GB max for Basic tier
    zoneRedundant: false
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
  }
}

output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
