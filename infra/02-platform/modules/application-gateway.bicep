//=============================================================================
// Application Gateway
//=============================================================================

//=============================================================================
// Imports
//=============================================================================

import { applicationGatewaySettingsType } from '../../99-shared/settings.bicep'
import { tagsType } from '../../99-shared/types.bicep'
import { getApiManagementFqdn } from '../../99-shared/helpers.bicep'

//=============================================================================
// Parameters
//=============================================================================

@description('Location to use for all resources')
param location string = resourceGroup().location

@description('The tags to associate with the resource')
param tags tagsType

@description('The settings for the Application Gateway')
param applicationGatewaySettings applicationGatewaySettingsType

@description('The ID of the subnet to use for the API Management service')
param subnetId string

@description('The name of the API Management Service to use')
param apiManagementServiceName string

@description('The name of the App Insights instance to use')
param appInsightsName string

@description('The name of the Key Vault that contains the secrets and certificates')
param keyVaultName string

@description('The name of the Log Analytics workspace to use')
param logAnalyticsWorkspaceName string

//=============================================================================
// Variables
//=============================================================================

var applicationGatewayName string = applicationGatewaySettings.applicationGatewayName
var applicationGatewayHostName string = 'agw.mtls-sample.dev'

//=============================================================================
// Existing Resources
//=============================================================================

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2025-07-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource agwPublicIPAddress 'Microsoft.Network/publicIPAddresses@2025-09-01' existing = {
  name: applicationGatewaySettings.publicIpAddressName
}

resource keyVault 'Microsoft.KeyVault/vaults@2025-05-01' existing = {
  name: keyVaultName
}

resource sslServerCertificateSecret 'Microsoft.KeyVault/vaults/secrets@2025-05-01' existing = {
  name: 'agw-ssl-server-certificate'
  parent: keyVault
}

//=============================================================================
// Resources
//=============================================================================

// Create user-assigned identity for Application Gateway and assign roles to it

resource agwIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: applicationGatewaySettings.identityName
  location: location
  tags: tags
}

module assignRolesToAgwUserAssignedIdentity '../../99-shared/assign-roles-to-principal.bicep' = {
  params: {
    principalId: agwIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    appInsightsName: appInsightsName
    keyVaultName: keyVaultName
  }
}

// Application Gateway

resource applicationGateway 'Microsoft.Network/applicationGateways@2025-09-01' = {
  name: applicationGatewayName
  location: location
  tags: tags

  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${agwIdentity.id}': {}
    }
  }

  properties: {
    sku: {
      name: 'Standard_v2'
      tier: 'Standard_v2'
    }
    enableHttp2: false
    autoscaleConfiguration: {
      minCapacity: 0
      maxCapacity: 2
    }

    gatewayIPConfigurations: [
      {
        name: 'agw-subnet-ip-config'
        properties: {
          subnet: {
            id: subnetId
          }
        }
      }
    ]

    // Frontend

    frontendIPConfigurations: [
      {
        name: 'agw-public-frontend-ip'
        properties: {
          publicIPAddress: {
            id: agwPublicIPAddress.id
          }
        }
      }
    ]

    frontendPorts: [
      {
        name: 'port-https'
        properties: {
          port: 443
        }
      }
      {
        name: 'port-mtls'
        properties: {
          port: 53029
        }
      }
    ]

    sslCertificates: [
      {
        name: 'agw-ssl-certificate'
        properties: {
          keyVaultSecretId: sslServerCertificateSecret.properties.secretUri
        }
      }
    ]

    trustedClientCertificates: [
      {
        name: 'intermediate-ca-with-root-ca'
        properties: {
          data: loadTextContent('../../../self-signed-certificates/certificates/dev-intermediate-ca-with-root-ca.cer')
        }
      }
    ]

    sslProfiles: [
      {
        name: 'mtls-ssl-profile'
        properties: {
          clientAuthConfiguration: {
            verifyClientAuthMode: applicationGatewaySettings.mtlsMode
            // By setting verifyClientCertIssuerDN to true the intermediate CA is also checked, not just the Root CA.
            // See https://learn.microsoft.com/en-us/azure/application-gateway/mutual-authentication-overview?tabs=powershell#verify-client-certificate-dn
            // This only works when the mTLS mode (verifyClientAuthMode) is set to Strict.
            verifyClientCertIssuerDN: applicationGatewaySettings.mtlsMode == 'Strict'
          }
          // For Strict mode, we need to configure trusted CA certificates in order to verify the self-signed client certificates.
          // For Passthrough mode, this can be omitted because all client certificates are forwarded to API Management.
          trustedClientCertificates: applicationGatewaySettings.mtlsMode == 'Strict'
            ? [
                {
                  id: resourceId(
                    'Microsoft.Network/applicationGateways/trustedClientCertificates',
                    applicationGatewayName,
                    'intermediate-ca-with-root-ca'
                  )
                }
              ]
            : []
        }
      }
    ]

    httpListeners: [
      {
        name: 'https-listener'
        properties: {
          protocol: 'Https'
          hostName: applicationGatewayHostName
          frontendIPConfiguration: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/frontendIPConfigurations',
              applicationGatewayName,
              'agw-public-frontend-ip'
            )
          }
          frontendPort: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', applicationGatewayName, 'port-https')
          }
          sslCertificate: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/sslCertificates',
              applicationGatewayName,
              'agw-ssl-certificate'
            )
          }
        }
      }
      {
        name: 'mtls-listener'
        properties: {
          protocol: 'Https'
          hostName: applicationGatewayHostName
          frontendIPConfiguration: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/frontendIPConfigurations',
              applicationGatewayName,
              'agw-public-frontend-ip'
            )
          }
          frontendPort: {
            id: resourceId('Microsoft.Network/applicationGateways/frontendPorts', applicationGatewayName, 'port-mtls')
          }
          sslCertificate: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/sslCertificates',
              applicationGatewayName,
              'agw-ssl-certificate'
            )
          }
          sslProfile: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/sslProfiles',
              applicationGatewayName,
              'mtls-ssl-profile'
            )
          }
        }
      }
    ]

    // Backend

    backendAddressPools: [
      {
        name: 'apim-gateway-backend-pool'
        properties: {
          backendAddresses: [
            {
              fqdn: getApiManagementFqdn(apiManagementServiceName)
            }
          ]
        }
      }
    ]

    probes: [
      {
        name: 'apim-gateway-probe'
        properties: {
          pickHostNameFromBackendHttpSettings: true
          interval: 30
          timeout: 30
          path: '/status-0123456789abcdef'
          protocol: 'Https'
          unhealthyThreshold: 3
          match: {
            statusCodes: [
              '200-399'
            ]
          }
        }
      }
    ]

    backendHttpSettingsCollection: [
      {
        name: 'apim-gateway-backend-settings'
        properties: {
          port: 443
          protocol: 'Https'
          cookieBasedAffinity: 'Disabled'
          hostName: getApiManagementFqdn(apiManagementServiceName)
          requestTimeout: 20
          probe: {
            id: resourceId('Microsoft.Network/applicationGateways/probes', applicationGatewayName, 'apim-gateway-probe')
          }
        }
      }
    ]

    // Rules

    rewriteRuleSets: [
      {
        name: 'default-rewrite-rules'
        properties: {
          rewriteRules: [
            {
              ruleSequence: 100
              conditions: []
              name: 'Remove X-Client-Certificate HTTP request header'
              actionSet: {
                requestHeaderConfigurations: [
                  // We need to remove the client certificate header from the default listener,
                  // to prevent clients from tricking APIM into thinking a successful mTLS connection was established.
                  {
                    headerName: 'X-Client-Certificate'
                    headerValue: ''
                  }
                ]
                responseHeaderConfigurations: []
              }
            }
          ]
        }
      }
      {
        name: 'mtls-rewrite-rules'
        properties: {
          rewriteRules: [
            {
              ruleSequence: 100
              conditions: []
              name: 'Add Client certificate to HTTP request header'
              actionSet: {
                requestHeaderConfigurations: [
                  {
                    headerName: 'X-Client-Certificate'
                    headerValue: '{var_client_certificate}'
                  }
                ]
                responseHeaderConfigurations: []
              }
            }
          ]
        }
      }
    ]

    requestRoutingRules: [
      {
        name: 'apim-https-routing-rule'
        properties: {
          priority: 10
          ruleType: 'Basic'
          httpListener: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/httpListeners',
              applicationGatewayName,
              'https-listener'
            )
          }
          rewriteRuleSet: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/rewriteRuleSets',
              applicationGatewayName,
              'default-rewrite-rules'
            )
          }
          backendAddressPool: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/backendAddressPools',
              applicationGatewayName,
              'apim-gateway-backend-pool'
            )
          }
          backendHttpSettings: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/backendHttpSettingsCollection',
              applicationGatewayName,
              'apim-gateway-backend-settings'
            )
          }
        }
      }
      {
        name: 'apim-mtls-routing-rule'
        properties: {
          priority: 20
          ruleType: 'Basic'
          httpListener: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/httpListeners',
              applicationGatewayName,
              'mtls-listener'
            )
          }
          rewriteRuleSet: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/rewriteRuleSets',
              applicationGatewayName,
              'mtls-rewrite-rules'
            )
          }
          backendAddressPool: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/backendAddressPools',
              applicationGatewayName,
              'apim-gateway-backend-pool'
            )
          }
          backendHttpSettings: {
            id: resourceId(
              'Microsoft.Network/applicationGateways/backendHttpSettingsCollection',
              applicationGatewayName,
              'apim-gateway-backend-settings'
            )
          }
        }
      }
    ]
  }

  dependsOn: [
    assignRolesToAgwUserAssignedIdentity
  ]
}

// Diagnostic settings for Application Gateway

#disable-next-line use-recent-api-versions // There isn't a newer version at the moment
resource applicationGatewayDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${applicationGatewayName}-diagnostics'
  scope: applicationGateway
  properties: {
    workspaceId: logAnalyticsWorkspace.id
    logs: [
      {
        categoryGroup: 'AllLogs'
        enabled: true
      }
    ]
  }
}

//=============================================================================
// Outputs
//=============================================================================

output publicIpAddress string = agwPublicIPAddress.properties.ipAddress
