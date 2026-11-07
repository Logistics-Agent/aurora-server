package handlers

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/application/services"
	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/route-planning-agent/internal/domain/repository"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/internal/infrastructure/external/aigov"
	"aurora/route-planning-agent/internal/infrastructure/external/compliance"
	"aurora/route-planning-agent/internal/infrastructure/external/vroom"
	"aurora/route-planning-agent/internal/transport/grpc/mappers"
	"aurora/route-planning-agent/proto/routeplanningpb"
	sharedctx "aurora/shared/context"
	"aurora/shared/pagination"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

type RoutePlanningHandler struct {
	routeplanningpb.UnimplementedRoutePlanningServiceServer

	routeRepo         repository.RouteRepository
	approvalRepo      repository.ApprovalRepository
	policyRepo        repository.RiskPolicyRepository
	ruleConfigRepo    repository.TenantRuleConfigRepository
	outboxRepo        repository.OutboxRepository
	uow               repository.UnitOfWork
	ruleEngine        domainrules.RuleEngine
	vroomService      vroom.OptimizationService
	aiService         aigov.RouteAiService
	complianceService compliance.ComplianceRagService
	governanceService services.RouteGovernanceService
	approvalService   services.ApprovalService
	policyProvider    services.RouteRiskPolicyProvider
	ruleConfigService domainrules.ITenantRuleConfigService
	logger            *slog.Logger
}

func NewRoutePlanningHandler(
	routeRepo repository.RouteRepository,
	approvalRepo repository.ApprovalRepository,
	policyRepo repository.RiskPolicyRepository,
	ruleConfigRepo repository.TenantRuleConfigRepository,
	outboxRepo repository.OutboxRepository,
	uow repository.UnitOfWork,
	ruleEngine domainrules.RuleEngine,
	vroomService vroom.OptimizationService,
	aiService aigov.RouteAiService,
	complianceService compliance.ComplianceRagService,
	governanceService services.RouteGovernanceService,
	approvalService services.ApprovalService,
	policyProvider services.RouteRiskPolicyProvider,
	ruleConfigService domainrules.ITenantRuleConfigService,
	logger *slog.Logger,
) *RoutePlanningHandler {
	if logger == nil {
		logger = slog.Default()
	}
	return &RoutePlanningHandler{
		routeRepo:         routeRepo,
		approvalRepo:      approvalRepo,
		policyRepo:        policyRepo,
		ruleConfigRepo:    ruleConfigRepo,
		outboxRepo:        outboxRepo,
		uow:               uow,
		ruleEngine:        ruleEngine,
		vroomService:      vroomService,
		aiService:         aiService,
		complianceService: complianceService,
		governanceService: governanceService,
		approvalService:   approvalService,
		policyProvider:    policyProvider,
		ruleConfigService: ruleConfigService,
		logger:            logger,
	}
}

func (h *RoutePlanningHandler) requireTenantID(ctx context.Context) (uuid.UUID, error) {
	tenantID, ok := sharedctx.GetTenantID(ctx)
	if !ok || tenantID == uuid.Nil {
		return uuid.Nil, status.Error(codes.InvalidArgument, "Thiếu hoặc sai định dạng metadata x-tenant-id")
	}
	return tenantID, nil
}

func (h *RoutePlanningHandler) getUserID(ctx context.Context) uuid.UUID {
	if uidStr, ok := sharedctx.GetUserID(ctx); ok && uidStr != "" {
		if uid, err := uuid.Parse(uidStr); err == nil {
			return uid
		}
	}
	return uuid.New()
}

func (h *RoutePlanningHandler) parseUUID(str, fieldName string) (uuid.UUID, error) {
	u, err := uuid.Parse(str)
	if err != nil || u == uuid.Nil {
		return uuid.Nil, status.Errorf(codes.InvalidArgument, "%s '%s' không phải định dạng UUID hợp lệ", fieldName, str)
	}
	return u, nil
}

// ===== Routes =====

func (h *RoutePlanningHandler) CreateRoute(ctx context.Context, req *routeplanningpb.CreateRouteRequest) (*routeplanningpb.RouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	if strings.TrimSpace(req.GetName()) == "" {
		return nil, status.Error(codes.InvalidArgument, "Tên tuyến đường (Name) không được để trống")
	}
	if len(req.GetStops()) < 2 {
		return nil, status.Error(codes.InvalidArgument, "Tuyến đường phải có tối thiểu 2 điểm dừng")
	}
	if req.GetMaxWeightKg() <= 0 {
		return nil, status.Error(codes.InvalidArgument, "Tải trọng tối đa (MaxWeightKg) phải lớn hơn 0")
	}
	if req.GetMaxVolumeM3() <= 0 {
		return nil, status.Error(codes.InvalidArgument, "Thể tích tối đa (MaxVolumeM3) phải lớn hơn 0")
	}

	routeType := enums.ParseRouteType(req.GetRouteType())
	stops := make([]entities.RouteStop, 0, len(req.GetStops()))
	for _, s := range req.GetStops() {
		stops = append(stops, entities.RouteStop{
			ID:                      uuid.New(),
			Sequence:                s.GetSequence(),
			StopType:                enums.ParseStopType(s.GetStopType()),
			LocationName:            s.GetLocationName(),
			Address:                 s.GetAddress(),
			Latitude:                s.GetLatitude(),
			Longitude:               s.GetLongitude(),
			EstimatedArrivalMinutes: s.GetEstimatedArrivalMinutes(),
			ServiceDurationMinutes:  s.GetServiceDurationMinutes(),
		})
	}

	route := &entities.Route{
		Name:                     strings.TrimSpace(req.GetName()),
		Type:                     routeType,
		Status:                   enums.RouteStatusDraft,
		RiskLevel:                enums.RouteRiskLevelLow,
		GovernanceDecision:       enums.GovernanceDecisionNoApprovalRequired,
		EstimatedDistanceKm:      req.GetEstimatedDistanceKm(),
		EstimatedDurationMinutes: req.GetEstimatedDurationMinutes(),
		MaxWeightKg:              req.GetMaxWeightKg(),
		MaxVolumeM3:              req.GetMaxVolumeM3(),
		IsAiGenerated:            false,
		Version:                  1,
		Stops:                    stops,
	}
	route.ID = uuid.New()
	route.TenantID = tenantID
	route.CreatedAt = time.Now().UTC()

	createdByUserID := h.getUserID(ctx)
	if uidStr, ok := sharedctx.GetUserID(ctx); ok && uidStr != "" {
		route.CreatedBy = &uidStr
	}

	if desc := req.GetDescription(); desc != "" {
		route.Description = &desc
	}

	// Ghi entity và outbox event trong cùng một transaction
	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.routeRepo.Create(txCtx, route); err != nil {
			return err
		}

		eventPayload, _ := json.Marshal(dtos.RouteCreatedEvent{
			RouteID:         route.ID,
			TenantID:        tenantID,
			RouteName:       route.Name,
			RouteType:       route.Type.String(),
			RiskLevel:       route.RiskLevel.String(),
			CreatedByUserID: createdByUserID,
		})

		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteCreatedEvent",
			Payload:   string(eventPayload),
		})
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Tạo route thất bại: %v", err)
	}

	return mappers.MapRouteToResponse(route), nil
}

func (h *RoutePlanningHandler) GetRoute(ctx context.Context, req *routeplanningpb.GetRouteRequest) (*routeplanningpb.RouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy tuyến đường: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	return mappers.MapRouteToResponse(route), nil
}

func (h *RoutePlanningHandler) ListRoutes(ctx context.Context, req *routeplanningpb.ListRoutesRequest) (*routeplanningpb.ListRoutesResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	page, limit := pagination.NormalizePagination(req.GetPage(), req.GetLimit())

	var statusFilter *enums.RouteStatus
	if s := req.GetStatus(); s != "" {
		st := enums.ParseRouteStatus(s)
		statusFilter = &st
	}

	routes, total, err := h.routeRepo.List(ctx, tenantID, page, limit, statusFilter)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn danh sách route: %v", err)
	}

	respRoutes := make([]*routeplanningpb.RouteResponse, 0, len(routes))
	for i := range routes {
		respRoutes = append(respRoutes, mappers.MapRouteToResponse(&routes[i]))
	}

	return &routeplanningpb.ListRoutesResponse{
		Routes:     respRoutes,
		TotalItems: int32(total),
		Page:       page,
		Limit:      limit,
		TotalPages: pagination.CalculateTotalPages(total, limit),
	}, nil
}

func (h *RoutePlanningHandler) UpdateRoute(ctx context.Context, req *routeplanningpb.UpdateRouteRequest) (*routeplanningpb.RouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy route: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	// 1. Chỉ cho phép sửa khi route ở trạng thái Draft hoặc Ready
	if route.Status != enums.RouteStatusDraft && route.Status != enums.RouteStatusReady {
		return nil, status.Errorf(codes.FailedPrecondition, "Route đang ở trạng thái %s — chỉ Draft hoặc Ready mới được sửa", route.Status.String())
	}

	// 2. Validation đầu vào
	if strings.TrimSpace(req.GetName()) == "" {
		return nil, status.Error(codes.InvalidArgument, "Tên tuyến đường (Name) không được để trống")
	}
	if len(req.GetStops()) < 2 {
		return nil, status.Error(codes.InvalidArgument, "Tuyến đường phải có tối thiểu 2 điểm dừng")
	}
	if req.GetMaxWeightKg() <= 0 {
		return nil, status.Error(codes.InvalidArgument, "Tải trọng tối đa (MaxWeightKg) phải lớn hơn 0")
	}
	if req.GetMaxVolumeM3() <= 0 {
		return nil, status.Error(codes.InvalidArgument, "Thể tích tối đa (MaxVolumeM3) phải lớn hơn 0")
	}

	route.Name = strings.TrimSpace(req.GetName())
	if desc := req.GetDescription(); desc != "" {
		route.Description = &desc
	}
	route.Type = enums.ParseRouteType(req.GetRouteType())
	route.MaxWeightKg = req.GetMaxWeightKg()
	route.MaxVolumeM3 = req.GetMaxVolumeM3()
	route.EstimatedDistanceKm = req.GetEstimatedDistanceKm()
	route.EstimatedDurationMinutes = req.GetEstimatedDurationMinutes()

	// Dữ liệu thay đổi → risk cần đánh giá lại, optimization cũ hết hiệu lực
	route.RiskLevel = enums.RouteRiskLevelLow
	route.GovernanceDecision = enums.GovernanceDecisionNoApprovalRequired
	route.LastAssessedAt = nil
	route.OptimizedAt = nil

	if uidStr, ok := sharedctx.GetUserID(ctx); ok && uidStr != "" {
		route.UpdatedBy = &uidStr
	}

	stops := make([]entities.RouteStop, 0, len(req.GetStops()))
	for _, s := range req.GetStops() {
		stops = append(stops, entities.RouteStop{
			ID:                      uuid.New(),
			RouteID:                 route.ID,
			Sequence:                s.GetSequence(),
			StopType:                enums.ParseStopType(s.GetStopType()),
			LocationName:            s.GetLocationName(),
			Address:                 s.GetAddress(),
			Latitude:                s.GetLatitude(),
			Longitude:               s.GetLongitude(),
			EstimatedArrivalMinutes: s.GetEstimatedArrivalMinutes(),
			ServiceDurationMinutes:  s.GetServiceDurationMinutes(),
		})
	}
	route.Stops = stops

	updatedByUserID := h.getUserID(ctx)
	expectedVersion := route.Version
	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.routeRepo.UpdateWithVersion(txCtx, route, expectedVersion); err != nil {
			return err
		}

		eventPayload, _ := json.Marshal(dtos.RouteUpdatedEvent{
			RouteID:         route.ID,
			TenantID:        tenantID,
			RouteName:       route.Name,
			Version:         route.Version,
			UpdatedByUserID: updatedByUserID,
		})

		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteUpdatedEvent",
			Payload:   string(eventPayload),
		})
	})

	if err != nil {
		if errors.Is(err, domain.ErrOptimisticLock) {
			return nil, status.Errorf(codes.Aborted, "Xung đột phiên bản: Tuyến đường đã bị thay đổi bởi tác vụ khác")
		}
		return nil, status.Errorf(codes.Internal, "Cập nhật route thất bại: %v", err)
	}

	return mappers.MapRouteToResponse(route), nil
}

func (h *RoutePlanningHandler) DeleteRoute(ctx context.Context, req *routeplanningpb.DeleteRouteRequest) (*routeplanningpb.DeleteRouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy route: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	// Đánh giá rủi ro xóa mềm (Fail-closed khi chính sách chưa được cấu hình, chặn xóa khi Active)
	policy, err := h.policyProvider.GetEffectivePolicy(ctx, tenantID, "RoutePlanning")
	if err != nil {
		return nil, status.Errorf(codes.FailedPrecondition, "Chính sách rủi ro chưa được cấu hình hoặc không khả dụng: %v", err)
	}
	softRisk := h.governanceService.AssessSoftDeleteRisk(route, policy)
	if softRisk.Decision == enums.GovernanceDecisionBlocked {
		return nil, status.Errorf(codes.FailedPrecondition, "%s", softRisk.ReasonDetails)
	}

	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.routeRepo.Delete(txCtx, tenantID, routeID); err != nil {
			return err
		}

		deletedByUserID := h.getUserID(ctx)
		eventPayload, _ := json.Marshal(dtos.RouteDeletedEvent{
			RouteID:         routeID,
			TenantID:        tenantID,
			DeletedByUserID: deletedByUserID,
		})

		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteDeletedEvent",
			Payload:   string(eventPayload),
		})
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Xóa route thất bại: %v", err)
	}

	return &routeplanningpb.DeleteRouteResponse{Success: true}, nil
}

func (h *RoutePlanningHandler) UpdateRouteStatus(ctx context.Context, req *routeplanningpb.UpdateRouteStatusRequest) (*routeplanningpb.RouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy route: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	oldStatus := route.Status.String()
	newStatus := enums.ParseRouteStatus(req.GetNewStatus())

	// Nếu kích hoạt route sang Active hoặc Ready -> bắt buộc kiểm tra thẩm quyền rủi ro
	if newStatus == enums.RouteStatusActive || newStatus == enums.RouteStatusReady {
		effectivePolicy, err := h.policyProvider.GetEffectivePolicy(ctx, tenantID, "RoutePlanning")
		if err != nil {
			return nil, status.Errorf(codes.FailedPrecondition, "Lỗi lấy chính sách rủi ro: %v", err)
		}

		if err := h.governanceService.ValidateExecutionAuthorized(ctx, route, effectivePolicy); err != nil {
			return nil, status.Errorf(codes.PermissionDenied, "Không đủ thẩm quyền kích hoạt tuyến đường: %v", err)
		}
	}

	route.Status = newStatus
	changedByUserID := h.getUserID(ctx)
	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.routeRepo.Update(txCtx, route); err != nil {
			return err
		}

		eventPayload, _ := json.Marshal(dtos.RouteStatusChangedEvent{
			RouteID:         route.ID,
			TenantID:        tenantID,
			OldStatus:       oldStatus,
			NewStatus:       newStatus.String(),
			ChangedByUserID: changedByUserID,
		})

		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteStatusChangedEvent",
			Payload:   string(eventPayload),
		})
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Cập nhật trạng thái thất bại: %v", err)
	}

	return mappers.MapRouteToResponse(route), nil
}

// ===== Optimization & Recommendation =====

func (h *RoutePlanningHandler) OptimizeRoute(ctx context.Context, req *routeplanningpb.OptimizeRouteRequest) (*routeplanningpb.RouteResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy route: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	if len(route.Stops) < 2 {
		return nil, status.Error(codes.InvalidArgument, "Tuyến đường phải có tối thiểu 2 điểm dừng để tối ưu")
	}

	// Gọi VROOM solver giải bài toán VRP
	optResult, err := h.vroomService.Optimize(ctx, route)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Tối ưu hóa VROOM thất bại: %v", err)
	}

	// Map lại các stops theo thứ tự tối ưu
	stopMap := make(map[uuid.UUID]entities.RouteStop, len(route.Stops))
	for _, s := range route.Stops {
		stopMap[s.ID] = s
	}

	newStops := make([]entities.RouteStop, 0, len(optResult.Stops))
	for _, optStop := range optResult.Stops {
		if s, ok := stopMap[optStop.StopID]; ok {
			s.Sequence = optStop.Sequence
			s.EstimatedArrivalMinutes = optStop.EstimatedArrivalMinutes
			newStops = append(newStops, s)
		}
	}

	now := time.Now().UTC()
	route.Stops = newStops
	route.EstimatedDistanceKm = optResult.TotalDistanceKm
	route.EstimatedDurationMinutes = optResult.TotalDurationMinutes
	route.OptimizedAt = &now
	route.Status = enums.RouteStatusReady

	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.routeRepo.Update(txCtx, route); err != nil {
			return err
		}

		eventPayload, _ := json.Marshal(dtos.RouteOptimizedEvent{
			RouteID:              route.ID,
			TenantID:             tenantID,
			Provider:             "VROOM",
			Model:                "vroom-engine",
			TotalDistanceKm:      route.EstimatedDistanceKm,
			TotalDurationMinutes: route.EstimatedDurationMinutes,
		})

		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteOptimizedEvent",
			Payload:   string(eventPayload),
		})
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lưu kết quả tối ưu hóa thất bại: %v", err)
	}

	return mappers.MapRouteToResponse(route), nil
}

func (h *RoutePlanningHandler) GetRouteRecommendation(ctx context.Context, req *routeplanningpb.GetRouteRecommendationRequest) (*routeplanningpb.RouteRecommendationResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	routeID, err := h.parseUUID(req.GetRouteId(), "Route ID")
	if err != nil {
		return nil, err
	}

	route, err := h.routeRepo.GetByID(ctx, tenantID, routeID)
	if err != nil {
		if err == domain.ErrRouteNotFound {
			return nil, status.Errorf(codes.NotFound, "Không tìm thấy route: %s", routeID)
		}
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn route: %v", err)
	}

	// 1. Lấy chính sách hiệu lực (Fail-closed nếu chưa cấu hình)
	effectivePolicy, err := h.policyProvider.GetEffectivePolicy(ctx, tenantID, "RoutePlanning")
	if err != nil {
		return nil, status.Errorf(codes.FailedPrecondition, "Chính sách rủi ro chưa được cấu hình: %v", err)
	}

	// 2. Chạy Rule Engine tất định
	ruleCtx := &domainrules.RouteRuleContext{
		Route:             route,
		TenantID:          tenantID,
		RuleConfigService: h.ruleConfigService,
	}
	ruleResults, err := h.ruleEngine.EvaluateAll(ctx, ruleCtx)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi chạy Rule Engine: %v", err)
	}

	// 3. Kiểm tra Compliance RAG (soft-fail nếu chưa sẵn sàng)
	var riskReasons []string
	for _, r := range ruleResults {
		if !r.Passed && r.Message != "" {
			riskReasons = append(riskReasons, r.Message)
		}
	}
	routeJSON, _ := json.Marshal(route)
	complianceResult := h.complianceService.CheckCompliance(ctx, tenantID, string(routeJSON), riskReasons)

	// 4. Lấy khuyến nghị từ AI Governance
	routeDto := &dtos.RouteDto{
		ID:                       route.ID,
		TenantID:                 route.TenantID,
		Name:                     route.Name,
		Type:                     route.Type.String(),
		Status:                   route.Status.String(),
		RiskLevel:                route.RiskLevel.String(),
		GovernanceDecision:       route.GovernanceDecision.String(),
		EstimatedDistanceKm:      route.EstimatedDistanceKm,
		EstimatedDurationMinutes: route.EstimatedDurationMinutes,
		MaxWeightKg:              route.MaxWeightKg,
		MaxVolumeM3:              route.MaxVolumeM3,
		IsAiGenerated:            route.IsAiGenerated,
		Version:                  route.Version,
		CreatedAt:                route.CreatedAt,
	}
	aiResult := h.aiService.GetRecommendation(ctx, routeDto, ruleResults, complianceResult)

	// 5. Tổng hợp rủi ro qua Governance Service
	assessmentResult, err := h.governanceService.AssessRoute(ctx, route, effectivePolicy, ruleResults, complianceResult, aiResult)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi thẩm định rủi ro: %v", err)
	}

	// Cập nhật trạng thái rủi ro cho tuyến đường
	route.RiskLevel = assessmentResult.RiskLevel
	route.GovernanceDecision = assessmentResult.Decision
	now := time.Now().UTC()
	route.LastAssessedAt = &now

	var approvalIDStr string
	var newApproval *entities.ApprovalRequest
	finalDecision := "ExecutedByRules"
	if aiResult != nil && aiResult.Success {
		finalDecision = "ExecutedByAi"
	}

	userID := h.getUserID(ctx)

	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if assessmentResult.Decision == enums.GovernanceDecisionManagerApprovalRequired {
			finalDecision = "PendingApproval"

			// Kiểm tra hoặc tạo mới Approval Ticket
			existingTicket, _ := h.approvalRepo.GetPendingByRouteID(txCtx, tenantID, route.ID)
			if existingTicket != nil {
				approvalIDStr = existingTicket.ID.String()
			} else {
				reason := assessmentResult.ReasonDetails
				aiSummary := aiResult.Recommendation.Summary
				compSummary := complianceResult.MergedContext

				var err error
				newApproval, err = h.approvalService.Create(
					txCtx, route.ID, reason, aiSummary, &compSummary,
					route.Version, effectivePolicy.PolicyID, effectivePolicy.Version, tenantID,
				)
				if err != nil {
					return fmt.Errorf("tạo approval ticket thất bại: %w", err)
				}
				approvalIDStr = newApproval.ID.String()

				// Outbox: Phát sự kiện RouteApprovalRequestedEvent
				approvalEvt := dtos.RouteApprovalRequestedEvent{
					ApprovalRequestID: newApproval.ID,
					RouteID:           route.ID,
					TenantID:          tenantID,
					Reason:            reason,
					AiSummary:         aiSummary,
				}
				approvalPayload, err := json.Marshal(approvalEvt)
				if err != nil {
					return fmt.Errorf("marshal RouteApprovalRequestedEvent thất bại: %w", err)
				}
				if err := h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
					EventType: "RouteApprovalRequestedEvent",
					Payload:   string(approvalPayload),
					CreatedAt: now,
				}); err != nil {
					return fmt.Errorf("lưu RouteApprovalRequestedEvent vào outbox thất bại: %w", err)
				}
			}
		} else if assessmentResult.Decision == enums.GovernanceDecisionBlocked {
			finalDecision = "Blocked"
		} else {
			if route.Status == enums.RouteStatusDraft || route.Status == enums.RouteStatusOptimizing {
				route.Status = enums.RouteStatusReady
			}
		}

		// Lưu bản ghi RiskAssessment vào DB
		matchedCodes := assessmentResult.MatchedRuleCodes
		reasonCodes := assessmentResult.ReasonCodes
		riskAss := &entities.RiskAssessment{
			RouteID:            route.ID,
			RouteVersion:       route.Version,
			PolicyID:           effectivePolicy.PolicyID,
			PolicyVersion:      effectivePolicy.Version,
			RiskLevel:          assessmentResult.RiskLevel,
			GovernanceDecision: assessmentResult.Decision,
			ConfidenceScore:    assessmentResult.ConfidenceScore,
			MatchedRuleCodes:   fmt.Sprintf("%v", matchedCodes),
			Source:             assessmentResult.Source,
			ReasonCodes:        fmt.Sprintf("%v", reasonCodes),
			ReasonDetails:      assessmentResult.ReasonDetails,
			PolicyApplied:      effectivePolicy.PolicyID,
			PolicySource:       effectivePolicy.Source,
			AssessedByUserID:   userID,
			AssessedAt:         now,
		}
		riskAss.ID = uuid.New()
		riskAss.TenantID = tenantID
		riskAss.CreatedAt = now
		if err := h.policyRepo.CreateRiskAssessment(txCtx, riskAss); err != nil {
			return fmt.Errorf("lưu RiskAssessment thất bại: %w", err)
		}

		// Lưu DecisionAuditLog bất biến phục vụ kiểm toán
		ruleResultsJSON, _ := json.Marshal(ruleResults)
		var compSummaryStr *string
		var compDocRefs *string
		if complianceResult != nil {
			compSummaryStr = &complianceResult.MergedContext
			if len(complianceResult.ApplicableRegulations) > 0 {
				refs := strings.Join(complianceResult.ApplicableRegulations, ", ")
				compDocRefs = &refs
			}
		}
		var llmProv, llmMod, llmSum *string
		if aiResult != nil && aiResult.Success {
			p := aiResult.Provider
			m := aiResult.Model
			s := aiResult.Recommendation.Summary
			llmProv = &p
			llmMod = &m
			llmSum = &s
		}
		var approvalUUID *uuid.UUID
		if approvalIDStr != "" {
			if aID, err := uuid.Parse(approvalIDStr); err == nil {
				approvalUUID = &aID
			}
		}

		auditLog := &entities.RouteDecisionAuditLog{
			RouteID:                  route.ID,
			RequestedByUserID:        userID,
			RiskLevel:                assessmentResult.RiskLevel.String(),
			AutomationDecision:       finalDecision,
			RuleResultsJSON:          string(ruleResultsJSON),
			PolicyApplied:            effectivePolicy.PolicyID,
			LlmProvider:              llmProv,
			LlmModel:                 llmMod,
			LlmSummary:               llmSum,
			ComplianceCheckPerformed: complianceResult != nil,
			ComplianceSummary:        compSummaryStr,
			ComplianceDocumentRefs:   compDocRefs,
			ApprovalRequestID:        approvalUUID,
		}
		auditLog.TenantID = tenantID
		if err := h.approvalRepo.CreateDecisionAuditLog(txCtx, auditLog); err != nil {
			return fmt.Errorf("lưu DecisionAuditLog thất bại: %w", err)
		}

		// Outbox: Phát sự kiện RouteRiskEvaluatedEvent
		riskEvaluatedEvt := dtos.RouteRiskEvaluatedEvent{
			RouteID:            route.ID,
			RouteVersion:       route.Version,
			TenantID:           tenantID,
			RiskLevel:          assessmentResult.RiskLevel.String(),
			GovernanceDecision: assessmentResult.Decision.String(),
			PolicyID:           effectivePolicy.PolicyID,
			PolicyVersion:      effectivePolicy.Version,
			PolicySource:       effectivePolicy.Source.String(),
			MatchedRuleCodes:   assessmentResult.MatchedRuleCodes,
			Source:             assessmentResult.Source,
			EvaluatedByUserID:  userID,
		}
		riskEvtPayload, err := json.Marshal(riskEvaluatedEvt)
		if err != nil {
			return fmt.Errorf("marshal RouteRiskEvaluatedEvent thất bại: %w", err)
		}
		if err := h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "RouteRiskEvaluatedEvent",
			Payload:   string(riskEvtPayload),
			CreatedAt: now,
		}); err != nil {
			return fmt.Errorf("lưu RouteRiskEvaluatedEvent vào outbox thất bại: %w", err)
		}

		if err := h.routeRepo.Update(txCtx, route); err != nil {
			return fmt.Errorf("cập nhật route thất bại: %w", err)
		}

		return nil
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lưu kết quả thẩm định rủi ro thất bại: %v", err)
	}

	var confScore float64 = 0.5
	if assessmentResult.ConfidenceScore != nil {
		confScore = *assessmentResult.ConfidenceScore
	}

	return &routeplanningpb.RouteRecommendationResponse{
		RouteId:               route.ID.String(),
		RiskLevel:             assessmentResult.RiskLevel.String(),
		AutomationDecision:    finalDecision,
		RecommendationSource:  assessmentResult.Source,
		Summary:               aiResult.Recommendation.Summary,
		Suggestions:           aiResult.Recommendation.Suggestions,
		ConfidenceScore:       confScore,
		ApprovalRequestId:     approvalIDStr,
		ApplicableRegulations: complianceResult.ApplicableRegulations,
	}, nil
}

// ===== Approvals =====

func (h *RoutePlanningHandler) ApproveRoute(ctx context.Context, req *routeplanningpb.ApproveRouteRequest) (*routeplanningpb.ApprovalResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	if !req.GetIsApproved() {
		return nil, status.Error(codes.InvalidArgument, "Dùng RejectRoute để từ chối (bắt buộc kèm reason). ApproveRoute chỉ dùng để phê duyệt.")
	}

	approvalID, err := h.parseUUID(req.GetApprovalId(), "Approval ID")
	if err != nil {
		return nil, err
	}

	reviewerUserID := h.getUserID(ctx)

	var comment *string
	if c := req.GetComment(); c != "" {
		comment = &c
	}

	approval, err := h.approvalService.Approve(ctx, approvalID, reviewerUserID, tenantID, comment)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Phê duyệt thất bại: %v", err)
	}

	// Outbox: Phát sự kiện RouteApprovedEvent
	approvedEvt := dtos.RouteApprovedEvent{
		ApprovalRequestID: approval.ID,
		RouteID:           approval.RouteID,
		TenantID:          tenantID,
		ReviewerUserID:    reviewerUserID,
		Comment:           comment,
	}
	evtPayload, _ := json.Marshal(approvedEvt)
	_ = h.outboxRepo.Save(ctx, &entities.OutboxMessage{
		EventType: "RouteApprovedEvent",
		Payload:   string(evtPayload),
		CreatedAt: time.Now().UTC(),
	})

	return mappers.MapApprovalToResponse(approval), nil
}

func (h *RoutePlanningHandler) RejectRoute(ctx context.Context, req *routeplanningpb.RejectRouteRequest) (*routeplanningpb.ApprovalResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	approvalID, err := h.parseUUID(req.GetApprovalId(), "Approval ID")
	if err != nil {
		return nil, err
	}

	reason := req.GetReason()
	if reason == "" {
		return nil, status.Error(codes.InvalidArgument, "Reason là bắt buộc khi reject approval request")
	}

	reviewerUserID := h.getUserID(ctx)

	var comment *string
	if c := req.GetComment(); c != "" {
		comment = &c
	}

	approval, err := h.approvalService.Reject(ctx, approvalID, reviewerUserID, tenantID, reason, comment)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Từ chối phê duyệt thất bại: %v", err)
	}

	// Outbox: Phát sự kiện RouteRejectedEvent
	rejectedEvt := dtos.RouteRejectedEvent{
		ApprovalRequestID: approval.ID,
		RouteID:           approval.RouteID,
		TenantID:          tenantID,
		ReviewerUserID:    reviewerUserID,
		Reason:            reason,
		Comment:           comment,
	}
	evtPayload, _ := json.Marshal(rejectedEvt)
	_ = h.outboxRepo.Save(ctx, &entities.OutboxMessage{
		EventType: "RouteRejectedEvent",
		Payload:   string(evtPayload),
		CreatedAt: time.Now().UTC(),
	})

	return mappers.MapApprovalToResponse(approval), nil
}

func (h *RoutePlanningHandler) ListPendingApprovals(ctx context.Context, req *routeplanningpb.ListPendingApprovalsRequest) (*routeplanningpb.ListApprovalsResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	page, limit := pagination.NormalizePagination(req.GetPage(), req.GetLimit())

	approvals, total, err := h.approvalRepo.ListPending(ctx, tenantID, page, limit)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn approvals: %v", err)
	}

	respList := make([]*routeplanningpb.ApprovalResponse, 0, len(approvals))
	for i := range approvals {
		respList = append(respList, mappers.MapApprovalToResponse(&approvals[i]))
	}

	return &routeplanningpb.ListApprovalsResponse{
		Approvals:  respList,
		TotalItems: int32(total),
		Page:       page,
		Limit:      limit,
		TotalPages: pagination.CalculateTotalPages(total, limit),
	}, nil
}

// ===== Configs =====

func (h *RoutePlanningHandler) GetTenantAiConfig(ctx context.Context, req *routeplanningpb.GetTenantAiConfigRequest) (*routeplanningpb.TenantAiConfigResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	config, err := h.policyRepo.GetConfig(ctx, tenantID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn cấu hình rủi ro: %v", err)
	}

	if config == nil {
		return &routeplanningpb.TenantAiConfigResponse{
			Id:         uuid.New().String(),
			TenantId:   tenantID.String(),
			Feature:    req.GetFeature(),
			Policy:     "RulesAndLlm",
			AiProvider: "Gemini",
			IsActive:   true,
			UpdatedAt:  time.Now().UTC().Format(time.RFC3339),
		}, nil
	}

	policyVal := config.ActivePolicyID
	if strings.TrimSpace(policyVal) == "" {
		policyVal = "RulesAndLlm"
	}

	return &routeplanningpb.TenantAiConfigResponse{
		Id:         config.ID.String(),
		TenantId:   config.TenantID.String(),
		Feature:    req.GetFeature(),
		Policy:     policyVal,
		AiProvider: "Gemini",
		IsActive:   true,
		UpdatedAt:  config.UpdatedAt.Format(time.RFC3339),
	}, nil
}

func (h *RoutePlanningHandler) UpsertTenantAiConfig(ctx context.Context, req *routeplanningpb.UpsertTenantAiConfigRequest) (*routeplanningpb.TenantAiConfigResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	existing, err := h.policyRepo.GetConfig(ctx, tenantID)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn cấu hình rủi ro: %v", err)
	}

	policyMode := enums.RiskPolicyModeUseCustomPolicy
	if strings.EqualFold(req.GetPolicy(), "Manual") {
		policyMode = enums.RiskPolicyModeUsePlatformDefault
	}

	now := time.Now().UTC()
	var config *entities.TenantRiskPolicyConfig
	if existing == nil {
		config = &entities.TenantRiskPolicyConfig{
			ID:                  uuid.New(),
			TenantID:            tenantID,
			PolicyMode:          policyMode,
			ActivePolicyID:      req.GetPolicy(),
			ActivePolicyVersion: 1,
			UpdatedAt:           now,
		}
	} else {
		existing.PolicyMode = policyMode
		existing.ActivePolicyID = req.GetPolicy()
		existing.UpdatedAt = now
		config = existing
	}

	if err := h.policyRepo.UpsertConfig(ctx, config); err != nil {
		return nil, status.Errorf(codes.Internal, "Lưu cấu hình AI thất bại: %v", err)
	}

	aiProvider := req.GetAiProvider()
	if strings.TrimSpace(aiProvider) == "" {
		aiProvider = "Gemini"
	}

	return &routeplanningpb.TenantAiConfigResponse{
		Id:         config.ID.String(),
		TenantId:   config.TenantID.String(),
		Feature:    req.GetFeature(),
		Policy:     req.GetPolicy(),
		AiProvider: aiProvider,
		IsActive:   req.GetIsActive(),
		UpdatedAt:  config.UpdatedAt.Format(time.RFC3339),
	}, nil
}

func (h *RoutePlanningHandler) UpsertTenantRuleConfig(ctx context.Context, req *routeplanningpb.UpsertTenantRuleConfigRequest) (*routeplanningpb.TenantRuleConfigResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	thresholdsJSON, _ := json.Marshal(req.GetThresholds())
	config := &entities.TenantRuleConfig{
		ID:             uuid.New(),
		TenantID:       tenantID,
		RuleName:       req.GetRuleName(),
		IsEnabled:      req.GetIsEnabled(),
		ThresholdsJSON: string(thresholdsJSON),
		UpdatedAt:      time.Now().UTC(),
	}

	if err := h.ruleConfigRepo.Upsert(ctx, config); err != nil {
		return nil, status.Errorf(codes.Internal, "Lưu cấu hình rule thất bại: %v", err)
	}

	// Invalidate cache
	if inv, ok := h.ruleConfigService.(services.CacheInvalidator); ok {
		inv.InvalidateCache(tenantID.String(), req.GetRuleName())
	}

	return mappers.MapTenantRuleConfigToResponse(config, req.GetThresholds()), nil
}

func (h *RoutePlanningHandler) ListTenantRuleConfigs(ctx context.Context, req *routeplanningpb.ListTenantRuleConfigsRequest) (*routeplanningpb.ListTenantRuleConfigsResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	page, limit := pagination.NormalizePagination(req.GetPage(), req.GetLimit())
	configs, total, err := h.ruleConfigRepo.List(ctx, tenantID, page, limit)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn danh sách rule configs: %v", err)
	}

	respList := make([]*routeplanningpb.TenantRuleConfigResponse, 0, len(configs))
	for i := range configs {
		var thresholds map[string]float64
		if configs[i].ThresholdsJSON != "" {
			_ = json.Unmarshal([]byte(configs[i].ThresholdsJSON), &thresholds)
		}
		respList = append(respList, mappers.MapTenantRuleConfigToResponse(&configs[i], thresholds))
	}

	return &routeplanningpb.ListTenantRuleConfigsResponse{
		Configs:    respList,
		TotalItems: int32(total),
		Page:       page,
		Limit:      limit,
	}, nil
}

// ===== Risk Policy Lifecycle =====

func (h *RoutePlanningHandler) CreateRiskPolicyDraft(ctx context.Context, req *routeplanningpb.CreateRiskPolicyDraftRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	if strings.TrimSpace(req.GetName()) == "" {
		return nil, status.Error(codes.InvalidArgument, "Tên chính sách (Name) không được để trống.")
	}

	scope := strings.TrimSpace(req.GetScope())
	if scope == "" {
		scope = "RoutePlanning"
	}

	now := time.Now().UTC()
	policy := &entities.TenantRiskPolicy{
		Name:    req.GetName(),
		Scope:   scope,
		Status:  enums.TenantRiskPolicyStatusDraft,
		Source:  enums.ParseRiskPolicySource(req.GetSource()),
	}
	policy.ID = uuid.New()
	policy.TenantID = tenantID
	policy.CreatedAt = now

	if d := req.GetDescription(); d != "" {
		policy.Description = &d
	}
	if sDoc := req.GetSourceDocumentId(); sDoc != "" {
		policy.SourceDocumentID = &sDoc
	}

	rules := make([]entities.TenantRiskRule, 0, len(req.GetRules()))
	for _, r := range req.GetRules() {
		rule := entities.TenantRiskRule{
			PolicyID:       policy.ID,
			RuleCode:       r.GetRuleCode(),
			RuleName:       r.GetRuleName(),
			ThresholdsJSON: r.GetThresholdsJson(),
			RiskEffect:     enums.ParseRouteRiskLevel(r.GetRiskEffect()),
			IsEnabled:      r.GetIsEnabled(),
		}
		rule.ID = uuid.New()
		rule.TenantID = tenantID
		rule.CreatedAt = policy.CreatedAt
		if sRef := r.GetSourceReference(); sRef != "" {
			rule.SourceReference = &sRef
		}
		rules = append(rules, rule)
	}
	policy.Rules = rules

	userID := h.getUserID(ctx)

	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		if err := h.policyRepo.CreatePolicyDraft(txCtx, policy); err != nil {
			return err
		}

		createdEvt := dtos.TenantRiskPolicyCreatedEvent{
			PolicyID:        policy.ID,
			TenantID:        tenantID,
			Scope:           policy.Scope,
			Version:         policy.Version,
			Source:          policy.Source.String(),
			CreatedByUserID: userID,
		}
		evtPayload, _ := json.Marshal(createdEvt)
		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "TenantRiskPolicyCreatedEvent",
			Payload:   string(evtPayload),
			CreatedAt: now,
		})
	})
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Tạo bản nháp policy thất bại: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) UpdateRiskPolicyDraft(ctx context.Context, req *routeplanningpb.UpdateRiskPolicyDraftRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	policy, err := h.policyRepo.GetPolicyByID(ctx, tenantID, policyID)
	if err != nil {
		return nil, status.Errorf(codes.NotFound, "Không tìm thấy policy: %v", err)
	}

	if policy.Status != enums.TenantRiskPolicyStatusDraft {
		return nil, status.Error(codes.FailedPrecondition, "Chỉ có thể chỉnh sửa chính sách ở trạng thái Draft")
	}

	policy.Name = req.GetName()
	if d := req.GetDescription(); d != "" {
		policy.Description = &d
	}

	rules := make([]entities.TenantRiskRule, 0, len(req.GetRules()))
	now := time.Now().UTC()
	for _, r := range req.GetRules() {
		rule := entities.TenantRiskRule{
			PolicyID:       policy.ID,
			RuleCode:       r.GetRuleCode(),
			RuleName:       r.GetRuleName(),
			ThresholdsJSON: r.GetThresholdsJson(),
			RiskEffect:     enums.ParseRouteRiskLevel(r.GetRiskEffect()),
			IsEnabled:      r.GetIsEnabled(),
		}
		rule.ID = uuid.New()
		rule.TenantID = tenantID
		rule.CreatedAt = now
		if sRef := r.GetSourceReference(); sRef != "" {
			rule.SourceReference = &sRef
		}
		rules = append(rules, rule)
	}
	policy.Rules = rules

	if err := h.policyRepo.UpdatePolicyDraft(ctx, policy); err != nil {
		return nil, status.Errorf(codes.Internal, "Cập nhật policy thất bại: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) SubmitRiskPolicyForReview(ctx context.Context, req *routeplanningpb.SubmitRiskPolicyRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	policy, err := h.policyRepo.GetPolicyByID(ctx, tenantID, policyID)
	if err != nil {
		return nil, status.Errorf(codes.NotFound, "Không tìm thấy policy: %v", err)
	}

	now := time.Now().UTC()
	policy.Status = enums.TenantRiskPolicyStatusPendingReview
	policy.SubmittedAt = &now

	if err := h.policyRepo.UpdatePolicyDraft(ctx, policy); err != nil {
		return nil, status.Errorf(codes.Internal, "Gửi duyệt policy thất bại: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) RejectRiskPolicy(ctx context.Context, req *routeplanningpb.RejectRiskPolicyRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	policy, err := h.policyRepo.GetPolicyByID(ctx, tenantID, policyID)
	if err != nil {
		return nil, status.Errorf(codes.NotFound, "Không tìm thấy policy: %v", err)
	}

	now := time.Now().UTC()
	policy.Status = enums.TenantRiskPolicyStatusRejected
	policy.ReviewedAt = &now
	reason := req.GetReason()
	policy.RejectionReason = &reason
	if c := req.GetComment(); c != "" {
		policy.ReviewerComment = &c
	}

	if err := h.policyRepo.UpdatePolicyDraft(ctx, policy); err != nil {
		return nil, status.Errorf(codes.Internal, "Từ chối policy thất bại: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) PublishRiskPolicy(ctx context.Context, req *routeplanningpb.PublishRiskPolicyRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	policy, err := h.policyRepo.GetPolicyByID(ctx, tenantID, policyID)
	if err != nil {
		return nil, status.Errorf(codes.NotFound, "Không tìm thấy policy: %v", err)
	}

	if policy.Status != enums.TenantRiskPolicyStatusPendingReview && policy.Status != enums.TenantRiskPolicyStatusDraft {
		return nil, status.Errorf(codes.FailedPrecondition, "Chính sách phải ở trạng thái 'PendingReview' hoặc 'Draft' trước khi phát hành.")
	}

	userID := h.getUserID(ctx)
	now := time.Now().UTC()

	err = h.uow.DoInTransaction(ctx, func(txCtx context.Context) error {
		var supersededPolicyID *uuid.UUID
		var supersededVersion *int32

		// 1. Tìm các policy đang Active thuộc scope để chuyển sang Superseded
		activePolicies, err := h.policyRepo.GetActivePoliciesByScope(txCtx, tenantID, policy.Scope)
		if err != nil {
			return err
		}

		for i := range activePolicies {
			oldActive := &activePolicies[i]
			oldActive.Status = enums.TenantRiskPolicyStatusSuperseded
			oldActive.SupersededAt = &now
			oldActive.UpdatedAt = &now
			if err := h.policyRepo.UpdatePolicyDraft(txCtx, oldActive); err != nil {
				return err
			}
			supersededPolicyID = &oldActive.ID
			v := oldActive.Version
			supersededVersion = &v

			supersededEvt := dtos.TenantRiskPolicySupersededEvent{
				PolicyID:             oldActive.ID,
				TenantID:             tenantID,
				Scope:                oldActive.Scope,
				Version:              oldActive.Version,
				SupersededByPolicyID: policy.ID,
				SupersededByVersion:  policy.Version,
			}
			payload, _ := json.Marshal(supersededEvt)
			if err := h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
				EventType: "TenantRiskPolicySupersededEvent",
				Payload:   string(payload),
				CreatedAt: now,
			}); err != nil {
				return err
			}
		}

		// 2. Chuyển policy hiện tại sang Active
		policy.Status = enums.TenantRiskPolicyStatusActive
		policy.PublishedByUserID = &userID
		policy.PublishedAt = &now
		policy.UpdatedAt = &now
		if err := h.policyRepo.UpdatePolicyDraft(txCtx, policy); err != nil {
			return err
		}

		// 3. Cập nhật cấu hình ActivePolicy cho tenant
		cfg := &entities.TenantRiskPolicyConfig{
			TenantID:            tenantID,
			PolicyMode:          enums.RiskPolicyModeUseCustomPolicy,
			ActivePolicyID:      policy.ID.String(),
			ActivePolicyVersion: policy.Version,
			UpdatedAt:           now,
		}
		if err := h.policyRepo.UpsertConfig(txCtx, cfg); err != nil {
			return err
		}

		// 4. Đồng bộ active rules sang TenantRuleConfigs cho legacy/cache
		for _, rule := range policy.Rules {
			ruleCfg := &entities.TenantRuleConfig{
				ID:             uuid.New(),
				TenantID:       tenantID,
				RuleName:       rule.RuleName,
				IsEnabled:      rule.IsEnabled,
				ThresholdsJSON: rule.ThresholdsJSON,
				UpdatedAt:      now,
			}
			_ = h.ruleConfigRepo.Upsert(txCtx, ruleCfg)
		}

		// 5. Outbox: Phát sự kiện TenantRiskPolicyPublishedEvent
		publishedEvt := dtos.TenantRiskPolicyPublishedEvent{
			PolicyID:           policy.ID,
			TenantID:           tenantID,
			Scope:              policy.Scope,
			Version:            policy.Version,
			PublishedByUserID:  userID,
			SupersededPolicyID: supersededPolicyID,
			SupersededVersion:  supersededVersion,
		}
		publishedPayload, _ := json.Marshal(publishedEvt)
		return h.outboxRepo.Save(txCtx, &entities.OutboxMessage{
			EventType: "TenantRiskPolicyPublishedEvent",
			Payload:   string(publishedPayload),
			CreatedAt: now,
		})
	})

	if err != nil {
		return nil, status.Errorf(codes.Internal, "Phát hành policy thất bại: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) GetRiskPolicy(ctx context.Context, req *routeplanningpb.GetRiskPolicyRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	policy, err := h.policyRepo.GetPolicyByID(ctx, tenantID, policyID)
	if err != nil {
		return nil, status.Errorf(codes.NotFound, "Không tìm thấy policy: %v", err)
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) GetActiveRiskPolicy(ctx context.Context, req *routeplanningpb.GetActiveRiskPolicyRequest) (*routeplanningpb.RiskPolicyResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	scope := req.GetScope()
	if scope == "" {
		scope = "RoutePlanning"
	}

	policy, err := h.policyRepo.GetActivePolicy(ctx, tenantID, scope)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn active policy: %v", err)
	}
	if policy == nil {
		return nil, status.Error(codes.NotFound, "Không tìm thấy active policy")
	}

	return mappers.MapRiskPolicyToResponse(policy), nil
}

func (h *RoutePlanningHandler) ListRiskPolicyVersions(ctx context.Context, req *routeplanningpb.ListRiskPolicyVersionsRequest) (*routeplanningpb.ListRiskPolicyVersionsResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	page, limit := pagination.NormalizePagination(req.GetPage(), req.GetLimit())
	scope := req.GetScope()
	if scope == "" {
		scope = "RoutePlanning"
	}

	policies, total, err := h.policyRepo.ListPolicyVersions(ctx, tenantID, scope, page, limit)
	if err != nil {
		return nil, status.Errorf(codes.Internal, "Lỗi truy vấn versions: %v", err)
	}

	respList := make([]*routeplanningpb.RiskPolicyResponse, 0, len(policies))
	for i := range policies {
		respList = append(respList, mappers.MapRiskPolicyToResponse(&policies[i]))
	}

	return &routeplanningpb.ListRiskPolicyVersionsResponse{
		Policies:   respList,
		TotalItems: int32(total),
		Page:       page,
		Limit:      limit,
	}, nil
}

func (h *RoutePlanningHandler) DeleteRiskPolicyDraft(ctx context.Context, req *routeplanningpb.DeleteRiskPolicyDraftRequest) (*routeplanningpb.DeleteRiskPolicyDraftResponse, error) {
	tenantID, err := h.requireTenantID(ctx)
	if err != nil {
		return nil, err
	}

	policyID, err := h.parseUUID(req.GetPolicyId(), "Policy ID")
	if err != nil {
		return nil, err
	}

	if err := h.policyRepo.DeletePolicyDraft(ctx, tenantID, policyID); err != nil {
		return nil, status.Errorf(codes.NotFound, "Xóa policy draft thất bại: %v", err)
	}

	return &routeplanningpb.DeleteRiskPolicyDraftResponse{Success: true}, nil
}
