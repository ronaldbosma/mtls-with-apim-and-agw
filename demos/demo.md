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





## 1. What resources are getting deployed

The following resources will be deployed:

![Deployed Resources](/images/deployed-resources.png)

## 2. What can I demo from this scenario after deployment

> TODO
