# Aurora Micro-BFF & API Gateway Documentation Suite

> **Location**: `docs/technical/bff/`  
> **Source-of-Truth**: `src/dotnet/BFF/`

---

## Document Index

1. [`OVERVIEW.md`](file:///D:/IT/CD/aurora-server/docs/technical/bff/OVERVIEW.md): Architecture principles, Micro-BFF separation (`Staff.Bff`, `Admin.Bff`, `System.Bff`), YARP `API.Gateway`, security boundaries.
2. [`DETAILS.md`](file:///D:/IT/CD/aurora-server/docs/technical/bff/DETAILS.md): `BuildingBlocks.BFF` deep dive, gRPC connection pooling, HTTP/2 multiplexing, `GlobalGrpcExceptionFilter` error mapping table, cookie-to-bearer bridge.
3. [`INTERVIEW_QA.md`](file:///D:/IT/CD/aurora-server/docs/technical/bff/INTERVIEW_QA.md): Staff/Principal architect interview questions and defense on BFF patterns, resilience, rate limiting, and security.

### Cross-References:
- **API Catalog & Integration**: [`docs/technical/frontend/API_CATALOG.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/API_CATALOG.md)
- **Frontend Integration Guide**: [`docs/technical/frontend/FE_INTEGRATION_GUIDE.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/FE_INTEGRATION_GUIDE.md)
- **Role Permission Matrix**: [`docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md`](file:///D:/IT/CD/aurora-server/docs/technical/frontend/ROLE_PERMISSION_API_MATRIX.md)
- **BFF Legacy API Specs**: [`docs/bff-api/`](file:///D:/IT/CD/aurora-server/docs/bff-api)
