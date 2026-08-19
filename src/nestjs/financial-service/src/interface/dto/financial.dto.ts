export interface EstimateCostRequest {
  tenantId?: string;
  originCountry: string;
  originPort: string;
  destinationCountry: string;
  destinationPort: string;
  weightKg: number;
  volumeCbm: number;
  lengthCm?: number;
  widthCm?: number;
  heightCm?: number;
  transportMode?: string; // AIR, SEA, ROAD
  cargoType?: string;
  cargoValue?: number;
  currency?: string;
  hsCodes?: string[];
  fscRatePercent?: number;         // Fuel Surcharge % (mặc định 10%)
  ebsRatePercent?: number;         // Emergency Bunker Surcharge % (mặc định 0%)
  insuranceRatePercent?: number;   // Cargo Insurance % (mặc định 0.3%)
}

export interface EstimateCostResponse {
  baseFreightCost: number;
  portHandlingFees: number;
  fuelSurchargeFee: number;  // FSC + EBS
  cargoInsuranceFee: number; // Cargo Insurance
  importDutyFee: number;
  vatFee: number;
  totalCustomsFee: number;
  totalEstimatedCost: number;
  chargeableWeightKg: number;
  volumetricWeightKg: number;
  currency: string;
  calculationMethod: string;
  description: string;
}

export interface GetCustomsDutyRequest {
  tenantId?: string;
  originCountry: string;
  destinationCountry: string;
  hsCode: string;
  cargoValue?: number;
}

export interface GetCustomsDutyResponse {
  hsCode: string;
  importTaxRate: number;
  vatRate: number;
  importDutyAmount: number;
  vatAmount: number;
  totalTaxAmount: number;
  description: string;
}

export interface GetMinAcceptableRateRequest {
  tenantId?: string;
  originPort: string;
  destinationPort: string;
  transportMode?: string;
  weightKg: number;
  volumeCbm: number;
  cargoType?: string;
  minMarginPercent?: number;
  targetMarginPercent?: number;
}

export interface GetMinAcceptableRateResponse {
  costPrice: number;
  minAcceptableRate: number;
  targetRate: number;
  currency: string;
  breakdownNote: string;
}

// ── TASK-001: Dynamic Margin Decay ───────────────────────────────────────────
// Dùng cho Negotiation Agent để tính giá sàn thay đổi theo thời gian còn lại
// tới giờ cut-off chuyến tàu/máy bay.

export interface GetDynamicMarginRequest {
  tenantId?: string;
  shipmentId: string;
  costPrice: number;              // Giá thành thực tế (USD)
  baseMarginPercent: number;      // Biên lợi nhuận cơ bản tối đa (%)
  remainingSeconds: number;       // Số giây còn lại tới cut-off
  totalSeconds: number;           // Tổng số giây từ lúc mở đàm phán tới cut-off
  gamma?: number;                 // Hệ số suy giảm (>= 1, mặc định 2)
}

export interface GetDynamicMarginResponse {
  costPrice: number;
  listPrice: number;              // Giá niêm yết (costPrice + baseMargin)
  minAcceptablePrice: number;     // Giá sàn tại thời điểm t (decay theo thời gian)
  currentMarginPercent: number;   // Biên lợi nhuận hiện tại (%)
  decayFactor: number;            // Hệ số suy giảm thực tế (0.0 - 1.0)
  currency: string;
  note: string;
}

// ── TASK-002: Exchange Rate Engine ────────────────────────────────────────────

export interface GetExchangeRateRequest {
  tenantId?: string;
  fromCurrency: string;  // Ví dụ: 'USD'
  toCurrency: string;    // Ví dụ: 'VND'
  date?: string;         // ISO date string (mặc định là hôm nay)
}

export interface GetExchangeRateResponse {
  fromCurrency: string;
  toCurrency: string;
  rate: number;          // 1 fromCurrency = rate toCurrency
  validDate: string;     // ISO date string
  source: string;        // VIETCOMBANK | SBV | MOCK
}

