# Azure Deployment Guide

This guide covers how to host **Marsville** on Azure so that locally-running team agents can connect to a shared game server during the challenge session.

---

## Architecture

| Component | Azure Service | Notes |
|---|---|---|
| `Marsville2` backend | App Service (B1 Linux, .NET 10) | Persistent process — in-memory state survives requests |
| `marsville-ui` frontend | Static Web Apps (Free) | Vite-built SPA, CDN-backed |
| Agent connectivity | Direct HTTPS to App Service | Teams supply `X-Registration-Key` header when registering |

> ⚠️ **Single instance only.** Do not scale out the App Service — the game state lives in process memory. If the instance restarts, all state is lost (teams must re-register).

---

## One-time Infrastructure Setup (Azure CLI)

```bash
RESOURCE_GROUP=marsville-rg
LOCATION=westeurope
APP_SERVICE_PLAN=marsville-plan
WEBAPP_NAME=marsville-backend       # must be globally unique
SWA_NAME=marsville-ui               # must be globally unique

# Resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# App Service plan (B1 Linux)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --sku B1 --is-linux

# Web App (.NET 10)
az webapp create \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:10.0"

# Enable WebSockets (required for SignalR)
az webapp config set \
  --name $WEBAPP_NAME \
  --resource-group $RESOURCE_GROUP \
  --web-sockets-enabled true

# Static Web App (frontend)
az staticwebapp create \
  --name $SWA_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Free
```

---

## App Service Configuration

Set these in Azure Portal → App Service → Configuration → Application settings,  
or via CLI:

```bash
az webapp config appsettings set \
  --name $WEBAPP_NAME --resource-group $RESOURCE_GROUP \
  --settings \
    AdminPassword="<strong-password>" \
    RegistrationSecret="<shared-with-teams>" \
    "CorsOrigins__0"="https://<swa-name>.azurestaticapps.net" \
    ASPNETCORE_ENVIRONMENT="Production"
```

| Setting | Description |
|---|---|
| `AdminPassword` | Password for admin endpoints (`X-Admin-Password` header). Keep secret. |
| `RegistrationSecret` | Shared secret teams include as `X-Registration-Key` when registering. Leave empty to disable gating. |
| `CorsOrigins__0` | The Static Web Apps URL (e.g. `https://marsville-ui.azurestaticapps.net`). |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |

---

## GitHub Actions Secrets & Variables

In the repository → Settings → Secrets and variables → Actions:

| Name | Type | Value |
|---|---|---|
| `AZURE_WEBAPP_PUBLISH_PROFILE` | Secret | Download from Azure Portal → App Service → Get publish profile |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Secret | From Azure Portal → Static Web App → Manage deployment token |
| `AZURE_WEBAPP_NAME` | Variable | Your App Service name (e.g. `marsville-backend`) |
| `VITE_API_BASE_URL` | Variable | Full App Service origin, e.g. `https://marsville-backend.azurewebsites.net` |

Once set, push to `main` to trigger both workflows automatically.

---

## Participant Agent Setup

Teams run their locally-built agent pointing at the Azure backend:

```bash
cd MarsvilleAgent
dotnet run -- <TeamName> https://marsville-backend.azurewebsites.net 1 <RegistrationKey>
```

Arguments:
1. `TeamName` — your team's display name
2. Server URL — the App Service HTTPS URL (provided by organiser)
3. Agent count — number of parallel agents (usually `1`)
4. `RegistrationKey` — the shared secret (provided by organiser, omit if not configured)

The agent will automatically include `X-Registration-Key` when registering, then use its
`X-Player-Token` for all subsequent game actions.

---

## Re-starting a Challenge Session

If you need to reset state between challenge rounds without redeploying:

1. Use the Admin Panel in the UI (or call `POST /api/admin/leaderboard/reset`) to clear scores.
2. If the App Service instance was recycled (e.g., after a deploy), all teams must re-register — the agent handles this automatically when it receives a `401`.
