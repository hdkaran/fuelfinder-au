// Azure DNS Zone for fuelstock.com.au
// Creates the DNS zone and all required records so you can point your registrar's
// nameservers at Azure DNS and have everything resolve automatically.
//
// Deployment order:
//   Step 1 — Deploy without customDomain params to get Azure default hostnames
//   Step 2 — Deploy this module (nameservers are now known)
//   Step 3 — Update registrar nameservers to the 4 Azure nameservers in the output
//   Step 4 — Re-deploy master.bicep with customDomain + customDomainApi set
//
// Apex domain (fuelstock.com.au) → Static Web App
//   Azure DNS does not support CNAME at the apex. We use an alias A record that
//   points directly to the SWA resource by ID. Azure resolves this automatically.
//
// www subdomain (www.fuelstock.com.au) → Static Web App
//   Standard CNAME → SWA default hostname
//
// api subdomain (api.fuelstock.com.au) → App Service
//   Standard CNAME → App Service default hostname
//
// SWA domain validation TXT record
//   When you add a custom domain in the SWA portal (or via Bicep), Azure generates a
//   validation token. The 'asuid.fuelstock.com.au' TXT record must match that token.
//   After first deploy, get the token from Azure Portal → Static Web App → Custom domains,
//   then uncomment and set the 'validationToken' param below and re-deploy.

@description('Apex domain to create the zone for')
param apexDomain string = 'fuelstock.com.au'

@description('Default hostname of the Static Web App (e.g. xyz.azurestaticapps.net)')
param staticWebAppHostname string

@description('Default hostname of the App Service (e.g. fuelfinder-api-abc123.azurewebsites.net)')
param appServiceHostname string

@description('Resource ID of the Static Web App — used for the apex alias A record')
param staticWebAppId string

@description('Resource tags')
param tags object

@description('SWA domain ownership validation token. Get from Azure Portal after first deploy, then set and re-deploy.')
param swaValidationToken string = ''

// ── DNS Zone ─────────────────────────────────────────────────────────────────

resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' = {
  name: apexDomain
  location: 'global'
  tags: tags
  properties: {
    zoneType: 'Public'
  }
}

// ── Apex domain (fuelstock.com.au) → Static Web App ─────────────────────────
// Azure DNS alias record — resolves the SWA IP automatically without hardcoding.
// No TTL management needed; Azure updates the IP if SWA infrastructure changes.

resource apexAliasRecord 'Microsoft.Network/dnsZones/A@2018-05-01' = {
  parent: dnsZone
  name: '@'
  properties: {
    TTL: 300
    targetResource: {
      id: staticWebAppId
    }
  }
}

// ── www subdomain → Static Web App ───────────────────────────────────────────

resource wwwCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: 'www'
  properties: {
    TTL: 300
    CNAMERecord: {
      cname: staticWebAppHostname
    }
  }
}

// ── api subdomain → App Service ───────────────────────────────────────────────
// This CNAME must exist BEFORE the App Service custom domain binding is created,
// because Azure validates domain ownership via DNS during cert provisioning.

resource apiCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: 'api'
  properties: {
    TTL: 300
    CNAMERecord: {
      cname: appServiceHostname
    }
  }
}

// ── SWA domain validation TXT record ─────────────────────────────────────────
// Azure Static Web Apps requires a TXT record at asuid.{domain} to prove ownership
// before it will provision an SSL cert for the apex and www domains.
// Get the token value from: Azure Portal → Static Web App → Custom domains → Add → Copy token
// Then set the swaValidationToken param and re-deploy this module.

resource swaValidationTxt 'Microsoft.Network/dnsZones/TXT@2018-05-01' = if (!empty(swaValidationToken)) {
  parent: dnsZone
  name: 'asuid'
  properties: {
    TTL: 3600
    TXTRecords: [
      {
        value: [swaValidationToken]
      }
    ]
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────

output nameServers array = dnsZone.properties.nameServers
output dnsZoneId string = dnsZone.id

// Human-readable registrar instructions
output registrarInstructions string = 'Update your registrar nameservers to: ${join(dnsZone.properties.nameServers, ', ')}'
