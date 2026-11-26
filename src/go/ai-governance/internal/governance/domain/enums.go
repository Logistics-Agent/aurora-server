package domain

type AiProvider string

const (
	AiProviderGemini      AiProvider = "GEMINI"
	AiProviderAzureOpenAI AiProvider = "AZURE_OPENAI"
)

type AutomationLevel string

const (
	AutomationLevelAssisted             AutomationLevel = "ASSISTED"
	AutomationLevelSemiAutonomous       AutomationLevel = "SEMI_AUTONOMOUS"
	AutomationLevelSupervisedAutonomous AutomationLevel = "SUPERVISED_AUTONOMOUS"
	AutomationLevelFullAutonomous       AutomationLevel = "FULL_AUTONOMOUS"
)

type DenyReason string

const (
	DenyReasonTenantNotFound       DenyReason = "TENANT_NOT_FOUND"
	DenyReasonTenantSuspended      DenyReason = "TENANT_SUSPENDED"
	DenyReasonTenantCancelled      DenyReason = "TENANT_CANCELLED"
	DenyReasonCloudAiDisabled      DenyReason = "CLOUD_AI_DISABLED"
	DenyReasonCapabilityNotAllowed DenyReason = "CAPABILITY_NOT_ALLOWED"
	DenyReasonQuotaExceeded        DenyReason = "QUOTA_EXCEEDED"
	DenyReasonPolicyError          DenyReason = "POLICY_ERROR"
)

type ModelTier string

const (
	ModelTierStandard ModelTier = "STANDARD"
	ModelTierPremium  ModelTier = "PREMIUM"
)

type QuotaMetric string

const (
	QuotaMetricRequests QuotaMetric = "REQUESTS"
	QuotaMetricTokens   QuotaMetric = "TOKENS"
)

type QuotaPeriod string

const (
	QuotaPeriodMinute QuotaPeriod = "MINUTE"
	QuotaPeriodDay    QuotaPeriod = "DAY"
	QuotaPeriodMonth  QuotaPeriod = "MONTH"
)

type TenantStatus string

const (
	TenantStatusActive    TenantStatus = "ACTIVE"
	TenantStatusSuspended TenantStatus = "SUSPENDED"
	TenantStatusCancelled TenantStatus = "CANCELLED"
)
