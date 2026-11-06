package enums

import "strings"

// RouteStatus tương ứng RouteStatus trong .NET (0-based)
type RouteStatus int

const (
	RouteStatusDraft RouteStatus = iota
	RouteStatusOptimizing
	RouteStatusReady
	RouteStatusActive
	RouteStatusCompleted
	RouteStatusCancelled
	RouteStatusArchived
)

func (s RouteStatus) String() string {
	switch s {
	case RouteStatusDraft:
		return "Draft"
	case RouteStatusOptimizing:
		return "Optimizing"
	case RouteStatusReady:
		return "Ready"
	case RouteStatusActive:
		return "Active"
	case RouteStatusCompleted:
		return "Completed"
	case RouteStatusCancelled:
		return "Cancelled"
	case RouteStatusArchived:
		return "Archived"
	default:
		return "Draft"
	}
}

func ParseRouteStatus(str string) RouteStatus {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "draft":
		return RouteStatusDraft
	case "optimizing":
		return RouteStatusOptimizing
	case "ready":
		return RouteStatusReady
	case "active":
		return RouteStatusActive
	case "completed":
		return RouteStatusCompleted
	case "cancelled":
		return RouteStatusCancelled
	case "archived":
		return RouteStatusArchived
	default:
		return RouteStatusDraft
	}
}

// RouteType tương ứng RouteType trong .NET
type RouteType int

const (
	RouteTypeFixed RouteType = iota
	RouteTypeFlexible
	RouteTypeOnDemand
)

func (t RouteType) String() string {
	switch t {
	case RouteTypeFixed:
		return "Fixed"
	case RouteTypeFlexible:
		return "Flexible"
	case RouteTypeOnDemand:
		return "OnDemand"
	default:
		return "Fixed"
	}
}

func ParseRouteType(str string) RouteType {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "fixed":
		return RouteTypeFixed
	case "flexible":
		return RouteTypeFlexible
	case "ondemand", "on-demand":
		return RouteTypeOnDemand
	default:
		return RouteTypeFixed
	}
}

// StopType tương ứng StopType trong .NET
type StopType int

const (
	StopTypePickup StopType = iota
	StopTypeDelivery
	StopTypeWarehouse
	StopTypeHub
	StopTypePort
	StopTypeCustoms
)

func (t StopType) String() string {
	switch t {
	case StopTypePickup:
		return "Pickup"
	case StopTypeDelivery:
		return "Delivery"
	case StopTypeWarehouse:
		return "Warehouse"
	case StopTypeHub:
		return "Hub"
	case StopTypePort:
		return "Port"
	case StopTypeCustoms:
		return "Customs"
	default:
		return "Pickup"
	}
}

func ParseStopType(str string) StopType {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "pickup":
		return StopTypePickup
	case "delivery":
		return StopTypeDelivery
	case "warehouse":
		return StopTypeWarehouse
	case "hub":
		return StopTypeHub
	case "port":
		return StopTypePort
	case "customs":
		return StopTypeCustoms
	default:
		return StopTypePickup
	}
}

// RouteRiskLevel tương ứng RouteRiskLevel trong .NET (0 = Low, 1 = Medium, 2 = High, 3 = Critical)
type RouteRiskLevel int

const (
	RouteRiskLevelLow RouteRiskLevel = iota
	RouteRiskLevelMedium
	RouteRiskLevelHigh
	RouteRiskLevelCritical
)

func (r RouteRiskLevel) String() string {
	switch r {
	case RouteRiskLevelLow:
		return "Low"
	case RouteRiskLevelMedium:
		return "Medium"
	case RouteRiskLevelHigh:
		return "High"
	case RouteRiskLevelCritical:
		return "Critical"
	default:
		return "Low"
	}
}

func ParseRouteRiskLevel(str string) RouteRiskLevel {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "low":
		return RouteRiskLevelLow
	case "medium":
		return RouteRiskLevelMedium
	case "high":
		return RouteRiskLevelHigh
	case "critical":
		return RouteRiskLevelCritical
	default:
		return RouteRiskLevelLow
	}
}

// GovernanceDecision tương ứng GovernanceDecision trong .NET
type GovernanceDecision int

const (
	GovernanceDecisionNoApprovalRequired GovernanceDecision = iota
	GovernanceDecisionStaffAllowed
	GovernanceDecisionManagerApprovalRequired
	GovernanceDecisionBlocked
)

func (d GovernanceDecision) String() string {
	switch d {
	case GovernanceDecisionNoApprovalRequired:
		return "NoApprovalRequired"
	case GovernanceDecisionStaffAllowed:
		return "StaffAllowed"
	case GovernanceDecisionManagerApprovalRequired:
		return "ManagerApprovalRequired"
	case GovernanceDecisionBlocked:
		return "Blocked"
	default:
		return "NoApprovalRequired"
	}
}

// ApprovalStatus tương ứng ApprovalStatus trong .NET
type ApprovalStatus int

const (
	ApprovalStatusPending ApprovalStatus = iota
	ApprovalStatusApproved
	ApprovalStatusRejected
	ApprovalStatusExpired
)

func (s ApprovalStatus) String() string {
	switch s {
	case ApprovalStatusPending:
		return "Pending"
	case ApprovalStatusApproved:
		return "Approved"
	case ApprovalStatusRejected:
		return "Rejected"
	case ApprovalStatusExpired:
		return "Expired"
	default:
		return "Pending"
	}
}

// TenantRiskPolicyStatus trong .NET bắt đầu từ 1
type TenantRiskPolicyStatus int

const (
	TenantRiskPolicyStatusDraft TenantRiskPolicyStatus = 1 + iota
	TenantRiskPolicyStatusPendingReview
	TenantRiskPolicyStatusActive
	TenantRiskPolicyStatusSuperseded
	TenantRiskPolicyStatusRejected
)

func (s TenantRiskPolicyStatus) String() string {
	switch s {
	case TenantRiskPolicyStatusDraft:
		return "Draft"
	case TenantRiskPolicyStatusPendingReview:
		return "PendingReview"
	case TenantRiskPolicyStatusActive:
		return "Active"
	case TenantRiskPolicyStatusSuperseded:
		return "Superseded"
	case TenantRiskPolicyStatusRejected:
		return "Rejected"
	default:
		return "Draft"
	}
}

func ParseTenantRiskPolicyStatus(str string) TenantRiskPolicyStatus {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "draft":
		return TenantRiskPolicyStatusDraft
	case "pendingreview", "underreview":
		return TenantRiskPolicyStatusPendingReview
	case "active", "published":
		return TenantRiskPolicyStatusActive
	case "superseded":
		return TenantRiskPolicyStatusSuperseded
	case "rejected":
		return TenantRiskPolicyStatusRejected
	default:
		return TenantRiskPolicyStatusDraft
	}
}

// RiskPolicyMode tương ứng RiskPolicyMode trong .NET
type RiskPolicyMode int

const (
	RiskPolicyModeUsePlatformDefault RiskPolicyMode = iota
	RiskPolicyModeUseCustomPolicy
)

func (m RiskPolicyMode) String() string {
	switch m {
	case RiskPolicyModeUsePlatformDefault:
		return "UsePlatformDefault"
	case RiskPolicyModeUseCustomPolicy:
		return "UseCustomPolicy"
	default:
		return "UsePlatformDefault"
	}
}

// RiskPolicySource tương ứng RiskPolicySource trong .NET
type RiskPolicySource int

const (
	RiskPolicySourcePlatformDefault RiskPolicySource = iota
	RiskPolicySourceTenant
	RiskPolicySourceDocumentOcr
	RiskPolicySourceManual
)

func (s RiskPolicySource) String() string {
	switch s {
	case RiskPolicySourcePlatformDefault:
		return "PlatformDefault"
	case RiskPolicySourceTenant:
		return "Tenant"
	case RiskPolicySourceDocumentOcr:
		return "DocumentOcr"
	case RiskPolicySourceManual:
		return "Manual"
	default:
		return "PlatformDefault"
	}
}

func ParseRiskPolicySource(str string) RiskPolicySource {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "platformdefault":
		return RiskPolicySourcePlatformDefault
	case "tenant":
		return RiskPolicySourceTenant
	case "documentocr":
		return RiskPolicySourceDocumentOcr
	case "manual":
		return RiskPolicySourceManual
	default:
		return RiskPolicySourcePlatformDefault
	}
}

// AutomationPolicy tương ứng AutomationPolicy trong .NET
type AutomationPolicy int

const (
	AutomationPolicyManual AutomationPolicy = iota
	AutomationPolicyRulesOnly
	AutomationPolicyRulesAndLlm
	AutomationPolicyRulesLlmApproval
)

func (a AutomationPolicy) String() string {
	switch a {
	case AutomationPolicyManual:
		return "Manual"
	case AutomationPolicyRulesOnly:
		return "RulesOnly"
	case AutomationPolicyRulesAndLlm:
		return "RulesAndLlm"
	case AutomationPolicyRulesLlmApproval:
		return "RulesLlmApproval"
	default:
		return "Manual"
	}
}

func ParseAutomationPolicy(str string) AutomationPolicy {
	switch strings.ToLower(strings.TrimSpace(str)) {
	case "manual":
		return AutomationPolicyManual
	case "rulesonly":
		return AutomationPolicyRulesOnly
	case "rulesandllm":
		return AutomationPolicyRulesAndLlm
	case "rulesllmapproval":
		return AutomationPolicyRulesLlmApproval
	default:
		return AutomationPolicyManual
	}
}

