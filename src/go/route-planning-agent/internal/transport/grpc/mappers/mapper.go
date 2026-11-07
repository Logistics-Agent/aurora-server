package mappers

import (
	"time"

	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/proto/routeplanningpb"
)

func MapRouteToResponse(r *entities.Route) *routeplanningpb.RouteResponse {
	if r == nil {
		return nil
	}

	stops := make([]*routeplanningpb.RouteStopResponse, 0, len(r.Stops))
	for _, s := range r.Stops {
		stops = append(stops, &routeplanningpb.RouteStopResponse{
			Id:                      s.ID.String(),
			Sequence:                s.Sequence,
			StopType:                s.StopType.String(),
			LocationName:            s.LocationName,
			Address:                 s.Address,
			Latitude:                s.Latitude,
			Longitude:               s.Longitude,
			EstimatedArrivalMinutes: s.EstimatedArrivalMinutes,
			ServiceDurationMinutes:  s.ServiceDurationMinutes,
		})
	}

	var desc string
	if r.Description != nil {
		desc = *r.Description
	}

	var optimizedAtStr string
	if r.OptimizedAt != nil {
		optimizedAtStr = r.OptimizedAt.Format(time.RFC3339)
	}

	return &routeplanningpb.RouteResponse{
		Id:                       r.ID.String(),
		TenantId:                 r.TenantID.String(),
		Name:                     r.Name,
		Description:              desc,
		RouteType:                r.Type.String(),
		Status:                   r.Status.String(),
		RiskLevel:                r.RiskLevel.String(),
		EstimatedDistanceKm:      r.EstimatedDistanceKm,
		EstimatedDurationMinutes: r.EstimatedDurationMinutes,
		MaxWeightKg:              r.MaxWeightKg,
		MaxVolumeM3:              r.MaxVolumeM3,
		IsAiGenerated:            r.IsAiGenerated,
		OptimizedAt:              optimizedAtStr,
		Version:                  r.Version,
		CreatedAt:                r.CreatedAt.Format(time.RFC3339),
		Stops:                    stops,
	}
}

func MapApprovalToResponse(a *entities.ApprovalRequest) *routeplanningpb.ApprovalResponse {
	if a == nil {
		return nil
	}

	routeName := ""
	if a.Route != nil {
		routeName = a.Route.Name
	}

	compSummary := ""
	if a.ComplianceSummary != nil {
		compSummary = *a.ComplianceSummary
	}

	rejReason := ""
	if a.RejectionReason != nil {
		rejReason = *a.RejectionReason
	}

	return &routeplanningpb.ApprovalResponse{
		Id:                a.ID.String(),
		RouteId:           a.RouteID.String(),
		RouteName:         routeName,
		Status:            a.Status.String(),
		Reason:            a.Reason,
		AiSummary:         a.AiSummary,
		ComplianceSummary: compSummary,
		CreatedAt:         a.CreatedAt.Format(time.RFC3339),
		RejectionReason:   rejReason,
	}
}

func MapRiskPolicyToResponse(p *entities.TenantRiskPolicy) *routeplanningpb.RiskPolicyResponse {
	if p == nil {
		return nil
	}

	rules := make([]*routeplanningpb.RiskRuleResponse, 0, len(p.Rules))
	for _, r := range p.Rules {
		srcRef := ""
		if r.SourceReference != nil {
			srcRef = *r.SourceReference
		}
		rules = append(rules, &routeplanningpb.RiskRuleResponse{
			Id:              r.ID.String(),
			PolicyId:        r.PolicyID.String(),
			RuleCode:        r.RuleCode,
			RuleName:        r.RuleName,
			ThresholdsJson:  r.ThresholdsJSON,
			RiskEffect:      r.RiskEffect.String(),
			IsEnabled:       r.IsEnabled,
			SourceReference: srcRef,
			CreatedAt:       r.CreatedAt.Format(time.RFC3339),
		})
	}

	desc := ""
	if p.Description != nil {
		desc = *p.Description
	}

	srcDocID := ""
	if p.SourceDocumentID != nil {
		srcDocID = *p.SourceDocumentID
	}

	submittedBy := ""
	if p.SubmittedByUserID != nil {
		submittedBy = p.SubmittedByUserID.String()
	}

	submittedAt := ""
	if p.SubmittedAt != nil {
		submittedAt = p.SubmittedAt.Format(time.RFC3339)
	}

	reviewedBy := ""
	if p.ReviewedByUserID != nil {
		reviewedBy = p.ReviewedByUserID.String()
	}

	reviewedAt := ""
	if p.ReviewedAt != nil {
		reviewedAt = p.ReviewedAt.Format(time.RFC3339)
	}

	reviewerComment := ""
	if p.ReviewerComment != nil {
		reviewerComment = *p.ReviewerComment
	}

	publishedBy := ""
	if p.PublishedByUserID != nil {
		publishedBy = p.PublishedByUserID.String()
	}

	publishedAt := ""
	if p.PublishedAt != nil {
		publishedAt = p.PublishedAt.Format(time.RFC3339)
	}

	rejectionReason := ""
	if p.RejectionReason != nil {
		rejectionReason = *p.RejectionReason
	}

	supersededAt := ""
	if p.SupersededAt != nil {
		supersededAt = p.SupersededAt.Format(time.RFC3339)
	}

	updatedAt := ""
	if p.UpdatedAt != nil {
		updatedAt = p.UpdatedAt.Format(time.RFC3339)
	}

	return &routeplanningpb.RiskPolicyResponse{
		Id:                p.ID.String(),
		TenantId:          p.TenantID.String(),
		Name:              p.Name,
		Description:       desc,
		Scope:             p.Scope,
		Version:           p.Version,
		Status:            p.Status.String(),
		Source:            p.Source.String(),
		SourceDocumentId:  srcDocID,
		SubmittedByUserId: submittedBy,
		SubmittedAt:       submittedAt,
		ReviewedByUserId:  reviewedBy,
		ReviewedAt:        reviewedAt,
		ReviewerComment:   reviewerComment,
		PublishedByUserId: publishedBy,
		PublishedAt:       publishedAt,
		RejectionReason:   rejectionReason,
		SupersededAt:      supersededAt,
		CreatedAt:         p.CreatedAt.Format(time.RFC3339),
		UpdatedAt:         updatedAt,
		Rules:             rules,
	}
}

func MapTenantRuleConfigToResponse(c *entities.TenantRuleConfig, thresholds map[string]float64) *routeplanningpb.TenantRuleConfigResponse {
	if c == nil {
		return nil
	}

	return &routeplanningpb.TenantRuleConfigResponse{
		Id:         c.ID.String(),
		TenantId:   c.TenantID.String(),
		RuleName:   c.RuleName,
		IsEnabled:  c.IsEnabled,
		Thresholds: thresholds,
		UpdatedAt:  c.UpdatedAt.Format(time.RFC3339),
	}
}

