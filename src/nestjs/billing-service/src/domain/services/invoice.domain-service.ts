import { Injectable } from '@nestjs/common';

/**
 * Domain Service: InvoiceDomainService
 * Contains pure domain business logic for invoice number formatting, tax calculations,
 * payment terms (T+30), and credit evaluation.
 */
@Injectable()
export class InvoiceDomainService {
  /**
   * Generates formatted Invoice Number: INV-{YYYYMM}-{SEQ} (e.g. INV-202607-0042)
   */
  generateInvoiceNumber(seqNumber: number, date: Date = new Date()): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const seqFormatted = String(seqNumber).padStart(4, '0');
    return `INV-${year}${month}-${seqFormatted}`;
  }

  /**
   * Calculates Due Date based on payment terms days (e.g. T+30)
   */
  calculateDueDate(startDate: Date = new Date(), termsDays: number = 30): Date {
    const dueDate = new Date(startDate.getTime());
    dueDate.setDate(dueDate.getDate() + (termsDays || 30));
    return dueDate;
  }

  /**
   * Calculates Subtotal, Tax Amount (VAT), and Total Amount
   */
  calculateInvoiceTotals(
    items: { quantity: number; unitPrice: number }[],
    taxRatePercent: number = 5.0,
  ): {
    subtotal: number;
    taxAmount: number;
    totalAmount: number;
  } {
    const subtotal = items.reduce(
      (sum, item) => sum + (item.quantity || 1) * (item.unitPrice || 0),
      0,
    );
    const taxAmount = subtotal * ((taxRatePercent || 0) / 100.0);
    const totalAmount = subtotal + taxAmount;

    return {
      subtotal: Number(subtotal.toFixed(2)),
      taxAmount: Number(taxAmount.toFixed(2)),
      totalAmount: Number(totalAmount.toFixed(2)),
    };
  }

  /**
   * Evaluates Customer Credit Approval based on limit, current debt, and overdue count
   */
  evaluateCreditApproval(
    creditLimit: number,
    currentDebt: number,
    overdueCount: number,
    requestedAmount: number,
  ): {
    isApproved: boolean;
    availableCredit: number;
    reason: string;
  } {
    const availableCredit = Math.max(0, creditLimit - currentDebt);

    if (overdueCount > 0) {
      return {
        isApproved: false,
        availableCredit,
        reason: `Customer has ${overdueCount} overdue unpaid invoices. Credit blocked.`,
      };
    }

    if (requestedAmount > availableCredit) {
      return {
        isApproved: false,
        availableCredit,
        reason: `Requested amount $${requestedAmount} exceeds available credit $${availableCredit}.`,
      };
    }

    return {
      isApproved: true,
      availableCredit: Number((availableCredit - requestedAmount).toFixed(2)),
      reason: 'Credit check approved.',
    };
  }
}
