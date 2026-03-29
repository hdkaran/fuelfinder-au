// Azure Static Web Apps for FuelFinder AU frontend (React/Vite)
// Free tier — includes global CDN, CI/CD integration, and custom domains.
//
// Custom domain (optional):
//   Set customDomain to your hostname (e.g. "fuelfinder.com.au").
//   Add a CNAME/ALIAS record pointing customDomain → defaultHostname output.
//   SWA validates domain ownership automatically using TXT records.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region for the Static Web App resource (metadata only — content is globally distributed)')
param location string

@description('Resource tags')
param tags object

@description('Optional custom domain hostname (e.g. "fuelfinder.com.au"). Leave empty to use the Azure default hostname.')
param customDomain string = ''

// Static Web App — Free tier
resource staticWebApp 'Microsoft.Web/staticSites@2022-09-01' = {
  name: '${baseName}-web'
  location: location
  tags: tags
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    buildProperties: {
      appLocation: 'src/web'         // Root of the React app
      outputLocation: 'dist'         // Vite build output
      appBuildCommand: 'npm run build'
    }
  }
}

// Custom domain binding — only created when customDomain param is non-empty.
// Pre-requisite: DNS record must exist before this resource is created.
resource swaCustomDomain 'Microsoft.Web/staticSites/customDomains@2022-09-01' = if (!empty(customDomain)) {
  parent: staticWebApp
  name: customDomain
  properties: {}
}

output defaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
