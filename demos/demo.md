# mTLS with API Management - Demo

In this demo scenario, you will demonstrate how mutual TLS (mTLS) can be used with Azure API Management. The template deploys an API Management service with two APIs: a Protected API that validates client certificates, and an Unprotected API that acts as an mTLS client itself when calling the Protected API as a backend. An optional Application Gateway sits in front of API Management to handle client certificate validation at the network edge. The following three scenarios are covered:

- **Scenario 1 — Validate client certificates in API Management**:  
A client calls the Protected API directly over mTLS. API Management validates the presented client certificate. This scenario covers multiple validation approaches implemented via APIM policies.
- **Scenario 2 — Validate client certificates when API Management is behind an Application Gateway**:  
A client connects to the Application Gateway using mTLS. The Application Gateway can be configured in `Strict` mode (enforcing a valid client certificate) or `Passthrough` mode (forwarding the connection regardless). API Management then processes the client certificate passed on in a request header by the Application Gateway.
- **Scenario 3 — Securing backend connections with mTLS**:  
A client calls the Unprotected API over regular TLS. The Unprotected API retrieves a client certificate from Key Vault and uses it to call the Protected API as a backend over mTLS. This demonstrates how API Management can act as an mTLS client when communicating with mTLS-protected backends.

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

> TODO
