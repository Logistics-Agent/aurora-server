# Financial Rating & Customs Tax Service — Deep Technical Details

> **Service Layer**: Rating Mathematics, Surcharges, Duty Calculations & Currency  
> **Source-of-Truth**: `src/nestjs/financial-service`, `financial.service.ts`, `prisma/schema.prisma`.

---

## 1. Freight Rating & Chargeable Weight Mathematics

### 1.1 Chargeable Weight Calculation
In air freight:
$$\text{VolumetricWeight}_{\text{Air}} = \frac{\text{Length (cm)} \times \text{Width (cm)} \times \text{Height (cm)}}{5000}$$
$$\text{ChargeableWeight} = \max(\text{GrossWeightKg}, \; \text{VolumetricWeight}_{\text{Air}})$$

In Ocean LCL (Revenue Ton / CBM):
$$\text{CBM} = \frac{\text{Length (cm)} \times \text{Width (cm)} \times \text{Height (cm)}}{1,000,000}$$
$$\text{RevenueTon} = \max\left(\text{CBM}, \; \frac{\text{GrossWeightKg}}{1000}\right)$$

### 1.2 Freight Cost Summation Formula
$$\text{TotalFreight} = (\text{BaseRate} \times \text{ChargeableWeight}) + \text{BAF} + \text{CAF} + \text{THC}_{\text{Origin}} + \text{THC}_{\text{Dest}} + \text{DocumentationFee}$$

---

## 2. Customs Import Duty & Tax Calculation (CIF Basis)

Import taxes are calculated hierarchically based on **CIF Value** (Cost + Insurance + Freight):

1. **Customs Value (CIF)**:
   $$\text{CIF} = \text{FOB Value} + \text{Freight} + \text{Insurance}$$
2. **Import Duty**:
   $$\text{ImportDuty} = \text{CIF} \times \text{DutyRate}(\text{HS Code})$$
3. **Special Consumption / Excise Tax (SCT)** (if applicable):
   $$\text{SCT} = (\text{CIF} + \text{ImportDuty}) \times \text{SCT Rate}$$
4. **Value-Added Tax (VAT)**:
   $$\text{VAT} = (\text{CIF} + \text{ImportDuty} + \text{SCT}) \times \text{VAT Rate}$$
5. **Total Payable Tax**:
   $$\text{TotalCustomsTax} = \text{ImportDuty} + \text{SCT} + \text{VAT}$$

---

## 3. High-Precision Decimal Implementation

To avoid IEEE 754 floating-point arithmetic errors in financial calculations:
- Uses `Decimal.js` for all arithmetic operations.
- Rounding mode: `ROUND_HALF_UP` (standard banking round).
- Currency conversion is executed prior to final tax summation.
