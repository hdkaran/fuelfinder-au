# DNS Setup — fuelstock.com.au on GoDaddy

DNS is managed in GoDaddy. No Azure DNS zone required.

## Overview

```
fuelstock.com.au        →  Azure Static Web Apps  (frontend)
www.fuelstock.com.au    →  Azure Static Web Apps  (frontend)
api.fuelstock.com.au    →  Azure App Service       (backend API)
```

---

## Step 1 — First deploy (no custom domains)

Deploy the infra with `customDomain` and `customDomainApi` left **empty** in `parameters.json`.
This gives you the Azure default hostnames you need for the GoDaddy records.

```bash
az deployment group create \
  --resource-group fuelfinder-rg \
  --template-file infra/master.bicep \
  --parameters @infra/parameters.json \
  --parameters customDomain='' customDomainApi=''
```

Note the two output values:

```bash
# Get them from the deployment outputs
az deployment group show \
  --resource-group fuelfinder-rg \
  --name master \
  --query properties.outputs
```

You'll see something like:
```
staticWebAppHostname  →  agreeable-rock-abc123.azurestaticapps.net
appServiceHostname    →  fuelfinder-api-x4f2ab.azurewebsites.net
```

---

## Step 2 — Add DNS records in GoDaddy

GoDaddy Portal → **My Products → Domains → fuelstock.com.au → Manage DNS → Add Record**

Add these 4 records (replace hostnames with your actual outputs from Step 1):

| Type | Name | Value | TTL |
|------|------|-------|-----|
| `ALIAS` | `@` | `agreeable-rock-abc123.azurestaticapps.net` | 600 sec |
| `CNAME` | `www` | `agreeable-rock-abc123.azurestaticapps.net` | 1 hour |
| `CNAME` | `api` | `fuelfinder-api-x4f2ab.azurewebsites.net` | 1 hour |
| `TXT` | `asuid` | *(get from Step 3 below)* | 1 hour |

> **ALIAS vs A record:** GoDaddy supports "ALIAS" type for the apex `@` domain. If you don't see it, look for "ANAME". If neither is available, temporarily use an `A` record with the IP from `nslookup agreeable-rock-abc123.azurestaticapps.net` — but note this IP can change; switch to ALIAS as soon as possible.

---

## Step 3 — Get the SWA validation token

Azure needs to verify you own the domain before it will provision the SSL cert for `fuelstock.com.au`.

**Via Azure Portal:**
1. Azure Portal → Static Web Apps → `fuelfinder-web` → **Custom domains**
2. Click **+ Add**
3. Enter `fuelstock.com.au` → Click **Next**
4. Azure shows a validation token (a long string like `abc123def456...`)
5. Copy it

**Via Azure CLI:**
```bash
az staticwebapp hostname set \
  --name fuelfinder-web \
  --resource-group fuelfinder-rg \
  --hostname fuelstock.com.au \
  --validation-method dns-txt-token
```

Add the token as the `TXT asuid` record in GoDaddy (from Step 2 table above).

---

## Step 4 — Wait for DNS propagation

```bash
# Check apex
nslookup fuelstock.com.au

# Check api subdomain
nslookup api.fuelstock.com.au

# Or use dig
dig fuelstock.com.au
dig api.fuelstock.com.au CNAME
```

Propagation typically takes 5–30 minutes with GoDaddy's default TTL.

---

## Step 5 — Re-deploy with custom domain params

Once DNS resolves correctly, run the full deploy with the custom domain values set:

```bash
az deployment group create \
  --resource-group fuelfinder-rg \
  --template-file infra/master.bicep \
  --parameters @infra/parameters.json
```

This triggers:
- App Service hostname binding + free managed SSL cert for `api.fuelstock.com.au`
- SWA custom domain binding for `fuelstock.com.au` and `www.fuelstock.com.au`

Azure provisions the SSL certs automatically — takes ~10 minutes.

---

## Step 6 — Smoke test

```bash
# API health check
curl https://api.fuelstock.com.au/health
# Expected: {"status":"healthy"}

# Frontend
curl -I https://fuelstock.com.au
# Expected: HTTP/2 200

# www redirect
curl -I https://www.fuelstock.com.au
# Expected: HTTP/2 200 or 301 redirect to apex
```

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `SSL_ERROR_RX_RECORD_TOO_LONG` | SSL cert still provisioning — wait 10 min and retry |
| `CORS` error in browser console | Check App Service app settings have `AllowedOrigins__0=https://fuelstock.com.au` |
| `nslookup` returns GoDaddy IP | DNS hasn't propagated yet — wait and retry |
| SWA shows "Domain validation failed" | TXT `asuid` record missing or token incorrect — re-check Step 3 |
| App Service cert fails | CNAME for `api.fuelstock.com.au` not yet propagated — verify with `dig` first |
