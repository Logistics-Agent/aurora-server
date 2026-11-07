package services

import (
	"context"
	"fmt"
	"time"

	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/route-planning-agent/internal/domain/repository"

	"github.com/google/uuid"
)

type approvalService struct {
	approvalRepo repository.ApprovalRepository
	routeRepo    repository.RouteRepository
	uow          repository.UnitOfWork
}

func NewApprovalService(
	approvalRepo repository.ApprovalRepository,
	routeRepo repository.RouteRepository,
	uow repository.UnitOfWork,
) ApprovalService {
	return &approvalService{
		approvalRepo: approvalRepo,
		routeRepo:    routeRepo,
		uow:          uow,
	}
}

func (s *approvalService) Create(
	ctx context.Context,
	routeID uuid.UUID,
	reason, aiSummary string,
	complianceSummary *string,
	routeVersion int32,
	policyID string,
	policyVersion int32,
	tenantID uuid.UUID,
) (*entities.ApprovalRequest, error) {
	if routeVersion <= 0 {
		routeVersion = 1
	}
	if policyID == "" {
		policyID = PlatformDefaultPolicyID
	}
	if policyVersion <= 0 {
		policyVersion = 1
	}

	approval := &entities.ApprovalRequest{
		RouteID:           routeID,
		RouteVersion:      routeVersion,
		PolicyID:          policyID,
		PolicyVersion:     policyVersion,
		Feature:           "RoutePlanning",
		Status:            enums.ApprovalStatusPending,
		Reason:            reason,
		AiSummary:         aiSummary,
		ComplianceSummary: complianceSummary,
	}
	approval.ID = uuid.New()
	approval.TenantID = tenantID
	approval.CreatedAt = time.Now().UTC()

	if err := s.approvalRepo.Create(ctx, approval); err != nil {
		return nil, fmt.Errorf("tạo yêu cầu phê duyệt thất bại: %w", err)
	}

	return approval, nil
}

func (s *approvalService) Approve(
	ctx context.Context,
	approvalID, reviewerUserID, tenantID uuid.UUID,
	comment *string,
) (*entities.ApprovalRequest, error) {
	var approval *entities.ApprovalRequest

	err := s.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		var err error
		approval, err = s.approvalRepo.GetByID(txCtx, tenantID, approvalID)
		if err != nil {
			return err
		}
		if approval == nil {
			return domain.ErrApprovalNotFound
		}

		if approval.Status != enums.ApprovalStatusPending {
			return domain.ErrApprovalAlreadyReviewed
		}

		now := time.Now().UTC()
		approval.Status = enums.ApprovalStatusApproved
		approval.ReviewedByUserID = &reviewerUserID
		approval.ReviewedAt = &now
		approval.ReviewerComment = comment

		if err := s.approvalRepo.Update(txCtx, approval); err != nil {
			return err
		}

		// Tuyến đường chuyển sang trạng thái Ready và StaffAllowed
		route, err := s.routeRepo.GetByID(txCtx, tenantID, approval.RouteID)
		if err != nil {
			return fmt.Errorf("không tìm thấy tuyến đường liên kết với approval: %w", err)
		}
		route.Status = enums.RouteStatusReady
		route.GovernanceDecision = enums.GovernanceDecisionStaffAllowed
		if err := s.routeRepo.Update(txCtx, route); err != nil {
			return fmt.Errorf("cập nhật trạng thái tuyến đường sau phê duyệt thất bại: %w", err)
		}

		return nil
	})

	if err != nil {
		return nil, err
	}

	return approval, nil
}

func (s *approvalService) Reject(
	ctx context.Context,
	approvalID, reviewerUserID, tenantID uuid.UUID,
	reason string,
	comment *string,
) (*entities.ApprovalRequest, error) {
	if reason == "" {
		return nil, domain.NewDomainError("INVALID_ARGUMENT", "Reason là bắt buộc khi reject approval request")
	}

	var approval *entities.ApprovalRequest

	err := s.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		var err error
		approval, err = s.approvalRepo.GetByID(txCtx, tenantID, approvalID)
		if err != nil {
			return err
		}
		if approval == nil {
			return domain.ErrApprovalNotFound
		}

		if approval.Status != enums.ApprovalStatusPending {
			return domain.ErrApprovalAlreadyReviewed
		}

		now := time.Now().UTC()
		approval.Status = enums.ApprovalStatusRejected
		approval.ReviewedByUserID = &reviewerUserID
		approval.ReviewedAt = &now
		approval.RejectionReason = &reason
		approval.ReviewerComment = comment

		if err := s.approvalRepo.Update(txCtx, approval); err != nil {
			return err
		}

		// Từ chối -> chuyển về Draft để nhân viên chỉnh sửa lại
		route, err := s.routeRepo.GetByID(txCtx, tenantID, approval.RouteID)
		if err != nil {
			return fmt.Errorf("không tìm thấy tuyến đường liên kết với approval: %w", err)
		}
		route.Status = enums.RouteStatusDraft
		route.GovernanceDecision = enums.GovernanceDecisionManagerApprovalRequired
		if err := s.routeRepo.Update(txCtx, route); err != nil {
			return fmt.Errorf("cập nhật trạng thái tuyến đường sau từ chối thất bại: %w", err)
		}

		return nil
	})

	if err != nil {
		return nil, err
	}

	return approval, nil
}

