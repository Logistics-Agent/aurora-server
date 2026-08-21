package com.aurora.devopsagent.Infrastructure.RAG;

import com.aurora.devopsagent.Domain.ValueObject.RedactedIncidentContext;
import com.aurora.devopsrag.grpc.DevOpsRagServiceGrpc;
import com.aurora.devopsrag.grpc.KnowledgeEntrySnippet;
import com.aurora.devopsrag.grpc.QueryKnowledgeRequest;
import com.aurora.devopsrag.grpc.QueryKnowledgeResponse;
import com.aurora.shared.constants.GrpcMetadataKeys;
import io.grpc.CallOptions;
import io.grpc.Channel;
import io.grpc.ClientCall;
import io.grpc.ClientInterceptor;
import io.grpc.ForwardingClientCall;
import io.grpc.Metadata;
import io.grpc.MethodDescriptor;
import io.grpc.StatusRuntimeException;
import net.devh.boot.grpc.client.inject.GrpcClient;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.util.Collections;
import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * DevOpsRagClient: Consumes DevOps-RAG service for runbook & past incident knowledge retrieval.
 * Invariant: Accepts strictly sanitized RedactedIncidentContext.
 */
@Service
public class DevOpsRagClient {

    private static final Logger log = LoggerFactory.getLogger(DevOpsRagClient.class);
    private static final String SERVICE_ID = "devops-agent";
    private static final long TIMEOUT_SECONDS = 5;

    public record RetrievalResult(
            List<KnowledgeEntrySnippet> snippets,
            boolean success,
            String failureReason
    ) {
        public static RetrievalResult success(List<KnowledgeEntrySnippet> snippets) {
            return new RetrievalResult(snippets, true, null);
        }

        public static RetrievalResult fallback(String failureReason) {
            return new RetrievalResult(Collections.emptyList(), false, failureReason);
        }
    }

    @GrpcClient("rag-service")
    private DevOpsRagServiceGrpc.DevOpsRagServiceBlockingStub ragStub;

    public DevOpsRagClient() {}

    public DevOpsRagClient(DevOpsRagServiceGrpc.DevOpsRagServiceBlockingStub ragStub) {
        this.ragStub = ragStub;
    }

    /**
     * Query knowledge using sanitized RedactedIncidentContext.
     */
    public RetrievalResult queryKnowledge(RedactedIncidentContext context) {
        if (ragStub == null) {
            log.warn("DevOpsRag stub not configured; falling back to empty knowledge context.");
            return RetrievalResult.fallback("STUB_UNCONFIGURED");
        }

        QueryKnowledgeRequest request = QueryKnowledgeRequest.newBuilder()
                .setQuery(context.errorSignature() != null ? context.errorSignature() : "")
                .setErrorSignature(context.errorSignature() != null ? context.errorSignature() : "")
                .setServiceContext(context.affectedService() != null ? context.affectedService() : "")
                .setTopK(5)
                .build();

        try {
            DevOpsRagServiceGrpc.DevOpsRagServiceBlockingStub stubWithMetadata = ragStub
                    .withInterceptors(new ClientInterceptor() {
                        @Override
                        public <ReqT, RespT> ClientCall<ReqT, RespT> interceptCall(
                                MethodDescriptor<ReqT, RespT> method,
                                CallOptions callOptions,
                                Channel next) {
                            return new ForwardingClientCall.SimpleForwardingClientCall<>(next.newCall(method, callOptions)) {
                                @Override
                                public void start(Listener<RespT> responseListener, Metadata headers) {
                                    headers.put(GrpcMetadataKeys.SERVICE_ID, SERVICE_ID);
                                    if (context.correlationId() != null) {
                                        headers.put(GrpcMetadataKeys.CORRELATION_ID, context.correlationId());
                                    }
                                    super.start(responseListener, headers);
                                }
                            };
                        }
                    })
                    .withDeadlineAfter(TIMEOUT_SECONDS, TimeUnit.SECONDS);

            QueryKnowledgeResponse response = stubWithMetadata.queryKnowledge(request);
            log.debug("Retrieved {} knowledge snippets from DevOps-RAG for correlationId={}",
                    response.getEntriesCount(), context.correlationId());
            return RetrievalResult.success(response.getEntriesList());

        } catch (StatusRuntimeException e) {
            String failureCode = e.getStatus().getCode().name();
            log.warn("DevOps-RAG service query failed (status={}): {}. Falling back to empty knowledge.",
                    failureCode, e.getMessage());
            return RetrievalResult.fallback(failureCode);
        } catch (Exception e) {
            log.warn("DevOps-RAG unexpected error: {}. Falling back to empty knowledge.", e.getMessage());
            return RetrievalResult.fallback("UNEXPECTED_ERROR: " + e.getMessage());
        }
    }
}
