import { Prisma } from '@prisma/client';
import { InvoiceDomainService } from './invoice.domain-service';

/** Allocate the next global monthly number while holding a PostgreSQL transaction lock. */
export async function allocateInvoiceNumber(
  tx: Prisma.TransactionClient,
  domainService: InvoiceDomainService,
  now: Date = new Date(),
): Promise<string> {
  await tx.$queryRaw`SELECT true AS locked FROM (SELECT pg_advisory_xact_lock(483291)) AS lock_result`;
  const prefix = domainService.generateInvoiceNumber(0, now).slice(0, -4);
  const rows = await tx.$queryRaw<Array<{ sequence: number }>>`
    SELECT COALESCE(MAX(SUBSTRING(invoice_number FROM 12)::integer), 0) AS sequence
    FROM invoices
    WHERE invoice_number LIKE ${prefix + '%'}
      AND SUBSTRING(invoice_number FROM 12) ~ '^[0-9]+$'
  `;
  return domainService.generateInvoiceNumber(Number(rows[0].sequence) + 1, now);
}
