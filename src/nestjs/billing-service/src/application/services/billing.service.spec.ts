import { describe, it } from 'node:test';
import * as assert from 'node:assert/strict';
import { BillingService } from './billing.service';
import { BadRequestException, ConflictException, UnauthorizedException } from '@nestjs/common';
import { createHmac } from 'crypto';
import { Subject, lastValueFrom, of, throwError } from 'rxjs';
import { IdempotencyInterceptor } from '../../common/interceptors/idempotency.interceptor';

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

  it('verifies HMAC-SHA256 payment webhook signature and rejects forged signatures', () => {
    const service = new BillingService({} as any, {} as any, {} as any, {} as any);
    const payload = {
      tenantId: 'tenant-1',
      invoiceId: 'inv-1',
      amountPaid: 500,
      transactionRef: 'TX-WEBHOOK-001',
    };
    const validSig = createHmac('sha256', 'aurora-webhook-secret')
      .update('tenant-1:inv-1:500.00:TX-WEBHOOK-001')
      .digest('hex');

    assert.doesNotThrow(() => service.verifyPaymentWebhookSignature(payload, validSig));
    assert.throws(
      () => service.verifyPaymentWebhookSignature(payload, 'invalid-forged-signature'),
      UnauthorizedException,
    );
  });

  it('completes recordPayment when post-commit RabbitMQ publish fails', async () => {
    const existingInvoice = {
      id: 'inv-2',
      tenantId: 'tenant-1',
      customerId: 'cust-1',
      invoiceNumber: 'INV-202611-0002',
      totalAmount: 1000,
      status: 'UNPAID',
      dueDate: new Date(Date.now() + 86400000),
      payments: [],
    };
    const prismaMock: any = {
      $transaction: async (cb: any) =>
        cb({
          $queryRaw: async () => [{ id: 'inv-2' }],
          invoice: {
            findUnique: async () => existingInvoice,
            update: async () => ({ ...existingInvoice, status: 'PAID' }),
          },
          paymentRecord: {
            create: async (args: any) => ({
              id: 'pay-new',
              ...args.data,
              createdAt: new Date('2026-11-14T00:00:00.000Z'),
            }),
          },
        }),
    };
    const messagingMock: any = {
      publishPaymentReceived: async () => {
        throw new Error('RabbitMQ connection down');
      },
    };

    const service = new BillingService(prismaMock, {} as any, messagingMock, {} as any);
    const res = await service.recordPayment({
      tenantId: 'tenant-1',
      invoiceId: 'inv-2',
      amountPaid: 1000,
      paymentMethod: 'BANK_TRANSFER',
      transactionRef: 'TX-MQ-DOWN-001',
    });

    assert.equal(res.paymentRecordId, 'pay-new');
    assert.equal(res.newInvoiceStatus, 'PAID');
    assert.equal(res.remainingBalance, 0);
  });

  it('ignores non-SUCCESS payments and applies OVERDUE / FOR UPDATE rules in debit and credit notes', async () => {
    let lockQueries = 0;
    const invoiceUpdates: any[] = [];
    const overdueDate = new Date(Date.now() - 86400000);
    const futureDate = new Date(Date.now() + 86400000);

    let currentInvoice = {
      id: 'inv-adj-1',
      tenantId: 'tenant-1',
      invoiceNumber: 'INV-202611-0099',
      totalAmount: 1000,
      status: 'PARTIALLY_PAID',
      dueDate: overdueDate,
      payments: [
        {
          id: 'p-ok',
          amountPaid: 400,
          paymentMethod: 'BANK_TRANSFER',
          transactionRef: 'TX-OK',
          status: 'SUCCESS',
          createdAt: new Date('2026-11-14T00:00:00.000Z'),
        },
        {
          id: 'p-fail',
          amountPaid: 600,
          paymentMethod: 'BANK_TRANSFER',
          transactionRef: 'TX-FAIL',
          status: 'FAILED',
          createdAt: new Date('2026-11-14T00:00:00.000Z'),
        },
      ],
    };

    const prismaMock: any = {
      invoice: {
        findFirst: async () => ({ ...currentInvoice, items: [] }),
      },
      $transaction: async (cb: any) =>
        cb({
          $queryRaw: async () => {
            lockQueries++;
            return [{ id: currentInvoice.id }];
          },
          invoice: {
            findFirst: async () => currentInvoice,
            update: async (args: any) => {
              invoiceUpdates.push(args.data);
              currentInvoice = { ...currentInvoice, ...args.data };
              return currentInvoice;
            },
          },
          adjustmentNote: {
            create: async (args: any) => ({
              id: `note-${invoiceUpdates.length + 1}`,
              ...args.data,
              createdAt: new Date('2026-11-14T00:00:00.000Z'),
            }),
          },
        }),
    };

    const service = new BillingService(prismaMock, {} as any, {} as any, {} as any);

    const detail = await service.getInvoiceDetail({ tenantId: 'tenant-1', invoiceId: 'inv-adj-1' });
    assert.equal(detail.remainingBalance, 600);

    await service.issueDebitNote({
      tenantId: 'tenant-1',
      invoiceId: 'inv-adj-1',
      amount: 200,
      reasonCode: 'DEMURRAGE',
      description: 'Extra fee',
    });
    assert.equal(invoiceUpdates[0].totalAmount, 1200);
    assert.equal(invoiceUpdates[0].status, 'OVERDUE');

    currentInvoice.dueDate = futureDate;
    await service.issueCreditNote({
      tenantId: 'tenant-1',
      invoiceId: 'inv-adj-1',
      amount: 800,
      reasonCode: 'OVERCHARGE',
      description: 'Adjustment',
    });
    assert.equal(invoiceUpdates[1].totalAmount, 400);
    assert.equal(invoiceUpdates[1].status, 'PAID');
    assert.equal(lockQueries, 2);
  });

  it('enforces atomic IN_PROGRESS lock, replay, and failure cleanup in IdempotencyInterceptor', async () => {
    const configMock: any = {
      get: (key: string, def?: any) => (key === 'redis.disabled' ? true : def),
    };
    const interceptor = new IdempotencyInterceptor(configMock);

    const makeHttpContext = (idempotencyKey: string, tenantBodyId = 'tenant-body-1') =>
      ({
        getType: () => 'http',
        switchToHttp: () => ({
          getRequest: () => ({
            headers: { 'x-idempotency-key': idempotencyKey },
            body: { tenantId: tenantBodyId },
          }),
        }),
      }) as any;

    const inFlight$ = new Subject<any>();
    let handlerCalls = 0;

    // 1. Concurrent request with same key while first is still in flight -> 409 Conflict
    const firstObs = await interceptor.intercept(makeHttpContext('idem-race-1'), {
      handle: () => {
        handlerCalls++;
        return inFlight$.asObservable();
      },
    });
    const firstPromise = lastValueFrom(firstObs);

    await assert.rejects(
      () =>
        interceptor.intercept(makeHttpContext('idem-race-1'), {
          handle: () => {
            handlerCalls++;
            return of({ ok: true });
          },
        }),
      ConflictException,
    );
    assert.equal(handlerCalls, 1);

    // 2. Complete first request, then duplicate key replays cached response without re-running handler
    inFlight$.next({ invoiceId: 'inv-999' });
    inFlight$.complete();
    const firstResult = await firstPromise;
    assert.deepEqual(firstResult, { invoiceId: 'inv-999' });

    const replayObs = await interceptor.intercept(makeHttpContext('idem-race-1'), {
      handle: () => {
        handlerCalls++;
        return of({ invoiceId: 'inv-should-not-run' });
      },
    });
    assert.deepEqual(await lastValueFrom(replayObs), { invoiceId: 'inv-999' });
    assert.equal(handlerCalls, 1);

    // 3. Handler failure cleans up lock so immediate retry executes normally
    const failObs = await interceptor.intercept(makeHttpContext('idem-fail-1'), {
      handle: () => throwError(() => new BadRequestException('Validation failed')),
    });
    await assert.rejects(() => lastValueFrom(failObs), BadRequestException);

    const retryObs = await interceptor.intercept(makeHttpContext('idem-fail-1'), {
      handle: () => of({ retried: true }),
    });
    assert.deepEqual(await lastValueFrom(retryObs), { retried: true });

    await interceptor.onModuleDestroy();
  });
});
