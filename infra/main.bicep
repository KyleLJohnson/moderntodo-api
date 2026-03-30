targetScope = 'resourceGroup'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('Environment name prefix used across all resource names.')
param environmentName string

@description('Unique suffix to avoid global name conflicts.')
param resourceToken string = uniqueString(resourceGroup().id)

@description('Allowed origin for CORS on the Function App. Set to the Static Web App URL in production.')
param staticWebAppUrl string = '*'

var abbrs = {
  function: 'func'
  hostingPlan: 'plan'
  storage: 'st'
  appInsights: 'appi'
  logAnalytics: 'log'
}

var tags = {
  'azd-env-name': environmentName
}

// Log Analytics workspace (required by Application Insights)
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${abbrs.logAnalytics}${environmentName}${resourceToken}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${abbrs.appInsights}${environmentName}${resourceToken}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Storage account — shared by Functions runtime AND SQLite blob persistence
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: '${abbrs.storage}${resourceToken}'
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    accessTier: 'Hot'
  }
}

// Consumption plan for Azure Functions
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${abbrs.hostingPlan}${environmentName}${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: {}
}

// Azure Functions app (isolated .NET 8)
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: '${abbrs.function}${environmentName}${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  kind: 'functionapp'
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      cors: {
        allowedOrigins: [staticWebAppUrl]
        supportCredentials: false
      }
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'BLOB_CONNECTION_STRING', value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: '1' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
      ]
    }
  }
}

output FUNCTION_APP_HOSTNAME string = functionApp.properties.defaultHostName
output FUNCTION_APP_RESOURCE_ID string = functionApp.id
output AZURE_STORAGE_ACCOUNT_NAME string = storage.name
