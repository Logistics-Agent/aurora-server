import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import { BillingService } from './billing.service';
import { BadRequestException } from '@nestjs/common';

describe('BillingService', () => {
  it('enforces idempotency on duplicate transactionRef in recordPayment', async () => {
    let publishedCount = 0;
    const existingInvoice = {
      id: 'inv-1',
      tenantId: 'tenant-1',
      customerId: 'cust-1',
      invoiceNumber: 'INV-202611-0001',
      totalAmount: 1000,
      status: 'PARTIALLY_PAID',
      dueDate: new Date(Date.now() + 86400000),
      payments: [
        {
          id: 'pay-1',
          amountPaid: 400,
          paymentMethod: 'BANK_TRANSFER',
          transactionRef: 'TX-DUP-001',
          status: 'SUCCESS',
          createdAt: new Date('2026-11-14T00:00:00.000Z'),
        },
      ],
    };

    const prismaMock: any = {
      $transaction: async (cb: any) =>
        cb({
          $queryRaw: async () => [{ id: 'inv-1' }],
          invoice: {
            findUnique: async () => existingInvoice,
            update: async () => existingInvoice,
          },
          paymentRecord: {
            create: async () => {
              throw new Error('Should not create duplicate payment record');
            },
          },
        }),
    };

    const messagingMock: any = {
      publishPaymentReceived: async () => {
        publishedCount++;
      },
    };

    const service = new BillingService(prismaMock, {} as any, messagingMock, {} as any);
    const res = await service.recordPayment({
      tenantId: 'tenant-1',
      invoiceId: 'inv-1',
      amountPaid: 400,
      paymentMethod: 'BANK_TRANSFER',
      transactionRef: 'TX-DUP-001',
    });

    assert.equal(res.paymentRecordId, 'pay-1');
    assert.equal(res.remainingBalance, 600);
    assert.equal(publishedCount, 0);
  });

  it('releases agreementId when cancelling an UNPAID invoice so it can be re-issued', async () => {
    let updatedData: any = null;
    const prismaMock: any = {
      invoice: {
        findFirst: async () => ({
          id: 'inv-1',
          tenantId: 'tenant-1',
          invoiceNumber: 'INV-202611-0001',
          status: 'UNPAID',
          subtotal: 1000,
          taxAmount: 50,
          totalAmount: 1050,
          dueDate: new Date(),
          createdAt: new Date(),
          items: [],
        }),
        update: async (args: any) => {
          updatedData = args.data;
          return {
            id: 'inv-1',
            tenantId: 'tenant-1',
            invoiceNumber: 'INV-202611-0001',
            status: 'CANCELLED',
            subtotal: 1000,
            taxAmount: 50,
            totalAmount: 1050,
            dueDate: new Date(),
            createdAt: new Date(),
            items: [],
          };
        },
      },
    };

    const service = new BillingService(prismaMock, {} as any, {} as any, {} as any);
    const res = await service.cancelInvoice({
      tenantId: 'tenant-1',
      invoiceId: 'inv-1',
      reason: 'Customer requested revision',
    });

    assert.equal(res.status, 'CANCELLED');
    assert.equal(updatedData.agreementId, null);
  });

  it('protects escrow freeze against insufficient available funds via CAS updateMany', async () => {
    const prismaMock: any = {
      escrowWallet: {
        findFirst: async () => ({
          id: 'wallet-1',
          tenantId: 'tenant-1',
          carrierId: 'carrier-1',
          balance: 1000,
          frozenAmount: 300,
          currency: 'USD',
        }),
      },
      $transaction: async (cb: any) =>
        cb({
          escrowWallet: {
            updateMany: async () => ({ count: 1 }),
          },
          escrowTransaction: {
            create: async (args: any) => ({
              id: 'tx-1',
              ...args.data,
              createdAt: new Date(),
            }),
          },
        }),
    };

    const service = new BillingService(prismaMock, {} as any, {} as any, {} as any);
    const txRes = await service.freezeEscrowAmount({
      tenantId: 'tenant-1',
      walletId: 'wallet-1',
      shipmentId: 'shp-1',
      amount: 500,
      referenceNo: 'REF-1',
    });
    assert.equal(txRes.type, 'FREEZE');
    assert.equal(txRes.amount, 500);

    await assert.rejects(
      () =>
        service.freezeEscrowAmount({
          tenantId: 'tenant-1',
          walletId: 'wallet-1',
          shipmentId: 'shp-1',
          amount: 800,
          referenceNo: 'REF-2',
        }),
      BadRequestException,
    );
  });
});
