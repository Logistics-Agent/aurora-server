package services

import (
	"context"
	"fmt"
	"strings"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/route-planning-agent/internal/domain/repository"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
)

type routeGovernanceService struct {
	riskRepo     repository.RiskPolicyRepository
	approvalRepo repository.ApprovalRepository
}

func NewRouteGovernanceService(
	riskRepo repository.RiskPolicyRepository,
	approvalRepo repository.ApprovalRepository,
) RouteGovernanceService {
	return &routeGovernanceService{
		riskRepo:     riskRepo,
		approvalRepo: approvalRepo,
	}
}

func (s *routeGovernanceService) AssessRoute(
	ctx context.Context,
	route *entities.Route,
	effectivePolicy *EffectiveRiskPolicy,
	ruleResults []domainrules.RuleResult,
	complianceResult *dtos.ComplianceCheckResultDto,
	aiResult *dtos.RouteAiResult,
) (*RiskAssessmentResult, error) {
	matchedRuleCodes := make([]string, 0)
	reasonCodes := make([]string, 0)
	reasonDetailsList := make([]string, 0)

	// 1. Tính toán rủi ro từ Rule Engine (Deterministic Rules)
	deterministicRisk := enums.RouteRiskLevelLow
	ruleRequiresApproval := false

	for _, rule := range ruleResults {
		if !rule.Passed {
			code := rule.RuleCode
			if code == "" {
				code = rule.RuleName
			}
			matchedRuleCodes = append(matchedRuleCodes, code)
			reasonCodes = append(reasonCodes, rule.RuleName)
			if rule.Message != "" {
				reasonDetailsList = append(reasonDetailsList, fmt.Sprintf("%s: %s", code, rule.Message))
			}
		}

		if rule.RiskLevel > deterministicRisk {
			deterministicRisk = rule.RiskLevel
		}

		if rule.RequiresApproval {
			ruleRequiresApproval = true
		}
	}

	// 2. Tính toán tín hiệu từ Compliance RAG
	complianceRequiresApproval := false
	if complianceResult != nil {
		if complianceResult.HasViolations {
			matchedRuleCodes = append(matchedRuleCodes, "REGULATORY_COMPLIANCE_VIOLATION")
			reasonCodes = append(reasonCodes, "ComplianceViolations")
			reasonDetailsList = append(reasonDetailsList, complianceResult.ViolationSummaries...)

			if deterministicRisk < enums.RouteRiskLevelHigh {
				deterministicRisk = enums.RouteRiskLevelHigh
			}
		}

		if complianceResult.RequiresHumanApproval {
			complianceRequiresApproval = true
			if deterministicRisk < enums.RouteRiskLevelHigh {
				deterministicRisk = enums.RouteRiskLevelHigh
			}
		}
	}

	// 3. Tính toán tín hiệu từ AI (LLM) — AI chỉ là MỘT tín hiệu, không ghi đè quy tắc tất định
	aiRisk := enums.RouteRiskLevelLow
	var confidenceScore *float64
	if aiResult != nil {
		confidenceScore = aiResult.Recommendation.ConfidenceScore
		aiRisk = enums.ParseRouteRiskLevel(aiResult.Recommendation.RiskLevel)
	}

	// 4. QUY TẮC BẢO VỆ: AI không bao giờ được phép hạ rủi ro xuống dưới mức quy tắc tất định đã xác định
	effectiveRisk := deterministicRisk
	if aiRisk > effectiveRisk {
		effectiveRisk = aiRisk
	}

	// 5. Xác định quyết định quản trị (Governance Decision)
	var decision enums.GovernanceDecision
	if ruleRequiresApproval || complianceRequiresApproval {
		decision = enums.GovernanceDecisionManagerApprovalRequired
	} else {
		switch effectiveRisk {
		case enums.RouteRiskLevelLow:
			decision = enums.GovernanceDecisionNoApprovalRequired
		case enums.RouteRiskLevelMedium:
			decision = enums.GovernanceDecisionStaffAllowed
		case enums.RouteRiskLevelHigh:
			decision = enums.GovernanceDecisionManagerApprovalRequired
		case enums.RouteRiskLevelCritical:
			decision = enums.GovernanceDecisionBlocked
		default:
			decision = enums.GovernanceDecisionStaffAllowed
		}
	}

	source := "DeterministicRules"
	if aiResult != nil {
		source = "Composite"
	} else if complianceResult != nil {
		source = "RulesAndCompliance"
	}

	details := fmt.Sprintf("Đánh giá rủi ro %s theo chính sách %s v%d — Quyết định: %s",
		effectiveRisk.String(), effectivePolicy.PolicyID, effectivePolicy.Version, decision.String())
	if len(reasonDetailsList) > 0 {
		details = strings.Join(reasonDetailsList, "; ")
	}

	return &RiskAssessmentResult{
		RiskLevel:        effectiveRisk,
		Decision:         decision,
		Source:           source,
		PolicyID:         effectivePolicy.PolicyID,
		PolicyVersion:    effectivePolicy.Version,
		PolicySource:     effectivePolicy.Source,
		MatchedRuleCodes: matchedRuleCodes,
		ReasonCodes:      reasonCodes,
		ReasonDetails:    details,
		ConfidenceScore:  confidenceScore,
		RuleResults:      ruleResults,
	}, nil
}

func (s *routeGovernanceService) AssessSoftDeleteRisk(
	route *entities.Route,
	effectivePolicy *EffectiveRiskPolicy,
) *RiskAssessmentResult {
	matchedRuleCodes := []string{"ROUTE_SOFT_DELETE_OPERATION"}
	reasonCodes := []string{"RouteSoftDelete"}

	var riskLevel enums.RouteRiskLevel
	var decision enums.GovernanceDecision
	var details string

	switch route.Status {
	case enums.RouteStatusActive:
		riskLevel = enums.RouteRiskLevelCritical
		decision = enums.GovernanceDecisionBlocked
		details = "Không thể xóa tuyến đường đang ở trạng thái 'Active' (đang chạy thực tế). Hãy chuyển trạng thái hoặc hủy trước."
	case enums.RouteStatusReady:
		riskLevel = enums.RouteRiskLevelMedium
		decision = enums.GovernanceDecisionStaffAllowed
		details = "Xóa tuyến đường đã ở trạng thái 'Ready' (sẵn sàng vận hành)."
	case enums.RouteStatusCompleted, enums.RouteStatusCancelled, enums.RouteStatusArchived:
		riskLevel = enums.RouteRiskLevelLow
		decision = enums.GovernanceDecisionStaffAllowed
		details = fmt.Sprintf("Xóa tuyến đường đã kết thúc hoặc lưu trữ (trạng thái '%s').", route.Status.String())
	default:
		riskLevel = enums.RouteRiskLevelLow
		decision = enums.GovernanceDecisionStaffAllowed
		details = "Xóa tuyến đường bản nháp (Draft/Optimizing)."
	}

	return &RiskAssessmentResult{
		RiskLevel:        riskLevel,
		Decision:         decision,
		Source:           "SoftDeleteGovernance",
		PolicyID:         effectivePolicy.PolicyID,
		PolicyVersion:    effectivePolicy.Version,
		PolicySource:     effectivePolicy.Source,
		MatchedRuleCodes: matchedRuleCodes,
		ReasonCodes:      reasonCodes,
		ReasonDetails:    details,
	}
}

func (s *routeGovernanceService) ValidateExecutionAuthorized(
	ctx context.Context,
	route *entities.Route,
	effectivePolicy *EffectiveRiskPolicy,
) error {
	latestAssessment, err := s.riskRepo.GetLatestAssessment(ctx, route.TenantID, route.ID)
	if err != nil {
		return err
	}
	if latestAssessment == nil {
		return domain.NewDomainError("FORBIDDEN",
			fmt.Sprintf("Tuyến đường '%s' chưa được đánh giá rủi ro vận hành. Bắt buộc chạy GetRouteRecommendation trước khi thực thi.", route.ID))
	}

	if latestAssessment.RouteVersion != route.Version {
		return domain.NewDomainError("FORBIDDEN",
			fmt.Sprintf("Bản đánh giá rủi ro của tuyến đường '%s' (RouteVersion=%d) đã lỗi thời so với phiên bản dữ liệu hiện tại (RouteVersion=%d).",
				route.ID, latestAssessment.RouteVersion, route.Version))
	}

	if latestAssessment.PolicyID != effectivePolicy.PolicyID || latestAssessment.PolicyVersion != effectivePolicy.Version {
		return domain.NewDomainError("FORBIDDEN",
			fmt.Sprintf("Bản đánh giá rủi ro của tuyến đường '%s' đã lỗi thời do chính sách rủi ro đã thay đổi.", route.ID))
	}

	if route.GovernanceDecision == enums.GovernanceDecisionBlocked || latestAssessment.GovernanceDecision == enums.GovernanceDecisionBlocked {
		return domain.NewDomainError("FORBIDDEN",
			fmt.Sprintf("Tuyến đường '%s' bị CHẶN thực thi do rủi ro mức CRITICAL: %s", route.ID, latestAssessment.ReasonDetails))
	}

	if route.GovernanceDecision == enums.GovernanceDecisionManagerApprovalRequired {
		validApproval, err := s.approvalRepo.GetValidApproved(
			ctx, route.TenantID, route.ID, route.Version, effectivePolicy.PolicyID, effectivePolicy.Version,
		)
		if err != nil {
			return err
		}
		if validApproval == nil {
			return domain.NewDomainError("FORBIDDEN",
				fmt.Sprintf("Tuyến đường '%s' có mức độ rủi ro '%s' và yêu cầu Manager phê duyệt. Chưa có phê duyệt hợp lệ của Quản lý cho phiên bản RouteVersion=%d và PolicyVersion=%d.",
					route.ID, route.RiskLevel.String(), route.Version, effectivePolicy.Version))
		}
	}

	return nil
}

