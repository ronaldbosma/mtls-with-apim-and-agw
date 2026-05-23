using System.Net;

using IntegrationTests.Clients;
using IntegrationTests.Configuration;

namespace IntegrationTests;

/// <summary>
/// Integration tests for Scenario 3 — No client certificate validation at the Application Gateway or API Management:
///
/// A client calls the Unprotected API over regular TLS.
/// The Unprotected API retrieves a client certificate from Key Vault and uses it to call the Protected API as a backend over mTLS.
/// This demonstrates how API Management can act as an mTLS client when communicating with mTLS-protected backends.
/// </summary>
[TestClass]
public sealed class Scenario3Tests
{
    private static readonly TestConfiguration Config = TestConfiguration.Load();

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the Unprotected API's client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingPolicy_NoClientCertificateProvided_200OkReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl);

        // Act
        var response = await apimClient.GetAsync("unprotected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the Unprotected API's client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingContext_NoClientCertificateProvided_200OkReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl);

        // Act
        var response = await apimClient.GetAsync("unprotected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }
}