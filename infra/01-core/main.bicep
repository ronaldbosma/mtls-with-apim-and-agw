//=============================================================================
// mTLS with API Management - Core layer
//=============================================================================

targetScope = 'subscription'

//=============================================================================
// Imports
//=============================================================================

import { getResourceName, generateInstanceId } from '../99-shared/naming-conventions.bicep'
import { getTemplateTags } from '../99-shared/helpers.bicep'
import { apimSkuType, appInsightsSettingsType, applicationGatewayMtlsModeType } from '../99-shared/settings.bicep'

//=============================================================================
// Parameters
//=============================================================================

@minLength(1)
@description('Location to use for all resources')
param location string

@minLength(1)
@maxLength(32)
@description('The name of the environment to deploy to')
param environmentName string

@description('Whether to include the Application Gateway in the deployment')
@metadata({
  azd: {
    default: true
  }
})
param includeApplicationGateway bool

// The following parameters are not used in this layer, but in the platform and application layers.
// They are defined here so the user is asked for them at the start of deployment.
// Simplify the deployment experience by avoiding asking for the same parameters multiple times in different layers.

@description('The mode to use for mTLS on the Application Gateway. This value is ignored if the Application Gateway is not included in the deployment.')
@metadata({
  azd: {
    default: 'Strict'
  }
})
param applicationGatewayMtlsMode applicationGatewayMtlsModeType

@description('The SKU of the API Management service to deploy. Consumption is not supported because setting "enableClientCertificate" to true makes mTLS mandatory for all APIs, breaking some scenarios.')
@metadata({
  azd: {
    default: 'BasicV2'
  }
})
param apiManagementSku apimSkuType

@description('Indicates whether the Protected API should validate the certificate chain of the client certificate.')
@metadata({
  azd: {
    default: false
  }
})
param validateCertificateChainInProtectedApi bool

//=============================================================================
// Variables
//=============================================================================

// Generate an instance ID to ensure unique resource names
var instanceId string = generateInstanceId(environmentName, location)

var resourceGroupName string = getResourceName('resourceGroup', environmentName, location, instanceId)

var agwPublicIpAddressName string = getResourceName('publicIpAddress', environmentName, location, 'agw-${instanceId}')

var appInsightsSettings appInsightsSettingsType = {
  appInsightsName: getResourceName('applicationInsights', environmentName, location, instanceId)
  logAnalyticsWorkspaceName: getResourceName('logAnalyticsWorkspace', environmentName, location, instanceId)
  retentionInDays: 30
}

var keyVaultName string = getResourceName('keyVault', environmentName, location, instanceId)

var tags { *: string } = getTemplateTags(environmentName)

//=============================================================================
// Resources
//=============================================================================

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module agwPublicIpAddress 'modules/public-ip-address.bicep' = if (includeApplicationGateway) {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    publicIpAddressName: agwPublicIpAddressName
  }
}

module appInsights 'modules/app-insights.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    appInsightsSettings: appInsightsSettings
  }
}

module keyVault 'modules/key-vault.bicep' = {
  scope: resourceGroup
  params: {
    location: location
    tags: tags
    keyVaultName: keyVaultName
  }
}

module assignRolesToDeployer '../99-shared/assign-roles-to-principal.bicep' = {
  scope: resourceGroup
  params: {
    principalId: deployer().objectId
    isAdmin: true
    appInsightsName: appInsightsSettings.appInsightsName
    keyVaultName: keyVaultName
  }
  dependsOn: [
    appInsights
    keyVault
  ]
}

//=============================================================================
// Outputs
//=============================================================================

// Return environment details
output AZURE_ENV_INSTANCE_ID string = instanceId

// Return the names of the resources
output AZURE_APPLICATION_GATEWAY_PUBLIC_IP_ADDRESS_NAME string = includeApplicationGateway ? agwPublicIpAddressName : ''
output AZURE_APPLICATION_INSIGHTS_NAME string = appInsightsSettings.appInsightsName
output AZURE_KEY_VAULT_NAME string = keyVaultName
output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = appInsightsSettings.logAnalyticsWorkspaceName
output AZURE_RESOURCE_GROUP string = resourceGroupName

// Return resource endpoints
output AZURE_APPLICATION_GATEWAY_PUBLIC_IP_ADDRESS_VALUE string = agwPublicIpAddress.?outputs.ipAddress ?? ''
output AZURE_KEY_VAULT_URI string = keyVault.outputs.vaultUri

// Return which services are included in the deployment
output INCLUDE_APPLICATION_GATEWAY bool = includeApplicationGateway

// Return settings
output AZURE_API_MANAGEMENT_SKU string = apiManagementSku
output AZURE_APPLICATION_GATEWAY_MTLS_MODE string = includeApplicationGateway ? applicationGatewayMtlsMode : ''
output VALIDATE_CERTIFICATE_CHAIN_IN_PROTECTED_API bool = validateCertificateChainInProtectedApi
