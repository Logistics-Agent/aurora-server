import { PrismaService } from '../prisma/prisma.service';
export declare class OverdueInvoiceCronJob {
    private readonly prisma;
    private readonly logger;
    constructor(prisma: PrismaService);
    markOverdueInvoices(): Promise<void>;
}
