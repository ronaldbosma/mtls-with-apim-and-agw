//=============================================================================
// Public IP Address
//=============================================================================

//=============================================================================
// Imports
//=============================================================================

import { tagsType } from '../../99-shared/types.bicep'

//=============================================================================
// Parameters
//=============================================================================

@description('Location to use for all resources')
param location string

@description('The tags to associate with the resource')
param tags tagsType

@description('The name of the Public IP Address')
param publicIpAddressName string

//=============================================================================
// Resources
//=============================================================================

resource publicIPAddress 'Microsoft.Network/publicIPAddresses@2025-07-01' = {
  name: publicIpAddressName
  location: location
  tags: tags
  sku: {
    name: 'Standard'
  }
  properties: {
    publicIPAddressVersion: 'IPv4'
    publicIPAllocationMethod: 'Static'
    idleTimeoutInMinutes: 4
  }
}

//=============================================================================
// Outputs
//=============================================================================

output ipAddress string = publicIPAddress.properties.ipAddress
