# Demo Scenario 3 — Securing backend connections with mTLS

In this scenario, a client calls the Unprotected API over regular TLS. The Unprotected API then calls the Protected API as a backend over mTLS, presenting a client certificate stored in Key Vault. This demonstrates how API Management can act as an mTLS client when communicating with mTLS-protected backends.

For a detailed explanation of the concepts, see [Securing backend connections with mTLS in API Management](https://ronaldbosma.github.io/blog/2024/05/24/securing-backend-connections-with-mtls-in-api-management/).

Before testing this scenario, make sure you have checked the [shared testing instructions](./demo.md#shared-testing-instructions).

> [!NOTE]
> This demo uses the Protected API in the same API Management instance as the backend. The same approach applies to any external backend that requires mTLS — only the backend URL in the Bicep configuration would differ.

## 1. Review the implementation

### Setup overview

The setup for this scenario is shown in the image below:

![Scenario 3 Setup](/images/diagrams-scenario3-setup.png)

The caller connects to API Management using regular TLS — no client certificate is required from the caller's side. When the Unprotected API forwards the request to the Protected API backend, it attaches the `Unprotected API` client certificate. API Management loads this certificate from Key Vault at runtime using a managed identity with the `Key Vault Secrets User` role.

### The Unprotected API in Bicep

Open [infra/03-application/unprotected-api/unprotected-api.bicep](../infra/03-application/unprotected-api/unprotected-api.bicep) and review the key components.

**Client certificate**

A `Microsoft.ApiManagement/service/certificates` resource is created that links to the `dev-unprotected-api` secret in Key Vault:

```bicep
resource clientCertificate 'Microsoft.ApiManagement/service/certificates@...' = {
  name: 'unprotected-api-client-certificate'
  ...
  properties: {
    keyVault: {
      secretIdentifier: clientCertificateSecret.properties.secretUri
    }
  }
}
```

Key Vault stores the certificate, and API Management references it by its secret URI. The certificate (`Unprotected API`) is part of the self-signed certificate tree described in the [demo overview](./demo.md#self-signed-client-certificates) — it is issued by `APIM Sample DEV Intermediate CA` and registered in API Management, so the Protected API will accept it.

> [!NOTE]
> The private key of the certificate must be exportable. API Management needs to export the private key from Key Vault to use it in the mTLS handshake. If the private key is not exportable, the deployment will fail with: `Certificate with id '...' does not contain private key`.

**Backend**

The `protected-backend` resource configures where to forward requests and which client certificate to present:

```bicep
resource protectedBackend 'Microsoft.ApiManagement/service/backends@...' = {
  name: 'protected-backend'
  ...
  properties: {
    url: '${apiManagementService.properties.gatewayUrl}/protected'
    protocol: 'http'
    credentials: {
      certificateIds: [ clientCertificate.id ]
    }
    tls: {
      validateCertificateChain: true
      validateCertificateName: true
    }
  }
}
```

The `certificateIds` array references the certificate resource created above. The `tls` block ensures the backend's SSL server certificate is validated. The backend URL points to the Protected API on the same API Management instance; for an external backend, this would be an external URL.

**API and operation**

The Unprotected API accepts all `GET` requests on `/{*path}` and forwards them to the `protected-backend` via the policy in [infra/03-application/unprotected-api/unprotected-api.xml](../infra/03-application/unprotected-api/unprotected-api.xml):

```xml
<set-backend-service backend-id="protected-backend" />
```

This wildcard routing means `GET /unprotected/validate-using-policy` is forwarded as `GET /protected/validate-using-policy`, and similarly for `validate-using-context`.

## 2. Test the scenario

Use [tests/scenario3.http](../tests/scenario3.http) for manual testing, or run `Scenario3Tests.cs` from the integration test project. See the [shared testing prerequisites](./demo.md#shared-testing-prerequisites) for setup instructions.

No client certificate is needed in your REST Client or test configuration — the caller connects using plain TLS. API Management attaches the certificate automatically when calling the backend.

The two operations to test are:
- `GET /unprotected/validate-using-policy` — forwards to the Protected API's `validate-using-policy` operation.
- `GET /unprotected/validate-using-context` — forwards to the Protected API's `validate-using-context` operation.

| Operation | Expected result | Reason |
|---|---|---|
| `validate-using-policy` | `200 OK` — certificate details in body | Unprotected API presents its registered certificate to the Protected API |
| `validate-using-context` | `200 OK` — certificate details in body | Same as above |

The response body contains the full details of the `Unprotected API` client certificate (subject, issuer, thumbprint, validity dates), confirming which certificate was used for the backend mTLS connection.

> [!NOTE]
> These tests will fail on v2 tiers when certificate chain validation is enabled in the Protected API, because the CA certificates cannot be uploaded on those tiers. See the [README](../README.md#validate-client-certificate-chain-in-protected-api) for details.

