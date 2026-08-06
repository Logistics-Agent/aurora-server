export interface EstimateCostRequest {
    originCountry: string;
    originPort: string;
    destinationCountry: string;
    destinationPort: string;
    weightKg: number;
    volumeCbm: number;
    cargoType: string;
    hsCodes: string[];
}
export interface EstimateCostResponse {
    baseFreightCost: number;
    portHandlingFees: number;
    customsDuties: number;
    totalEstimatedCost: number;
    currency: string;
    calculationMethod: string;
    description: string;
}
export interface GetCustomsDutyRequest {
    originCountry: string;
    destinationCountry: string;
    hsCode: string;
}
export interface GetCustomsDutyResponse {
    hsCode: string;
    dutyRatePercentage: number;
    description: string;
}
