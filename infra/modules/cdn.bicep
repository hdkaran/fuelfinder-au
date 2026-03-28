// Azure CDN Profile + Endpoint for FuelFinder AU
// Standard Microsoft tier — fronts the Static Web App for global edge caching.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region — not used directly (CDN resources are always global) but kept for consistency with other modules')
#disable-next-line no-unused-params
param location string

@description('Resource tags')
param tags object

@description('Default hostname of the Static Web App — used as the CDN origin')
param staticWebAppHostname string

// CDN Profile — Standard Microsoft tier
resource cdnProfile 'Microsoft.Cdn/profiles@2023-05-01' = {
  name: '${baseName}-cdn'
  location: 'global'  // CDN profiles are always global regardless of param
  tags: tags
  sku: {
    name: 'Standard_Microsoft'
  }
}

// CDN Endpoint — proxies requests to the Static Web App origin
resource cdnEndpoint 'Microsoft.Cdn/profiles/endpoints@2023-05-01' = {
  parent: cdnProfile
  name: '${baseName}-endpoint'
  location: 'global'
  tags: tags
  properties: {
    originHostHeader: staticWebAppHostname
    isHttpAllowed: false   // HTTPS only
    isHttpsAllowed: true
    queryStringCachingBehavior: 'IgnoreQueryString'
    origins: [
      {
        name: 'static-web-app-origin'
        properties: {
          hostName: staticWebAppHostname
          httpsPort: 443
          originHostHeader: staticWebAppHostname
        }
      }
    ]
    deliveryPolicy: {
      rules: [
        {
          // Redirect HTTP → HTTPS
          name: 'EnforceHTTPS'
          order: 1
          conditions: [
            {
              name: 'RequestScheme'
              parameters: {
                typeName: 'DeliveryRuleRequestSchemeConditionParameters'
                operator: 'Equal'
                matchValues: ['HTTP']
              }
            }
          ]
          actions: [
            {
              name: 'UrlRedirect'
              parameters: {
                typeName: 'DeliveryRuleUrlRedirectActionParameters'
                redirectType: 'PermanentRedirect'
                destinationProtocol: 'Https'
              }
            }
          ]
        }
      ]
    }
  }
}

output cdnEndpointHostname string = cdnEndpoint.properties.hostName
output cdnEndpointName string = cdnEndpoint.name
