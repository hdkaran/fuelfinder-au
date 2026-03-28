// Azure Static Web Apps for FuelFinder AU frontend (React/Vite)
// Free tier — includes global CDN, CI/CD integration, and custom domains.

@description('Base name prefix for resource names')
param baseName string

@description('Azure region for the Static Web App resource (metadata only — content is globally distributed)')
param location string

@description('Resource tags')
param tags object

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

output defaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
