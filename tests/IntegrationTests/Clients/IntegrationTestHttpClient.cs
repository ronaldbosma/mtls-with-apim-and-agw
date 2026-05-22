using System.Net;
using System.Security.Cryptography.X509Certificates;

using IntegrationTests.Clients.Handlers;

namespace IntegrationTests.Clients;

/// <summary>
/// Represents an HTTP client used for integration testing, configured with a base address, or a combination of IP address and host.
/// A custom message handler is added that logs HTTP requests and responses.
/// Optionally, it can be configured to use a client certificate for authentication.
/// </summary>
internal class IntegrationTestHttpClient : HttpClient
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationTestHttpClient"/> class with the specified base address.
    /// No authentication is configured for this client.
    /// </summary>
    /// <param name="baseAddress">The base address of the HTTP client.</param>
    public IntegrationTestHttpClient(Uri baseAddress)
        : this(baseAddress, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationTestHttpClient"/> class with the specified base address and client certificate.
    /// </summary>
    /// <param name="baseAddress">The base address of the HTTP client.</param>
    /// <param name="clientCertificate">The client certificate to use for authentication.</param>
    public IntegrationTestHttpClient(Uri baseAddress, X509Certificate2? clientCertificate)
        : base(CreateHttpHandler(certificate: clientCertificate))
    {
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationTestHttpClient"/> class with the specified IP address and host header.
    /// </summary>
    /// <param name="ipAddress">The IP address to use in the base address of the HTTP client.</param>
    /// <param name="port">The port to use in the base address of the HTTP client.</param>
    /// <param name="host">The value to set in the Host header of the HTTP client.</param>
    public IntegrationTestHttpClient(IPAddress ipAddress, int port, string host)
        : this(ipAddress, port, host, clientCertificate: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationTestHttpClient"/> class with the specified IP address and host header.
    /// </summary>
    /// <param name="ipAddress">The IP address to use in the base address of the HTTP client.</param>
    /// <param name="port">The port to use in the base address of the HTTP client.</param>
    /// <param name="host">The value to set in the Host header of the HTTP client.</param>
    /// <param name="clientCertificate">The client certificate to use for authentication.</param>
    public IntegrationTestHttpClient(IPAddress ipAddress, int port, string host, X509Certificate2? clientCertificate)
        : base(CreateHttpHandler(skipServerCertificateValidation: true, certificate: clientCertificate))
    {
        BaseAddress = new Uri($"https://{ipAddress}:{port}");
        DefaultRequestHeaders.Host = host;
    }

    private static HttpMessageLoggingHandler CreateHttpHandler(bool skipServerCertificateValidation = false, X509Certificate2? certificate = null)
    {
        var handler = new HttpClientHandler();

        // If e.g. the base address has an IP address instead of a hostname, the server certificate validation will fail.
        // Since this is only used for testing purposes, we only check that a certificate is present, but we don't validate it further.
        if (skipServerCertificateValidation)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => cert != null;
        }

        if (certificate is not null)
        {
            handler.ClientCertificates.Add(certificate);
        }

        // Wrap the HttpClientHandler with the HttpMessageLoggingHandler to enable logging of HTTP requests and responses.
        return new HttpMessageLoggingHandler(handler);
    }
}