//=============================================================================
// Virtual Network
//=============================================================================

//=============================================================================
// Imports
//=============================================================================

import { virtualNetworkSettingsType } from '../../99-shared/settings.bicep'
import { tagsType } from '../../99-shared/types.bicep'

//=============================================================================
// Parameters
//=============================================================================

@description('Location to use for all resources')
param location string

@description('The tags to associate with the resource')
param tags tagsType

@description('The settings for the virtual network')
param virtualNetworkSettings virtualNetworkSettingsType

//=============================================================================
// Resources
//=============================================================================

// Virtual Network
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-09-01' = {
  name: virtualNetworkSettings.virtualNetworkName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: virtualNetworkSettings.applicationGatewaySubnetName
        properties: {
          addressPrefix: '10.0.0.0/24'
        }
      }
    ]
  }

  resource agwSubnet 'subnets' existing = {
    name: virtualNetworkSettings.applicationGatewaySubnetName
  }
}

//=============================================================================
// Outputs
//=============================================================================

output agwSubnetId string = virtualNetwork::agwSubnet.id
