// Azure Cache for Redis for FuelFinder AU
// C0 Basic 250 MB — suitable for MVP session/response caching.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region')
param location string

@description('Resource tags')
param tags object

// Redis Cache — C0 Basic (250 MB, no SLA, no replication)
resource redisCache 'Microsoft.Cache/redis@2023-04-01' = {
  name: '${baseName}-redis'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'Basic'
      family: 'C'
      capacity: 0  // C0 = 250 MB
    }
    enableNonSslPort: false   // TLS only
    minimumTlsVersion: '1.2'
    redisVersion: '6'
    publicNetworkAccess: 'Enabled'
    redisConfiguration: {
      'maxmemory-policy': 'allkeys-lru'
    }
  }
}

output redisHostName string = redisCache.properties.hostName
output redisSslPort int = redisCache.properties.sslPort
output redisId string = redisCache.id
