# Backend For Frontend (BFF) & API Gateway — Architectural Interview Q&A

> **Target Role**: Staff Frontend Architect, Senior .NET Platform Engineer, Fullstack Architect  
> **Topic**: BFF Architecture, YARP Reverse Proxy, gRPC Protocol Translation, Cookie Security & Edge Resilience

---

### Q1: Why adopt a Micro-BFF pattern instead of a single monolithic API Gateway?
**Answer**:
1. **Frontend Isolation & Decoupling**: Different personas (Staff vs Tenant Admins vs System Superadmins) have radically different data contracts, release frequencies, and permission requirements. A single monolithic gateway becomes a deployment bottleneck and code collision hazard.
2. **Tailored Data Aggregation**: `Staff.Bff` aggregates multiple chat, OCR, and shipment gRPC calls into single optimized payloads tailored for the Operator SPA, minimizing client roundtrips over cellular/low-latency connections.
3. **Targeted Security Boundaries**: `System.Bff` can be deployed on private VPC subnets or IP-whitelisted endpoints, while `Staff.Bff` and `Admin.Bff` are publicly exposed through YARP edge routing.
4. **Independent Scalability**: High-throughput operational endpoints (e.g. GPS tracking and mail polling in `Staff.Bff`) can scale out independently of administrative functions in `Admin.Bff`.

---

### Q2: How does Aurora solve the browser authentication security dilemma (Cookie vs Bearer token in LocalStorage)?
**Answer**:
We implement a **Cookie-to-Bearer Bridge in the BFF**:
- **Edge Layer (Browser $\leftrightarrow$ BFF)**: Uses `HttpOnly`, `Secure`, `SameSite=Lax` cookies. JavaScript running in the browser cannot read the JWT, eliminating the primary vector for token theft via XSS.
- **Service Mesh Layer (BFF $\leftrightarrow$ Downstream Microservices)**: The BFF extracts the claims from the authenticated cookie, verifies the cryptographic signature against IAM public keys, and injects the token into standard gRPC metadata (`Authorization: Bearer <jwt>`).
- This gives the frontend maximum browser security while keeping the backend microservices stateless, interoperable, and zero-trust.

---

### Q3: How are gRPC errors translated without leaking internal infrastructure details?
**Answer**:
`BuildingBlocks.BFF` registers a `GlobalGrpcExceptionFilter` that intercepts all `RpcException` instances:
- Maps standard gRPC `StatusCode` enums (e.g. `NOT_FOUND`, `PERMISSION_DENIED`, `DEADLINE_EXCEEDED`) to standard HTTP status codes (404, 403, 504) and serializes them to RFC 7807 `ProblemDetails`.
- Masks internal server stack traces, database query logs, and internal IP addresses in production.
- Preserves the `traceId` and `correlationId` so the client can reference the error with customer support while engineers debug via distributed tracing.

---

### Q4: How does the BFF handle concurrent gRPC aggregation without blocking threads?
**Answer**:
- Leverages **.NET 8 Asynchronous `Task.WhenAll` Aggregation** combined with HTTP/2 persistent connection multiplexing (`Grpc.Net.Client`).
- When a page requires Shipment status, OCR extraction results, and Carrier tracking details simultaneously, the controller initiates 3 asynchronous gRPC unary calls in parallel rather than sequentially, reducing page load latency by up to 60%.
- gRPC channels are managed via `IHttpClientFactory` / `AddGrpcClient` to prevent socket exhaustion and manage DNS TTL refreshes cleanly.
