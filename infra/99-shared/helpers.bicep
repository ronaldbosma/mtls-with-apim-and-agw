//=============================================================================
// Helper functions to construct URLs, Key Vault references, etc.
//=============================================================================

// API Management functions

@export()
func getApiManagementFqdn(apimServiceName string) string => '${apimServiceName}.azure-api.net'

// Tags

@export()
func getTemplateTags(environmentName string) { *: string } => {
  'azd-env-name': environmentName
  'azd-template': 'ronaldbosma/mtls-with-apim-and-agw'
}
