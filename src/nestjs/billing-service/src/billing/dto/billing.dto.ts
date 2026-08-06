export interface InvoiceLineItemInput {
  description: string;
  amount: number;
  category: string;
}

export interface CreateInvoiceRequest {
  tenantId: string;
  shipmentId: string;
  items: InvoiceLineItemInput[];
  dueDate: string;
}

export interface GetInvoiceRequest {
  invoiceId: string;
}

export interface ListInvoicesRequest {
  tenantId: string;
  page?: number;
  limit?: number;
  status?: string;
}

export interface InvoiceLineItemMessage {
  id: string;
  description: string;
  amount: number;
  category: string;
}

export interface InvoiceResponse {
  id: string;
  tenantId: string;
  shipmentId: string;
  invoiceNumber: string;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  status: string;
  dueDate: string;
  createdAt: string;
  items: InvoiceLineItemMessage[];
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
  tenantId: string;
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
