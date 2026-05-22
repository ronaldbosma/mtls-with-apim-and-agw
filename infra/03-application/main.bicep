//=============================================================================
// mTLS with API Management - Application layer
//=============================================================================

targetScope = 'subscription'

//=============================================================================
// Parameters
//=============================================================================

@description('The name of the resource group in which to deploy the resources.')
param resourceGroupName string

@description('The name of the API Management service')
param apiManagementServiceName string

@description('The name of the Key Vault that contains the secrets and certificates')
param keyVaultName string

@description('Indicates whether the Protected API should validate the certificate chain of the client certificate.')
param validateCertificateChainInProtectedApi bool

//=============================================================================
// Existing Resources
//=============================================================================

resource resourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' existing = {
  name: resourceGroupName
}

//=============================================================================
// Resources
//=============================================================================

module protectedApi 'protected-api/protected-api.bicep' = {
  scope: resourceGroup
  params: {
    apiManagementServiceName: apiManagementServiceName
    validateCertificateChain: validateCertificateChainInProtectedApi
  }
}

module unprotectedApi 'unprotected-api/unprotected-api.bicep' = {
  scope: resourceGroup
  params: {
    apiManagementServiceName: apiManagementServiceName
    keyVaultName: keyVaultName
  }
}
