//=============================================================================
// Unprotected API in API Management
//
// This API demonstrates how to call a backend service from API Management
// that is protected with mTLS.
//=============================================================================

//=============================================================================
// Parameters
//=============================================================================

@description('The name of the API Management service')
param apiManagementServiceName string

@description('The name of the Key Vault that contains the secrets and certificates')
param keyVaultName string

//=============================================================================
// Existing resources
//=============================================================================

resource apiManagementService 'Microsoft.ApiManagement/service@2025-03-01-preview' existing = {
  name: apiManagementServiceName
}

resource keyVault 'Microsoft.KeyVault/vaults@2026-02-01' existing = {
  name: keyVaultName
}

resource clientCertificateSecret 'Microsoft.KeyVault/vaults/secrets@2026-02-01' existing = {
  name: 'dev-unprotected-api'
  parent: keyVault
}

//=============================================================================
// Resources
//=============================================================================

// Client Certificate

resource clientCertificate 'Microsoft.ApiManagement/service/certificates@2025-03-01-preview' = {
  name: 'unprotected-api-client-certificate'
  parent: apiManagementService
  properties: {
    keyVault: {
      secretIdentifier: clientCertificateSecret.properties.secretUri
    }
  }
}

// Backend

resource protectedBackend 'Microsoft.ApiManagement/service/backends@2025-03-01-preview' = {
  parent: apiManagementService
  name: 'protected-backend'
  properties: {
    description: 'The protected backend. Forwards requests to the Protected API in the same API Management instance.'

    // Note: This configuration uses the public gateway URL for the backend.
    // For API Management instances running inside a VNet, you would typically use https://localhost/... as the backend URL.
    url: '${apiManagementService.properties.gatewayUrl}/protected'
    protocol: 'http'

    credentials: {
      // The client certificate will be used for authentication when calling the backend API.
      certificateIds: [
        clientCertificate.id
      ]

      // Note: The Host header configuration is only necessary when the backend URL is set to https://localhost.
      // For public gateway URLs, this configuration can be omitted.
      header: {
        Host: [parseUri(apiManagementService.properties.gatewayUrl).host]
      }
    }

    tls: {
      validateCertificateChain: true
      validateCertificateName: true
    }
  }
}

// API

resource unprotectedApi 'Microsoft.ApiManagement/service/apis@2025-03-01-preview' = {
  name: 'unprotected-api'
  parent: apiManagementService
  properties: {
    displayName: 'Unprotected API'
    path: 'unprotected'
    protocols: [
      'https'
    ]
    subscriptionRequired: false // API is unprotected, no subscription key required
  }

  resource policies 'policies' = {
    name: 'policy'
    properties: {
      format: 'rawxml'
      value: loadTextContent('unprotected-api.xml')
    }

    dependsOn: [
      protectedBackend
    ]
  }
}

// Operation that will forward all GET requests to the backend API
resource getOperation 'Microsoft.ApiManagement/service/apis/operations@2025-03-01-preview' = {
  name: 'get'
  parent: unprotectedApi
  properties: {
    displayName: 'Forward request to Protected API'
    description: 'Forwards all GET requests to the Protected API'
    method: 'GET'
    urlTemplate: '/{*path}'
    templateParameters: [
      {
        name: 'path'
        type: 'string'
        required: true
      }
    ]
  }
}
