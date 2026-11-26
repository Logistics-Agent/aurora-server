package rag

import (
	"context"
	"log/slog"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/proto/devopsragpb"
	"aurora/shared/constants"

	"google.golang.org/grpc"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
)

const (
	serviceID         = "devops-agent"
	ragTimeoutSeconds = 5 * time.Second
)

type RagQueryStub interface {
	QueryKnowledge(ctx context.Context, in *devopsragpb.QueryKnowledgeRequest, opts ...grpc.CallOption) (*devopsragpb.QueryKnowledgeResponse, error)
}

type DevOpsRagClient struct {
	stub   RagQueryStub
	logger *slog.Logger
}

func NewDevOpsRagClient(stub RagQueryStub, logger *slog.Logger) *DevOpsRagClient {
	if logger == nil {
		logger = slog.Default()
	}
	return &DevOpsRagClient{stub: stub, logger: logger}
}

func (c *DevOpsRagClient) QueryKnowledge(ctx context.Context, redacted domain.RedactedIncidentContext) application.RagRetrievalResult {
	if c.stub == nil {
		c.logger.Warn("DevOpsRag stub not configured; falling back to empty knowledge context")
		return application.RagRetrievalResult{Success: false, FailureReason: "STUB_UNCONFIGURED"}
	}

	req := &devopsragpb.QueryKnowledgeRequest{
		Query:          redacted.ErrorSignature,
		ErrorSignature: redacted.ErrorSignature,
		ServiceContext: redacted.AffectedService,
		TopK:           5,
	}

	pairs := []string{constants.HeaderServiceID, serviceID}
	if redacted.CorrelationID != "" {
		pairs = append(pairs, constants.HeaderCorrelationID, redacted.CorrelationID)
	}
	outCtx, cancel := context.WithTimeout(metadata.NewOutgoingContext(ctx, metadata.Pairs(pairs...)), ragTimeoutSeconds)
	defer cancel()

	resp, err := c.stub.QueryKnowledge(outCtx, req)
	if err != nil {
		if st, ok := status.FromError(err); ok {
			c.logger.Warn("DevOps-RAG query failed, falling back to empty knowledge",
				slog.String("status", st.Code().String()), slog.String("error", st.Message()))
			return application.RagRetrievalResult{Success: false, FailureReason: st.Code().String()}
		}
		c.logger.Warn("DevOps-RAG unexpected error, falling back to empty knowledge", slog.Any("error", err))
		return application.RagRetrievalResult{Success: false, FailureReason: "UNEXPECTED_ERROR: " + err.Error()}
	}

	snippets := make([]application.KnowledgeSnippet, 0, len(resp.GetEntries()))
	for _, entry := range resp.GetEntries() {
		snippets = append(snippets, application.KnowledgeSnippet{
			EntryID:        entry.GetEntryId(),
			Title:          entry.GetTitle(),
			Content:        entry.GetContent(),
			RelevanceScore: entry.GetRelevanceScore(),
		})
	}
	return application.RagRetrievalResult{Snippets: snippets, Success: true}
}
