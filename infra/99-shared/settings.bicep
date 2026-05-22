// API Management

// Exclude Consumption because setting 'enableClientCertificate' to true makes mTLS mandatory for all APIs,
// which breaks several demo scenarios that must remain accessible without client certificates.
@description('The SKU of the API Management service')
@export()
type apimSkuType = 'Developer' | 'Basic' | 'Standard' | 'Premium' | 'BasicV2' | 'StandardV2' | 'PremiumV2'

@description('The settings for the API Management service')
@export()
type apiManagementSettingsType = {
  @description('The name of the API Management service')
  serviceName: string

  @description('The SKU of the API Management service')
  sku: apimSkuType
}

// Application Insights

@description('Retention options for Application Insights')
type appInsightsRetentionInDaysType = 30 | 60 | 90 | 120 | 180 | 270 | 365 | 550 | 730

@description('The settings for the App Insights instance')
@export()
type appInsightsSettingsType = {
  @description('The name of the App Insights instance')
  appInsightsName: string

  @description('The name of the Log Analytics workspace that will be used by the App Insights instance')
  logAnalyticsWorkspaceName: string

  @description('Retention in days of the logging')
  retentionInDays: appInsightsRetentionInDaysType
}

// Application Gateway
@description('The mTLS mode of the Application Gateway')
@export()
type applicationGatewayMtlsModeType = 'Passthrough' | 'Strict'

@description('The settings for the Application Gateway')
@export()
type applicationGatewaySettingsType = {
  @description('The name of the Application Gateway')
  applicationGatewayName: string

  @description('The name of the user-assigned managed identity for the Application Gateway')
  identityName: string

  @description('The name of the public IP address for the Application Gateway')
  publicIpAddressName: string

  @description('The mTLS mode of the Application Gateway')
  mtlsMode: applicationGatewayMtlsModeType
}

// Virtual Network

@description('The settings for the virtual network')
@export()
type virtualNetworkSettingsType = {
  @description('The name of the virtual network')
  virtualNetworkName: string

  @description('The name of the Application Gateway subnet')
  applicationGatewaySubnetName: string
}
