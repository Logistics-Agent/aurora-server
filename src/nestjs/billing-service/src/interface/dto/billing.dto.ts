export interface GenerateInvoiceRequest {
  tenantId?: string;
  shipmentId: string;
  customerId?: string;
  paymentTermsDays?: number;
}

export interface CreditCheckRequest {
  tenantId?: string;
  customerId: string;
  newAmount?: number;
}

export interface CreditCheckResponse {
  customerId: string;
  isCreditApproved: boolean;
  creditLimit: number;
  currentOutstandingDebt: number;
  availableCredit: number;
  overdueInvoiceCount: number;
  message: string;
}

export interface InvoiceLineItemInput {
  description: string;
  amount: number;
  quantity?: number;
  unitPrice?: number;
  category?: string;
}

export interface CreateInvoiceRequest {
  tenantId?: string;
  shipmentId: string;
  customerId?: string;
  items: InvoiceLineItemInput[];
  dueDate?: string;
}

export interface GetInvoiceRequest {
  invoiceId: string;
}

export interface ListInvoicesRequest {
  tenantId?: string;
  page?: number;
  limit?: number;
  status?: string;
}

export interface InvoiceLineItemMessage {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
  category: string;
}

export interface PaymentRecordMessage {
  id: string;
  amountPaid: number;
  paymentMethod: string;
  transactionRef: string;
  status: string;
  createdAt: string;
}

export interface InvoiceResponse {
  id: string;
  tenantId: string;
  shipmentId: string;
  customerId: string;
  invoiceNumber: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  currency: string;
  status: string;
  dueDate: string;
  pdfS3Key: string;
  pdfUrl: string;
  createdAt: string;
  items: InvoiceLineItemMessage[];
}

export interface InvoiceDetailResponse {
  invoice: InvoiceResponse;
  payments: PaymentRecordMessage[];
  remainingBalance: number;
}

export interface ListInvoicesResponse {
  invoices: InvoiceResponse[];
  totalItems: number;
  page: number;
  limit: number;
}

export interface UpdateInvoiceStatusRequest {
  invoiceId: string;
  status: string;
}

export interface CreateEscrowWalletRequest {
  tenantId?: string;
  carrierId: string;
  currency?: string;
}

export interface GetWalletBalanceRequest {
  walletId: string;
}

export interface WalletResponse {
  walletId: string;
  tenantId: string;
  carrierId: string;
  balance: number;
  frozenAmount: number;
  availableAmount: number;
  currency: string;
}

export interface FreezeEscrowRequest {
  walletId: string;
  shipmentId: string;
  amount: number;
  referenceNo?: string;
}

export interface ReleaseEscrowRequest {
  walletId: string;
  shipmentId: string;
  amount: number;
  referenceNo?: string;
}

export interface RefundEscrowRequest {
  walletId: string;
  shipmentId: string;
  amount: number;
  referenceNo?: string;
}

export interface TransactionResponse {
  transactionId: string;
  walletId: string;
  shipmentId: string;
  type: string;
  amount: number;
  status: string;
  referenceNo: string;
  createdAt: string;
}

// ── New: RecordPayment ────────────────────────────────────────────────────

export interface RecordPaymentRequest {
  tenantId?: string;
  invoiceId: string;
  amountPaid: number;
  paymentMethod: string;  // 'BANK_TRANSFER' | 'CREDIT_CARD' | 'CASH'
  transactionRef?: string;
}

export interface RecordPaymentResponse {
  paymentRecordId: string;
  invoiceId: string;
  amountPaid: number;
  paymentMethod: string;
  transactionRef: string;
  newInvoiceStatus: string;
  remainingBalance: number;
  createdAt: string;
}

// ── New: CancelInvoice ────────────────────────────────────────────────────

export interface CancelInvoiceRequest {
  invoiceId: string;
  reason?: string;
}

// ── TASK-003: Debit Note / Credit Note ───────────────────────────────────────

export interface IssueDebitNoteRequest {
  tenantId?: string;
  invoiceId: string;
  reasonCode: string;  // DEMURRAGE | DETENTION | CUSTOMS_INSPECTION | WEIGHT_DISCREPANCY
  amount: number;
  description: string;
}

export interface IssueCreditNoteRequest {
  tenantId?: string;
  invoiceId: string;
  reasonCode: string;  // WEIGHT_DISCREPANCY | OVERCHARGE | DAMAGE_COMPENSATION
  amount: number;
  description: string;
}

export interface AdjustmentNoteResponse {
  adjustmentNoteId: string;
  invoiceId: string;
  type: string;          // DEBIT | CREDIT
  reasonCode: string;
  amount: number;
  description: string;
  status: string;
  newInvoiceTotalAmount: number;  // Tổng hoá đơn sau điều chỉnh
  createdAt: string;
}


