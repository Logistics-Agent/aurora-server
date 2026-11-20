import { Injectable, Logger, OnModuleInit } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import * as fs from 'fs';
import * as path from 'path';

export interface AuthoritativeQuotePricing {
  listPrice: number;
  bottomPrice: number;
}

@Injectable()
export class FinancialGrpcClient implements OnModuleInit {
  private readonly logger = new Logger(FinancialGrpcClient.name);
  private client: any = null;
  private grpcUrl = '';

  constructor(private readonly configService: ConfigService) {}

  onModuleInit() {
    const rawUrl = this.configService.get<string>('FINANCIAL_SERVICE_GRPC_URL')?.trim();
    if (!rawUrl) {
      this.logger.warn(
        '[FinancialGrpcClient] FINANCIAL_SERVICE_GRPC_URL is not configured; gRPC pricing lookup disabled.',
      );
      return;
    }

    this.grpcUrl = rawUrl.replace(/^https?:\/\//i, '');
    const possibleProtoPaths = [
      path.resolve(__dirname, '../../../../../../protos/financial.proto'),
      path.resolve(process.cwd(), 'protos/financial.proto'),
      '/protos/financial.proto',
    ];
    const protoPath = possibleProtoPaths.find((p) => fs.existsSync(p));
    if (!protoPath) {
      this.logger.warn('[FinancialGrpcClient] financial.proto not found; gRPC pricing lookup disabled.');
      return;
    }

    try {
      const packageDefinition = protoLoader.loadSync(protoPath, {
        keepCase: false,
        longs: String,
        enums: String,
        defaults: true,
        oneofs: true,
      });

      const protoDescriptor = grpc.loadPackageDefinition(packageDefinition) as any;
      const ServiceCtor = protoDescriptor.financial.FinancialService;
      this.client = new ServiceCtor(this.grpcUrl, grpc.credentials.createInsecure());
      this.logger.log(`[FinancialGrpcClient] Connected to FinancialService gRPC at ${this.grpcUrl}`);
    } catch (err) {
      this.logger.warn(
        `[FinancialGrpcClient] Failed to initialize gRPC client at ${this.grpcUrl}: ${(err as Error).message}`,
      );
    }
  }

  isConfigured(): boolean {
    return Boolean(this.client);
  }

  async resolveQuotePricing(
    tenantId: string,
    shipmentId?: string,
    quoteId?: string,
  ): Promise<AuthoritativeQuotePricing | null> {
    if (!this.client) {
      return null;
    }

    if (quoteId) {
      const byQuote = await this.fetchFirstValidPricing(tenantId, { quoteId });
      if (byQuote) return byQuote;
    }

    if (shipmentId) {
      const byShipment = await this.fetchFirstValidPricing(tenantId, { shipmentId });
      if (byShipment) return byShipment;
    }

    return null;
  }

  private async fetchFirstValidPricing(
    tenantId: string,
    filter: { quoteId?: string; shipmentId?: string },
  ): Promise<AuthoritativeQuotePricing | null> {
    const metadata = new grpc.Metadata();
    metadata.add('x-tenant-id', tenantId);

    const request = {
      tenantId,
      quoteId: filter.quoteId ?? '',
      shipmentId: filter.shipmentId ?? '',
      sourceThreadId: '',
    };

    try {
      const response = await new Promise<any>((resolve, reject) => {
        this.client.ListQuotes(
          request,
          metadata,
          { deadline: Date.now() + 3000 },
          (err: any, res: any) => {
            if (err) return reject(err);
            resolve(res);
          },
        );
      });

      const quotes = Array.isArray(response?.quotes) ? response.quotes : [];
      for (const q of quotes) {
        const listPrice = Number(q?.targetRate ?? q?.target_rate);
        const bottomPrice = Number(q?.minAcceptableRate ?? q?.min_acceptable_rate);
        if (
          Number.isFinite(listPrice) &&
          Number.isFinite(bottomPrice) &&
          listPrice > 0 &&
          bottomPrice > 0 &&
          bottomPrice <= listPrice
        ) {
          return { listPrice, bottomPrice };
        }
      }
      return null;
    } catch (err) {
      this.logger.warn(
        `[FinancialGrpcClient] ListQuotes RPC failed (${JSON.stringify(filter)}): ${(err as Error).message}`,
      );
      return null;
    }
  }
}
