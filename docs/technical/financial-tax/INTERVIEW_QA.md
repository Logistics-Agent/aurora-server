# Financial Rating & Customs Tax Service — Interview Q&A Guide

> **Target Audience**: Junior, Mid-level, Senior & Fintech System Design Interviewers  
> **Source-of-Truth**: Grounded 100% in NestJS `financial-service` implementation.

---

### Q1 (Junior): How is chargeable weight calculated in air freight versus ocean freight?
**Answer**:  
- **Air Freight**: Chargeable weight is the greater of actual gross weight (kg) and volumetric weight calculated using the IATA standard formula: $\frac{\text{Length} \times \text{Width} \times \text{Height (cm)}}{5000}$.
- **Ocean Freight (LCL)**: Priced on Revenue Tons (W/M), which is the greater of total cubic meters (CBM) and metric tons ($\frac{\text{GrossWeightKg}}{1000}$).

---

### Q2 (Mid): How does the service avoid floating-point errors (e.g. `0.1 + 0.2 = 0.30000000000000004`) in billing and tax calculations?
**Answer**:  
JavaScript native `number` types (IEEE 754 floating-point) are strictly prohibited for financial calculations. The service uses arbitrary-precision decimal libraries (`Decimal.js` / BigNumber) for all rate additions, percentages, and duty multiplications, with explicit `ROUND_HALF_UP` banking rounding configured at currency precision (e.g. 2 decimal places for USD, 0 for VND).

---

### Q3 (Mid): How does the customs duty engine calculate taxes for an imported shipment?
**Answer**:  
Taxes are calculated hierarchically on the CIF (Cost, Insurance, Freight) value:
1. **Import Duty** = $\text{CIF} \times \text{DutyRate}(\text{HS Code})$.
2. **Excise Tax** = $(\text{CIF} + \text{ImportDuty}) \times \text{ExciseRate}$.
3. **VAT** = $(\text{CIF} + \text{ImportDuty} + \text{ExciseTax}) \times \text{VAT Rate}$.
The engine looks up the exact HS Code from `CustomsTariffSchedules` taking into account preferential trade agreements (e.g. EVFTA Form EUR.1).

---

### Q4 (Senior): How are volatile foreign exchange rates handled across quotes and invoices?
**Answer**:  
When a quotation is created, the service captures the current exchange rate and stores a locked `ExchangeRateSnapshot` with a validity window (e.g. 14 days). If an invoice is generated within that window, it honors the locked rate, protecting the customer from currency fluctuations. Live spot rates are cached in Redis from central banking feeds with a 1-hour TTL.

---

### Q5 (System Design): What are the tradeoffs of isolating rating and tax into a separate microservice?
**Answer**:  
- **Pros**: Complex financial rules, tariff databases, and currency conversion algorithms are isolated from shipment state machines and billing workflows, enabling independent scaling and localized tax regulation updates.
- **Cons**: Requires an internal network call during quotation and invoice generation, which Aurora optimizes via Redis caching of rate cards and tariffs.
