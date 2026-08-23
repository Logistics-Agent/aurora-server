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
      get: jest.fn().mockImplementation((key: string) => {
        if (key === 'NODE_ENV') return 'development';
        return undefined;
      }),
    };

    const promptBuilder = new ConversationalPromptBuilder();
    summaryService = new ConversationSummaryService(mockConfig, repo, mockAiGovernance, promptBuilder);
  });

  it('should generate summary using capability assistant.summarize and update conversation watermark', async () => {
    const conv = await repo.createConversation({
      id: 'conv-100',
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
    expect(updated?.summaryUpToSequence).toBe(1);
  });

  it('OldSummaryJob_ShouldNotOverwriteNewerSummary', async () => {
    const conv = await repo.createConversation({
      id: 'conv-200',
      tenantId: 'tenant-1',
      userId: 'user-1',
      actorType: ActorType.STAFF,
      preferredLanguage: 'vi',
      status: 'ACTIVE',
      summary: 'Newer summary up to 10',
      summaryUpToSequence: 10,
      version: 1,
      createdAt: new Date(),
      updatedAt: new Date(),
      lastActivityAt: new Date(),
    });

    // Stale job arrives requesting summary up to 5
    await summaryService.handleSummaryJob({
      conversationId: 'conv-200',
      tenantId: 'tenant-1',
      userId: 'user-1',
      upToSequenceNumber: 5,
    });

    // AiGovernance.generate should not be called for stale job
    expect(mockAiGovernance.generate).not.toHaveBeenCalled();

    const updated = await repo.getConversation('tenant-1', 'user-1', 'conv-200');
    expect(updated?.summary).toBe('Newer summary up to 10');
    expect(updated?.summaryUpToSequence).toBe(10);
  });

  it('Production_RabbitUnavailable_ShouldNotRunInProcessSummary', async () => {
    const prodConfig: any = {
      get: jest.fn().mockImplementation((key: string) => {
        if (key === 'NODE_ENV') return 'production';
        return undefined;
      }),
    };

    const prodSummaryService = new ConversationSummaryService(
      prodConfig,
      repo,
      mockAiGovernance,
      new ConversationalPromptBuilder(),
    );

    // Enqueue summary job when rabbit is offline in production
    await prodSummaryService.enqueueSummaryJob({
      conversationId: 'conv-prod-1',
      tenantId: 'tenant-1',
      userId: 'user-1',
      upToSequenceNumber: 10,
    });

    // AiGovernance should NOT have been called in-process
    expect(mockAiGovernance.generate).not.toHaveBeenCalled();
  });
});
