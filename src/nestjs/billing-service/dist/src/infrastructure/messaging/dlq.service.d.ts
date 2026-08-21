export interface DeadLetterMessage {
    originalTopic: string;
    originalPayload: any;
    errorReason: string;
    retryCount: number;
    failedAt: string;
    tenantId: string;
}
export declare class DLQService {
    private readonly logger;
    sendToDeadLetterQueue(msg: DeadLetterMessage): Promise<void>;
}
