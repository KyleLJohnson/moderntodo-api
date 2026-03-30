using './main.bicep'

param environmentName = readEnvironmentVariable('AZURE_ENV_NAME', 'moderntodo-api')
param location = readEnvironmentVariable('AZURE_LOCATION', 'eastus2')
// Optionally set staticWebAppUrl to the deployed frontend URL to scope CORS:
// param staticWebAppUrl = 'https://your-swa-hostname.azurestaticapps.net'
