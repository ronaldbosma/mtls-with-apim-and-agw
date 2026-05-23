using System.Net;
using System.Security.Cryptography.X509Certificates;

using IntegrationTests.Clients;
using IntegrationTests.Configuration;

namespace IntegrationTests;

/// <summary>
/// Integration tests for Scenario 2 — Validate client certificates at the Application Gateway:
///
/// A client connects to the Application Gateway using mTLS.
/// The Application Gateway can be configured in `Strict` mode (enforcing a valid client certificate) or `Passthrough` mode (forwarding the connection regardless).
/// API Management then processes the client certificate passed on in a request header by the Application Gateway.
/// </summary>
[TestClass]
public class Scenario2Tests
{
    private static readonly TestConfiguration Config = TestConfiguration.Load();
    private static X509Certificate2? s_validClientCertificate;
    private static X509Certificate2? s_unregisteredClientCertificate;
    private static X509Certificate2? s_untrustedClientCertificate;
    private static X509Certificate2? s_expiredClientCertificate;
    private static X509Certificate2? s_notYetValidClientCertificate;

    private const string APPLICATION_GATEWAY_HOST_NAME = "agw.mtls-sample.dev";
    private const int HTTPS_PORT = 443;
    private const int MTLS_PORT = 53029;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        // Check prerequisites
        if (!Config.IsApplicationGatewayIncluded)
        {
            Assert.Inconclusive("The Application Gateway is not deployed. Tests in this class will be skipped.");
        }

        // Load client certificates
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

    /// <summary>
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_ValidClientCertificate_200OkReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_validClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// When no client certificate is provided:
    /// - In Strict mode, the Application Gateway returns a 400 Bad Request.
    /// - In Passthrough mode, the Application Gateway forwards the request to APIM which will return a 401 Unauthorized.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_NoClientCertificate_ErrorReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        if (Config.IsApplicationGatewayMtlsModeStrict!.Value)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            ResponseAssert.ContentContains(response, "No required SSL certificate was sent");
        }
        else
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
            ResponseAssert.ContentContains(response, "Client certificate missing");
        }
    }

    /// <summary>
    /// The Application Gateway will forward all requests to APIM where the client certificate was signed by a trusted Intermediate CA.
    /// APIM will check if it knows the client certificate and return 401 Unauthorized.
    ///
    /// This test will fail for an APIM v2 tier where certificate chain validation is enabled, because the client certificate will be untrusted.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_UnregisteredClientCertificate_401UnauthorizedReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_unregisteredClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateIdentityNotMatched");
        ResponseAssert.ContentContains(response, "Invalid client certificate");
    }

    /// <summary>
    /// When an untrusted client certificate is provided: (not signed by a trusted Intermediate CA):
    /// - In Strict mode, the Application Gateway returns a 400 Bad Request.
    /// - In Passthrough mode, the Application Gateway forwards the request to APIM which will return a 401 Unauthorized.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_UntrustedClientCertificate_400BadRequestReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_untrustedClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        if (Config.IsApplicationGatewayMtlsModeStrict!.Value)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            ResponseAssert.ContentContains(response, "The SSL certificate error");
        }
        else
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            var expectedErrorReason = Config.CertificateChainIsValidatedInProtectedApi ? "ClientCertificateNotTrusted" : "ClientCertificateIdentityNotMatched";
            ResponseAssert.HasErrorReason(response, expectedErrorReason);
            ResponseAssert.ContentContains(response, "Invalid client certificate");
        }
    }

    /// <summary>
    /// When an expired client certificate is provided:
    /// - In Strict mode, the Application Gateway returns a 400 Bad Request.
    /// - In Passthrough mode, the Application Gateway forwards the request to APIM which will return a 401 Unauthorized.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_ExpiredClientCertificate_400BadRequestReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_expiredClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        if (Config.IsApplicationGatewayMtlsModeStrict!.Value)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            ResponseAssert.ContentContains(response, "The SSL certificate error");
        }
        else
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ResponseAssert.HasErrorReason(response, "ClientCertificateExpired");
            ResponseAssert.ContentContains(response, "Invalid client certificate");
        }
    }

    /// <summary>
    /// When an client certificate is provided that is not yet valid:
    /// - In Strict mode, the Application Gateway returns a 400 Bad Request.
    /// - In Passthrough mode, the Application Gateway forwards the request to APIM which will return a 401 Unauthorized.
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwMtlsEndpoint_NotYetValidClientCertificate_400BadRequestReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_notYetValidClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        if (Config.IsApplicationGatewayMtlsModeStrict!.Value)
        {
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            ResponseAssert.ContentContains(response, "The SSL certificate error");
        }
        else
        {
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            ResponseAssert.HasErrorReason(response, "ClientCertificateNotYetValid");
            ResponseAssert.ContentContains(response, "Invalid client certificate");
        }
    }

    /// <summary>
    /// The validate-from-agw operation relies on the X-Client-Certificate to verify if a valid client certificate was provided to the Application Gateway on the mTLS endpoint.
    /// When calling the operation via the App Gateway's HTTPS listener endpoint, a client could pass the public part of a valid client certificate in the header and 'spoof' a successful mTLS authentication.
    /// This test verifies this kind of attack is blocked (the HTTPS listener on the Application Gateway should remove the X-Client-Certificate header).
    /// </summary>
    [TestMethod]
    public async Task ValidateFromAgw_AgwSslEndpoint_PassValidClientCertificateInHeader_401UnauthorizedReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, HTTPS_PORT, APPLICATION_GATEWAY_HOST_NAME);
        agwClient.DefaultRequestHeaders.Add("X-Client-Certificate", "-----BEGIN%20CERTIFICATE-----%0AMIIDTjCCAjagAwIBAgIQLufEA4lCPr9M8X%2BQ4LsLkjANBgkqhkiG9w0BAQsFADAq%0AMSgwJgYDVQQDDB9BUElNIFNhbXBsZSBERVYgSW50ZXJtZWRpYXRlIENBMCAXDTI2%0AMDUxNDE0MDc0MVoYDzIwNzYwNTE0MTQwNzQxWjAXMRUwEwYDVQQDDAxWYWxpZCBD%0AbGllbnQwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDIMFowR5%2BO%2BlzC%0AS6MogAxhPiHOOFzW9H0Y86dD2zn421A%2Fytkqfpefmm0kpr%2BZLyRJ9wqOFGszciBe%0Amz6x01YzK9tVcLOP7BPe9hKSfFFHO3C9uBWswBTaQ88WRwbnKLFsk7iK7fjHdTdI%0AWxCk4LCiXkz%2FsteUy5dKvWwSSSFR14JIdENFY6%2FJ6qEABITQ%2BZbzFZC1Bsw7pWmt%0A%2F0v%2BdXdp44e3%2B%2BHUXZc%2BdYj9SZ%2BMtTkLf44io64oo63SPfYj%2FrAfwsbk4WvJDuHS%0AR%2FA9EGpfGGKXsewvZDKpZydZq0bLi8C5E5F6HrO2%2BnQzklQpFfSt68qJtMvahUDG%0AeUrsSf79AgMBAAGjgYAwfjAOBgNVHQ8BAf8EBAMCBaAwFwYDVR0RBBAwDoIMVmFs%0AaWQgQ2xpZW50MBMGA1UdJQQMMAoGCCsGAQUFBwMCMB8GA1UdIwQYMBaAFAFeY55E%0AYGtzPUr%2BqcS%2Bqy8TRbKkMB0GA1UdDgQWBBSXE2sqAqcDBF46nEXz6XhH7GBv%2FDAN%0ABgkqhkiG9w0BAQsFAAOCAQEAsPkox8E4lcL79ABBz1feBwJzgNXsWweiZdOW2wPv%0AEmeTk6KY4Tr0SQcLxwOnxhzoAPpyxlr1wPA7uaT0eyrxGNYiN13zHSZv1CLl6e%2Bf%0AHyyFBXJuW1rjBo9lC8rBpPO6TKSDbMjSaMLBIyFBu5Zm93lIPm%2BdnixTCBc5UFjd%0A8%2BgUImcvKvFEOsIgfqu%2BhNZfPrZop89YSEBjfXzZim8IL2wR0rcSKZUWzDZrTBm2%0AO2HeBTQhD6eg8uobvMUVdODmBDhpfVI6sO35%2BG%2Bd1Ael4yUpHtZAVcavp3h6aNHI%0A8iWB9JcHF0vAi3R%2FIeyV6CagmxWQ9wncDxnGHSySMGOBLQ%3D%3D%0A-----END%20CERTIFICATE-----%0A");

        // Act
        var response = await agwClient.GetAsync("protected/validate-from-agw");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
        ResponseAssert.ContentContains(response, "Client certificate missing");
    }

    /// <summary>
    /// Even though a valid client certificate is provided, a 401 Unauthorized response is expected because the Application Gateway terminates the TLS connection.
    /// The client certificate is not reused to create an mTLS connection between the Application Gateway and APIM,
    /// so APIM has no way to validate it via the validate-client-certificate policy.
    /// </summary>
    [TestMethod]
    public async Task ValidateUsingPolicy_AgwMtlsEndpoint_ValidClientCertificate_401UnauthorizedReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_validClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-using-policy");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
        ResponseAssert.ContentContains(response, "Client certificate missing");
    }

    /// <summary>
    /// Even though a valid client certificate is provided, a 401 Unauthorized response is expected because the Application Gateway terminates the TLS connection.
    /// The client certificate is not reused to create an mTLS connection between the Application Gateway and APIM,
    /// so APIM has no way to validate it via the context.Request.Certificate property.
    /// </summary>
    [TestMethod]
    public async Task ValidateUsingContext_AgwMtlsEndpoint_ValidClientCertificate_401UnauthorizedReturned()
    {
        // Arrange
        using var agwClient = new IntegrationTestHttpClient(Config.ApplicationGatewayIpAddress!, MTLS_PORT, APPLICATION_GATEWAY_HOST_NAME, s_validClientCertificate);

        // Act
        var response = await agwClient.GetAsync("protected/validate-using-context");

        // Assert
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        ResponseAssert.HasErrorReason(response, "ClientCertificateNotFound");
        ResponseAssert.ContentContains(response, "Client certificate missing");
    }
}
