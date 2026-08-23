import { ConversationSummaryService } from './conversation-summary.service';
import { InMemoryConversationStore } from '../../infrastructure/persistence/in-memory-conversation.store';
import { ConversationalPromptBuilder } from '../prompt/conversational-prompt-builder';
import { ActorType } from '../../domain/enums/actor-type.enum';

describe('ConversationSummaryService', () => {
  let summaryService: ConversationSummaryService;
  let repo: InMemoryConversationStore;
  let mockAiGovernance: any;
  let mockConfig: any;

  beforeEach(() => {
    repo = new InMemoryConversationStore();
    mockAiGovernance = {
      generate: jest.fn().mockResolvedValue({
        content: 'Summary: User inquired about DG lithium batteries and SOP packaging.',
        decisionId: 'dec-sum-1',
        automationLevel: 'ASSISTED',
      }),
    };
    mockConfig = {
      get: jest.fn().mockReturnValue(undefined), // default rabbitmq
    };

    const promptBuilder = new ConversationalPromptBuilder();
    summaryService = new ConversationSummaryService(mockConfig, repo, mockAiGovernance, promptBuilder);
  });

  it('should generate summary using capability assistant.summarize and update conversation', async () => {
    const conv = await repo.createConversation({
      id: 'conv-100',
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

    await repo.appendMessage('tenant-1', 'user-1', {
      id: 'm1',
      conversationId: 'conv-100',
      role: 'USER',
      content: 'Hello, what are the packaging rules?',
      createdAt: new Date(),
    });

    await summaryService.handleSummaryJob({
      conversationId: 'conv-100',
      tenantId: 'tenant-1',
      userId: 'user-1',
      expectedConversationVersion: 1,
      upToSequenceNumber: 1,
    });

    expect(mockAiGovernance.generate).toHaveBeenCalledWith(
      'assistant.summarize',
      expect.any(String),
      expect.any(Object),
      256,
    );

    const updated = await repo.getConversation('tenant-1', 'user-1', 'conv-100');
    expect(updated?.summary).toBe('Summary: User inquired about DG lithium batteries and SOP packaging.');
    expect(updated?.version).toBe(2);
  });
});
