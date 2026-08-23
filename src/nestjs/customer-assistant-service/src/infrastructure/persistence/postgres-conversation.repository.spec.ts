import { InMemoryConversationStore } from './in-memory-conversation.store';
import { ActorType } from '../../domain/enums/actor-type.enum';
import { ConversationConcurrencyConflictException } from '../../domain/errors/assistant.errors';

describe('Conversation Repository Persistence & Concurrency', () => {
  let repo: InMemoryConversationStore;

  beforeEach(() => {
    repo = new InMemoryConversationStore();
  });

  it('should allocate deterministic sequence numbers per conversation message', async () => {
    const conv = await repo.createConversation({
      id: 'conv-seq-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    const m1 = await repo.appendMessage('tenant-1', 'user-1', {
      id: 'msg-1',
      conversationId: conv.id,
      role: 'USER',
      content: 'Turn 1',
      createdAt: new Date(),
    });

    const m2 = await repo.appendMessage('tenant-1', 'user-1', {
      id: 'msg-2',
      conversationId: conv.id,
      role: 'ASSISTANT',
      content: 'Turn 2',
      createdAt: new Date(),
    });

    expect(m1.sequenceNumber).toBe(1);
    expect(m2.sequenceNumber).toBe(2);

    const history = await repo.getRecentMessages('tenant-1', 'user-1', conv.id, 10);
    expect(history.length).toBe(2);
    expect(history[0].sequenceNumber).toBe(1);
    expect(history[1].sequenceNumber).toBe(2);
  });

  it('should throw ConversationConcurrencyConflictException when updating with stale version', async () => {
    const conv = await repo.createConversation({
      id: 'conv-ver-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    // Update 1: version 1 -> 2
    conv.summary = 'Summary v1';
    await repo.updateConversation(conv, 1);

    expect(conv.version).toBe(2);

    // Update 2 with stale expected version 1 should fail
    conv.summary = 'Stale overwrite';
    await expect(repo.updateConversation(conv, 1)).rejects.toThrow(
      ConversationConcurrencyConflictException,
    );
  });

  it('should filter recent messages by sequence watermark', async () => {
    const conv = await repo.createConversation({
      id: 'conv-watermark-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    for (let i = 1; i <= 5; i++) {
      await repo.appendMessage('tenant-1', 'user-1', {
        id: `msg-${i}`,
        conversationId: conv.id,
        role: i % 2 === 1 ? 'USER' : 'ASSISTANT',
        content: `Message ${i}`,
        createdAt: new Date(),
      });
    }

    // Query messages bounded up to sequence 3
    const bounded = await repo.getRecentMessages('tenant-1', 'user-1', conv.id, 10, 3);
    expect(bounded.length).toBe(3);
    expect(bounded.map((m) => m.sequenceNumber)).toEqual([1, 2, 3]);
  });
});
