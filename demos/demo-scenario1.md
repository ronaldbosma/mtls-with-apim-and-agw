# Demo Scenario 1 — Validate client certificates in API Management

In this scenario, a client calls the Protected API directly over mTLS. API Management validates the presented client certificate. Two different validation approaches are demonstrated, each implemented as a separate operation in the Protected API.

For a detailed explanation of the concepts, see [Validate client certificates in API Management](https://ronaldbosma.github.io/blog/2024/02/02/validate-client-certificates-in-api-management/).

Before testing this scenario, make sure you have checked the [shared testing instructions](./demo.md#shared-testing-instructions).

## 1. Review the policy implementations

The Protected API exposes two operations that each validate the client certificate differently. Open the policy files to review the implementation before testing.

### Validate using policy

Open [infra/03-application/protected-api/validate-using-policy.operation.xml](../infra/03-application/protected-api/validate-using-policy.operation.xml).

This operation uses the built-in [`validate-client-certificate`](https://learn.microsoft.com/en-us/azure/api-management/validate-client-certificate-policy) policy. Key points to highlight:

- `validate-not-before="true"` and `validate-not-after="true"` reject expired or not-yet-valid certificates.
- `validate-trust` is driven by the `{{validate-certificate-chain}}` named value. When `true`, API Management validates the certificate chain against the CA certificates uploaded to the service. When `false`, only subject/issuer-subject matching is used.
- The `<identities>` block allows certificates with subject `CN=Valid Client` or `CN=Unprotected API` issued by `CN=APIM Sample DEV Intermediate CA`. Any other certificate is rejected.
- A `200 OK` with the certificate details in the body is returned on success. This is useful for demos to confirm which certificate was presented.

> **Note**: when `validate-trust` is `false`, subject/issuer-subject matching alone is spoofable — a client could present a self-signed certificate with the same DN strings. For a properly protected endpoint, enable chain validation or validate by thumbprint.

### Validate using context

Open [infra/03-application/protected-api/validate-using-context.operation.xml](../infra/03-application/protected-api/validate-using-context.operation.xml).

This operation uses the `context.Request.Certificate` property in a policy expression to implement custom validation logic. Key points to highlight:

- The validation is done in a `set-variable` policy that returns a descriptive reason string on failure, or `null` on success.
- It checks for the presence of a certificate, validates the `NotBefore` and `NotAfter` dates, optionally validates the trust chain using `VerifyNoRevocation()`, and finally checks the thumbprint against the certificates uploaded to API Management (`context.Deployment.Certificates`).
- The `{{validate-certificate-chain}}` named value controls whether the trust chain is validated, same as the policy-based approach.
- On rejection, the operation returns `401 Unauthorized` with the reason exposed via the `ErrorReason` response header and the JSON response body. This is useful for demos and tests to show exactly why a certificate was rejected.

### CA certificate uploads

Open [infra/02-platform/modules/api-management.bicep](../infra/02-platform/modules/api-management.bicep) and locate the `certificates` property on the API Management resource.

The root CA (`root-ca`) and the DEV intermediate CA (`dev-intermediate-ca`) are uploaded to API Management when the SKU supports it (`Developer`, `Basic`, `Standard`, or `Premium`). This enables chain validation for certificates issued by the DEV intermediate CA. The V2 tiers (`BasicV2`, `StandardV2` and `PremiumV2`) and `Consumption` do not support uploading CA certificates, so chain validation is not available on those tiers.

> **Note**: The `validateCertificateChainInProtectedApi` configuration option controls whether chain validation is enabled in the policies. Enabling it on a v2 tier will cause all requests with the self-signed certificates from this repository to return `401 Unauthorized`, because the CA certificates are not available to API Management on those tiers. See the [README](../README.md#validate-client-certificate-chain-in-protected-api) for details.

## 2. Test the scenario

Use [tests/scenario1.http](../tests/scenario1.http) for manual testing, or run `Scenario1Tests.cs` from the integration test project. See the [shared testing prerequisites](./demo.md#shared-testing-prerequisites) for setup instructions.

The two operations to test are:
- `GET /protected/validate-using-policy` — validates using the `validate-client-certificate` policy.
- `GET /protected/validate-using-context` — validates using the `context.Request.Certificate` property.

Test the following cases for each operation. Switch the active client certificate in your VS Code user settings (for manual testing) or observe the test output (for automated tests).

| Certificate | Expected result | Reason |
|---|---|---|
| **Valid Client** | `200 OK` — certificate details in body | Certificate is registered and valid |
| _(no certificate)_ | `401 Unauthorized` | `ClientCertificateNotFound` |
| **Unregistered Client** | `401 Unauthorized` | `ClientCertificateIdentityNotMatched` — certificate is not registered in API Management |
| **Untrusted Client** | `401 Unauthorized` | `ClientCertificateNotTrusted` (chain validation on) or `ClientCertificateIdentityNotMatched` (chain validation off) — issued by untrusted intermediate CA |
| **Expired Client** | `401 Unauthorized` | `ClientCertificateExpired` |
| **Not Yet Valid Client** | `401 Unauthorized` | `ClientCertificateNotYetValid` |

> [!TIP]
> On a successful `200 OK` response, the body contains the full certificate details (subject, issuer, serial number, thumbprint, validity dates). This makes it easy to show the audience exactly which certificate was used.

> [!NOTE]
> Tests for the Valid Client and Unregistered Client will fail on v2 tiers when certificate chain validation is enabled, because the CA certificates cannot be uploaded on those tiers and the chain validation will always fail.

