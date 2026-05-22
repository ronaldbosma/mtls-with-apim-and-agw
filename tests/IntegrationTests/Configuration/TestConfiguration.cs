using System.Net;

using Microsoft.Extensions.Configuration;

namespace IntegrationTests.Configuration;

/// <summary>
/// Contains configuration settings for the integration tests.
/// </summary>
internal class TestConfiguration
{
    public required Uri AzureApiManagementGatewayUrl { get; init; }
    public required Uri AzureKeyVaultUri { get; init; }
    public required bool CertificateChainIsValidatedInProtectedApi { get; init; }

    public required bool IsApplicationGatewayIncluded { get; init; }
    public string? ApplicationGatewayHostName { get; init; }
    public IPAddress? ApplicationGatewayIpAddress { get; init; }
    public bool? IsApplicationGatewayMtlsModeStrict { get; init; }

    public static TestConfiguration Load()
    {
        AzdDotEnv.Load(optional: true); // Loads Azure Developer CLI environment variables; optional since .env file might be missing in CI/CD pipelines

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var isApplicationGatewayIncluded = configuration.GetRequiredBool("INCLUDE_APPLICATION_GATEWAY");

        return new TestConfiguration
        {
            AzureApiManagementGatewayUrl = configuration.GetRequiredUri("AZURE_API_MANAGEMENT_GATEWAY_URL"),
            AzureKeyVaultUri = configuration.GetRequiredUri("AZURE_KEY_VAULT_URI"),
            CertificateChainIsValidatedInProtectedApi = configuration.GetRequiredBool("VALIDATE_CERTIFICATE_CHAIN_IN_PROTECTED_API"),

            IsApplicationGatewayIncluded = isApplicationGatewayIncluded,
            ApplicationGatewayHostName = "agw.mtls-sample.dev",
            ApplicationGatewayIpAddress = isApplicationGatewayIncluded ? configuration.GetRequiredIPAddress("AZURE_APPLICATION_GATEWAY_PUBLIC_IP_ADDRESS_VALUE") : null,
            IsApplicationGatewayMtlsModeStrict = isApplicationGatewayIncluded ? configuration.GetRequiredString("AZURE_APPLICATION_GATEWAY_MTLS_MODE") == "Strict" : null
        };
    }
}