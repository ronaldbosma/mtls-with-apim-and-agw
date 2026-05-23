using System.Net;
using System.Security.Cryptography.X509Certificates;

using IntegrationTests.Clients;
using IntegrationTests.Configuration;

namespace IntegrationTests;

/// <summary>
/// Integration tests for Scenario 1 — Validate client certificates in API Management:
///
/// A client calls the Protected API directly over mTLS. API Management validates the presented client certificate.
/// This scenario covers multiple validation approaches implemented via APIM policies.
/// </summary>
[TestClass]
public sealed class Scenario1Tests
{
    private static readonly TestConfiguration Config = TestConfiguration.Load();
    private static X509Certificate2? s_validClientCertificate;
    private static X509Certificate2? s_unregisteredClientCertificate;
    private static X509Certificate2? s_untrustedClientCertificate;
    private static X509Certificate2? s_expiredClientCertificate;
    private static X509Certificate2? s_notYetValidClientCertificate;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        var keyVaultClient = new KeyVaultClient(Config.AzureKeyVaultUri);
        s_validClientCertificate = await keyVaultClient.GetCertificateAsync("dev-valid-client");
        s_unregisteredClientCertificate = await keyVaultClient.GetCertificateAsync("dev-unregistered-client");
        s_untrustedClientCertificate = await keyVaultClient.GetCertificateAsync("tst-untrusted-client");
        s_expiredClientCertificate = await keyVaultClient.GetCertificateAsync("dev-expired-client", passwordSecretName: "client-certificate-password");
        s_notYetValidClientCertificate = await keyVaultClient.GetCertificateAsync("dev-notyetvalid-client");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_validClientCertificate?.Dispose();
        s_unregisteredClientCertificate?.Dispose();
        s_untrustedClientCertificate?.Dispose();
        s_expiredClientCertificate?.Dispose();
        s_notYetValidClientCertificate?.Dispose();
    }

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingPolicy_ValidClientCertificateProvided_200OkReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_validClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ValidateUsingPolicy_NoClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
        await ResponseAssert.ContentContains(response, "Client certificate missing");
    }

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingPolicy_UnregisteredClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_unregisteredClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateIdentityNotMatched");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingPolicy_UntrustedClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_untrustedClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);

        var expectedReason = Config.CertificateChainIsValidatedInProtectedApi ? "ClientCertificateNotTrusted" : "ClientCertificateIdentityNotMatched";
        ResponseAssert.HasErrorReason(response, expectedReason);
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingPolicy_ExpiredClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_expiredClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateExpired");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingPolicy_NotYetValidClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_notYetValidClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotYetValid");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingContext_ValidClientCertificateProvided_200OkReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_validClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task ValidateUsingContext_NoClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
        await ResponseAssert.ContentContains(response, "Client certificate missing");
    }

    /// <remarks>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </remarks>
    [TestMethod]
    public async Task ValidateUsingContext_UnregisteredClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_unregisteredClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateIdentityNotMatched");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingContext_UntrustedClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_untrustedClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var expectedReason = Config.CertificateChainIsValidatedInProtectedApi ? "ClientCertificateNotTrusted" : "ClientCertificateIdentityNotMatched";
        ResponseAssert.HasErrorReason(response, expectedReason);
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingContext_ExpiredClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_expiredClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateExpired");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    [TestMethod]
    public async Task ValidateUsingContext_NotYetValidClientCertificateProvided_401UnauthorizedReturned()
    {
        // Arrange
        using var apimClient = new IntegrationTestHttpClient(Config.AzureApiManagementGatewayUrl, s_notYetValidClientCertificate!);

        // Act
        var response = await apimClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotYetValid");
        await ResponseAssert.ContentContains(response, "Invalid client certificate");
    }
}