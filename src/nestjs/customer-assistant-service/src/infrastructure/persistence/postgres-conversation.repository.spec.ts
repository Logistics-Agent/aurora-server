import { InMemoryConversationStore } from './in-memory-conversation.store';
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
});
