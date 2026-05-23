# mTLS with API Management - Demo

This demo shows how mutual TLS (mTLS) can be used with Azure API Management and Application Gateway. The template deploys an API Management service with two APIs: a Protected API that validates client certificates, and an Unprotected API that acts as an mTLS client itself when calling the Protected API as a backend. An optional Application Gateway sits in front of API Management to handle client certificate validation at the network edge.

See the following diagram for an overview:

![Overview](/images/diagrams-overview.png)

### Self-signed client certificates

The demo uses a set of self-signed certificates to simulate different client scenarios. These certificates are already included in the repository and are imported into Key Vault during deployment. The following certificate tree is used:

![Self-signed certificates](/images/diagrams-self-signed-certificates.png)

- **APIM Sample Root CA**: the root CA for this sample. Trusted by Application Gateway and, if the API Management tier supports it, also by API Management.
  - **APIM Sample DEV Intermediate CA**: intermediate CA for the 'dev' environment. Trusted by Application Gateway and, if the API Management tier supports it, also by API Management.
    - **Valid Client**: registered in API Management as a valid client.
    - **Unregistered Client**: not registered in API Management; used to verify that unregistered clients are blocked.
    - **Unprotected API**: used by the Unprotected API when it calls the Protected API over mTLS.
    - **Expired Client**: an expired certificate used to verify that expired certificates are rejected.
    - **Not Yet Valid Client**: a certificate with a future start date used to verify that not-yet-valid certificates are rejected.
  - **APIM Sample TST Intermediate CA**: intermediate CA for another environment. Not trusted by Application Gateway or API Management.
    - **Untrusted Client**: used to verify that certificates from an untrusted intermediate CA are rejected.

## 1. What resources are getting deployed

The following resources will be deployed:

![Deployed Resources](/images/deployed-resources.png)

## 2. What can I demo from this scenario after deployment

### Scenarios

The following scenarios are available. Each scenario has its own demo guide with step-by-step instructions. The shared testing instructions below apply to all scenarios.

- [Scenario 1 — Validate client certificates in API Management](./demo-scenario1.md)
- [Scenario 2 — Validate client certificates when API Management is behind an Application Gateway](./demo-scenario2.md)
- [Scenario 3 — Securing backend connections with mTLS](./demo-scenario3.md)

### Shared testing instructions

Each scenario can be tested manually using the REST Client extension in Visual Studio Code, or automatically using the .NET integration tests. The setup below applies to all scenarios.

#### Manual testing using Visual Studio Code

The repository includes an `.http` file per scenario under the [tests](../tests) folder: `scenario1.http`, `scenario2.http`, and `scenario3.http`. These files contain ready-made requests for each scenario.

To send requests with a client certificate using the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension, configure the client certificate in your Visual Studio Code user settings:

1. Open the Command Palette (`Ctrl+Shift+P`) and choose `Preferences: Open User Settings (JSON)`.

1. Add the following configuration. Replace the hostname, ip address and path placeholders with your own values.

   ```json
   "rest-client.certificates": {
       "<your-api-management-instance-name>.azure-api.net": {
           "pfx": "<path-to-repo>/self-signed-certificates/certificates/valid-client.pfx",
           "passphrase": "P@ssw0rd"
       },
        "<your-application-gateway-ip-address>:53029": {
            "pfx": "<path-to-repo>/self-signed-certificates/certificates/dev-expired-client.pfx",
            "passphrase": "P@ssw0rd"
        }
   }
   ```

1. Open the relevant `.http` file in Visual Studio Code, set the `@apimHostname` and/or `@agwIPAddress` variables at the top of the file to your API Management instance name and Application Gateway IP address respectively, and click `Send Request` above a request to execute it.

To test with a different certificate (e.g. an expired or unregistered client), update the `pfx` path in the settings to point to the desired certificate from the [self-signed-certificates/certificates](../self-signed-certificates/certificates) folder.

#### Automated integration tests

The repository includes a .NET integration test project at [tests/IntegrationTests](../tests/IntegrationTests). A dedicated test class has been created per scenario (`Scenario1Tests.cs`, `Scenario2Tests.cs`, `Scenario3Tests.cs`), each covering multiple certificate cases.

The tests use your local `azd` environment variables from `.azure/<environment-name>/.env` to connect to the deployed resources. Make sure your `azd` environment is set to the correct deployment before running the tests.

**Run from your IDE (recommended for demos)**

Each test logs the full HTTP request (method, URL, and body) and the full HTTP response (status code, reason, headers, and body) to the test output. This makes it easy to walk through exactly what was sent and what API Management or the Application Gateway returned for each certificate case. To see these logs, run the tests from your IDE and open the test output for an individual test:

- **Visual Studio**: run the tests via the Test Explorer and click a test result to view its output.
- **VS Code**: install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension, run the tests via the Testing panel and click a test result to view its output.

**Run from the command line**

The test output logs are not shown by default when running from the command line. To only verify that all tests pass, navigate to the `tests/IntegrationTests` folder and run:

```powershell
dotnet run
```

#### Understanding 401 responses

When a request is rejected by API Management due to a certificate problem, the `401 Unauthorized` response includes the `ErrorReason` header to indicate why the certificate was rejected. For example:

```http
HTTP/1.1 401 Unauthorized
ErrorReason: ClientCertificateExpired

{
  "statusCode": 401,
  "message": "Invalid client certificate"
}
```

The rejection reason is also traced to Application Insights via the `trace` policy, so you can look it up in the Application Insights logs if the response headers are not visible (e.g. when testing via a browser or a tool that does not show raw headers).
