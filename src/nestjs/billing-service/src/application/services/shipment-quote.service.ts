import {
  BadRequestException, ConflictException, Injectable, NotFoundException,
  ServiceUnavailableException,
} from '@nestjs/common';
import { Prisma, ShipmentApprovedQuote } from '@prisma/client';
import { PrismaService } from '../../infrastructure/prisma/prisma.service';
import { ShipmentWorkflowGrpcClient } from '../../infrastructure/grpc-clients/shipment-workflow.grpc-client';

export interface CreateShipmentQuoteDraftRequest {
  tenantId: string;
  shipmentId: string;
  customerId: string;
  currency: string;
  listPrice: string;
  floorPrice: string;
  evidenceReference: string;
  validFrom: string;
  validUntil: string;
}

export interface ShipmentQuoteActionRequest {
  tenantId: string;
  quoteId: string;
}

export interface RevokeShipmentQuoteRequest extends ShipmentQuoteActionRequest {
  reason: string;
}

export interface ListShipmentQuotesRequest {
  tenantId: string;
  shipmentId: string;
  customerId?: string;
}

export interface GetApprovedShipmentQuoteRequest {
  tenantId: string;
  shipmentId: string;
  customerId: string;
}

@Injectable()
export class ShipmentQuoteService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly shipments: ShipmentWorkflowGrpcClient,
  ) {}

  async createDraft(input: CreateShipmentQuoteDraftRequest, actorUserId: string) {
    const tenantId = this.required(input.tenantId, 'tenantId');
    const shipmentId = this.required(input.shipmentId, 'shipmentId');
    const customerId = this.required(input.customerId, 'customerId');
    const evidenceReference = this.required(input.evidenceReference, 'evidenceReference');
    if (evidenceReference.length > 1000) throw new BadRequestException('evidenceReference is too long');
    if (input.currency?.toUpperCase() !== 'USD') {
      throw new BadRequestException('Only USD is supported');
    }
    const listPrice = this.money(input.listPrice, 'listPrice');
    const floorPrice = this.money(input.floorPrice, 'floorPrice');
    if (floorPrice.gt(listPrice)) {
      throw new BadRequestException('floorPrice must not exceed listPrice');
    }
    const validFrom = this.date(input.validFrom, 'validFrom');
    const validUntil = this.date(input.validUntil, 'validUntil');
    if (validFrom >= validUntil || validUntil <= new Date()) {
      throw new BadRequestException('Quote validity must end after its start and in the future');
    }
    await this.verifyShipment(tenantId, shipmentId, customerId);

    const latest = await this.prisma.shipmentApprovedQuote.findFirst({
      where: { tenantId, shipmentId, customerId }, orderBy: { revision: 'desc' },
    });
    try {
      const quote = await this.prisma.shipmentApprovedQuote.create({
        data: {
          tenantId, shipmentId, customerId,
          revision: (latest?.revision ?? 0) + 1,
          currency: 'USD', listPrice, floorPrice, evidenceReference,
          validFrom, validUntil, status: 'DRAFT', createdBy: actorUserId,
        },
      });
      return this.map(quote);
    } catch (error) {
      if (this.isConcurrentConflict(error)) {
        throw new ConflictException('Quote revision changed; reload and try again');
      }
      throw error;
    }
  }

  async approve(input: ShipmentQuoteActionRequest, actorUserId: string) {
    const tenantId = this.required(input.tenantId, 'tenantId');
    const quoteId = this.required(input.quoteId, 'quoteId');
    const draft = await this.prisma.shipmentApprovedQuote.findFirst({ where: { id: quoteId, tenantId } });
    if (!draft) throw new NotFoundException('Quote not found');
    if (draft.status !== 'DRAFT') throw new ConflictException('Only a draft quote can be approved');
    if (draft.createdBy === actorUserId) {
      throw new ConflictException('The quote creator cannot approve their own quote');
    }
    if (draft.validFrom > new Date()) throw new ConflictException('Quote is not yet effective');
    if (draft.validUntil <= new Date()) throw new ConflictException('Quote has expired');
    await this.verifyShipment(tenantId, draft.shipmentId, draft.customerId);

    try {
      return await this.prisma.$transaction(async (tx) => {
        await tx.shipmentApprovedQuote.updateMany({
          where: {
            tenantId, shipmentId: draft.shipmentId, customerId: draft.customerId,
            status: 'APPROVED',
          },
          data: { status: 'SUPERSEDED' },
        });
        const changed = await tx.shipmentApprovedQuote.updateMany({
          where: { id: quoteId, tenantId, status: 'DRAFT' },
          data: { status: 'APPROVED', approvedBy: actorUserId, approvedAt: new Date() },
        });
        if (changed.count !== 1) throw new ConflictException('Quote was changed by another user');
        const approved = await tx.shipmentApprovedQuote.findUniqueOrThrow({ where: { id: quoteId } });
        return this.map(approved);
      }, { isolationLevel: Prisma.TransactionIsolationLevel.Serializable });
    } catch (error) {
      if (this.isConcurrentConflict(error)) {
        throw new ConflictException('Another quote was approved concurrently; reload and try again');
      }
      throw error;
    }
  }

  async revoke(input: RevokeShipmentQuoteRequest, actorUserId: string) {
    const tenantId = this.required(input.tenantId, 'tenantId');
    const quoteId = this.required(input.quoteId, 'quoteId');
    const reason = this.required(input.reason, 'reason');
    if (reason.length > 1000) throw new BadRequestException('reason is too long');
    const quote = await this.prisma.shipmentApprovedQuote.findFirst({ where: { id: quoteId, tenantId } });
    if (!quote) throw new NotFoundException('Quote not found');
    const changed = await this.prisma.shipmentApprovedQuote.updateMany({
      where: { id: quoteId, tenantId, status: 'APPROVED' },
      data: { status: 'REVOKED', revokedBy: actorUserId, revokedAt: new Date(), revokeReason: reason },
    });
    if (changed.count !== 1) throw new ConflictException('Only an approved quote can be revoked');
    return this.map(await this.prisma.shipmentApprovedQuote.findUniqueOrThrow({ where: { id: quoteId } }));
  }

  async list(input: ListShipmentQuotesRequest) {
    const tenantId = this.required(input.tenantId, 'tenantId');
    const shipmentId = this.required(input.shipmentId, 'shipmentId');
    const quotes = await this.prisma.shipmentApprovedQuote.findMany({
      where: { tenantId, shipmentId, ...(input.customerId ? { customerId: input.customerId.trim() } : {}) },
      orderBy: [{ revision: 'desc' }, { createdAt: 'desc' }],
      take: 100,
    });
    return { quotes: quotes.map((quote) => this.map(quote)) };
  }

  async getApproved(input: GetApprovedShipmentQuoteRequest) {
    const tenantId = this.required(input.tenantId, 'tenantId');
    const shipmentId = this.required(input.shipmentId, 'shipmentId');
    const customerId = this.required(input.customerId, 'customerId');
    const now = new Date();
    const quote = await this.prisma.shipmentApprovedQuote.findFirst({
      where: {
        tenantId, shipmentId, customerId, status: 'APPROVED',
        validFrom: { lte: now }, validUntil: { gt: now },
      },
      orderBy: { revision: 'desc' },
    });
    if (!quote || !quote.approvedBy || !quote.approvedAt) {
      throw new NotFoundException('No approved, current quote for this shipment and customer');
    }
    return this.map(quote);
  }

  private async verifyShipment(tenantId: string, shipmentId: string, customerId: string) {
    let shipment;
    try {
      shipment = await this.shipments.getShipment(tenantId, shipmentId);
    } catch {
      throw new ServiceUnavailableException('Shipment verification is unavailable');
    }
    if (shipment.id?.toLowerCase() !== shipmentId.toLowerCase() ||
        shipment.tenantId?.toLowerCase() !== tenantId.toLowerCase() ||
        shipment.customerId?.toLowerCase() !== customerId.toLowerCase()) {
      throw new BadRequestException('Shipment does not belong to the stated tenant and customer');
    }
    if (shipment.status === 'Cancelled' || shipment.status === 'Completed') {
      throw new ConflictException('Cannot quote a closed shipment');
    }
  }

  private required(value: string, field: string) {
    const trimmed = value?.trim();
    if (!trimmed) throw new BadRequestException(`${field} is required`);
    return trimmed;
  }

  private money(value: string, field: string) {
    if (!/^(?:0|[1-9]\d{0,15})(?:\.\d{1,2})?$/.test(value || '')) {
      throw new BadRequestException(`${field} must be a positive USD decimal with at most two places`);
    }
    const amount = new Prisma.Decimal(value);
    if (amount.lte(0) || amount.mul(100).gt(Number.MAX_SAFE_INTEGER)) {
      throw new BadRequestException(`${field} is outside the supported range`);
    }
    return amount;
  }

  private date(value: string, field: string) {
    const date = new Date(value);
    if (!value || !Number.isFinite(date.getTime()) || !/Z$|[+-]\d{2}:\d{2}$/.test(value)) {
      throw new BadRequestException(`${field} must be an ISO timestamp with timezone`);
    }
    return date;
  }

  private isConcurrentConflict(error: unknown) {
    return ['P2002', 'P2034'].includes((error as { code?: string })?.code || '');
  }

  private map(quote: ShipmentApprovedQuote) {
    return {
      id: quote.id, tenantId: quote.tenantId, shipmentId: quote.shipmentId,
      customerId: quote.customerId, revision: quote.revision, currency: quote.currency,
      listPrice: quote.listPrice.toFixed(2), floorPrice: quote.floorPrice.toFixed(2),
      evidenceReference: quote.evidenceReference,
      validFrom: quote.validFrom.toISOString(), validUntil: quote.validUntil.toISOString(),
      status: quote.status, createdBy: quote.createdBy, createdAt: quote.createdAt.toISOString(),
      approvedBy: quote.approvedBy || '', approvedAt: quote.approvedAt?.toISOString() || '',
      revokedBy: quote.revokedBy || '', revokedAt: quote.revokedAt?.toISOString() || '',
      revokeReason: quote.revokeReason || '',
    };
  }
}
