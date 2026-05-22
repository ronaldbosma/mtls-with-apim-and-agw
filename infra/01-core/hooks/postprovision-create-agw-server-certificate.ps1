<#
  This PowerShell script is executed after the core layer is provisioned.
  It will create a self-signed SSL server certificate for the IP address of the Application Gateway in Key Vault.
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$SubscriptionId = $env:AZURE_SUBSCRIPTION_ID,

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = $env:AZURE_KEY_VAULT_NAME,

    [Parameter(Mandatory = $false)]
    [string]$IncludeApplicationGateway = $env:INCLUDE_APPLICATION_GATEWAY
)


if ($IncludeApplicationGateway -eq "false") {
    Write-Host "Application Gateway is not included in this deployment. Skipping SSL server certificate creation."
    exit 0
}


# Validate required parameters
if ([string]::IsNullOrEmpty($SubscriptionId)) {
    throw "SubscriptionId parameter is required. Please provide it as a parameter or set the AZURE_SUBSCRIPTION_ID environment variable."
}

if ([string]::IsNullOrEmpty($KeyVaultName)) {
    throw "KeyVaultName parameter is required. Please provide it as a parameter or set the AZURE_KEY_VAULT_NAME environment variable."
}


# First, ensure the Azure CLI is logged in and set to the correct subscription
az account set --subscription $SubscriptionId
if ($LASTEXITCODE -ne 0) {
    throw "Unable to set the Azure subscription. Please make sure that you're logged into the Azure CLI with the same credentials as the Azure Developer CLI."
}


$certificateName = "agw-ssl-server-certificate"
$applicationGatewayHostName = "agw.mtls-sample.dev"

# Create a self-signed SSL server certificate in Key Vault
Write-Host "Creating self-signed server certificate '$certificateName' in Key Vault '$KeyVaultName' for DNS name '$applicationGatewayHostName'..."

$certificatePolicy = @{
    "issuerParameters"          = @{ "name" = "Self" }
    "keyProperties"             = @{ "exportable" = $true; "keyType" = "RSA"; "keySize" = 2048; "reuseKey" = $false }
    "secretProperties"          = @{ "contentType" = "application/x-pkcs12" }
    "x509CertificateProperties" = @{ "subject" = "CN=$applicationGatewayHostName"; "dnsNames" = @($applicationGatewayHostName); "validityInMonths" = 12 }
    "lifetimeActions"           = @(@{ "trigger" = @{ "lifetimePercentage" = 80 }; "action" = @{ "actionType" = "AutoRenew" } })
}


# Write the certificate policy to a temporary file to avoid PowerShell/CLI JSON quoting issues
$tempPolicyFile = [System.IO.Path]::GetTempFileName()
$certificatePolicy | ConvertTo-Json -Depth 10 | Set-Content -Path $tempPolicyFile -Encoding UTF8

# Create the certificate using the @<file> syntax
az keyvault certificate create --vault-name $KeyVaultName --name $certificateName --policy @$tempPolicyFile
if ($LASTEXITCODE -ne 0) {
    Remove-Item $tempPolicyFile -ErrorAction SilentlyContinue
    throw "Failed to create self-signed certificate '$certificateName' in Key Vault '$KeyVaultName'."
}

Remove-Item $tempPolicyFile -ErrorAction SilentlyContinue

Write-Host "Certificate '$certificateName' created successfully in Key Vault '$KeyVaultName'."
