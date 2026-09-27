import { ConfigService } from '@nestjs/config';
import { ServiceUnavailableException } from '@nestjs/common';
import { RegulatoryComplianceGrpcClient } from './regulatory-compliance.grpc-client';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { CurrentUser } from '../security/current-user.interface';

describe('RegulatoryComplianceGrpcClient knowledge access', () => {
  const customer: CurrentUser = {
    tenantId: 'tenant-1',
    userId: 'customer-1',
    actorType: ActorType.CUSTOMER,
    roles: ['CUSTOMER'],
    permissions: [],
  };

  it('requests only public categories and drops any internal evidence returned by the server', async () => {
    const client = new RegulatoryComplianceGrpcClient({} as ConfigService);
    const queryKnowledge = jest.fn((_request, _metadata, _options, callback) =>
      callback(null, {
        evidence: [
          { category: 'KNOWLEDGE_CATEGORY_SOP', title: 'Internal SOP', excerpt: 'Secret' },
          { category: 'KNOWLEDGE_CATEGORY_PUBLIC_FAQ', title: 'Public FAQ', excerpt: 'Safe' },
        ],
      }),
    );
    (client as unknown as { client: object }).client = { QueryKnowledge: queryKnowledge };

    const evidence = await client.queryKnowledge(
      'lithium batteries',
      ['PUBLIC_FAQ', 'CUSTOMER_GUIDE', 'PUBLIC_PROCEDURE'],
      5,
      0.4,
      customer,
    );

    expect(queryKnowledge.mock.calls[0][0].categories).toEqual([7, 8, 9]);
    expect(evidence.map((item) => item.title)).toEqual(['Public FAQ']);
  });

  it('raises an outage instead of returning an empty search result', async () => {
    const client = new RegulatoryComplianceGrpcClient({} as ConfigService);
    (client as unknown as { client: object }).client = {
      QueryKnowledge: (_request: unknown, _metadata: unknown, _options: unknown, callback: Function) =>
        callback(new Error('unavailable')),
    };

    await expect(client.queryKnowledge('question', ['PUBLIC_FAQ'], 5, 0.4, customer))
      .rejects.toBeInstanceOf(ServiceUnavailableException);
  });
});
