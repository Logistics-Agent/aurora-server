import { InMemoryConversationStore } from './in-memory-conversation.store';
import { PostgresConversationRepository } from './postgres-conversation.repository';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ConversationConcurrencyConflictException } from '../../domain/errors/assistant.errors';

describe('Conversation Repository Persistence & Concurrency', () => {
  let repo: InMemoryConversationStore;

  beforeEach(() => {
    repo = new InMemoryConversationStore();
  });

  it('ConcurrentAppendMessages_ShouldAllocateUniqueOrderedSequence', async () => {
    const conv = await repo.createConversation({
      id: 'conv-concurrent-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      summaryUpToSequence: 0,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    // Run concurrent appends
    const p1 = repo.appendMessage('tenant-1', 'user-1', {
      id: 'msg-c1',
      conversationId: conv.id,
      role: 'USER',
      content: 'Concurrent Turn 1',
      createdAt: new Date(),
    });

    const p2 = repo.appendMessage('tenant-1', 'user-1', {
      id: 'msg-c2',
      conversationId: conv.id,
      role: 'ASSISTANT',
      content: 'Concurrent Turn 2',
      createdAt: new Date(),
    });

    const [m1, m2] = await Promise.all([p1, p2]);

    expect(m1.sequenceNumber).toBeDefined();
    expect(m2.sequenceNumber).toBeDefined();
    expect(m1.sequenceNumber).not.toEqual(m2.sequenceNumber);

    const history = await repo.getRecentMessages('tenant-1', 'user-1', conv.id, 10);
    expect(history.length).toBe(2);
    expect(history.map((m) => m.sequenceNumber)).toEqual([1, 2]);
  });

  it('WatermarkSummaryUpdate_ShouldBeIdempotent', async () => {
    const conv = await repo.createConversation({
      id: 'conv-watermark-idemp',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      summaryUpToSequence: 0,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    // First update up to seq 10
    const res1 = await repo.updateSummaryWatermark('tenant-1', 'user-1', conv.id, 'Summary up to 10', 10);
    expect(res1).toBe(true);

    const check1 = await repo.getConversation('tenant-1', 'user-1', conv.id);
    expect(check1?.summary).toBe('Summary up to 10');
    expect(check1?.summaryUpToSequence).toBe(10);

    // Stale update up to seq 8 should be ignored
    const res2 = await repo.updateSummaryWatermark('tenant-1', 'user-1', conv.id, 'Stale summary 8', 8);
    expect(res2).toBe(false);

    const check2 = await repo.getConversation('tenant-1', 'user-1', conv.id);
    expect(check2?.summary).toBe('Summary up to 10'); // unchanged
    expect(check2?.summaryUpToSequence).toBe(10);
  });

  it('UnsummarizedMessagesQuery_ShouldReturnMessagesAfterWatermark', async () => {
    const conv = await repo.createConversation({
      id: 'conv-unsum-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      summaryUpToSequence: 0,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    for (let i = 1; i <= 6; i++) {
      await repo.appendMessage('tenant-1', 'user-1', {
        id: `msg-${i}`,
        conversationId: conv.id,
        role: i % 2 === 1 ? 'USER' : 'ASSISTANT',
        content: `Message ${i}`,
        createdAt: new Date(),
      });
    }

    // Unsummarized after sequence 4
    const unsummarized = await repo.getUnsummarizedMessages('tenant-1', 'user-1', conv.id, 4);
    expect(unsummarized.length).toBe(2);
    expect(unsummarized.map((m) => m.sequenceNumber)).toEqual([5, 6]);
  });

  it('EnforcesMultiTenantAndUserIsolation_WhenReadingMessages', async () => {
    await repo.createConversation({
      id: 'conv-iso-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.CUSTOMER,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      summaryUpToSequence: 0,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });
    await repo.appendMessage('tenant-1', 'user-1', {
      id: 'msg-iso-1',
      conversationId: 'conv-iso-1',
      role: 'USER',
      content: 'Confidential tenant 1 message',
      createdAt: new Date(),
    });

    expect(await repo.getRecentMessages('tenant-2', 'user-1', 'conv-iso-1', 10)).toEqual([]);
    expect(await repo.getRecentMessages('tenant-1', 'user-2', 'conv-iso-1', 10)).toEqual([]);
    expect(await repo.getUnsummarizedMessages('tenant-2', 'user-1', 'conv-iso-1', 0)).toEqual([]);
    expect(await repo.getUnsummarizedMessages('tenant-1', 'user-2', 'conv-iso-1', 0)).toEqual([]);

    const queryCalls: Array<{ sql: string; params: any[] }> = [];
    const pgRepo = new PostgresConversationRepository(
      { get: jest.fn().mockReturnValue('test') } as any,
      { getRecentMessages: jest.fn().mockResolvedValue(null), setRecentMessages: jest.fn() } as any,
    );
    (pgRepo as any).isConnected = true;
    (pgRepo as any).pool = {
      query: jest.fn(async (sql: string, params: any[]) => {
        queryCalls.push({ sql, params });
        return { rows: [] };
      }),
    };

    await pgRepo.getRecentMessages('tenant-2', 'user-1', 'conv-iso-1', 10);
    await pgRepo.getUnsummarizedMessages('tenant-2', 'user-1', 'conv-iso-1', 0, 5, 20);

    expect(queryCalls[0].sql).toContain('INNER JOIN conversations c');
    expect(queryCalls[0].sql).toContain('c.tenant_id = $2');
    expect(queryCalls[0].sql).toContain('c.user_id = $3');
    expect(queryCalls[0].params).toEqual(['conv-iso-1', 'tenant-2', 'user-1', null, 10]);

    expect(queryCalls[1].sql).toContain('INNER JOIN conversations c');
    expect(queryCalls[1].sql).toContain('c.tenant_id = $2');
    expect(queryCalls[1].sql).toContain('c.user_id = $3');
    expect(queryCalls[1].params).toEqual(['conv-iso-1', 'tenant-2', 'user-1', 0, 5, 20]);
  });
});
