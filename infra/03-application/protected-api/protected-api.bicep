//=============================================================================
// Protected API in API Management
//=============================================================================

//=============================================================================
// Parameters
//=============================================================================

@description('The name of the API Management service')
param apiManagementServiceName string

@description('Whether to include the Application Gateway in the deployment')
param includeApplicationGateway bool

@description('Indicates whether the API should validate the certificate chain of the client certificate.')
param validateCertificateChain bool

//=============================================================================
// Existing resources
//=============================================================================

resource apiManagementService 'Microsoft.ApiManagement/service@2025-03-01-preview' existing = {
  name: apiManagementServiceName
}

//=============================================================================
// Resources
//=============================================================================

// Named Values

resource validateCertificateChainNamedValue 'Microsoft.ApiManagement/service/namedValues@2025-03-01-preview' = {
  name: 'validate-certificate-chain'
  parent: apiManagementService
  properties: {
    displayName: 'validate-certificate-chain'
    value: toLower(string(validateCertificateChain))
  }
}

// Add Trusted Client Certificates to API Management
// NOTE: The 'Unprotected API' client certificate is also trusted, but it's already added to API Management in ../unprotected-api/unprotected-api.bicep referencing Key Vault.
//       Adding it here as well (without the private key) would cause a 'duplicate certificate' deployment error.

resource validClientClientCertificate 'Microsoft.ApiManagement/service/certificates@2025-03-01-preview' = {
  name: 'valid-client-client-certificate'
  parent: apiManagementService
  properties: {
    data: loadTextContent('../../../self-signed-certificates/certificates/dev-valid-client.without-markers.cer')
  }
}

// API

resource protectedApi 'Microsoft.ApiManagement/service/apis@2025-03-01-preview' = {
  name: 'protected-api'
  parent: apiManagementService
  properties: {
    displayName: 'Protected API'
    path: 'protected'
    protocols: [
      'https'
    ]
    subscriptionRequired: false // API is protected with mTLS
  }
}

// Operation to validate client certificate using validate-client-certificate policy
resource validateUsingPolicyOperation 'Microsoft.ApiManagement/service/apis/operations@2025-03-01-preview' = {
  name: 'validate-using-policy'
  parent: protectedApi
  properties: {
    displayName: 'Validate (using policy)'
    description: 'Validates client certificate using validate-client-certificate policy'
    method: 'GET'
    urlTemplate: '/validate-using-policy'
  }

  resource policies 'policies' = {
    name: 'policy'
    properties: {
      format: 'rawxml'
      value: loadTextContent('./validate-using-policy.operation.xml')
    }
    dependsOn: [
      validateCertificateChainNamedValue
    ]
  }
}

// Operation to validate client certificate using context.Request.Certificate property
resource validateUsingContextOperation 'Microsoft.ApiManagement/service/apis/operations@2025-03-01-preview' = {
  name: 'validate-using-context'
  parent: protectedApi
  properties: {
    displayName: 'Validate (using context)'
    description: 'Validates client certificate using the context.Request.Certificate property'
    method: 'GET'
    urlTemplate: '/validate-using-context'
  }

  resource policies 'policies' = {
    name: 'policy'
    properties: {
      format: 'rawxml'
      value: loadTextContent('./validate-using-context.operation.xml')
    }
    dependsOn: [
      validateCertificateChainNamedValue
    ]
  }
}

// Operation to validate client certificate received from Application Gateway
resource validateFromAgwOperation 'Microsoft.ApiManagement/service/apis/operations@2025-03-01-preview' = if (includeApplicationGateway) {
  name: 'validate-from-agw'
  parent: protectedApi
  properties: {
    displayName: 'Validate (from Application Gateway)'
    description: 'Validates client certificate received from Application Gateway'
    method: 'GET'
    urlTemplate: '/validate-from-agw'
  }

  resource policies 'policies' = {
    name: 'policy'
    properties: {
      format: 'rawxml'
      value: loadTextContent('./validate-from-agw.operation.xml')
    }
    dependsOn: [
      validateCertificateChainNamedValue
    ]
  }
}
