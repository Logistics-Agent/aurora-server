package compliance

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/proto/complianceragpb"

	"github.com/google/uuid"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type ComplianceRagService interface {
	CheckCompliance(ctx context.Context, tenantID uuid.UUID, routeJSON string, riskReasons []string) *dtos.ComplianceCheckResultDto
}

type Client struct {
	grpcClient complianceragpb.ComplianceRagClient
	logger     *slog.Logger
}

func NewClient(target string, logger *slog.Logger) (*Client, error) {
	if logger == nil {
		logger = slog.Default()
	}
	if target == "" {
		target = "localhost:5003"
	}

	conn, err := grpc.NewClient(target, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return nil, fmt.Errorf("không thể kết nối tới ComplianceRag (%s): %w", target, err)
	}

	return &Client{
		grpcClient: complianceragpb.NewComplianceRagClient(conn),
		logger:     logger,
	}, nil
}

func NewClientWithGrpcClient(grpcClient complianceragpb.ComplianceRagClient, logger *slog.Logger) *Client {
	if logger == nil {
		logger = slog.Default()
	}
	return &Client{
		grpcClient: grpcClient,
		logger:     logger,
	}
}

// CheckCompliance gọi gRPC ComplianceRag, có cơ chế soft-fail nếu service không khả dụng
func (c *Client) CheckCompliance(
	ctx context.Context,
	tenantID uuid.UUID,
	routeJSON string,
	riskReasons []string,
) *dtos.ComplianceCheckResultDto {
	// Timeout ngắn phòng khi service downstream chưa sẵn sàng
	callCtx, cancel := context.WithTimeout(ctx, 3*time.Second)
	defer cancel()

	req := &complianceragpb.CheckRouteComplianceRequest{
		TenantId:    tenantID.String(),
		RouteJson:   routeJSON,
		RiskReasons: riskReasons,
	}

	resp, err := c.grpcClient.CheckRouteCompliance(callCtx, req)
	if err != nil {
		// Soft-fail: Log warning nhưng không throw error làm gãy luồng chính
		c.logger.Warn("ComplianceRag service chưa sẵn sàng hoặc timeout — soft fail an toàn",
			slog.String("tenant_id", tenantID.String()),
			slog.Any("error", err),
		)
		return &dtos.ComplianceCheckResultDto{
			HasViolations:         false,
			ViolationSummaries:    []string{},
			DocumentRefs:          []string{},
			ApplicableRegulations: []string{},
			MergedContext:         "",
			RequiresHumanApproval: false,
		}
	}

	return &dtos.ComplianceCheckResultDto{
		HasViolations:         resp.GetHasViolations(),
		ViolationSummaries:    resp.GetViolationSummaries(),
		DocumentRefs:          resp.GetDocumentRefs(),
		ApplicableRegulations: resp.GetApplicableRegulations(),
		MergedContext:         resp.GetMergedContext(),
		RequiresHumanApproval: resp.GetRequiresHumanApproval(),
	}
}

