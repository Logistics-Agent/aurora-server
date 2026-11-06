package compliance_test

import (
	"context"
	"errors"
	"testing"

	"aurora/route-planning-agent/internal/infrastructure/external/compliance"
	"aurora/route-planning-agent/proto/complianceragpb"

	"github.com/google/uuid"
	"google.golang.org/grpc"
)

type mockComplianceClient struct {
	checkResp *complianceragpb.CheckRouteComplianceResponse
	checkErr  error
}

func (m *mockComplianceClient) CheckRouteCompliance(ctx context.Context, in *complianceragpb.CheckRouteComplianceRequest, opts ...grpc.CallOption) (*complianceragpb.CheckRouteComplianceResponse, error) {
	if m.checkErr != nil {
		return nil, m.checkErr
	}
	return m.checkResp, nil
}

func TestComplianceClientSuccess(t *testing.T) {
	mockGrpc := &mockComplianceClient{
		checkResp: &complianceragpb.CheckRouteComplianceResponse{
			HasViolations:         true,
			ViolationSummaries:    []string{"Vi phạm giờ cấm xe tải trọng lớn"},
			RequiresHumanApproval: true,
			ApplicableRegulations: []string{"Nghị định 100/2019/NĐ-CP"},
		},
	}

	client := compliance.NewClientWithGrpcClient(mockGrpc, nil)
	tenantID := uuid.New()

	res := client.CheckCompliance(context.Background(), tenantID, "{}", []string{"HeavyWeight"})

	if !res.HasViolations {
		t.Errorf("expected HasViolations to be true")
	}
	if !res.RequiresHumanApproval {
		t.Errorf("expected RequiresHumanApproval to be true")
	}
	if len(res.ViolationSummaries) != 1 {
		t.Errorf("expected 1 violation summary")
	}
}

func TestComplianceClientSoftFail(t *testing.T) {
	mockGrpc := &mockComplianceClient{
		checkErr: errors.New("service unavailable (not deployed yet)"),
	}

	client := compliance.NewClientWithGrpcClient(mockGrpc, nil)
	tenantID := uuid.New()

	// Soft-fail: không được panic hay crash, trả về kết quả an toàn
	res := client.CheckCompliance(context.Background(), tenantID, "{}", []string{})

	if res.HasViolations {
		t.Errorf("expected HasViolations false on soft-fail")
	}
	if res.RequiresHumanApproval {
		t.Errorf("expected RequiresHumanApproval false on soft-fail")
	}
}

