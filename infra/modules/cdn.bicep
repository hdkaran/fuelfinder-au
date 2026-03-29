// Azure CDN Profile + Endpoint for FuelFinder AU
// Standard Microsoft tier — fronts the Static Web App for global edge caching.
//
// Custom domain (optional):
//   1. Set customDomain param to your apex or subdomain (e.g. "fuelfinder.com.au" or "www.fuelfinder.com.au")
//   2. Add a CNAME record pointing customDomain → cdnEndpointHostname output
//   3. Re-run this Bicep to provision the CDN custom domain resource and enable managed HTTPS

@description('Base name prefix for resource names')
param baseName string

@description('Azure region — not used directly (CDN resources are always global) but kept for consistency with other modules')
#disable-next-line no-unused-params
param location string

@description('Resource tags')
param tags object

@description('Default hostname of the Static Web App — used as the CDN origin')
param staticWebAppHostname string

@description('Optional custom domain hostname (e.g. "fuelfinder.com.au"). Leave empty to use the default CDN hostname.')
param customDomain string = ''

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
        {
          // SPA fallback — rewrite all non-file paths to /index.html so React
          // Router handles client-side navigation correctly from the CDN edge.
          name: 'SpaFallback'
          order: 2
          conditions: [
            {
              name: 'UrlFileExtension'
              parameters: {
                typeName: 'DeliveryRuleUrlFileExtensionMatchConditionParameters'
                operator: 'LessThan'
                negateCondition: false
                matchValues: ['1']
                transforms: []
              }
            }
          ]
          actions: [
            {
              name: 'UrlRewrite'
              parameters: {
                typeName: 'DeliveryRuleUrlRewriteActionParameters'
                sourcePattern: '/'
                destination: '/index.html'
                preserveUnmatchedPath: false
              }
            }
          ]
        }
      ]
    }
  }
}

// Custom domain — only provisioned when customDomain param is non-empty.
// Pre-requisite: CNAME record pointing customDomain → cdnEndpoint.properties.hostName
// must exist in DNS before this resource is created, or Azure validation will fail.
resource cdnCustomDomain 'Microsoft.Cdn/profiles/endpoints/customDomains@2023-05-01' = if (!empty(customDomain)) {
  parent: cdnEndpoint
  name: replace(customDomain, '.', '-')  // CDN resource name can't contain dots
  properties: {
    hostName: customDomain
  }
}

output cdnEndpointHostname string = cdnEndpoint.properties.hostName
output cdnEndpointName string = cdnEndpoint.name
output cdnProfileName string = cdnProfile.name
