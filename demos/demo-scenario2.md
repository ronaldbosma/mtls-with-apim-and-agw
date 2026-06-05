# Demo Scenario 2 — Validate client certificates when API Management is behind an Application Gateway

In this scenario, a client calls the Protected API via an Application Gateway using mTLS. The Application Gateway terminates the TLS session and forwards the client certificate to API Management in a request header. API Management then validates the forwarded certificate.

For a detailed explanation of the concepts, see [Validate client certificates in API Management when it's behind an Application Gateway](https://ronaldbosma.github.io/blog/2024/02/19/validate-client-certificates-in-api-management-when-its-behind-an-application-gateway/).

Before testing this scenario, make sure you have checked the [shared testing instructions](./demo.md#shared-testing-instructions).

> [!NOTE]
> This scenario requires the Application Gateway to be deployed. See the [README](../README.md) for details on how to include it in the deployment.

## 1. Review the implementation

### Communication flow

The flow for this scenario is shown in the image below:

![Scenario 2 Flow](/images/diagrams-scenario2-flow.drawio.png)

The client establishes an mTLS connection with the Application Gateway. The Application Gateway terminates the mTLS session, validates the client certificate, and forwards the request to API Management over a regular TLS connection. The client certificate is **not** forwarded automatically — the Application Gateway places the public part of the certificate into the `X-Client-Certificate` request header using a rewrite rule.

This means that the `validate-client-certificate` policy and `context.Request.Certificate` used in Scenario 1 are not available here, because API Management never receives the certificate as part of a TLS handshake.

### Application Gateway setup

The Application Gateway configuration is shown in the image below:

![Scenario 2 AGW Setup](/images/diagrams-scenario2-agw-setup.drawio.svg)

Open [infra/02-platform/modules/application-gateway.bicep](../infra/02-platform/modules/application-gateway.bicep) and review the key components.

The Application Gateway exposes two listeners on the same frontend IP:

- **`https-listener`** on port `443` — accepts standard HTTPS traffic. A rewrite rule (`default-rewrite-rules`) strips the `X-Client-Certificate` header from all inbound requests. This prevents clients from spoofing a valid certificate by injecting this header manually.
- **`mtls-listener`** on port `53029` — accepts mTLS traffic. This listener is linked to an SSL profile (`mtls-ssl-profile`) that references the trusted intermediate and root CA certificates. A rewrite rule (`mtls-rewrite-rules`) reads the `{var_client_certificate}` server variable and writes its value into the `X-Client-Certificate` header.

Key points to highlight:
- The `verifyClientCertIssuerDN` setting is set to `true` when the mTLS mode is `Strict`. This ensures that only certificates issued by `APIM Sample DEV Intermediate CA` are accepted — not those from other intermediate CAs (such as `APIM Sample TST Intermediate CA`) even if the root CA is trusted.
- Both listeners route to the same backend pool pointing to API Management.
- The Application Gateway supports two mTLS modes (configured via the `applicationGatewayMtlsMode` parameter):
  - **`Strict`**: A valid client certificate is required. The TLS handshake fails with `400 Bad Request` if the certificate is missing, expired, not yet valid, or issued by an untrusted CA.
  - **`Passthrough`**: The client certificate is optional. Invalid or missing certificates are still forwarded to APIM, which must then reject them.

### The `validate-from-agw` policy in API Management

Open [infra/03-application/protected-api/validate-from-agw.operation.xml](../infra/03-application/protected-api/validate-from-agw.operation.xml).

This operation implements client certificate validation using the forwarded `X-Client-Certificate` header. Key points to highlight:

- If the header is missing, `ClientCertificateNotFound` is returned immediately.
- The header value is URL-decoded, stripped of PEM markers, and parsed into an `X509Certificate2` instance.
- The same validation checks are applied as in Scenario 1 (validity period, optional chain validation, thumbprint match against certificates uploaded to API Management).
- The `{{validate-certificate-chain}}` named value controls whether chain validation is performed, as in Scenario 1.
- On success, a `200 OK` is returned with the certificate details in the body.
- On failure, a `401 Unauthorized` is returned with the reason in the `ErrorReason` header.

### Security: stripping the header on the HTTPS listener

Because the `validate-from-agw` operation trusts the `X-Client-Certificate` header, any client that can reach API Management directly (bypassing the Application Gateway) could inject a valid certificate value into the header and authenticate successfully. The rewrite rule on the HTTPS listener removes this header for requests arriving on port `443`, so spoofing is not possible through the Application Gateway's own HTTPS endpoint.

However, if API Management is directly accessible (e.g., not isolated in an internal virtual network), a client can call APIM directly and still inject the header. This is demonstrated in the test requests below. For a fully secure solution, API Management should only be reachable via the Application Gateway.

## 2. Test the scenario

Use [tests/scenario2.http](../tests/scenario2.http) for manual testing, or run `Scenario2Tests.cs` from the integration test project. See the [shared testing instructions](./demo.md#shared-testing-instructions) for setup instructions.

For manual testing, configure your Visual Studio Code user settings to use `<your-application-gateway-ip-address>:53029` as the mTLS endpoint. See the [shared testing instructions](./demo.md#manual-testing-using-visual-studio-code) for details.

The main operation to test is:
- `GET /protected/validate-from-agw` — validates the client certificate forwarded by the Application Gateway.

### Test cases for `validate-from-agw` via the mTLS endpoint (port 53029)

The results differ depending on the configured mTLS mode:

**Strict mode** (`applicationGatewayMtlsMode = Strict`):

| Certificate | Expected result | Reason |
|---|---|---|
| **Valid Client** | `200 OK` — certificate details in body | Certificate passes AGW validation and is registered in API Management |
| _(no certificate)_ | `400 Bad Request` | Application Gateway rejects the connection — no client certificate provided |
| **Unregistered Client** | `401 Unauthorized` | AGW accepts (trusted issuer), APIM rejects — `ClientCertificateIdentityNotMatched` |
| **Untrusted Client** | `400 Bad Request` | Application Gateway rejects the connection — certificate not issued by a trusted CA |
| **Expired Client** | `400 Bad Request` | Application Gateway rejects the connection — certificate is expired |
| **Not Yet Valid Client** | `400 Bad Request` | Application Gateway rejects the connection — certificate is not yet valid |

**Passthrough mode** (`applicationGatewayMtlsMode = Passthrough`):

| Certificate | Expected result | Reason |
|---|---|---|
| **Valid Client** | `200 OK` — certificate details in body | Certificate is registered and valid |
| _(no certificate)_ | `401 Unauthorized` | `ClientCertificateNotFound` — no header forwarded |
| **Unregistered Client** | `401 Unauthorized` | `ClientCertificateIdentityNotMatched` |
| **Untrusted Client** | `401 Unauthorized` | `ClientCertificateNotTrusted` (chain validation on) or `ClientCertificateIdentityNotMatched` (chain validation off) |
| **Expired Client** | `401 Unauthorized` | `ClientCertificateExpired` |
| **Not Yet Valid Client** | `401 Unauthorized` | `ClientCertificateNotYetValid` |

### Demonstrate that `validate-using-policy` and `validate-using-context` don't work here

Also call these two operations via the mTLS endpoint with the **Valid Client** certificate:
- `GET /protected/validate-using-policy`
- `GET /protected/validate-using-context`

Both return `401 Unauthorized` with `ClientCertificateNotFound`, even though a valid client certificate was presented to the Application Gateway. This demonstrates that the Application Gateway terminates the TLS session and does not re-use the client certificate when connecting to API Management — so the Scenario 1 approach cannot be used here.

### Demonstrate the security concern

The `validate-from-agw` operation trusts the `X-Client-Certificate` header. A malicious client could try to forge authentication by injecting the public part of a valid certificate into this header without going through the mTLS handshake. Two attack surfaces exist: through the Application Gateway's HTTPS listener, and directly against API Management.

**Through the Application Gateway's HTTPS listener (port 443)**

Call `validate-from-agw` via the Application Gateway's standard HTTPS endpoint with a valid certificate value injected in the `X-Client-Certificate` header:

```http
GET https://<your-application-gateway-ip-address>/protected/validate-from-agw
Host: agw.mtls-sample.dev
X-Client-Certificate: <url-encoded-certificate>
```

This returns `401 Unauthorized` with `ClientCertificateNotFound` — the rewrite rule on the HTTPS listener strips the header before the request reaches API Management, so the spoofing attempt is blocked.

> [!NOTE]
> The test file [tests/scenario2.http](../tests/scenario2.http) includes a pre-built request with a valid certificate value for this demonstration. The integration test `ValidateFromAgw_AgwSslEndpoint_PassValidClientCertificateInHeader_401UnauthorizedReturned` in `Scenario2Tests.cs` verifies this behaviour automatically.

**Directly against API Management (bypassing the Application Gateway)**

Call `validate-from-agw` directly against API Management with the same injected header:

```http
GET https://<your-api-management-instance-name>.azure-api.net/protected/validate-from-agw
X-Client-Certificate: <url-encoded-certificate>
```

This returns `200 OK`, demonstrating that if API Management is directly accessible, a client can forge authentication by injecting the header. Use this to highlight the importance of network isolation (deploying APIM in an internal virtual network accessible only through the Application Gateway).

> [!NOTE]
> The test file [tests/scenario2.http](../tests/scenario2.http) includes a pre-built request with a valid certificate value for this demonstration. The integration test `ValidateFromAgw_DirectyApimCall_PassValidClientCertificateInHeader_200OkReturned` in `Scenario2Tests.cs` covers the same case automatically.
