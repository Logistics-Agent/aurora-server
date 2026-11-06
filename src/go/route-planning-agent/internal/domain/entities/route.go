package entities

import (
	"time"

	"aurora/route-planning-agent/internal/domain/enums"
	"aurora/shared/entity"

	"github.com/google/uuid"
)

// Route thực thể tuyến đường chính trong hệ thống
type Route struct {
	entity.TenantAuditableEntity

	Name                     string                   `gorm:"column:Name;type:varchar(200);not null" json:"name"`
	Description              *string                  `gorm:"column:Description;type:varchar(1000)" json:"description,omitempty"`
	Type                     enums.RouteType          `gorm:"column:Type;type:integer;not null" json:"type"`
	Status                   enums.RouteStatus        `gorm:"column:Status;type:integer;not null" json:"status"`
	RiskLevel                enums.RouteRiskLevel     `gorm:"column:RiskLevel;type:integer;not null" json:"risk_level"`
	GovernanceDecision       enums.GovernanceDecision `gorm:"column:GovernanceDecision;type:integer;not null" json:"governance_decision"`
	LastAssessedAt           *time.Time               `gorm:"column:LastAssessedAt;type:timestamp with time zone" json:"last_assessed_at,omitempty"`
	EstimatedDistanceKm      float64                  `gorm:"column:EstimatedDistanceKm;type:numeric(12,3);not null" json:"estimated_distance_km"`
	EstimatedDurationMinutes int32                    `gorm:"column:EstimatedDurationMinutes;type:integer;not null" json:"estimated_duration_minutes"`
	MaxWeightKg              float64                  `gorm:"column:MaxWeightKg;type:numeric(12,3);not null" json:"max_weight_kg"`
	MaxVolumeM3              float64                  `gorm:"column:MaxVolumeM3;type:numeric(12,3);not null" json:"max_volume_m3"`
	IsAiGenerated            bool                     `gorm:"column:IsAiGenerated;type:boolean;not null" json:"is_ai_generated"`
	OptimizedAt              *time.Time               `gorm:"column:OptimizedAt;type:timestamp with time zone" json:"optimized_at,omitempty"`
	Version                  int32                    `gorm:"column:Version;type:integer;not null;default:1" json:"version"`
	IsDeleted                bool                     `gorm:"column:IsDeleted;type:boolean;not null;default:false" json:"is_deleted"`

	Stops                 []RouteStop                `gorm:"foreignKey:RouteID;constraint:OnDelete:CASCADE" json:"stops,omitempty"`
	OptimizationHistories []RouteOptimizationHistory `gorm:"foreignKey:RouteID" json:"optimization_histories,omitempty"`
}

func (Route) TableName() string {
	return `"Routes"`
}

// RouteStop điểm dừng trong hành trình của tuyến đường
type RouteStop struct {
	ID                     uuid.UUID       `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
	RouteID                uuid.UUID       `gorm:"column:RouteId;type:uuid;not null;index" json:"route_id"`
	Sequence               int32           `gorm:"column:Sequence;type:integer;not null" json:"sequence"`
	StopType               enums.StopType  `gorm:"column:StopType;type:integer;not null" json:"stop_type"`
	LocationName           string          `gorm:"column:LocationName;type:varchar(200);not null" json:"location_name"`
	Address                string          `gorm:"column:Address;type:varchar(500);not null" json:"address"`
	Latitude               float64         `gorm:"column:Latitude;type:double precision;not null" json:"latitude"`
	Longitude              float64         `gorm:"column:Longitude;type:double precision;not null" json:"longitude"`
	EstimatedArrivalMinutes int32           `gorm:"column:EstimatedArrivalMinutes;type:integer;not null" json:"estimated_arrival_minutes"`
	ServiceDurationMinutes int32           `gorm:"column:ServiceDurationMinutes;type:integer;not null" json:"service_duration_minutes"`
}

func (RouteStop) TableName() string {
	return `"RouteStops"`
}

// RouteOptimizationHistory lịch sử giải thuật tối ưu hóa và mức tiêu thụ token LLM
type RouteOptimizationHistory struct {
	ID                   uuid.UUID `gorm:"column:Id;primaryKey;type:uuid" json:"id"`
	RouteID              uuid.UUID `gorm:"column:RouteId;type:uuid;not null;index" json:"route_id"`
	Provider             string    `gorm:"column:Provider;type:varchar(50);not null" json:"provider"`
	Model                string    `gorm:"column:Model;type:varchar(100);not null" json:"model"`
	PromptVersion        string    `gorm:"column:PromptVersion;type:varchar(20);not null" json:"prompt_version"`
	TotalDistanceKm      float64   `gorm:"column:TotalDistanceKm;type:numeric(12,3);not null" json:"total_distance_km"`
	TotalDurationMinutes int32     `gorm:"column:TotalDurationMinutes;type:integer;not null" json:"total_duration_minutes"`
	InputTokens          int32     `gorm:"column:InputTokens;type:integer;not null" json:"input_tokens"`
	OutputTokens         int32     `gorm:"column:OutputTokens;type:integer;not null" json:"output_tokens"`
	CreatedAt            time.Time `gorm:"column:CreatedAt;type:timestamp with time zone;not null" json:"created_at"`
}

func (RouteOptimizationHistory) TableName() string {
	return `"OptimizationHistories"`
}

