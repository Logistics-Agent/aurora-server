package service

import (
	"context"
	"encoding/json"
	"log/slog"
	"time"

	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/proto/commonpb"
	"aurora/ai-governance/proto/iampb"

	"github.com/google/uuid"
	"github.com/redis/go-redis/v9"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

const (
	tenantPlanCachePrefix = "ai-governance:tenant-plan:"
	iamGRPCTimeout        = 5 * time.Second
)

type tenantPlanCacheEntry struct {
	PlanCode       string `json:"planCode"`
	Status         string `json:"status"`
	CloudAiEnabled bool   `json:"cloudAiEnabled"`
}

type IamTenantClient interface {
	GetTenant(ctx context.Context, in *iampb.GetTenantRequest, opts ...grpc.CallOption) (*iampb.TenantResponse, error)
}

type CachedTenantPlanResolver struct {
	redisClient *redis.Client
	iamClient   IamTenantClient
	cacheTTL    time.Duration
	logger      *slog.Logger
}

func NewCachedTenantPlanResolver(
	redisClient *redis.Client,
	iamClient IamTenantClient,
	cacheTTL time.Duration,
	logger *slog.Logger,
) *CachedTenantPlanResolver {
	if logger == nil {
		logger = slog.Default()
	}
	if cacheTTL <= 0 {
		cacheTTL = time.Hour
	}
	return &CachedTenantPlanResolver{
		redisClient: redisClient,
		iamClient:   iamClient,
		cacheTTL:    cacheTTL,
		logger:      logger,
	}
}

func (r *CachedTenantPlanResolver) Resolve(ctx context.Context, tenantID uuid.UUID) domain.TenantPlanResult {
	if tenantID == uuid.Nil {
		return domain.TenantPlanResult{Kind: domain.TenantPlanNotFound, Reason: "Tenant ID is null"}
	}

	cacheKey := tenantPlanCachePrefix + tenantID.String()

	// 1. Try L2 Redis Cache
	if r.redisClient != nil {
		cachedJSON, err := r.redisClient.Get(ctx, cacheKey).Result()
		if err == nil && cachedJSON != "" {
			var entry tenantPlanCacheEntry
			if unmarshalErr := json.Unmarshal([]byte(cachedJSON), &entry); unmarshalErr == nil {
				st := parseTenantStatus(entry.Status)
				r.logger.Debug("TenantPlan cache HIT",
					slog.String("tenantId", tenantID.String()),
					slog.String("planCode", entry.PlanCode),
					slog.String("status", entry.Status))
				if st == domain.TenantStatusSuspended {
					return domain.TenantPlanResult{
						Kind:   domain.TenantPlanSuspended,
						Reason: "Tenant " + tenantID.String() + " is suspended (from cache)",
					}
				}
				return domain.TenantPlanResult{
					Kind: domain.TenantPlanSuccess,
					Info: &domain.TenantPlanInfo{
						TenantID:       tenantID,
						PlanCode:       entry.PlanCode,
						Status:         st,
						CloudAiEnabled: entry.CloudAiEnabled,
					},
				}
			}
		} else if err != nil && err != redis.Nil {
			r.logger.Warn("Redis error reading tenant plan cache; falling back to IamTenant gRPC",
				slog.String("tenantId", tenantID.String()), slog.Any("error", err))
		}
	}

	// 2. Fallback to IamTenant gRPC
	r.logger.Debug("TenantPlan cache MISS, querying IamTenant service", slog.String("tenantId", tenantID.String()))
	return r.fetchFromIamTenant(ctx, tenantID, cacheKey)
}

func (r *CachedTenantPlanResolver) fetchFromIamTenant(ctx context.Context, tenantID uuid.UUID, cacheKey string) domain.TenantPlanResult {
	if r.iamClient == nil {
		r.logger.Error("IamTenant gRPC client not initialized", slog.String("tenantId", tenantID.String()))
		return domain.TenantPlanResult{
			Kind:   domain.TenantPlanIamUnavailable,
			Reason: "IamTenant gRPC client not configured",
		}
	}

	callCtx, cancel := context.WithTimeout(ctx, iamGRPCTimeout)
	defer cancel()

	resp, err := r.iamClient.GetTenant(callCtx, &iampb.GetTenantRequest{
		Identifier: &iampb.GetTenantRequest_Id{Id: tenantID.String()},
	})
	if err != nil {
		if st, ok := status.FromError(err); ok {
			if st.Code() == codes.NotFound || st.Code() == codes.InvalidArgument {
				r.logger.Warn("IamTenant reported tenant not found", slog.String("tenantId", tenantID.String()), slog.String("code", st.Code().String()))
				return domain.TenantPlanResult{
					Kind:   domain.TenantPlanNotFound,
					Reason: "Tenant not found in IamTenant: " + st.Message(),
				}
			}
			r.logger.Error("IamTenant gRPC call failed", slog.String("tenantId", tenantID.String()), slog.String("status", st.Code().String()))
			return domain.TenantPlanResult{
				Kind:   domain.TenantPlanIamUnavailable,
				Reason: "IamTenant gRPC service unavailable: " + st.Message(),
			}
		}
		r.logger.Error("Unexpected error invoking IamTenant gRPC", slog.String("tenantId", tenantID.String()), slog.Any("error", err))
		return domain.TenantPlanResult{
			Kind:   domain.TenantPlanIamUnavailable,
			Reason: "Unexpected error calling IamTenant: " + err.Error(),
		}
	}

	planCode := mapPlanTypeToCode(resp.GetPlanType())
	tenantStatus := mapTenantStatus(resp.GetStatus())
	cloudAiEnabled := tenantStatus == domain.TenantStatusActive

	// 3. Populate Redis Cache
	if r.redisClient != nil {
		entry := tenantPlanCacheEntry{
			PlanCode:       planCode,
			Status:         string(tenantStatus),
			CloudAiEnabled: cloudAiEnabled,
		}
		if payload, err := json.Marshal(entry); err == nil {
			if setErr := r.redisClient.Set(ctx, cacheKey, string(payload), r.cacheTTL).Err(); setErr != nil {
				r.logger.Warn("Failed to write TenantPlan to Redis", slog.String("tenantId", tenantID.String()), slog.Any("error", setErr))
			}
		}
	}

	if tenantStatus == domain.TenantStatusSuspended {
		return domain.TenantPlanResult{
			Kind:   domain.TenantPlanSuspended,
			Reason: "Tenant " + tenantID.String() + " is suspended",
		}
	}

	return domain.TenantPlanResult{
		Kind: domain.TenantPlanSuccess,
		Info: &domain.TenantPlanInfo{
			TenantID:       tenantID,
			PlanCode:       planCode,
			Status:         tenantStatus,
			CloudAiEnabled: cloudAiEnabled,
		},
	}
}

func mapPlanTypeToCode(pt commonpb.PlanType) string {
	switch pt {
	case commonpb.PlanType_ENTERPRISE:
		return "ENTERPRISE"
	default:
		return "STANDARD"
	}
}

func mapTenantStatus(st commonpb.TenantStatus) domain.TenantStatus {
	switch st {
	case commonpb.TenantStatus_TENANT_STATUS_SUSPENDED:
		return domain.TenantStatusSuspended
	default:
		return domain.TenantStatusActive
	}
}

func parseTenantStatus(s string) domain.TenantStatus {
	switch domain.TenantStatus(s) {
	case domain.TenantStatusSuspended:
		return domain.TenantStatusSuspended
	case domain.TenantStatusCancelled:
		return domain.TenantStatusCancelled
	default:
		return domain.TenantStatusActive
	}
}
