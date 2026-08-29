# Financial Rating & Customs Tax Service — Service Overview

> **Service Layer**: Freight Rating Engine, Surcharges & Customs Tariff Computation  
> **Target Audience**: Technical Recruiters, Fintech Engineers, System Architects  
> **Source-of-Truth**: `src/nestjs/financial-service`, `FinancialService`, `prisma/schema.prisma`.

---

## 1. Service Purpose & Problem Solved

Freight pricing and international tax calculations are among the most error-prone domains in logistics. Freight costs involve complex multi-variable rating matrices (chargeable weight, ocean bunker surcharges BAF, currency adjustment factors CAF, terminal handling charges THC), while cross-border trade requires calculating exact customs import duties and VAT based on HS Codes.

The **Financial & Tax Service** provides a **Deterministic Freight Rating & Multi-Currency Tax Engine**:
- **Chargeable Weight Optimization**: Computes volumetric weight ($\frac{L \times W \times H}{5000}$ for air, $\frac{L \times W \times H}{6000}$ for courier, or CBM for ocean) vs actual gross weight.
- **Dynamic Surcharge Matrix**: Evaluates fuel surcharges, peak season surcharges (PSS), demurrage/detention rates, and toll fees.
- **Customs Tariff & Import Duty Calculator**: Evaluates ad-valorem and specific customs duties, VAT, and excise taxes based on product HS Code and preferential trade agreements.
- **Multi-Currency Conversion**: Evaluates spot and locked contract exchange rates with strict rounding precision.

---

## 2. Architecture & Tech Stack

```
[ ShipmentWorkflow / Quotation Engine / BFF ]
                      │
                      ▼ (REST API / gRPC)
┌─────────────────────────────────────────────────────────────┐
│                 Financial & Tax Microservice (NestJS)       │
│  ├── Freight Rating Engine (Base Rate + Distance Tiers)     │
│  ├── Chargeable & Volumetric Weight Calculator              │
│  ├── Surcharge Matrix (BAF, CAF, THC, Tolls)                │
│  ├── Customs Tariff & Duty Calculator (HS Codes)            │
│  └── Multi-Currency Exchange Rate Converter                 │
└──────────────┬───────────────────────────────┬──────────────┘
               │                               │
               ▼                               ▼
      [ PostgreSQL 16 (Neon) ]           [ Redis Cache ]
    (Tariffs, RateCards, Quotes)       (Live Exchange Rates)
```

| Layer | Technology |
|---|---|
| **Runtime & Framework** | Node.js 20, NestJS 10, TypeScript |
| **Persistence & ORM** | Prisma ORM, PostgreSQL 16 (Neon Serverless SSL) |
| **Caching** | Redis 7 (Exchange rates & rate card caching) |
| **API Protocol** | REST API (`/api/v1/financial/*`), internal gRPC |

---

## 3. Owned Data & Schema Boundaries

The service owns:
- **`RateCards`**: Base rates per lane (origin $\rightarrow$ destination), transportation mode, container type, and weight breaks.
- **`SurchargeConfigs`**: Rules for Fuel Surcharges (BAF), Currency Adjustment Factors (CAF), and Terminal Handling Charges (THC).
- **`CustomsTariffSchedules`**: HS Code duty rates, preferential tariff rules (e.g. EVFTA, CPTPP, ASEAN), and VAT percentages.
- **`ExchangeRates`**: Currency pairs (`USD`, `EUR`, `VND`, `SGD`), effective timestamps, and bid/ask spreads.
- **`FreightCostEstimations`**: Itemized price quote records with full breakdown.

---

## 4. API & Contract Surface

Exposed endpoints:
- `POST /api/v1/financial/rates/calculate`: Computes itemized freight cost (base rate, chargeable weight, and all surcharges).
- `POST /api/v1/financial/customs/estimate`: Computes import duties, excise taxes, and VAT from CIF invoice value and HS Code.
- `GET /api/v1/financial/exchange-rates`: Retrieves current authenticated currency conversion rates.
- `POST /api/v1/financial/rate-cards`: Admin API to configure lane pricing matrices.

---

## 5. Security & Invariants

1. **Zero Floating-Point Drift**: All financial calculations use high-precision decimals (`Decimal.js` / BigNumber) with strict ISO currency rounding.
2. **Deterministic Rules**: Pricing is strictly mathematical; AI models cannot modify rate card numbers.
3. **Current Maturity**: Production-ready rating and customs duty computation engine.
